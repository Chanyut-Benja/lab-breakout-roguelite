using UnityEngine;

/// <summary>
/// ระบบควบคุมการโจมตีอัตโนมัติ (Auto-Attack Weapon Controller)
/// แทนที่การยิงกระสุนแบบเดิมด้วยคลังอาวุธจาก PlayerInventory (สูงสุด 3 ช่อง)
/// เมื่อเลือก N-01 Vanguard จะใช้ Nano-Blade ฟันคลื่นครึ่งวงกลมแทนการยิงกระสุนปืนปกติ
/// </summary>
public class PlayerManualShooter : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float autoTargetSearchRadius = 14f;

    private readonly float[] slotNextFireTimes = new float[PlayerInventory.MaxWeaponSlots];
    private Camera mainCam;
    private PlayerController playerController;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
    }

    private void Start()
    {
        mainCam = Camera.main;
        if (firePoint == null) firePoint = transform;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            inv.EnsureStarterWeaponInitialized();
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory == null) return;

        inventory.EnsureStarterWeaponInitialized();
        var weapons = inventory.Weapons;
        if (weapons == null || weapons.Count == 0) return;

        Vector2 aimDir = GetPrimaryAimDirection();
        if (aimDir.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        float cooldownReduction = PlayerStats.Instance != null ? PlayerStats.Instance.CooldownReduction : 0f;

        for (int i = 0; i < weapons.Count && i < slotNextFireTimes.Length; i++)
        {
            WeaponSlotEntry slot = weapons[i];
            if (slot == null) continue;

            if (Time.time >= slotNextFireTimes[i])
            {
                float baseCooldown = GetWeaponBaseCooldown(slot.weaponType, slot.level, slot.isEvolved);
                float effectiveCooldown = Mathf.Max(0.08f, baseCooldown * (1f - cooldownReduction));

                ExecuteWeaponAttack(slot, aimDir);
                slotNextFireTimes[i] = Time.time + effectiveCooldown;
            }
        }
    }

    private Vector2 GetPrimaryAimDirection()
    {
        // 1. ค้นหาศัตรูที่อยู่ใกล้ที่สุดในระยะค้นหา
        Transform nearestEnemy = FindNearestEnemy(autoTargetSearchRadius);
        if (nearestEnemy != null)
        {
            Vector2 toEnemy = (Vector2)(nearestEnemy.position - transform.position);
            if (toEnemy.sqrMagnitude > 0.0001f)
            {
                return toEnemy.normalized;
            }
        }

        // 2. หากไม่มีศัตรูในระยะ ให้ใช้ทิศทางที่ผู้เล่นกำลังหัน/เดิน หรือทิศเมาส์
        if (playerController != null && playerController.LastMoveDirection.sqrMagnitude > 0.001f)
        {
            return playerController.LastMoveDirection.normalized;
        }

        if (mainCam == null) mainCam = Camera.main;
        if (mainCam != null)
        {
            Vector3 mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);
            Vector2 toMouse = (Vector2)(mousePos - transform.position);
            if (toMouse.sqrMagnitude > 0.001f)
            {
                return toMouse.normalized;
            }
        }

        return Vector2.right;
    }

    private Transform FindNearestEnemy(float maxRadius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, maxRadius);
        Transform closest = null;
        float closestDistSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (candidate == null || !candidate.activeInHierarchy || candidate == gameObject) continue;

            bool isValidEnemy = candidate.CompareTag("Enemy")
                || candidate.GetComponent<IDamageable>() != null
                || col.GetComponent<IDamageable>() != null
                || candidate.GetComponent<EnemyAI>() != null;

            if (!isValidEnemy) continue;

            float dSqr = ((Vector2)candidate.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (dSqr < closestDistSqr)
            {
                closestDistSqr = dSqr;
                closest = candidate.transform;
            }
        }

        return closest;
    }

    private float GetWeaponBaseCooldown(WeaponType type, int level, bool isEvolved)
    {
        if (isEvolved)
        {
            EvolvedWeaponType evolvedType = PlayerInventory.GetEvolvedWeaponType(type);
            return EvolvedWeaponBehaviors.GetEvolvedBaseCooldown(evolvedType);
        }

        return type switch
        {
            WeaponType.NanoBlade => NanoBladeSlash.GetBaseCooldown(level, false),                  // 0.80s -> 0.45s
            WeaponType.BioNeedler => BioNeedlerWeapon.GetBaseCooldown(level, false),               // 0.70s -> 0.45s
            WeaponType.PlasmaBlaster => PlasmaBlasterWeapon.GetBaseCooldown(level, false),         // 0.30s -> 0.20s
            WeaponType.TeslaOrb => TeslaOrbWeapon.GetBaseCooldown(level, false),                   // 0.60s
            WeaponType.GravityGrenadier => GravityGrenadierWeapon.GetBaseCooldown(level, false),   // 1.50s -> 0.80s
            _ => 0.50f
        };
    }

    private void ExecuteWeaponAttack(WeaponSlotEntry slot, Vector2 aimDirection)
    {
        Transform origin = firePoint != null ? firePoint : transform;

        // หากอาวุธใน Slot วิวัฒนาการ (Evolved) แล้ว ให้แทนที่การโจมตีด้วยร่าง Evolved ทันที
        if (slot.isEvolved)
        {
            EvolvedWeaponType evolvedForm = slot.evolvedForm != EvolvedWeaponType.None
                ? slot.evolvedForm
                : PlayerInventory.GetEvolvedWeaponType(slot.weaponType);

            EvolvedWeaponBehaviors.ExecuteEvolvedAttack(transform, origin, aimDirection, evolvedForm);
            return;
        }

        switch (slot.weaponType)
        {
            case WeaponType.NanoBlade:
                // ฟันคลื่นพลังงานรูปครึ่งวงกลมระยะประชิด (ดาเมจ 25->65, ผลักถอย 1.5->3.0m, คูลดาวน์ 0.8->0.45s)
                NanoBladeSlash.PerformSlash(transform, aimDirection, slot.level, false);
                break;

            case WeaponType.BioNeedler:
                // กระสุนเข็มพิษเจาะทะลุศัตรู (ดาเมจตรง 15->45, พิษ 5->12/s นาน 3s, ทะลุ 3->7 ตัว, คูลดาวน์ 0.7->0.45s)
                BioNeedlerWeapon.Fire(origin, aimDirection, slot.level, false);
                break;

            case WeaponType.PlasmaBlaster:
                // ลูกบอลพลาสม่าล็อกเป้าพุ่งเข้าหาศัตรูที่อยู่ใกล้ที่สุดโดยอัตโนมัติ (ดาเมจ 10->30, คูลดาวน์ 0.3->0.2s)
                PlasmaBlasterWeapon.Fire(origin, aimDirection, slot.level, false);
                break;

            case WeaponType.TeslaOrb:
                // ลูกแก้วไฟฟ้าหมุนรอบตัว รัศมี 2.0m->3.5m ช็อตศัตรูทุก 0.6s (ดาเมจ 20->50, จำนวนลูก Lv.1=1, Lv.3=2, Lv.5=3)
                TeslaOrbWeapon.EnsureActiveOnPlayer(transform, slot.level, false);
                break;

            case WeaponType.GravityGrenadier:
                // ยิงลูกบอลสร้างหลุมดำดูดศัตรูเข้ากลาง (ดาเมจ 18->50, ดูด 8/s->20/s, คูลดาวน์ 1.5->0.8s)
                GravityGrenadierWeapon.Fire(origin, aimDirection, slot.level, false);
                break;
        }
    }

    private void FireWeaponProjectile(Vector2 shootDirection, float baseWeaponDamage, int pierceCount, float speed, Color color)
    {
        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bulletObj;

        if (bulletPrefab != null)
        {
            bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);
        }
        else
        {
            bulletObj = new GameObject("WeaponProjectile");
            bulletObj.transform.position = spawnPos;
            SpriteRenderer sr = bulletObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
            CircleCollider2D col = bulletObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;
            bulletObj.AddComponent<Bullet>();
        }

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Setup(shootDirection, baseWeaponDamage, pierceCount, speed, color);
        }

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }
    }
}
