using UnityEngine;

/// <summary>
/// ScriptableObject และระบบคำนวณค่าโบนัสของ Tech Passive ทั้ง 5 ชนิด (Max Lv.5) ตามตารางบาลานซ์ 4.2:
/// 1. Targeting Lens: Crit Rate +5% -> +25%, Crit Dmg +20% -> +100%
/// 2. Genetic Extractor: Pickup Radius +25% -> +125%, RP Bonus +10% -> +50%
/// 3. Overclock Chip: Cooldown Reduction +8% -> +40% (Cap รวมใน PlayerStats ไม่เกิน 60%)
/// 4. Bio-Fuel Tank: Attack Area Size +10% -> +50%
/// 5. Graviton Capacitor: Move Speed +8% -> +40%
/// </summary>
[CreateAssetMenu(fileName = "NewTechPassiveData", menuName = "RogueLite/Tech Passives/Tech Passive Data")]
public class TechPassiveData : ScriptableObject
{
    public const int MaxLevel = 5;

    [Header("Tech Passive Identity")]
    public TechPassiveType techType = TechPassiveType.TargetingLens;
    public string techName = "Targeting Lens";
    [TextArea(2, 4)] public string description = "Increases Critical Hit Rate and Critical Damage.";
    public WeaponType matchingWeapon = WeaponType.NanoBlade;
    public Sprite icon;

    [Header("Per-Level Bonus Scaling (Lv.1 -> Lv.5)")]
    public float primaryBonusPerLevel = 0.05f;
    public float secondaryBonusPerLevel = 0.20f;

    /// <summary>
    /// 1. Targeting Lens: Crit Rate +5% ต่อเลเวล (Lv.1 = +5% -> Lv.5 = +25%)
    /// </summary>
    public static float GetTargetingLensCritRateBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.05f;
    }

    /// <summary>
    /// 1. Targeting Lens: Crit Damage +20% ต่อเลเวล (Lv.1 = +20% -> Lv.5 = +100%)
    /// </summary>
    public static float GetTargetingLensCritDamageBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.20f;
    }

    /// <summary>
    /// 2. Genetic Extractor: Pickup Radius +25% ต่อเลเวล (Lv.1 = +25% -> Lv.5 = +125%)
    /// </summary>
    public static float GetGeneticExtractorPickupRadiusBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.25f;
    }

    /// <summary>
    /// 2. Genetic Extractor: RP Bonus +10% ต่อเลเวล (Lv.1 = +10% -> Lv.5 = +50%)
    /// </summary>
    public static float GetGeneticExtractorRpBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.10f;
    }

    /// <summary>
    /// 3. Overclock Chip: Cooldown Reduction +8% ต่อเลเวล (Lv.1 = +8% -> Lv.5 = +40%, Cap รวมไม่เกิน 60%)
    /// </summary>
    public static float GetOverclockChipCooldownReductionBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.08f;
    }

    /// <summary>
    /// 4. Bio-Fuel Tank: Attack Area Size +10% ต่อเลเวล (Lv.1 = +10% -> Lv.5 = +50%)
    /// </summary>
    public static float GetBioFuelTankAreaSizeBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.10f;
    }

    /// <summary>
    /// 5. Graviton Capacitor: Move Speed +8% ต่อเลเวล (Lv.1 = +8% -> Lv.5 = +40%)
    /// </summary>
    public static float GetGravitonCapacitorMoveSpeedBonus(int level)
    {
        return Mathf.Clamp(level, 0, MaxLevel) * 0.08f;
    }

    /// <summary>
    /// สร้างการ์ด LevelUpUpgradeData สำหรับ Tech Passive ตามเลเวลที่กำหนด (Lv.1 -> Lv.5)
    /// </summary>
    public static LevelUpUpgradeData CreateUpgradeCard(TechPassiveType type, int targetLevel)
    {
        int clampedLv = Mathf.Clamp(targetLevel, 1, MaxLevel);
        string prefix = clampedLv == 1 ? "NEW TECH: " : "";
        string title = $"{prefix}{PlayerInventory.GetTechName(type)} (Lv.{clampedLv})";
        string desc = PlayerInventory.GetTechUpgradeDescription(type, clampedLv);
        return LevelUpUpgradeData.CreateTechCard(type, clampedLv, title, desc);
    }
}
