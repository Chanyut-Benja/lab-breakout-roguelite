using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บค่าบาลานซ์ของอาวุธ Bio-Needler (Lv.1 -> Lv.5) ตามตารางหัวข้อ 4.1
/// </summary>
[CreateAssetMenu(fileName = "BioNeedlerData", menuName = "RogueLite/Weapons/Bio-Needler Data")]
public class BioNeedlerData : ScriptableObject
{
    [Header("Weapon Icon (2D Sprite)")]
    public Sprite icon;

    [Header("Direct Damage (Lv.1 -> Lv.5)")]
    public float level1Damage = 15f;
    public float level5Damage = 45f;

    [Header("Cooldown (Lv.1 -> Lv.5)")]
    public float level1Cooldown = 0.70f;
    public float level5Cooldown = 0.45f;

    [Header("Poison DoT (Lv.1 -> Lv.5)")]
    public float level1PoisonDps = 5f;
    public float level5PoisonDps = 12f;
    public float poisonDuration = 3.0f;

    [Header("Piercing (Lv.1 = 3 -> Lv.5 = 7)")]
    public int level1PierceCount = 3;
    public int level5PierceCount = 7;

    [Header("Projectile Flight Settings")]
    public float projectileSpeed = 16f;
    public float projectileLifeTime = 2.8f;

    public float GetDamage(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Damage, level5Damage, t);
    }

    public float GetCooldown(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Cooldown, level5Cooldown, t);
    }

    public float GetPoisonDps(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1PoisonDps, level5PoisonDps, t);
    }

    public int GetPierceCount(int level)
    {
        int clamped = Mathf.Clamp(level, 1, 5);
        return (level1PierceCount - 1) + clamped; // Lv.1=3, Lv.2=4, Lv.3=5, Lv.4=6, Lv.5=7
    }
}

/// <summary>
/// Projectile กระสุนเข็มพิษของ Bio-Needler:
/// พุ่งเป็นเส้นตรงความเร็วสูง เจาะทะลุศัตรู 3 -> 7 ตัว ทำดาเมจตรง 15 -> 45
/// พร้อมติดสถานะพิษ 5/s -> 12/s นาน 3 วินาที และเชื่อมกับ Passive Mind Shock (คริติคอลติด Stun 1.0s)
/// </summary>
public class BioNeedlerProjectile : MonoBehaviour
{
    private Vector2 moveDirection = Vector2.right;
    private float speed = 16f;
    private float lifeTime = 2.8f;
    private float baseDirectDamage = 15f;
    private float poisonDps = 5f;
    private float poisonDuration = 3.0f;
    private int remainingPierces = 3;
    private bool isEvolved = false;

    private float elapsed = 0f;
    private readonly HashSet<GameObject> piercedTargets = new HashSet<GameObject>();

    public void Initialize(
        Vector2 direction,
        float directDamage,
        float dotDps,
        float dotDuration,
        int pierceLimit,
        float projSpeed = 16f,
        float maxLifeTime = 2.8f,
        bool evolved = false)
    {
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        baseDirectDamage = directDamage;
        poisonDps = dotDps;
        poisonDuration = dotDuration;
        remainingPierces = Mathf.Max(1, pierceLimit);
        speed = projSpeed;
        lifeTime = maxLifeTime;
        isEvolved = evolved;

        piercedTargets.Clear();
        elapsed = 0f;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        Vector3 prevPos = transform.position;
        Vector3 step = (Vector3)(moveDirection * speed * Time.deltaTime);
        Vector3 nextPos = prevPos + step;

        // ตรวจจับการชนตามแนวเส้นทางการพุ่งความเร็วสูงเพื่อไม่ให้ทะลุข้ามศัตรูในเฟรมเดียว
        CheckHitsAlongSegment(prevPos, nextPos);
        if (this == null || !gameObject.activeInHierarchy) return;

        transform.position = nextPos;

        elapsed += Time.deltaTime;
        if (elapsed >= lifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void CheckHitsAlongSegment(Vector3 startPos, Vector3 endPos)
    {
        Vector2 delta = (Vector2)(endPos - startPos);
        float dist = delta.magnitude;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(startPos, 0.22f, moveDirection, Mathf.Max(0.05f, dist));
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != null)
            {
                TryProcessHit(hits[i].collider);
                if (remainingPierces <= 0) return;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (remainingPierces <= 0) return;
        TryProcessHit(collision);
    }

    private void TryProcessHit(Collider2D collision)
    {
        if (collision == null || collision.CompareTag("Player")) return;

        GameObject targetObj = collision.attachedRigidbody != null ? collision.attachedRigidbody.gameObject : collision.gameObject;
        if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) return;
        if (piercedTargets.Contains(targetObj)) return;

        IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? collision.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? collision.GetComponent<EnemyAI>();

        if (!collision.CompareTag("Enemy") && damageable == null && enemyAI == null)
            return;

        piercedTargets.Add(targetObj);

        // 1. คำนวณ Direct Damage ร่วมกับโอกาส Critical Hit
        bool isCrit = false;
        float finalDamage = baseDirectDamage;
        if (PlayerStats.Instance != null)
        {
            finalDamage = PlayerStats.Instance.RollCriticalDamage(baseDirectDamage, out isCrit);
        }

        // 2. ทำความเสียหายตรง (Direct Damage: 15 -> 45)
        if (damageable != null)
        {
            damageable.TakeDamage(finalDamage);
        }
        else if (enemyAI != null)
        {
            enemyAI.TakeDamage(finalDamage);
        }

        // 3. ติดสถานะพิษ (Poison DoT: 5/s -> 12/s เป็นเวลา 3.0 วินาที)
        PlayerStats.ApplyPoisonToTarget(targetObj, poisonDps, poisonDuration);

        // 4. หากติด Critical Hit และเป็นตัวละคร N-02 (Psionic) จะทำงาน Passive Mind Shock: ติด Stun 1.0 วินาที
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.TryApplyMindShockOnCrit(targetObj, isCrit);
        }

        // 5. กรณีที่อาวุธ Evolved เป็น Plague Spore Spreader: ดูด EXP เข้าหาผู้เล่นอัตโนมัติตามเอกสารหน้า 4
        if (isEvolved)
        {
            EXPCrystal.VacuumAllActiveCrystals();
        }

        // 6. หักจำนวนการเจาะทะลุ (Lv.1 ทะลุ 3 ตัว -> Lv.5 ทะลุ 7 ตัว)
        remainingPierces--;
        if (remainingPierces <= 0)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// WeaponBehavior ของอาวุธ Bio-Needler สำหรับคำนวณสเตตัสตามเลเวล (Lv.1 -> Lv.5) และสร้างกระสุนเข็มพิษ
/// </summary>
public class BioNeedlerWeapon : MonoBehaviour
{
    [SerializeField] private BioNeedlerData weaponData;

    /// <summary>
    /// ดาเมจตรงของ Bio-Needler: Lv.1 = 15 -> Lv.5 = 45 (Lv.1=15, Lv.2=22.5, Lv.3=30, Lv.4=37.5, Lv.5=45)
    /// </summary>
    public static float GetDirectDamage(int level, bool isEvolved = false, BioNeedlerData data = null)
    {
        if (isEvolved) return 80f;
        if (data != null) return data.GetDamage(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(15f, 45f, t);
    }

    /// <summary>
    /// ดาเมจพิษต่อวินาที (Poison DoT): Lv.1 = 5/s -> Lv.5 = 12/s นาน 3 วินาที
    /// </summary>
    public static float GetPoisonDps(int level, bool isEvolved = false, BioNeedlerData data = null)
    {
        if (isEvolved) return 80f;
        if (data != null) return data.GetPoisonDps(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(5f, 12f, t);
    }

    /// <summary>
    /// จำนวนศัตรูที่เจาะทะลุได้: Lv.1 = 3, Lv.2 = 4, Lv.3 = 5, Lv.4 = 6, Lv.5 = 7
    /// </summary>
    public static int GetPierceCount(int level, bool isEvolved = false, BioNeedlerData data = null)
    {
        if (isEvolved) return 10;
        if (data != null) return data.GetPierceCount(level);

        int clamped = Mathf.Clamp(level, 1, 5);
        return 2 + clamped; // Lv.1=3, Lv.2=4, Lv.3=5, Lv.4=6, Lv.5=7
    }

    /// <summary>
    /// คูลดาวน์การยิง: Lv.1 = 0.70s -> Lv.5 = 0.45s
    /// </summary>
    public static float GetBaseCooldown(int level, bool isEvolved = false, BioNeedlerData data = null)
    {
        if (data != null) return data.GetCooldown(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(0.70f, 0.45f, t);
    }

    public static BioNeedlerProjectile Fire(
        Transform fireOrigin,
        Vector2 direction,
        int level,
        bool isEvolved = false,
        BioNeedlerData data = null)
    {
        if (fireOrigin == null) return null;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        int clampedLevel = Mathf.Clamp(level, 1, 5);

        float directDmg = GetDirectDamage(clampedLevel, isEvolved, data);
        float poisonDps = GetPoisonDps(clampedLevel, isEvolved, data);
        float poisonDuration = data != null ? data.poisonDuration : 3.0f;
        int pierceCount = GetPierceCount(clampedLevel, isEvolved, data);
        float projSpeed = data != null ? data.projectileSpeed : 16f;
        float lifeTime = data != null ? data.projectileLifeTime : 2.8f;

        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        GameObject needleObj = new GameObject(isEvolved ? "PlagueSpore_Capsule" : $"BioNeedler_Dart_Lv{clampedLevel}");
        needleObj.transform.position = fireOrigin.position + (Vector3)(dir * 0.35f);
        needleObj.transform.localScale = new Vector3(1.15f * areaMult, 0.45f * areaMult, 1f);

        SpriteRenderer sr = needleObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = new Color(0.25f, 1f, 0.45f, 1f); // สีเขียวนีออนตามเอกสารหน้า 4
        sr.sortingOrder = 14;

        CircleCollider2D col = needleObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.22f * areaMult;

        Rigidbody2D rb = needleObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        BioNeedlerProjectile projectile = needleObj.AddComponent<BioNeedlerProjectile>();
        projectile.Initialize(dir, directDmg, poisonDps, poisonDuration, pierceCount, projSpeed, lifeTime, isEvolved);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }

        return projectile;
    }
}
