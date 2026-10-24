using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บค่าบาลานซ์ของอาวุธ Plasma Blaster (Lv.1 -> Lv.5) ตามตารางหัวข้อ 4.1
/// </summary>
[CreateAssetMenu(fileName = "PlasmaBlasterData", menuName = "RogueLite/Weapons/Plasma Blaster Data")]
public class PlasmaBlasterData : ScriptableObject
{
    [Header("Weapon Icon (2D Sprite)")]
    public Sprite icon;

    [Header("Damage (Lv.1 -> Lv.5)")]
    public float level1Damage = 10f;
    public float level5Damage = 30f;

    [Header("Cooldown / Fire Interval (Lv.1 -> Lv.5)")]
    public float level1Cooldown = 0.30f;
    public float level5Cooldown = 0.20f;

    [Header("Auto-Target & Projectile Settings")]
    public float autoTargetRadius = 18f;
    public float projectileSpeed = 15f;
    public float homingTurnRateDeg = 360f;
    public float projectileLifeTime = 2.5f;

    public float GetDamage(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Damage, level5Damage, t); // Lv.1=10, Lv.2=15, Lv.3=20, Lv.4=25, Lv.5=30
    }

    public float GetCooldown(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Cooldown, level5Cooldown, t); // Lv.1=0.30s -> Lv.5=0.20s
    }
}

/// <summary>
/// Projectile ลูกบอลพลาสม่าของอาวุธ Plasma Blaster:
/// ล็อกเป้าหมายและพุ่งเข้าหาศัตรูที่อยู่ใกล้ที่สุดโดยอัตโนมัติ (Auto-Target Homing)
/// </summary>
public class PlasmaBlasterProjectile : MonoBehaviour
{
    private Transform lockedTarget;
    private Vector2 moveDirection = Vector2.right;
    private float speed = 15f;
    private float turnRateDegPerSec = 360f;
    private float searchRadius = 18f;
    private float baseDamage = 10f;
    private float lifeTime = 2.5f;
    private bool isEvolved = false;

    private float elapsed = 0f;
    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>();

    public void Initialize(
        Transform initialTarget,
        Vector2 initialDirection,
        float damage,
        float projSpeed = 15f,
        float turnRate = 360f,
        float targetSearchRadius = 18f,
        float maxLifeTime = 2.5f,
        bool evolved = false)
    {
        lockedTarget = initialTarget;
        baseDamage = damage;
        speed = projSpeed;
        turnRateDegPerSec = turnRate;
        searchRadius = targetSearchRadius;
        lifeTime = maxLifeTime;
        isEvolved = evolved;

        hitTargets.Clear();
        elapsed = 0f;

        if (lockedTarget != null && lockedTarget.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = (Vector2)(lockedTarget.position - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                moveDirection = toTarget.normalized;
            }
        }
        else if (initialDirection.sqrMagnitude > 0.0001f)
        {
            moveDirection = initialDirection.normalized;
        }
        else
        {
            moveDirection = Vector2.right;
        }

        UpdateRotation();
    }

    private void Update()
    {
        // หากเป้าหมายเดิมตายหรือหายไป ให้ค้นหาศัตรูที่ใกล้ที่สุดตัวใหม่อัตโนมัติ
        if (lockedTarget == null || !lockedTarget.gameObject.activeInHierarchy)
        {
            lockedTarget = PlasmaBlasterWeapon.FindNearestEnemy(transform.position, searchRadius);
        }

        // ปรับทิศทางพุ่งเข้าหาศัตรูที่ล็อกเป้าไว้อย่างแม่นยำ
        if (lockedTarget != null && lockedTarget.gameObject.activeInHierarchy)
        {
            Vector2 toTarget = (Vector2)(lockedTarget.position - transform.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                Vector2 desiredDir = toTarget.normalized;
                float maxRadiansDelta = turnRateDegPerSec * Mathf.Deg2Rad * Time.deltaTime;
                Vector3 rotated = Vector3.RotateTowards(moveDirection, desiredDir, maxRadiansDelta, 0f);
                if (rotated.sqrMagnitude > 0.0001f)
                {
                    moveDirection = ((Vector2)rotated).normalized;
                    UpdateRotation();
                }
            }
        }

        Vector3 prevPos = transform.position;
        Vector3 nextPos = prevPos + (Vector3)(moveDirection * speed * Time.deltaTime);

        CheckHitsAlongSegment(prevPos, nextPos);
        if (this == null || !gameObject.activeInHierarchy) return;

        transform.position = nextPos;

        elapsed += Time.deltaTime;
        if (elapsed >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void UpdateRotation()
    {
        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void CheckHitsAlongSegment(Vector3 startPos, Vector3 endPos)
    {
        float dist = Vector2.Distance(startPos, endPos);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(startPos, 0.22f, moveDirection, Mathf.Max(0.05f, dist));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null)
            {
                if (TryProcessHit(hits[i].collider))
                {
                    return;
                }
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryProcessHit(collision);
    }

    private bool TryProcessHit(Collider2D collision)
    {
        if (collision == null || collision.CompareTag("Player")) return false;

        GameObject targetObj = collision.attachedRigidbody != null ? collision.attachedRigidbody.gameObject : collision.gameObject;
        if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) return false;
        if (hitTargets.Contains(targetObj)) return false;

        IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? collision.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? collision.GetComponent<EnemyAI>();

        if (!collision.CompareTag("Enemy") && damageable == null && enemyAI == null)
            return false;

        hitTargets.Add(targetObj);

        bool isCrit = false;
        float finalDamage = baseDamage;
        if (PlayerStats.Instance != null)
        {
            finalDamage = PlayerStats.Instance.RollCriticalDamage(baseDamage, out isCrit);
        }

        if (damageable != null)
        {
            damageable.TakeDamage(finalDamage);
        }
        else if (enemyAI != null)
        {
            enemyAI.TakeDamage(finalDamage);
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.TryApplyMindShockOnCrit(targetObj, isCrit);
        }

        // กรณีวิวัฒนาการเป็น Supernova Cannon: ระเบิดความเสียหายรอบจุดปะทะ
        if (isEvolved)
        {
            TriggerSupernovaExplosion(targetObj.transform.position, finalDamage * 0.5f, targetObj);
        }

        Destroy(gameObject);
        return true;
    }

    private static void TriggerSupernovaExplosion(Vector3 center, float splashDamage, GameObject primaryTarget)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, 2.2f);
        HashSet<GameObject> splashed = new HashSet<GameObject>();
        if (primaryTarget != null) splashed.Add(primaryTarget);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject obj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (obj == null || !obj.activeInHierarchy || splashed.Contains(obj)) continue;

            IDamageable dmg = obj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? obj.GetComponentInChildren<IDamageable>();
            EnemyAI ai = obj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (dmg == null && ai == null && !col.CompareTag("Enemy")) continue;

            splashed.Add(obj);
            if (dmg != null) dmg.TakeDamage(splashDamage);
            else if (ai != null) ai.TakeDamage(splashDamage);
        }
    }
}

/// <summary>
/// WeaponBehavior ของอาวุธ Plasma Blaster (Auto-Target Homing Plasma Ball):
/// - สเกล Damage (Lv.1 -> Lv.5): 10 -> 30 (Lv.1=10, Lv.2=15, Lv.3=20, Lv.4=25, Lv.5=30)
/// - สเกล Cooldown (Lv.1 -> Lv.5): 0.30s -> 0.20s (Lv.1=0.30s, Lv.2=0.275s, Lv.3=0.25s, Lv.4=0.225s, Lv.5=0.20s)
/// - รองรับการยิงสวนกลับอัตโนมัติจาก Passive Overclock Dash ของ N-03 (Cyber-Swift)
/// </summary>
public class PlasmaBlasterWeapon : MonoBehaviour
{
    [SerializeField] private PlasmaBlasterData weaponData;

    /// <summary>
    /// คำนวณดาเมจพื้นฐานของ Plasma Blaster ตามเลเวล (Lv.1 = 10 -> Lv.5 = 30, Evolved = 90)
    /// </summary>
    public static float GetDamage(int level, bool isEvolved = false, PlasmaBlasterData data = null)
    {
        if (isEvolved) return 90f;
        if (data != null) return data.GetDamage(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(10f, 30f, t); // Lv.1=10, Lv.2=15, Lv.3=20, Lv.4=25, Lv.5=30
    }

    /// <summary>
    /// คำนวณคูลดาวน์ (Fire Interval) ของ Plasma Blaster ตามเลเวล (Lv.1 = 0.30s -> Lv.5 = 0.20s)
    /// </summary>
    public static float GetBaseCooldown(int level, bool isEvolved = false, PlasmaBlasterData data = null)
    {
        if (data != null) return data.GetCooldown(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(0.30f, 0.20f, t); // Lv.1=0.30s -> Lv.5=0.20s
    }

    /// <summary>
    /// ค้นหาศัตรูที่อยู่ใกล้ตำแหน่ง origin ที่สุดภายในระยะ searchRadius
    /// </summary>
    public static Transform FindNearestEnemy(Vector3 origin, float searchRadius = 18f)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, searchRadius);
        Transform closest = null;
        float closestDistSqr = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject candidate = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (candidate == null || !candidate.activeInHierarchy || candidate.CompareTag("Player")) continue;

            Enemies.Core.EnemyHealth enemyHealth = candidate.GetComponent<Enemies.Core.EnemyHealth>() ?? col.GetComponent<Enemies.Core.EnemyHealth>();
            if (enemyHealth != null && enemyHealth.IsDead) continue;

            bool isValidEnemy = candidate.CompareTag("Enemy")
                || candidate.GetComponent<IDamageable>() != null
                || col.GetComponent<IDamageable>() != null
                || candidate.GetComponent<EnemyAI>() != null;

            if (!isValidEnemy) continue;

            float dSqr = ((Vector2)candidate.transform.position - (Vector2)origin).sqrMagnitude;
            if (dSqr < closestDistSqr)
            {
                closestDistSqr = dSqr;
                closest = candidate.transform;
            }
        }

        return closest;
    }

    /// <summary>
    /// ยิงลูกบอลพลาสม่าโดยล็อกเป้าเข้าหาศัตรูที่อยู่ใกล้ที่สุดโดยอัตโนมัติ
    /// </summary>
    public static PlasmaBlasterProjectile Fire(
        Transform fireOrigin,
        Vector2 fallbackDirection,
        int level,
        bool isEvolved = false,
        PlasmaBlasterData data = null)
    {
        if (fireOrigin == null) return null;

        int clampedLevel = Mathf.Clamp(level, 1, 5);
        float damage = GetDamage(clampedLevel, isEvolved, data);
        float searchRadius = data != null ? data.autoTargetRadius : 18f;
        float projSpeed = data != null ? data.projectileSpeed : 15f;
        float turnRate = data != null ? data.homingTurnRateDeg : 360f;
        float lifeTime = data != null ? data.projectileLifeTime : 2.5f;

        Transform nearestTarget = FindNearestEnemy(fireOrigin.position, searchRadius);
        Vector2 launchDir = fallbackDirection.sqrMagnitude > 0.0001f ? fallbackDirection.normalized : Vector2.right;

        if (nearestTarget != null)
        {
            Vector2 toTarget = (Vector2)(nearestTarget.position - fireOrigin.position);
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                launchDir = toTarget.normalized;
            }
        }

        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        GameObject plasmaObj = new GameObject(isEvolved ? "Supernova_PlasmaBall" : $"PlasmaBlaster_Ball_Lv{clampedLevel}");
        plasmaObj.transform.position = fireOrigin.position + (Vector3)(launchDir * 0.35f);
        float scale = (isEvolved ? 1.15f : Mathf.Lerp(0.75f, 0.95f, (clampedLevel - 1) / 4f)) * areaMult;
        plasmaObj.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = plasmaObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = isEvolved
            ? new Color(1f, 0.35f, 0.15f, 1f)
            : new Color(1f, 0.65f, 0.18f, 1f); // สีส้ม-เหลืองพลาสม่าตามเอกสารหน้า 4
        sr.sortingOrder = 14;

        CircleCollider2D col = plasmaObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.22f * areaMult;

        Rigidbody2D rb = plasmaObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        PlasmaBlasterProjectile projectile = plasmaObj.AddComponent<PlasmaBlasterProjectile>();
        projectile.Initialize(nearestTarget, launchDir, damage, projSpeed, turnRate, searchRadius, lifeTime, isEvolved);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }

        return projectile;
    }

    /// <summary>
    /// ยิงกระสุนพลาสม่าสวนกลับ 1 นัดทันทีไปยังศัตรูที่อยู่ใกล้ที่สุดเมื่อเริ่มกด Dash (Passive: Overclock Dash ของ N-03 Cyber-Swift)
    /// </summary>
    public static PlasmaBlasterProjectile FireOverclockDashShot(Transform playerTransform, Vector2 fallbackDirection)
    {
        if (playerTransform == null) return null;

        int plasmaLevel = 1;
        bool isEvolved = false;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            WeaponSlotEntry slot = inv.GetWeaponSlot(WeaponType.PlasmaBlaster);
            if (slot != null)
            {
                plasmaLevel = slot.level;
                isEvolved = slot.isEvolved;
            }
        }

        return Fire(playerTransform, fallbackDirection, plasmaLevel, isEvolved);
    }
}
