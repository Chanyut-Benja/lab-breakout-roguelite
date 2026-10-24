using System.Collections;
using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ระบบควบคุมการทำงานของ Boon ทั้ง 5 ชนิด (Max Lv.2) และ Boon Resonance ทั้ง 5 สาย
/// ตามเอกสารบาลานซ์ชีทและข้อกำหนดทางการ:
/// 
/// 1. สาย 1: Blade Shield Matrix (Nano-Barrier + Executioner Matrix)
///    - เมื่อผู้เล่นโดนดาเมจในขณะที่ยังมี Shield ของ Nano-Barrier เหลืออยู่ (OnShieldDamaged):
///      * ปล่อยคลื่นใบมีดพลังงานหมุนวน 360 องศา กระจายออกจากตัวผู้เล่นเป็นวงกลม (รัศมี 6-8 เมตร)
///      * ทำความเสียหาย Pierce Damage ต่อเนื่องแก่ศัตรูทั้งหมดในระยะ และมีอัตรา Critical Strike 100%
/// 
/// 2. สาย 2: Epidemic Outbreak (Toxic Spore + Plague Spore Spreader)
///    - ดักจับ Event OnEnemyKilled ของศัตรูที่อยู่ในเขตหมอกพิษ Toxic Spore หรือติดสถานะ Poison:
///      * เมื่อศัตรูตาย ให้ระเบิดออก (Corpse Explosion) เป็นหมอกพิษย่อยกระจายรัศมี 3 เมตร
///      * ส่งต่อสถานะพิษรุนแรงและดาเมจทันทีแก่ศัตรูรอบข้าง ทำให้เกิดปฏิกิริยาลูกโซ่ (Chain Reaction) ต่อเนื่อง
/// 
/// 3. สาย 3: Solar Core Ring (Flame Ring + Supernova Cannon)
///    - ขยายรัศมีของ Flame Ring ให้กว้างขึ้น 50%
///    - ยิงลำแสง Solar Flare ลำแสงเพลิงดวงอาทิตย์พุ่งสุ่มโจมตีศัตรูนอกวงแหวนไฟทุกๆ 1.5 วินาที
///      สร้างความเสียหายรุนแรงและลดค่าเกราะศัตรูเพิ่มเป็น 25%
/// 
/// 4. สาย 4: Thunderstorm Domain (Static Aura + Storm Citadel)
///    - ปลดล็อคเงื่อนไขที่ต้องเดินสะสมระยะทางของ Static Aura ออก
///    - สร้างสนามพายุฟ้าผ่าอัตโนมัติรอบตัวผู้เล่น โดยจะสุ่มผ่าฟ้าแลบใส่ศัตรูในระยะ 10 เมตร ทุกๆ 0.5 วินาที
///      พร้อมสร้างสถานะชะงัก (Stun 0.2 วินาที)
/// 
/// 5. สาย 5: Event Horizon Void (Magnetic Pulse + Singularity Cannon)
///    - ทุกครั้งที่ Pulse ทำงานปล่อยคลื่นผลัก/สโลว์ตามรอบเวลา:
///      * เปิดจุดศูนย์กลางแรงโน้มถ่วง (Mini Black Hole) ณ ตำแหน่งที่คลื่นระเบิดออกเป็นเวลา 2 วินาที
///      * ดูดมอนสเตอร์ทุกตัวในระยะเข้ามายังจุดศูนย์กลางและทำดาเมจแรงโน้มถ่วงต่อเนื่อง ก่อนจะสลายตัว
/// </summary>
public class BoonCombatController : MonoBehaviour
{
    public static BoonCombatController Instance { get; private set; }

    private PlayerHealth playerHealth;
    private PlayerStats playerStats;

    // Standard Boon Timers & Accumulators
    private float barrierRegenTimer = 0f;
    private float toxicSporeTickTimer = 0f;
    private float flameRingTickTimer = 0f;
    private float magneticPulseTimer = 0f;
    private Vector2 lastPlayerPosition;
    private float accumulatedWalkDistance = 0f;

    // Resonance Timers
    private float solarFlareTimer = 0f;
    private float thunderstormDomainTimer = 0f;
    private float bladeShieldPeriodicTimer = 0f;
    private int chainExplosionDepth = 0;
    private const int MaxChainExplosionDepth = 5;

    // Visual & Zone GameObjects
    private GameObject nanoBarrierVisualObj;
    private SpriteRenderer nanoBarrierRenderer;

    private GameObject toxicSporeZoneObj;
    private GameObject toxicSporeVisualChild;
    private CircleCollider2D toxicSporeCollider;
    private SpriteRenderer toxicSporeRenderer;
    private ToxicSporeTriggerTracker toxicSporeTracker;

    private GameObject flameRingZoneObj;
    private SpriteRenderer flameRingRenderer;

    private GameObject staticAuraZoneObj;
    private SpriteRenderer staticAuraRenderer;

    private GameObject magneticPulseZoneObj;
    private SpriteRenderer magneticPulseRenderer;

    public static BoonCombatController EnsureActiveOnPlayer(Transform playerTransform)
    {
        if (playerTransform == null) return null;

        BoonCombatController ctrl = playerTransform.GetComponent<BoonCombatController>();
        if (ctrl == null)
        {
            ctrl = playerTransform.gameObject.AddComponent<BoonCombatController>();
        }
        return ctrl;
    }

    public static bool IsEventHorizonVoidResonantActive()
    {
        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return false;

        BoonSlotEntry pulseSlot = inv.GetBoonSlot(BoonItemType.MagneticPulse);
        return pulseSlot != null && pulseSlot.isResonant;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        playerHealth = GetComponent<PlayerHealth>();
        playerStats = GetComponent<PlayerStats>();
        lastPlayerPosition = transform.position;

        CreateVisualHoldersAndZones();
    }

    private void Start()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerStats == null) playerStats = GetComponent<PlayerStats>();
        lastPlayerPosition = transform.position;

        RefreshBoonVisualsAndZones();
    }

    private void OnEnable()
    {
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnShieldDamaged -= HandleShieldDamaged;
            playerHealth.OnShieldDamaged += HandleShieldDamaged;
            playerHealth.OnBarrierBroken -= HandleBarrierBroken;
            playerHealth.OnBarrierBroken += HandleBarrierBroken;
            playerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            playerHealth.OnPlayerDamaged += HandlePlayerDamaged;
        }

        EnemyHealth.OnAnyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyHealth.OnAnyEnemyDiedWithPoisonState += HandleEnemyDiedWithPoisonState;

        EnemyAI.OnLegacyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyAI.OnLegacyEnemyDiedWithPoisonState += HandleEnemyDiedWithPoisonState;

        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonatedStatic;
        PlayerInventory.OnBoonResonatedStatic += HandleBoonResonatedStatic;

        lastPlayerPosition = transform.position;
    }

    private void OnDisable()
    {
        if (playerHealth != null)
        {
            playerHealth.OnShieldDamaged -= HandleShieldDamaged;
            playerHealth.OnBarrierBroken -= HandleBarrierBroken;
            playerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
        }

        EnemyHealth.OnAnyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyAI.OnLegacyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;

        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonatedStatic;
    }

    private void HandleBoonResonatedStatic(BoonSlotEntry entry)
    {
        RefreshBoonVisualsAndZones();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv == null) return;

        float dt = Time.deltaTime;

        // 1. สาย 1: Nano-Barrier & Blade Shield Matrix
        UpdateNanoBarrier(inv, dt);

        // 2. สาย 2: Toxic Spore & Epidemic Outbreak
        UpdateToxicSpore(inv, dt);

        // 3. สาย 3: Flame Ring & Solar Core Ring
        UpdateFlameRing(inv, dt);

        // 4. สาย 4: Static Aura & Thunderstorm Domain
        UpdateStaticAura(inv, dt);

        // 5. สาย 5: Magnetic Pulse & Event Horizon Void
        UpdateMagneticPulse(inv, dt);

        // หมุนเวียนเอฟเฟกต์แอนิเมชันให้ดูมีชีวิตชีวา
        UpdateVisualAnimations(dt);
    }

    // ====================================================================================================
    // สาย 1: Nano-Barrier & Blade Shield Matrix (Executioner Matrix + Nano-Barrier Lv.2)
    // ====================================================================================================
    private void UpdateNanoBarrier(PlayerInventory inv, float dt)
    {
        BoonSlotEntry barrierSlot = inv.GetBoonSlot(BoonItemType.NanoBarrier);
        if (barrierSlot == null || barrierSlot.level <= 0)
        {
            if (nanoBarrierVisualObj != null && nanoBarrierVisualObj.activeSelf)
                nanoBarrierVisualObj.SetActive(false);
            return;
        }

        float maxShield = barrierSlot.level >= 2 ? 50f : 20f;
        float regenInterval = barrierSlot.level >= 2 ? 8f : 15f;

        if (playerHealth != null)
        {
            if (Mathf.Abs(playerHealth.MaxBarrierShield - maxShield) > 0.1f)
            {
                playerHealth.ConfigureNanoBarrier(maxShield);
            }

            // คูลดาวน์ฟื้นฟูเกราะเมื่อเกราะไม่เต็ม
            if (playerHealth.CurrentBarrierShield < maxShield)
            {
                barrierRegenTimer += dt;
                if (barrierRegenTimer >= regenInterval)
                {
                    barrierRegenTimer = 0f;
                    playerHealth.RestoreNanoBarrierToFull();
                    SpawnShieldRestoredVisual();
                }
            }
            else
            {
                barrierRegenTimer = 0f;
            }

            // แสดงเกราะเมื่อมี Shield เหลืออยู่
            if (nanoBarrierVisualObj != null)
            {
                bool hasShield = playerHealth.CurrentBarrierShield > 0f;
                if (nanoBarrierVisualObj.activeSelf != hasShield)
                {
                    nanoBarrierVisualObj.SetActive(hasShield);
                }
            }
        }

        // เสริม: Resonance ปล่อยคลื่นใบมีดกวาดล้างรอบตัวเป็นระยะเพิ่มเติม
        if (barrierSlot.isResonant)
        {
            bladeShieldPeriodicTimer += dt;
            if (bladeShieldPeriodicTimer >= 3.0f)
            {
                bladeShieldPeriodicTimer = 0f;
                TriggerBladeShieldMatrixBurst();
            }
        }
    }

    /// <summary>
    /// สาย 1: Blade Shield Matrix (Nano-Barrier + Executioner Matrix)
    /// เมื่อผู้เล่นโดนดาเมจในขณะที่ยังมี Shield ของ Nano-Barrier เหลืออยู่ (OnShieldDamaged):
    /// - ปล่อยคลื่นใบมีดพลังงานหมุนวน 360 องศา กระจายออกจากตัวผู้เล่นเป็นวงกลม (รัศมี 6-8 เมตร)
    /// - ทำความเสียหาย Pierce Damage ต่อเนื่องแก่ศัตรูทั้งหมดในระยะ และมีอัตรา Critical Strike 100% สอดคล้องกับคุณสมบัติของมีด
    /// </summary>
    private void HandleShieldDamaged(float absorbedDamage)
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv == null) return;

        BoonSlotEntry barrierSlot = inv.GetBoonSlot(BoonItemType.NanoBarrier);
        if (barrierSlot != null && barrierSlot.isResonant)
        {
            TriggerBladeShieldMatrixBurst();
        }
    }

    private void HandleBarrierBroken()
    {
        if (nanoBarrierVisualObj != null)
        {
            nanoBarrierVisualObj.SetActive(false);
        }

        SpawnShockwaveVisual(transform.position, 1.8f, new Color(0.3f, 0.85f, 1f, 0.9f));

        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv == null) return;
        BoonSlotEntry barrierSlot = inv.GetBoonSlot(BoonItemType.NanoBarrier);
        if (barrierSlot != null && barrierSlot.isResonant)
        {
            TriggerBladeShieldMatrixBurst();
        }
    }

    private void TriggerBladeShieldMatrixBurst()
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 7.0f * areaMult; // รัศมี 6-8 เมตร (7m กึ่งกลางสเปก)

        // คำนวณ Critical Strike 100% ตามคุณสมบัติของ Executioner Matrix (มีด)
        float baseDmg = 130f;
        float finalPierceCritDamage = playerStats != null
            ? playerStats.RollCriticalDamage(baseDmg, out _, true)
            : (baseDmg * 2.0f);

        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null || !enemy.activeInHierarchy) continue;

            // Pierce Damage เจาะทะลุศัตรูทั้งหมด พร้อม Guaranteed Critical Strike 100%
            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, finalPierceCritDamage, true);

            // กระแทกผลักศัตรูออกไปตามแนวรัศมี
            Vector2 pushDir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
            if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector2.right;
            Vector3 pushedPos = enemy.transform.position + (Vector3)(pushDir * 2.4f);

            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = pushedPos;
            }
            enemy.transform.position = pushedPos;
        }

        // VFX: คลื่นใบมีดพลังงานหมุนวน 360 องศา กระจายออกจากตัวผู้เล่นเป็นวงกลม
        StartCoroutine(AnimateBladeShieldMatrixRing(radius));
    }

    private IEnumerator AnimateBladeShieldMatrixRing(float radius)
    {
        int bladeCount = 12;
        List<GameObject> blades = new List<GameObject>();
        Color bladeColor = new Color(0.35f, 0.95f, 1.0f, 0.95f); // นีออนฟ้าสว่างเรืองแสง

        for (int i = 0; i < bladeCount; i++)
        {
            float angle = i * (360f / bladeCount);
            GameObject b = new GameObject("VFX_ResonantBlade");
            b.transform.position = transform.position;
            b.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            SpriteRenderer sr = b.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(sr, ProceduralSprites.NanoSlash);
            sr.sprite = ProceduralSprites.NanoSlash;
            sr.color = bladeColor;
            sr.sortingOrder = 22;
            b.transform.localScale = Vector3.one * 1.5f;

            blades.Add(b);
        }

        float duration = 0.35f;
        float elapsed = 0f;
        Vector3 centerPos = transform.position;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentDist = Mathf.Lerp(0.5f, radius, t);
            float currentSpin = t * 180f;

            for (int i = 0; i < blades.Count; i++)
            {
                if (blades[i] == null) continue;
                float angle = (i * (360f / bladeCount) + currentSpin) * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * currentDist;
                blades[i].transform.position = centerPos + offset;
                blades[i].transform.rotation = Quaternion.Euler(0f, 0f, (angle * Mathf.Rad2Deg) + 90f);

                SpriteRenderer sr = blades[i].GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color c = bladeColor;
                    c.a = Mathf.Lerp(bladeColor.a, 0f, t);
                    sr.color = c;
                }
            }
            yield return null;
        }

        for (int i = 0; i < blades.Count; i++)
        {
            if (blades[i] != null) Destroy(blades[i]);
        }

        SpawnShockwaveVisual(centerPos, radius, new Color(0.4f, 0.9f, 1f, 0.75f));
    }

    private void SpawnShieldRestoredVisual()
    {
        if (nanoBarrierVisualObj != null)
        {
            nanoBarrierVisualObj.SetActive(true);
        }
        SpawnShockwaveVisual(transform.position, 1.6f, new Color(0.2f, 0.9f, 1f, 0.75f));
    }

    // ====================================================================================================
    // สาย 2: Toxic Spore & Epidemic Outbreak (Plague Spore Spreader + Toxic Spore Lv.2)
    // ====================================================================================================
    private void UpdateToxicSpore(PlayerInventory inv, float dt)
    {
        BoonSlotEntry sporeSlot = inv.GetBoonSlot(BoonItemType.ToxicSpore);
        if (sporeSlot == null || sporeSlot.level <= 0)
        {
            if (toxicSporeZoneObj != null && toxicSporeZoneObj.activeSelf)
                toxicSporeZoneObj.SetActive(false);
            return;
        }

        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = (sporeSlot.level >= 2 ? 5.0f : 3.0f) * areaMult;

        if (toxicSporeZoneObj != null)
        {
            if (!toxicSporeZoneObj.activeSelf) toxicSporeZoneObj.SetActive(true);

            toxicSporeZoneObj.transform.localScale = Vector3.one;

            if (toxicSporeCollider != null && Mathf.Abs(toxicSporeCollider.radius - radius) > 0.05f)
            {
                toxicSporeCollider.radius = radius;
            }

            if (toxicSporeVisualChild != null)
            {
                toxicSporeVisualChild.transform.localScale = Vector3.one * (radius * 2f);
            }

            // ถ้าอยู่ในสถานะ Resonance ปรับสีหมอกสปอร์ให้เข้มข้น เรืองแสงนีออนมรกต
            if (toxicSporeRenderer != null)
            {
                toxicSporeRenderer.color = sporeSlot.isResonant
                    ? new Color(0.20f, 1.0f, 0.35f, 0.30f)
                    : new Color(0.25f, 0.95f, 0.3f, 0.18f);
            }
        }

        toxicSporeTickTimer += dt;
        if (toxicSporeTickTimer >= 0.5f)
        {
            toxicSporeTickTimer = 0f;

            float damagePerHalfSec = (sporeSlot.level >= 2 ? 30f : 15f) * 0.5f;

            HashSet<GameObject> affectedEnemies = new HashSet<GameObject>();
            if (toxicSporeTracker != null)
            {
                foreach (GameObject en in toxicSporeTracker.EnemiesInZone)
                {
                    if (en != null && en.activeInHierarchy) affectedEnemies.Add(en);
                }
            }

            List<GameObject> circleEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
            for (int i = 0; i < circleEnemies.Count; i++)
            {
                if (circleEnemies[i] != null) affectedEnemies.Add(circleEnemies[i]);
            }

            foreach (GameObject enemy in affectedEnemies)
            {
                if (enemy == null || !enemy.activeInHierarchy) continue;

                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, damagePerHalfSec, false);
                PlayerStats.ApplyPoisonToTarget(enemy, damagePerHalfSec, 1.5f);

                if (sporeSlot.level >= 2)
                {
                    ApplySlowToTarget(enemy, 0.20f, 0.7f);
                }
            }
        }
    }

    /// <summary>
    /// สาย 2: Epidemic Outbreak (Toxic Spore + Plague Spore Spreader)
    /// ดักจับ Event OnEnemyKilled ของศัตรูที่อยู่ในเขตหมอกพิษ Toxic Spore หรือติดสถานะ Poison:
    /// - เมื่อศัตรูตาย ให้ระเบิดออก (Corpse Explosion) เป็นหมอกพิษย่อยกระจายรัศมี 3 เมตร
    /// - ส่งต่อสถานะพิษรุนแรงและดาเมจทันทีแก่ศัตรูรอบข้าง ทำให้เกิดปฏิกิริยาลูกโซ่ (Chain Reaction) ต่อเนื่อง
    /// </summary>
    private void HandleEnemyDiedWithPoisonState(GameObject deadEnemy, Vector2 deathPosition, bool wasPoisoned)
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv == null) return;

        BoonSlotEntry sporeSlot = inv.GetBoonSlot(BoonItemType.ToxicSpore);
        if (sporeSlot == null || !sporeSlot.isResonant) return;

        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float toxicZoneRadius = (sporeSlot.level >= 2 ? 5.0f : 3.0f) * areaMult;
        bool isInSporeZone = Vector2.Distance(deathPosition, transform.position) <= toxicZoneRadius;

        // เงื่อนไข: อยู่ในเขตหมอกพิษ Toxic Spore หรือติดสถานะ Poison
        if (!wasPoisoned && !isInSporeZone) return;

        TriggerCorpseExplosion(deathPosition, deadEnemy);
    }

    private void TriggerCorpseExplosion(Vector2 explosionCenter, GameObject sourceDeadEnemy)
    {
        if (chainExplosionDepth >= MaxChainExplosionDepth) return;
        chainExplosionDepth++;

        try
        {
            float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
            float explosionRadius = 3.0f * areaMult; // รัศมี 3 เมตรตามสเปก
            float instantDamage = 150f;
            float epidemicPoisonDps = 45f;
            float epidemicDuration = 3.5f;

            List<GameObject> nearbyEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(explosionCenter, explosionRadius);
            for (int i = 0; i < nearbyEnemies.Count; i++)
            {
                GameObject target = nearbyEnemies[i];
                if (target == null || !target.activeInHierarchy || target == sourceDeadEnemy) continue;

                // ส่งต่อดาเมจระเบิดทันที
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, instantDamage, false);

                // ส่งต่อสถานะพิษรุนแรงแก่ศัตรูรอบข้าง (Chain Reaction)
                PlayerStats.ApplyPoisonToTarget(target, epidemicPoisonDps, epidemicDuration);
            }

            // Visual: หมอกพิษย่อยระเบิดกระจายออกรัศมี 3 เมตร
            SpawnShockwaveVisual(explosionCenter, explosionRadius, new Color(0.2f, 1.0f, 0.4f, 0.85f));
            SpawnMiniSporeCloudVisual(explosionCenter, explosionRadius);
        }
        finally
        {
            chainExplosionDepth--;
        }
    }

    private void SpawnMiniSporeCloudVisual(Vector2 pos, float radius)
    {
        GameObject cloudObj = new GameObject("VFX_Epidemic_CorpseExplosion");
        cloudObj.transform.position = pos;
        cloudObj.transform.localScale = Vector3.one * (radius * 2f);

        SpriteRenderer sr = cloudObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.CircleDisc);
        sr.sprite = ProceduralSprites.CircleDisc;
        sr.color = new Color(0.15f, 0.95f, 0.35f, 0.45f);
        sr.sortingOrder = 15;

        Destroy(cloudObj, 0.45f);
    }

    // ====================================================================================================
    // สาย 3: Flame Ring & Solar Core Ring (Supernova Cannon + Flame Ring Lv.2)
    // ====================================================================================================
    private void UpdateFlameRing(PlayerInventory inv, float dt)
    {
        BoonSlotEntry flameSlot = inv.GetBoonSlot(BoonItemType.FlameRing);
        if (flameSlot == null || flameSlot.level <= 0)
        {
            if (flameRingZoneObj != null && flameRingZoneObj.activeSelf)
                flameRingZoneObj.SetActive(false);
            return;
        }

        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float baseRadius = flameSlot.level >= 2 ? 5.0f : 3.5f;

        // สาย 3: เมื่อเข้าสู่สถานะ Resonance ขยายรัศมีของ Flame Ring ให้กว้างขึ้น 50%
        if (flameSlot.isResonant)
        {
            baseRadius *= 1.50f; // ขยายกว้างขึ้น 50% (จาก 5.0m กลายเป็น 7.5m!)
        }
        float radius = baseRadius * areaMult;

        if (flameRingZoneObj != null)
        {
            if (!flameRingZoneObj.activeSelf) flameRingZoneObj.SetActive(true);
            flameRingZoneObj.transform.localScale = Vector3.one * (radius * 2f);

            if (flameRingRenderer != null)
            {
                // สีส้มทองสุริยะเข้มข้น เรืองแสงนีออนเมื่ออยู่ในสถานะ Resonance
                flameRingRenderer.color = flameSlot.isResonant
                    ? new Color(1f, 0.55f, 0.1f, 0.65f)
                    : new Color(1f, 0.45f, 0.05f, 0.42f);
            }
        }

        flameRingTickTimer += dt;
        if (flameRingTickTimer >= 0.5f)
        {
            flameRingTickTimer = 0f;
            float dmgPerHalfSec = (flameSlot.level >= 2 ? 50f : 25f) * 0.5f;

            List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
            for (int i = 0; i < enemies.Count; i++)
            {
                GameObject enemy = enemies[i];
                if (enemy == null || !enemy.activeInHierarchy) continue;

                ApplyArmorReductionToTarget(enemy, 0.15f, 1.0f);
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, dmgPerHalfSec, false);
            }
        }

        // สาย 3: Resonance Solar Core Ring
        // เพิ่ม Coroutine/Timer ยิงลำแสง Solar Flare ลำแสงเพลิงดวงอาทิตย์พุ่งสุ่มโจมตีศัตรูนอกวงแหวนไฟทุกๆ 1.5 วินาที
        // สร้างความเสียหายรุนแรงและลดค่าเกราะศัตรูเพิ่มเป็น 25%
        if (flameSlot.isResonant)
        {
            solarFlareTimer += dt;
            if (solarFlareTimer >= 1.5f)
            {
                solarFlareTimer = 0f;
                TriggerSolarFlareStrike(radius);
            }
        }
    }

    private void TriggerSolarFlareStrike(float innerRingRadius)
    {
        // ค้นหาศัตรูที่อยู่นอกวงแหวนไฟ (ระยะระหว่าง innerRingRadius จนถึง 14 เมตร)
        float maxSearchRadius = innerRingRadius + 7.5f;
        List<GameObject> allNearby = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, maxSearchRadius);

        List<GameObject> outsideEnemies = new List<GameObject>();
        for (int i = 0; i < allNearby.Count; i++)
        {
            GameObject e = allNearby[i];
            if (e == null || !e.activeInHierarchy) continue;

            float dist = Vector2.Distance(transform.position, e.transform.position);
            if (dist >= innerRingRadius * 0.95f)
            {
                outsideEnemies.Add(e);
            }
        }

        if (outsideEnemies.Count == 0)
        {
            // ถ้าไม่มีศัตรูนอกวงแหวนไฟ ให้สุ่มศัตรูที่อยู่ในระยะใกล้สุดแทน
            if (allNearby.Count > 0) outsideEnemies.AddRange(allNearby);
            else return;
        }

        // สุ่มเป้าหมาย 1-2 ตัว
        int targetCount = Mathf.Min(outsideEnemies.Count, 2);
        for (int i = 0; i < targetCount; i++)
        {
            int rIdx = Random.Range(0, outsideEnemies.Count);
            GameObject target = outsideEnemies[rIdx];
            outsideEnemies.RemoveAt(rIdx);

            if (target != null && target.activeInHierarchy)
            {
                // สร้างความเสียหายรุนแรง (140 ดาเมจ)
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, 140f, false);

                // ลดค่าเกราะศัตรูเพิ่มเป็น 25% (ApplyArmorReduction 25%)
                ApplyArmorReductionToTarget(target, 0.25f, 3.5f);

                // VFX: ยิงลำแสง Solar Flare ลำแสงเพลิงดวงอาทิตย์พุ่งจากวงแหวนไฟไปยังเป้าหมาย
                Vector2 startPos = (Vector2)transform.position + ((Vector2)(target.transform.position - transform.position).normalized * innerRingRadius);
                StartCoroutine(AnimateSolarFlareBeam(startPos, target.transform.position));
                SpawnShockwaveVisual(target.transform.position, 1.5f, new Color(1f, 0.5f, 0.1f, 0.9f));
            }
        }
    }

    private IEnumerator AnimateSolarFlareBeam(Vector3 start, Vector3 end)
    {
        GameObject beamObj = new GameObject("VFX_SolarFlareBeam");
        beamObj.transform.position = (start + end) * 0.5f;

        Vector2 diff = end - start;
        float dist = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        beamObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        beamObj.transform.localScale = new Vector3(dist, 0.35f, 1f);

        SpriteRenderer sr = beamObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = new Color(1f, 0.60f, 0.15f, 0.95f); // สีเพลิงสุริยะ
        sr.sortingOrder = 24;

        float duration = 0.18f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (beamObj != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.95f, 0f, t);
                sr.color = c;
            }
            yield return null;
        }

        if (beamObj != null) Destroy(beamObj);
    }

    // ====================================================================================================
    // สาย 4: Static Aura & Thunderstorm Domain (Storm Citadel + Static Aura Lv.2)
    // ====================================================================================================
    private void UpdateStaticAura(PlayerInventory inv, float dt)
    {
        BoonSlotEntry staticSlot = inv.GetBoonSlot(BoonItemType.StaticAura);
        if (staticSlot == null || staticSlot.level <= 0)
        {
            if (staticAuraZoneObj != null && staticAuraZoneObj.activeSelf)
                staticAuraZoneObj.SetActive(false);
            return;
        }

        if (staticAuraZoneObj != null && !staticAuraZoneObj.activeSelf)
        {
            staticAuraZoneObj.SetActive(true);
        }

        // สาย 4: Thunderstorm Domain (Static Aura + Storm Citadel)
        // ปลดล็อคเงื่อนไขที่ต้องเดินสะสมระยะทางของ Static Aura ออก
        // เปลี่ยนเป็นการสร้างสนามพายุฟ้าผ่าอัตโนมัติรอบตัวผู้เล่น โดยจะสุ่มผ่าฟ้าแลบใส่ศัตรูในระยะ 10 เมตร ทุกๆ 0.5 วินาที
        // พร้อมสร้างสถานะชะงัก (Stun 0.2 วินาที)
        if (staticSlot.isResonant)
        {
            if (staticAuraRenderer != null)
            {
                staticAuraRenderer.color = new Color(0.4f, 0.85f, 1f, 0.85f); // ฟ้าผ่าพายุนีออนเรืองแสง
                staticAuraZoneObj.transform.localScale = Vector3.one * 3.5f;
            }

            thunderstormDomainTimer += dt;
            if (thunderstormDomainTimer >= 0.5f)
            {
                thunderstormDomainTimer = 0f;
                TriggerThunderstormDomainLightningStrike();
            }
        }
        else
        {
            // โหมดปกติก่อน Resonant: ต้องเดินสะสมระยะทาง
            if (staticAuraRenderer != null)
            {
                staticAuraRenderer.color = new Color(0.95f, 0.95f, 0.25f, 0.45f);
                staticAuraZoneObj.transform.localScale = Vector3.one * 1.5f;
            }

            Vector2 currentPosition = transform.position;
            float stepDistance = Vector2.Distance(currentPosition, lastPlayerPosition);
            lastPlayerPosition = currentPosition;

            if (stepDistance > 0.001f && stepDistance < 15f)
            {
                accumulatedWalkDistance += stepDistance;
            }

            float triggerDistance = staticSlot.level >= 2 ? 4.0f : 5.0f;
            if (accumulatedWalkDistance >= triggerDistance)
            {
                accumulatedWalkDistance -= triggerDistance;
                int targetCount = staticSlot.level >= 2 ? 3 : 1;
                float damage = staticSlot.level >= 2 ? 70f : 40f;
                TriggerStandardLightningStrike(targetCount, damage);
            }
        }
    }

    private void TriggerThunderstormDomainLightningStrike()
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 10.0f * areaMult; // ระยะ 10 เมตรตามสเปก

        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
        if (enemies.Count == 0) return;

        // สุ่มผ่าฟ้าแลบใส่ศัตรู 1-2 ตัว
        int strikeCount = Mathf.Min(enemies.Count, 2);
        for (int i = 0; i < strikeCount; i++)
        {
            int rIdx = Random.Range(0, enemies.Count);
            GameObject target = enemies[rIdx];
            enemies.RemoveAt(rIdx);

            if (target != null && target.activeInHierarchy)
            {
                // ดาเมจฟ้าผ่า 75
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, 75f, true);

                // สถานะชะงัก (Stun 0.2 วินาที)
                PlayerStats.ApplyStunToTarget(target, 0.2f);

                // VFX สายฟ้าผ่าฟาดลงมาจากท้องฟ้า
                Vector3 targetPos = target.transform.position;
                Vector3 skyPos = targetPos + new Vector3(Random.Range(-0.5f, 0.5f), 5.5f, 0f);
                SpawnLightningArcVisual(skyPos, targetPos, new Color(0.5f, 0.95f, 1f, 1f));
                SpawnShockwaveVisual(targetPos, 1.4f, new Color(0.4f, 0.85f, 1f, 0.95f));
            }
        }
    }

    private void TriggerStandardLightningStrike(int targetCount, float damage)
    {
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, 8.5f);
        if (enemies.Count == 0) return;

        for (int i = 0; i < targetCount && enemies.Count > 0; i++)
        {
            int rIdx = Random.Range(0, enemies.Count);
            GameObject target = enemies[rIdx];
            enemies.RemoveAt(rIdx);

            if (target != null && target.activeInHierarchy)
            {
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, damage, true);
                SpawnLightningArcVisual(transform.position, target.transform.position, new Color(0.95f, 0.95f, 0.25f, 0.95f));
                SpawnShockwaveVisual(target.transform.position, 1.2f, new Color(0.95f, 0.95f, 0.25f, 0.95f));
            }
        }
    }

    // ====================================================================================================
    // สาย 5: Magnetic Pulse & Event Horizon Void (Singularity Cannon + Magnetic Pulse Lv.2)
    // ====================================================================================================
    private void UpdateMagneticPulse(PlayerInventory inv, float dt)
    {
        BoonSlotEntry pulseSlot = inv.GetBoonSlot(BoonItemType.MagneticPulse);
        if (pulseSlot == null || pulseSlot.level <= 0)
        {
            if (magneticPulseZoneObj != null && magneticPulseZoneObj.activeSelf)
                magneticPulseZoneObj.SetActive(false);
            return;
        }

        if (magneticPulseZoneObj != null && !magneticPulseZoneObj.activeSelf)
        {
            magneticPulseZoneObj.SetActive(true);
        }

        if (magneticPulseRenderer != null)
        {
            magneticPulseRenderer.color = pulseSlot.isResonant
                ? new Color(0.70f, 0.15f, 1.0f, 0.65f) // ม่วงเข้มเรืองแสงหลุมดำ
                : new Color(0.85f, 0.3f, 1f, 0.35f);
        }

        float interval = pulseSlot.level >= 2 ? 2.0f : 4.0f;
        magneticPulseTimer += dt;

        if (magneticPulseTimer >= interval)
        {
            magneticPulseTimer = 0f;
            float slowAmount = pulseSlot.level >= 2 ? 0.50f : 0.30f;
            TriggerMagneticPulseWave(slowAmount, pulseSlot.level >= 2, pulseSlot.isResonant);
        }
    }

    /// <summary>
    /// สาย 5: Event Horizon Void (Magnetic Pulse + Singularity Cannon)
    /// ทุกครั้งที่ Pulse ทำงานปล่อยคลื่นผลัก/สโลว์ตามรอบเวลา:
    /// - จะเปิดจุดศูนย์กลางแรงโน้มถ่วง (Mini Black Hole) ณ ตำแหน่งที่คลื่นระเบิดออกเป็นเวลา 2 วินาที
    /// - ดูดมอนสเตอร์ทุกตัวในระยะเข้ามายังจุดศูนย์กลางและทำดาเมจแรงโน้มถ่วงต่อเนื่อง ก่อนจะสลายตัว
    /// </summary>
    private void TriggerMagneticPulseWave(float slowAmount, bool canReflectProjectiles, bool isResonant)
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 4.5f * areaMult;
        Vector3 pulseCenter = transform.position;

        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(pulseCenter, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null || !enemy.activeInHierarchy) continue;

            ApplySlowToTarget(enemy, slowAmount, 1.5f);
        }

        if (canReflectProjectiles)
        {
            ReflectOrClearEnemyProjectiles(radius);
        }

        StartCoroutine(AnimateExpandingPulse(radius, new Color(0.85f, 0.25f, 1f, 0.85f)));

        // ถ้าเข้าสู่สถานะ Resonance ให้เปิด Mini Black Hole เป็นเวลา 2 วินาที
        if (isResonant)
        {
            StartCoroutine(SpawnEventHorizonMiniBlackHole(pulseCenter, 5.5f * areaMult, 2.0f));
        }
    }

    private IEnumerator SpawnEventHorizonMiniBlackHole(Vector3 centerPos, float pullRadius, float lifetime)
    {
        GameObject holeObj = new GameObject("VFX_Resonant_EventHorizonVoid");
        holeObj.transform.position = centerPos;
        holeObj.transform.localScale = Vector3.one * 0.5f;

        SpriteRenderer sr = holeObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.CircleDisc);
        sr.sprite = ProceduralSprites.CircleDisc;
        sr.color = new Color(0.12f, 0.02f, 0.20f, 0.90f); // หลุมดำม่วงเข้ม
        sr.sortingOrder = 20;

        GameObject ringChild = new GameObject("Ring");
        ringChild.transform.SetParent(holeObj.transform, false);
        ringChild.transform.localPosition = Vector3.zero;
        ringChild.transform.localScale = Vector3.one * 1.35f;

        SpriteRenderer ringSr = ringChild.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(ringSr, ProceduralSprites.CircleRing);
        ringSr.sprite = ProceduralSprites.CircleRing;
        ringSr.color = new Color(0.85f, 0.25f, 1f, 0.85f);
        ringSr.sortingOrder = 21;

        float elapsed = 0f;
        float tickDamageTimer = 0f;
        float gravDmgPerTick = 35f;

        while (elapsed < lifetime)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            tickDamageTimer += dt;

            // หมุนวนหลุมดำ
            if (holeObj != null)
            {
                holeObj.transform.Rotate(0f, 0f, 180f * dt);
                float pulseScale = 1.0f + Mathf.Sin(elapsed * 8f) * 0.15f;
                holeObj.transform.localScale = Vector3.one * (1.8f * pulseScale);
            }

            // ดูดมอนสเตอร์ทุกตัวในระยะเข้ามายังจุดศูนย์กลาง
            List<GameObject> suckedEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(centerPos, pullRadius);
            for (int i = 0; i < suckedEnemies.Count; i++)
            {
                GameObject enemy = suckedEnemies[i];
                if (enemy == null || !enemy.activeInHierarchy) continue;

                Vector3 targetPos = Vector2.MoveTowards(enemy.transform.position, centerPos, 5.5f * dt);
                Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.position = targetPos;
                }
                enemy.transform.position = targetPos;
            }

            // ทำดาเมจแรงโน้มถ่วงต่อเนื่องทุก 0.25 วินาที
            if (tickDamageTimer >= 0.25f)
            {
                tickDamageTimer = 0f;
                for (int i = 0; i < suckedEnemies.Count; i++)
                {
                    if (suckedEnemies[i] != null && suckedEnemies[i].activeInHierarchy)
                    {
                        EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(suckedEnemies[i], gravDmgPerTick, false);
                    }
                }
            }

            yield return null;
        }

        // ก่อนจะสลายตัว: Implosion Shockwave ระเบิดยุบตัว
        List<GameObject> finalEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(centerPos, pullRadius);
        for (int i = 0; i < finalEnemies.Count; i++)
        {
            if (finalEnemies[i] != null && finalEnemies[i].activeInHierarchy)
            {
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(finalEnemies[i], 100f, false);
                PlayerStats.ApplyStunToTarget(finalEnemies[i], 0.5f);
            }
        }

        SpawnShockwaveVisual(centerPos, pullRadius, new Color(0.85f, 0.25f, 1f, 0.95f));

        if (holeObj != null) Destroy(holeObj);
    }

    private void HandlePlayerDamaged(float damage)
    {
        barrierRegenTimer = 0f;
    }

    // ====================================================================================================
    // Visual Helpers & Utility Methods
    // ====================================================================================================
    private void CreateVisualHoldersAndZones()
    {
        // 1. Nano-Barrier Visual
        if (nanoBarrierVisualObj == null)
        {
            nanoBarrierVisualObj = new GameObject("VFX_Boon_NanoBarrier");
            nanoBarrierVisualObj.transform.SetParent(transform, false);
            nanoBarrierVisualObj.transform.localPosition = Vector3.zero;
            nanoBarrierVisualObj.transform.localScale = Vector3.one * 1.5f;

            nanoBarrierRenderer = nanoBarrierVisualObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(nanoBarrierRenderer, ProceduralSprites.ShieldHex);
            nanoBarrierRenderer.sprite = ProceduralSprites.ShieldHex;
            nanoBarrierRenderer.color = new Color(0.2f, 0.85f, 1f, 0.55f);
            nanoBarrierRenderer.sortingOrder = 16;
            nanoBarrierVisualObj.SetActive(false);
        }

        // 2. Toxic Spore Zone
        if (toxicSporeZoneObj == null)
        {
            toxicSporeZoneObj = new GameObject("Zone_Boon_ToxicSpore");
            toxicSporeZoneObj.transform.SetParent(transform, false);
            toxicSporeZoneObj.transform.localPosition = Vector3.zero;
            toxicSporeZoneObj.transform.localScale = Vector3.one;

            toxicSporeCollider = toxicSporeZoneObj.AddComponent<CircleCollider2D>();
            toxicSporeCollider.isTrigger = true;
            toxicSporeCollider.radius = 3.0f;

            toxicSporeTracker = toxicSporeZoneObj.AddComponent<ToxicSporeTriggerTracker>();

            toxicSporeVisualChild = new GameObject("ToxicSpore_Visual");
            toxicSporeVisualChild.transform.SetParent(toxicSporeZoneObj.transform, false);
            toxicSporeVisualChild.transform.localPosition = Vector3.zero;
            toxicSporeVisualChild.transform.localScale = Vector3.one * 6.0f;

            toxicSporeRenderer = toxicSporeVisualChild.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(toxicSporeRenderer, ProceduralSprites.CircleDisc);
            toxicSporeRenderer.sprite = ProceduralSprites.CircleDisc;
            toxicSporeRenderer.color = new Color(0.25f, 0.95f, 0.3f, 0.18f);
            toxicSporeRenderer.sortingOrder = 11;

            toxicSporeZoneObj.SetActive(false);
        }

        // 3. Flame Ring Zone
        if (flameRingZoneObj == null)
        {
            flameRingZoneObj = new GameObject("Zone_Boon_FlameRing");
            flameRingZoneObj.transform.SetParent(transform, false);
            flameRingZoneObj.transform.localPosition = Vector3.zero;
            flameRingZoneObj.transform.localScale = Vector3.one * 7.0f;

            flameRingRenderer = flameRingZoneObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(flameRingRenderer, ProceduralSprites.CircleRing);
            flameRingRenderer.sprite = ProceduralSprites.CircleRing;
            flameRingRenderer.color = new Color(1f, 0.45f, 0.05f, 0.42f);
            flameRingRenderer.sortingOrder = 12;
            flameRingZoneObj.SetActive(false);
        }

        // 4. Static Aura Zone
        if (staticAuraZoneObj == null)
        {
            staticAuraZoneObj = new GameObject("Zone_Boon_StaticAura");
            staticAuraZoneObj.transform.SetParent(transform, false);
            staticAuraZoneObj.transform.localPosition = Vector3.zero;
            staticAuraZoneObj.transform.localScale = Vector3.one * 1.5f;

            staticAuraRenderer = staticAuraZoneObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(staticAuraRenderer, ProceduralSprites.CircleRing);
            staticAuraRenderer.sprite = ProceduralSprites.CircleRing;
            staticAuraRenderer.color = new Color(0.95f, 0.95f, 0.25f, 0.45f);
            staticAuraRenderer.sortingOrder = 13;
            staticAuraZoneObj.SetActive(false);
        }

        // 5. Magnetic Pulse Zone
        if (magneticPulseZoneObj == null)
        {
            magneticPulseZoneObj = new GameObject("Zone_Boon_MagneticPulse");
            magneticPulseZoneObj.transform.SetParent(transform, false);
            magneticPulseZoneObj.transform.localPosition = Vector3.zero;
            magneticPulseZoneObj.transform.localScale = Vector3.one * 1.6f;

            magneticPulseRenderer = magneticPulseZoneObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(magneticPulseRenderer, ProceduralSprites.CircleRing);
            magneticPulseRenderer.sprite = ProceduralSprites.CircleRing;
            magneticPulseRenderer.color = new Color(0.85f, 0.3f, 1f, 0.35f);
            magneticPulseRenderer.sortingOrder = 13;
            magneticPulseZoneObj.SetActive(false);
        }
    }

    private void UpdateVisualAnimations(float dt)
    {
        if (nanoBarrierVisualObj != null && nanoBarrierVisualObj.activeSelf)
        {
            nanoBarrierVisualObj.transform.Rotate(0f, 0f, 25f * dt);
        }

        if (toxicSporeVisualChild != null && toxicSporeZoneObj != null && toxicSporeZoneObj.activeSelf)
        {
            toxicSporeVisualChild.transform.Rotate(0f, 0f, 12f * dt);
        }

        if (flameRingZoneObj != null && flameRingZoneObj.activeSelf)
        {
            flameRingZoneObj.transform.Rotate(0f, 0f, -40f * dt);
        }

        if (staticAuraZoneObj != null && staticAuraZoneObj.activeSelf)
        {
            staticAuraZoneObj.transform.Rotate(0f, 0f, 85f * dt);
        }

        if (magneticPulseZoneObj != null && magneticPulseZoneObj.activeSelf)
        {
            magneticPulseZoneObj.transform.Rotate(0f, 0f, 30f * dt);
        }
    }

    public void RefreshBoonVisualsAndZones()
    {
        CreateVisualHoldersAndZones();

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv == null) return;

        // Nano-Barrier
        BoonSlotEntry barrier = inv.GetBoonSlot(BoonItemType.NanoBarrier);
        if (barrier != null && barrier.level > 0 && playerHealth != null)
        {
            float targetShield = barrier.level >= 2 ? 50f : 20f;
            playerHealth.ConfigureNanoBarrier(targetShield);
            if (nanoBarrierVisualObj != null)
            {
                nanoBarrierVisualObj.SetActive(playerHealth.CurrentBarrierShield > 0f);
                if (nanoBarrierRenderer != null)
                {
                    nanoBarrierRenderer.color = barrier.isResonant
                        ? new Color(0.4f, 0.95f, 1f, 0.85f) // นีออนฟ้าเข้มสว่าง
                        : new Color(0.2f, 0.85f, 1f, 0.55f);
                }
            }
        }
        else if (nanoBarrierVisualObj != null)
        {
            nanoBarrierVisualObj.SetActive(false);
        }

        // Toxic Spore
        BoonSlotEntry spore = inv.GetBoonSlot(BoonItemType.ToxicSpore);
        if (spore != null && spore.level > 0 && toxicSporeZoneObj != null)
        {
            float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
            float r = (spore.level >= 2 ? 5.0f : 3.0f) * areaMult;
            toxicSporeZoneObj.SetActive(true);
            toxicSporeZoneObj.transform.localScale = Vector3.one;
            if (toxicSporeCollider != null) toxicSporeCollider.radius = r;
            if (toxicSporeVisualChild != null) toxicSporeVisualChild.transform.localScale = Vector3.one * (r * 2f);
            if (toxicSporeRenderer != null)
            {
                toxicSporeRenderer.color = spore.isResonant
                    ? new Color(0.20f, 1.0f, 0.35f, 0.30f)
                    : new Color(0.25f, 0.95f, 0.3f, 0.18f);
            }
        }
        else if (toxicSporeZoneObj != null)
        {
            toxicSporeZoneObj.SetActive(false);
        }

        // Flame Ring
        BoonSlotEntry flame = inv.GetBoonSlot(BoonItemType.FlameRing);
        if (flame != null && flame.level > 0 && flameRingZoneObj != null)
        {
            float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
            float baseR = flame.level >= 2 ? 5.0f : 3.5f;
            if (flame.isResonant) baseR *= 1.50f; // ขยายขึ้น 50%
            float r = baseR * areaMult;

            flameRingZoneObj.SetActive(true);
            flameRingZoneObj.transform.localScale = Vector3.one * (r * 2f);
            if (flameRingRenderer != null)
            {
                flameRingRenderer.color = flame.isResonant
                    ? new Color(1f, 0.55f, 0.1f, 0.65f)
                    : new Color(1f, 0.45f, 0.05f, 0.42f);
            }
        }
        else if (flameRingZoneObj != null)
        {
            flameRingZoneObj.SetActive(false);
        }

        // Static Aura
        BoonSlotEntry st = inv.GetBoonSlot(BoonItemType.StaticAura);
        if (st != null && st.level > 0 && staticAuraZoneObj != null)
        {
            staticAuraZoneObj.SetActive(true);
            if (staticAuraRenderer != null)
            {
                staticAuraRenderer.color = st.isResonant
                    ? new Color(0.4f, 0.85f, 1f, 0.85f)
                    : new Color(0.95f, 0.95f, 0.25f, 0.45f);
            }
        }
        else if (staticAuraZoneObj != null)
        {
            staticAuraZoneObj.SetActive(false);
        }

        // Magnetic Pulse
        BoonSlotEntry pulse = inv.GetBoonSlot(BoonItemType.MagneticPulse);
        if (pulse != null && pulse.level > 0 && magneticPulseZoneObj != null)
        {
            magneticPulseZoneObj.SetActive(true);
            if (magneticPulseRenderer != null)
            {
                magneticPulseRenderer.color = pulse.isResonant
                    ? new Color(0.70f, 0.15f, 1.0f, 0.65f)
                    : new Color(0.85f, 0.3f, 1f, 0.35f);
            }
        }
        else if (magneticPulseZoneObj != null)
        {
            magneticPulseZoneObj.SetActive(false);
        }
    }

    private static void ApplySlowToTarget(GameObject target, float slowPercent, float duration)
    {
        if (target == null) return;

        EnemyHealth eh = target.GetComponent<EnemyHealth>() ?? target.GetComponentInChildren<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            eh.ApplySlow(slowPercent, duration);
        }

        EnemyBase eb = target.GetComponent<EnemyBase>() ?? target.GetComponentInParent<EnemyBase>();
        if (eb != null)
        {
            eb.ApplySlow(slowPercent, duration);
        }

        EnemyAI ea = target.GetComponent<EnemyAI>() ?? target.GetComponentInChildren<EnemyAI>() ?? target.GetComponentInParent<EnemyAI>();
        if (ea != null)
        {
            ea.ApplySlow(slowPercent, duration);
        }
    }

    private static void ApplyArmorReductionToTarget(GameObject target, float reductionPercent, float duration)
    {
        if (target == null) return;

        EnemyHealth eh = target.GetComponent<EnemyHealth>() ?? target.GetComponentInChildren<EnemyHealth>() ?? target.GetComponentInParent<EnemyHealth>();
        if (eh != null)
        {
            eh.ApplyArmorReduction(reductionPercent, duration);
        }

        EnemyAI ea = target.GetComponent<EnemyAI>() ?? target.GetComponentInChildren<EnemyAI>() ?? target.GetComponentInParent<EnemyAI>();
        if (ea != null)
        {
            ea.ApplyArmorReduction(reductionPercent, duration);
        }
    }

    private static void SpawnShockwaveVisual(Vector3 pos, float radius, Color color)
    {
        GameObject waveObj = new GameObject("VFX_Boon_Shockwave");
        waveObj.transform.position = pos;
        waveObj.transform.localScale = Vector3.one * (radius * 2f);

        SpriteRenderer sr = waveObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.CircleRing);
        sr.sprite = ProceduralSprites.CircleRing;
        sr.color = color;
        sr.sortingOrder = 17;

        Destroy(waveObj, 0.25f);
    }

    private void SpawnLightningArcVisual(Vector3 startPos, Vector3 targetPos, Color arcColor)
    {
        GameObject arcObj = new GameObject("VFX_Boon_LightningArc");
        arcObj.transform.position = (startPos + targetPos) * 0.5f;

        Vector2 diff = targetPos - startPos;
        float dist = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        arcObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        arcObj.transform.localScale = new Vector3(dist, 0.22f, 1f);

        SpriteRenderer sr = arcObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = arcColor;
        sr.sortingOrder = 23;

        Destroy(arcObj, 0.14f);
    }

    private IEnumerator AnimateExpandingPulse(float maxRadius, Color color)
    {
        GameObject pulseObj = new GameObject("VFX_MagneticPulseWave");
        pulseObj.transform.position = transform.position;
        pulseObj.transform.localScale = Vector3.one * 0.3f;

        SpriteRenderer sr = pulseObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.CircleRing);
        sr.sprite = ProceduralSprites.CircleRing;
        sr.color = color;
        sr.sortingOrder = 18;

        float duration = 0.32f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float currentD = Mathf.Lerp(0.5f, maxRadius * 2f, t);
            if (pulseObj != null)
            {
                pulseObj.transform.localScale = Vector3.one * currentD;
                Color c = color;
                c.a = Mathf.Lerp(color.a, 0f, t);
                sr.color = c;
            }
            yield return null;
        }

        if (pulseObj != null) Destroy(pulseObj);
    }

    private void ReflectOrClearEnemyProjectiles(float radius)
    {
#if UNITY_2023_1_OR_NEWER
        EnemyProjectile[] projs = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
#else
        EnemyProjectile[] projs = FindObjectsOfType<EnemyProjectile>();
#endif
        for (int i = 0; i < projs.Length; i++)
        {
            if (projs[i] == null) continue;
            if (Vector2.Distance(transform.position, projs[i].transform.position) <= radius)
            {
                if (Random.value < 0.30f)
                {
                    Rigidbody2D rb = projs[i].GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
#if UNITY_6000_0_OR_NEWER
                        rb.linearVelocity = -rb.linearVelocity * 1.5f;
#else
                        rb.velocity = -rb.velocity * 1.5f;
#endif
                    }
                }
                else
                {
                    Destroy(projs[i].gameObject);
                }
            }
        }
    }
}

/// <summary>
/// Trigger tracker attached to Toxic Spore zone to register monsters inside the circle
/// </summary>
public class ToxicSporeTriggerTracker : MonoBehaviour
{
    private readonly HashSet<GameObject> enemiesInZone = new HashSet<GameObject>();
    public HashSet<GameObject> EnemiesInZone => enemiesInZone;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null || other.CompareTag("Player")) return;
        GameObject enemy = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (enemy != null && (enemy.CompareTag("Enemy") || enemy.GetComponent<EnemyHealth>() != null || enemy.GetComponent<EnemyAI>() != null))
        {
            enemiesInZone.Add(enemy);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        GameObject enemy = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
        if (enemy != null)
        {
            enemiesInZone.Remove(enemy);
        }
    }

    private void OnDisable()
    {
        enemiesInZone.Clear();
    }
}
