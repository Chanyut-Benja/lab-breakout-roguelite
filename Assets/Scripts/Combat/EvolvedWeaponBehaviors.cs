using System.Collections;
using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ระบบจัดการพฤติกรรมการโจมตีของอาวุธขั้นสุดยอด (Evolved Weapons) ทั้ง 5 ชนิดตามเอกสารบาลานซ์ หัวข้อ 5 และหน้า 4:
/// 1. Executioner Matrix (Nano-Blade + Targeting Lens): ดาเมจ 130, ติดคริติคอล 100%, ใบมีดซิกแซกตัดทะลุทั้งจอ ทุก 0.5s
/// 2. Plague Spore Spreader (Bio-Needler + Genetic Extractor): เมฆพิษ 80/s กว้าง ค้างบนพื้น ดูด EXP Gem ออโต้
/// 3. Supernova Cannon (Plasma Blaster + Overclock Chip): เลเซอร์กวาด 360 องศาตลอดเวลา หมุนรอบละ 2s ดาเมจ 90/hit
/// 4. Storm Citadel (Tesla Orb + Bio-Fuel Tank): ฟ้าผ่าสุ่มทั่วจอ 6 ครั้ง/วินาที ดาเมจ 60
/// 5. Singularity Cannon (Gravity Grenadier + Graviton Capacitor): ยิงลำแสงผลักรวมจุดเดียว ระเบิด 1,200 ทุก 4s
/// </summary>
public static class EvolvedWeaponBehaviors
{
    public static float GetEvolvedBaseCooldown(EvolvedWeaponType evolvedType)
    {
        return evolvedType switch
        {
            EvolvedWeaponType.ExecutionerMatrix => 0.90f,   // slower, bigger waves (was 0.50); ซิกแซกตัดทะลุทั้งจอ ทุก 0.5s
            EvolvedWeaponType.PlagueSporeSpreader => 0.90f, // ปล่อยแคปซูลเมฆพิษค้างบนพื้นต่อเนื่อง
            EvolvedWeaponType.SupernovaCannon => 0.20f,     // ทำงานต่อเนื่องตลอดเวลาผ่าน SupernovaBeamController
            EvolvedWeaponType.StormCitadel => 0.1667f,      // ฟ้าผ่าสุ่มทั่วจอ 6 ครั้ง/วินาที (1 / 6 = 0.1667s)
            EvolvedWeaponType.SingularityCannon => 3.00f,   // ยิงลำแสงผลักรวมจุดเดียว ระเบิด 1,200 ทุก 4s
            _ => 0.50f
        };
    }

    public static void ExecuteEvolvedAttack(Transform playerTransform, Transform fireOrigin, Vector2 aimDirection, EvolvedWeaponType evolvedType)
    {
        if (playerTransform == null) return;
        Transform origin = fireOrigin != null ? fireOrigin : playerTransform;

        switch (evolvedType)
        {
            case EvolvedWeaponType.ExecutionerMatrix:
                FireExecutionerMatrix(playerTransform, aimDirection);
                break;

            case EvolvedWeaponType.PlagueSporeSpreader:
                SpawnPlagueSporeCloud(origin, aimDirection);
                break;

            case EvolvedWeaponType.SupernovaCannon:
                SupernovaBeamController.EnsureActiveOnPlayer(playerTransform);
                break;

            case EvolvedWeaponType.StormCitadel:
                StormCitadelController.EnsureActiveOnPlayer(playerTransform);
                break;

            case EvolvedWeaponType.SingularityCannon:
                FireSingularityCannon(playerTransform, aimDirection);
                break;
        }
    }

    // ====================================================================================================
    // 1. Executioner Matrix (Nano-Blade + Targeting Lens)
    // Dmg 130, คริติคอล 100%, ซิกแซกตัดทะลุศัตรูทั้งจอ ทุก 0.5s
    // ====================================================================================================
    public static void FireExecutionerMatrix(Transform attacker, Vector2 initialDirection)
    {
        if (attacker == null) return;

        // Two-way slash: one crescent wave along the aim direction and one the opposite way. Each cuts through everything (130, always crit).
        Vector2 dir = initialDirection.sqrMagnitude > 0.0001f ? initialDirection.normalized : Vector2.right;
        float area = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        Color c = new Color(0.75f, 0.98f, 1f, 1f);
        BladeWave.Spawn(attacker.position, dir, 130f, 3.6f * area, 18f, 1.2f, c, true);
        BladeWave.Spawn(attacker.position, -dir, 130f, 3.6f * area, 18f, 1.2f, c, true);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }
    }

    // ====================================================================================================
    // 2. Plague Spore Spreader (Bio-Needler + Genetic Extractor)
    // เมฆพิษ 80/s กว้าง ค้างบนพื้น ดูด EXP Gem ออโต้
    // ====================================================================================================
    public static void SpawnPlagueSporeCloud(Transform origin, Vector2 aimDirection)
    {
        if (origin == null) return;

        Vector3 targetPos = origin.position + (Vector3)(aimDirection.normalized * 2.5f);
        Transform nearestEnemy = AimSettings.MouseAim ? null : PlasmaBlasterWeapon.FindNearestEnemy(origin.position, 14f);
        if (AimSettings.MouseAim)
        {
            Vector2 m = AimSettings.MouseWorld(origin.position);
            Vector2 d = m - (Vector2)origin.position;
            if (d.magnitude > 14f) m = (Vector2)origin.position + d.normalized * 14f;
            targetPos = m;
        }
        if (nearestEnemy != null)
        {
            targetPos = nearestEnemy.position;
        }

        // Fire a spore capsule (like a Bio-Needler dart); it bursts into the poison cloud where it lands.
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        GameObject capsuleObj = new GameObject("PlagueSpore_Capsule");
        capsuleObj.transform.position = origin.position;
        capsuleObj.transform.localScale = new Vector3(1.3f * areaMult, 0.5f * areaMult, 1f);
        SpriteRenderer csr = capsuleObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(csr, ProceduralSprites.Shot);
        csr.sprite = ProceduralSprites.Shot;
        csr.color = new Color(0.45f, 1f, 0.35f, 1f);
        csr.sortingOrder = 14;
        ProjectileFx.Decorate(capsuleObj, new Color(0.45f, 1f, 0.35f, 1f), 2.4f, 0.18f, 0.2f);
        PlagueSporeCapsule capsule = capsuleObj.AddComponent<PlagueSporeCapsule>();
        capsule.Launch(nearestEnemy, targetPos, 3.8f * areaMult);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }
    }

    // ====================================================================================================
    // 5. Singularity Cannon (Gravity Grenadier + Graviton Capacitor)
    // ยิงลำแสงผลักศัตรูรวมจุดเดียว ระเบิด 1,200 ดาเมจ ทุก 4s
    // ====================================================================================================
    public static void FireSingularityCannon(Transform attacker, Vector2 aimDirection)
    {
        // Evolved Gravity: a small black hole is thrown at the nearest enemy (or along the aim) and pulls everything in.
        if (attacker == null) return;
        GravityGrenadierWeapon.Fire(attacker, aimDirection, 5, true);
    }

    // ====================================================================================================
    // Helper: ดึงรายการศัตรูทั้งหมดในรัศมีรอบตำแหน่งที่กำหนด
    // ====================================================================================================
    public static List<GameObject> GetActiveEnemiesInRadius(Vector3 center, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        List<GameObject> enemies = new List<GameObject>();
        HashSet<GameObject> seen = new HashSet<GameObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject obj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (obj == null || !obj.activeInHierarchy || obj.CompareTag("Player") || seen.Contains(obj))
                continue;

            EnemyHealth eh = obj.GetComponent<EnemyHealth>() ?? col.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;

            IDamageable dmg = obj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? obj.GetComponentInChildren<IDamageable>();
            EnemyAI ai = obj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (!obj.CompareTag("Enemy") && dmg == null && ai == null)
                continue;

            seen.Add(obj);
            enemies.Add(obj);
        }

        return enemies;
    }

    public static void DealEvolvedDamageToEnemy(GameObject targetObj, float rawDamage, bool forceGuaranteedCrit = false)
    {
        if (targetObj == null || !targetObj.activeInHierarchy) return;

        IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? targetObj.GetComponentInChildren<EnemyAI>();

        bool isCrit = forceGuaranteedCrit;
        float finalDamage = rawDamage;
        if (PlayerStats.Instance != null)
        {
            finalDamage = PlayerStats.Instance.RollCriticalDamage(rawDamage, out isCrit, forceGuaranteedCrit);
        }
        else if (forceGuaranteedCrit)
        {
            finalDamage = rawDamage * 1.5f;
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
}

/// <summary>
/// Plague Spore capsule: flies to the target like a dart, then bursts into a lingering poison cloud.
/// </summary>
public class PlagueSporeCapsule : MonoBehaviour
{
    private Transform targetEnemy;
    private Vector3 targetPoint;
    private float cloudRadius = 3.8f;
    private float speed = 22f;
    private float life = 2.0f;
    private bool exploded;

    public void Launch(Transform enemy, Vector3 fallbackPoint, float radius)
    {
        targetEnemy = enemy;
        targetPoint = fallbackPoint;
        cloudRadius = radius;
    }

    private void Update()
    {
        if (exploded) return;
        if (targetEnemy != null && targetEnemy.gameObject.activeInHierarchy) targetPoint = targetEnemy.position;

        Vector3 to = targetPoint - transform.position;
        float step = speed * Time.deltaTime;
        if (to.magnitude <= step + 0.3f)
        {
            Explode();
            return;
        }
        Vector3 dir = to.normalized;
        transform.position += dir * step;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        life -= Time.deltaTime;
        if (life <= 0f) Explode();
    }

    private void Explode()
    {
        exploded = true;
        GameObject cloudObj = new GameObject("PlagueSporeSpreader_Cloud");
        cloudObj.transform.position = transform.position;
        PlagueSporeCloudZone cloud = cloudObj.AddComponent<PlagueSporeCloudZone>();
        cloud.Initialize(80f, cloudRadius, 3.5f);
        PoisonSmoke.Burst(transform.position, cloudRadius, 16, 1.0f);
        EXPCrystal.VacuumAllActiveCrystals();
        Destroy(gameObject);
    }
}

/// <summary>
/// 2. Plague Spore Spreader: เมฆพิษกว้างค้างบนพื้น ทำดาเมจ 80/วินาที และดูด EXP Gem เข้าหาผู้เล่นอัตโนมัติ
/// </summary>
public class PlagueSporeCloudZone : MonoBehaviour
{
    private float dps = 80f;
    private float radius = 3.8f;
    private float duration = 3.5f;
    private float elapsed = 0f;
    private float tickAccumulator = 0f;
    private SpriteRenderer spriteRenderer;

    public void Initialize(float cloudDps, float cloudRadius, float cloudDuration)
    {
        dps = cloudDps;
        radius = cloudRadius;
        duration = cloudDuration;

        // soft round smoke instead of the old bar-shaped sprite
        GameObject baseGo = new GameObject("CloudBase");
        baseGo.transform.SetParent(transform, false);
        baseGo.transform.localScale = Vector3.one * (radius * 2.1f);
        spriteRenderer = baseGo.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(spriteRenderer, WeaponVfx.Glow);
        spriteRenderer.sprite = WeaponVfx.Glow;
        spriteRenderer.color = new Color(0.45f, 0.95f, 0.40f, 0.38f);
        spriteRenderer.sortingOrder = 6;
        PoisonSmoke.Burst(transform.position, radius * 0.8f, 8, 1.2f);
    }

    private float puffTimer;

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;
        tickAccumulator += dt;

        puffTimer += dt;
        if (puffTimer >= 0.22f && elapsed < duration - 0.5f)
        {
            puffTimer = 0f;
            PoisonSmoke.Puff(transform.position + (Vector3)(Random.insideUnitCircle * radius * 0.7f), radius * 0.55f, 1.4f);
        }
        if (spriteRenderer != null)
        {
            Color bc = spriteRenderer.color;
            bc.a = 0.38f * Mathf.Clamp01((duration - elapsed) / 0.8f);
            spriteRenderer.color = bc;
        }

        // ดูด EXP Gem ทั้งฉากเข้าหาตัวผู้เล่นอัตโนมัติอย่างต่อเนื่องขณะที่เมฆพิษทำงาน
        EXPCrystal.VacuumAllActiveCrystals();

        if (tickAccumulator >= 0.25f || elapsed >= duration)
        {
            float tickDmg = dps * tickAccumulator;
            tickAccumulator = 0f;

            List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
            for (int i = 0; i < enemies.Count; i++)
            {
                GameObject enemy = enemies[i];
                if (enemy == null) continue;
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, tickDmg, false);
                PlayerStats.ApplyPoisonToTarget(enemy, dps, 2.0f);
            }
        }

        if (elapsed >= duration)
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// 3. Supernova Cannon (Plasma Blaster + Overclock Chip):
/// เลเซอร์พลังงานความร้อนสูงกวาดรอบตัว 360 องศาตลอดเวลา หมุนครบรอบทุก 2 วินาที (180 deg/s) ดาเมจ 90/hit
/// </summary>
public class SupernovaBeamController : MonoBehaviour
{
    private const int Spokes = 6;                     // six rays, 60 degrees apart
    private const float RotationDegPerSec = 70f;      // slow spin of the whole star
    private const float PulsePeriod = 2.4f;           // rays extend out, hold, then fold back in
    private const float DamagePerHit = 90f;
    private const float BeamLength = 8.5f;
    private const float HitCooldownPerEnemy = 0.25f;

    private LineRenderer[] glow;
    private LineRenderer[] core;
    private GameObject root;
    private float angleDeg;
    private float pulseT;
    private readonly Dictionary<GameObject, float> enemyNextHitTime = new Dictionary<GameObject, float>();

    public static SupernovaBeamController EnsureActiveOnPlayer(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        SupernovaBeamController ctrl = playerTransform.GetComponent<SupernovaBeamController>();
        if (ctrl == null)
        {
            ctrl = playerTransform.gameObject.AddComponent<SupernovaBeamController>();
        }
        ctrl.enabled = true;
        return ctrl;
    }

    private void Awake()
    {
        BuildVisual();
    }

    private LineRenderer NewLine(Transform parent, string n, float width, Color c, int order)
    {
        GameObject g = new GameObject(n);
        g.transform.SetParent(parent, false);
        LineRenderer lr = g.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = width;
        lr.endWidth = width * 0.55f;
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, c.a * 0.55f);
        lr.sortingOrder = order;
        return lr;
    }

    private void BuildVisual()
    {
        if (root != null) return;
        root = new GameObject("SupernovaCannon_StarBeams");
        root.transform.SetParent(transform, false);
        glow = new LineRenderer[Spokes];
        core = new LineRenderer[Spokes];
        for (int i = 0; i < Spokes; i++)
        {
            glow[i] = NewLine(root.transform, "Glow" + i, 0.9f, new Color(1f, 0.5f, 0.12f, 0.45f), 16);
            core[i] = NewLine(root.transform, "Core" + i, 0.32f, new Color(1f, 0.95f, 0.75f, 0.98f), 17);
        }
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        if (AimSettings.CombatPaused) { if (root != null && root.activeSelf) root.SetActive(false); return; }

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        WeaponSlotEntry plasmaSlot = inv != null ? inv.GetWeaponSlot(WeaponType.PlasmaBlaster) : null;
        bool shouldBeActive = plasmaSlot != null && plasmaSlot.isEvolved;

        if (!shouldBeActive)
        {
            if (root != null && root.activeSelf) root.SetActive(false);
            return;
        }

        if (root == null) BuildVisual();
        if (!root.activeSelf) root.SetActive(true);

        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        float maxLen = BeamLength * areaMult;
        float halfWidth = 0.4f * areaMult;

        angleDeg = (angleDeg + RotationDegPerSec * Time.deltaTime) % 360f;
        pulseT += Time.deltaTime;
        // 0 -> 1 quickly (shoot out), hold near full, then fold back to 0 and pop out again
        float ph = (pulseT % PulsePeriod) / PulsePeriod;
        float env = ph < 0.25f ? Mathf.SmoothStep(0f, 1f, ph / 0.25f)
                  : ph < 0.65f ? 1f
                  : Mathf.SmoothStep(1f, 0f, (ph - 0.65f) / 0.35f);
        float len = Mathf.Max(0.01f, maxLen * env);

        Vector2 origin = transform.position;
        for (int i = 0; i < Spokes; i++)
        {
            float a = (angleDeg + i * (360f / Spokes)) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector3 end = origin + d * len;
            glow[i].SetPosition(0, origin); glow[i].SetPosition(1, end);
            core[i].SetPosition(0, origin); core[i].SetPosition(1, end);
            glow[i].widthMultiplier = core[i].widthMultiplier = 0.4f + 0.6f * env;
        }

        if (env < 0.12f) return;   // folded in: no damage

        List<GameObject> near = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(origin, len + halfWidth + 1f);
        for (int n = 0; n < near.Count; n++)
        {
            GameObject targetObj = near[n];
            if (targetObj == null || !targetObj.activeInHierarchy) continue;
            if (enemyNextHitTime.TryGetValue(targetObj, out float nextAllowed) && Time.time < nextAllowed) continue;

            Vector2 rel = (Vector2)targetObj.transform.position - origin;
            bool hit = false;
            for (int i = 0; i < Spokes && !hit; i++)
            {
                float a = (angleDeg + i * (360f / Spokes)) * Mathf.Deg2Rad;
                Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float along = Vector2.Dot(rel, d);
                float perp = Mathf.Abs(rel.x * d.y - rel.y * d.x);
                hit = along > -0.3f && along < len + 0.3f && perp < halfWidth + 0.35f;
            }
            if (!hit) continue;

            enemyNextHitTime[targetObj] = Time.time + HitCooldownPerEnemy;
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(targetObj, DamagePerHit, false);
        }
    }
}

/// <summary>
/// 4. Storm Citadel (Tesla Orb + Bio-Fuel Tank):
/// ปล่อยสายฟ้าผ่าสุ่มทั่วจอ 6 ครั้ง/วินาที (ทุก 0.1667s) สร้างความเสียหาย 60 ต่อครั้ง
/// </summary>
public class StormCitadelController : MonoBehaviour
{
    private const float StrikesPerSecond = 3f;
    private const float StrikeInterval = 1f / StrikesPerSecond; // 3 strikes per second (nerfed from 6)
    private const float StrikeDamage = 45f;
    private const float ScreenStrikeRadius = 11f;

    // Storm Citadel is now the orbiting-orbs evolution (TeslaOrbWeapon); the random sky strikes are switched off.
    private static readonly bool StrikesEnabled = false;
    private float strikeTimer = 0f;

    public static StormCitadelController EnsureActiveOnPlayer(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        StormCitadelController ctrl = playerTransform.GetComponent<StormCitadelController>();
        if (ctrl == null)
        {
            ctrl = playerTransform.gameObject.AddComponent<StormCitadelController>();
        }
        ctrl.enabled = true;
        return ctrl;
    }

    private void Update()
    {
        if (!StrikesEnabled) return;
        if (Time.timeScale <= 0f) return;

        if (AimSettings.CombatPaused) return;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        WeaponSlotEntry teslaSlot = inv != null ? inv.GetWeaponSlot(WeaponType.TeslaOrb) : null;
        if (teslaSlot == null || !teslaSlot.isEvolved) return;

        strikeTimer += Time.deltaTime;
        while (strikeTimer >= StrikeInterval)
        {
            strikeTimer -= StrikeInterval;
            TriggerRandomLightningStrike();
        }
    }

    private void TriggerRandomLightningStrike()
    {
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        List<GameObject> candidates = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, ScreenStrikeRadius * areaMult);

        Vector3 strikePos;
        if (candidates.Count > 0)
        {
            GameObject chosen = candidates[Random.Range(0, candidates.Count)];
            strikePos = chosen.transform.position;
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(chosen, StrikeDamage, false);

            // ช็อตศัตรูข้างเคียงในจุดที่สายฟ้าลงระยะ 1.2m
            List<GameObject> splash = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(strikePos, 1.0f * areaMult);
            for (int i = 0; i < splash.Count; i++)
            {
                if (splash[i] != null && splash[i] != chosen)
                {
                    EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(splash[i], StrikeDamage, false);
                }
            }
        }
        else
        {
            Vector2 randomOffset = Random.insideUnitCircle * 8f;
            strikePos = transform.position + new Vector3(randomOffset.x, randomOffset.y, 0f);
        }

        SpawnLightningBoltVisual(strikePos, areaMult);
    }

    private static void SpawnLightningBoltVisual(Vector3 pos, float areaMult)
    {
        Vector2 top = new Vector2(pos.x + Random.Range(-0.6f, 0.6f), pos.y + 6f);
        WeaponVfx.Lightning(top, pos, new Color(0.6f, 0.95f, 1f, 1f), 0.16f, 0.14f, 1);
        WeaponVfx.Flash(pos, new Color(0.7f, 1f, 1f, 0.9f), 1.3f * areaMult, 0.14f);
    }
}

/// <summary>
/// 5. Singularity Cannon (Gravity Grenadier + Graviton Capacitor):
/// ยิงลำแสงแรงโน้มถ่วงผลัก/รวบศัตรูมารวมกันที่จุดเดียว แล้วระเบิดทำดาเมจ 1,200 ทุก 4 วินาที
/// </summary>
public class SingularityCannonCollapse : MonoBehaviour
{
    private Vector3 originPos;
    private Vector3 collapseCenter;
    private float explosionDamage = 1200f;
    private float pullRadius = 7.5f;
    private float collapseDuration = 0.65f;
    private float elapsed = 0f;
    private bool hasExploded = false;
    private SpriteRenderer coreRenderer;

    public void Initialize(Vector3 shooterOrigin, Vector3 targetCenter, float damage = 1200f, float radius = 7.5f)
    {
        originPos = shooterOrigin;
        collapseCenter = targetCenter;
        explosionDamage = damage;
        pullRadius = radius;
        elapsed = 0f;
        hasExploded = false;

        coreRenderer = gameObject.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(coreRenderer, ProceduralSprites.Shot);
        coreRenderer.sprite = ProceduralSprites.Shot;
        coreRenderer.color = new Color(0.55f, 0.15f, 1.0f, 0.92f);
        coreRenderer.sortingOrder = 16;
        transform.localScale = Vector3.one * (pullRadius * 0.8f);

        // Resonance: Event Horizon Void -> หลุมดำหยุดเวลาศัตรู (Stun) 1.5s เมื่อเริ่มดูด
        if (BoonCombatController.IsEventHorizonVoidResonantActive())
        {
            List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(collapseCenter, pullRadius);
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null)
                {
                    PlayerStats.ApplyStunToTarget(enemies[i], 1.5f);
                }
            }
        }
    }

    private void Update()
    {
        if (hasExploded) return;

        float dt = Time.deltaTime;
        elapsed += dt;

        // ดึงรวบศัตรูทั้งหมดในรัศมีให้มารวมกันที่จุดศูนย์กลางเดียวอย่างรวดเร็ว
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(collapseCenter, pullRadius);
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            Vector3 nextPos = Vector2.MoveTowards(enemy.transform.position, collapseCenter, 18f * dt);
            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = nextPos;
            }
            enemy.transform.position = nextPos;
        }

        // บีบขนาดวงหลุมดำเข้าสู่จุดศูนย์กลางก่อนระเบิด
        float t = Mathf.Clamp01(elapsed / collapseDuration);
        transform.localScale = Vector3.one * Mathf.Lerp(pullRadius * 0.9f, 0.6f, t);

        if (elapsed >= collapseDuration)
        {
            TriggerSingularityDetonation();
        }
    }

    private void TriggerSingularityDetonation()
    {
        if (hasExploded) return;
        hasExploded = true;

        // ระเบิดความเสียหาย 1,200 แก่ศัตรูทั้งหมดที่ถูกรวบเข้ามา
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(collapseCenter, pullRadius);
        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] != null)
            {
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemies[i], explosionDamage, false);
            }
        }

        if (coreRenderer != null)
        {
            coreRenderer.color = new Color(0.92f, 0.70f, 1f, 1f);
            transform.localScale = Vector3.one * (pullRadius * 1.4f);
        }

        Destroy(gameObject, 0.15f);
    }
}
