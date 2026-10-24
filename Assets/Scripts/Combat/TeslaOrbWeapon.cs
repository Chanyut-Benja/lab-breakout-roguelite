using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บค่าบาลานซ์ของอาวุธ Tesla Orb (Lv.1 -> Lv.5) ตามตารางหัวข้อ 4.1
/// </summary>
[CreateAssetMenu(fileName = "TeslaOrbData", menuName = "RogueLite/Weapons/Tesla Orb Data")]
public class TeslaOrbData : ScriptableObject
{
    [Header("Weapon Icon (2D Sprite)")]
    public Sprite icon;

    [Header("Damage (Lv.1 = 20 -> Lv.5 = 50)")]
    public float level1Damage = 20f;
    public float level5Damage = 50f;

    [Header("Orbit Radius (Lv.1 = 2.0m -> Lv.5 = 3.5m)")]
    public float level1OrbitRadius = 2.0f;
    public float level5OrbitRadius = 3.5f;

    [Header("Shock Interval & Rotation")]
    public float shockInterval = 0.60f;
    public float orbitAngularSpeedDeg = 180f;
    public float shockHitRadius = 1.15f;

    public float GetDamage(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1Damage, level5Damage, t); // Lv.1=20, Lv.2=27.5, Lv.3=35, Lv.4=42.5, Lv.5=50
    }

    public float GetOrbitRadius(int level)
    {
        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(level1OrbitRadius, level5OrbitRadius, t); // Lv.1=2.0m -> Lv.5=3.5m
    }

    public int GetOrbCount(int level)
    {
        int clamped = Mathf.Clamp(level, 1, 5);
        if (clamped >= 5) return 3;
        if (clamped >= 3) return 2;
        return 1; // Lv.1-2 = 1 ลูก, Lv.3-4 = 2 ลูก, Lv.5 = 3 ลูก
    }
}

/// <summary>
/// ลูกแก้วไฟฟ้าแต่ละลูกของ Tesla Orb ที่หมุนโคจรรอบตัวผู้เล่นและช็อตศัตรูที่อยู่ในรัศมี
/// </summary>
public class TeslaOrbNode : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float pulsePhase;

    public SpriteRenderer Renderer => spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        ProceduralSprites.EnsureVisible(spriteRenderer, ProceduralSprites.Shot);
        spriteRenderer.sprite = ProceduralSprites.Shot;
        spriteRenderer.sortingOrder = 16;
        pulsePhase = Random.Range(0f, Mathf.PI * 2f);
    }

    public void ConfigureVisual(bool isEvolved, float areaMultiplier)
    {
        if (spriteRenderer == null) return;

        spriteRenderer.color = isEvolved
            ? new Color(0.55f, 0.95f, 1f, 1f)
            : new Color(0.25f, 0.85f, 1f, 0.95f);

        float baseScale = isEvolved ? 0.95f : 0.75f;
        transform.localScale = Vector3.one * (baseScale * Mathf.Clamp(areaMultiplier, 1f, 1.8f));
    }

    private void Update()
    {
        // เอฟเฟกต์กระพริบพลังงานไฟฟ้าและหมุนรอบตัวเอง
        transform.Rotate(0f, 0f, 260f * Time.deltaTime);

        if (spriteRenderer != null)
        {
            float pulse = 0.88f + 0.12f * Mathf.Sin((Time.time * 12f) + pulsePhase);
            Color c = spriteRenderer.color;
            c.a = Mathf.Clamp01(pulse);
            spriteRenderer.color = c;
        }
    }
}

/// <summary>
/// WeaponBehavior ของอาวุธ Tesla Orb (และร่าง Evolved: Storm Citadel) ตามตารางบาลานซ์ 4.1:
/// - ลูกแก้วไฟฟ้าหมุนรอบตัวผู้เล่นที่รัศมี 2.0m (Lv.1) -> 3.5m (Lv.5)
/// - ช็อตศัตรูทุก 0.6 วินาที
/// - Damage: 20 (Lv.1) -> 50 (Lv.5)
/// - จำนวนลูกแก้ว: Lv.1 = 1 ลูก, Lv.3 = 2 ลูก, Lv.5 = 3 ลูก
/// </summary>
public class TeslaOrbWeapon : MonoBehaviour
{
    [SerializeField] private TeslaOrbData weaponData;

    private readonly List<TeslaOrbNode> activeOrbs = new List<TeslaOrbNode>();
    private float currentOrbitAngleDeg = 0f;
    private float shockTimer = 0f;
    private int currentLevel = 1;
    private bool isEvolved = false;
    private bool isActiveWeapon = false;

    /// <summary>
    /// ดาเมจพื้นฐานของ Tesla Orb ตามเลเวล: Lv.1 = 20 -> Lv.5 = 50 (Evolved = 60)
    /// </summary>
    public static float GetDamage(int level, bool evolved = false, TeslaOrbData data = null)
    {
        if (evolved) return 60f;
        if (data != null) return data.GetDamage(level);

        float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
        return Mathf.Lerp(20f, 50f, t); // Lv.1=20, Lv.2=27.5, Lv.3=35, Lv.4=42.5, Lv.5=50
    }

    /// <summary>
    /// รัศมีวงโคจรรอบตัวผู้เล่น: Lv.1 = 2.0m -> Lv.5 = 3.5m
    /// </summary>
    public static float GetOrbitRadius(int level, bool evolved = false, TeslaOrbData data = null)
    {
        float baseRadius;
        if (evolved)
        {
            baseRadius = 4.0f;
        }
        else if (data != null)
        {
            baseRadius = data.GetOrbitRadius(level);
        }
        else
        {
            float t = Mathf.Clamp01((Mathf.Clamp(level, 1, 5) - 1) / 4f);
            baseRadius = Mathf.Lerp(2.0f, 3.5f, t);
        }

        float areaMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        return baseRadius * areaMultiplier;
    }

    /// <summary>
    /// จำนวนลูกแก้วไฟฟ้าตามเลเวล: Lv.1-2 = 1 ลูก, Lv.3-4 = 2 ลูก, Lv.5 = 3 ลูก (Evolved = 4 ลูก)
    /// </summary>
    public static int GetOrbCount(int level, bool evolved = false, TeslaOrbData data = null)
    {
        if (evolved) return 4;
        if (data != null) return data.GetOrbCount(level);

        int clamped = Mathf.Clamp(level, 1, 5);
        if (clamped >= 5) return 3;
        if (clamped >= 3) return 2;
        return 1;
    }

    /// <summary>
    /// คูลดาวน์การช็อตศัตรู: ทุก 0.60 วินาที
    /// </summary>
    public static float GetBaseCooldown(int level = 1, bool evolved = false, TeslaOrbData data = null)
    {
        if (data != null) return data.shockInterval;
        return 0.60f;
    }

    /// <summary>
    /// เรียกใช้จากระบบควบคุมอาวุธเพื่อเปิดใช้งาน/อัปเดตสถานะลูกแก้วหมุนรอบตัวผู้เล่น
    /// </summary>
    public static TeslaOrbWeapon EnsureActiveOnPlayer(Transform playerTransform, int level, bool evolved = false)
    {
        if (playerTransform == null) return null;

        TeslaOrbWeapon controller = playerTransform.GetComponent<TeslaOrbWeapon>();
        if (controller == null)
        {
            controller = playerTransform.gameObject.AddComponent<TeslaOrbWeapon>();
        }

        controller.SyncState(level, evolved);
        return controller;
    }

    public void SyncState(int level, bool evolved)
    {
        isActiveWeapon = true;
        currentLevel = Mathf.Clamp(level, 1, 5);
        isEvolved = evolved;
        RebuildOrbsIfNeeded();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        // ตรวจสอบกับ PlayerInventory เสมอเพื่อให้ลูกแก้วอัปเดตทันทีเมื่อเลเวลอัป
        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            WeaponSlotEntry slot = inv.GetWeaponSlot(WeaponType.TeslaOrb);
            if (slot != null)
            {
                isActiveWeapon = true;
                currentLevel = Mathf.Clamp(slot.level, 1, 5);
                isEvolved = slot.isEvolved;
            }
            else
            {
                isActiveWeapon = false;
            }
        }

        if (!isActiveWeapon || isEvolved)
        {
            SetOrbsActive(false);
            if (isEvolved)
            {
                StormCitadelController.EnsureActiveOnPlayer(transform);
            }
            return;
        }

        RebuildOrbsIfNeeded();
        UpdateOrbitPositions();
        UpdatePeriodicShock();
    }

    private void RebuildOrbsIfNeeded()
    {
        int targetCount = GetOrbCount(currentLevel, isEvolved, weaponData);
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;

        activeOrbs.RemoveAll(o => o == null);

        while (activeOrbs.Count < targetCount)
        {
            int idx = activeOrbs.Count + 1;
            GameObject orbObj = new GameObject($"TeslaOrb_Node_{idx}");
            orbObj.transform.SetParent(transform, false);

            CircleCollider2D col = orbObj.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.35f;

            TeslaOrbNode node = orbObj.AddComponent<TeslaOrbNode>();
            node.ConfigureVisual(isEvolved, areaMult);
            activeOrbs.Add(node);
        }

        for (int i = 0; i < activeOrbs.Count; i++)
        {
            bool shouldBeActive = i < targetCount;
            if (activeOrbs[i].gameObject.activeSelf != shouldBeActive)
            {
                activeOrbs[i].gameObject.SetActive(shouldBeActive);
            }

            if (shouldBeActive)
            {
                activeOrbs[i].ConfigureVisual(isEvolved, areaMult);
            }
        }
    }

    private void SetOrbsActive(bool active)
    {
        for (int i = 0; i < activeOrbs.Count; i++)
        {
            if (activeOrbs[i] != null && activeOrbs[i].gameObject.activeSelf != active)
            {
                activeOrbs[i].gameObject.SetActive(active);
            }
        }
    }

    private void UpdateOrbitPositions()
    {
        int count = GetOrbCount(currentLevel, isEvolved, weaponData);
        if (count <= 0) return;

        float angularSpeed = weaponData != null ? weaponData.orbitAngularSpeedDeg : 180f;
        currentOrbitAngleDeg = (currentOrbitAngleDeg + angularSpeed * Time.deltaTime) % 360f;

        float radius = GetOrbitRadius(currentLevel, isEvolved, weaponData);
        float stepDeg = 360f / count;

        for (int i = 0; i < count && i < activeOrbs.Count; i++)
        {
            TeslaOrbNode orb = activeOrbs[i];
            if (orb == null || !orb.gameObject.activeInHierarchy) continue;

            float angleRad = (currentOrbitAngleDeg + (i * stepDeg)) * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0f) * radius;
            orb.transform.position = transform.position + offset;
        }
    }

    private void UpdatePeriodicShock()
    {
        float baseInterval = GetBaseCooldown(currentLevel, isEvolved, weaponData);
        float cdr = PlayerStats.Instance != null ? PlayerStats.Instance.CooldownReduction : 0f;
        float effectiveInterval = Mathf.Max(0.15f, baseInterval * (1f - cdr));

        shockTimer += Time.deltaTime;
        if (shockTimer >= effectiveInterval)
        {
            shockTimer -= effectiveInterval;
            TriggerOrbShockPulse();
        }
    }

    /// <summary>
    /// ช็อตศัตรูทุกตัวที่อยู่ในรัศมีของลูกแก้วไฟฟ้าแต่ละลูกและตามวงโคจร (ทุก 0.6s)
    /// </summary>
    public void TriggerOrbShockPulse()
    {
        int count = GetOrbCount(currentLevel, isEvolved, weaponData);
        if (count <= 0) return;

        float baseDamage = GetDamage(currentLevel, isEvolved, weaponData);
        float areaMult = PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f;
        float orbShockRadius = (weaponData != null ? weaponData.shockHitRadius : 1.15f) * areaMult;
        float orbitRadius = GetOrbitRadius(currentLevel, isEvolved, weaponData);

        HashSet<GameObject> shockedTargets = new HashSet<GameObject>();

        // 1. ช็อตศัตรูรอบตำแหน่งของลูกแก้วแต่ละลูก
        for (int i = 0; i < count && i < activeOrbs.Count; i++)
        {
            TeslaOrbNode orb = activeOrbs[i];
            if (orb == null || !orb.gameObject.activeInHierarchy) continue;

            ShockEnemiesInCircle(orb.transform.position, orbShockRadius, baseDamage, shockedTargets);
        }

        // 2. ช็อตศัตรูที่สัมผัสแนววงแหวนโคจรไฟฟ้า (รัศมี 2.0m -> 3.5m)
        Collider2D[] ringCandidates = Physics2D.OverlapCircleAll(transform.position, orbitRadius + (0.65f * areaMult));
        for (int i = 0; i < ringCandidates.Length; i++)
        {
            Collider2D col = ringCandidates[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player") || shockedTargets.Contains(targetObj))
                continue;

            float dist = Vector2.Distance(transform.position, targetObj.transform.position);
            // หากเป็นร่าง Evolved (Storm Citadel) หรืออยู่ในแนวรัศมีลูกแก้วไฟฟ้า ให้โดนช็อต
            if (isEvolved || Mathf.Abs(dist - orbitRadius) <= (0.85f * areaMult))
            {
                TryApplyShockDamage(targetObj, col, baseDamage, shockedTargets);
            }
        }
    }

    private void ShockEnemiesInCircle(Vector3 center, float radius, float baseDamage, HashSet<GameObject> shockedTargets)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(center, radius);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i];
            if (col == null || col.CompareTag("Player")) continue;

            GameObject targetObj = col.attachedRigidbody != null ? col.attachedRigidbody.gameObject : col.gameObject;
            if (targetObj == null || !targetObj.activeInHierarchy || targetObj.CompareTag("Player") || shockedTargets.Contains(targetObj))
                continue;

            TryApplyShockDamage(targetObj, col, baseDamage, shockedTargets);
        }
    }

    private void TryApplyShockDamage(GameObject targetObj, Collider2D col, float baseDamage, HashSet<GameObject> shockedTargets)
    {
        IDamageable damageable = targetObj.GetComponent<IDamageable>() ?? col.GetComponent<IDamageable>() ?? targetObj.GetComponentInChildren<IDamageable>();
        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? col.GetComponent<EnemyAI>();

        if (!targetObj.CompareTag("Enemy") && damageable == null && enemyAI == null)
            return;

        shockedTargets.Add(targetObj);

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
    }

    private void OnDisable()
    {
        SetOrbsActive(false);
    }
}
