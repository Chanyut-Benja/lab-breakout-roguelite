using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skill tree page: one character at a time (switch with < >), the node tree in the middle, details + Unlock on the right.
/// Built in code the first time the Research panel opens (MainMenuManager.OpenResearchLab calls Show).
/// It reads and writes through ResearchTreeManager, so nodes, costs, RP and saves are the shared ones.
/// </summary>
public class SkillTreeScreen : MonoBehaviour
{
    private class NodeView
    {
        public SkillTreeNodeDefinition def;
        public RectTransform rt;
        public Image image;
        public Outline outline;
        public Text label;
    }

    private static readonly string[] CharacterNames = { "N-01 Vanguard", "N-02 Psionic", "N-03 Cyber-Swift" };
    private static readonly Color[] Themes =
    {
        new Color(1.00f, 0.24f, 0.28f, 1f),   // Vanguard red
        new Color(0.18f, 0.92f, 0.42f, 1f),   // Psionic green
        new Color(0.25f, 0.56f, 1.00f, 1f)    // Cyber-Swift blue
    };

    private static SkillTreeScreen instance;
    private static Sprite circleSprite;

    private RectTransform area;
    private Text rpText, charText, statsText, infoTitle, infoDesc, infoCost, buyLabel;
    private Button buyButton;
    private Image leftBg, infoBg, prevBg, nextBg, buyBg;
    private Outline leftOl, infoOl;
    private readonly List<Outline> sceneOutlines = new List<Outline>();
    private readonly List<Image> sceneBackImages = new List<Image>();
    private Text sceneTitle;
    private Font font;
    private ResearchTreeManager subscribedTo;

    private readonly List<NodeView> views = new List<NodeView>();
    private readonly List<RectTransform> lines = new List<RectTransform>();
    private SubjectCharacterType builtChar = (SubjectCharacterType)(-1);
    private string selectedId = "";
    private Vector2 lastAreaSize;

    // ------------------------------------------------------------------ entry
    public static void Show(Transform researchPanel)
    {
        if (researchPanel == null) return;

        // hide the old three-column tree, its lines layer and the legacy cards
        foreach (Transform child in researchPanel)
        {
            if (child == null || instance != null && child == instance.transform) continue;
            string n = child.name.ToLowerInvariant();
            if (child.name == ResearchTreeManager.TREE_ROOT_OBJECT_NAME || n.Contains("lineslayer") || n.Contains("nodetree"))
                child.gameObject.SetActive(false);
        }

        if (instance == null)
        {
            // Uses the prefab Resources/UI/SkillTreeScreen (made by Tools > Build UI Panels) if it exists, otherwise builds in code.
            GameObject prefab = Resources.Load<GameObject>("UI/SkillTreeScreen");
            GameObject go;
            if (prefab != null)
            {
                go = Instantiate(prefab, researchPanel, false);
                go.name = "SkillTreeScreen";
                instance = go.GetComponent<SkillTreeScreen>();
                if (instance == null) { instance = go.AddComponent<SkillTreeScreen>(); prefab = null; }
            }
            else
            {
                go = new GameObject("SkillTreeScreen", typeof(RectTransform));
                go.transform.SetParent(researchPanel, false);
                instance = go.AddComponent<SkillTreeScreen>();
            }
            instance.font = InGameHUDController.GetDefaultFont();
            if (instance.font == null) instance.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (prefab == null) instance.Build();
            instance.Bind();
        }
        else if (instance.transform.parent != researchPanel)
        {
            instance.transform.SetParent(researchPanel, false);
        }

        SkinLibrary.AddBackground(researchPanel, 0.93f);   // cover art, dimmed so the nodes stay readable (puts itself at the back)
        instance.transform.SetAsFirstSibling();
        if (researchPanel.Find("CoverBackground") != null) instance.transform.SetSiblingIndex(Mathf.Min(2, researchPanel.childCount - 1));   // background, dim, then the tree; title and Back stay on top
        instance.StyleSceneChrome(researchPanel);
        instance.gameObject.SetActive(true);
        instance.Hook();
        instance.Refresh();
    }

#if UNITY_EDITOR
    /// <summary>Editor only: builds the page frame so it can be saved as a prefab (the nodes are generated from game data at runtime).</summary>
    public static GameObject CreateForEditor()
    {
        GameObject go = new GameObject("SkillTreeScreen", typeof(RectTransform));
        SkillTreeScreen s = go.AddComponent<SkillTreeScreen>();
        s.font = InGameHUDController.GetDefaultFont();
        if (s.font == null) s.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        s.Build();
        return go;
    }
#endif

    // Finds the parts of the page by name and hooks up the buttons.
    private void Bind()
    {
        Transform left = transform.Find("Left");
        leftBg = left.GetComponent<Image>();
        leftOl = left.GetComponent<Outline>();
        Transform leftIn = left.Find("In");
        rpText = leftIn.Find("RP").GetComponent<Text>();
        charText = leftIn.Find("Char").GetComponent<Text>();
        statsText = leftIn.Find("Stats").GetComponent<Text>();
        Button prev = leftIn.Find("Prev").GetComponent<Button>();
        Button next = leftIn.Find("Next").GetComponent<Button>();
        prevBg = prev.GetComponent<Image>();
        nextBg = next.GetComponent<Image>();
        prev.onClick.RemoveAllListeners(); prev.onClick.AddListener(() => Switch(-1));
        next.onClick.RemoveAllListeners(); next.onClick.AddListener(() => Switch(1));

        area = (RectTransform)transform.Find("Tree");

        Transform info = transform.Find("Info");
        infoBg = info.GetComponent<Image>();
        infoOl = info.GetComponent<Outline>();
        infoTitle = info.Find("Title").GetComponent<Text>();
        infoDesc = info.Find("Desc").GetComponent<Text>();
        infoCost = info.Find("Cost").GetComponent<Text>();
        buyButton = info.Find("Buy").GetComponent<Button>();
        buyLabel = buyButton.transform.Find("Label").GetComponent<Text>();
        buyBg = buyButton.GetComponent<Image>();
        buyButton.onClick.RemoveAllListeners();
        buyButton.onClick.AddListener(OnBuy);
    }

    private void Hook()
    {
        ResearchTreeManager m = ResearchTreeManager.Instance;
        if (m == null || m == subscribedTo) return;
        if (subscribedTo != null) subscribedTo.OnTreeUpdated -= Refresh;
        subscribedTo = m;
        subscribedTo.OnTreeUpdated += Refresh;
    }

    private void OnDestroy()
    {
        if (subscribedTo != null) subscribedTo.OnTreeUpdated -= Refresh;
        if (instance == this) instance = null;
    }

    private void Update()
    {
        if (area != null && area.rect.size != lastAreaSize && area.rect.size.x > 1f)
        {
            lastAreaSize = area.rect.size;
            Relayout();
        }
    }

    // ------------------------------------------------------------------ build helpers
    private static Sprite Circle()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(c - d + 1f)));
            }
        }
        tex.Apply();
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return circleSprite;
    }

    private RectTransform NewRect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    private Text MakeText(string name, Transform parent, Vector2 min, Vector2 max, int size, Color color, TextAnchor align)
    {
        RectTransform rt = NewRect(name, parent, min, max);
        Text t = rt.gameObject.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Truncate;
        return t;
    }

    private Button MakeButton(string name, Transform parent, Vector2 min, Vector2 max, string label, Color color, out Text text)
    {
        RectTransform rt = NewRect(name, parent, min, max);
        Image img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        text = MakeText("Label", rt, Vector2.zero, Vector2.one, 18, Color.white, TextAnchor.MiddleCenter);
        text.text = label;
        return b;
    }

    private void Build()
    {
        RectTransform self = (RectTransform)transform;
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;

        Color light = new Color(1f, 0.94f, 0.88f, 1f);   // warm white, matches the menu logo
        Color panel = new Color(0.13f, 0.11f, 0.17f, 0.94f);   // dark plum, same as the main menu buttons
        Color navBtn = new Color(0.30f, 0.16f, 0.12f, 1f);

        // ----- left: RP, character switcher, stats
        RectTransform left = NewRect("Left", self, new Vector2(0.01f, 0.10f), new Vector2(0.235f, 0.86f));
        leftBg = left.gameObject.AddComponent<Image>();
        leftBg.color = panel;
        leftOl = AddOrangeOutline(left.gameObject);
        RectTransform leftIn = NewRect("In", left, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f));
        rpText = MakeText("RP", leftIn, new Vector2(0f, 0.90f), new Vector2(1f, 1f), 28, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft);

        Text t;
        Button prev = MakeButton("Prev", leftIn, new Vector2(0f, 0.78f), new Vector2(0.14f, 0.88f), "<", navBtn, out t);
        prevBg = prev.GetComponent<Image>();
        charText = MakeText("Char", leftIn, new Vector2(0.16f, 0.78f), new Vector2(0.84f, 0.88f), 24, light, TextAnchor.MiddleCenter);
        charText.fontStyle = FontStyle.Bold;
        Button next = MakeButton("Next", leftIn, new Vector2(0.86f, 0.78f), new Vector2(1f, 0.88f), ">", navBtn, out t);
        nextBg = next.GetComponent<Image>();

        statsText = MakeText("Stats", leftIn, new Vector2(0f, 0f), new Vector2(1f, 0.74f), 22, light, TextAnchor.UpperLeft);

        // ----- middle: node area
        area = NewRect("Tree", self, new Vector2(0.235f, 0.02f), new Vector2(0.765f, 0.94f));

        // ----- right: details
        RectTransform info = NewRect("Info", self, new Vector2(0.765f, 0.16f), new Vector2(0.99f, 0.84f));
        infoBg = info.gameObject.AddComponent<Image>();
        infoBg.color = panel;
        infoOl = AddOrangeOutline(info.gameObject);
        infoTitle = MakeText("Title", info, new Vector2(0.05f, 0.84f), new Vector2(0.95f, 0.98f), 28, Color.white, TextAnchor.MiddleLeft);
        infoTitle.fontStyle = FontStyle.Bold;
        infoDesc = MakeText("Desc", info, new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.84f), 22, light, TextAnchor.UpperLeft);
        infoCost = MakeText("Cost", info, new Vector2(0.05f, 0.18f), new Vector2(0.95f, 0.32f), 22, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft);
        buyButton = MakeButton("Buy", info, new Vector2(0.15f, 0.04f), new Vector2(0.85f, 0.16f), "Unlock", new Color(0.62f, 0.27f, 0.10f), out buyLabel);
        buyBg = buyButton.GetComponent<Image>();
    }

    private static Outline AddOrangeOutline(GameObject go)
    {
        Outline o = go.GetComponent<Outline>();
        if (o == null) o = go.AddComponent<Outline>();
        o.effectColor = new Color(0.86f, 0.36f, 0.14f, 1f);
        o.effectDistance = new Vector2(2.5f, -2.5f);
        return o;
    }

    /// <summary>Restyle the scene's own "Research Lab" title and BACK button to match the menu theme.</summary>
    private void StyleSceneChrome(Transform researchPanel)
    {
        sceneOutlines.Clear();
        sceneBackImages.Clear();
        foreach (Text tx in researchPanel.GetComponentsInChildren<Text>(true))
        {
            if (tx == null || tx.transform.IsChildOf(transform)) continue;
            if (tx.text == null || tx.text.Trim().ToLowerInvariant() != "research lab") continue;
            tx.color = new Color(1f, 0.94f, 0.88f, 1f);
            tx.fontStyle = FontStyle.Bold;
            sceneTitle = tx;
            sceneOutlines.Add(AddOrangeOutline(tx.gameObject));
        }
        foreach (Button b in researchPanel.GetComponentsInChildren<Button>(true))
        {
            if (b == null || b.transform.IsChildOf(transform)) continue;
            if (b.name.ToLowerInvariant().IndexOf("back") < 0) continue;
            Image img = b.GetComponent<Image>();
            if (img != null) { img.color = new Color(0.13f, 0.11f, 0.17f, 0.94f); sceneBackImages.Add(img); }
            sceneOutlines.Add(AddOrangeOutline(b.gameObject));
        }
    }

    /// <summary>Tint the whole page (panels, borders, title, buttons) with the selected character's colour.</summary>
    private void ApplyTheme(Color th)
    {
        Color darkBase = new Color(0.07f, 0.06f, 0.09f, 1f);
        Color bg = Color.Lerp(darkBase, th, 0.17f); bg.a = 0.94f;
        Color btn = Color.Lerp(darkBase, th, 0.45f); btn.a = 1f;
        Color text = Color.Lerp(Color.white, th, 0.15f);

        if (leftBg != null) leftBg.color = bg;
        if (infoBg != null) infoBg.color = bg;
        if (leftOl != null) leftOl.effectColor = th;
        if (infoOl != null) infoOl.effectColor = th;
        if (prevBg != null) prevBg.color = btn;
        if (nextBg != null) nextBg.color = btn;
        if (buyBg != null) buyBg.color = Color.Lerp(darkBase, th, 0.7f);
        if (statsText != null) statsText.color = text;
        if (infoDesc != null) infoDesc.color = text;
        if (sceneTitle != null) sceneTitle.color = Color.Lerp(Color.white, th, 0.35f);
        for (int i = 0; i < sceneOutlines.Count; i++) if (sceneOutlines[i] != null) sceneOutlines[i].effectColor = th;
        for (int i = 0; i < sceneBackImages.Count; i++) if (sceneBackImages[i] != null) sceneBackImages[i].color = bg;
    }

    private void Switch(int dir)
    {
        ResearchTreeManager m = ResearchTreeManager.Instance;
        if (m == null) return;
        int next = ((int)m.SelectedTreeCharacter + 3 + dir) % 3;
        m.SelectCharacterTree((SubjectCharacterType)next);
    }

    // ------------------------------------------------------------------ nodes
    private void RebuildNodes(SubjectCharacterType character)
    {
        for (int i = 0; i < views.Count; i++) if (views[i].rt != null) Destroy(views[i].rt.gameObject);
        for (int i = 0; i < lines.Count; i++) if (lines[i] != null) Destroy(lines[i].gameObject);
        views.Clear();
        lines.Clear();

        List<SkillTreeNodeDefinition> nodes = ResearchTreeManager.Instance.GetNodesForCharacter(character);

        // connecting lines first (drawn under the nodes)
        for (int i = 0; i < nodes.Count; i++)
        {
            RectTransform line = NewRect("Line_" + nodes[i].nodeId, area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            Image li = line.gameObject.AddComponent<Image>();
            li.raycastTarget = false;
            lines.Add(line);
        }
        for (int i = 0; i < nodes.Count; i++) views.Add(MakeNodeView(nodes[i]));

        builtChar = character;
        lastAreaSize = Vector2.zero;
        Relayout();
    }

    private NodeView MakeNodeView(SkillTreeNodeDefinition def)
    {
        NodeView v = new NodeView { def = def };
        RectTransform rt = NewRect(def.nodeId, area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        v.rt = rt;
        v.image = rt.gameObject.AddComponent<Image>();
        v.image.sprite = Circle();
        v.outline = rt.gameObject.AddComponent<Outline>();
        v.outline.effectColor = new Color(1f, 1f, 1f, 0.95f);
        v.outline.effectDistance = new Vector2(3f, -3f);
        v.outline.enabled = false;

        Button b = rt.gameObject.AddComponent<Button>();
        b.targetGraphic = v.image;
        string id = def.nodeId;
        b.onClick.AddListener(() => { selectedId = id; Refresh(); });

        v.label = MakeText("Label", rt, Vector2.zero, Vector2.one, 12, Color.white, TextAnchor.MiddleCenter);
        v.label.horizontalOverflow = HorizontalWrapMode.Overflow;
        v.label.verticalOverflow = VerticalWrapMode.Overflow;
        v.label.text = ShortLabel(def);

        if (def.nodeType != SkillNodeTierType.Core)
        {
            RectTransform nr = NewRect("Name", rt, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f));
            nr.sizeDelta = new Vector2(190f, 44f);
            nr.pivot = new Vector2(0.5f, 1f);
            nr.anchoredPosition = new Vector2(0f, -2f);
            Text nt = nr.gameObject.AddComponent<Text>();
            nt.font = font;
            nt.fontSize = 15;
            nt.alignment = TextAnchor.UpperCenter;
            nt.color = Color.white;
            nt.raycastTarget = false;
            nt.horizontalOverflow = HorizontalWrapMode.Wrap;
            nt.verticalOverflow = VerticalWrapMode.Overflow;
            nt.text = def.title;
            Outline no = nr.gameObject.AddComponent<Outline>();
            no.effectColor = new Color(0f, 0f, 0f, 0.95f);
            no.effectDistance = new Vector2(1.5f, -1.5f);
        }
        return v;
    }

    private static string ShortLabel(SkillTreeNodeDefinition d)
    {
        if (d.nodeType == SkillNodeTierType.Core) return "CORE";
        string[] words = d.title.Split(' ');
        if (d.nodeType == SkillNodeTierType.Major)
        {
            string s = "";
            for (int i = 0; i < words.Length && i < 2; i++) if (words[i].Length > 0) s += char.ToUpperInvariant(words[i][0]);
            return s;
        }
        string w = words[0].Replace("+", "");
        return w.Length > 4 ? w.Substring(0, 4).ToUpperInvariant() : w.ToUpperInvariant();
    }

    private const float DesignW = 700f, DesignH = 830f, DesignCenterY = 125f;

    private Vector2 treeCenter;

    private Vector2 NodePos(SkillTreeNodeDefinition d, float s)
    {
        return (new Vector2(d.anchoredPosition.x, d.anchoredPosition.y) - treeCenter) * s;
    }

    private void Relayout()
    {
        if (area == null || views.Count == 0) return;
        Rect r = area.rect;

        // fit the whole tree (including the name labels under the bottom nodes) inside the area
        float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < views.Count; i++)
        {
            Vector2 p = views[i].def.anchoredPosition;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        float contentW = (maxX - minX) + 230f;          // node + side labels
        float contentH = (maxY - minY) + 78f + 52f;     // node + label under the lowest node
        treeCenter = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f - 6.5f);
        float s = Mathf.Min(r.width / contentW, r.height / contentH) * 0.96f;
        if (s < 0.1f) return;

        for (int i = 0; i < views.Count; i++)
        {
            NodeView v = views[i];
            float size = (v.def.nodeType == SkillNodeTierType.Core ? 84f : v.def.nodeType == SkillNodeTierType.Major ? 78f : 58f) * s;
            v.rt.sizeDelta = new Vector2(size, size);
            v.rt.anchoredPosition = NodePos(v.def, s);
            v.label.fontSize = Mathf.Max(9, Mathf.RoundToInt(size * 0.28f));
        }

        List<SkillTreeNodeDefinition> nodes = ResearchTreeManager.Instance.GetNodesForCharacter(builtChar);
        float thick = Mathf.Max(3f, 6f * s);
        for (int i = 0; i < nodes.Count && i < lines.Count; i++)
        {
            SkillTreeNodeDefinition parent = nodes.Find(n => n.nodeId == nodes[i].prerequisiteNodeId);
            if (parent == null) { lines[i].gameObject.SetActive(false); continue; }
            Vector2 a = NodePos(parent, s), b = NodePos(nodes[i], s);
            Vector2 diff = b - a;
            lines[i].gameObject.SetActive(true);
            lines[i].anchoredPosition = (a + b) * 0.5f;
            lines[i].sizeDelta = new Vector2(diff.magnitude, thick);
            lines[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(diff.y, diff.x) * Mathf.Rad2Deg);
        }
    }

    // ------------------------------------------------------------------ refresh
    public void Refresh()
    {
        ResearchTreeManager m = ResearchTreeManager.Instance;
        if (area == null || m == null) return;

        SubjectCharacterType character = m.SelectedTreeCharacter;
        if (builtChar != character)
        {
            selectedId = "";
            RebuildNodes(character);
        }

        Color theme = Themes[Mathf.Clamp((int)character, 0, 2)];
        int rp = m.GetCurrentResearchPoints();
        rpText.text = "Research Points: " + rp;
        charText.text = CharacterNames[Mathf.Clamp((int)character, 0, 2)];
        charText.color = theme;
        ApplyTheme(theme);

        for (int i = 0; i < views.Count; i++)
        {
            NodeView v = views[i];
            SkillTreeNodeDefinition d = v.def;
            bool owned = m.IsNodeUnlocked(d.nodeId);
            bool parentOk = m.IsNodeUnlocked(d.prerequisiteNodeId);
            Color c;
            if (d.nodeType == SkillNodeTierType.Core) c = theme * 0.8f;
            else if (owned) c = d.nodeType == SkillNodeTierType.Major ? new Color(1f, 0.78f, 0.2f) : theme;
            else if (!parentOk) c = new Color(0.25f, 0.28f, 0.32f);
            else if (rp >= d.costRP) c = Color.Lerp(theme, Color.white, 0.55f);
            else c = new Color(0.45f, 0.5f, 0.55f);
            c.a = 1f;
            v.image.color = c;
            v.label.color = owned && d.nodeType == SkillNodeTierType.Major ? new Color(0.2f, 0.1f, 0f) : Color.white;
            v.outline.enabled = d.nodeId == selectedId;
        }

        List<SkillTreeNodeDefinition> nodes = m.GetNodesForCharacter(character);
        for (int i = 0; i < nodes.Count && i < lines.Count; i++)
        {
            Image li = lines[i].GetComponent<Image>();
            li.color = m.IsNodeUnlocked(nodes[i].nodeId) ? theme : new Color(0.35f, 0.42f, 0.5f, 1f);
        }

        statsText.text = BuildStats(character, nodes, m);
        ShowInfo(m, nodes, rp);
    }

    private static void Add(System.Text.StringBuilder sb, string label, float value, bool percent)
    {
        if (Mathf.Abs(value) < 0.0001f) return;
        sb.AppendLine("- " + label + " " + (percent ? "+" + Mathf.RoundToInt(value * 100f) + "%" : "+" + value.ToString("0.#")));
    }

    private static string BuildStats(SubjectCharacterType character, List<SkillTreeNodeDefinition> nodes, ResearchTreeManager m)
    {
        CharacterMetaBonuses b = ResearchTreeManager.CalculateCharacterMetaBonuses(character);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("Bonuses");
        int before = sb.Length;
        Add(sb, "Armor", b.flatArmor, false);
        Add(sb, "Max HP", b.maxHpPercentBonus, true);
        Add(sb, "HP regen /s", b.hpRegenPerSecond, false);
        Add(sb, "Knockback", b.knockbackMultiplierBonus, true);
        Add(sb, "Move speed", b.moveSpeedPercentBonus, true);
        Add(sb, "Damage", b.damagePercentBonus, true);
        Add(sb, "Attack speed", b.attackSpeedCooldownBonus, true);
        Add(sb, "Crit chance", b.critChanceBonus, true);
        Add(sb, "Crit damage", b.critDamageBonus, true);
        Add(sb, "DoT damage", b.dotDamageMultiplierBonus, true);
        Add(sb, "Stun time (s)", b.stunDurationBonusSeconds, false);
        Add(sb, "Area size", b.aoeSizePercentBonus, true);
        Add(sb, "Dash cooldown", -b.dashCooldownReductionPercent, true);
        Add(sb, "Dash distance", b.dashDistancePercentBonus, true);
        Add(sb, "I-frame (s)", b.iFrameDurationBonusSeconds, false);
        if (b.bonusDashCharges > 0) sb.AppendLine("- Extra dash charge +" + b.bonusDashCharges);
        if (sb.Length == before) sb.AppendLine("(none yet)");

        sb.AppendLine();
        sb.AppendLine("Major nodes");
        bool any = false;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].nodeType != SkillNodeTierType.Major || !m.IsNodeUnlocked(nodes[i].nodeId)) continue;
            sb.AppendLine("- " + nodes[i].title);
            any = true;
        }
        if (!any) sb.AppendLine("(none yet)");
        return sb.ToString();
    }

    private void ShowInfo(ResearchTreeManager m, List<SkillTreeNodeDefinition> nodes, int rp)
    {
        SkillTreeNodeDefinition d = string.IsNullOrEmpty(selectedId) ? null : nodes.Find(n => n.nodeId == selectedId);
        if (d == null)
        {
            infoTitle.text = "Skill Tree";
            infoDesc.text = "Pick a node to see what it does. Unlock the nodes next to the Core first, then work outwards to the Major nodes.";
            infoCost.text = "";
            buyButton.gameObject.SetActive(false);
            return;
        }
        if (d.nodeType == SkillNodeTierType.Core)
        {
            infoTitle.text = "Core";
            infoDesc.text = d.description;
            infoCost.text = "";
            buyButton.gameObject.SetActive(false);
            return;
        }

        buyButton.gameObject.SetActive(true);
        infoTitle.text = d.title + (d.nodeType == SkillNodeTierType.Major ? "  (Major)" : "");
        infoDesc.text = d.description;

        bool owned = m.IsNodeUnlocked(d.nodeId);
        bool parentOk = m.IsNodeUnlocked(d.prerequisiteNodeId);
        bool afford = rp >= d.costRP;

        if (owned) { infoCost.text = "UNLOCKED"; buyLabel.text = "Owned"; }
        else if (!parentOk)
        {
            SkillTreeNodeDefinition p = nodes.Find(n => n.nodeId == d.prerequisiteNodeId);
            infoCost.text = "Locked - unlock \"" + (p != null ? p.title : d.prerequisiteNodeId) + "\" first";
            buyLabel.text = "Locked";
        }
        else
        {
            infoCost.text = "Cost: " + d.costRP + " RP" + (afford ? "" : "  (not enough RP)");
            buyLabel.text = "Unlock";
        }
        buyButton.interactable = !owned && parentOk && afford;
    }

    private void OnBuy()
    {
        ResearchTreeManager m = ResearchTreeManager.Instance;
        if (m == null || string.IsNullOrEmpty(selectedId)) return;
        m.TryUnlockNodeById(selectedId);   // saves, then raises OnTreeUpdated -> Refresh
        Refresh();
    }
}
