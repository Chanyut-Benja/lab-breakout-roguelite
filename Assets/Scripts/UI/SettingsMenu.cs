using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ค่าตั้งค่าเสียง/จอ (บันทึกลง PlayerPrefs และใช้ทันทีที่เปิดเกม)
/// Master / Music / SFX  +  Resolution / Window Mode / FPS limit
/// </summary>
public static class LabSettings
{
    public const string KeyMaster = "MasterVolume";
    public const string KeyMusic = "BGMVolume";   // ใช้คีย์เดียวกับ AudioManager
    public const string KeySfx = "SFXVolume";     // ใช้คีย์เดียวกับ AudioManager
    const string KeyResW = "Display_ResW";
    const string KeyResH = "Display_ResH";
    const string KeyMode = "Display_Mode";
    const string KeyFps = "Display_FPS";

    public static readonly string[] ModeNames = { "FULLSCREEN", "BORDERLESS", "WINDOWED" };
    public static readonly int[] FpsOptions = { 30, 60, 90, 120, 144, 0 }; // 0 = ไม่จำกัด

    public static float Master => PlayerPrefs.GetFloat(KeyMaster, 1f);
    public static float Music => PlayerPrefs.GetFloat(KeyMusic, 0.75f);
    public static float Sfx => PlayerPrefs.GetFloat(KeySfx, 0.75f);
    public static int ModeIndex => Mathf.Clamp(PlayerPrefs.GetInt(KeyMode, 1), 0, ModeNames.Length - 1);
    public static int Fps => PlayerPrefs.GetInt(KeyFps, 60);

    public static Vector2Int SavedResolution
    {
        get
        {
            int w = PlayerPrefs.GetInt(KeyResW, 0);
            int h = PlayerPrefs.GetInt(KeyResH, 0);
            if (w <= 0 || h <= 0) return new Vector2Int(Screen.width, Screen.height);
            return new Vector2Int(w, h);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ApplyOnStart()
    {
        ApplyAudio();
        ApplyFps(Fps);
        // ถ้าไม่เคยตั้งค่าจอ ปล่อยตามค่าเริ่มต้นของ Unity
        if (PlayerPrefs.HasKey(KeyResW)) ApplyDisplay(SavedResolution, ModeIndex);
    }

    public static void ApplyAudio()
    {
        AudioListener.volume = Master;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetBGMVolume(Music);
            AudioManager.Instance.SetSFXVolume(Sfx);
        }
    }

    public static void SetMaster(float v)
    {
        PlayerPrefs.SetFloat(KeyMaster, v);
        AudioListener.volume = v;
        PlayerPrefs.Save();
    }

    public static void SetMusic(float v)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetBGMVolume(v);
        else { PlayerPrefs.SetFloat(KeyMusic, v); PlayerPrefs.Save(); }
    }

    public static void SetSfx(float v)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.SetSFXVolume(v);
        else { PlayerPrefs.SetFloat(KeySfx, v); PlayerPrefs.Save(); }
    }

    public static void ApplyFps(int fps)
    {
        PlayerPrefs.SetInt(KeyFps, fps);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = fps <= 0 ? -1 : fps;
        PlayerPrefs.Save();
    }

    public static void ApplyDisplay(Vector2Int res, int modeIndex)
    {
        PlayerPrefs.SetInt(KeyResW, res.x);
        PlayerPrefs.SetInt(KeyResH, res.y);
        PlayerPrefs.SetInt(KeyMode, modeIndex);
        PlayerPrefs.Save();

        FullScreenMode mode = modeIndex == 0 ? FullScreenMode.ExclusiveFullScreen
                            : modeIndex == 1 ? FullScreenMode.FullScreenWindow
                                             : FullScreenMode.Windowed;
        Screen.SetResolution(res.x, res.y, mode);
    }

    /// <summary>รายการความละเอียดที่จอรองรับ (ไม่ซ้ำ, ตั้งแต่ 1280x720 ขึ้นไป)</summary>
    public static List<Vector2Int> GetResolutions()
    {
        var list = new List<Vector2Int>();
        foreach (UnityEngine.Resolution r in Screen.resolutions)
        {
            if (r.width < 1280 || r.height < 720) continue;
            var v = new Vector2Int(r.width, r.height);
            if (!list.Contains(v)) list.Add(v);
        }
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        list.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
        return list;
    }
}

/// <summary>
/// หน้าจอ Settings (สร้างด้วยโค้ดทั้งหมด ไม่ต้องตั้งค่าใน Scene)
/// เรียกด้วย SettingsMenu.Open()
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    static SettingsMenu instance;

    static readonly Color Panel = new Color(0.10f, 0.08f, 0.13f, 0.97f);
    static readonly Color Orange = new Color(0.86f, 0.36f, 0.14f, 1f);
    static readonly Color RowBg = new Color(0.16f, 0.13f, 0.20f, 1f);
    static readonly Color TrackBg = new Color(0.05f, 0.04f, 0.08f, 1f);

    Font font;
    GameObject root;
    List<Vector2Int> resolutions;
    int resIndex, modeIndex, fpsIndex;
    Text resLabel, modeLabel, fpsLabel;

    public static void Open()
    {
        if (instance == null)
        {
            var go = new GameObject("SettingsMenu");
            instance = go.AddComponent<SettingsMenu>();
            instance.Setup();
        }
        instance.root.SetActive(true);
        instance.Refresh();
        instance.StartCoroutine(instance.Reveal());
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

    // Uses the prefab Resources/UI/SettingsPanel (made by Tools > Build UI Panels) if it exists, otherwise builds in code.
    void Setup()
    {
        font = InGameHUDController.GetDefaultFont();
        GameObject prefab = Resources.Load<GameObject>("UI/SettingsPanel");
        if (prefab != null)
        {
            root = Instantiate(prefab, transform);
            root.name = "SettingsCanvas";
            DontDestroyOnLoad(gameObject);
        }
        else Build();
        Bind();
    }

#if UNITY_EDITOR
    /// <summary>Editor only: builds the panel hierarchy so it can be saved as a prefab.</summary>
    public static GameObject CreateForEditor()
    {
        var go = new GameObject("SettingsMenu");
        var m = go.AddComponent<SettingsMenu>();
        m.Build();
        m.Bind();
        m.Refresh();
        return go;
    }
#endif

    // Finds the rows by name and hooks up sliders / arrows / back button.
    void Bind()
    {
        Transform box = root.transform.Find("Box");
        BindSlider(box, "MASTER", LabSettings.Master, LabSettings.SetMaster);
        BindSlider(box, "MUSIC", LabSettings.Music, LabSettings.SetMusic);
        BindSlider(box, "SFX", LabSettings.Sfx, LabSettings.SetSfx);

        resolutions = LabSettings.GetResolutions();
        var cur = LabSettings.SavedResolution;
        resIndex = resolutions.IndexOf(cur);
        if (resIndex < 0) resIndex = resolutions.Count - 1;
        modeIndex = LabSettings.ModeIndex;
        fpsIndex = System.Array.IndexOf(LabSettings.FpsOptions, LabSettings.Fps);
        if (fpsIndex < 0) fpsIndex = 1;

        resLabel = BindSelector(box, "RESOLUTION", step =>
        {
            resIndex = (resIndex + step + resolutions.Count) % resolutions.Count;
            LabSettings.ApplyDisplay(resolutions[resIndex], modeIndex);
            Refresh();
        });
        modeLabel = BindSelector(box, "WINDOW MODE", step =>
        {
            int n = LabSettings.ModeNames.Length;
            modeIndex = (modeIndex + step + n) % n;
            LabSettings.ApplyDisplay(resolutions[resIndex], modeIndex);
            Refresh();
        });
        fpsLabel = BindSelector(box, "FPS LIMIT", step =>
        {
            int n = LabSettings.FpsOptions.Length;
            fpsIndex = (fpsIndex + step + n) % n;
            LabSettings.ApplyFps(LabSettings.FpsOptions[fpsIndex]);
            Refresh();
        });

        Button back = box.Find("BackButton").GetComponent<Button>();
        back.onClick.RemoveAllListeners();
        back.onClick.AddListener(Close);
    }

    static void BindSlider(Transform box, string label, float value, System.Action<float> onChange)
    {
        Transform row = box.Find(label + "Row");
        Slider slider = row.Find("Slider").GetComponent<Slider>();
        Text valueText = row.Find("Value").GetComponent<Text>();
        slider.onValueChanged.RemoveAllListeners();
        slider.SetValueWithoutNotify(value);
        valueText.text = Mathf.RoundToInt(value * 100f) + "%";
        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = Mathf.RoundToInt(v * 100f) + "%";
            onChange(v);
        });
    }

    static Text BindSelector(Transform box, string label, System.Action<int> onStep)
    {
        Transform row = box.Find(label + "Row");
        Button left = row.Find("Arrow<").GetComponent<Button>();
        Button right = row.Find("Arrow>").GetComponent<Button>();
        left.onClick.RemoveAllListeners();
        right.onClick.RemoveAllListeners();
        left.onClick.AddListener(() => onStep(-1));
        right.onClick.AddListener(() => onStep(1));
        return row.Find("Value").GetComponent<Text>();
    }

    static int closedFrame = -1;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;

    public static void Close()
    {
        if (instance != null && instance.root != null) instance.root.SetActive(false);
        closedFrame = Time.frameCount;
    }

    public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

    void Update()
    {
        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ---------------------------------------------------------------- build
    void Build()
    {
        font = InGameHUDController.GetDefaultFont();

        // overlay canvas แยกจากเมนู (ไม่ถูกโค้ดล้างเมนูแตะต้อง)
        var canvasGo = new GameObject("SettingsCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();
        root = canvasGo;
        if (Application.isPlaying) DontDestroyOnLoad(gameObject);

        // พื้นหลังมืดทั้งจอ
        var dim = NewImage("Dim", canvasGo.transform, new Color(0f, 0f, 0f, 0.8f));
        Stretch(dim.rectTransform);

        // กล่องหลัก
        var box = NewImage("Box", canvasGo.transform, Panel);
        var brt = box.rectTransform;
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.sizeDelta = new Vector2(1000f, 880f);
        var ol = box.gameObject.AddComponent<Outline>();
        ol.effectColor = Orange;
        ol.effectDistance = new Vector2(3f, -3f);

        NewText("Title", box.transform, "SETTINGS", 52, FontStyle.Bold, Color.white,
            new Vector2(0f, 385f), new Vector2(800f, 80f));

        float y = 290f;
        NewText("AudioHeader", box.transform, "AUDIO", 30, FontStyle.Bold, Orange, new Vector2(0f, y), new Vector2(800f, 44f));
        y -= 70f;
        AddSlider(box.transform, "MASTER", y); y -= 80f;
        AddSlider(box.transform, "MUSIC", y); y -= 80f;
        AddSlider(box.transform, "SFX", y); y -= 90f;

        NewText("DisplayHeader", box.transform, "DISPLAY", 30, FontStyle.Bold, Orange, new Vector2(0f, y), new Vector2(800f, 44f));
        y -= 70f;
        AddSelector(box.transform, "RESOLUTION", y); y -= 80f;
        AddSelector(box.transform, "WINDOW MODE", y); y -= 80f;
        AddSelector(box.transform, "FPS LIMIT", y);

        // ปุ่ม BACK
        var back = NewImage("BackButton", box.transform, new Color(0.13f, 0.11f, 0.17f, 0.94f));
        var rt = back.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 40f);
        rt.sizeDelta = new Vector2(360f, 70f);
        var bo = back.gameObject.AddComponent<Outline>();
        bo.effectColor = Orange;
        bo.effectDistance = new Vector2(2.5f, -2.5f);
        var btn = back.gameObject.AddComponent<Button>();
        btn.targetGraphic = back;
        var cb = btn.colors;
        cb.highlightedColor = new Color(1f, 0.85f, 0.75f, 1f);
        cb.pressedColor = new Color(0.7f, 0.65f, 0.65f, 1f);
        btn.colors = cb;
        var bt = NewText("Text", back.transform, "BACK", 28, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
        Stretch(bt.rectTransform);
    }

    void Refresh()
    {
        if (resLabel == null) return;
        var r = resolutions[resIndex];
        resLabel.text = r.x + " x " + r.y;
        modeLabel.text = LabSettings.ModeNames[modeIndex];
        int f = LabSettings.FpsOptions[fpsIndex];
        fpsLabel.text = f <= 0 ? "UNLIMITED" : f + " FPS";
    }

    // ---------------------------------------------------------------- widgets
    void AddSlider(Transform parent, string label, float y)
    {
        var row = NewImage(label + "Row", parent, RowBg);
        PlaceRow(row.rectTransform, y);
        NewText("Label", row.transform, label, 26, FontStyle.Bold, Color.white,
            new Vector2(-340f, 0f), new Vector2(220f, 50f)).alignment = TextAnchor.MiddleLeft;

        var valueText = NewText("Value", row.transform, "", 26, FontStyle.Bold, Color.white,
            new Vector2(380f, 0f), new Vector2(110f, 50f));
        valueText.alignment = TextAnchor.MiddleRight;

        // slider
        var sGo = new GameObject("Slider", typeof(RectTransform));
        sGo.transform.SetParent(row.transform, false);
        var srt = (RectTransform)sGo.transform;
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(30f, 0f);
        srt.sizeDelta = new Vector2(480f, 28f);

        var track = NewImage("Track", sGo.transform, TrackBg);
        Stretch(track.rectTransform);

        var fillArea = new GameObject("FillArea", typeof(RectTransform));
        fillArea.transform.SetParent(sGo.transform, false);
        var fart = (RectTransform)fillArea.transform;
        Stretch(fart);
        var fill = NewImage("Fill", fillArea.transform, Orange);
        Stretch(fill.rectTransform);

        var handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(sGo.transform, false);
        var hart = (RectTransform)handleArea.transform;
        Stretch(hart);
        hart.offsetMin = new Vector2(14f, 0f);
        hart.offsetMax = new Vector2(-14f, 0f);
        var handle = NewImage("Handle", handleArea.transform, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(28f, 44f);

        var slider = sGo.AddComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
    }

    void AddSelector(Transform parent, string label, float y)
    {
        var row = NewImage(label + "Row", parent, RowBg);
        PlaceRow(row.rectTransform, y);
        NewText("Label", row.transform, label, 26, FontStyle.Bold, Color.white,
            new Vector2(-340f, 0f), new Vector2(240f, 50f)).alignment = TextAnchor.MiddleLeft;

        MakeArrow(row.transform, "<", new Vector2(-70f, 0f));
        NewText("Value", row.transform, "", 26, FontStyle.Bold, Color.white,
            new Vector2(130f, 0f), new Vector2(340f, 50f));
        MakeArrow(row.transform, ">", new Vector2(330f, 0f));
    }

    void MakeArrow(Transform parent, string glyph, Vector2 pos)
    {
        var img = NewImage("Arrow" + glyph, parent, new Color(0.24f, 0.19f, 0.30f, 1f));
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(56f, 50f);
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var c = btn.colors;
        c.highlightedColor = new Color(1f, 0.85f, 0.75f, 1f);
        c.pressedColor = new Color(0.7f, 0.65f, 0.65f, 1f);
        btn.colors = c;
        var t = NewText("Text", img.transform, glyph, 30, FontStyle.Bold, Color.white, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
    }

    // ---------------------------------------------------------------- helpers
    static void PlaceRow(RectTransform rt, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(900f, 64f);
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
