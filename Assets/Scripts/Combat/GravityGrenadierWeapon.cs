using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บค่าบาลานซ์ของอาวุธ Gravity Grenadier (Lv.1 -> Lv.5) ตามตารางหัวข้อ 4.1
/// </summary>
[CreateAssetMenu(fileName = "GravityGrenadierData", menuName = "RogueLite/Weapons/Gravity Grenadier Data")]
public class GravityGrenadierData : ScriptableObject
{
    [Header("Weapon Icon (2D Sprite)")]
    public Sprite icon;

    [Header("Impact Damage (Lv.1 = 18 -> Lv.5 = 50)")]
    public float level1Damage = 18f;
    public float level5Damage = 50f;

    [Header("Pull / Vortex DoT DPS (Lv.1 = 8/s -> Lv.5 = 20/s)")]
    public float level1PullDps = 8f;
    public float level5PullDps = 20f;

    [Header("Cooldown (Lv.1 = 1.5s -> Lv.5 = 0.8s)")]
    public float level1Cooldown = 1.50f;
    public float level5Cooldown = 0.80f;

    [Header("Black Hole Vortex Settings")]
    public float vortexDuration = 2.0f;
    public float vortexRadius = 2.8f;
    public float vortexPullSpeed = 4.2f;
    public float projectileSpeed = 10f;
    public float maxFlightDistance = 6.5f;

    public float GetImpactDamage(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Damage, level5Damage, t); // Lv.1=18, Lv.2=26, Lv.3=34, Lv.4=42, Lv.5=50
    }

    public float GetPullDps(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1PullDps, level5PullDps, t); // Lv.1=8/s, Lv.2=11/s, Lv.3=14/s, Lv.4=17/s, Lv.5=20/s
    }

    public float GetCooldown(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Cooldown, level5Cooldown, t); // Lv.1=1.50s -> Lv.5=0.80s
    }
}

/// <summary>
/// หลุมดำ (Gravity Vortex / Black Hole) ที่เกิดจากการระเบิดของ Gravity Grenadier:
/// ดูดศัตรูรอบข้างเข้าสู่จุดศูนย์กลางอย่างต่อเนื่อง พร้อมทำความเสียหายดูด 8/s -> 20/s
/// </summary>
public class GravityBlackHoleZone : MonoBehaviour
{
    private float pullDps = 8f;
    private float duration = 2.0f;
    private float radius = 2.8f;
    private float pullSpeed = 4.2f;
    private bool isEvolved = false;

    private float elapsed = 0f;
    private float swirlTimer = 0f;
    private float tickAccumulator = 0f;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseScale = Vector3.one;
    private Transform ringTf;

    public void Initialize(float dps, float zoneDuration, float zoneRadius, float suctionSpeed, bool evolved)
    {
        pullDps = dps;
        duration = Mathf.Max(0.5f, zoneDuration);
        radius = Mathf.Max(1.0f, zoneRadius);
        pullSpeed = Mathf.Max(1.0f, suctionSpeed);
        isEvolved = evolved;

        elapsed = 0f;
        tickAccumulator = 0f;

        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        // mini black hole: faint field disc + dark core + rotating accretion ring
        ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.CircleDisc);
        spriteRenderer.sprite = ProceduralSprites.CircleDisc;
        spriteRenderer.color = isEvolved
            ? new Color(0.35f, 0.06f, 0.65f, 0.28f)
            : new Color(0.50f, 0.20f, 0.85f, 0.24f);
        spriteRenderer.sortingOrder = 6;

        GameObject core = new GameObject("Core");
        core.transform.SetParent(transform, false);
        core.transform.localScale = Vector3.one * 0.12f;
        SpriteRenderer coreSr = core.AddComponent<SpriteRenderer>();
        coreSr.sprite = ProceduralSprites.CircleDisc;
        coreSr.color = new Color(0.02f, 0f, 0.05f, 0.97f);
        coreSr.sortingOrder = 8;

        GameObject ring = new GameObject("Ring");
        ring.transform.SetParent(transform, false);
        ring.transform.localScale = Vector3.one * 0.55f;
        ringTf = ring.transform;
        SpriteRenderer ringSr = ring.AddComponent<SpriteRenderer>();
        ringSr.sprite = ProceduralSprites.CircleRing;
        ringSr.color = isEvolved ? new Color(0.85f, 0.35f, 1f, 0.95f) : new Color(0.75f, 0.45f, 1f, 0.9f);
        ringSr.sortingOrder = 9;

        GameObject edge = new GameObject("Edge");
        edge.transform.SetParent(transform, false);
        edge.transform.localScale = Vector3.one * 1.0f;
        SpriteRenderer edgeSr = edge.AddComponent<SpriteRenderer>();
        edgeSr.sprite = ProceduralSprites.CircleRing;
        edgeSr.color = new Color(0.6f, 0.3f, 1f, 0.35f);
        edgeSr.sortingOrder = 7;

        baseScale = Vector3.one * (radius * 2f);
        transform.localScale = baseScale * 0.4f;

        // Resonance: Event Horizon Void (Gravity Grenadier Evolved + Magnetic Pulse Lv.2)
        // หลุมดำหยุดเวลาศัตรู (Stun) 1.5s เมื่อเริ่มดูด
        if (BoonCombatController.IsEventHorizonVoidResonantActive())
        {
            ApplyEventHorizonVoidInitialStun(1.5f);
        }
    }

    private void ApplyEventHorizonVoidInitialStun(float stunDuration)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius * 1.2f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) continue;

            PlayerStats.ApplyStunToTarget(targetObj, stunDuration);
        }
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;
        tickAccumulator += dt;

        // เอฟเฟกต์หมุนวนหลุมดำและขยายขนาด
        if (ringTf != null) ringTf.Rotate(0f, 0f, -300f * dt);
        float lifeProgress = Mathf.Clamp01(elapsed / duration);
        float scaleCurve = lifeProgress < 0.15f
            ? Mathf.Lerp(0.4f, 1.0f, lifeProgress / 0.15f)
            : (lifeProgress > 0.85f ? Mathf.Lerp(1.0f, 0.2f, (lifeProgress - 0.85f) / 0.15f) : 1.0f);
        transform.localScale = baseScale * scaleCurve;

        // ดูดศัตรูเข้าหาศูนย์กลางทุกเฟรม
        PullEnemiesTowardCenter(dt);

        swirlTimer -= dt;
        if (swirlTimer <= 0f)
        {
            swirlTimer = 0.05f;
            float a = Random.value * Mathf.PI * 2f;
            Vector2 offset = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            Vector2 inward = -offset.normalized * 5f + new Vector2(-offset.y, offset.x).normalized * 2.5f;
            WeaponVfx.Particle((Vector2)transform.position + offset, inward, new Color(0.7f, 0.4f, 1f, 0.85f), 0.45f, 0.14f, 0f, 0f, WeaponVfx.Circle);
        }

        // ทำความเสียหายดูดต่อเนื่อง (8/s -> 20/s) ทุก 0.25 วินาที
        if (tickAccumulator >= 0.25f || elapsed >= duration)
        {
            float tickDamage = pullDps * tickAccumulator;
            tickAccumulator = 0f;
            DealVortexTickDamage(tickDamage);
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void PullEnemiesTowardCenter(float dt)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) continue;

            IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (!targetObj.CompareTag("Enemy") && damageable == null && enemyAI == null) continue;

            Vector2 toCenter = (Vector2)(transform.position - targetObj.transform.position);
            float dist = toCenter.magnitude;
            if (dist <= 0.18f) continue;

            float resistance = 0f;
            EnemyBase enemyBase = targetObj.GetComponent<EnemyBase>();
            if (enemyBase != null && enemyBase.Data != null)
            {
                resistance = Mathf.Clamp01(enemyBase.Data.KnockbackResistance * 0.6f);
            }

            float step = pullSpeed * (1f - resistance) * dt;
            Vector3 nextPos = Vector2.MoveTowards(targetObj.transform.position, transform.position, step);

            Rigidbody2D rb = targetObj.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = nextPos;
            }
            targetObj.transform.position = nextPos;
        }
    }

    private void DealVortexTickDamage(float tickDamage)
    {
        if (tickDamage <= 0f) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radius);
        HashSet<GameObject> damaged = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player") || damaged.Contains(targetObj))
                continue;

            IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (!targetObj.CompareTag("Enemy") && damageable == null && enemyAI == null)
                continue;

            damaged.Add(targetObj);

            bool isCrit = false;
            float finalTickDmg = tickDamage;
            if (PlayerStats.Instance != null)
            {
                finalTickDmg = PlayerStats.Instance.RollCriticalDamage(tickDamage, out isCrit);
            }

            if (damageable != null)
            {
                damageable.TakeDamage(finalTickDmg);
            }
            else if (enemyAI != null)
            {
                enemyAI.TakeDamage(finalTickDmg);
            }

            if (PlayerStats.Instance != null)
            {
                PlayerStats.Instance.TryApplyMindShockOnCrit(targetObj, isCrit);
            }
        }
    }
}

/// <summary>
/// ลูกระเบิดแรงโน้มถ่วงที่ยิงออกจาก Gravity Grenadier:
/// พุ่งไปยังทิศทางเป้าหมาย เมื่อชนศัตรูหรือถึงระยะสูงสุดจะระเบิดสร้างความเสียหายทันที (18 -> 50)
/// และเปิดหลุมดำดูดศัตรูเข้ากลางพร้อมทำดาเมจต่อเนื่อง (8/s -> 20/s)
/// </summary>
public class GravityGrenadeProjectile : MonoBehaviour
{
    private Vector2 moveDirection = Vector2.right;
    private Vector3 startPosition;
    private float speed = 10f;
    private float maxTravelDistance = 6.5f;
    private float impactDamage = 18f;
    private float pullDps = 8f;
    private float vortexDuration = 2.0f;
    private float vortexRadius = 2.8f;
    private float vortexPullSpeed = 4.2f;
    private bool isEvolved = false;
    private bool hasDetonated = false;

    public void Initialize(
        Vector2 direction,
        float directDamage,
        float dotPullDps,
        float duration,
        float radius,
        float suctionSpeed,
        float projSpeed = 10f,
        float maxDist = 6.5f,
        bool evolved = false)
    {
        moveDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        startPosition = transform.position;
        impactDamage = directDamage;
        pullDps = dotPullDps;
        vortexDuration = duration;
        vortexRadius = radius;
        vortexPullSpeed = suctionSpeed;
        speed = projSpeed;
        maxTravelDistance = maxDist;
        isEvolved = evolved;
        hasDetonated = false;

        float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (hasDetonated) return;

        Vector3 prevPos = transform.position;
        Vector3 nextPos = prevPos + (Vector3)(moveDirection * speed * Time.deltaTime);

        // เช็กการชนระหว่างเคลื่อนที่
        float stepDist = Vector2.Distance(prevPos, nextPos);
        RaycastHit2D[] hits = Physics2D.CircleCastAll(prevPos, 0.25f, moveDirection, Mathf.Max(0.05f, stepDist));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col != null && IsValidEnemyCollider(col))
            {
                transform.position = hits[i].point;
                Detonate();
                return;
            }
        }

        transform.position = nextPos;

        if (Vector2.Distance(startPosition, transform.position) >= maxTravelDistance)
        {
            Detonate();
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasDetonated) return;
        if (IsValidEnemyCollider(collision))
        {
            Detonate();
        }
    }

    private bool IsValidEnemyCollider(Collider2D col)
    {
        if (col == null || col.CompareTag("Player")) return false;
        GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
        if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) return false;

        return targetObj.CompareTag("Enemy")
            || targetObj.GetComponent<IDamageable>() != null
            || col.GetComponent<IDamageable>() != null
            || targetObj.GetComponent<EnemyAI>() != null;
    }

    private void Detonate()
    {
        if (hasDetonated) return;
        hasDetonated = true;

        // 1. สร้างความเสียหายแรงระเบิดทันที (Impact Damage: 18 -> 50) แก่ศัตรูในรัศมีหลุมดำ
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, vortexRadius);
        HashSet<GameObject> hitTargets = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player") || hitTargets.Contains(targetObj))
                continue;

            IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (!targetObj.CompareTag("Enemy") && damageable == null && enemyAI == null)
                continue;

            hitTargets.Add(targetObj);

            bool isCrit = false;
            float finalDamage = impactDamage;
            if (PlayerStats.Instance != null)
            {
                finalDamage = PlayerStats.Instance.RollCriticalDamage(impactDamage, out isCrit);
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
        }

        // 2. สร้างหลุมดำ (Gravity Black Hole Zone) ดูดศัตรูเข้ากลางและทำดาเมจ 8/s -> 20/s
        GameObject zoneObj = new GameObject(isEvolved ? "Singularity_BlackHole" : "GravityGrenadier_BlackHole");
        zoneObj.transform.position = transform.position;
        GravityBlackHoleZone zone = zoneObj.AddComponent<GravityBlackHoleZone>();
        zone.Initialize(pullDps, vortexDuration, vortexRadius, vortexPullSpeed, isEvolved);

        Destroy(gameObject);
    }
}

/// <summary>
/// WeaponBehavior ของอาวุธ Gravity Grenadier (และร่าง Evolved: Singularity Cannon) ตามตารางบาลานซ์ 4.1:
/// - ยิงลูกบอลสร้างหลุมดำดูดศัตรูเข้ากลาง
/// - Damage: 18 (Lv.1) -> 50 (Lv.5)
/// - ดาเมจดูดต่อเนื่อง: 8/s (Lv.1) -> 20/s (Lv.5)
/// - Cooldown: 1.5s (Lv.1) -> 0.8s (Lv.5)
/// </summary>
public class GravityGrenadierWeapon : MonoBehaviour
{
    [SerializeField] private GravityGrenadierData weaponData;

    /// <summary>
    /// ดาเมจระเบิดของ Gravity Grenadier: Lv.1 = 18 -> Lv.5 = 50 (Lv.1=18, Lv.2=26, Lv.3=34, Lv.4=42, Lv.5=50)
    /// </summary>
    public static float GetImpactDamage(int level, bool isEvolved = false, GravityGrenadierData data = null)
    {
        if (isEvolved) return 60f;   // thrown black hole: impact

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(16f, 42f, t);   // beam damage
    }

    /// <summary>
    /// ดาเมจดูดหลุมดำต่อวินาที: Lv.1 = 8/s -> Lv.5 = 20/s (Lv.1=8, Lv.2=11, Lv.3=14, Lv.4=17, Lv.5=20)
    /// </summary>
    public static float GetPullDps(int level, bool isEvolved = false, GravityGrenadierData data = null)
    {
        if (isEvolved) return 55f;   // black hole DPS
        if (data != null) return data.GetPullDps(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(8f, 20f, t);
    }

    /// <summary>
    /// คูลดาวน์การยิง: Lv.1 = 1.50s -> Lv.5 = 0.80s
    /// </summary>
    public static float GetBaseCooldown(int level, bool isEvolved = false, GravityGrenadierData data = null)
    {
        if (isEvolved) return 3.0f;

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(1.20f, 0.90f, t); // Lv.1=1.50s, Lv.2=1.325s, Lv.3=1.15s, Lv.4=0.975s, Lv.5=0.80s
    }

    public static float GetBeamLength(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(8f, 11f, t);
    }

    /// <summary>Normal Gravity: a straight beam that pierces everything on its line (16 -> 42 damage, 8 -> 11 m).</summary>
    public static void FireBeam(Transform fireOrigin, Vector2 aimDirection, int level)
    {
        if (fireOrigin == null) return;
        float damage = GetImpactDamage(level);
        float length = GetBeamLength(level);
        float area = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        float half = 0.35f * area;

        Vector2 origin = fireOrigin.position;
        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Transform nearest = AimSettings.MouseAim ? null : PlasmaBlasterWeapon.FindNearestEnemy(origin, length);
        if (nearest != null)
        {
            Vector2 to = (Vector2)nearest.position - origin;
            if (to.sqrMagnitude > 0.0001f) dir = to.normalized;
        }
        Vector2 end = origin + dir * length;

        List<Collider2D> found = new List<Collider2D>();
        int n = CombatUtil.QueryEnemies(origin + dir * (length * 0.5f), length * 0.5f + half + 1f, found);
        for (int i = 0; i < n; i++)
        {
            Collider2D c = found[i];
            Vector2 rel = (Vector2)c.bounds.center - origin;
            float along = Vector2.Dot(rel, dir);
            float perp = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
            if (along < -0.3f || along > length + 0.3f || perp > half + 0.35f) continue;

            EnemyBase eb = c.GetComponentInParent<EnemyBase>();
            if (eb == null) continue;
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(eb.gameObject, damage, false);
        }

        // two layers: wide purple glow + thin white core
        WeaponVfx.Beam(origin, end, half * 2.6f, new Color(0.65f, 0.3f, 1f, 0.5f), 0.2f);
        WeaponVfx.Beam(origin, end, half * 0.9f, new Color(0.95f, 0.88f, 1f, 0.95f), 0.14f);
        WeaponVfx.Burst(origin + dir * 0.6f, new Color(0.8f, 0.55f, 1f, 1f), 8, 5f, 0.25f, 0.14f);
        WeaponVfx.Burst(end, new Color(0.8f, 0.55f, 1f, 0.9f), 6, 4f, 0.3f, 0.12f);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }
    }

    public static GravityGrenadeProjectile Fire(
        Transform fireOrigin,
        Vector2 aimDirection,
        int level,
        bool isEvolved = false,
        GravityGrenadierData data = null)
    {
        if (fireOrigin == null) return null;

        if (!isEvolved)
        {
            FireBeam(fireOrigin, aimDirection, level);   // normal Gravity = piercing beam; only the evolved form throws the orb
            return null;
        }

        int clampedLevel = Mathf.Clamp(level, 1, 5);
        float impactDmg = GetImpactDamage(clampedLevel, isEvolved, data);
        float pullDps = GetPullDps(clampedLevel, isEvolved, data);

        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        float baseRadius = isEvolved ? 3.0f : (data != null ? data.vortexRadius : 2.8f);
        float vortexRadius = baseRadius * areaMult;
        float vortexDuration = isEvolved ? 4.0f : (data != null ? data.vortexDuration : 2.0f);
        float pullSpeed = data != null ? data.vortexPullSpeed : (isEvolved ? 6.5f : 4.2f);
        float projSpeed = isEvolved ? 18f : (data != null ? data.projectileSpeed : 10f);
        float maxDist = isEvolved ? 12f : (data != null ? data.maxFlightDistance : 6.5f);

        // ค้นหาศัตรูที่ใกล้ที่สุดเพื่อเล็งลูกบอลหลุมดำใส่กลุ่มศัตรูโดยอัตโนมัติ
        Vector2 launchDir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Transform nearestEnemy = AimSettings.MouseAim ? null : PlasmaBlasterWeapon.FindNearestEnemy(fireOrigin.position, 14f);
        if (AimSettings.MouseAim)
        {
            // thrown to the mouse point
            float toMouse = (AimSettings.MouseWorld(fireOrigin.position) - (Vector2)fireOrigin.position).magnitude;
            maxDist = Mathf.Clamp(toMouse, 1.2f, maxDist);
        }
        if (nearestEnemy != null)
        {
            Vector2 toEnemy = (Vector2)(nearestEnemy.position - fireOrigin.position);
            if (toEnemy.sqrMagnitude > 0.0001f)
            {
                launchDir = toEnemy.normalized;
                maxDist = Mathf.Clamp(toEnemy.magnitude, 1.2f, maxDist);
            }
        }

        GameObject grenadeObj = new GameObject(isEvolved ? "Singularity_Grenade" : $"GravityGrenadier_Orb_Lv{clampedLevel}");
        grenadeObj.transform.position = fireOrigin.position + (Vector3)(launchDir * 0.35f);
        grenadeObj.transform.localScale = Vector3.one * (isEvolved ? 0.75f : 0.6f);

        SpriteRenderer sr = grenadeObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.CircleDisc);
        sr.sprite = ProceduralSprites.CircleDisc;   // a round ball, not the bar-shaped bullet
        sr.color = new Color(0.45f, 0.12f, 0.85f, 1f);
        sr.sortingOrder = 14;
        GameObject halo = new GameObject("Halo");
        halo.transform.SetParent(grenadeObj.transform, false);
        halo.transform.localScale = Vector3.one * 1.6f;
        SpriteRenderer haloSr = halo.AddComponent<SpriteRenderer>();
        haloSr.sprite = ProceduralSprites.CircleRing;
        haloSr.color = new Color(0.85f, 0.55f, 1f, 0.8f);
        haloSr.sortingOrder = 15;

        CircleCollider2D col = grenadeObj.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.25f;

        Rigidbody2D rb = grenadeObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        GravityGrenadeProjectile projectile = grenadeObj.AddComponent<GravityGrenadeProjectile>();
        projectile.Initialize(launchDir, impactDmg, pullDps, vortexDuration, vortexRadius, pullSpeed, projSpeed, maxDist, isEvolved);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }

        return projectile;
    }
}


/// <summary>
/// Resonance: Event Horizon Void. A pentagram seal opens under the collapsing black hole and
/// wipes every ordinary enemy on screen (bosses take heavy damage instead). Fired by BoonCombatController every 25s.
/// </summary>
public class EventHorizonPentagram : MonoBehaviour
{
    private static float lastTrigger = -99f;
    private const float Cooldown = 20f;
    private const float BossDamage = 900f;

    private LineRenderer ring, star;
    private float t;
    private bool killed;
    private Vector3 center;
    private const float Grow = 0.55f, Hold = 0.35f, Fade = 0.7f;
    private const float Radius = 9f;

    public static void Trigger(Vector3 pos)
    {
        if (Time.time - lastTrigger < Cooldown) return;
        lastTrigger = Time.time;
        GameObject go = new GameObject("VFX_EventHorizonPentagram");
        go.transform.position = pos;
        go.AddComponent<EventHorizonPentagram>().center = pos;
    }

    private LineRenderer MakeLine(string n, int count, bool loop, float width)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(transform, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = count;
        lr.startWidth = lr.endWidth = width;
        lr.sortingOrder = 30;
        return lr;
    }

    private void Start()
    {
        const int seg = 64;
        ring = MakeLine("Ring", seg, true, 0.16f);
        for (int i = 0; i < seg; i++)
        {
            float a = i * Mathf.PI * 2f / seg;
            ring.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }
        // five-point star: connect every second vertex
        star = MakeLine("Star", 6, false, 0.14f);
        int[] order = { 0, 2, 4, 1, 3, 0 };
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.PI * 0.5f + order[i] * Mathf.PI * 2f / 5f;
            star.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f));
        }
        transform.localScale = Vector3.one * 0.1f;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float grow = Mathf.Clamp01(t / Grow);
        float ease = 1f - (1f - grow) * (1f - grow);
        transform.localScale = Vector3.one * (Radius * ease);
        transform.Rotate(0f, 0f, 35f * Time.deltaTime);

        float alpha = t < Grow + Hold ? 1f : Mathf.Clamp01(1f - (t - Grow - Hold) / Fade);
        Color c = new Color(0.85f, 0.15f, 1f, alpha);
        Color c2 = new Color(1f, 0.35f, 0.6f, alpha);
        if (ring != null) { ring.startColor = c; ring.endColor = c; }
        if (star != null) { star.startColor = c2; star.endColor = c2; }

        if (!killed && t >= Grow)
        {
            killed = true;
            Wipe();
            WeaponVfx.Burst(center, new Color(0.9f, 0.4f, 1f, 1f), 40, 12f, 0.6f, 0.22f);
        }
        if (t >= Grow + Hold + Fade) Destroy(gameObject);
    }

    private void Wipe()
    {
        List<GameObject> all = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(center, Radius * 1.6f);
        for (int i = 0; i < all.Count; i++)
        {
            GameObject e = all[i];
            if (e == null || !e.activeInHierarchy) continue;
            bool boss = e.GetComponentInParent<Enemies.Bosses.BossBase>() != null;
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(e, boss ? BossDamage : 99999f, false);
        }
    }
}
