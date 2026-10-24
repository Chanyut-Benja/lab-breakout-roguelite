using UnityEngine;

public enum UpgradeCategory
{
    LegacyStat = 0,
    Weapon = 1,
    TechPassive = 2,
    Boon = 3
}

public enum LevelUpUpgradeType
{
    IncreaseDamage,
    IncreaseMaxHealth,
    IncreaseMoveSpeed,
    IncreaseAttackSpeed,
    IncreasePickupRange,
    HealPlayer
}

[CreateAssetMenu(fileName = "NewUpgradeData", menuName = "Progression/LevelUp Upgrade Data")]
public class LevelUpUpgradeData : ScriptableObject
{
    [SerializeField] private string id;
    public string upgradeTitle;
    [TextArea(2, 4)] public string description;
    public UpgradeCategory category = UpgradeCategory.LegacyStat;

    [Header("Slot Item Target (Weapon / Tech / Boon)")]
    public WeaponType weaponType;
    public TechPassiveType techType;
    public BoonItemType boonItemType;
    public int targetLevel = 1;

    [Header("Legacy Stat Fallback")]
    public LevelUpUpgradeType upgradeType;
    public float value;
    public Sprite icon;

    public string Id
    {
        get
        {
            if (!string.IsNullOrEmpty(id)) return id;
            switch (category)
            {
                case UpgradeCategory.Weapon:
                    return $"weapon_{weaponType}";
                case UpgradeCategory.TechPassive:
                    return $"tech_{techType}";
                case UpgradeCategory.Boon:
                    return $"boon_{boonItemType}";
                default:
                    return string.IsNullOrEmpty(upgradeTitle) ? name : upgradeTitle;
            }
        }
        set => id = value;
    }

    public static LevelUpUpgradeData Create(string title, string desc, LevelUpUpgradeType type, float val)
    {
        LevelUpUpgradeData data = CreateInstance<LevelUpUpgradeData>();
        data.category = UpgradeCategory.LegacyStat;
        data.upgradeTitle = title;
        data.description = desc;
        data.upgradeType = type;
        data.value = val;
        data.id = $"stat_{type}";
        return data;
    }

    public static LevelUpUpgradeData CreateWeaponCard(WeaponType weapon, int level, string title, string desc)
    {
        LevelUpUpgradeData data = CreateInstance<LevelUpUpgradeData>();
        data.category = UpgradeCategory.Weapon;
        data.weaponType = weapon;
        data.targetLevel = level;
        data.upgradeTitle = title;
        data.description = desc;
        data.icon = PlayerInventory.GetWeaponIcon(weapon);
        data.id = $"weapon_{weapon}";
        return data;
    }

    public static LevelUpUpgradeData CreateTechCard(TechPassiveType tech, int level, string title, string desc)
    {
        LevelUpUpgradeData data = CreateInstance<LevelUpUpgradeData>();
        data.category = UpgradeCategory.TechPassive;
        data.techType = tech;
        data.targetLevel = level;
        data.upgradeTitle = title;
        data.description = desc;
        data.icon = PlayerInventory.GetTechPassiveIcon(tech);
        data.id = $"tech_{tech}";
        return data;
    }

    public static LevelUpUpgradeData CreateBoonCard(BoonItemType boon, int level, string title, string desc)
    {
        LevelUpUpgradeData data = CreateInstance<LevelUpUpgradeData>();
        data.category = UpgradeCategory.Boon;
        data.boonItemType = boon;
        data.targetLevel = level;
        data.upgradeTitle = title;
        data.description = desc;
        data.icon = PlayerInventory.GetBoonIcon(boon);
        data.id = $"boon_{boon}";
        return data;
    }
}
