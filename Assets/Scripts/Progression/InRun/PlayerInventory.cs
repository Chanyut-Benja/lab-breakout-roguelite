using System;
using System.Collections.Generic;
using UnityEngine;

public enum SynergyLine
{
    NanoArmor = 0,      // สาย 1: นาโน / เกราะ
    BioSpore = 1,       // สาย 2: พิษ / สปอร์
    PlasmaFire = 2,     // สาย 3: พลาสม่า / ไฟ
    TeslaLightning = 3, // สาย 4: สายฟ้า
    GravityVoid = 4     // สาย 5: แรงโน้มถ่วง
}

public enum WeaponType
{
    NanoBlade = 0,
    BioNeedler = 1,
    PlasmaBlaster = 2,
    TeslaOrb = 3,
    GravityGrenadier = 4
}

public enum EvolvedWeaponType
{
    None = -1,
    ExecutionerMatrix = 0,
    PlagueSporeSpreader = 1,
    SupernovaCannon = 2,
    StormCitadel = 3,
    SingularityCannon = 4
}

public enum TechPassiveType
{
    TargetingLens = 0,
    GeneticExtractor = 1,
    OverclockChip = 2,
    BioFuelTank = 3,
    GravitonCapacitor = 4
}

public enum BoonItemType
{
    NanoBarrier = 0,
    ToxicSpore = 1,
    FlameRing = 2,
    StaticAura = 3,
    MagneticPulse = 4,

    // Backward-compatibility aliases
    KineticOverdrive = NanoBarrier,
    ToxicCatalyst = ToxicSpore,
    PlasmaAccelerator = FlameRing,
    CryoCondenser = StaticAura,
    NaniteAegis = MagneticPulse
}

public enum ResonantBoonType
{
    None = -1,
    BladeShieldMatrix = 0,
    EpidemicOutbreak = 1,
    SolarCoreRing = 2,
    ThunderstormDomain = 3,
    EventHorizonVoid = 4
}

[Serializable]
public class WeaponSlotEntry
{
    public WeaponType weaponType;
    public int level = 1; // Max Lv.5
    public bool isStarterWeapon = false;
    public bool isEvolved = false;
    public EvolvedWeaponType evolvedForm = EvolvedWeaponType.None;
}

[Serializable]
public class TechSlotEntry
{
    public TechPassiveType techType;
    public int level = 1; // Max Lv.5
}

[Serializable]
public class BoonSlotEntry
{
    public BoonItemType boonType;
    public int level = 1; // Max Lv.2
    public bool isResonant = false;
    public ResonantBoonType resonantForm = ResonantBoonType.None;
}

public class PlayerInventory : MonoBehaviour
{
    public static PlayerInventory Instance { get; private set; }

    public const int MaxWeaponSlots = 3;
    public const int MaxTechSlots = 3;
    public const int MaxBoonSlots = 3;

    public const int MaxWeaponLevel = 5;
    public const int MaxTechLevel = 5;
    public const int MaxBoonLevel = 2;

    [Header("Inventory Slots (3 Weapons, 3 Techs, 3 Boons)")]
    [SerializeField] private List<WeaponSlotEntry> weapons = new List<WeaponSlotEntry>();
    [SerializeField] private List<TechSlotEntry> techPassives = new List<TechSlotEntry>();
    [SerializeField] private List<BoonSlotEntry> boons = new List<BoonSlotEntry>();

    [Header("Evolution Cores")]
    [SerializeField] private int evolutionCores = 0;

    [Header("Reroll Tokens (Black Market & Progression)")]
    public int RerollCount { get; private set; } = 0;
    public int BanishCount { get; private set; } = 0;

    // Banished items (ตัดการ์ดทิ้งตลอดรันด้วย Banish Token)
    private readonly HashSet<WeaponType> banishedWeapons = new HashSet<WeaponType>();
    private readonly HashSet<TechPassiveType> banishedTechs = new HashSet<TechPassiveType>();
    private readonly HashSet<BoonItemType> banishedBoons = new HashSet<BoonItemType>();
    private readonly HashSet<string> banishedItemIds = new HashSet<string>();

    public IReadOnlyList<WeaponSlotEntry> Weapons => weapons;
    public IReadOnlyList<TechSlotEntry> TechPassives => techPassives;
    public IReadOnlyList<BoonSlotEntry> Boons => boons;
    public int EvolutionCores => evolutionCores;

    public void ClearBoonsForTesting()
    {
        boons.Clear();
        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
    }

    public bool IsWeaponBanished(WeaponType type) => banishedWeapons.Contains(type);
    public bool IsTechBanished(TechPassiveType type) => banishedTechs.Contains(type);
    public bool IsBoonBanished(BoonItemType type) => banishedBoons.Contains(type);
    public bool IsItemBanished(string itemId) => !string.IsNullOrEmpty(itemId) && banishedItemIds.Contains(itemId);

    public void BanishItem(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        banishedItemIds.Add(itemId);

        if (itemId.StartsWith("weapon_") && Enum.TryParse<WeaponType>(itemId.Substring("weapon_".Length), out var wType))
        {
            banishedWeapons.Add(wType);
        }
        else if (itemId.StartsWith("tech_") && Enum.TryParse<TechPassiveType>(itemId.Substring("tech_".Length), out var tType))
        {
            banishedTechs.Add(tType);
        }
        else if (itemId.StartsWith("boon_") && Enum.TryParse<BoonItemType>(itemId.Substring("boon_".Length), out var bType))
        {
            banishedBoons.Add(bType);
        }
    }

    public void BanishCard(LevelUpUpgradeData card)
    {
        if (card == null) return;
        if (!string.IsNullOrEmpty(card.Id))
        {
            banishedItemIds.Add(card.Id);
        }

        switch (card.category)
        {
            case UpgradeCategory.Weapon:
                banishedWeapons.Add(card.weaponType);
                Debug.Log($"[PlayerInventory] Banished Weapon for this run: {GetWeaponName(card.weaponType)}");
                break;
            case UpgradeCategory.TechPassive:
                banishedTechs.Add(card.techType);
                Debug.Log($"[PlayerInventory] Banished Tech Passive for this run: {GetTechName(card.techType)}");
                break;
            case UpgradeCategory.Boon:
                banishedBoons.Add(card.boonItemType);
                Debug.Log($"[PlayerInventory] Banished Boon for this run: {GetBoonName(card.boonItemType)}");
                break;
        }
    }

    public event Action OnInventoryChanged;
    public event Action<WeaponSlotEntry> OnWeaponAdded;
    public event Action<WeaponSlotEntry> OnWeaponUpgraded;
    public event Action<WeaponSlotEntry> OnWeaponEvolved;
    public event Action<TechSlotEntry> OnTechPassiveAdded;
    public event Action<TechSlotEntry> OnTechPassiveUpgraded;
    public event Action<BoonSlotEntry> OnBoonAdded;
    public event Action<BoonSlotEntry> OnBoonUpgraded;
    public event Action<BoonSlotEntry> OnBoonResonated;
    public event Action<int> OnEvolutionCoreCountChanged;
    public event Action<int> OnRerollCountChanged;
    public event Action<int> OnBanishCountChanged;

    public static event Action<WeaponSlotEntry> OnWeaponAddedStatic;
    public static event Action<WeaponSlotEntry> OnWeaponUpgradedStatic;
    public static event Action<WeaponSlotEntry> OnWeaponEvolvedStatic;
    public static event Action<TechSlotEntry> OnTechPassiveAddedStatic;
    public static event Action<TechSlotEntry> OnTechPassiveUpgradedStatic;
    public static event Action<BoonSlotEntry> OnBoonAddedStatic;
    public static event Action<BoonSlotEntry> OnBoonUpgradedStatic;
    public static event Action<BoonSlotEntry> OnBoonResonatedStatic;
    public static event Action<int> OnEvolutionCoreCountChangedStatic;
    public static event Action<int> OnRerollCountChangedStatic;
    public static event Action<int> OnBanishCountChangedStatic;

    public static PlayerInventory GetOrCreate()
    {
        if (Instance != null) return Instance;

        if (PlayerStats.Instance != null)
        {
            PlayerInventory inv = PlayerStats.Instance.GetComponent<PlayerInventory>();
            if (inv == null) inv = PlayerStats.Instance.gameObject.AddComponent<PlayerInventory>();
            return inv;
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            PlayerInventory inv = player.GetComponent<PlayerInventory>();
            if (inv == null) inv = player.AddComponent<PlayerInventory>();
            return inv;
        }

        return null;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void Start()
    {
        EnsureStarterWeaponInitialized();
    }

    public void EnsureStarterWeaponInitialized()
    {
        if (weapons.Count > 0) return;

        WeaponType starter = ResolveCharacterStartingWeapon();

        weapons.Add(new WeaponSlotEntry
        {
            weaponType = starter,
            level = 1,
            isStarterWeapon = true,
            isEvolved = false,
            evolvedForm = EvolvedWeaponType.None
        });

        Debug.Log($"[PlayerInventory] Initialized Starting Weapon Slot [0]: {GetWeaponName(starter)} (Lv.1)");
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
    }

    public static WeaponType ResolveCharacterStartingWeapon()
    {
        if (CharacterSelectionManager.Instance != null && CharacterSelectionManager.Instance.CurrentSelectedCharacter != null)
        {
            var selected = CharacterSelectionManager.Instance.CurrentSelectedCharacter;
            return CharacterSelectionManager.ResolveStartingWeapon(
                selected.characterIndex,
                $"{selected.characterID} {selected.characterName}");
        }

        // ค่าเริ่มต้นคือ N-01 Vanguard -> Nano-Blade
        return WeaponType.NanoBlade;
    }

    #region Synergy Mapping Tables (Section 4 & 5)

    public static SynergyLine GetSynergyLine(WeaponType weapon) => (SynergyLine)((int)weapon);
    public static SynergyLine GetSynergyLine(TechPassiveType tech) => (SynergyLine)((int)tech);
    public static SynergyLine GetSynergyLine(BoonItemType boon) => (SynergyLine)((int)boon);

    public static TechPassiveType GetMatchingTech(WeaponType weapon) => (TechPassiveType)((int)weapon);
    public static WeaponType GetMatchingWeapon(TechPassiveType tech) => (WeaponType)((int)tech);
    public static WeaponType GetMatchingWeapon(BoonItemType boon) => (WeaponType)((int)boon);
    public static BoonItemType GetMatchingBoon(WeaponType weapon) => (BoonItemType)((int)weapon);

    public static EvolvedWeaponType GetEvolvedWeaponType(WeaponType weapon) => (EvolvedWeaponType)((int)weapon);
    public static ResonantBoonType GetResonantBoonType(BoonItemType boon) => (ResonantBoonType)((int)boon);

    public static string GetWeaponName(WeaponType type) => type switch
    {
        WeaponType.NanoBlade => "Nano-Blade",
        WeaponType.BioNeedler => "Bio-Needler",
        WeaponType.PlasmaBlaster => "Plasma Blaster",
        WeaponType.TeslaOrb => "Tesla Orb",
        WeaponType.GravityGrenadier => "Gravity Grenadier",
        _ => type.ToString()
    };

    private static readonly Dictionary<WeaponType, Sprite> cachedWeaponIcons = new Dictionary<WeaponType, Sprite>();

    /// <summary>
    /// ดึงไอคอน 2D Sprite ของอาวุธตาม WeaponType จาก WeaponsData หรือ Assets/Sprites/Icons/Weapons/
    /// </summary>
    public static Sprite GetWeaponIcon(WeaponType type)
    {
        if (cachedWeaponIcons.TryGetValue(type, out Sprite s) && s != null)
            return s;

        // 1. ดึงจาก WeaponsData ScriptableObject
        if (WeaponsData.Instance != null)
        {
            Sprite wIcon = WeaponsData.Instance.GetIcon(type);
            if (wIcon != null)
            {
                cachedWeaponIcons[type] = wIcon;
                return wIcon;
            }
        }

        // 2. ดึงจาก Resources
        string iconName = type switch
        {
            WeaponType.NanoBlade => "Icon_NanoBlade",
            WeaponType.BioNeedler => "Icon_BioNeedler",
            WeaponType.PlasmaBlaster => "Icon_PlasmaBlaster",
            WeaponType.TeslaOrb => "Icon_TeslaOrb",
            WeaponType.GravityGrenadier => "Icon_GravityGrenadier",
            _ => null
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            Sprite rSprite = Resources.Load<Sprite>($"Icons/Weapons/{iconName}");
            if (rSprite == null) rSprite = Resources.Load<Sprite>(iconName);
#if UNITY_EDITOR
            if (rSprite == null)
            {
                rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Weapons/{iconName}.png");
                if (rSprite == null)
                    rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Weapons/{iconName}.jpg");
            }
#endif
            if (rSprite != null)
            {
                cachedWeaponIcons[type] = rSprite;
                return rSprite;
            }
        }

        return null;
    }

    private static readonly Dictionary<EvolvedWeaponType, Sprite> cachedEvolvedIcons = new Dictionary<EvolvedWeaponType, Sprite>();

    /// <summary>
    /// ดึงไอคอน 2D Sprite ของอาวุธ Evolved ตาม EvolvedWeaponType จาก Assets/Sprites/Icons/Evolution/ หรือ Resources
    /// </summary>
    public static Sprite GetEvolvedWeaponIcon(EvolvedWeaponType type)
    {
        if (cachedEvolvedIcons.TryGetValue(type, out Sprite s) && s != null)
            return s;

        // 1. ดึงจาก EvolutionDatabase ScriptableObject ถ้ามี
        if (EvolutionDatabase.Instance != null)
        {
            Sprite evoSprite = EvolutionDatabase.Instance.GetIcon(type);
            if (evoSprite != null)
            {
                cachedEvolvedIcons[type] = evoSprite;
                return evoSprite;
            }
        }

        string iconName = type switch
        {
            EvolvedWeaponType.ExecutionerMatrix => "Icon_ExecutionerMatrix",
            EvolvedWeaponType.PlagueSporeSpreader => "Icon_PlagueSporeSpreader",
            EvolvedWeaponType.SupernovaCannon => "Icon_SupernovaCannon",
            EvolvedWeaponType.StormCitadel => "Icon_StormCitadel",
            EvolvedWeaponType.SingularityCannon => "Icon_SingularityCannon",
            _ => null
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            Sprite rSprite = Resources.Load<Sprite>($"Icons/Evolution/{iconName}");
            if (rSprite == null) rSprite = Resources.Load<Sprite>(iconName);
#if UNITY_EDITOR
            if (rSprite == null)
            {
                rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Evolution/{iconName}.png");
                if (rSprite == null)
                    rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Evolution/{iconName}.jpg");
            }
#endif
            if (rSprite != null)
            {
                cachedEvolvedIcons[type] = rSprite;
                return rSprite;
            }
        }

        return null;
    }

    private static readonly Dictionary<TechPassiveType, Sprite> cachedTechIcons = new Dictionary<TechPassiveType, Sprite>();

    /// <summary>
    /// ดึงไอคอน 2D Sprite ของ Tech Passive ตาม TechPassiveType จาก TechPassivesData หรือ Assets/Sprites/Icons/TechPassives/
    /// </summary>
    public static Sprite GetTechPassiveIcon(TechPassiveType type)
    {
        if (cachedTechIcons.TryGetValue(type, out Sprite s) && s != null)
            return s;

        // 1. ดึงจาก TechPassivesData ScriptableObject
        if (TechPassivesData.Instance != null)
        {
            Sprite tIcon = TechPassivesData.Instance.GetIcon(type);
            if (tIcon != null)
            {
                cachedTechIcons[type] = tIcon;
                return tIcon;
            }
        }

        // 2. ดึงจาก Resources
        string iconName = type switch
        {
            TechPassiveType.TargetingLens => "Icon_TargetingLens",
            TechPassiveType.GeneticExtractor => "Icon_GeneticExtractor",
            TechPassiveType.OverclockChip => "Icon_OverclockChip",
            TechPassiveType.BioFuelTank => "Icon_BioFuelTank",
            TechPassiveType.GravitonCapacitor => "Icon_GravitonCapacitor",
            _ => null
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            Sprite rSprite = Resources.Load<Sprite>($"Icons/TechPassives/{iconName}");
            if (rSprite == null) rSprite = Resources.Load<Sprite>(iconName);
#if UNITY_EDITOR
            if (rSprite == null)
            {
                rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/TechPassives/{iconName}.png");
                if (rSprite == null)
                    rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/TechPassives/{iconName}.jpg");
            }
#endif
            if (rSprite != null)
            {
                cachedTechIcons[type] = rSprite;
                return rSprite;
            }
        }

        return null;
    }

    private static readonly Dictionary<BoonItemType, Sprite> cachedBoonIcons = new Dictionary<BoonItemType, Sprite>();

    /// <summary>
    /// ดึงไอคอน 2D Sprite ของ Boon (พรวิจัยชีวภาพ) ตาม BoonItemType จาก BoonsData หรือ Assets/Sprites/Icons/Boons/
    /// </summary>
    public static Sprite GetBoonIcon(BoonItemType type)
    {
        if (cachedBoonIcons.TryGetValue(type, out Sprite s) && s != null)
            return s;

        // 1. ดึงจาก BoonsData ScriptableObject
        if (BoonsData.Instance != null)
        {
            Sprite bIcon = BoonsData.Instance.GetIcon(type);
            if (bIcon != null)
            {
                cachedBoonIcons[type] = bIcon;
                return bIcon;
            }
        }

        // 2. ดึงจาก Resources
        string iconName = type switch
        {
            BoonItemType.NanoBarrier => "Icon_NanoBarrier",
            BoonItemType.ToxicSpore => "Icon_ToxicSpore",
            BoonItemType.FlameRing => "Icon_FlameRing",
            BoonItemType.StaticAura => "Icon_StaticAura",
            BoonItemType.MagneticPulse => "Icon_MagneticPulse",
            _ => null
        };

        if (!string.IsNullOrEmpty(iconName))
        {
            Sprite rSprite = Resources.Load<Sprite>($"Icons/Boons/{iconName}");
            if (rSprite == null) rSprite = Resources.Load<Sprite>(iconName);
#if UNITY_EDITOR
            if (rSprite == null)
            {
                rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Boons/{iconName}.png");
                if (rSprite == null)
                    rSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Icons/Boons/{iconName}.jpg");
            }
#endif
            if (rSprite != null)
            {
                cachedBoonIcons[type] = rSprite;
                return rSprite;
            }
        }

        return null;
    }

    public static void ClearBoonIconCache()
    {
        cachedBoonIcons.Clear();
    }

    public static Sprite GetBoonIconByName(string name)
    {
        if (BoonsData.Instance != null)
        {
            Sprite s = BoonsData.Instance.GetIconByName(name);
            if (s != null) return s;
        }
        return GetBoonIcon(BoonItemType.NanoBarrier);
    }

    public static string GetEvolvedWeaponName(EvolvedWeaponType type) => type switch
    {
        EvolvedWeaponType.ExecutionerMatrix => "Executioner Matrix",
        EvolvedWeaponType.PlagueSporeSpreader => "Plague Spore Spreader",
        EvolvedWeaponType.SupernovaCannon => "Supernova Cannon",
        EvolvedWeaponType.StormCitadel => "Storm Citadel",
        EvolvedWeaponType.SingularityCannon => "Singularity Cannon",
        _ => type.ToString()
    };

    public static string GetTechName(TechPassiveType type) => type switch
    {
        TechPassiveType.TargetingLens => "Targeting Lens",
        TechPassiveType.GeneticExtractor => "Genetic Extractor",
        TechPassiveType.OverclockChip => "Overclock Chip",
        TechPassiveType.BioFuelTank => "Bio-Fuel Tank",
        TechPassiveType.GravitonCapacitor => "Graviton Capacitor",
        _ => type.ToString()
    };

    public static string GetBoonName(BoonItemType type) => type switch
    {
        BoonItemType.NanoBarrier => "Nano-Barrier",
        BoonItemType.ToxicSpore => "Toxic Spore",
        BoonItemType.FlameRing => "Flame Ring",
        BoonItemType.StaticAura => "Static Aura",
        BoonItemType.MagneticPulse => "Magnetic Pulse",
        _ => type.ToString()
    };

    public static string GetResonantBoonName(ResonantBoonType type) => type switch
    {
        ResonantBoonType.BladeShieldMatrix => "Resonance: Blade Shield Matrix",
        ResonantBoonType.EpidemicOutbreak => "Resonance: Epidemic Outbreak",
        ResonantBoonType.SolarCoreRing => "Resonance: Solar Core Ring",
        ResonantBoonType.ThunderstormDomain => "Resonance: Thunderstorm Domain",
        ResonantBoonType.EventHorizonVoid => "Resonance: Event Horizon Void",
        _ => type.ToString()
    };

    #endregion

    #region Queries

    public WeaponSlotEntry GetWeaponSlot(WeaponType type)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponType == type) return weapons[i];
        }
        return null;
    }

    public TechSlotEntry GetTechSlot(TechPassiveType type)
    {
        for (int i = 0; i < techPassives.Count; i++)
        {
            if (techPassives[i].techType == type) return techPassives[i];
        }
        return null;
    }

    public BoonSlotEntry GetBoonSlot(BoonItemType type)
    {
        for (int i = 0; i < boons.Count; i++)
        {
            if (boons[i].boonType == type) return boons[i];
        }
        return null;
    }

    public int GetWeaponLevel(WeaponType type) => GetWeaponSlot(type)?.level ?? 0;
    public int GetTechLevel(TechPassiveType type) => GetTechSlot(type)?.level ?? 0;
    public int GetBoonLevel(BoonItemType type) => GetBoonSlot(type)?.level ?? 0;

    public bool IsWeaponSlotsFull => weapons.Count >= MaxWeaponSlots;
    public bool IsTechSlotsFull => techPassives.Count >= MaxTechSlots;
    public bool IsBoonSlotsFull => boons.Count >= MaxBoonSlots;

    public bool AreAllBoonsMaxed()
    {
        if (boons.Count < MaxBoonSlots) return false;
        for (int i = 0; i < boons.Count; i++)
        {
            if (boons[i].level < MaxBoonLevel) return false;
        }
        return true;
    }

    public bool CanGeneEnhancerApply()
    {
        for (int i = 0; i < boons.Count; i++)
        {
            if (boons[i].level < MaxBoonLevel) return true;

            WeaponSlotEntry matchingWeapon = GetWeaponSlot(GetMatchingWeapon(boons[i].boonType));
            if (boons[i].level >= MaxBoonLevel && !boons[i].isResonant && matchingWeapon != null && matchingWeapon.isEvolved)
            {
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Add / Upgrade Weapons, Tech Passives, Boons & Cores

    public bool AddOrUpgradeWeapon(WeaponType type)
    {
        EnsureStarterWeaponInitialized();

        WeaponSlotEntry modifiedEntry = null;
        bool isNew = false;
        WeaponSlotEntry existing = GetWeaponSlot(type);
        if (existing != null)
        {
            if (existing.level >= MaxWeaponLevel) return false;
            existing.level++;
            modifiedEntry = existing;
            Debug.Log($"[PlayerInventory] Upgraded Weapon {GetWeaponName(type)} to Lv.{existing.level}");
        }
        else
        {
            if (weapons.Count >= MaxWeaponSlots)
            {
                Debug.LogWarning($"[PlayerInventory] Weapon slots full ({MaxWeaponSlots}/{MaxWeaponSlots})! Cannot add {GetWeaponName(type)}.");
                return false;
            }

            modifiedEntry = new WeaponSlotEntry
            {
                weaponType = type,
                level = 1,
                isStarterWeapon = false,
                isEvolved = false,
                evolvedForm = EvolvedWeaponType.None
            };
            weapons.Add(modifiedEntry);
            isNew = true;
            Debug.Log($"[PlayerInventory] Equipped new Weapon: {GetWeaponName(type)} (Lv.1)");
        }

        if (isNew)
        {
            OnWeaponAdded?.Invoke(modifiedEntry);
            OnWeaponAddedStatic?.Invoke(modifiedEntry);
        }
        else
        {
            OnWeaponUpgraded?.Invoke(modifiedEntry);
            OnWeaponUpgradedStatic?.Invoke(modifiedEntry);
        }

        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool AddOrUpgradeTech(TechPassiveType type)
    {
        EnsureStarterWeaponInitialized();

        TechSlotEntry modifiedEntry = null;
        bool isNew = false;
        TechSlotEntry existing = GetTechSlot(type);
        if (existing != null)
        {
            if (existing.level >= MaxTechLevel) return false;
            existing.level++;
            modifiedEntry = existing;
            Debug.Log($"[PlayerInventory] Upgraded Tech {GetTechName(type)} to Lv.{existing.level}");
        }
        else
        {
            if (techPassives.Count >= MaxTechSlots)
            {
                Debug.LogWarning($"[PlayerInventory] Tech slots full ({MaxTechSlots}/{MaxTechSlots})! Cannot add {GetTechName(type)}.");
                return false;
            }

            modifiedEntry = new TechSlotEntry
            {
                techType = type,
                level = 1
            };
            techPassives.Add(modifiedEntry);
            isNew = true;
            Debug.Log($"[PlayerInventory] Equipped new Tech Passive: {GetTechName(type)} (Lv.1)");
        }

        if (isNew)
        {
            OnTechPassiveAdded?.Invoke(modifiedEntry);
            OnTechPassiveAddedStatic?.Invoke(modifiedEntry);
        }
        else
        {
            OnTechPassiveUpgraded?.Invoke(modifiedEntry);
            OnTechPassiveUpgradedStatic?.Invoke(modifiedEntry);
        }

        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
        return true;
    }

    public bool AddOrUpgradeBoon(BoonItemType type)
    {
        EnsureStarterWeaponInitialized();

        BoonSlotEntry modifiedEntry = null;
        bool isNew = false;
        BoonSlotEntry existing = GetBoonSlot(type);
        int finalLevel = 1;
        if (existing != null)
        {
            if (existing.level < MaxBoonLevel)
            {
                existing.level++;
                Debug.Log($"[PlayerInventory] Upgraded Boon {GetBoonName(type)} to Lv.{existing.level} (Max)");
            }
            finalLevel = existing.level;
            modifiedEntry = existing;
        }
        else
        {
            if (boons.Count >= MaxBoonSlots)
            {
                Debug.LogWarning($"[PlayerInventory] Boon slots full ({MaxBoonSlots}/{MaxBoonSlots})! Cannot add {GetBoonName(type)}.");
                return false;
            }

            modifiedEntry = new BoonSlotEntry
            {
                boonType = type,
                level = 1,
                isResonant = false,
                resonantForm = ResonantBoonType.None
            };
            boons.Add(modifiedEntry);
            isNew = true;
            finalLevel = 1;
            Debug.Log($"[PlayerInventory] Acquired new Boon: {GetBoonName(type)} (Lv.1)");
        }

        string boonName = GetBoonName(type);
        Debug.Log($"[Boon] Selected: {boonName}, Current Level: {finalLevel}");

        if (isNew)
        {
            OnBoonAdded?.Invoke(modifiedEntry);
            OnBoonAddedStatic?.Invoke(modifiedEntry);
        }
        else
        {
            OnBoonUpgraded?.Invoke(modifiedEntry);
            OnBoonUpgradedStatic?.Invoke(modifiedEntry);
        }

        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// ได้รับ Evolution Core จากบอส (ห้อง 3, 6, 9) และตรวจสอบการ Evolve ทันที
    /// </summary>
    public void AddEvolutionCore(int amount = 1)
    {
        if (amount <= 0) return;
        EnsureStarterWeaponInitialized();

        evolutionCores += amount;
        Debug.Log($"[PlayerInventory] Obtained +{amount} Evolution Core! (Held Cores: {evolutionCores})");

        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnEvolutionCoreCountChanged?.Invoke(evolutionCores);
        OnEvolutionCoreCountChangedStatic?.Invoke(evolutionCores);
        OnInventoryChanged?.Invoke();
    }

    public bool ConsumeEvolutionCore(int amount = 1)
    {
        if (amount <= 0) return true;
        if (evolutionCores < amount) return false;
        evolutionCores -= amount;
        OnEvolutionCoreCountChanged?.Invoke(evolutionCores);
        OnEvolutionCoreCountChangedStatic?.Invoke(evolutionCores);
        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// สำหรับ DebugCheats [I]: ปรับอาวุธทุกชิ้นในตัวเป็น Lv.5 และ Tech ทุกชิ้นเป็น Lv.1+ ทันที
    /// </summary>
    public void AddReroll(int count = 1)
    {
        if (count <= 0) return;
        RerollCount += count;
        OnRerollCountChanged?.Invoke(RerollCount);
        OnRerollCountChangedStatic?.Invoke(RerollCount);
        Debug.Log($"[PlayerInventory] Added {count} Reroll. Total RerollCount: {RerollCount}");
    }

    public bool TryUseReroll()
    {
        if (RerollCount > 0)
        {
            RerollCount--;
            OnRerollCountChanged?.Invoke(RerollCount);
            OnRerollCountChangedStatic?.Invoke(RerollCount);
            Debug.Log($"[PlayerInventory] Used 1 Reroll. Remaining RerollCount: {RerollCount}");
            return true;
        }
        return false;
    }

    public void AddBanish(int count = 1)
    {
        if (count <= 0) return;
        BanishCount += count;
        OnBanishCountChanged?.Invoke(BanishCount);
        OnBanishCountChangedStatic?.Invoke(BanishCount);
        Debug.Log($"[PlayerInventory] Added {count} Banish. Total BanishCount: {BanishCount}");
    }

    public bool TryUseBanish()
    {
        if (BanishCount > 0)
        {
            BanishCount--;
            OnBanishCountChanged?.Invoke(BanishCount);
            OnBanishCountChangedStatic?.Invoke(BanishCount);
            Debug.Log($"[PlayerInventory] Used 1 Banish. Remaining BanishCount: {BanishCount}");
            return true;
        }
        return false;
    }

    public void DebugSetAllHeldWeaponsLv5AndTechsLv1()
    {
        EnsureStarterWeaponInitialized();

        for (int i = 0; i < weapons.Count; i++)
        {
            weapons[i].level = MaxWeaponLevel;
        }

        for (int i = 0; i < techPassives.Count; i++)
        {
            if (techPassives[i].level < 1)
            {
                techPassives[i].level = 1;
            }
        }

        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// สำหรับ DebugCheats [P]: เสก Evolution Core + ตั้งค่าอาวุธ Lv.5 + Tech ตรงสาย (Lv.1+) ครบสูตรเพื่อ Trigger Evolution ทันที
    /// </summary>
    public void DebugTriggerInstantEvolution()
    {
        EnsureStarterWeaponInitialized();

        // ค้นหาอาวุธชิ้นแรกที่ยังไม่ Evolve (หรืออาวุธชิ้นแรกในสล็อต)
        WeaponSlotEntry targetWeapon = null;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (!weapons[i].isEvolved)
            {
                targetWeapon = weapons[i];
                break;
            }
        }

        if (targetWeapon == null && weapons.Count > 0)
        {
            targetWeapon = weapons[0];
        }

        if (targetWeapon == null) return;

        // 1. ปรับอาวุธเป็น Lv.5
        targetWeapon.level = MaxWeaponLevel;

        // 2. ตรวจสอบว่ามี Tech ตรงสายหรือยัง ถ้ายังไม่มีให้เพิ่มหรือแทนที่ในช่อง Tech เป็น Lv.1 ทันที
        TechPassiveType matchingTech = GetMatchingTech(targetWeapon.weaponType);
        TechSlotEntry techSlot = GetTechSlot(matchingTech);
        if (techSlot == null)
        {
            if (techPassives.Count < MaxTechSlots)
            {
                techPassives.Add(new TechSlotEntry
                {
                    techType = matchingTech,
                    level = 1
                });
            }
            else if (techPassives.Count > 0)
            {
                techPassives[techPassives.Count - 1].techType = matchingTech;
                techPassives[techPassives.Count - 1].level = Mathf.Max(1, techPassives[techPassives.Count - 1].level);
            }
        }
        else if (techSlot.level < 1)
        {
            techSlot.level = 1;
        }

        // 3. เสก Evolution Core 1 ชิ้นและสั่ง CheckEvolutionsAndResonances ทันที
        evolutionCores++;
        CheckEvolutionsAndResonances();
        SyncStatsToPlayer();
        OnInventoryChanged?.Invoke();
    }

    /// <summary>
    /// สำหรับประตู Gene Enhancer หรือรางวัลบอสชั้น 2: อัป Boon เป็น Lv.2 ทันที หรือปลุก Resonance ฟรี
    /// </summary>
    public bool TryEnhanceBoonOrResonate()
    {
        EnsureStarterWeaponInitialized();

        // 1. เช็กว่ามี Boon Lv.2 ที่อาวุธตรงสาย Evolve แล้วแต่ยังไม่ Resonate หรือไม่
        CheckEvolutionsAndResonances();

        // 2. ลองหา Boon Lv.1 ที่มีอาวุธตรงสายเพื่ออัปเป็น Lv.2 ก่อน (จะได้เกิด Resonance ทันทีถ้าอาวุธ Evolved แล้ว)
        for (int i = 0; i < boons.Count; i++)
        {
            if (boons[i].level < MaxBoonLevel)
            {
                WeaponSlotEntry matchingWeapon = GetWeaponSlot(GetMatchingWeapon(boons[i].boonType));
                if (matchingWeapon != null && matchingWeapon.isEvolved)
                {
                    boons[i].level = MaxBoonLevel;
                    Debug.Log($"[PlayerInventory] Gene Enhancer upgraded {GetBoonName(boons[i].boonType)} to Lv.2 and triggered Resonance!");
                    CheckEvolutionsAndResonances();
                    SyncStatsToPlayer();
                    OnInventoryChanged?.Invoke();
                    return true;
                }
            }
        }

        // 3. อัป Boon Lv.1 ชิ้นแรกที่มีอยู่ให้เป็น Lv.2 ทันที
        for (int i = 0; i < boons.Count; i++)
        {
            if (boons[i].level < MaxBoonLevel)
            {
                boons[i].level = MaxBoonLevel;
                Debug.Log($"[PlayerInventory] Gene Enhancer upgraded {GetBoonName(boons[i].boonType)} to Lv.2 Max!");
                CheckEvolutionsAndResonances();
                SyncStatsToPlayer();
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Evolution & Boon Resonance Logic (Section 2 & 5)

    public void NotifyWeaponEvolved(WeaponSlotEntry weapon)
    {
        OnWeaponEvolved?.Invoke(weapon);
        OnWeaponEvolvedStatic?.Invoke(weapon);
    }

    /// <summary>
    /// ตรวจสอบเงื่อนไข Evolution (Weapon Lv.5 + Tech ตรงสาย Lv.1+ + Evolution Core)
    /// และตรวจสอบเงื่อนไข Boon Resonance (ต้องมี Weapon Evolved สายนั้นก่อน + Boon ตรงสาย Lv.2 Max)
    /// </summary>
    public void CheckEvolutionsAndResonances()
    {
        // 1. ตรวจสอบและดำเนินการ Weapon Evolution ผ่าน EvolutionManager
        EvolutionManager evoManager = EvolutionManager.Instance ?? EvolutionManager.GetOrCreate();
        if (evoManager != null)
        {
            evoManager.CheckAndTriggerEvolutions(this);
        }
        else
        {
            // Fallback กรณี EvolutionManager ยังไม่ได้โหลด
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponSlotEntry weapon = weapons[i];
                if (weapon.isEvolved || weapon.level < MaxWeaponLevel || evolutionCores <= 0)
                    continue;

                TechPassiveType requiredTech = GetMatchingTech(weapon.weaponType);
                TechSlotEntry heldTech = GetTechSlot(requiredTech);

                if (heldTech != null && heldTech.level >= 1)
                {
                    evolutionCores--;
                    weapon.isEvolved = true;
                    weapon.evolvedForm = GetEvolvedWeaponType(weapon.weaponType);

                    Debug.Log($"[PlayerInventory] ★ WEAPON EVOLVED! {GetWeaponName(weapon.weaponType)} + {GetTechName(requiredTech)} -> {GetEvolvedWeaponName(weapon.evolvedForm)}! (Remaining Cores: {evolutionCores})");
                    OnWeaponEvolved?.Invoke(weapon);
                }
            }
        }

        // 2. ตรวจสอบ Boon Resonance (เงื่อนไข: ต้องมีอาวุธ Evolved สายนั้นก่อนเสมอ + Boon ตรงสายถึง Lv.2 Max)
        CheckResonancesOnly();
    }

    public void CheckResonancesOnly()
    {
        for (int i = 0; i < boons.Count; i++)
        {
            BoonSlotEntry boon = boons[i];
            if (boon.isResonant || boon.level < MaxBoonLevel)
                continue;

            WeaponType requiredWeapon = GetMatchingWeapon(boon.boonType);
            WeaponSlotEntry heldWeapon = GetWeaponSlot(requiredWeapon);

            if (heldWeapon != null && heldWeapon.isEvolved)
            {
                // ตรวจสอบความสอดคล้องตาม Resonance Matrix ทั้ง 5 สาย
                bool isMatchingMatrix = false;
                ResonantBoonType matchedForm = ResonantBoonType.None;

                switch (boon.boonType)
                {
                    case BoonItemType.NanoBarrier: // สาย 1
                        if (heldWeapon.evolvedForm == EvolvedWeaponType.ExecutionerMatrix || heldWeapon.weaponType == WeaponType.NanoBlade)
                        {
                            isMatchingMatrix = true;
                            matchedForm = ResonantBoonType.BladeShieldMatrix;
                        }
                        break;
                    case BoonItemType.ToxicSpore: // สาย 2
                        if (heldWeapon.evolvedForm == EvolvedWeaponType.PlagueSporeSpreader || heldWeapon.weaponType == WeaponType.BioNeedler)
                        {
                            isMatchingMatrix = true;
                            matchedForm = ResonantBoonType.EpidemicOutbreak;
                        }
                        break;
                    case BoonItemType.FlameRing: // สาย 3
                        if (heldWeapon.evolvedForm == EvolvedWeaponType.SupernovaCannon || heldWeapon.weaponType == WeaponType.PlasmaBlaster)
                        {
                            isMatchingMatrix = true;
                            matchedForm = ResonantBoonType.SolarCoreRing;
                        }
                        break;
                    case BoonItemType.StaticAura: // สาย 4
                        if (heldWeapon.evolvedForm == EvolvedWeaponType.StormCitadel || heldWeapon.weaponType == WeaponType.TeslaOrb)
                        {
                            isMatchingMatrix = true;
                            matchedForm = ResonantBoonType.ThunderstormDomain;
                        }
                        break;
                    case BoonItemType.MagneticPulse: // สาย 5
                        if (heldWeapon.evolvedForm == EvolvedWeaponType.SingularityCannon || heldWeapon.weaponType == WeaponType.GravityGrenadier)
                        {
                            isMatchingMatrix = true;
                            matchedForm = ResonantBoonType.EventHorizonVoid;
                        }
                        break;
                }

                if (isMatchingMatrix)
                {
                    boon.isResonant = true;
                    boon.resonantForm = matchedForm;

                    if (BoonManager.Instance != null)
                    {
                        BoonManager.Instance.SetBoonResonant(boon.boonType, true);
                    }

                    Debug.Log($"[PlayerInventory] ★★ BOON RESONANCE! {GetEvolvedWeaponName(heldWeapon.evolvedForm)} + {GetBoonName(boon.boonType)} (Lv.2) -> {GetResonantBoonName(boon.resonantForm)}!");
                    
                    OnBoonResonated?.Invoke(boon);
                    OnBoonResonatedStatic?.Invoke(boon);

                    if (InGameHUDController.Instance != null)
                    {
                        InGameHUDController.Instance.HighlightBoonResonance(boon.boonType);
                    }

                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayResonanceSFX();
                    }
                }
            }
        }
    }

    #endregion

    #region Card Generation & Weighting (Section 7)

    /// <summary>
    /// สุ่มการ์ดเลเวลอัป 3 ใบ ตามกฎ Slot (3 Weapons, 3 Techs), น้ำหนัก x3 สำหรับของที่ถืออยู่,
    /// และระบบจองการ์ด 1 ใบเมื่ออาวุธถึง Lv.3 เพื่อช่วยปั้น Evolution
    /// </summary>
    public List<LevelUpUpgradeData> GenerateWeightedLevelUpCards(int count = 3)
    {
        EnsureStarterWeaponInitialized();

        List<LevelUpUpgradeData> result = new List<LevelUpUpgradeData>();
        List<(LevelUpUpgradeData card, float weight)> candidates = BuildEligibleLevelUpCandidates();

        if (candidates.Count == 0)
        {
            return result;
        }

        // กฎการจองการ์ด (Section 7): เมื่ออาวุธประจำตัว (หรืออาวุธที่ใกล้ Evolve ที่สุด) แตะ Lv.3+
        // ให้จองการ์ด 1 ใน 3 ช่อง เพื่อเสนอชิ้นส่วนที่ยังขาดสำหรับการ Evolve (Tech ตรงสาย หรือ อัปเลเวลอาวุธนั้น)
        LevelUpUpgradeData reservedCard = TryGetReservedEvolutionCard(candidates);
        if (reservedCard != null)
        {
            result.Add(reservedCard);
            candidates.RemoveAll(c => IsSameCardTarget(c.card, reservedCard));
        }

        while (result.Count < count && candidates.Count > 0)
        {
            LevelUpUpgradeData picked = PickWeightedCandidate(candidates);
            if (picked == null) break;

            result.Add(picked);
            candidates.RemoveAll(c => IsSameCardTarget(c.card, picked));
        }

        return result;
    }

    private List<(LevelUpUpgradeData card, float weight)> BuildEligibleLevelUpCandidates()
    {
        List<(LevelUpUpgradeData card, float weight)> pool = new List<(LevelUpUpgradeData card, float weight)>();

        // 1. Weapons (Max 3 Slots, Max Lv.5)
        foreach (WeaponType weaponType in Enum.GetValues(typeof(WeaponType)))
        {
            if (IsWeaponBanished(weaponType) || IsItemBanished($"weapon_{weaponType}")) continue;

            WeaponSlotEntry existing = GetWeaponSlot(weaponType);
            if (existing != null)
            {
                if (existing.level < MaxWeaponLevel)
                {
                    int nextLv = existing.level + 1;
                    var card = LevelUpUpgradeData.CreateWeaponCard(
                        weaponType,
                        nextLv,
                        $"{GetWeaponName(weaponType)} (Lv.{nextLv})",
                        GetWeaponUpgradeDescription(weaponType, nextLv));
                    // ของที่ผู้เล่นถืออยู่แล้ว มีน้ำหนักการสุ่ม x3
                    pool.Add((card, 3f));
                }
            }
            else if (!IsWeaponSlotsFull)
            {
                var card = LevelUpUpgradeData.CreateWeaponCard(
                    weaponType,
                    1,
                    $"NEW: {GetWeaponName(weaponType)} (Lv.1)",
                    GetWeaponUpgradeDescription(weaponType, 1));
                // ของใหม่ น้ำหนักการสุ่ม x1
                pool.Add((card, 1f));
            }
        }

        // 2. Tech Passives (Max 3 Slots, Max Lv.5)
        foreach (TechPassiveType techType in Enum.GetValues(typeof(TechPassiveType)))
        {
            if (IsTechBanished(techType) || IsItemBanished($"tech_{techType}")) continue;

            TechSlotEntry existing = GetTechSlot(techType);
            if (existing != null)
            {
                if (existing.level < MaxTechLevel)
                {
                    int nextLv = existing.level + 1;
                    var card = LevelUpUpgradeData.CreateTechCard(
                        techType,
                        nextLv,
                        $"{GetTechName(techType)} (Lv.{nextLv})",
                        GetTechUpgradeDescription(techType, nextLv));
                    // ของที่ผู้เล่นถืออยู่แล้ว มีน้ำหนักการสุ่ม x3
                    pool.Add((card, 3f));
                }
            }
            else if (!IsTechSlotsFull)
            {
                var card = LevelUpUpgradeData.CreateTechCard(
                    techType,
                    1,
                    $"NEW TECH: {GetTechName(techType)} (Lv.1)",
                    GetTechUpgradeDescription(techType, 1));
                // ของใหม่ น้ำหนักการสุ่ม x1
                pool.Add((card, 1f));
            }
        }

        return pool;
    }

    private LevelUpUpgradeData TryGetReservedEvolutionCard(List<(LevelUpUpgradeData card, float weight)> candidates)
    {
        // ค้นหาอาวุธที่ถึง Lv.3 ขึ้นไปและยังไม่ Evolve โดยให้ความสำคัญกับอาวุธประจำตัวหรืออาวุธที่ใกล้ Evolve ที่สุด
        WeaponSlotEntry bestCandidateWeapon = null;
        int highestProgressScore = -1;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSlotEntry w = weapons[i];
            if (w.isEvolved || w.level < 3) continue;

            TechPassiveType matchingTech = GetMatchingTech(w.weaponType);
            bool hasMatchingTech = GetTechLevel(matchingTech) >= 1;
            bool needsWeaponLevel = w.level < MaxWeaponLevel;
            bool needsTech = !hasMatchingTech && !IsTechSlotsFull;

            // ถ้าเงื่อนไข Evolve ครบแล้ว (อาวุธ Lv.5 + มี Tech ตรงสายแล้ว รอแค่ Core) ไม่ต้องจองการ์ดอีก
            if (!needsWeaponLevel && !needsTech) continue;

            int score = w.level * 10 + (hasMatchingTech ? 5 : 0) + (w.isStarterWeapon ? 2 : 0);
            if (score > highestProgressScore)
            {
                highestProgressScore = score;
                bestCandidateWeapon = w;
            }
        }

        if (bestCandidateWeapon == null) return null;

        TechPassiveType reqTech = GetMatchingTech(bestCandidateWeapon.weaponType);
        bool ownsTech = GetTechLevel(reqTech) >= 1;

        List<LevelUpUpgradeData> missingPieces = new List<LevelUpUpgradeData>();

        // ชิ้นส่วนที่ขาด 1: การอัปเลเวลอาวุธนั้นจนถึง Lv.5
        if (bestCandidateWeapon.level < MaxWeaponLevel)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i].card;
                if (c.category == UpgradeCategory.Weapon && c.weaponType == bestCandidateWeapon.weaponType)
                {
                    missingPieces.Add(c);
                    break;
                }
            }
        }

        // ชิ้นส่วนที่ขาด 2: Tech Passive ตรงสาย (กรณีที่ยังไม่มีและช่อง Tech ยังว่าง)
        if (!ownsTech && !IsTechSlotsFull)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i].card;
                if (c.category == UpgradeCategory.TechPassive && c.techType == reqTech)
                {
                    missingPieces.Add(c);
                    break;
                }
            }
        }

        if (missingPieces.Count == 0) return null;
        return missingPieces[UnityEngine.Random.Range(0, missingPieces.Count)];
    }

    public List<LevelUpUpgradeData> GenerateWeightedBoonCards(int count = 3)
    {
        EnsureStarterWeaponInitialized();

        List<LevelUpUpgradeData> result = new List<LevelUpUpgradeData>();
        List<(LevelUpUpgradeData card, float weight)> pool = new List<(LevelUpUpgradeData card, float weight)>();

        foreach (BoonItemType boonType in Enum.GetValues(typeof(BoonItemType)))
        {
            if (IsBoonBanished(boonType) || IsItemBanished($"boon_{boonType}")) continue;

            BoonSlotEntry existing = GetBoonSlot(boonType);
            WeaponType matchingWeapon = GetMatchingWeapon(boonType);
            bool holdsMatchingWeapon = GetWeaponSlot(matchingWeapon) != null;

            if (existing != null)
            {
                if (existing.level < MaxBoonLevel)
                {
                    int nextLv = existing.level + 1;
                    WeaponSlotEntry w = GetWeaponSlot(matchingWeapon);
                    bool willResonate = (w != null && w.isEvolved);
                    string title = willResonate
                        ? $"★ RESONATE: {GetResonantBoonName(GetResonantBoonType(boonType))}"
                        : $"{GetBoonName(boonType)} (Lv.{nextLv} MAX)";

                    var card = LevelUpUpgradeData.CreateBoonCard(
                        boonType,
                        nextLv,
                        title,
                        GetBoonUpgradeDescription(boonType, nextLv, willResonate));

                    float weight = holdsMatchingWeapon ? 4f : 2.5f;
                    pool.Add((card, weight));
                }
            }
            else if (!IsBoonSlotsFull)
            {
                var card = LevelUpUpgradeData.CreateBoonCard(
                    boonType,
                    1,
                    $"NEW BOON: {GetBoonName(boonType)} (Lv.1)",
                    GetBoonUpgradeDescription(boonType, 1, false));

                // ถ่วงน้ำหนักให้ Boon ตรงสายอาวุธที่ถือออกบ่อยขึ้นตามเอกสารหน้า 8
                float weight = holdsMatchingWeapon ? 3f : 1f;
                pool.Add((card, weight));
            }
        }

        while (result.Count < count && pool.Count > 0)
        {
            LevelUpUpgradeData picked = PickWeightedCandidate(pool);
            if (picked == null) break;

            result.Add(picked);
            pool.RemoveAll(c => IsSameCardTarget(c.card, picked));
        }

        return result;
    }

    private static bool IsSameCardTarget(LevelUpUpgradeData a, LevelUpUpgradeData b)
    {
        if (a == null || b == null) return false;
        if (a.category != b.category) return false;

        return a.category switch
        {
            UpgradeCategory.Weapon => a.weaponType == b.weaponType,
            UpgradeCategory.TechPassive => a.techType == b.techType,
            UpgradeCategory.Boon => a.boonItemType == b.boonItemType,
            _ => a.upgradeTitle == b.upgradeTitle
        };
    }

    private static LevelUpUpgradeData PickWeightedCandidate(List<(LevelUpUpgradeData card, float weight)> pool)
    {
        if (pool == null || pool.Count == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += pool[i].weight;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].weight;
            if (roll <= cumulative)
            {
                return pool[i].card;
            }
        }

        return pool[pool.Count - 1].card;
    }

    #endregion

    #region Descriptions & Stat Syncing (Section 4)

    public static string GetWeaponUpgradeDescription(WeaponType type, int targetLevel)
    {
        return type switch
        {
            WeaponType.NanoBlade => $"Slash arc DMG {NanoBladeSlash.GetBaseDamage(targetLevel):F0}, Knockback {NanoBladeSlash.GetKnockbackDistance(targetLevel):F2}m, CD {NanoBladeSlash.GetBaseCooldown(targetLevel):F2}s (Tech: Targeting Lens)",
            WeaponType.BioNeedler => $"Poison needle DMG {BioNeedlerWeapon.GetDirectDamage(targetLevel):F1}, Poison {BioNeedlerWeapon.GetPoisonDps(targetLevel):F1}/s, Pierce {BioNeedlerWeapon.GetPierceCount(targetLevel)} (Tech: Genetic Extractor)",
            WeaponType.PlasmaBlaster => $"Auto-target plasma DMG {PlasmaBlasterWeapon.GetDamage(targetLevel):F0}, CD {PlasmaBlasterWeapon.GetBaseCooldown(targetLevel):F2}s (Tech: Overclock Chip)",
            WeaponType.TeslaOrb => $"Orbiting electric orbs ({TeslaOrbWeapon.GetOrbCount(targetLevel)} orbs, radius {TeslaOrbWeapon.GetOrbitRadius(targetLevel):F2}m) DMG {TeslaOrbWeapon.GetDamage(targetLevel):F1} every 0.6s (Tech: Bio-Fuel Tank)",
            WeaponType.GravityGrenadier => $"Black hole grenade DMG {GravityGrenadierWeapon.GetImpactDamage(targetLevel):F0}, Pull DoT {GravityGrenadierWeapon.GetPullDps(targetLevel):F0}/s, CD {GravityGrenadierWeapon.GetBaseCooldown(targetLevel):F2}s (Tech: Graviton Capacitor)",
            _ => "Upgrade Weapon"
        };
    }

    public static string GetTechUpgradeDescription(TechPassiveType type, int targetLevel)
    {
        int clamped = Mathf.Clamp(targetLevel, 1, MaxTechLevel);
        return type switch
        {
            TechPassiveType.TargetingLens => $"Crit Rate +{clamped * 5}%, Crit Dmg +{clamped * 20}% (Max +25% / +100% | Evolves Nano-Blade)",
            TechPassiveType.GeneticExtractor => $"Pickup Radius +{clamped * 25}%, RP Bonus +{clamped * 10}% (Max +125% / +50% | Evolves Bio-Needler)",
            TechPassiveType.OverclockChip => $"Cooldown Reduction +{clamped * 8}% (Max +40%, Total Cap 60% | Evolves Plasma Blaster)",
            TechPassiveType.BioFuelTank => $"Attack Area Size +{clamped * 10}% (Max +50% | Evolves Tesla Orb)",
            TechPassiveType.GravitonCapacitor => $"Move Speed +{clamped * 8}% (Max +40% | Evolves Gravity Grenadier)",
            _ => "Upgrade Tech Passive"
        };
    }

    public static string GetBoonUpgradeDescription(BoonItemType type, int targetLevel, bool willResonate)
    {
        if (willResonate)
        {
            return type switch
            {
                BoonItemType.NanoBarrier => "Resonance: Blade Shield Matrix (ทำงานร่วมกับ Nano-Blade Evolved)",
                BoonItemType.ToxicSpore => "Resonance: Epidemic Outbreak (ทำงานร่วมกับ Bio-Needler Evolved)",
                BoonItemType.FlameRing => "Resonance: Solar Core Ring (ทำงานร่วมกับ Plasma Blaster Evolved)",
                BoonItemType.StaticAura => "Resonance: Thunderstorm Domain (ทำงานร่วมกับ Tesla Orb Evolved)",
                BoonItemType.MagneticPulse => "Resonance: Event Horizon Void (ทำงานร่วมกับ Gravity Grenadier Evolved)",
                _ => "Resonant Boon Upgrade"
            };
        }

        return type switch
        {
            BoonItemType.NanoBarrier => targetLevel == 1
                ? "สร้างเกราะ 20 ฟื้นทุก 15 วินาที"
                : "เกราะ 50 ฟื้นทุก 8 วินาที (Max)",
            BoonItemType.ToxicSpore => targetLevel == 1
                ? "หมอกสปอร์พิษ 15/s รัศมี 3m"
                : "หมอกสปอร์พิษ 30/s รัศมี 5m + สโลว์ศัตรู 20% (Max)",
            BoonItemType.FlameRing => targetLevel == 1
                ? "วงแหวนไฟรอบตัวเผาผลาญ 25/s"
                : "วงแหวนไฟ 50/s ขยายวงกว้าง 50% และลดเกราะศัตรู 15% (Max)",
            BoonItemType.StaticAura => targetLevel == 1
                ? "ผ่าสายฟ้า 1 ตัว ทุกระยะเดิน 5m (ดาเมจ 40)"
                : "ผ่าสายฟ้า 3 ตัว ทุกระยะเดิน 4m (ดาเมจ 70) (Max)",
            BoonItemType.MagneticPulse => targetLevel == 1
                ? "ปล่อยพัลส์แม่เหล็กทุก 4 วินาที สโลว์ศัตรู 30%"
                : "ปล่อยพัลส์ทุก 2 วินาที สโลว์ 50% สะท้อนกระสุน 30% (Max)",
            _ => "Upgrade Boon"
        };
    }

    public void SyncStatsToPlayer()
    {
        PlayerStats stats = PlayerStats.Instance != null ? PlayerStats.Instance : GetComponent<PlayerStats>();
        if (stats == null) return;

        // คำนวณโบนัสจาก Tech Passives ทั้ง 5 ชนิดตามตารางหัวข้อ 4.2
        int lensLv = GetTechLevel(TechPassiveType.TargetingLens);
        int extractorLv = GetTechLevel(TechPassiveType.GeneticExtractor);
        int overclockLv = GetTechLevel(TechPassiveType.OverclockChip);
        int fuelLv = GetTechLevel(TechPassiveType.BioFuelTank);
        int gravitonLv = GetTechLevel(TechPassiveType.GravitonCapacitor);

        float techCritRate = TechPassiveData.GetTargetingLensCritRateBonus(lensLv);              // +5% -> +25%
        float techCritDmg = TechPassiveData.GetTargetingLensCritDamageBonus(lensLv);             // +20% -> +100%
        float techPickupRadius = TechPassiveData.GetGeneticExtractorPickupRadiusBonus(extractorLv); // +25% -> +125%
        float techRpBonus = TechPassiveData.GetGeneticExtractorRpBonus(extractorLv);             // +10% -> +50%
        float techCooldownRed = TechPassiveData.GetOverclockChipCooldownReductionBonus(overclockLv); // +8% -> +40% (Cap รวม 60%)
        float techAreaSize = TechPassiveData.GetBioFuelTankAreaSizeBonus(fuelLv);                // +10% -> +50%
        float techMoveSpeed = TechPassiveData.GetGravitonCapacitorMoveSpeedBonus(gravitonLv);    // +8% -> +40%

        // คำนวณโบนัสดาเมจรวมจากเลเวลอาวุธและการ Evolve เพื่อให้ระบบยิงปัจจุบันได้รับผลทันที
        float weaponDamageBonusPercent = 0f;
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSlotEntry w = weapons[i];
            weaponDamageBonusPercent += (w.level - 1) * 0.35f;
            if (w.isEvolved) weaponDamageBonusPercent += 1.50f;
        }

        stats.SetInventoryBonuses(
            techCritRate,
            techCritDmg,
            techPickupRadius,
            techRpBonus,
            techCooldownRed,
            techAreaSize,
            techMoveSpeed,
            weaponDamageBonusPercent);

        // คำนวณโบนัสจาก Boon ทั้ง 5 ชนิดใหม่
        int kineticLv = GetBoonLevel(BoonItemType.KineticOverdrive);
        float boonAtkSpeed = kineticLv == 1 ? 0.15f : (kineticLv >= 2 ? 0.30f : 0f);

        int catalystLv = GetBoonLevel(BoonItemType.ToxicCatalyst);
        float boonDoTDamage = catalystLv == 1 ? 0.25f : (catalystLv >= 2 ? 0.50f : 0f);

        int plasmaLv = GetBoonLevel(BoonItemType.PlasmaAccelerator);
        float boonProjSpeed = plasmaLv >= 1 ? 0.30f : 0f;
        float boonExplosionRadius = plasmaLv == 1 ? 0.20f : (plasmaLv >= 2 ? 0.40f : 0f);

        int cryoLv = GetBoonLevel(BoonItemType.CryoCondenser);
        float boonSlowPercent = cryoLv == 1 ? 0.20f : (cryoLv >= 2 ? 0.35f : 0f);
        float boonFreezeDuration = cryoLv == 1 ? 0.5f : (cryoLv >= 2 ? 1.0f : 0f);

        int aegisLv = GetBoonLevel(BoonItemType.NaniteAegis);
        float boonArmor = aegisLv == 1 ? 8f : (aegisLv >= 2 ? 15f : 0f);
        float boonHPRegen = aegisLv == 1 ? 1.0f : (aegisLv >= 2 ? 1.5f : 0f);

        stats.SetBoonBonuses(
            boonAtkSpeed,
            boonDoTDamage,
            boonProjSpeed,
            boonExplosionRadius,
            boonSlowPercent,
            boonFreezeDuration,
            boonArmor,
            boonHPRegen);

        // อัปเดตสถานะอาวุธต่อเนื่องบนตัวผู้เล่นทันที (รวมถึงเมื่อเกิด Auto-Evolve แทนที่อาวุธเดิมใน Slot)
        WeaponSlotEntry teslaSlot = GetWeaponSlot(WeaponType.TeslaOrb);
        if (teslaSlot != null)
        {
            if (teslaSlot.isEvolved)
            {
                StormCitadelController.EnsureActiveOnPlayer(stats.transform);
            }
            else
            {
                TeslaOrbWeapon.EnsureActiveOnPlayer(stats.transform, teslaSlot.level, false);
            }
        }

        WeaponSlotEntry plasmaSlot = GetWeaponSlot(WeaponType.PlasmaBlaster);
        if (plasmaSlot != null && plasmaSlot.isEvolved)
        {
            SupernovaBeamController.EnsureActiveOnPlayer(stats.transform);
        }

        // เปิดใช้งาน BoonCombatController เพื่อประมวลผล Boon 5 สาย และ Resonance 5 สาย
        BoonCombatController boonCtrl = BoonCombatController.EnsureActiveOnPlayer(stats.transform);
        if (boonCtrl != null)
        {
            boonCtrl.RefreshBoonVisualsAndZones();
        }
    }

    #endregion
}
