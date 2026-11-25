using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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

    // ค่าคงที่สำหรับคำนวณ RP หลังจบรันตามเอกสารบาลานซ์หัวข้อ 9.4 และ 1.3
    public const int RP_PER_NORMAL_ROOM = 25;
    public const int RP_PER_BOSS_KILL = 75;
    public const int RP_BONUS_FLOOR_1 = 400;
    public const int RP_BONUS_FLOOR_2 = 1000;
    public const int RP_MINIMUM_GUARANTEE = 50;

    // ลำดับขั้นราคาอัปเกรดตามเอกสารบาลานซ์: 150 -> 200 -> 250 -> 500 -> 800 -> 1,500 RP
    public static readonly int[] UpgradeCostTiers = { 150, 200, 250, 500, 800, 1500 };

    [Header("Skill Tree Configuration")]
    [SerializeField] private SubjectCharacterType selectedTreeCharacter = SubjectCharacterType.Vanguard_N01;
    [SerializeField] private int maxLevel = 6;
    [SerializeField] private RectTransform researchPanelOverride;

    public event Action OnTreeUpdated;

    private readonly List<SkillTreeNodeDefinition> allNodeDefinitions = new List<SkillTreeNodeDefinition>();

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

        // The minimum guarantee only applies once the player has actually cleared something,
        // so ending a run immediately cannot be used to farm free RP.
        bool achievedSomething = normalRoomsCleared > 0 || bossesDefeated > 0;
        return achievedSomething ? Mathf.Max(RP_MINIMUM_GUARANTEE, multipliedRP) : Mathf.Max(0, multipliedRP);
    }

    private void BuildSkillTreeDefinitions()
    {
        allNodeDefinitions.Clear();
        ReplaceWithOldTree();   // the node data lives in OldTreeData.cs
    }

    /// <summary>Swaps the friend's 9-node trees for our original 12-node, 4-direction trees (same RP prices).</summary>
    private void ReplaceWithOldTree()
    {
        allNodeDefinitions.Clear();
        const float cell = 130f;
        for (int c = 0; c < 3; c++)
        {
            SubjectCharacterType ch = (SubjectCharacterType)c;
            string prefix = OldTreeData.CharPrefix(c);
            string coreId = prefix + "Core";
            string[] coreDesc =
            {
                "Start of the Vanguard tree (Kinetic Absorption).",
                "Start of the Psionic tree (Mind Shock).",
                "Start of the Cyber-Swift tree (Dash)."
            };
            allNodeDefinitions.Add(new SkillTreeNodeDefinition(coreId, ch, SkillNodeTierType.Core,
                "CORE: N-0" + (c + 1), coreDesc[c], 0, "", Vector2.zero, 0));

            List<OldTreeNode> nodes = OldTreeData.NodesFor(c);
            for (int i = 0; i < nodes.Count; i++)
            {
                OldTreeNode n = nodes[i];
                string parent = n.parent == "core" ? coreId : prefix + n.parent;
                allNodeDefinitions.Add(new SkillTreeNodeDefinition(prefix + n.id, ch,
                    n.major ? SkillNodeTierType.Major : SkillNodeTierType.Minor,
                    n.name, n.description, n.cost, parent, new Vector2(n.gx * cell, n.gy * cell), 0));
            }
        }
    }

    // Adds the bonuses of the old-tree nodes the player owns into 'b'.
    private static void ApplyOldTreeBonuses(GameData data, SubjectCharacterType character, ref CharacterMetaBonuses b)
    {
        int c = (int)character;
        List<OldTreeNode> nodes = OldTreeData.NodesFor(c);
        string prefix = OldTreeData.CharPrefix(c);
        for (int i = 0; i < nodes.Count; i++)
        {
            OldTreeNode n = nodes[i];
            if (!data.unlockedSkillNodes.Contains(prefix + n.id)) continue;
            switch (n.effect)
            {
                case OldEffect.Armor: b.flatArmor += n.value; break;
                case OldEffect.HpPercent: b.maxHpPercentBonus += n.value; break;
                case OldEffect.MovePercent: b.moveSpeedPercentBonus += n.value; break;
                case OldEffect.DamagePercent: b.damagePercentBonus += n.value; break;
                case OldEffect.AttackSpeedPercent: b.attackSpeedCooldownBonus += n.value; break;
                case OldEffect.CooldownReduction: b.attackSpeedCooldownBonus += n.value; break;
                case OldEffect.CritRate: b.critChanceBonus += n.value; break;
                case OldEffect.CritDamage: b.critDamageBonus += n.value; break;
                case OldEffect.EffectDamagePercent: b.dotDamageMultiplierBonus += n.value; break;
                case OldEffect.PoisonPercent: b.dotDamageMultiplierBonus += n.value; break;
                case OldEffect.DashDistance: b.dashDistancePercentBonus += n.value; break;
                case OldEffect.DashRecharge: b.dashCooldownReductionPercent += n.value; break;
                case OldEffect.IFrame: b.iFrameDurationBonusSeconds += n.value; break;

                case OldEffect.BlastNova: b.hasBlastNovaMajor = true; break;
                case OldEffect.AbsoluteArmor: b.hasAbsoluteArmorMajor = true; break;
                case OldEffect.KineticReflect: b.hasKineticReflectMajor = true; b.reflectArmorRatio = 1.5f; break;
                case OldEffect.JuggernautMomentum: b.hasJuggernautMomentumMajor = true; b.damagePercentBonus += 0.20f; break;
                case OldEffect.TitanRevitalization: b.hasTitanRevitalizationMajor = true; break;
                case OldEffect.MindCollapse: b.hasMindCollapseMajor = true; break;
                case OldEffect.PsionicBarrier: b.hasPsionicBarrierMajor = true; break;
                case OldEffect.OverchargeNova: b.hasOverchargeNovaMajor = true; break;
                case OldEffect.VenomCatalyst: b.hasVenomCatalystMajor = true; break;
                case OldEffect.TemporalRift: b.hasTemporalRiftMajor = true; break;
                case OldEffect.PhantomClone: b.hasPhantomCloneMajor = true; break;
                case OldEffect.BulletTime: b.hasBulletTimeMajor = true; break;
                case OldEffect.OverclockOverdrive: b.hasOverclockOverdriveMajor = true; break;
                case OldEffect.PhaseShifter: b.hasPhaseShifterMajor = true; b.bonusDashCharges = 1; break;
                case OldEffect.LightningReflexes: b.hasLightningReflexesMajor = true; break;
            }
        }
        b.dashCooldownReductionPercent = Mathf.Clamp(b.dashCooldownReductionPercent, 0f, 0.45f);
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
            return false;
        }

        if (!string.IsNullOrEmpty(def.prerequisiteNodeId) && !data.unlockedSkillNodes.Contains(def.prerequisiteNodeId))
        {
            SkillTreeNodeDefinition prereq = allNodeDefinitions.Find(n => n.nodeId == def.prerequisiteNodeId);
            string prereqTitle = prereq != null ? prereq.title : def.prerequisiteNodeId;
            return false;
        }

        if (data.researchPoints < def.costRP)
        {
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

        // Our original 12-node trees replace the friend's formulas (the old branch levels are no longer used).
        bonuses.flatMaxHpBonus = data.hpUpgradeLevel * 10f;
        ApplyOldTreeBonuses(data, character, ref bonuses);
        return bonuses;
    }

    #region Research panel helpers

    private Transform ResolveResearchPanel()
    {
        if (researchPanelOverride != null) return researchPanelOverride;

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
        backRt.anchorMin = new Vector2(1f, 0f);
        backRt.anchorMax = new Vector2(1f, 0f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.anchoredPosition = new Vector2(-120f, 60f); // วางมุมซ้ายล่าง (120, 60)
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

    /// <summary>When true the Research panel shows SkillTreeScreen.</summary>
    public static bool UseCustomScreen = true;

    // Kept for old callers: the tree is drawn by SkillTreeScreen now.
    public void EnsureNodeTreeVisualUI(bool forceRebuild = false) { }
    public void RefreshAllNodeTreeVisuals() { OnTreeUpdated?.Invoke(); }

    #endregion
}
