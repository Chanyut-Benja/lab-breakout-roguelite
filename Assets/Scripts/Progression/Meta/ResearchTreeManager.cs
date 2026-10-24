using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public enum MetaUpgradeType
{
    MaxHealth = 0,  // Branch 0 of Selected Character (Vanguard: Armor, Psionic: Crit Chance, Cyber-Swift: Move Speed)
    MoveSpeed = 1,  // Branch 1 of Selected Character (Vanguard: Reflect, Psionic: DoT Dmg, Cyber-Swift: Dash Cooldown)
    Damage = 2,     // Branch 2 of Selected Character (Vanguard: HP Regen, Psionic: Stun Duration, Cyber-Swift: Attack Speed)
    AttackRate = 3  // Branch 3 of Selected Character (Vanguard: Knockback, Psionic: AOE, Cyber-Swift: I-Frame Duration)
}

public enum SubjectCharacterType
{
    Vanguard_N01 = 0,
    Psionic_N02 = 1,
    CyberSwift_N03 = 2
}

public enum SkillNodeTierType
{
    Core,
    Minor,
    Major
}

[Serializable]
public class SkillTreeNodeDefinition
{
    public string nodeId;
    public SubjectCharacterType character;
    public SkillNodeTierType nodeType;
    public string title;
    public string description;
    public int costRP;
    public string prerequisiteNodeId;
    public Vector2 anchoredPosition;
    public int mappedBranchIndex;

    public SkillTreeNodeDefinition(
        string nodeId,
        SubjectCharacterType character,
        SkillNodeTierType nodeType,
        string title,
        string description,
        int costRP,
        string prerequisiteNodeId,
        Vector2 anchoredPosition,
        int mappedBranchIndex = 0)
    {
        this.nodeId = nodeId;
        this.character = character;
        this.nodeType = nodeType;
        this.title = title;
        this.description = description;
        this.costRP = costRP;
        this.prerequisiteNodeId = prerequisiteNodeId;
        this.anchoredPosition = anchoredPosition;
        this.mappedBranchIndex = mappedBranchIndex;
    }
}

/// <summary>
/// โบนัสสเตตัสเริ่มต้นจากผัง Skill Tree ของตัวละครที่เลือก ส่งเข้า PlayerStats ตอนเริ่มรันใหม่
/// </summary>
[Serializable]
public struct CharacterMetaBonuses
{
    public SubjectCharacterType character;

    // General Stat Bonuses
    public float maxHpPercentBonus;
    public float flatMaxHpBonus;
    public float moveSpeedPercentBonus;
    public float damagePercentBonus;
    public float attackSpeedCooldownBonus;

    // N-01 Vanguard Focus: Armor, Reflect, HP Regen, Knockback
    public float flatArmor;
    public float reflectArmorRatio;     // เช่น 1.50f = สะท้อน 150% ของ Armor
    public float hpRegenPerSecond;      // ฟื้นฟูเลือดต่อวินาที
    public float knockbackMultiplierBonus;
    public bool hasBlastNovaMajor;
    public bool hasAbsoluteArmorMajor;
    public bool hasKineticReflectMajor;
    public bool hasJuggernautMomentumMajor;
    public bool hasTitanRevitalizationMajor;

    // N-02 Psionic Focus: Crit Chance, DoT Dmg, Stun Duration, AOE
    public float critChanceBonus;
    public float critDamageBonus;
    public float dotDamageMultiplierBonus;
    public float stunDurationBonusSeconds;
    public float aoeSizePercentBonus;
    public bool hasMindCollapseMajor;
    public bool hasPsionicBarrierMajor;
    public bool hasOverchargeNovaMajor;
    public bool hasVenomCatalystMajor;
    public bool hasTemporalRiftMajor;

    // N-03 Cyber-Swift Focus: Move Speed, Dash Cooldown, Attack Speed, I-Frame Duration
    public float dashCooldownReductionPercent;
    public float dashDistancePercentBonus;
    public float iFrameDurationBonusSeconds;
    public int bonusDashCharges;
    public bool hasPhantomCloneMajor;
    public bool hasBulletTimeMajor;
    public bool hasOverclockOverdriveMajor;
    public bool hasPhaseShifterMajor;
    public bool hasLightningReflexesMajor;
}

[ExecuteAlways]
public class ResearchTreeManager : MonoBehaviour
{
    public static ResearchTreeManager Instance { get; private set; }

    public const string TREE_ROOT_OBJECT_NAME = "ThreeColumnNodeTreeRoot";
    public const string PREFAB_FOLDER_PATH = "Assets/Prefabs/UI";
    public const string PREFAB_ASSET_PATH = "Assets/Prefabs/UI/ThreeColumnNodeTreeRoot.prefab";

    // ค่าคงที่สำหรับคำนวณ RP หลังจบรันตามเอกสารบาลานซ์หัวข้อ 9.4 และ 1.3
    public const int RP_PER_NORMAL_ROOM = 25;
    public const int RP_PER_BOSS_KILL = 75;
    public const int RP_BONUS_FLOOR_1 = 400;
    public const int RP_BONUS_FLOOR_2 = 1000;
    public const int RP_MINIMUM_GUARANTEE = 50;

    // ลำดับขั้นราคาอัปเกรดตามเอกสารบาลานซ์: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP
    public static readonly int[] UpgradeCostTiers = { 150, 200, 250, 500, 800, 1500 };

    // สีประจำตัวละครทั้ง 3 คอลัมน์: เขียว (N-01), แดง (N-02), ส้ม (N-03)
    private static readonly Color VanguardGreen = new Color(0.18f, 0.92f, 0.42f, 1f);
    private static readonly Color PsionicRed = new Color(0.96f, 0.24f, 0.30f, 1f);
    private static readonly Color CyberSwiftOrange = new Color(1.00f, 0.56f, 0.14f, 1f);

    [Header("Skill Tree Configuration")]
    [SerializeField] private SubjectCharacterType selectedTreeCharacter = SubjectCharacterType.Vanguard_N01;
    [SerializeField] private int maxLevel = 6;
    [SerializeField] private RectTransform researchPanelOverride;

    public event Action OnTreeUpdated;

    private readonly List<SkillTreeNodeDefinition> allNodeDefinitions = new List<SkillTreeNodeDefinition>();

    private class NodeVisualEntry
    {
        public SkillTreeNodeDefinition definition;
        public Color themeColor;
        public Image outerRingImage;
        public Image nodeBodyImage;
        public Text titleText;
        public Text costOrStateText;
        public Button button;
        public Image connectingLineToParent;
    }

    private readonly List<NodeVisualEntry> spawnedNodeVisuals = new List<NodeVisualEntry>();
    private Text bottomStatusBannerText;
    private static Sprite cachedCircleSprite;

    public SubjectCharacterType SelectedTreeCharacter => selectedTreeCharacter;
    public int MaxLevel => UpgradeCostTiers.Length;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }
        }
        else
        {
            Instance = this;
        }

        maxLevel = UpgradeCostTiers.Length;
        BuildSkillTreeDefinitions();
    }

    private void Start()
    {
        // ปิดหรือลบโค้ดส่วนที่สั่งสร้างผังวิจัย/Connecting Line อัตโนมัติเมื่อเริ่มเกม
        // ให้สร้างหรือแสดงเฉพาะตอนที่เปิดหน้าผังวิจัย (ผ่าน OpenResearchLab) เท่านั้น
        Transform rPanel = ResolveResearchPanel();
        if (rPanel != null && rPanel.gameObject.activeSelf)
        {
            EnsureNodeTreeVisualUI(false);
            RefreshAllNodeTreeVisuals();
        }
        else if (rPanel != null && MainMenuManager.Instance != null)
        {
            rPanel.gameObject.SetActive(false);
            foreach (Transform child in rPanel)
            {
                if (child != null && child.name.ToLowerInvariant().Contains("lineslayer"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// คำนวณ RP ที่ได้รับเมื่อจบรัน (ตอน GameOver หรือ Victory) ตามหัวข้อ 9.4 และ 1.3:
    /// - เคลียร์ห้องปกติ = 25 RP/ห้อง
    /// - ชนะบอส = 75 RP/ตัว
    /// - โบนัสจบ Floor 1 = +400 RP, Floor 2 (ชนะเกม) = +1,000 RP
    /// - นำผลรวมคูณโบนัสจาก Genetic Extractor (ถ้ามี)
    /// - การันตีขั้นต่ำ 50 RP เสมอแม้ตายห้องแรก
    /// </summary>
    public static int CalculateRunResearchPoints(
        int normalRoomsCleared,
        int bossesDefeated,
        bool floor1Completed,
        bool floor2Completed,
        float geneticExtractorMultiplier = 1f,
        int extraInRunRP = 0)
    {
        int rawRP = (Mathf.Max(0, normalRoomsCleared) * RP_PER_NORMAL_ROOM)
                  + (Mathf.Max(0, bossesDefeated) * RP_PER_BOSS_KILL)
                  + (floor1Completed ? RP_BONUS_FLOOR_1 : 0)
                  + (floor2Completed ? RP_BONUS_FLOOR_2 : 0)
                  + Mathf.Max(0, extraInRunRP);

        float effectiveMultiplier = Mathf.Max(1f, geneticExtractorMultiplier);
        int multipliedRP = Mathf.RoundToInt(rawRP * effectiveMultiplier);

        return Mathf.Max(RP_MINIMUM_GUARANTEE, multipliedRP);
    }

    private void BuildSkillTreeDefinitions()
    {
        allNodeDefinitions.Clear();

        // พิกัด AnchoredPosition ของโหนดภายในแต่ละคอลัมน์:
        // บนสุดเป็น Core Node วงกลม (0, 210) แตกกิ่งลงมาเป็น Node ย่อยซ้าย-ขวา
        Vector2 posCore       = new Vector2(0f, 210f);
        Vector2 posTier1Left  = new Vector2(-115f, 95f);
        Vector2 posTier1Right = new Vector2(115f, 95f);
        Vector2 posTier2Left  = new Vector2(-135f, -15f);
        Vector2 posTier2Right = new Vector2(135f, -15f);
        Vector2 posTier3Left  = new Vector2(-115f, -125f);
        Vector2 posTier3Right = new Vector2(115f, -125f);
        Vector2 posTier4Left  = new Vector2(-75f, -235f);
        Vector2 posTier4Right = new Vector2(75f, -235f);

        // ====================================================================
        // คอลัมน์ 1 (สีเขียว): Subject N-01 : Vanguard (Armor, Reflect, HP Regen, Knockback)
        // ลำดับขั้นราคา: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP
        // ====================================================================
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Core", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Core,
            "CORE: N-01", "จุดเริ่มต้นสาย Vanguard (Kinetic Absorption)", 0, "", posCore, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Minor_Top1", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Minor,
            "Armor +5", "Minor: เพิ่มเกราะป้องกันความเสียหาย +5", 150, "N01_Core", posTier1Left, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Minor_Bottom", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Minor,
            "HP Regen", "Minor: ฟื้นฟู HP +0.8/s และ Atk Spd +5%", 200, "N01_Core", posTier1Right, 2));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Minor_Top2", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Minor,
            "HP +10% & KB", "Minor: Max HP +10% และแรงผลัก Knockback +15%", 250, "N01_Minor_Top1", posTier2Left, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Major_1", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Major,
            "Blast Nova", "Major: ฟันครบ 3 ครั้งระเบิดคลื่นผลักศัตรูรอบตัว", 500, "N01_Minor_Bottom", posTier2Right, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Major_2", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Major,
            "Absolute Armor", "Major: HP < 30% รับเกราะอมตะ 3s + Armor +5", 800, "N01_Minor_Top2", posTier3Left, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Major_3", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Major,
            "Kinetic Reflect", "Major: สะท้อนดาเมจระยะประชิด 150% ของ Armor", 800, "N01_Major_1", posTier3Right, 1));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Major_4", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Major,
            "Juggernaut", "Major: เคลื่อนที่ต่อเนื่องเพิ่มดาเมจสูงสุด +20%", 1500, "N01_Major_2", posTier4Left, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N01_Major_5", SubjectCharacterType.Vanguard_N01, SkillNodeTierType.Major,
            "Titan Regen", "Major: ฟื้น HP 15%/30% เมื่อฆ่าบอส + Regen +1.5/s", 1500, "N01_Major_3", posTier4Right, 2));

        // ====================================================================
        // คอลัมน์ 2 (สีแดง): Subject N-02 : Psionic (Crit Chance, DoT Dmg, Stun Duration, AOE)
        // ลำดับขั้นราคา: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP
        // ====================================================================
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Core", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Core,
            "CORE: N-02", "จุดเริ่มต้นสาย Psionic (Mind Shock)", 0, "", posCore, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Minor_Left1", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Minor,
            "Crit +5%", "Minor: โอกาสติดคริติคอล +5% และ Crit Dmg +15%", 150, "N02_Core", posTier1Left, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Minor_Bottom", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Minor,
            "DoT +20%", "Minor: ความเสียหายพิษต่อเนื่อง (DoT) +20%", 200, "N02_Core", posTier1Right, 1));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Minor_Top1", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Minor,
            "Stun +0.25s", "Minor: นานขึ้น +0.25s และขนาดพื้นที่ AOE +10%", 250, "N02_Minor_Left1", posTier2Left, 2));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Major_1", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Major,
            "Mind Collapse", "Major: ศัตรูติด Stun โดนความเสียหายแรงขึ้น +25%", 500, "N02_Minor_Bottom", posTier2Right, 2));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Major_2", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Major,
            "Psionic Barrier", "Major: แปลงดาเมจสกิลเป็นเกราะ + AOE +15%", 800, "N02_Minor_Top1", posTier3Left, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Major_3", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Major,
            "Overcharge AOE", "Major: คลื่นพลังจิตทำลายกระสุนศัตรู + AOE +15%", 800, "N02_Major_1", posTier3Right, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Major_4", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Major,
            "Venom Catalyst", "Major: พิษแพร่กระจายเมื่อศัตรูตาย + DoT +25%", 1500, "N02_Major_2", posTier4Left, 1));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N02_Major_5", SubjectCharacterType.Psionic_N02, SkillNodeTierType.Major,
            "Temporal Rift", "Major: ทุกฆ่า 50 ตัว หยุดเวลาศัตรูทั้งจอ 1.5s", 1500, "N02_Major_3", posTier4Right, 2));

        // ====================================================================
        // คอลัมน์ 3 (สีส้ม): Subject N-03 : Cyber-Swift (Move Speed, Dash Cooldown, Attack Speed, I-Frame)
        // ลำดับขั้นราคา: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP
        // ====================================================================
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Core", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Core,
            "CORE: N-03", "จุดเริ่มต้นสาย Cyber-Swift (Overclock Dash)", 0, "", posCore, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Minor_Right1", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Minor,
            "Move Spd +6%", "Minor: ความเร็วเคลื่อนที่ +6%", 150, "N03_Core", posTier1Left, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Minor_Bottom", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Minor,
            "Atk Spd +6%", "Minor: อัตราการยิง (Attack Speed) +6%", 200, "N03_Core", posTier1Right, 2));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Minor_Left2", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Minor,
            "Dash CD -12%", "Minor: ชาร์จ Dash เร็วขึ้น 12% และ I-Frame +0.1s", 250, "N03_Minor_Right1", posTier2Left, 1));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Major_1", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Major,
            "Phantom Clone", "Major: Dash ทิ้งร่างโฮโลแกรมล่อศัตรู 2s ก่อนระเบิด", 500, "N03_Minor_Bottom", posTier2Right, 0));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Major_2", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Major,
            "Bullet Time", "Major: Dash หลบกระสุนสโลว์เวลา + I-Frame +0.2s", 800, "N03_Minor_Left2", posTier3Left, 3));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Major_3", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Major,
            "Overdrive Spd", "Major: เคลื่อนที่ทุก 10m เพิ่มยิงรัว +5% (สูงสุด 40%)", 800, "N03_Major_1", posTier3Right, 2));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Major_4", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Major,
            "Phase Shifter", "Major: เพิ่มจำนวน Dash สะสมสูงสุด +1 (รวม 3 Dash)", 1500, "N03_Major_2", posTier4Left, 1));
        allNodeDefinitions.Add(new SkillTreeNodeDefinition("N03_Major_5", SubjectCharacterType.CyberSwift_N03, SkillNodeTierType.Major,
            "Reflex Crit", "Major: หลบการโจมตีมีโอกาสติดคริติคอล 100%", 1500, "N03_Major_3", posTier4Right, 3));
    }

    public List<SkillTreeNodeDefinition> GetNodesForCharacter(SubjectCharacterType character)
    {
        if (allNodeDefinitions.Count == 0) BuildSkillTreeDefinitions();
        return allNodeDefinitions.FindAll(n => n.character == character);
    }

    public void SelectCharacterTree(SubjectCharacterType character)
    {
        selectedTreeCharacter = character;
        RefreshAllNodeTreeVisuals();
        OnTreeUpdated?.Invoke();
    }

    public int GetCurrentResearchPoints()
    {
        GameData data = SaveSystem.LoadGame();
        return data != null ? data.researchPoints : 0;
    }

    public int GetUpgradeLevel(MetaUpgradeType type)
    {
        return GetCharacterBranchLevel(selectedTreeCharacter, (int)type);
    }

    public int GetCharacterBranchLevel(SubjectCharacterType character, int branchIndex)
    {
        GameData data = SaveSystem.LoadGame();
        if (data == null) return 0;

        int clampedIdx = Mathf.Clamp(branchIndex, 0, 3);
        int[] branches = GetBranchArray(data, character);
        return branches != null && clampedIdx < branches.Length ? branches[clampedIdx] : 0;
    }

    private static int[] GetBranchArray(GameData data, SubjectCharacterType character)
    {
        if (data == null) return null;
        data.EnsureInitialized();

        return character switch
        {
            SubjectCharacterType.Vanguard_N01 => data.vanguardBranchLevels,
            SubjectCharacterType.Psionic_N02 => data.psionicBranchLevels,
            SubjectCharacterType.CyberSwift_N03 => data.cyberSwiftBranchLevels,
            _ => data.vanguardBranchLevels
        };
    }

    public int GetUpgradeCost(MetaUpgradeType type)
    {
        int currentLevel = GetUpgradeLevel(type);
        if (currentLevel < 0 || currentLevel >= UpgradeCostTiers.Length)
            return -1;

        return UpgradeCostTiers[currentLevel];
    }

    public bool CanUpgrade(MetaUpgradeType type)
    {
        int currentLevel = GetUpgradeLevel(type);
        if (currentLevel >= UpgradeCostTiers.Length) return false;

        int cost = GetUpgradeCost(type);
        return MeetsPrerequisites(type) && GetCurrentResearchPoints() >= cost;
    }

    public bool MeetsPrerequisites(MetaUpgradeType type)
    {
        switch (type)
        {
            case MetaUpgradeType.Damage:
                return GetUpgradeLevel(MetaUpgradeType.MaxHealth) >= 1 ||
                       GetUpgradeLevel(MetaUpgradeType.MoveSpeed) >= 1;
            case MetaUpgradeType.AttackRate:
                return GetUpgradeLevel(MetaUpgradeType.Damage) >= 1 ||
                       GetUpgradeLevel(MetaUpgradeType.MaxHealth) >= 1;
            default:
                return true;
        }
    }

    public string GetPrerequisiteText(MetaUpgradeType type)
    {
        return type switch
        {
            MetaUpgradeType.Damage => "Requires Tier 1 Node",
            MetaUpgradeType.AttackRate => "Requires Previous Node",
            _ => ""
        };
    }

    public string GetBranchTitle(MetaUpgradeType type)
    {
        return (selectedTreeCharacter, type) switch
        {
            (SubjectCharacterType.Vanguard_N01, MetaUpgradeType.MaxHealth) => "Vanguard Armor",
            (SubjectCharacterType.Vanguard_N01, MetaUpgradeType.MoveSpeed) => "Kinetic Reflect",
            (SubjectCharacterType.Vanguard_N01, MetaUpgradeType.Damage) => "Bio-HP Regen",
            (SubjectCharacterType.Vanguard_N01, MetaUpgradeType.AttackRate) => "Impact Knockback",

            (SubjectCharacterType.Psionic_N02, MetaUpgradeType.MaxHealth) => "Psionic Crit",
            (SubjectCharacterType.Psionic_N02, MetaUpgradeType.MoveSpeed) => "Neurotoxin DoT",
            (SubjectCharacterType.Psionic_N02, MetaUpgradeType.Damage) => "Mind Shock Stun",
            (SubjectCharacterType.Psionic_N02, MetaUpgradeType.AttackRate) => "Psionic AOE",

            (SubjectCharacterType.CyberSwift_N03, MetaUpgradeType.MaxHealth) => "Thruster Move Spd",
            (SubjectCharacterType.CyberSwift_N03, MetaUpgradeType.MoveSpeed) => "Dash Cooldown",
            (SubjectCharacterType.CyberSwift_N03, MetaUpgradeType.Damage) => "Rapid Attack Spd",
            (SubjectCharacterType.CyberSwift_N03, MetaUpgradeType.AttackRate) => "Agility I-Frame",
            _ => type.ToString()
        };
    }

    public bool TryUpgrade(MetaUpgradeType type)
    {
        if (!CanUpgrade(type)) return false;

        GameData data = SaveSystem.LoadGame();
        int cost = GetUpgradeCost(type);

        data.researchPoints -= cost;

        int[] branches = GetBranchArray(data, selectedTreeCharacter);
        int branchIdx = Mathf.Clamp((int)type, 0, 3);
        branches[branchIdx]++;

        SaveSystem.SaveGame(data);
        RefreshAllNodeTreeVisuals();
        OnTreeUpdated?.Invoke();
        return true;
    }

    public bool IsNodeUnlocked(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId)) return true;
        GameData data = SaveSystem.LoadGame();
        return data != null && data.unlockedSkillNodes != null && data.unlockedSkillNodes.Contains(nodeId);
    }

    public bool CanUnlockNode(SkillTreeNodeDefinition def)
    {
        if (def == null) return false;
        if (IsNodeUnlocked(def.nodeId)) return false;
        if (!string.IsNullOrEmpty(def.prerequisiteNodeId) && !IsNodeUnlocked(def.prerequisiteNodeId))
            return false;
        return GetCurrentResearchPoints() >= def.costRP;
    }

    public bool TryUnlockNodeById(string nodeId)
    {
        if (allNodeDefinitions.Count == 0) BuildSkillTreeDefinitions();
        SkillTreeNodeDefinition def = allNodeDefinitions.Find(n => n.nodeId == nodeId);
        if (def == null) return false;

        GameData data = SaveSystem.LoadGame();
        if (data.unlockedSkillNodes.Contains(nodeId))
        {
            if (bottomStatusBannerText != null)
                bottomStatusBannerText.text = $"[{def.title}] ปลดล็อกแล้ว: {def.description}";
            return false;
        }

        if (!string.IsNullOrEmpty(def.prerequisiteNodeId) && !data.unlockedSkillNodes.Contains(def.prerequisiteNodeId))
        {
            SkillTreeNodeDefinition prereq = allNodeDefinitions.Find(n => n.nodeId == def.prerequisiteNodeId);
            string prereqTitle = prereq != null ? prereq.title : def.prerequisiteNodeId;
            if (bottomStatusBannerText != null)
                bottomStatusBannerText.text = $"ต้องปลดล็อกโหนดก่อนหน้า [{prereqTitle}] ก่อน! ({def.title}: {def.description})";
            return false;
        }

        if (data.researchPoints < def.costRP)
        {
            if (bottomStatusBannerText != null)
                bottomStatusBannerText.text = $"RP ไม่เพียงพอสำหรับ [{def.title}] (ต้องการ {def.costRP} RP | มีอยู่ {data.researchPoints} RP) — {def.description}";
            return false;
        }

        data.researchPoints -= def.costRP;
        data.unlockedSkillNodes.Add(nodeId);

        int[] branches = GetBranchArray(data, def.character);
        if (branches != null)
        {
            int idx = Mathf.Clamp(def.mappedBranchIndex, 0, branches.Length - 1);
            branches[idx] = Mathf.Min(UpgradeCostTiers.Length, branches[idx] + 1);
        }

        SaveSystem.SaveGame(data);

        if (bottomStatusBannerText != null)
        {
            bottomStatusBannerText.text = $"ปลดล็อกสำเร็จ! [{def.title}] (-{def.costRP} RP) : {def.description}";
        }

        Debug.Log($"[ResearchTree] Unlocked Node '{def.nodeId}' ({def.title}) for {def.costRP} RP! Remaining RP: {data.researchPoints}");

        RefreshAllNodeTreeVisuals();
        OnTreeUpdated?.Invoke();
        return true;
    }

    /// <summary>
    /// คำนวณบัฟสเตตัสเริ่มต้นทั้งหมดจาก Skill Tree ของตัวละครที่เลือก เพื่อส่งเข้า PlayerStats ตอนเริ่มรันใหม่
    /// </summary>
    public static CharacterMetaBonuses CalculateCharacterMetaBonuses(SubjectCharacterType character)
    {
        GameData data = SaveSystem.LoadGame();
        CharacterMetaBonuses bonuses = new CharacterMetaBonuses
        {
            character = character
        };

        if (data == null) return bonuses;
        data.EnsureInitialized();

        List<string> unlocked = data.unlockedSkillNodes;

        switch (character)
        {
            case SubjectCharacterType.Vanguard_N01:
            {
                int armorLv = data.vanguardBranchLevels[0];
                int reflectLv = data.vanguardBranchLevels[1];
                int regenLv = data.vanguardBranchLevels[2];
                int knockbackLv = data.vanguardBranchLevels[3];

                bonuses.flatArmor = armorLv * 4f
                    + (unlocked.Contains("N01_Minor_Top1") ? 5f : 0f)
                    + (unlocked.Contains("N01_Major_2") ? 5f : 0f);
                bonuses.maxHpPercentBonus = (armorLv >= 2 || unlocked.Contains("N01_Minor_Top2")) ? 0.10f : 0f;
                bonuses.flatMaxHpBonus = data.hpUpgradeLevel * 10f;

                bonuses.reflectArmorRatio = (reflectLv > 0 || unlocked.Contains("N01_Major_3"))
                    ? Mathf.Max(1.50f, 0.50f + reflectLv * 0.25f)
                    : 0f;
                bonuses.hpRegenPerSecond = regenLv * 0.6f
                    + (unlocked.Contains("N01_Minor_Bottom") ? 0.8f : 0f)
                    + (unlocked.Contains("N01_Major_5") ? 1.5f : 0f);
                bonuses.knockbackMultiplierBonus = knockbackLv * 0.12f
                    + (unlocked.Contains("N01_Minor_Top2") ? 0.15f : 0f)
                    + (unlocked.Contains("N01_Major_1") ? 0.30f : 0f);
                bonuses.damagePercentBonus = unlocked.Contains("N01_Major_4") ? 0.20f : (knockbackLv * 0.03f);
                bonuses.attackSpeedCooldownBonus = unlocked.Contains("N01_Minor_Bottom") ? 0.05f : 0f;

                bonuses.hasBlastNovaMajor = unlocked.Contains("N01_Major_1") || knockbackLv >= 4;
                bonuses.hasAbsoluteArmorMajor = unlocked.Contains("N01_Major_2") || armorLv >= 5;
                bonuses.hasKineticReflectMajor = unlocked.Contains("N01_Major_3") || reflectLv >= 4;
                bonuses.hasJuggernautMomentumMajor = unlocked.Contains("N01_Major_4") || knockbackLv >= 6;
                bonuses.hasTitanRevitalizationMajor = unlocked.Contains("N01_Major_5") || regenLv >= 5;
                break;
            }

            case SubjectCharacterType.Psionic_N02:
            {
                int critLv = data.psionicBranchLevels[0];
                int dotLv = data.psionicBranchLevels[1];
                int stunLv = data.psionicBranchLevels[2];
                int aoeLv = data.psionicBranchLevels[3];

                bonuses.critChanceBonus = critLv * 0.03f + (unlocked.Contains("N02_Minor_Left1") ? 0.05f : 0f);
                bonuses.critDamageBonus = critLv * 0.10f + (unlocked.Contains("N02_Minor_Left1") ? 0.15f : 0f);
                bonuses.dotDamageMultiplierBonus = dotLv * 0.12f
                    + (unlocked.Contains("N02_Minor_Bottom") ? 0.20f : 0f)
                    + (unlocked.Contains("N02_Major_4") ? 0.25f : 0f);
                bonuses.stunDurationBonusSeconds = stunLv * 0.15f
                    + (unlocked.Contains("N02_Minor_Top1") ? 0.25f : 0f);
                bonuses.aoeSizePercentBonus = aoeLv * 0.08f
                    + (unlocked.Contains("N02_Minor_Top1") ? 0.10f : 0f)
                    + (unlocked.Contains("N02_Major_2") ? 0.15f : 0f)
                    + (unlocked.Contains("N02_Major_3") ? 0.15f : 0f);

                bonuses.hasMindCollapseMajor = unlocked.Contains("N02_Major_1") || stunLv >= 4;
                bonuses.hasPsionicBarrierMajor = unlocked.Contains("N02_Major_2") || critLv >= 4;
                bonuses.hasOverchargeNovaMajor = unlocked.Contains("N02_Major_3") || aoeLv >= 4;
                bonuses.hasVenomCatalystMajor = unlocked.Contains("N02_Major_4") || dotLv >= 5;
                bonuses.hasTemporalRiftMajor = unlocked.Contains("N02_Major_5") || stunLv >= 6;
                break;
            }

            case SubjectCharacterType.CyberSwift_N03:
            {
                int moveSpdLv = data.cyberSwiftBranchLevels[0];
                int dashCdLv = data.cyberSwiftBranchLevels[1];
                int atkSpdLv = data.cyberSwiftBranchLevels[2];
                int iFrameLv = data.cyberSwiftBranchLevels[3];

                bonuses.moveSpeedPercentBonus = moveSpdLv * 0.04f + (unlocked.Contains("N03_Minor_Right1") ? 0.06f : 0f);
                bonuses.dashCooldownReductionPercent = Mathf.Clamp(
                    dashCdLv * 0.06f + (unlocked.Contains("N03_Minor_Left2") ? 0.12f : 0f),
                    0f, 0.45f);
                bonuses.dashDistancePercentBonus = unlocked.Contains("N03_Minor_Left2") ? 0.10f : 0f;
                bonuses.attackSpeedCooldownBonus = atkSpdLv * 0.04f
                    + (unlocked.Contains("N03_Minor_Bottom") ? 0.06f : 0f)
                    + (unlocked.Contains("N03_Major_3") ? 0.10f : 0f);
                bonuses.iFrameDurationBonusSeconds = iFrameLv * 0.08f
                    + (unlocked.Contains("N03_Minor_Left2") ? 0.10f : 0f)
                    + (unlocked.Contains("N03_Major_2") ? 0.20f : 0f);
                bonuses.bonusDashCharges = (dashCdLv >= 5 || unlocked.Contains("N03_Major_4")) ? 1 : 0;

                bonuses.hasPhantomCloneMajor = unlocked.Contains("N03_Major_1") || moveSpdLv >= 4;
                bonuses.hasBulletTimeMajor = unlocked.Contains("N03_Major_2") || iFrameLv >= 4;
                bonuses.hasOverclockOverdriveMajor = unlocked.Contains("N03_Major_3") || atkSpdLv >= 4;
                bonuses.hasPhaseShifterMajor = bonuses.bonusDashCharges > 0;
                bonuses.hasLightningReflexesMajor = unlocked.Contains("N03_Major_5") || iFrameLv >= 6;
                break;
            }
        }

        return bonuses;
    }

    #region 3-Column Visual Node Tree UI Builder & Edit Mode Persistence

    private Transform ResolveResearchPanel()
    {
        if (researchPanelOverride != null) return researchPanelOverride;

        ResearchNodeUI anyLegacyNode = FindFirstObjectByType<ResearchNodeUI>(FindObjectsInactive.Include);
        if (anyLegacyNode != null && anyLegacyNode.transform.parent != null && anyLegacyNode.transform.parent.name == "ResearchPanel")
        {
            return anyLegacyNode.transform.parent;
        }

        GameObject foundByName = GameObject.Find("ResearchPanel");
        if (foundByName != null) return foundByName.transform;

        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform tr = canvases[i].transform.Find("ResearchPanel");
            if (tr != null) return tr;
        }

        return null;
    }

    /// <summary>
    /// ContextMenu สำหรับสั่ง Generate ตัว ThreeColumnNodeTreeRoot ลงใน ResearchPanel ในโหมด Edit Mode โดยตรง
    /// พร้อมบันทึกเป็น Prefab ลงใน Assets/Prefabs/UI/ThreeColumnNodeTreeRoot.prefab และ MarkSceneDirty ให้เซฟลง Scene ได้ถาวร
    /// </summary>
    [ContextMenu("Generate ThreeColumnNodeTreeRoot in Scene & Save Prefab")]
    public void EditorGenerateNodeTreeInSceneAndSavePrefab()
    {
        EnsureNodeTreeVisualUI(false);
#if UNITY_EDITOR
        SaveGeneratedTreeAsPrefabAndMarkSceneDirty();
#endif
    }

    [ContextMenu("Rebuild ThreeColumnNodeTreeRoot (Force Reset)")]
    public void EditorRebuildNodeTreeInSceneAndSavePrefab()
    {
        EnsureNodeTreeVisualUI(true);
#if UNITY_EDITOR
        SaveGeneratedTreeAsPrefabAndMarkSceneDirty();
#endif
    }

    /// <summary>
    /// สร้างหรือผูก (Bind) UI ผัง Research Lab แบบ Node Tree 3 คอลัมน์:
    /// หากมี ThreeColumnNodeTreeRoot อยู่ใน Scene แล้ว (จาก Edit Mode) จะทำการ Bind เข้ากับ 오브เจกต์เดิมทันทีโดยไม่ลบของที่จัดไว้
    /// </summary>
    public void EnsureNodeTreeVisualUI(bool forceRebuild = false)
    {
        if (allNodeDefinitions.Count == 0) BuildSkillTreeDefinitions();

        Transform researchPanel = ResolveResearchPanel();
        if (researchPanel == null) return;

        // ซ่อนการ์ดแบบเก่าทั้ง 4 ใบเพื่อแสดงผัง Node Tree 3 คอลัมน์แทน
        ResearchNodeUI[] legacyNodes = researchPanel.GetComponentsInChildren<ResearchNodeUI>(true);
        for (int i = 0; i < legacyNodes.Length; i++)
        {
            if (legacyNodes[i] != null)
            {
                legacyNodes[i].gameObject.SetActive(false);
            }
        }

        Image panelBg = researchPanel.GetComponent<Image>();
        if (panelBg != null)
        {
            panelBg.sprite = null;
            panelBg.color = new Color(0.06f, 0.08f, 0.12f, 0.98f);
        }

        Transform existingRoot = researchPanel.Find(TREE_ROOT_OBJECT_NAME);
        if (existingRoot != null)
        {
            if (forceRebuild)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabInstance(existingRoot.gameObject))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        PrefabUtility.GetOutermostPrefabInstanceRoot(existingRoot.gameObject),
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction
                    );
                }
#endif
                if (Application.isPlaying)
                    Destroy(existingRoot.gameObject);
                else
                    DestroyImmediate(existingRoot.gameObject);
            }
            else
            {
                BindExistingNodeTreeVisualUI(existingRoot);
                SetupResearchBackButton(researchPanel);
                RefreshAllNodeTreeVisuals();
                return;
            }
        }

        spawnedNodeVisuals.Clear();

        GameObject treeRoot = new GameObject(TREE_ROOT_OBJECT_NAME, typeof(RectTransform));
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(treeRoot, "Generate ThreeColumnNodeTreeRoot");
        }
#endif
        treeRoot.transform.SetParent(researchPanel, false);
        RectTransform rootRt = treeRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = new Vector2(0f, -10f);
        rootRt.sizeDelta = new Vector2(1760f, 700f);

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // สร้าง 3 คอลัมน์แยก 3 ตัวละคร: เขียว (N-01), แดง (N-02), ส้ม (N-03)
        BuildCharacterColumn(
            treeRoot.transform,
            SubjectCharacterType.Vanguard_N01,
            "N-01 : VANGUARD",
            "Armor • Reflect • HP Regen • Knockback",
            VanguardGreen,
            new Vector2(-560f, 15f),
            font);

        BuildCharacterColumn(
            treeRoot.transform,
            SubjectCharacterType.Psionic_N02,
            "N-02 : PSIONIC",
            "Crit Chance • DoT Dmg • Stun • AOE",
            PsionicRed,
            new Vector2(0f, 15f),
            font);

        BuildCharacterColumn(
            treeRoot.transform,
            SubjectCharacterType.CyberSwift_N03,
            "N-03 : CYBER-SWIFT",
            "Move Speed • Dash CD • Atk Spd • I-Frame",
            CyberSwiftOrange,
            new Vector2(560f, 15f),
            font);

        // แถบแสดงคำอธิบายโหนดด้านล่าง
        GameObject bannerObj = new GameObject("NodeInfoBanner", typeof(RectTransform), typeof(Image));
        bannerObj.transform.SetParent(treeRoot.transform, false);
        RectTransform bannerRt = bannerObj.GetComponent<RectTransform>();
        bannerRt.anchorMin = new Vector2(0.5f, 0.5f);
        bannerRt.anchorMax = new Vector2(0.5f, 0.5f);
        bannerRt.pivot = new Vector2(0.5f, 0.5f);
        bannerRt.anchoredPosition = new Vector2(0f, -335f);
        bannerRt.sizeDelta = new Vector2(1580f, 42f);

        Image bannerImg = bannerObj.GetComponent<Image>();
        bannerImg.color = new Color(0.10f, 0.14f, 0.20f, 0.92f);

        GameObject bannerTxtObj = new GameObject("BannerText", typeof(RectTransform), typeof(Text));
        bannerTxtObj.transform.SetParent(bannerObj.transform, false);
        RectTransform bannerTxtRt = bannerTxtObj.GetComponent<RectTransform>();
        bannerTxtRt.anchorMin = Vector2.zero;
        bannerTxtRt.anchorMax = Vector2.one;
        bannerTxtRt.offsetMin = new Vector2(16f, 0f);
        bannerTxtRt.offsetMax = new Vector2(-16f, 0f);

        bottomStatusBannerText = bannerTxtObj.GetComponent<Text>();
        bottomStatusBannerText.font = font;
        bottomStatusBannerText.fontSize = 18;
        bottomStatusBannerText.alignment = TextAnchor.MiddleCenter;
        bottomStatusBannerText.color = new Color(0.85f, 0.95f, 1f, 1f);
        bottomStatusBannerText.text = "คลิกที่ Node ในผังเพื่อปลดล็อก (ลำดับขั้นราคา: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP)";
        bottomStatusBannerText.raycastTarget = false;

        Transform titleTr = researchPanel.Find("TitleText (Research)");
        if (titleTr != null)
        {
            Text titleTxt = titleTr.GetComponent<Text>();
            if (titleTxt != null)
            {
                titleTxt.color = new Color(0.92f, 0.97f, 1f, 1f);
            }
            RectTransform titleRt = titleTr.GetComponent<RectTransform>();
            if (titleRt != null)
            {
                titleRt.anchoredPosition = new Vector2(0f, -52f);
                titleRt.sizeDelta = new Vector2(600f, 44f);
            }
        }

        SetupResearchBackButton(researchPanel);

        RefreshAllNodeTreeVisuals();
    }

    /// <summary>
    /// ตั้งค่าปุ่ม [ < BACK ] ขนาดกว้าง 180px สูง 50px ที่มุมซ้ายล่างของ ResearchPanel
    /// ผูก Event onClick ให้สั่งปิด ResearchPanel และเปิด MainPanel กลับขึ้นมา
    /// </summary>
    public void SetupResearchBackButton(Transform panel = null)
    {
        Transform rPanel = panel != null ? panel : ResolveResearchPanel();
        if (rPanel == null) return;

        Transform backBtnTr = rPanel.Find("BackButton");
        GameObject backBtnObj;
        if (backBtnTr == null)
        {
            backBtnObj = new GameObject("BackButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            backBtnObj.transform.SetParent(rPanel, false);
            backBtnTr = backBtnObj.transform;
        }
        else
        {
            backBtnObj = backBtnTr.gameObject;
        }

        backBtnObj.SetActive(true);
        backBtnTr.SetAsLastSibling(); // วางบนสุดเสมอเพื่อให้คลิกได้แน่นอน

        // 1. RectTransform: Anchor ซ้ายล่าง (Min: 0, 0 / Max: 0, 0 / Pivot: 0.5, 0.5), AnchoredPosition: (120, 60), ขนาด: 180px x 55px
        RectTransform backRt = backBtnTr.GetComponent<RectTransform>();
        if (backRt == null) backRt = backBtnObj.AddComponent<RectTransform>();
        backRt.anchorMin = new Vector2(0f, 0f);
        backRt.anchorMax = new Vector2(0f, 0f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(120f, 60f); // วางมุมซ้ายล่าง (120, 60)
        backRt.sizeDelta = new Vector2(180f, 55f);       // กว้าง 180px สูง 55px
        backRt.localScale = Vector3.one;

        // 2. Image: พื้นหลังสีเข้มสไตล์ Dark Cyber-Lab (#0A0D14)
        Image btnImg = backBtnTr.GetComponent<Image>();
        if (btnImg == null) btnImg = backBtnObj.AddComponent<Image>();
        btnImg.sprite = null;
        btnImg.color = new Color(0.04f, 0.05f, 0.08f, 1f);
        btnImg.raycastTarget = true;

        // 3. Neon Cyan Border Outline (2px)
        Outline outline = backBtnTr.GetComponent<Outline>();
        if (outline == null) outline = backBtnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        // 4. Hover Animation
        if (backBtnObj.GetComponent<CardHoverEffect>() == null)
        {
            backBtnObj.AddComponent<CardHoverEffect>();
        }

        // 5. Button Colors & onClick: ปิด ResearchPanel และเปิด MainPanel
        Button btn = backBtnTr.GetComponent<Button>();
        if (btn == null) btn = backBtnObj.AddComponent<Button>();

        var colors = btn.colors;
        colors.normalColor = new Color(0.04f, 0.05f, 0.08f, 1f);
        colors.highlightedColor = new Color(0f, 0.9f, 1f, 0.25f);
        colors.pressedColor = new Color(0f, 0.6f, 0.8f, 0.5f);
        colors.disabledColor = new Color(0.15f, 0.2f, 0.25f, 0.5f);
        btn.colors = colors;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(CloseResearchLab);

        // 6. Label Text: < BACK (Font 22sp Bold White)
        Text btnText = backBtnTr.GetComponentInChildren<Text>();
        if (btnText == null)
        {
            GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            txtObj.transform.SetParent(backBtnTr, false);
            btnText = txtObj.GetComponent<Text>();
        }

        RectTransform txtRt = btnText.GetComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero;
        txtRt.anchorMax = Vector2.one;
        txtRt.sizeDelta = Vector2.zero;
        txtRt.anchoredPosition = Vector2.zero;

        Font font = InGameHUDController.GetDefaultFont();
        btnText.text = "< BACK";
        btnText.font = font;
        btnText.fontSize = 22;
        btnText.fontStyle = FontStyle.Bold;
        btnText.color = Color.white;
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.raycastTarget = false;
    }

    public void CloseResearchLab()
    {
        Transform rPanel = ResolveResearchPanel();
        if (rPanel != null)
        {
            rPanel.gameObject.SetActive(false);
        }

        if (MainMenuManager.Instance != null)
        {
            MainMenuManager.Instance.ShowMainPanel();
        }
        else
        {
            Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas != null)
            {
                Transform mainP = canvas.transform.Find("MainPanel");
                if (mainP != null) mainP.gameObject.SetActive(true);
            }
        }
    }

    /// <summary>
    /// ผูก (Bind) สคริปต์เข้ากับ ThreeColumnNodeTreeRoot ที่มีอยู่แล้วใน Scene (จาก Edit Mode / Prefab)
    /// </summary>
    private void BindExistingNodeTreeVisualUI(Transform treeRoot)
    {
        if (treeRoot == null) return;
        spawnedNodeVisuals.Clear();

        Transform bannerTxtTr = treeRoot.Find("NodeInfoBanner/BannerText");
        if (bannerTxtTr != null)
        {
            bottomStatusBannerText = bannerTxtTr.GetComponent<Text>();
        }

        Transform rPanel = ResolveResearchPanel();

        for (int i = 0; i < allNodeDefinitions.Count; i++)
        {
            SkillTreeNodeDefinition def = allNodeDefinitions[i];
            Color themeColor = GetCharacterThemeColor(def.character);

            Transform colTr = treeRoot.Find($"Column_{def.character}");
            if (colTr == null) continue;

            Transform nodeTr = colTr.Find($"NodesLayer/Node_{def.nodeId}");
            if (nodeTr == null) continue;

            Image outerImg = nodeTr.GetComponent<Image>();
            Transform bodyTr = nodeTr.Find("NodeBody");
            Image bodyImg = bodyTr != null ? bodyTr.GetComponent<Image>() : null;
            Transform titleTr = nodeTr.Find("TitleText");
            Text titleTxt = titleTr != null ? titleTr.GetComponent<Text>() : null;
            Transform costTr = nodeTr.Find("CostText");
            Text costTxt = costTr != null ? costTr.GetComponent<Text>() : null;
            Button btn = nodeTr.GetComponent<Button>();

            if (def.nodeType == SkillNodeTierType.Core)
            {
                Sprite circle = GetOrCreateCircleSprite();
                if (outerImg != null && outerImg.sprite == null) outerImg.sprite = circle;
                if (bodyImg != null && bodyImg.sprite == null) bodyImg.sprite = circle;
            }

            Image lineImg = null;
            if (!string.IsNullOrEmpty(def.prerequisiteNodeId))
            {
                Transform lineTr = colTr.Find($"LinesLayer/Line_{def.prerequisiteNodeId}_to_{def.nodeId}");
                if (lineTr == null && rPanel != null)
                {
                    foreach (Transform child in rPanel)
                    {
                        if (child != null && child.name.ToLowerInvariant().Contains("lineslayer"))
                        {
                            lineTr = child.Find($"Line_{def.prerequisiteNodeId}_to_{def.nodeId}");
                            if (lineTr != null) break;
                        }
                    }
                }

                if (lineTr != null)
                {
                    lineImg = lineTr.GetComponent<Image>();
                }
            }

            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string capturedNodeId = def.nodeId;
                SubjectCharacterType capturedChar = def.character;
                btn.onClick.AddListener(() =>
                {
                    selectedTreeCharacter = capturedChar;
                    TryUnlockNodeById(capturedNodeId);
                });
            }

            spawnedNodeVisuals.Add(new NodeVisualEntry
            {
                definition = def,
                themeColor = themeColor,
                outerRingImage = outerImg,
                nodeBodyImage = bodyImg,
                titleText = titleTxt,
                costOrStateText = costTxt,
                button = btn,
                connectingLineToParent = lineImg
            });
        }
    }

    private static Color GetCharacterThemeColor(SubjectCharacterType character)
    {
        return character switch
        {
            SubjectCharacterType.Vanguard_N01 => VanguardGreen,
            SubjectCharacterType.Psionic_N02 => PsionicRed,
            SubjectCharacterType.CyberSwift_N03 => CyberSwiftOrange,
            _ => VanguardGreen
        };
    }

    private void BuildCharacterColumn(
        Transform parent,
        SubjectCharacterType character,
        string headerTitle,
        string subTitle,
        Color themeColor,
        Vector2 columnAnchoredPos,
        Font font)
    {
        GameObject colObj = new GameObject($"Column_{character}", typeof(RectTransform), typeof(Image));
        colObj.transform.SetParent(parent, false);
        RectTransform colRt = colObj.GetComponent<RectTransform>();
        colRt.anchorMin = new Vector2(0.5f, 0.5f);
        colRt.anchorMax = new Vector2(0.5f, 0.5f);
        colRt.pivot = new Vector2(0.5f, 0.5f);
        colRt.anchoredPosition = columnAnchoredPos;
        colRt.sizeDelta = new Vector2(510f, 620f);

        Image colBg = colObj.GetComponent<Image>();
        colBg.color = new Color(themeColor.r * 0.12f, themeColor.g * 0.12f, themeColor.b * 0.12f, 0.72f);
        colBg.raycastTarget = false;

        GameObject topAccent = new GameObject("TopAccent", typeof(RectTransform), typeof(Image));
        topAccent.transform.SetParent(colObj.transform, false);
        RectTransform accentRt = topAccent.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.anchoredPosition = Vector2.zero;
        accentRt.sizeDelta = new Vector2(0f, 6f);
        Image accentImg = topAccent.GetComponent<Image>();
        accentImg.color = themeColor;
        accentImg.raycastTarget = false;

        GameObject headerObj = new GameObject("HeaderTitle", typeof(RectTransform), typeof(Text));
        headerObj.transform.SetParent(colObj.transform, false);
        RectTransform headerRt = headerObj.GetComponent<RectTransform>();
        headerRt.anchoredPosition = new Vector2(0f, 282f);
        headerRt.sizeDelta = new Vector2(480f, 30f);
        Text headerTxt = headerObj.GetComponent<Text>();
        headerTxt.font = font;
        headerTxt.text = headerTitle;
        headerTxt.fontSize = 21;
        headerTxt.fontStyle = FontStyle.Bold;
        headerTxt.alignment = TextAnchor.MiddleCenter;
        headerTxt.color = themeColor;
        headerTxt.raycastTarget = false;

        GameObject subObj = new GameObject("SubTitle", typeof(RectTransform), typeof(Text));
        subObj.transform.SetParent(colObj.transform, false);
        RectTransform subRt = subObj.GetComponent<RectTransform>();
        subRt.anchoredPosition = new Vector2(0f, 258f);
        subRt.sizeDelta = new Vector2(490f, 22f);
        Text subTxt = subObj.GetComponent<Text>();
        subTxt.font = font;
        subTxt.text = subTitle;
        subTxt.fontSize = 14;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subTxt.color = new Color(0.80f, 0.86f, 0.92f, 0.85f);
        subTxt.raycastTarget = false;

        // บังคับให้ LinesLayer ถูกสร้างเป็นลูกของ ResearchPanel โดยตรง (เพื่อให้ปิด/ซ่อนพร้อมกับ ResearchPanel ได้อย่างสมบูรณ์)
        Transform rPanel = ResolveResearchPanel();
        Transform linesParent = rPanel != null ? rPanel : colObj.transform;

        GameObject linesContainer = new GameObject($"LinesLayer_{character}", typeof(RectTransform));
        linesContainer.transform.SetParent(linesParent, false);
        RectTransform linesRt = linesContainer.GetComponent<RectTransform>();
        linesRt.anchorMin = new Vector2(0.5f, 0.5f);
        linesRt.anchorMax = new Vector2(0.5f, 0.5f);
        linesRt.pivot = new Vector2(0.5f, 0.5f);
        linesRt.anchoredPosition = rPanel != null ? (new Vector2(0f, -10f) + columnAnchoredPos) : Vector2.zero;
        linesRt.sizeDelta = Vector2.zero;
        if (rPanel != null && !rPanel.gameObject.activeSelf)
        {
            linesContainer.SetActive(false);
        }

        GameObject nodesContainer = new GameObject("NodesLayer", typeof(RectTransform));
        nodesContainer.transform.SetParent(colObj.transform, false);
        RectTransform nodesRt = nodesContainer.GetComponent<RectTransform>();
        nodesRt.anchorMin = new Vector2(0.5f, 0.5f);
        nodesRt.anchorMax = new Vector2(0.5f, 0.5f);
        nodesRt.anchoredPosition = Vector2.zero;
        nodesRt.sizeDelta = Vector2.zero;

        List<SkillTreeNodeDefinition> charNodes = GetNodesForCharacter(character);
        Dictionary<string, SkillTreeNodeDefinition> nodeMap = new Dictionary<string, SkillTreeNodeDefinition>();
        for (int i = 0; i < charNodes.Count; i++)
        {
            nodeMap[charNodes[i].nodeId] = charNodes[i];
        }

        for (int i = 0; i < charNodes.Count; i++)
        {
            SkillTreeNodeDefinition def = charNodes[i];
            Image lineImg = null;

            if (!string.IsNullOrEmpty(def.prerequisiteNodeId) && nodeMap.TryGetValue(def.prerequisiteNodeId, out SkillTreeNodeDefinition parentDef))
            {
                lineImg = CreateConnectingLineImage(linesContainer.transform, parentDef.anchoredPosition, def.anchoredPosition, themeColor, $"Line_{parentDef.nodeId}_to_{def.nodeId}");
            }

            NodeVisualEntry entry = CreateNodeUIElement(nodesContainer.transform, def, themeColor, font);
            entry.connectingLineToParent = lineImg;
            spawnedNodeVisuals.Add(entry);
        }
    }

    private Image CreateConnectingLineImage(Transform linesParent, Vector2 fromPos, Vector2 toPos, Color themeColor, string lineName)
    {
        GameObject lineObj = new GameObject(lineName, typeof(RectTransform), typeof(Image));
        lineObj.transform.SetParent(linesParent, false);

        Vector2 diff = toPos - fromPos;
        float distance = diff.magnitude;
        float angle = Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg;

        RectTransform rt = lineObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = (fromPos + toPos) * 0.5f;
        rt.sizeDelta = new Vector2(distance, 4.5f);
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);

        Image img = lineObj.GetComponent<Image>();
        img.color = new Color(themeColor.r * 0.35f, themeColor.g * 0.35f, themeColor.b * 0.35f, 0.35f);
        img.raycastTarget = false;

        return img;
    }

    private NodeVisualEntry CreateNodeUIElement(Transform nodesParent, SkillTreeNodeDefinition def, Color themeColor, Font font)
    {
        bool isCore = def.nodeType == SkillNodeTierType.Core;
        Vector2 nodeSize = isCore ? new Vector2(88f, 88f) : new Vector2(182f, 64f);

        GameObject nodeObj = new GameObject($"Node_{def.nodeId}", typeof(RectTransform), typeof(Image), typeof(Button));
        nodeObj.transform.SetParent(nodesParent, false);

        RectTransform rt = nodeObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = def.anchoredPosition;
        rt.sizeDelta = nodeSize;

        Image outerImg = nodeObj.GetComponent<Image>();
        if (isCore)
        {
            outerImg.sprite = GetOrCreateCircleSprite();
            outerImg.type = Image.Type.Simple;
        }
        outerImg.color = themeColor;

        GameObject bodyObj = new GameObject("NodeBody", typeof(RectTransform), typeof(Image));
        bodyObj.transform.SetParent(nodeObj.transform, false);
        RectTransform bodyRt = bodyObj.GetComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(3f, 3f);
        bodyRt.offsetMax = new Vector2(-3f, -3f);

        Image bodyImg = bodyObj.GetComponent<Image>();
        if (isCore)
        {
            bodyImg.sprite = GetOrCreateCircleSprite();
            bodyImg.type = Image.Type.Simple;
        }
        bodyImg.raycastTarget = false;

        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleObj.transform.SetParent(nodeObj.transform, false);
        RectTransform titleRt = titleObj.GetComponent<RectTransform>();
        titleRt.anchorMin = Vector2.zero;
        titleRt.anchorMax = Vector2.one;
        titleRt.offsetMin = isCore ? new Vector2(4f, 16f) : new Vector2(6f, 22f);
        titleRt.offsetMax = isCore ? new Vector2(-4f, -6f) : new Vector2(-6f, -4f);

        Text titleTxt = titleObj.GetComponent<Text>();
        titleTxt.font = font;
        titleTxt.text = def.title;
        titleTxt.fontSize = isCore ? 14 : 15;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleTxt.color = Color.white;
        titleTxt.raycastTarget = false;

        GameObject costObj = new GameObject("CostText", typeof(RectTransform), typeof(Text));
        costObj.transform.SetParent(nodeObj.transform, false);
        RectTransform costRt = costObj.GetComponent<RectTransform>();
        costRt.anchorMin = Vector2.zero;
        costRt.anchorMax = Vector2.one;
        costRt.offsetMin = new Vector2(6f, 4f);
        costRt.offsetMax = isCore ? new Vector2(-6f, -42f) : new Vector2(-6f, -32f);

        Text costTxt = costObj.GetComponent<Text>();
        costTxt.font = font;
        costTxt.text = isCore ? "CORE" : $"{def.costRP} RP";
        costTxt.fontSize = 13;
        costTxt.alignment = TextAnchor.MiddleCenter;
        costTxt.color = new Color(0.9f, 0.95f, 1f, 0.9f);
        costTxt.raycastTarget = false;

        Button btn = nodeObj.GetComponent<Button>();
        btn.targetGraphic = outerImg;
        string capturedNodeId = def.nodeId;
        SubjectCharacterType capturedChar = def.character;
        btn.onClick.AddListener(() =>
        {
            selectedTreeCharacter = capturedChar;
            TryUnlockNodeById(capturedNodeId);
        });

        return new NodeVisualEntry
        {
            definition = def,
            themeColor = themeColor,
            outerRingImage = outerImg,
            nodeBodyImage = bodyImg,
            titleText = titleTxt,
            costOrStateText = costTxt,
            button = btn
        };
    }

    public void RefreshAllNodeTreeVisuals()
    {
        if (spawnedNodeVisuals.Count == 0) return;

        int currentRP = GetCurrentResearchPoints();

        for (int i = 0; i < spawnedNodeVisuals.Count; i++)
        {
            NodeVisualEntry entry = spawnedNodeVisuals[i];
            if (entry == null || entry.definition == null || entry.nodeBodyImage == null) continue;

            SkillTreeNodeDefinition def = entry.definition;
            Color c = entry.themeColor;
            bool isUnlocked = IsNodeUnlocked(def.nodeId);
            bool prereqUnlocked = string.IsNullOrEmpty(def.prerequisiteNodeId) || IsNodeUnlocked(def.prerequisiteNodeId);
            bool canAfford = currentRP >= def.costRP;

            if (isUnlocked)
            {
                entry.outerRingImage.color = Color.Lerp(c, Color.white, 0.35f);
                entry.nodeBodyImage.color = new Color(c.r, c.g, c.b, 1.0f);
                entry.titleText.color = new Color(0.05f, 0.08f, 0.10f, 1f);
                entry.costOrStateText.color = new Color(0.08f, 0.12f, 0.15f, 0.95f);
                entry.costOrStateText.text = def.nodeType == SkillNodeTierType.Core ? "ACTIVE" : "UNLOCKED";

                if (entry.connectingLineToParent != null)
                {
                    entry.connectingLineToParent.color = new Color(c.r, c.g, c.b, 0.96f);
                }
            }
            else if (prereqUnlocked)
            {
                float alpha = canAfford ? 0.58f : 0.38f;
                entry.outerRingImage.color = new Color(c.r * 0.75f, c.g * 0.75f, c.b * 0.75f, alpha + 0.15f);
                entry.nodeBodyImage.color = new Color(c.r * 0.32f, c.g * 0.32f, c.b * 0.32f, alpha);
                entry.titleText.color = new Color(1f, 1f, 1f, canAfford ? 0.95f : 0.68f);
                entry.costOrStateText.color = canAfford ? new Color(0.95f, 1f, 0.65f, 0.95f) : new Color(0.85f, 0.85f, 0.85f, 0.6f);
                entry.costOrStateText.text = $"{def.costRP} RP";

                if (entry.connectingLineToParent != null)
                {
                    entry.connectingLineToParent.color = new Color(c.r * 0.55f, c.g * 0.55f, c.b * 0.55f, 0.45f);
                }
            }
            else
            {
                entry.outerRingImage.color = new Color(c.r * 0.30f, c.g * 0.30f, c.b * 0.30f, 0.32f);
                entry.nodeBodyImage.color = new Color(c.r * 0.18f, c.g * 0.18f, c.b * 0.18f, 0.30f);
                entry.titleText.color = new Color(0.75f, 0.78f, 0.82f, 0.45f);
                entry.costOrStateText.color = new Color(0.68f, 0.72f, 0.76f, 0.42f);
                entry.costOrStateText.text = $"LOCKED ({def.costRP} RP)";

                if (entry.connectingLineToParent != null)
                {
                    entry.connectingLineToParent.color = new Color(c.r * 0.25f, c.g * 0.25f, c.b * 0.25f, 0.22f);
                }
            }
        }
    }

    private static Sprite GetOrCreateCircleSprite()
    {
#if UNITY_EDITOR
        // ใช้ Built-in Knob Sprite ของ Unity ใน Edit Mode / Prefab เพื่อให้ Serialize ลง Scene และ Prefab ได้อย่างถาวร
        Sprite builtinKnob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        if (builtinKnob != null) return builtinKnob;
#endif
        if (cachedCircleSprite != null) return cachedCircleSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        float center = (size - 1) * 0.5f;
        float radius = center - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - dist + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply();
        cachedCircleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return cachedCircleSprite;
    }

#if UNITY_EDITOR
    private void SaveGeneratedTreeAsPrefabAndMarkSceneDirty()
    {
        Transform researchPanel = ResolveResearchPanel();
        if (researchPanel == null) return;

        Transform treeRoot = researchPanel.Find(TREE_ROOT_OBJECT_NAME);
        if (treeRoot == null) return;

        if (!Directory.Exists(PREFAB_FOLDER_PATH))
        {
            Directory.CreateDirectory(PREFAB_FOLDER_PATH);
            AssetDatabase.Refresh();
        }

        // บันทึกก้อน UI ออกมาเป็น Prefab เก็บไว้ใน Assets/Prefabs/UI/ThreeColumnNodeTreeRoot.prefab
        if (!Application.isPlaying)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                treeRoot.gameObject,
                PREFAB_ASSET_PATH,
                InteractionMode.UserAction);

            EditorUtility.SetDirty(researchPanel.gameObject);
            EditorUtility.SetDirty(gameObject);
            if (gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(gameObject.scene);
                EditorSceneManager.SaveScene(gameObject.scene);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[ResearchTreeManager] Generated '{TREE_ROOT_OBJECT_NAME}' in Scene (Edit Mode) and saved Prefab to '{PREFAB_ASSET_PATH}'!");
        }
    }

    [MenuItem("Tools/Research Lab/Generate 3-Column Node Tree in Scene & Save Prefab")]
    public static void MenuGenerateNodeTreeInScene()
    {
        ResearchTreeManager manager = FindFirstObjectByType<ResearchTreeManager>(FindObjectsInactive.Include);
        if (manager == null)
        {
            Debug.LogWarning("[ResearchTreeManager] Could not find ResearchTreeManager in the active scene. Please open MainMenuScene first.");
            return;
        }

        manager.EditorRebuildNodeTreeInSceneAndSavePrefab();
    }

    // ปิดการทำงานอัตโนมัติใน Edit Mode เพื่อป้องกันการสร้าง GameObjects/Lines ค้างไว้ใน Scene
    // หากต้องการ Generate ให้กดผ่านเมนู: Tools > Research Lab > Generate 3-Column Node Tree in Scene & Save Prefab
    /*
    [InitializeOnLoadMethod]
    private static void AutoGenerateInEditModeOnLoad()
    {
        EditorApplication.delayCall += () =>
        {
            if (Application.isPlaying) return;

            ResearchTreeManager manager = FindFirstObjectByType<ResearchTreeManager>(FindObjectsInactive.Include);
            if (manager == null) return;

            Transform panel = manager.ResolveResearchPanel();
            if (panel != null && panel.Find(TREE_ROOT_OBJECT_NAME) == null)
            {
                manager.EditorGenerateNodeTreeInSceneAndSavePrefab();
            }
        };
    }
    */
#endif

    #endregion
}
