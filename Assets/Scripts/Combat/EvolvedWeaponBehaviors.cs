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
            EvolvedWeaponType.ExecutionerMatrix => 0.50f,   // ซิกแซกตัดทะลุทั้งจอ ทุก 0.5s
            EvolvedWeaponType.PlagueSporeSpreader => 0.90f, // ปล่อยแคปซูลเมฆพิษค้างบนพื้นต่อเนื่อง
            EvolvedWeaponType.SupernovaCannon => 0.20f,     // ทำงานต่อเนื่องตลอดเวลาผ่าน SupernovaBeamController
            EvolvedWeaponType.StormCitadel => 0.1667f,      // ฟ้าผ่าสุ่มทั่วจอ 6 ครั้ง/วินาที (1 / 6 = 0.1667s)
            EvolvedWeaponType.SingularityCannon => 4.00f,   // ยิงลำแสงผลักรวมจุดเดียว ระเบิด 1,200 ทุก 4s
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

        GameObject matrixObj = new GameObject("ExecutionerMatrix_ZigZagBlade");
        matrixObj.transform.position = attacker.position;

        ExecutionerMatrixProjectile proj = matrixObj.AddComponent<ExecutionerMatrixProjectile>();
        proj.Initialize(attacker.position, initialDirection, 130f);

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
        Transform nearestEnemy = PlasmaBlasterWeapon.FindNearestEnemy(origin.position, 14f);
        if (nearestEnemy != null)
        {
            targetPos = nearestEnemy.position;
        }

        GameObject cloudObj = new GameObject("PlagueSporeSpreader_Cloud");
        cloudObj.transform.position = targetPos;

        PlagueSporeCloudZone cloud = cloudObj.AddComponent<PlagueSporeCloudZone>();
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        cloud.Initialize(80f, 3.8f * areaMult, 3.5f);

        // ดูด EXP Crystal ทั้งหมดเข้าหาตัวผู้เล่นอัตโนมัติตามสเปกหน้า 4
        EXPCrystal.VacuumAllActiveCrystals();

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
        if (attacker == null) return;

        Vector2 dir = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
        Vector3 collapsePoint = attacker.position + (Vector3)(dir * 4.5f);

        Transform nearestEnemy = PlasmaBlasterWeapon.FindNearestEnemy(attacker.position, 16f);
        if (nearestEnemy != null)
        {
            collapsePoint = nearestEnemy.position;
        }

        GameObject singularityObj = new GameObject("SingularityCannon_BeamCollapse");
        singularityObj.transform.position = collapsePoint;

        SingularityCannonCollapse collapse = singularityObj.AddComponent<SingularityCannonCollapse>();
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        collapse.Initialize(attacker.position, collapsePoint, 1200f, 7.5f * areaMult);

        if (AudioManager.Instance != null && AudioManager.Instance.shootSFX != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.shootSFX);
        }
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
/// 1. Executioner Matrix: คลื่นใบมีดซิกแซกตัดทะลุศัตรูทั้งจอ ดาเมจ 130 และติด Critical 100% ทุกฮิต
/// </summary>
public class ExecutionerMatrixProjectile : MonoBehaviour
{
    private readonly List<Vector3> waypoints = new List<Vector3>();
    private int currentWaypointIndex = 0;
    private float baseDamage = 130f;
    private float speed = 34f;
    private float hitRadius = 1.35f;
    private SpriteRenderer spriteRenderer;
    private readonly HashSet<GameObject> slicedEnemies = new HashSet<GameObject>();

    public void Initialize(Vector3 startPos, Vector2 fallbackDir, float dmg = 130f)
    {
        baseDamage = dmg;
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        hitRadius = 1.35f * areaMult;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.NanoSlash);
        spriteRenderer.sprite = ProceduralSprites.NanoSlash;
        spriteRenderer.color = new Color(0.75f, 0.98f, 1f, 1f);
        spriteRenderer.sortingOrder = 18;
        transform.localScale = new Vector3(2.8f * areaMult, 2.2f * areaMult, 1f);

        BuildZigZagWaypoints(startPos, fallbackDir);

        // ทำดาเมจตัดทะลุศัตรูทุกตัวในหน้าจอทันที + วิ่งซิกแซกผ่านจุดเป้าหมาย
        List<GameObject> screenEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(startPos, 18f);
        for (int i = 0; i < screenEnemies.Count; i++)
        {
            GameObject enemy = screenEnemies[i];
            if (enemy != null && !slicedEnemies.Contains(enemy))
            {
                slicedEnemies.Add(enemy);
                // Dmg 130 + คริติคอล 100% (forceGuaranteedCrit = true)
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, baseDamage, true);
            }
        }
    }

    private void BuildZigZagWaypoints(Vector3 startPos, Vector2 fallbackDir)
    {
        waypoints.Clear();
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(startPos, 16f);

        if (enemies.Count > 0)
        {
            int maxBounce = Mathf.Min(enemies.Count, 8);
            for (int i = 0; i < maxBounce; i++)
            {
                if (enemies[i] != null)
                {
                    waypoints.Add(enemies[i].transform.position);
                }
            }
        }
        else
        {
            Vector2 dir = fallbackDir.sqrMagnitude > 0.001f ? fallbackDir.normalized : Vector2.right;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            waypoints.Add(startPos + (Vector3)(dir * 3.5f + perp * 2.5f));
            waypoints.Add(startPos + (Vector3)(dir * 7.0f - perp * 2.5f));
            waypoints.Add(startPos + (Vector3)(dir * 10.5f + perp * 2.0f));
        }
    }

    private void Update()
    {
        if (currentWaypointIndex >= waypoints.Count)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 target = waypoints[currentWaypointIndex];
        Vector2 toTarget = (Vector2)(target - transform.position);
        if (toTarget.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

        // เช็กศัตรูที่เกิดใหม่หรืออยู่ในเส้นทางซิกแซกเพิ่มเติม
        List<GameObject> alongPath = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, hitRadius);
        for (int i = 0; i < alongPath.Count; i++)
        {
            GameObject enemy = alongPath[i];
            if (enemy != null && !slicedEnemies.Contains(enemy))
            {
                slicedEnemies.Add(enemy);
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, baseDamage, true);
            }
        }

        if (Vector2.Distance(transform.position, target) <= 0.25f)
        {
            currentWaypointIndex++;
        }
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

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Shot);
        spriteRenderer.sprite = ProceduralSprites.Shot;
        spriteRenderer.color = new Color(0.20f, 0.95f, 0.35f, 0.52f);
        spriteRenderer.sortingOrder = 6;
        transform.localScale = Vector3.one * (radius * 1.6f);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        elapsed += dt;
        tickAccumulator += dt;

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
    private const float RotationPeriodSeconds = 2.0f; // หมุนรอบละ 2 วินาที (360° / 2s = 180°/s)
    private const float DamagePerHit = 90f;
    private const float BeamLength = 14f;
    private const float HitCooldownPerEnemy = 0.25f;

    private GameObject beamVisualObj;
    private SpriteRenderer beamRenderer;
    private float currentAngleDeg = 0f;
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
        CreateBeamVisual();
    }

    private void CreateBeamVisual()
    {
        if (beamVisualObj != null) return;

        beamVisualObj = new GameObject("SupernovaCannon_LaserBeam");
        beamVisualObj.transform.SetParent(transform, false);

        beamRenderer = beamVisualObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(beamRenderer, ProceduralSprites.Shot);
        beamRenderer.sprite = ProceduralSprites.Shot;
        beamRenderer.color = new Color(1f, 0.45f, 0.12f, 0.90f);
        beamRenderer.sortingOrder = 17;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        WeaponSlotEntry plasmaSlot = inv != null ? inv.GetWeaponSlot(WeaponType.PlasmaBlaster) : null;
        bool shouldBeActive = plasmaSlot != null && plasmaSlot.isEvolved;

        if (!shouldBeActive)
        {
            if (beamVisualObj != null && beamVisualObj.activeSelf)
                beamVisualObj.SetActive(false);
            return;
        }

        if (beamVisualObj == null) CreateBeamVisual();
        if (!beamVisualObj.activeSelf) beamVisualObj.SetActive(true);

        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        float effectiveLength = BeamLength * areaMult;
        float beamHalfWidth = 0.48f * areaMult;

        // หมุน 360 องศา รอบละ 2 วินาที (180 องศาต่อวินาที)
        float angularSpeedDeg = 360f / RotationPeriodSeconds;
        currentAngleDeg = (currentAngleDeg + angularSpeedDeg * Time.deltaTime) % 360f;

        float rad = currentAngleDeg * Mathf.Deg2Rad;
        Vector2 beamDir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)).normalized;

        // อัปเดตตำแหน่งและขนาดของลำแสงเลเซอร์
        beamVisualObj.transform.position = transform.position + (Vector3)(beamDir * (effectiveLength * 0.5f));
        beamVisualObj.transform.rotation = Quaternion.Euler(0f, 0f, currentAngleDeg);
        beamVisualObj.transform.localScale = new Vector3(effectiveLength, beamHalfWidth * 2.2f, 1f);

        // ตรวจจับศัตรูที่ถูกลำแสงเลเซอร์กวาดผ่าน (Dmg 90/hit)
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, beamHalfWidth, beamDir, effectiveLength);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player")) continue;

            if (enemyNextHitTime.TryGetValue(targetObj, out float nextAllowed) && Time.time < nextAllowed)
                continue;

            IDamageable dmg = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
            EnemyAI ai = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();
            if (!targetObj.CompareTag("Enemy") && dmg == null && ai == null)
                continue;

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
    private const float StrikesPerSecond = 6f;
    private const float StrikeInterval = 1f / StrikesPerSecond; // 0.1667 วินาที (6 ครั้ง/วินาที)
    private const float StrikeDamage = 60f;
    private const float ScreenStrikeRadius = 15f;

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
        if (Time.timeScale <= 0f) return;

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
            List<GameObject> splash = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(strikePos, 1.2f * areaMult);
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
        GameObject boltObj = new GameObject("StormCitadel_LightningStrike");
        boltObj.transform.position = pos;
        boltObj.transform.localScale = new Vector3(0.55f * areaMult, 2.8f * areaMult, 1f);

        SpriteRenderer sr = boltObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = new Color(0.45f, 0.92f, 1f, 0.95f);
        sr.sortingOrder = 19;

        Object.Destroy(boltObj, 0.12f);
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
