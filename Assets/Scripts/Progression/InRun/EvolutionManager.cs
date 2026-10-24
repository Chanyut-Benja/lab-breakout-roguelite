using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// ชุดข้อมูลโครงสร้างของอาวุธขั้นพัฒนา (Evolved Weapon Data)
/// ตามเอกสารบาลานซ์ชีททางการ SUBJECT N: Lab Breakout
/// </summary>
[Serializable]
public class EvolvedWeaponData
{
    [Header("Base Weapon Requirement")]
    public WeaponType baseWeaponType;
    public string baseWeaponName;
    public int requiredWeaponLevel = 5;

    [Header("Matching Tech Requirement")]
    public TechPassiveType requiredTech;
    public string techName;
    public int requiredTechLevel = 1;

    [Header("Evolution Core Requirement")]
    public int requiredEvolutionCores = 1;

    [Header("Evolved Form")]
    public EvolvedWeaponType evolvedType;
    public string evolvedName;
    public string recipeText;
    [TextArea(2, 4)]
    public string description;

    [Header("Boon Resonance Mapping")]
    public BoonItemType matchingBoon;
    public ResonantBoonType resonantBoon;
    public string resonanceName;

    [Header("Visual")]
    public Sprite icon;
}

/// <summary>
/// ตัวจัดการระบบการรวมร่างและพัฒนาอาวุธขั้นสุดยอดอัตโนมัติ (Auto-Evolution Manager)
/// ตามบาลานซ์ชีททางการ SUBJECT N: Lab Breakout
/// 
/// ทำงานแบบ Event-Driven Trigger ตรวจสอบเงื่อนไข 3 ประการ:
/// 1. อาวุธหลักแตะ Lv.5
/// 2. Tech Passive ตรงสายแตะ Lv.1+
/// 3. มี Evolution Core ใน Inventory (ได้รับจากบอสห้อง 3, 6, 9 หรือไอเทมดรอป)
/// 
/// เมื่อเงื่อนไขครบทั้ง 3 ข้อพร้อมกัน:
/// - หักใช้ Evolution Core 1 ชิ้นจาก Inventory
/// - แทนที่อาวุธเดิมใน Weapon Slot ให้กลายเป็นอาวุธ Evolved ตัวใหม่ทันที (เปลี่ยน Logic กระสุน, ดาเมจ และสเตตัสตามสเปก Evolved)
/// - อัปเดต Slot อาวุธใน In-Game HUD ให้แสดงชื่อและไอคอนของร่าง Evolve ใหม่
/// - ยิง Event แจ้งเตือนไปยังระบบ Boon เพื่อเปิดสิทธิ์การเกิด Boon Resonance (ถ้ามี Boon Lv.2 อยู่แล้ว หรือจะอัปถึง Lv.2 ภายหลัง)
/// - แสดงผล UI Popup แจ้งเตือนกลางหน้าจอ
/// </summary>
public class EvolutionManager : MonoBehaviour
{
    public static EvolutionManager Instance { get; private set; }

    [Header("Evolution Database (5 Lines)")]
    [SerializeField] private List<EvolvedWeaponData> evolutionDatabase = new List<EvolvedWeaponData>();

    [Header("Unity Events for UI & Audio Hook")]
    public UnityEvent<EvolvedWeaponData> onWeaponEvolvedUnityEvent = new UnityEvent<EvolvedWeaponData>();

    public event Action<EvolvedWeaponData> OnWeaponEvolved;

    private static readonly Dictionary<WeaponType, EvolvedWeaponData> recipeByWeapon = new Dictionary<WeaponType, EvolvedWeaponData>();
    private static readonly Dictionary<EvolvedWeaponType, EvolvedWeaponData> recipeByEvolved = new Dictionary<EvolvedWeaponType, EvolvedWeaponData>();

    private bool isEvaluating = false;

    public static EvolutionManager GetOrCreate()
    {
        if (Instance != null) return Instance;

        Instance = FindFirstObjectByType<EvolutionManager>();
        if (Instance != null)
        {
            Instance.InitializeDatabase();
            return Instance;
        }

        GameObject managerGo = new GameObject("EvolutionManager");
        Instance = managerGo.AddComponent<EvolutionManager>();
        Instance.InitializeDatabase();
        return Instance;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            InitializeDatabase();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        HookInventoryEvents();
    }

    private void OnDestroy()
    {
        UnhookInventoryEvents();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RuntimeAutoEnsureEvolutionManager()
    {
        if (Instance != null) return;

        GameObject managerGo = new GameObject("EvolutionManager");
        EvolutionManager mgr = managerGo.AddComponent<EvolutionManager>();
        DontDestroyOnLoad(managerGo);
        mgr.InitializeDatabase();
    }

    public void HookInventoryEvents()
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            inv.OnInventoryChanged -= HandleInventoryChanged;
            inv.OnInventoryChanged += HandleInventoryChanged;
        }
    }

    public void UnhookInventoryEvents()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnInventoryChanged -= HandleInventoryChanged;
        }
    }

    private void HandleInventoryChanged()
    {
        CheckAndTriggerEvolutions();
    }

    /// <summary>
    /// ตั้งค่าตาราง Recipe อาวุธ Evolved ทั้ง 5 สาย ตามเอกสารบาลานซ์ชีททางการ SUBJECT N: Lab Breakout
    /// </summary>
    public void InitializeDatabase()
    {
        if (evolutionDatabase != null && evolutionDatabase.Count >= 5)
        {
            CacheDictionaries();
            return;
        }

        evolutionDatabase = new List<EvolvedWeaponData>
        {
            // สาย 1: Nano-Blade (Lv.5) + Targeting Lens (Lv.1+) + Evolution Core -> Executioner Matrix
            new EvolvedWeaponData
            {
                baseWeaponType = WeaponType.NanoBlade,
                baseWeaponName = "Nano-Blade",
                requiredWeaponLevel = 5,
                requiredTech = TechPassiveType.TargetingLens,
                techName = "Targeting Lens",
                requiredTechLevel = 1,
                requiredEvolutionCores = 1,
                evolvedType = EvolvedWeaponType.ExecutionerMatrix,
                evolvedName = "Executioner Matrix",
                recipeText = "Nano-Blade Lv.5 + Targeting Lens",
                description = "ดาเมจ 130 • คริติคอล 100% • ใบมีดซิกแซกตัดทะลุศัตรูทั้งจอ ทุก 0.5 วินาที",
                matchingBoon = BoonItemType.NanoBarrier,
                resonantBoon = ResonantBoonType.BladeShieldMatrix,
                resonanceName = "Blade Shield Matrix"
            },

            // สาย 2: Bio-Needler (Lv.5) + Genetic Extractor (Lv.1+) + Evolution Core -> Plague Spore Spreader
            new EvolvedWeaponData
            {
                baseWeaponType = WeaponType.BioNeedler,
                baseWeaponName = "Bio-Needler",
                requiredWeaponLevel = 5,
                requiredTech = TechPassiveType.GeneticExtractor,
                techName = "Genetic Extractor",
                requiredTechLevel = 1,
                requiredEvolutionCores = 1,
                evolvedType = EvolvedWeaponType.PlagueSporeSpreader,
                evolvedName = "Plague Spore Spreader",
                recipeText = "Bio-Needler Lv.5 + Genetic Extractor",
                description = "หมอกพิษ 80/s กว้าง • ตกค้างบนพื้นต่อเนื่อง • ดูด EXP Gem ทั้งหมดอัตโนมัติ",
                matchingBoon = BoonItemType.ToxicSpore,
                resonantBoon = ResonantBoonType.EpidemicOutbreak,
                resonanceName = "Epidemic Outbreak"
            },

            // สาย 3: Plasma Blaster (Lv.5) + Overclock Chip (Lv.1+) + Evolution Core -> Supernova Cannon
            new EvolvedWeaponData
            {
                baseWeaponType = WeaponType.PlasmaBlaster,
                baseWeaponName = "Plasma Blaster",
                requiredWeaponLevel = 5,
                requiredTech = TechPassiveType.OverclockChip,
                techName = "Overclock Chip",
                requiredTechLevel = 1,
                requiredEvolutionCores = 1,
                evolvedType = EvolvedWeaponType.SupernovaCannon,
                evolvedName = "Supernova Cannon",
                recipeText = "Plasma Blaster Lv.5 + Overclock Chip",
                description = "ลำแสงเลเซอร์กวาด 360° ตลอดเวลา • หมุนรอบละ 2s • ดาเมจ 90/hit",
                matchingBoon = BoonItemType.FlameRing,
                resonantBoon = ResonantBoonType.SolarCoreRing,
                resonanceName = "Solar Core Ring"
            },

            // สาย 4: Tesla Orb (Lv.5) + Bio-Fuel Tank (Lv.1+) + Evolution Core -> Storm Citadel
            new EvolvedWeaponData
            {
                baseWeaponType = WeaponType.TeslaOrb,
                baseWeaponName = "Tesla Orb",
                requiredWeaponLevel = 5,
                requiredTech = TechPassiveType.BioFuelTank,
                techName = "Bio-Fuel Tank",
                requiredTechLevel = 1,
                requiredEvolutionCores = 1,
                evolvedType = EvolvedWeaponType.StormCitadel,
                evolvedName = "Storm Citadel",
                recipeText = "Tesla Orb Lv.5 + Bio-Fuel Tank",
                description = "ฟ้าผ่าสุ่มทั่วจอ 6 ครั้ง/วินาที • ดาเมจ 60 ต่อเป้าหมาย",
                matchingBoon = BoonItemType.StaticAura,
                resonantBoon = ResonantBoonType.ThunderstormDomain,
                resonanceName = "Thunderstorm Domain"
            },

            // สาย 5: Gravity Grenadier (Lv.5) + Graviton Capacitor (Lv.1+) + Evolution Core -> Singularity Cannon
            new EvolvedWeaponData
            {
                baseWeaponType = WeaponType.GravityGrenadier,
                baseWeaponName = "Gravity Grenadier",
                requiredWeaponLevel = 5,
                requiredTech = TechPassiveType.GravitonCapacitor,
                techName = "Graviton Capacitor",
                requiredTechLevel = 1,
                requiredEvolutionCores = 1,
                evolvedType = EvolvedWeaponType.SingularityCannon,
                evolvedName = "Singularity Cannon",
                recipeText = "Gravity Grenadier Lv.5 + Graviton Capacitor",
                description = "ลำแสงดูดรวมศัตรูจุดเดียว • ระเบิดทำลาย 1,200 ดาเมจ ทุก 4 วินาที",
                matchingBoon = BoonItemType.MagneticPulse,
                resonantBoon = ResonantBoonType.EventHorizonVoid,
                resonanceName = "Event Horizon Void"
            }
        };

        CacheDictionaries();
    }

    private void CacheDictionaries()
    {
        recipeByWeapon.Clear();
        recipeByEvolved.Clear();

        for (int i = 0; i < evolutionDatabase.Count; i++)
        {
            var data = evolutionDatabase[i];
            if (data.icon == null)
            {
                data.icon = PlayerInventory.GetEvolvedWeaponIcon(data.evolvedType);
            }
            recipeByWeapon[data.baseWeaponType] = data;
            recipeByEvolved[data.evolvedType] = data;
        }
    }

    public static EvolvedWeaponData GetRecipe(WeaponType weapon)
    {
        if (recipeByWeapon.TryGetValue(weapon, out EvolvedWeaponData data))
            return data;

        if (Instance != null)
        {
            Instance.InitializeDatabase();
            if (recipeByWeapon.TryGetValue(weapon, out data))
                return data;
        }
        else
        {
            EvolutionManager mgr = GetOrCreate();
            if (mgr != null && recipeByWeapon.TryGetValue(weapon, out data))
                return data;
        }

        return null;
    }

    public static EvolvedWeaponData GetRecipe(EvolvedWeaponType evolvedType)
    {
        if (recipeByEvolved.TryGetValue(evolvedType, out EvolvedWeaponData data))
            return data;

        if (Instance != null)
        {
            Instance.InitializeDatabase();
            if (recipeByEvolved.TryGetValue(evolvedType, out data))
                return data;
        }
        else
        {
            EvolutionManager mgr = GetOrCreate();
            if (mgr != null && recipeByEvolved.TryGetValue(evolvedType, out data))
                return data;
        }

        return null;
    }

    /// <summary>
    /// ตรวจสอบว่าอาวุธชนิดนี้สามารถ Evolve ได้หรือไม่ (ตรวจทั้ง 3 เงื่อนไข: อาวุธ Lv.5 + Tech ตรงสาย Lv.1+ + Evolution Core >= 1)
    /// ตรวจสอบไม่ให้เกิดบั๊กซ้ำซ้อนกับอาวุธที่ Evolve ไปแล้ว
    /// </summary>
    public bool CanEvolve(WeaponSlotEntry weapon, PlayerInventory inventory)
    {
        if (weapon == null || weapon.isEvolved || weapon.level < 5) return false;
        if (inventory == null || inventory.EvolutionCores <= 0) return false;

        EvolvedWeaponData recipe = GetRecipe(weapon.weaponType);
        if (recipe == null) return false;

        TechSlotEntry techSlot = inventory.GetTechSlot(recipe.requiredTech);
        return techSlot != null && techSlot.level >= recipe.requiredTechLevel;
    }

    /// <summary>
    /// ตรวจสอบและดำเนินการรวมร่างอาวุธทันทีเมื่อเงื่อนไขครบทั้ง 3 ข้อพร้อมกัน:
    /// 1. อาวุธหลักอัปเกรดแตะ Lv.5
    /// 2. ได้รับ Tech ตรงสายชิ้นแรก (Lv.1+)
    /// 3. มี Evolution Core ใน Inventory (ถ้ามี Core เหลือแต่เงื่อนไขยังไม่ครบ Core จะถูกเก็บไว้ใน Inventory ตามปกติ)
    /// </summary>
    public List<EvolvedWeaponData> CheckAndTriggerEvolutions(PlayerInventory inventory = null)
    {
        if (isEvaluating) return null;
        isEvaluating = true;

        List<EvolvedWeaponData> evolvedList = new List<EvolvedWeaponData>();

        try
        {
            if (inventory == null) inventory = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
            if (inventory == null) return evolvedList;

            var weapons = inventory.Weapons;
            if (weapons == null || weapons.Count == 0) return evolvedList;

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponSlotEntry weapon = weapons[i];
                // ป้องกันการรวมร่างซ้ำซ้อนกับอาวุธที่ Evolve ไปแล้ว
                if (weapon == null || weapon.isEvolved) continue;

                if (CanEvolve(weapon, inventory))
                {
                    EvolvedWeaponData recipe = GetRecipe(weapon.weaponType);
                    if (recipe != null)
                    {
                        // 1. หักใช้ Evolution Core 1 ชิ้นจาก Inventory
                        if (inventory.ConsumeEvolutionCore(recipe.requiredEvolutionCores))
                        {
                            // 2. ทำการแทนที่อาวุธเดิมใน Weapon Slot ให้กลายเป็นอาวุธ Evolved ตัวใหม่ทันที
                            weapon.isEvolved = true;
                            weapon.evolvedForm = recipe.evolvedType;

                            // หากเป็น Tesla Orb และกลายร่างเป็น Storm Citadel ให้หยุดลูกแก้วเดิมที่หมุนรอบตัว
                            if (weapon.weaponType == WeaponType.TeslaOrb)
                            {
                                GameObject playerObj = GameObject.FindWithTag("Player");
                                if (playerObj != null)
                                {
                                    TeslaOrbWeapon tesla = playerObj.GetComponent<TeslaOrbWeapon>();
                                    if (tesla != null)
                                    {
                                        Destroy(tesla);
                                    }
                                }
                            }

                            // โหลด Sprite ไอคอน
                            if (recipe.icon == null)
                            {
                                recipe.icon = PlayerInventory.GetEvolvedWeaponIcon(recipe.evolvedType);
                                if (recipe.icon == null)
                                {
                                    recipe.icon = PlayerInventory.GetWeaponIcon(weapon.weaponType);
                                }
                            }

                            evolvedList.Add(recipe);

                            Debug.Log($"[EvolutionManager] ★★★ AUTO-EVOLUTION COMPLETED: {recipe.baseWeaponName} + {recipe.techName} -> {recipe.evolvedName}! (Remaining Cores: {inventory.EvolutionCores})");

                            // ยิง Event ให้ PlayerInventory รับทราบ
                            inventory.NotifyWeaponEvolved(weapon);

                            // 3. ยิง Event ภายในระบบ Evolution
                            OnWeaponEvolved?.Invoke(recipe);
                            onWeaponEvolvedUnityEvent?.Invoke(recipe);

                            // 4. แสดง UI Evolution Popup แจ้งเตือนกลางหน้าจอ
                            EvolutionPopupUI.Show(recipe);

                            // 5. อัปเดต Slot อาวุธใน In-Game HUD ให้แสดงชื่อและไอคอนของร่าง Evolve ใหม่
                            if (InGameHUDController.Instance != null)
                            {
                                InGameHUDController.Instance.RefreshInventorySlots();
                            }

                            // 6. ยิง Event ตรวจสอบไปยังระบบ Boon เพื่อเปิดสิทธิ์การเกิด Boon Resonance ทันที
                            inventory.CheckResonancesOnly();

                            // 7. แจ้งเตือน BoonCombatController ให้รีเฟรช Visuals / Zones
                            GameObject player = GameObject.FindWithTag("Player");
                            if (player != null)
                            {
                                BoonCombatController ctrl = BoonCombatController.EnsureActiveOnPlayer(player.transform);
                                if (ctrl != null)
                                {
                                    ctrl.RefreshBoonVisualsAndZones();
                                }
                            }
                        }
                    }
                }
            }
        }
        finally
        {
            isEvaluating = false;
        }

        return evolvedList;
    }
}
