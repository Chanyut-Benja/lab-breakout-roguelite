using System.Collections;
using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// ระบบควบคุมการทำงานของ Boon ทั้ง 5 ชนิด (Max Lv.2) และ Resonance ทั้ง 5 สาย ตามเอกสารบาลานซ์ชีททางการ:
/// 
/// 1. Nano-Barrier (สายอาวุธ Nano-Blade):
///    - Lv.1: สร้างเกราะ 20 ฟื้นทุก 15 วินาที
///    - Lv.2: เกราะ 50 ฟื้นทุก 8 วินาที (Max)
///    - เมื่อโดนดาเมจให้หัก Shield ก่อน HP และมีตัวจับเวลานับคูลดาวน์ฟื้นฟู
///    - Resonance (Blade Shield Matrix): คลื่นใบมีดพลังงานจลน์ 360 องศา (DMG 200) กระแทกศัตรูรอบตัวทุก 2.0 วินาที
/// 
/// 2. Toxic Spore (สายอาวุธ Bio-Needler):
///    - Lv.1: หมอกสปอร์พิษ 15/s รัศมี 3m (Trigger Collider ทรงกลมรอบตัวผู้เล่น)
///    - Lv.2: หมอกสปอร์พิษ 30/s รัศมี 5m + ปรับ MoveSpeed ของมอนสเตอร์ใน Trigger ลง 20% (Max)
///    - Resonance (Epidemic Outbreak): ศัตรูติดพิษตายจะระเบิดเป็นสปอร์ลูกโซ่ขนาดใหญ่ (DMG 150) แพร่พิษรัศมีกว้างขึ้น 50%
/// 
/// 3. Flame Ring (สายอาวุธ Plasma Blaster):
///    - Lv.1: วงแหวนไฟรอบตัวเผาผลาญ 25/s (รัศมี 3.5m) พร้อมดีบัฟ Armor ลดลง 15%
///    - Lv.2: วงแหวนไฟ 50/s (รัศมี 5.0m) พร้อมดีบัฟ Armor ลดลง 15% (Max)
///    - VFX ขนาดวงแหวนไฟตรงกับรัศมีความเสียหายจริง
///    - Resonance (Solar Core Ring): คลื่นความร้อนพลาสมาต่อเนื่องทุก 3.0 วินาที (DMG 80) ดูดศัตรูเข้าหากึ่งกลาง
/// 
/// 4. Static Aura (สายอาวุธ Tesla Orb):
///    - Lv.1: ผ่าสายฟ้า 1 ตัว ทุกระยะเดิน 5m (ดาเมจ 40) ด้วย Vector2.Distance Tracker
///    - Lv.2: ผ่าสายฟ้า 3 ตัว ทุกระยะเดิน 4m (ดาเมจ 70) (Max)
///    - Resonance (Thunderstorm Domain): พายุสายฟ้าอัสนีบาตผ่าลงศัตรูรอบตัว 120 DMG พร้อมสตั้น 1.5 วินาที ทุก 3.5 วินาที
/// 
/// 5. Magnetic Pulse (สายอาวุธ Gravity Grenadier):
///    - Lv.1: ปล่อยคลื่น Pulse ทุก 4 วินาที สโลว์ศัตรู 30%
///    - Lv.2: ปล่อยคลื่น Pulse ทุก 2 วินาที สโลว์ศัตรู 50% สะท้อนกระสุน 30% (Max)
///    - Resonance (Event Horizon Void): เมื่อผู้เล่นโดนดาเมจ จะระเบิดคลื่นสะท้อนความเสียหาย 150 DMG ผลักศัตรูในรัศมี 4.5m
/// </summary>
public class BoonCombatController : MonoBehaviour
{
    public static BoonCombatController Instance { get; private set; }

    private PlayerHealth playerHealth;
    private PlayerStats playerStats;

    // Timers & Accumulators
    private float barrierRegenTimer = 0f;
    private float toxicSporeTickTimer = 0f;
    private float flameRingTickTimer = 0f;
    private float magneticPulseTimer = 0f;
    private Vector2 lastPlayerPosition;
    private float accumulatedWalkDistance = 0f;

    // Resonance Timers
    private float bladeMatrixTimer = 0f;
    private float solarCoreTimer = 0f;
    private float thunderstormTimer = 0f;
    private bool isChainSporeExploding = false;

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
            playerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            playerHealth.OnPlayerDamaged += HandlePlayerDamaged;
            playerHealth.OnBarrierBroken -= HandleBarrierBroken;
            playerHealth.OnBarrierBroken += HandleBarrierBroken;
        }

        EnemyHealth.OnAnyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyHealth.OnAnyEnemyDiedWithPoisonState += HandleEnemyDiedWithPoisonState;

        EnemyAI.OnLegacyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyAI.OnLegacyEnemyDiedWithPoisonState += HandleEnemyDiedWithPoisonState;

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnBoonResonated -= HandleBoonResonated;
            PlayerInventory.Instance.OnBoonResonated += HandleBoonResonated;
        }

        lastPlayerPosition = transform.position;
    }

    private void HandleBoonResonated(BoonSlotEntry entry)
    {
        RefreshBoonVisualsAndZones();
    }

    private void OnDisable()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnBoonResonated -= HandleBoonResonated;
        }

        if (playerHealth != null)
        {
            playerHealth.OnPlayerDamaged -= HandlePlayerDamaged;
            playerHealth.OnBarrierBroken -= HandleBarrierBroken;
        }

        EnemyHealth.OnAnyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
        EnemyAI.OnLegacyEnemyDiedWithPoisonState -= HandleEnemyDiedWithPoisonState;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;

        PlayerInventory inv = PlayerInventory.GetOrCreate();
        if (inv == null) return;

        float dt = Time.deltaTime;

        // 1. Nano-Barrier
        UpdateNanoBarrier(inv, dt);

        // 2. Toxic Spore
        UpdateToxicSpore(inv, dt);

        // 3. Flame Ring
        UpdateFlameRing(inv, dt);

        // 4. Static Aura
        UpdateStaticAura(inv, dt);

        // 5. Magnetic Pulse
        UpdateMagneticPulse(inv, dt);

        // Continuous subtle visual rotations
        UpdateVisualAnimations(dt);
    }

    // ====================================================================================================
    // 1. Nano-Barrier (Shield Lv.1 = 20 ฟื้น 15s / Shield Lv.2 = 50 ฟื้น 8s | หัก Shield ก่อน HP)
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

            // ตัวจับเวลานับคูลดาวน์ฟื้นฟูเมื่อเกราะไม่เต็ม
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

            // Visual แสดงผลเมื่อ Shield > 0
            if (nanoBarrierVisualObj != null)
            {
                bool hasShield = playerHealth.CurrentBarrierShield > 0f;
                if (nanoBarrierVisualObj.activeSelf != hasShield)
                {
                    nanoBarrierVisualObj.SetActive(hasShield);
                }
            }
        }

        // Resonance: Blade Shield Matrix
        if (barrierSlot.isResonant)
        {
            bladeMatrixTimer += dt;
            if (bladeMatrixTimer >= 2.0f)
            {
                bladeMatrixTimer = 0f;
                TriggerKineticBladeWave();
            }
        }
    }

    private void HandleBarrierBroken()
    {
        if (nanoBarrierVisualObj != null)
        {
            nanoBarrierVisualObj.SetActive(false);
        }

        SpawnShockwaveVisual(transform.position, 1.5f, new Color(0.3f, 0.85f, 1f, 0.85f));

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;
        BoonSlotEntry barrierSlot = inv.GetBoonSlot(BoonItemType.NanoBarrier);
        if (barrierSlot != null && barrierSlot.isResonant)
        {
            TriggerKineticBladeWave();
        }
    }

    private void SpawnShieldRestoredVisual()
    {
        if (nanoBarrierVisualObj != null)
        {
            nanoBarrierVisualObj.SetActive(true);
        }
        SpawnShockwaveVisual(transform.position, 1.6f, new Color(0.2f, 0.9f, 1f, 0.75f));
    }

    private void TriggerKineticBladeWave()
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 4.2f * areaMult;
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, 200f, true);

            Vector2 pushDir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
            if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector2.right;
            Vector3 pushedPos = enemy.transform.position + (Vector3)(pushDir * 2.2f);

            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = pushedPos;
            }
            enemy.transform.position = pushedPos;
        }

        SpawnShockwaveVisual(transform.position, radius, new Color(0.2f, 0.9f, 1f, 0.85f));
    }

    // ====================================================================================================
    // 2. Toxic Spore (Trigger Collider ทรงกลม Lv.1 = 3m, Lv.2 = 5m, ดาเมจพิษ 15/s หรือ 30/s, Slow 20% ที่ Lv.2)
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

        // กำหนดขนาด Trigger Collider (lossyScale = 1) และ Visual ให้ตรงกับรัศมี 3m / 5m เป๊ะๆ
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
                // sprite diameter = 1 unit, localScale = diameter (radius * 2) -> visual radius = radius
                toxicSporeVisualChild.transform.localScale = Vector3.one * (radius * 2f);
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

                // ดาเมจพิษแบบ Periodic Damage (15/s หรือ 30/s)
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, damagePerHalfSec, false);
                PlayerStats.ApplyPoisonToTarget(enemy, damagePerHalfSec, 1.5f);

                // ปรับค่า MoveSpeed ของมอนสเตอร์ที่อยู่ใน Trigger ลง 20% ที่ Lv.2
                if (sporeSlot.level >= 2)
                {
                    ApplySlowToTarget(enemy, 0.20f, 0.7f);
                }
            }
        }
    }

    private void HandleEnemyDiedWithPoisonState(GameObject deadEnemy, Vector2 deathPosition, bool wasPoisoned)
    {
        if (!wasPoisoned || isChainSporeExploding) return;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        BoonSlotEntry sporeSlot = inv.GetBoonSlot(BoonItemType.ToxicSpore);
        if (sporeSlot == null || !sporeSlot.isResonant) return;

        isChainSporeExploding = true;
        try
        {
            float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
            float explosionRadius = 4.8f * areaMult;
            List<GameObject> nearbyEnemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(deathPosition, explosionRadius);

            for (int i = 0; i < nearbyEnemies.Count; i++)
            {
                GameObject target = nearbyEnemies[i];
                if (target == null || target == deadEnemy) continue;

                PlayerStats.ApplyPoisonToTarget(target, 35f, 3.5f);
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, 150f, false);
            }

            SpawnShockwaveVisual(deathPosition, explosionRadius, new Color(0.15f, 0.95f, 0.35f, 0.8f));
        }
        finally
        {
            isChainSporeExploding = false;
        }
    }

    // ====================================================================================================
    // 3. Flame Ring (ตรวจจับศัตรูในระยะวงแหวน ทำดาเมจ 25/s และ 50/s พร้อมดีบัฟ Armor ลดลง 15%)
    //    VFX ของวงแหวนไฟตรงกับระยะ Logic (Lv.1 = 3.5m, Lv.2 = 5.0m)
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
        float radius = baseRadius * areaMult;

        // สเกล VFX ให้เส้นรอบวงรอบนอกของวงแหวนไฟตรงกับรัศมีดาเมจ 3.5m / 5.0m
        if (flameRingZoneObj != null)
        {
            if (!flameRingZoneObj.activeSelf) flameRingZoneObj.SetActive(true);
            // sprite diameter = 1 unit, localScale = radius * 2 -> visual radius = radius
            flameRingZoneObj.transform.localScale = Vector3.one * (radius * 2f);
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
                if (enemy == null) continue;

                // ดีบัฟ Armor ลดลง 15%
                ApplyArmorReductionToTarget(enemy, 0.15f, 1.0f);

                // ทำดาเมจ 25/s (Lv.1) และ 50/s (Lv.2)
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, dmgPerHalfSec, false);
            }
        }

        // Resonance: Solar Core Ring
        if (flameSlot.isResonant)
        {
            solarCoreTimer += dt;
            if (solarCoreTimer >= 3.0f)
            {
                solarCoreTimer = 0f;
                TriggerSolarCoreDetonation();
            }
        }
    }

    private void TriggerSolarCoreDetonation()
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 5.0f * areaMult;
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, 80f, false);

            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist > 1.2f)
            {
                Vector3 pulledPos = Vector2.MoveTowards(enemy.transform.position, transform.position, 1.5f);
                Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null && rb.bodyType != RigidbodyType2D.Static)
                {
                    rb.position = pulledPos;
                }
                enemy.transform.position = pulledPos;
            }
        }

        SpawnShockwaveVisual(transform.position, radius, new Color(1f, 0.45f, 0.1f, 0.85f));
    }

    // ====================================================================================================
    // 4. Static Aura (Distance Tracker Vector2.Distance ครบ 5m/4m ผ่าสายฟ้า 40/70)
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

        // Distance Tracker ตรวจจับระยะทางที่ผู้เล่นเคลื่อนที่ด้วย Vector2.Distance
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
            TriggerLightningStrike(targetCount, damage);
        }

        // Resonance: Thunderstorm Domain
        if (staticSlot.isResonant)
        {
            thunderstormTimer += dt;
            if (thunderstormTimer >= 3.5f)
            {
                thunderstormTimer = 0f;
                TriggerThunderstormNova(5.0f);
            }
        }
    }

    private void TriggerLightningStrike(int targetCount, float damage)
    {
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, 8.5f);
        if (enemies.Count == 0) return;

        for (int i = 0; i < targetCount && enemies.Count > 0; i++)
        {
            int rIdx = Random.Range(0, enemies.Count);
            GameObject target = enemies[rIdx];
            enemies.RemoveAt(rIdx);

            if (target != null)
            {
                EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(target, damage, true);
                SpawnLightningArcVisual(transform.position, target.transform.position);
                SpawnShockwaveVisual(target.transform.position, 1.2f, new Color(0.95f, 0.95f, 0.25f, 0.95f));
            }
        }
    }

    private void TriggerThunderstormNova(float radius)
    {
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);
        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, 120f, false);
            PlayerStats.ApplyStunToTarget(enemy, 1.5f);
        }

        SpawnShockwaveVisual(transform.position, radius, new Color(0.4f, 0.8f, 1f, 0.9f));
    }

    // ====================================================================================================
    // 5. Magnetic Pulse (Coroutine/Timer ปล่อยคลื่น Pulse ทุก 4s / 2s สโลว์ศัตรูในระยะ)
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

        float interval = pulseSlot.level >= 2 ? 2.0f : 4.0f;
        magneticPulseTimer += dt;

        if (magneticPulseTimer >= interval)
        {
            magneticPulseTimer = 0f;
            float slowAmount = pulseSlot.level >= 2 ? 0.50f : 0.30f;
            TriggerMagneticPulseWave(slowAmount, pulseSlot.level >= 2);
        }
    }

    private void TriggerMagneticPulseWave(float slowAmount, bool canReflectProjectiles)
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 4.5f * areaMult;
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            ApplySlowToTarget(enemy, slowAmount, 1.5f);
        }

        if (canReflectProjectiles)
        {
            ReflectOrClearEnemyProjectiles(radius);
        }

        // ปล่อยคลื่น Pulse ขยายออกไปตามระยะ 4.5m
        StartCoroutine(AnimateExpandingPulse(radius, new Color(0.85f, 0.25f, 1f, 0.85f)));
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

    private void HandlePlayerDamaged(float damage)
    {
        // รีเซ็ตคูลดาวน์การฟื้นฟูเกราะเมื่อโดนโจมตี
        barrierRegenTimer = 0f;

        PlayerInventory inv = PlayerInventory.Instance;
        if (inv == null) return;

        BoonSlotEntry pulseSlot = inv.GetBoonSlot(BoonItemType.MagneticPulse);
        if (pulseSlot != null && pulseSlot.isResonant)
        {
            TriggerEventHorizonRetaliation();
        }
    }

    private void TriggerEventHorizonRetaliation()
    {
        float areaMult = playerStats != null ? playerStats.AreaSizeMultiplier : 1f;
        float radius = 4.5f * areaMult;
        List<GameObject> enemies = EvolvedWeaponBehaviors.GetActiveEnemiesInRadius(transform.position, radius);

        for (int i = 0; i < enemies.Count; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null) continue;

            EvolvedWeaponBehaviors.DealEvolvedDamageToEnemy(enemy, 150f, false);

            Vector2 pushDir = ((Vector2)enemy.transform.position - (Vector2)transform.position).normalized;
            if (pushDir.sqrMagnitude < 0.001f) pushDir = Vector2.right;
            Vector3 pushedPos = enemy.transform.position + (Vector3)(pushDir * 2.2f);

            Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null && rb.bodyType != RigidbodyType2D.Static)
            {
                rb.position = pushedPos;
            }
            enemy.transform.position = pushedPos;
        }

        SpawnShockwaveVisual(transform.position, radius, new Color(1f, 0.85f, 0.2f, 0.9f));
    }

    // ====================================================================================================
    // Visual Creation & Animation Helpers
    // ====================================================================================================
    private void CreateVisualHoldersAndZones()
    {
        // 1. Nano-Barrier Visual (รัศมี 0.75m หุ้มรอบ Player 0.5m)
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

        // 2. Toxic Spore Zone (Trigger Collider ทรงกลม + หมอกสปอร์สีเขียว)
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
            toxicSporeVisualChild.transform.localScale = Vector3.one * 6.0f; // diameter = 6.0m (radius = 3.0m)

            toxicSporeRenderer = toxicSporeVisualChild.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(toxicSporeRenderer, ProceduralSprites.CircleDisc);
            toxicSporeRenderer.sprite = ProceduralSprites.CircleDisc;
            toxicSporeRenderer.color = new Color(0.25f, 0.95f, 0.3f, 0.18f);
            toxicSporeRenderer.sortingOrder = 11;

            toxicSporeZoneObj.SetActive(false);
        }

        // 3. Flame Ring Zone (วงแหวนไฟรอบตัว รัศมีตรงกับ Logic)
        if (flameRingZoneObj == null)
        {
            flameRingZoneObj = new GameObject("Zone_Boon_FlameRing");
            flameRingZoneObj.transform.SetParent(transform, false);
            flameRingZoneObj.transform.localPosition = Vector3.zero;
            flameRingZoneObj.transform.localScale = Vector3.one * 7.0f; // diameter = 7.0m (radius = 3.5m)

            flameRingRenderer = flameRingZoneObj.AddComponent<SpriteRenderer>();
            ProceduralSprites.EnsureVisible(flameRingRenderer, ProceduralSprites.CircleRing);
            flameRingRenderer.sprite = ProceduralSprites.CircleRing;
            flameRingRenderer.color = new Color(1f, 0.45f, 0.05f, 0.42f);
            flameRingRenderer.sortingOrder = 12;
            flameRingZoneObj.SetActive(false);
        }

        // 4. Static Aura Zone (ประกายไฟฟ้าออร่ารอบตัวผู้เล่น)
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

        // 5. Magnetic Pulse Zone (วงแหวนแม่เหล็กรอบตัวผู้เล่น)
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
            float r = (flame.level >= 2 ? 5.0f : 3.5f) * areaMult;
            flameRingZoneObj.SetActive(true);
            flameRingZoneObj.transform.localScale = Vector3.one * (r * 2f);
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
        }
        else if (magneticPulseZoneObj != null)
        {
            magneticPulseZoneObj.SetActive(false);
        }
    }

    // ====================================================================================================
    // Utility Helpers
    // ====================================================================================================
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

        Destroy(waveObj, 0.22f);
    }

    private void SpawnLightningArcVisual(Vector3 startPos, Vector3 targetPos)
    {
        GameObject arcObj = new GameObject("VFX_Boon_LightningArc");
        arcObj.transform.position = (startPos + targetPos) * 0.5f;

        Vector2 diff = targetPos - startPos;
        float dist = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        arcObj.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        arcObj.transform.localScale = new Vector3(dist, 0.18f, 1f);

        SpriteRenderer sr = arcObj.AddComponent<SpriteRenderer>();
        ProceduralSprites.EnsureVisible(sr, ProceduralSprites.Shot);
        sr.sprite = ProceduralSprites.Shot;
        sr.color = new Color(0.95f, 0.95f, 0.35f, 0.95f);
        sr.sortingOrder = 19;

        Destroy(arcObj, 0.12f);
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
