using System;
using Enemies.Core;
using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    public static PlayerStats Instance { get; private set; }

    [Header("Base Stats")]
    [SerializeField] private float baseMaxHealth = 100f;
    [SerializeField] private float baseMoveSpeed = 4.0f;
    [SerializeField] private float baseDamage = 15f;
    [SerializeField] private float baseFireInterval = 0.25f;
    [SerializeField] private float basePickupRadius = 4f;

    [Header("Combat Stats (Crit & Cooldown)")]
    [SerializeField] private float baseCritChance = 0.05f;       // Base Crit Chance = 5%
    [SerializeField] private float baseCritMultiplier = 1.5f;    // Base Crit Multiplier = 1.5x
    [SerializeField] private float maxCooldownReduction = 0.60f; // Cooldown Reduction Cap สูงสุดไม่เกิน 60%

    [Header("Character Passive - Mind Shock (N-02 Psionic)")]
    [SerializeField] private bool hasMindShockPassive = false;
    [SerializeField] private float mindShockStunDuration = 1.0f; // คริติคอลติด Stun 1.0 วินาที

    [Header("Character Passive - Overclock Dash (N-03 Cyber-Swift)")]
    [SerializeField] private bool hasOverclockDashPassive = false;
    [SerializeField] private int maxDashCharges = 1; // N-03 Cyber-Swift มี 2 Dash Charges

    // In-Run Multipliers / Buffs (from Legacy Boons & Shop Upgrades)
    private float boonBonusMaxHealth = 0f;
    private float boonMoveSpeedPercent = 0f;
    private float boonDamagePercent = 0f;
    private float boonAttackSpeedPercent = 0f;
    private float boonPickupRadiusPercent = 0f;

    // Boon Bonuses (from the 5 new Boons: Kinetic Overdrive, Toxic Catalyst, Plasma Accelerator, Cryo Condenser, Nanite Aegis)
    private float boonAttackSpeedBonus = 0f;
    private float boonDoTDamageBonus = 0f;
    private float boonProjectileSpeedBonus = 0f;
    private float boonExplosionRadiusBonus = 0f;
    private float boonSlowPercentBonus = 0f;
    private float boonFreezeDurationBonus = 0f;
    private float boonArmorBonus = 0f;
    private float boonHPRegenBonus = 0f;

    // Inventory Slot Bonuses (from Tech Passives & Weapons)
    private float techCritChanceBonus = 0f;
    private float techCritDamageBonus = 0f;
    private float techPickupRadiusBonus = 0f;
    private float techRpBonusPercent = 0f;
    private float techCooldownReductionBonus = 0f;
    private float techAreaSizeBonus = 0f;
    private float techMoveSpeedBonus = 0f;
    private float weaponDamageBonusPercent = 0f;

    // Temporary Next-Room Buff: Overdrive Stim (Move Speed +15%, Damage +15% เฉพาะห้องถัดไป 1 ห้อง)
    private int queuedOverdriveStimCount = 0;
    private float activeOverdriveMoveSpeedBonus = 0f;
    private float activeOverdriveDamageBonus = 0f;

    // Cached Meta Levels & Character-Specific Skill Tree Bonuses (Balance Section 9)
    private int metaHpLevel = 0;
    private int metaSpeedLevel = 0;
    private int metaDamageLevel = 0;
    private int metaAttackRateLevel = 0;
    private SubjectCharacterType activeCharacterType = SubjectCharacterType.Vanguard_N01;
    private CharacterMetaBonuses characterMetaBonuses;

    public SubjectCharacterType ActiveCharacterType => activeCharacterType;
    public CharacterMetaBonuses MetaBonuses => characterMetaBonuses;
    public bool HasMindShockPassive => hasMindShockPassive;
    public float MindShockStunDuration => mindShockStunDuration + characterMetaBonuses.stunDurationBonusSeconds;
    public bool HasOverclockDashPassive => hasOverclockDashPassive;
    public int MaxDashCharges => Mathf.Max(1, maxDashCharges + characterMetaBonuses.bonusDashCharges);
    public float Armor => characterMetaBonuses.flatArmor + boonArmorBonus;
    public float ReflectArmorRatio => characterMetaBonuses.reflectArmorRatio;
    public float HPRegenPerSecond => characterMetaBonuses.hpRegenPerSecond + boonHPRegenBonus;
    public float KnockbackMultiplier => 1f + characterMetaBonuses.knockbackMultiplierBonus;
    public float DoTDamageMultiplier => 1f + characterMetaBonuses.dotDamageMultiplierBonus + boonDoTDamageBonus;
    public float ProjectileSpeedMultiplier => 1f + boonProjectileSpeedBonus;
    public float SlowPercentBonus => boonSlowPercentBonus;
    public float FreezeDurationBonus => boonFreezeDurationBonus;
    public float DashCooldownMultiplier => Mathf.Clamp(1f - characterMetaBonuses.dashCooldownReductionPercent, 0.4f, 1f);
    public float DashDistanceMultiplier => 1f + characterMetaBonuses.dashDistancePercentBonus;
    public float BonusIFrameDuration => characterMetaBonuses.iFrameDurationBonusSeconds;

    // Events
    public event Action OnStatsChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (GetComponent<PlayerInventory>() == null)
        {
            gameObject.AddComponent<PlayerInventory>();
        }

        if (GetComponent<BoonCombatController>() == null)
        {
            gameObject.AddComponent<BoonCombatController>();
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DebugCheats>() == null)
        {
            gameObject.AddComponent<DebugCheats>();
        }
#endif

        LoadMetaUpgrades();
    }

    private void Start()
    {
        ApplyCharacterBaseStats();
        PlayerInventory.GetOrCreate()?.EnsureStarterWeaponInitialized();

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.OnRoomStarted -= HandleRoomStartedForOverdriveStim;
            RoomManager.Instance.OnRoomStarted += HandleRoomStartedForOverdriveStim;
            RoomManager.Instance.OnRoomClearedEvent -= HandleRoomClearedForOverdriveStim;
            RoomManager.Instance.OnRoomClearedEvent += HandleRoomClearedForOverdriveStim;
        }
    }

    private void OnDestroy()
    {
        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.OnRoomStarted -= HandleRoomStartedForOverdriveStim;
            RoomManager.Instance.OnRoomClearedEvent -= HandleRoomClearedForOverdriveStim;
        }
    }

    /// <summary>
    /// เรียกใช้เมื่อซื้อ Overdrive Stim จากร้านค้า Black Market:
    /// บัฟ Move Speed +15% และ Damage +15% เฉพาะห้องถัดไป 1 ห้องเท่านั้น
    /// </summary>
    public void QueueOverdriveStimForNextRoom(float bonusPercent = 0.15f)
    {
        queuedOverdriveStimCount++;
        Debug.Log($"[PlayerStats] Overdrive Stim queued for next room! (+{bonusPercent * 100f:F0}% Move Speed & Damage for 1 room)");
    }

    private void HandleRoomStartedForOverdriveStim(int roomNumber, int floorNumber)
    {
        if (queuedOverdriveStimCount > 0)
        {
            activeOverdriveMoveSpeedBonus = queuedOverdriveStimCount * 0.15f;
            activeOverdriveDamageBonus = queuedOverdriveStimCount * 0.15f;
            queuedOverdriveStimCount = 0;
            Debug.Log($"[PlayerStats] ★ Overdrive Stim ACTIVE for Room {roomNumber}! MoveSpeed +{activeOverdriveMoveSpeedBonus * 100f:F0}%, Damage +{activeOverdriveDamageBonus * 100f:F0}%");
            OnStatsChanged?.Invoke();
        }
        else if (activeOverdriveMoveSpeedBonus > 0f || activeOverdriveDamageBonus > 0f)
        {
            activeOverdriveMoveSpeedBonus = 0f;
            activeOverdriveDamageBonus = 0f;
            OnStatsChanged?.Invoke();
        }
    }

    private void HandleRoomClearedForOverdriveStim()
    {
        if (activeOverdriveMoveSpeedBonus > 0f || activeOverdriveDamageBonus > 0f)
        {
            activeOverdriveMoveSpeedBonus = 0f;
            activeOverdriveDamageBonus = 0f;
            Debug.Log("[PlayerStats] Overdrive Stim expired after clearing the room.");
            OnStatsChanged?.Invoke();
        }
    }

    private void ApplyCharacterBaseStats()
    {
        SpriteRenderer playerSprite = GetComponentInChildren<SpriteRenderer>();
        if (playerSprite != null) playerSprite.sprite = ProceduralSprites.Agent;

        if (CharacterSelectionManager.Instance != null && CharacterSelectionManager.Instance.CurrentSelectedCharacter != null)
        {
            var character = CharacterSelectionManager.Instance.CurrentSelectedCharacter;
            string combinedId = $"{character.characterID} {character.characterName}";
            bool isPsionic = character.hasMindShockPassive || CharacterSelectionManager.IsPsionicCharacter(character.characterIndex, combinedId);
            bool isCyberSwift = !isPsionic && (character.hasOverclockDashPassive || CharacterSelectionManager.IsCyberSwiftCharacter(character.characterIndex, combinedId) || character.startingWeapon == WeaponType.PlasmaBlaster);

            if (isPsionic)
            {
                activeCharacterType = SubjectCharacterType.Psionic_N02;
                baseMaxHealth = 100f;
                baseMoveSpeed = 4.0f;
                baseDamage = 15f;
                hasMindShockPassive = true;
                hasOverclockDashPassive = false;
                maxDashCharges = 1;
            }
            else if (isCyberSwift)
            {
                activeCharacterType = SubjectCharacterType.CyberSwift_N03;
                baseMaxHealth = 130f;
                baseMoveSpeed = 4.8f;
                baseDamage = 10f;
                hasMindShockPassive = false;
                hasOverclockDashPassive = true;
                maxDashCharges = 2;
            }
            else
            {
                activeCharacterType = SubjectCharacterType.Vanguard_N01;
                if (character.maxHP > 0) baseMaxHealth = character.maxHP;
                if (character.speed > 0) baseMoveSpeed = character.speed;
                if (character.damage > 0) baseDamage = character.damage;
                hasMindShockPassive = false;
                hasOverclockDashPassive = false;
                maxDashCharges = Mathf.Max(1, character.maxDashCharges);
            }

            // โหลดบัฟจากผัง Skill Tree ของตัวละครที่เลือกทันทีตอนเริ่มรันใหม่
            LoadMetaUpgrades();

            // ซิงค์ค่าจำนวน Dash Charges และสกิล Overclock Dash ไปยัง PlayerController
            PlayerController controller = GetComponent<PlayerController>();
            if (controller != null)
            {
                controller.ConfigureDashSettings(MaxDashCharges, hasOverclockDashPassive);
            }

            if (playerSprite != null)
            {
                playerSprite.color = character.characterColor;
            }

            // อัปเดตเลือดใน PlayerHealth ให้เท่ากับ MaxHealth ใหม่ทันที
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.SetMaxHealthFromStats(MaxHealth);
            }

            OnStatsChanged?.Invoke();
        }
        else
        {
            LoadMetaUpgrades();
            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.SetMaxHealthFromStats(MaxHealth);
            }
        }
    }

    public void LoadMetaUpgrades()
    {
        GameData save = SaveSystem.LoadGame();
        if (save != null)
        {
            metaHpLevel = save.hpUpgradeLevel;
            metaSpeedLevel = save.speedUpgradeLevel;
            metaDamageLevel = save.damageUpgradeLevel;
            metaAttackRateLevel = save.attackRateUpgradeLevel;
        }

        characterMetaBonuses = ResearchTreeManager.CalculateCharacterMetaBonuses(activeCharacterType);

        Debug.Log($"[PlayerStats Meta] Character: {activeCharacterType} | MaxHP: {MaxHealth:F0}, Speed: {MoveSpeed:F2}, Armor: {Armor:F0}, Crit: {CritChance * 100f:F0}%, DoT: x{DoTDamageMultiplier:F2}, Stun: {MindShockStunDuration:F2}s, DashCharges: {MaxDashCharges}");

        OnStatsChanged?.Invoke();
    }

    public void SetInventoryBonuses(
        float critChance,
        float critDamage,
        float pickupRadius,
        float rpBonus,
        float cooldownReduction,
        float areaSize,
        float moveSpeed,
        float weaponDamage)
    {
        techCritChanceBonus = critChance;
        techCritDamageBonus = critDamage;
        techPickupRadiusBonus = pickupRadius;
        techRpBonusPercent = rpBonus;
        techCooldownReductionBonus = cooldownReduction;
        techAreaSizeBonus = areaSize;
        techMoveSpeedBonus = moveSpeed;
        weaponDamageBonusPercent = weaponDamage;

        OnStatsChanged?.Invoke();
    }

    public void SetBoonBonuses(
        float attackSpeed,
        float dotDamage,
        float projectileSpeed,
        float explosionRadius,
        float slowPercent,
        float freezeDuration,
        float armor,
        float hpRegen)
    {
        boonAttackSpeedBonus = attackSpeed;
        boonDoTDamageBonus = dotDamage;
        boonProjectileSpeedBonus = projectileSpeed;
        boonExplosionRadiusBonus = explosionRadius;
        boonSlowPercentBonus = slowPercent;
        boonFreezeDurationBonus = freezeDuration;
        boonArmorBonus = armor;
        boonHPRegenBonus = hpRegen;

        OnStatsChanged?.Invoke();
    }

    #region Combat & Passive Helpers (Crit & Mind Shock)

    /// <summary>
    /// คำนวณความเสียหายร่วมกับโบนัสพลังโจมตี (เช่น Overdrive Stim +15%) และโอกาสติด Critical Hit
    /// </summary>
    public float RollCriticalDamage(float rawDamage, out bool isCrit, bool forceCrit = false)
    {
        float scaledDamage = rawDamage * DamageMultiplier;
        isCrit = forceCrit || (UnityEngine.Random.value < CritChance);
        return isCrit ? (scaledDamage * CritMultiplier) : scaledDamage;
    }

    /// <summary>
    /// เรียกใช้เมื่อโจมตีโดนศัตรู: หากผู้เล่นเป็น N-02 (Psionic) และโจมตีติด Critical Hit จะทำให้ศัตรูติด Stun (Mind Shock + Skill Tree Stun Duration)
    /// </summary>
    public void TryApplyMindShockOnCrit(GameObject targetObj, bool isCrit)
    {
        if (!isCrit || !hasMindShockPassive || targetObj == null) return;

        float totalStun = MindShockStunDuration;
        ApplyStunToTarget(targetObj, totalStun);

        // Major 1 (Mind Collapse): ศัตรูที่ติด Stun โดนความเสียหายแรงขึ้น 25%
        if (characterMetaBonuses.hasMindCollapseMajor)
        {
            EnemyHealth enemyHealth = targetObj.GetComponent<EnemyHealth>() ?? targetObj.GetComponentInChildren<EnemyHealth>() ?? targetObj.GetComponentInParent<EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.ApplyArmorReduction(0.25f, totalStun + 0.5f);
            }
        }
    }

    public static void ApplyStunToTarget(GameObject targetObj, float duration)
    {
        if (targetObj == null || duration <= 0f) return;

        EnemyHealth enemyHealth = targetObj.GetComponent<EnemyHealth>() ?? targetObj.GetComponentInChildren<EnemyHealth>() ?? targetObj.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.ApplyStun(duration);
        }

        EnemyBase enemyBase = targetObj.GetComponent<EnemyBase>() ?? targetObj.GetComponentInParent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.ApplyStun(duration);
        }

        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? targetObj.GetComponentInChildren<EnemyAI>() ?? targetObj.GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.ApplyStun(duration);
        }
    }

    public static void ApplyPoisonToTarget(GameObject targetObj, float dps, float duration = 3.0f)
    {
        if (targetObj == null || dps <= 0f || duration <= 0f) return;

        float dotMult = Instance != null ? Instance.DoTDamageMultiplier : 1f;
        float finalDps = dps * dotMult;

        EnemyHealth enemyHealth = targetObj.GetComponent<EnemyHealth>() ?? targetObj.GetComponentInChildren<EnemyHealth>() ?? targetObj.GetComponentInParent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.ApplyPoison(finalDps, duration);
        }

        EnemyAI enemyAI = targetObj.GetComponent<EnemyAI>() ?? targetObj.GetComponentInChildren<EnemyAI>() ?? targetObj.GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.ApplyPoison(finalDps, duration);
        }
    }

    #endregion

    #region Computed Stat Properties

    public float MaxHealth
    {
        get
        {
            float scaledBase = baseMaxHealth * (1f + characterMetaBonuses.maxHpPercentBonus);
            return scaledBase + characterMetaBonuses.flatMaxHpBonus + boonBonusMaxHealth;
        }
    }

    public float MoveSpeed
    {
        get
        {
            return baseMoveSpeed * (1f + characterMetaBonuses.moveSpeedPercentBonus + boonMoveSpeedPercent + techMoveSpeedBonus + activeOverdriveMoveSpeedBonus);
        }
    }

    public float Damage
    {
        get
        {
            return baseDamage * (1f + characterMetaBonuses.damagePercentBonus + boonDamagePercent + weaponDamageBonusPercent + activeOverdriveDamageBonus);
        }
    }

    public float DamageMultiplier => 1f + characterMetaBonuses.damagePercentBonus + boonDamagePercent + activeOverdriveDamageBonus;

    public float CritChance => Mathf.Clamp01(baseCritChance + characterMetaBonuses.critChanceBonus + techCritChanceBonus);

    public float CritMultiplier => baseCritMultiplier + characterMetaBonuses.critDamageBonus + techCritDamageBonus;

    public float MaxCooldownReduction => maxCooldownReduction;

    public float CooldownReduction
    {
        get
        {
            float rawReduction = characterMetaBonuses.attackSpeedCooldownBonus + boonAttackSpeedPercent + boonAttackSpeedBonus + techCooldownReductionBonus;
            return Mathf.Clamp(rawReduction, 0f, maxCooldownReduction);
        }
    }

    public float FireInterval
    {
        get
        {
            return baseFireInterval * (1f - CooldownReduction);
        }
    }

    public float PickupRadius
    {
        get
        {
            return basePickupRadius * (1f + boonPickupRadiusPercent + techPickupRadiusBonus);
        }
    }

    public float AreaSizeMultiplier => 1f + characterMetaBonuses.aoeSizePercentBonus + techAreaSizeBonus + boonExplosionRadiusBonus;

    public float RPBonusMultiplier => 1f + techRpBonusPercent;

    #endregion

    #region Upgrades & Boons Application

    private float ResolveHealthBonus(float value)
    {
        float resolved = value <= 0f ? 15f : value;
        return resolved <= 1f ? resolved * 100f : resolved;
    }

    private void AddMaxHealth(float amount)
    {
        boonBonusMaxHealth += amount;
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (health != null) health.IncreaseMaxHealth(amount);
    }

    public void ApplyBoon(BoonData boon)
    {
        if (boon == null) return;

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (boon.isSlotBoon && inventory != null)
        {
            inventory.AddOrUpgradeBoon(boon.boonItemType);
            return;
        }

        // ถ้าค่า value เป็น 0 ให้ใช้ค่าการอัปเกรดมาตรฐาน ( Default = 15% หรือ 0.15 )
        float addValue = boon.value <= 0f ? 15f : boon.value;
        float percentVal = addValue > 1f ? addValue / 100f : addValue;

        inventory?.AddOrUpgradeBoon(boon.boonItemType);

        Debug.Log($"[PlayerStats] Applied Boon: {boon.boonTitle} | Damage: {Damage:F1}, Speed: {MoveSpeed:F1}, FireInterval: {FireInterval:F2}s");
        OnStatsChanged?.Invoke();
    }

    public void ApplyLevelUpgrade(LevelUpUpgradeData upgrade)
    {
        if (upgrade == null) return;

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory != null)
        {
            switch (upgrade.category)
            {
                case UpgradeCategory.Weapon:
                    inventory.AddOrUpgradeWeapon(upgrade.weaponType);
                    return;

                case UpgradeCategory.TechPassive:
                    inventory.AddOrUpgradeTech(upgrade.techType);
                    return;

                case UpgradeCategory.Boon:
                    inventory.AddOrUpgradeBoon(upgrade.boonItemType);
                    return;
            }
        }

        // Legacy Stat Fallback (สำหรับ Shop / การ์ดสเตตัสเดิม)
        float addValue = upgrade.value <= 0f ? 15f : upgrade.value;
        float percentVal = addValue > 1f ? addValue / 100f : addValue;

        switch (upgrade.upgradeType)
        {
            case LevelUpUpgradeType.IncreaseDamage:
                boonDamagePercent += percentVal;
                break;

            case LevelUpUpgradeType.IncreaseMaxHealth:
                AddMaxHealth(ResolveHealthBonus(upgrade.value));
                break;

            case LevelUpUpgradeType.IncreaseMoveSpeed:
                boonMoveSpeedPercent += percentVal;
                break;

            case LevelUpUpgradeType.IncreaseAttackSpeed:
                boonAttackSpeedPercent += percentVal;
                break;

            case LevelUpUpgradeType.IncreasePickupRange:
                boonPickupRadiusPercent += percentVal;
                break;

            case LevelUpUpgradeType.HealPlayer:
                PlayerHealth healTarget = GetComponent<PlayerHealth>();
                if (healTarget != null)
                {
                    healTarget.Heal(addValue <= 1f ? 25f : addValue);
                }
                break;
        }

        Debug.Log($"[PlayerStats] Applied Level Upgrade: {upgrade.upgradeTitle} | Damage: {Damage:F1}, Speed: {MoveSpeed:F1}, FireInterval: {FireInterval:F2}s");
        OnStatsChanged?.Invoke();
    }

    #endregion
}
