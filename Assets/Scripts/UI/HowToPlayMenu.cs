using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// How to Play screen (built entirely in code, no scene setup needed).
/// Open with HowToPlayMenu.Open(). Works while the game is paused (no time-based animation).
/// </summary>
public class HowToPlayMenu : MonoBehaviour
{
    static HowToPlayMenu instance;

    static readonly Color Panel = new Color(0.10f, 0.08f, 0.13f, 0.97f);
    static readonly Color Orange = new Color(0.86f, 0.36f, 0.14f, 1f);
    const string OrangeHex = "#DB5C24";

    static readonly string[] PageTitles = { "CONTROLS & GOAL", "ROOMS & UPGRADES" };

    static readonly string[] PageBodies =
    {
        "<color=" + OrangeHex + "><b>CONTROLS</b></color>\n" +
        "WASD / Arrow Keys  -  Move\n" +
        "Space  -  Dash (uses a dash charge, you are safe while dashing)\n" +
        "Esc  -  Pause menu\n" +
        "Weapons fire automatically at the nearest enemy.\n\n" +
        "<color=" + OrangeHex + "><b>GOAL</b></color>\n" +
        "Escape the lab: survive 18 rooms. Every 3rd room (3, 6, 9, 12, 15, 18) is a boss fight.\n\n" +
        "<color=" + OrangeHex + "><b>HOW A ROOM WORKS</b></color>\n" +
        "1. Defeat the enemies in the room. Enemies drop EXP crystals - walk over them to collect.\n" +
        "2. When the room is clear, the doors open. Each door shows its reward.\n" +
        "3. Walk into the glowing area in front of a door to enter the next room.\n\n" +
        "<color=" + OrangeHex + "><b>LEVELING UP</b></color>\n" +
        "Collect EXP to level up and pick an upgrade: a new weapon, or a weapon level (up to Lv.5).\n" +
        "Weapons at Lv.5 can evolve into a much stronger form when you hold the right Boon.",

        "<color=" + OrangeHex + "><b>DOOR REWARDS</b></color>\n" +
        "Bio-Gene Chamber  -  Choose a random Boon (passive upgrade)\n" +
        "Gene Enhancer  -  Instantly upgrades one of your Boons to Lv.2\n" +
        "Medical Pod  -  Heals 30% of your max HP\n" +
        "Credit Vault  -  A big pile of credits\n" +
        "Black Market  -  Spend credits on items and buffs\n" +
        "Apex Boss Chamber  -  A boss fight\n\n" +
        "<color=" + OrangeHex + "><b>BOSSES</b></color>\n" +
        "Bosses have 3 phases: phase 2 at 66% HP and phase 3 (Enrage) at 33% HP.\n" +
        "If a fight lasts longer than 3 minutes, the boss Enrages automatically. Kill it fast!\n" +
        "Watch the red telegraphs on the floor and dash out of them.\n\n" +
        "<color=" + OrangeHex + "><b>BETWEEN RUNS</b></color>\n" +
        "Research Points earned in a run are spent in the SKILL TREE on the main menu for permanent upgrades.\n" +
        "Choosing END RUN in the pause menu still pays out Research Points for your progress."
    };

    Font font;
    GameObject root;
    Text titleText, bodyText, pageText;
    Button prevBtn, nextBtn;
    int page;

    public static void Open()
    {
        if (instance == null)
        {
            var go = new GameObject("HowToPlayMenu");
            instance = go.AddComponent<HowToPlayMenu>();
            instance.Setup();
        }
        instance.page = 0;
        instance.root.SetActive(true);
        instance.Refresh();
        instance.StartCoroutine(instance.Reveal());
    }

    static int closedFrame = -1;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    public static void Close()
    {
        if (instance != null && instance.root != null) instance.root.SetActive(false);
        closedFrame = Time.frameCount;
    }

    // Hide the panel until the pixel theme has been applied, so the old look never flashes.
    System.Collections.IEnumerator Reveal()
    {
        CanvasGroup cg = root.GetComponent<CanvasGroup>();
        if (cg == null) cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            PixelPanelAuto.ApplyNow();
            PixelFontAuto.ApplyNow();
            PixelButtonAutoSkin.ApplyNow();
        }
        cg.alpha = 1f;
    }

    // Uses the prefab Resources/UI/HowToPlayPanel (made by Tools > Build UI Panels) if it exists, otherwise builds in code.
    void Setup()
    {
        font = InGameHUDController.GetDefaultFont();
        GameObject prefab = Resources.Load<GameObject>("UI/HowToPlayPanel");
        if (prefab != null)
        {
            root = Instantiate(prefab, transform);
            root.name = "HowToPlayCanvas";
            DontDestroyOnLoad(gameObject);
        }
        else Build();
        Bind();
    }

#if UNITY_EDITOR
    /// <summary>Editor only: builds the panel hierarchy so it can be saved as a prefab.</summary>
    public static GameObject CreateForEditor()
    {
        var go = new GameObject("HowToPlayMenu");
        var m = go.AddComponent<HowToPlayMenu>();
        m.Build();
        m.Bind();
        m.Refresh();
        return go;
    }
#endif

    // Finds the parts of the panel by name and hooks up the buttons.
    void Bind()
    {
        Transform box = root.transform.Find("Box");
        titleText = box.Find("Title").GetComponent<Text>();
        bodyText = box.Find("Body").GetComponent<Text>();
        pageText = box.Find("Page").GetComponent<Text>();
        prevBtn = BindButton(box, "PREVButton", () => Step(-1));
        nextBtn = BindButton(box, "NEXTButton", () => Step(1));
        BindButton(box, "BACKButton", Close);
    }

    static Button BindButton(Transform parent, string name, UnityEngine.Events.UnityAction onClick)
    {
        Button b = parent.Find(name).GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(onClick);
        return b;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Step(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Step(-1);
    }

    void Step(int d)
    {
        page = Mathf.Clamp(page + d, 0, PageBodies.Length - 1);
        Refresh();
    }

    void Refresh()
    {
        titleText.text = "HOW TO PLAY  -  " + PageTitles[page];
        bodyText.text = PageBodies[page];
        pageText.text = (page + 1) + " / " + PageBodies.Length;
        prevBtn.interactable = page > 0;
        nextBtn.interactable = page < PageBodies.Length - 1;
    }

    void Build()
    {
        font = InGameHUDController.GetDefaultFont();

        var canvasGo = new GameObject("HowToPlayCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        root = canvasGo;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.8f));
        Stretch(dim.rectTransform);

        var box = NewImage("Box", canvasGo.transform, Panel);
        var brt = box.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(1300f, 900f);
        var ol = box.gameObject.AddComponent<Outline>();
        ol.effectColor = Orange;
        ol.effectDistance = new Vector2(3f, -3f);

        NewText("Title", box.transform, "", 44, FontStyle.Bold, Color.white,
            new Vector2(0f, 385f), new Vector2(1200f, 80f));

        bodyText = NewText("Body", box.transform, "", 26, FontStyle.Normal, Color.white,
            new Vector2(0f, 20f), new Vector2(1180f, 640f));
        bodyText.alignment = TextAnchor.UpperLeft;
        bodyText.supportRichText = true;
        bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        bodyText.verticalOverflow = VerticalWrapMode.Overflow;
        bodyText.lineSpacing = 1.1f;

        MakeButton(box.transform, "PREV", new Vector2(-440f, -385f), new Vector2(260f, 70f), null);
        MakeButton(box.transform, "NEXT", new Vector2(-150f, -385f), new Vector2(260f, 70f), null);
        MakeButton(box.transform, "BACK", new Vector2(440f, -385f), new Vector2(360f, 70f), null);
        NewText("Page", box.transform, "", 26, FontStyle.Bold, Color.white,
            new Vector2(120f, -385f), new Vector2(160f, 50f));
    }

    Button MakeButton(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var img = NewImage(label + "Button", parent, new Color(0.13f, 0.11f, 0.17f, 0.94f));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var o = img.gameObject.AddComponent<Outline>();
        o.effectColor = Orange;
        o.effectDistance = new Vector2(2.5f, -2.5f);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.highlightedColor = new Color(1f, 0.85f, 0.75f, 1f);
        c.pressedColor = new Color(0.7f, 0.65f, 0.65f, 1f);
        c.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        btn.colors = c;
        if (onClick != null) btn.onClick.AddListener(onClick);
        var t = NewText("Text", img.transform, label, 28, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        return btn;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Image NewImage(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    Text NewText(string name, Transform parent, string text, int size, FontStyle style, Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }
}
