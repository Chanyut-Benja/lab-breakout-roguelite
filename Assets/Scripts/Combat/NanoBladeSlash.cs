using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บค่าบาลานซ์และไอคอนของอาวุธ Nano-Blade ตามตารางหัวข้อ 4.1
/// </summary>
[CreateAssetMenu(fileName = "NanoBladeData", menuName = "RogueLite/Weapons/Nano-Blade Data")]
public class NanoBladeData : ScriptableObject
{
    [Header("Weapon Icon (2D Sprite)")]
    public Sprite icon;

    [Header("Direct Damage (Lv.1 = 25 -> Lv.5 = 65, Evolved = 130)")]
    public float level1Damage = 25f;
    public float level5Damage = 65f;
    public float evolvedDamage = 130f;

    [Header("Cooldown (Lv.1 = 0.80s -> Lv.5 = 0.45s, Evolved = 0.50s)")]
    public float level1Cooldown = 0.80f;
    public float level5Cooldown = 0.45f;
    public float evolvedCooldown = 0.50f;
}

/// <summary>
/// สคริปต์การโจมตีของอาวุธ Nano-Blade (และร่าง Evolved: Executioner Matrix)
/// ฟันคลื่นใบมีดพลังงานรูปครึ่งวงกลมระยะประชิด ทำดาเมจ 25 -> 65, ผลักถอย 1.5 -> 3.0 เมตร, คูลดาวน์ 0.8 -> 0.45 วินาที
/// </summary>
public class NanoBladeSlash : MonoBehaviour
{
    [Header("Slash Visual Settings")]
    [SerializeField] private float lifeTime = 0.22f;
    [SerializeField] private float travelSpeed = 6.5f;

    private Vector2 slashDirection = Vector2.right;
    private float elapsed = 0f;
    private Vector3 initialScale = Vector3.one;
    private SpriteRenderer spriteRenderer;
    private Color baseColor = new Color(0.35f, 0.92f, 1f, 0.95f);

    /// <summary>
    /// คำนวณดาเมจพื้นฐานของ Nano-Blade ตามเลเวล (Lv.1 = 25 -> Lv.5 = 65, Evolved = 130)
    /// </summary>
    public static float GetBaseDamage(int level, bool isEvolved = false)
    {
        if (isEvolved) return 130f;
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(25f, 65f, t); // Lv.1=25, Lv.2=35, Lv.3=45, Lv.4=55, Lv.5=65
    }

    /// <summary>
    /// คำนวณคูลดาวน์พื้นฐานของ Nano-Blade ตามเลเวล (Lv.1 = 0.80s -> Lv.5 = 0.45s, Evolved = 0.50s)
    /// </summary>
    public static float GetBaseCooldown(int level, bool isEvolved = false)
    {
        if (isEvolved) return 0.50f;
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(0.80f, 0.45f, t);
    }

    /// <summary>
    /// คำนวณระยะผลักศัตรูถอยหลัง (Knockback) ตามเลเวล (Lv.1 = 1.5m -> Lv.5 = 3.0m) ร่วมกับโบนัส Knockback จาก Skill Tree ของ Vanguard
    /// </summary>
    public static float GetKnockbackDistance(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        float baseKb = Mathf.Lerp(1.5f, 3.0f, t);
        float kbMult = PlayerStats.Instance != null ? PlayerStats.Instance.KnockbackMultiplier : 1f;
        return baseKb * kbMult;
    }

    /// <summary>
    /// คำนวณรัศมีระยะฟันครึ่งวงกลมตามเลเวลและโบนัสขนาดพื้นที่โจมตี (Bio-Fuel Tank)
    /// </summary>
    public static float GetSlashRange(int level, bool isEvolved = false)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        float baseRange = isEvolved ? 4.5f : Mathf.Lerp(2.6f, 3.4f, t);
        float areaMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        return baseRange * areaMultiplier;
    }

    /// <summary>
    /// สร้างคลื่นใบมีด Nano-Blade ฟันครึ่งวงกลมออกไปทางทิศเป้าหมาย ทำดาเมจและผลักศัตรูในขอบเขตทันที
    /// </summary>
    public static NanoBladeSlash PerformSlash(Transform attacker, Vector2 direction, int level, bool isEvolved = false)
    {
        if (attacker == null) return null;

        Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        int clampedLevel = Mathf.Clamp(level, 1, 5);

        float rawDamage = GetBaseDamage(clampedLevel, isEvolved);
        float knockbackMeters = GetKnockbackDistance(clampedLevel);
        float slashRange = GetSlashRange(clampedLevel, isEvolved);

        // 1. ตรวจจับและทำดาเมจ + ผลักศัตรูในขอบเขตครึ่งวงกลม (180 องศาด้านหน้า)
        ExecuteArcHitDetection(attacker.position, dir, slashRange, rawDamage, isEvolved, knockbackMeters);

        // 2. สร้าง Visual คลื่นใบมีดพลังงานสีฟ้า-ขาวที่ใหญ่และสว่างขึ้นตามเลเวล
        GameObject slashObj = new GameObject(isEvolved ? "ExecutionerMatrix_Slash" : $"NanoBlade_Slash_Lv{clampedLevel}");
        slashObj.transform.position = attacker.position + (Vector3)(dir * 0.55f);

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        slashObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        SpriteRenderer sr = slashObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.NanoSlash);
        sr.sprite = ProceduralSprites.NanoSlash;
        sr.sortingOrder = 15;

        NanoBladeSlash slash = slashObj.AddComponent<NanoBladeSlash>();
        slash.SetupVisual(dir, clampedLevel, slashRange, isEvolved, sr);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }

        return slash;
    }

    private static void ExecuteArcHitDetection(
        Vector3 origin,
        Vector2 forwardDir,
        float radius,
        float baseDamage,
        bool forceCrit,
        float knockbackMeters)
    {
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, radius);
        if (colliders == null || colliders.Length == 0) return;

        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || targetObj.CompareTag("Player") || hitTargets.Contains(targetObj))
                continue;

            IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI legacyEnemy = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();

            if (damageable == null && legacyEnemy == null && !col.CompareTag("Enemy"))
                continue;

            Vector2 toEnemy = (Vector2)(targetObj.transform.position - origin);
            float dist = toEnemy.magnitude;

            // เช็กว่าอยู่ในมุมครึ่งวงกลมด้านหน้า (180 องศา หรือ dot >= -0.15f เพื่อให้โดนศัตรูที่ประชิดตัวด้วย)
            Vector2 dirToEnemy = dist > 0.001f ? (toEnemy / dist) : forwardDir;
            float dot = Vector2.Dot(forwardDir, dirToEnemy);
            if (dist > 0.45f && dot < -0.15f)
                continue;

            hitTargets.Add(targetObj);

            bool isCrit = forceCrit;
            float finalDamage = baseDamage;
            if (PlayerStats.Instance != null)
            {
                finalDamage = PlayerStats.Instance.RollCriticalDamage(baseDamage, out isCrit, forceCrit);
            }

            Vector2 knockbackVelocity = dirToEnemy * (knockbackMeters * 4f);

            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage, knockbackVelocity);
            }
            else if (legacyEnemy != null)
            {
                legacyEnemy.TakeDamage(finalDamage, knockbackVelocity);
            }

            // เชื่อมโยง Passive Mind Shock (คริติคอลติด Stun 1.0s)
            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.TryApplyMindShockOnCrit(targetObj, isCrit);
            }

            // ผลักศัตรูถอยหลังตามระยะ 1.5m -> 3.0m อย่างแม่นยำ
            ApplyKnockbackDisplacement(targetObj, dirToEnemy, knockbackMeters);
        }
    }

    private static void ApplyKnockbackDisplacement(GameObject enemyObj, Vector2 pushDir, float knockbackMeters)
    {
        if (enemyObj == null || !enemyObj.activeInHierarchy || knockbackMeters <= 0f) return;

        float resistance = 0f;
        EnemyBase enemyBase = enemyObj.GetComponent<EnemyBase>();
        if (enemyBase != null && enemyBase.Data != null)
        {
            resistance = Mathf.Clamp01(enemyBase.Data.KnockbackResistance);
        }

        float effectiveMeters = knockbackMeters * (1f - resistance);
        if (effectiveMeters <= 0.01f) return;

        Vector3 targetPos = enemyObj.transform.position + (Vector3)(pushDir.normalized * effectiveMeters);

        Rigidbody2D rb = enemyObj.GetComponent<Rigidbody2D>();
        if (rb != null && rb.bodyType != RigidbodyType2D.Static)
        {
            rb.position = targetPos;
        }
        enemyObj.transform.position = targetPos;
    }

    private void SetupVisual(Vector2 dir, int level, float slashRange, bool isEvolved, SpriteRenderer sr)
    {
        slashDirection = dir;
        spriteRenderer = sr;

        // ใบมีดใหญ่และสว่างขึ้นตามเลเวล
        float levelRatio = Mathf.Clamp01((level - 1) / 4f);
        float scaleFactor = Mathf.Lerp(1.85f, 2.85f, levelRatio) * (slashRange / 2.6f);
        if (isEvolved) scaleFactor *= 1.25f;

        initialScale = new Vector3(scaleFactor, scaleFactor * 1.15f, 1f);
        transform.localScale = initialScale;

        baseColor = isEvolved
            ? new Color(0.85f, 0.98f, 1f, 1f)
            : Color.Lerp(new Color(0.30f, 0.85f, 1f, 0.90f), new Color(0.80f, 0.98f, 1f, 1f), levelRatio);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = baseColor;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / lifeTime);

        // พุ่งออกไปด้านหน้าระยะสั้นพร้อมขยายวงและจางหาย
        transform.position += (Vector3)(slashDirection * travelSpeed * (1f - t * 0.5f) * Time.deltaTime);
        transform.localScale = Vector3.Lerp(initialScale * 0.85f, initialScale * 1.18f, t);

        if (spriteRenderer != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - t);
            spriteRenderer.color = c;
        }

        if (elapsed >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
