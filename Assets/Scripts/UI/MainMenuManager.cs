using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Main Menu Manager: จัดการหน้าจอเมนูหลักในธีม Dark Cyber-Lab มินิมอล
/// 1. Main Panel (หน้าแรก):
///    - Title "SUBJECT N: LAB BREAKOUT": Font 56sp ตัวหนาสีขาวเรืองแสงขอบฟ้า ปรับตำแหน่งลงมาเล็กน้อย
///    - RPText: ตัด Image พื้นหลังออก เหลือเฉพาะตัวเลข RP ขนาด Font 30sp ชิดขวาบนเรืองแสง (#00E5FF)
///    - ปุ่มเมนูหลัก 3 ปุ่ม (Play, Skill Tree, Quit) ในธีมสีดำตัดขอบฟ้าเดียวกันทั้งหมด:
///      * PLAY (PlayButtonCharacter): ขนาดกว้าง 420px สูง 65px Font 26sp
///      * SKILL TREE (ResearchButton): ขนาดกว้าง 420px สูง 65px Font 26sp ผูกเข้ากับระบบ Research Lab
///      * QUIT (QuitButton): ขนาดกว้าง 420px สูง 65px Font 26sp
/// 2. ควบคุมการสลับ Panel: MainPanel, CharacterSelectPanel, ResearchPanel
/// </summary>
[ExecuteAlways]
public class MainMenuManager : MonoBehaviour
{
    public static MainMenuManager Instance { get; private set; }

    // Cyber-Lab Color Constants
    public static readonly Color NeonCyan = new Color(0f, 0.9f, 1f, 1f);               // #00E5FF
    public static readonly Color PureDarkBlack = new Color(0.04f, 0.05f, 0.08f, 1f);    // #0A0D14
    public static readonly Color BtnBorderCyan = new Color(0f, 0.85f, 1f, 0.85f);      // #00DAFF
    public static readonly Color BtnHighlight = new Color(0f, 0.9f, 1f, 1f);           // #00E5FF
    public static readonly Color BtnPressed = new Color(0f, 0.6f, 0.8f, 1f);            // #0099CC

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject researchPanel;
    [SerializeField] private GameObject characterSelectPanel;

    [Header("Top Bar Display")]
    [SerializeField] private Text rpAmountText;

    [Header("Main Menu Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button researchLabButton;
    [SerializeField] private Button quitButton;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "GameScene";
    public string GameSceneName => gameSceneName;

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

        // 3. ตรวจสอบสถานะเริ่มต้นเมื่อเข้าเกม:
        // - researchPanel และลูกหลานทั้งหมด (รวม LinesLayer) ต้อง SetActive(false)
        // - มีเฉพาะ MainPanel เท่านั้นที่ SetActive(true)
        if (researchPanel != null) researchPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);

        RelocateAndHideLinesLayers();
        CleanupStrayLinesAndDividers();

        ApplyCyberLabTheme();
    }

    private void Start()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
        }

        ShowMainPanel();
        UpdateRPDisplay();

        if (ResearchTreeManager.Instance != null && Application.isPlaying)
        {
            ResearchTreeManager.Instance.OnTreeUpdated += UpdateRPDisplay;
        }
    }

    private void OnEnable()
    {
        UpdateRPDisplay();
    }

    private void OnDestroy()
    {
        if (ResearchTreeManager.Instance != null)
        {
            ResearchTreeManager.Instance.OnTreeUpdated -= UpdateRPDisplay;
        }
    }

    /// <summary>
    /// ปรับสไตล์ Main Panel ให้เป็นธีม Dark Cyber-Lab มินิมอลตามสเปกล่าสุด
    /// </summary>
    [ContextMenu("Apply Cyber-Lab Theme")]
    public void ApplyCyberLabTheme()
    {
        Font sciFiFont = InGameHUDController.GetDefaultFont();

        CleanupStrayLinesAndDividers();

        if (mainPanel != null)
        {
            Image panelImg = mainPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.enabled = false;
                panelImg.raycastTarget = false;
            }

            // 1. Title "SUBJECT N: LAB BREAKOUT": Font 56sp ตัวหนาสีขาวเรืองแสงขอบฟ้า ปรับตำแหน่งลงมาเล็กน้อย (-140px)
            Transform titleTrans = mainPanel.transform.Find("TitleText");
            if (titleTrans != null)
            {
                RectTransform tRect = titleTrans.GetComponent<RectTransform>();
                if (tRect != null)
                {
                    tRect.anchorMin = new Vector2(0.5f, 1f);
                    tRect.anchorMax = new Vector2(0.5f, 1f);
                    tRect.pivot = new Vector2(0.5f, 0.5f);
                    tRect.anchoredPosition = new Vector2(0f, -140f);
                    tRect.sizeDelta = new Vector2(1100f, 75f);
                }

                Text tText = titleTrans.GetComponent<Text>();
                if (tText != null)
                {
                    tText.text = "SUBJECT N: LAB BREAKOUT";
                    tText.font = sciFiFont;
                    tText.fontSize = 56; // Font 56sp ตัวหนา
                    tText.fontStyle = FontStyle.Bold;
                    tText.color = Color.white;
                    tText.alignment = TextAnchor.MiddleCenter;
                    tText.raycastTarget = false;

                    // Cyan Glow Outline
                    Outline outline = titleTrans.GetComponent<Outline>();
                    if (outline == null) outline = titleTrans.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
                    outline.effectDistance = new Vector2(3f, -3f);
                }
            }

            // 2. RPText: เอา Image พื้นหลังออก เหลือเฉพาะตัวเลข RP ขนาด Font 30sp ชิดขวาบน
            if (rpAmountText == null)
            {
                Transform rpTrans = mainPanel.transform.Find("RPText");
                if (rpTrans == null) rpTrans = mainPanel.transform.Find("RPAmountText");
                if (rpTrans != null) rpAmountText = rpTrans.GetComponent<Text>();
            }
            if (rpAmountText != null)
            {
                // นำ Image Component พื้นหลังออก ถ้ามี
                Image bgImg = rpAmountText.GetComponent<Image>();
                if (bgImg != null)
                {
                    DestroyImmediate(bgImg);
                }

                rpAmountText.font = sciFiFont;
                rpAmountText.fontSize = 30; // Font 30sp
                rpAmountText.fontStyle = FontStyle.Bold;
                rpAmountText.color = NeonCyan; // #00E5FF
                rpAmountText.alignment = TextAnchor.MiddleRight;
                rpAmountText.raycastTarget = false;

                RectTransform rpRect = rpAmountText.GetComponent<RectTransform>();
                if (rpRect != null)
                {
                    rpRect.anchorMin = new Vector2(1f, 1f);
                    rpRect.anchorMax = new Vector2(1f, 1f);
                    rpRect.pivot = new Vector2(1f, 1f);
                    rpRect.anchoredPosition = new Vector2(-50f, -45f);
                    rpRect.sizeDelta = new Vector2(500f, 50f);
                }

                Shadow shadow = rpAmountText.GetComponent<Shadow>();
                if (shadow == null) shadow = rpAmountText.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                shadow.effectDistance = new Vector2(2f, -2f);
            }

            // 3. จัดการปุ่มหน้าหลัก 3 ปุ่ม (PLAY, SKILL TREE, QUIT) ให้เป็นธีมสีดำตัดขอบเดียวกันทั้งหมด ขนาด 420x65px Font 26sp
            SetupMenuButtons(sciFiFont);
        }
    }

    private void SetupMenuButtons(Font font)
    {
        // 1. Play Button (ค้นหา PlayButtonCharacter หรือ PlayButton)
        if (playButton == null)
        {
            Transform p1 = mainPanel.transform.Find("PlayButtonCharacter");
            if (p1 != null) playButton = p1.GetComponent<Button>();
            if (playButton == null)
            {
                Transform p2 = mainPanel.transform.Find("PlayButton");
                if (p2 != null) playButton = p2.GetComponent<Button>();
            }
        }

        // 2. Skill Tree Button (ค้นหา ResearchButton หรือ ResearchLabButton)
        if (researchLabButton == null)
        {
            Transform r1 = mainPanel.transform.Find("ResearchButton");
            if (r1 != null) researchLabButton = r1.GetComponent<Button>();
            if (researchLabButton == null)
            {
                Transform r2 = mainPanel.transform.Find("ResearchLabButton");
                if (r2 != null) researchLabButton = r2.GetComponent<Button>();
            }
        }

        // 3. Quit Button (QuitButton)
        if (quitButton == null)
        {
            Transform q = mainPanel.transform.Find("QuitButton");
            if (q != null) quitButton = q.GetComponent<Button>();
        }

        // ปิดปุ่มที่ซ้ำซ้อน (เช่น PlayButton ซ้ำกับ PlayButtonCharacter)
        Transform duplicatePlay = mainPanel.transform.Find("PlayButton");
        Transform charPlay = mainPanel.transform.Find("PlayButtonCharacter");
        if (duplicatePlay != null && charPlay != null && playButton == charPlay.GetComponent<Button>())
        {
            duplicatePlay.gameObject.SetActive(false);
        }

        // จัดวาง 3 ปุ่มเรียงแนวดิ่ง ระยะห่าง 20px
        // Button 1: PLAY (กว้าง 420px สูง 65px Font 26sp, Y = 35)
        if (playButton != null)
        {
            playButton.gameObject.SetActive(true);
            StyleMenuButton(playButton, "PLAY", new Vector2(0f, 35f), font);
            playButton.onClick.RemoveAllListeners();
            playButton.onClick.AddListener(StartRun);
        }

        // Button 2: SKILL TREE (กว้าง 420px สูง 65px Font 26sp, Y = -50)
        if (researchLabButton != null)
        {
            researchLabButton.gameObject.SetActive(true);
            StyleMenuButton(researchLabButton, "SKILL TREE", new Vector2(0f, -50f), font);
            researchLabButton.onClick.RemoveAllListeners();
            researchLabButton.onClick.AddListener(OpenResearchLab);
        }

        // Button 3: QUIT (กว้าง 420px สูง 65px Font 26sp, Y = -135)
        if (quitButton != null)
        {
            quitButton.gameObject.SetActive(true);
            StyleMenuButton(quitButton, "QUIT", new Vector2(0f, -135f), font);
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void StyleMenuButton(Button btn, string label, Vector2 anchoredPos, Font font)
    {
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(420f, 65f); // กว้าง 420px สูง 65px
            rt.localScale = Vector3.one;
        }

        // ธีมสีดำล้วนตัดขอบฟ้า (#0A0D14 พื้นหลัง, #00DAFF กรอบ)
        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.color = PureDarkBlack;
            btnImg.sprite = null;
        }

        // Neon Cyan Border Outline (2.5px)
        Outline outline = btn.GetComponent<Outline>();
        if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
        outline.effectColor = BtnBorderCyan;
        outline.effectDistance = new Vector2(2.5f, -2.5f);

        // Smooth Hover Animation (1.05x)
        if (btn.GetComponent<CardHoverEffect>() == null)
        {
            btn.gameObject.AddComponent<CardHoverEffect>();
        }

        var colors = btn.colors;
        colors.normalColor = PureDarkBlack;
        colors.highlightedColor = BtnHighlight;
        colors.pressedColor = BtnPressed;
        colors.disabledColor = new Color(0.15f, 0.2f, 0.25f, 0.5f);
        colors.colorMultiplier = 1f;
        btn.colors = colors;

        // Label Text: Font 26sp ตัวหนาสีขาว
        Text btnText = btn.GetComponentInChildren<Text>();
        if (btnText != null)
        {
            btnText.text = label;
            btnText.font = font;
            btnText.fontSize = 26; // Font 26sp
            btnText.fontStyle = FontStyle.Bold;
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.raycastTarget = false;

            Shadow shadow = btnText.GetComponent<Shadow>();
            if (shadow == null) shadow = btnText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }
    }

    public void UpdateRPDisplay()
    {
        int rp = SaveSystem.GetResearchPoints();

        if (rpAmountText != null)
        {
            rpAmountText.text = $"RESEARCH POINTS: {rp} RP";
        }
    }

    public void StartRun()
    {
        OpenCharacterSelect();
    }

    public void ShowMainPanel()
    {
        if (researchPanel != null) researchPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);

        RelocateAndHideLinesLayers();
        CleanupStrayLinesAndDividers();

        if (mainPanel != null) mainPanel.SetActive(true);
        UpdateRPDisplay();
    }

    /// <summary>
    /// ค้นหา LinesLayer ทั้งหมดใน Scene
    /// 1. บังคับให้ GameObject ของ LinesLayer ทั้งหมดถูกสร้างหรือย้ายไปเป็นลูกของ ResearchPanel โดยตรง (transform.SetParent(researchPanel.transform, false))
    /// 2. ตั้งค่าให้ LinesLayer ปิดการทำงาน (SetActive(false)) พร้อมกับ ResearchPanel เมื่อผู้เล่นอยู่หน้า MainPanel
    /// </summary>
    [ContextMenu("Relocate & Hide Lines Layers")]
    public void RelocateAndHideLinesLayers()
    {
        Canvas canvas = null;
        if (mainPanel != null) canvas = mainPanel.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;

        Transform[] allTransforms = canvas.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            Transform t = allTransforms[i];
            if (t == null) continue;

            if (t.name.ToLowerInvariant().Contains("lineslayer"))
            {
                if (researchPanel != null)
                {
                    // หากยังไม่ได้อยู่ใต้ ResearchPanel ให้ย้ายมาอยู่ใต้ ResearchPanel โดยตรง
                    if (t.parent != researchPanel.transform)
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                        {
                            var root = UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(t.gameObject);
                            if (root != null)
                            {
                                UnityEditor.PrefabUtility.UnpackPrefabInstance(root, UnityEditor.PrefabUnpackMode.Completely, UnityEditor.InteractionMode.AutomatedAction);
                            }
                        }
#endif
                        try
                        {
                            t.SetParent(researchPanel.transform, false);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"[MainMenuManager] Could not reparent {t.name}: {ex.Message}");
                        }
                    }

                    // ปิด LinesLayer ทันทีเมื่อ ResearchPanel ไม่ได้แอคทีฟ
                    if (!researchPanel.activeSelf)
                    {
                        t.gameObject.SetActive(false);
                    }
                }
                else
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
    }

    /// <summary>
    /// ตรวจสอบและลบ/ปิดการแสดงผลของเส้นกากบาท เส้นแบ่งแนวนอน/แนวตั้ง ConnectingLine หรือ UI Panel ที่มีขอบเส้น 9-slice หลุดมาใน MainMenuScene ทั้งหมด
    /// </summary>
    [ContextMenu("Cleanup Stray Lines & Dividers")]
    public void CleanupStrayLinesAndDividers()
    {
        // 1. ปิด Image พื้นหลังและขอบของ MainPanel, ResearchPanel, CharacterSelectPanel
        if (mainPanel != null)
        {
            Image panelImg = mainPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.sprite = null;
                panelImg.enabled = false;
            }
        }

        if (researchPanel != null)
        {
            Image rImg = researchPanel.GetComponent<Image>();
            if (rImg != null)
            {
                rImg.sprite = null;
                rImg.enabled = false;
            }
            if (mainPanel != null && mainPanel.activeSelf)
            {
                researchPanel.SetActive(false);
            }
        }

        if (characterSelectPanel != null)
        {
            Image cImg = characterSelectPanel.GetComponent<Image>();
            if (cImg != null)
            {
                cImg.sprite = null;
                cImg.enabled = false;
            }
            if (mainPanel != null && mainPanel.activeSelf)
            {
                characterSelectPanel.SetActive(false);
            }
        }

        // 2. ตรวจสอบ Canvas ทั้งหมด
        Canvas canvas = null;
        if (mainPanel != null) canvas = mainPanel.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);

        if (canvas != null)
        {
            Image canvasImg = canvas.GetComponent<Image>();
            if (canvasImg != null)
            {
                canvasImg.sprite = null;
                canvasImg.enabled = false;
            }

            Transform[] allInCanvas = canvas.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < allInCanvas.Length; i++)
            {
                Transform child = allInCanvas[i];
                if (child == null || child == canvas.transform || child == mainPanel?.transform) continue;

                // ข้ามองค์ประกอบทั้งหมดที่อยู่ภายใต้ ResearchPanel หรือ CharacterSelectPanel ไม่แตะต้อง Hierarchy ภายใน
                if (researchPanel != null && child.IsChildOf(researchPanel.transform)) continue;
                if (characterSelectPanel != null && child.IsChildOf(characterSelectPanel.transform)) continue;

                // ข้ามปุ่มเมนูหลักทั้ง 3 ปุ่ม และ TitleText, RPText
                if (child == playButton?.transform || child == researchLabButton?.transform || child == quitButton?.transform) continue;
                if (child.name == "TitleText" || child.name == "RPText" || child.name == "RPAmountText") continue;

                // ข้าม child ภายในปุ่มเมนูหลัก (เช่น Text)
                if (child.parent != null && (child.parent == playButton?.transform || child.parent == researchLabButton?.transform || child.parent == quitButton?.transform))
                    continue;

                string cName = child.name.ToLowerInvariant();

                // ก) ตรวจสอบชื่อที่บ่งบอกว่าเป็นเส้น หรือองค์ประกอบแบ่งส่วนที่หลุดออกมานอก ResearchPanel
                bool isSuspiciousName = cName.Contains("line") || cName.Contains("divider") || cName.Contains("connect") ||
                                       cName.Contains("cross") || cName.Contains("separator") || cName.Contains("axis") ||
                                       cName.Contains("grid") || cName.Contains("accent");

                if (isSuspiciousName)
                {
                    // หากเป็นผัง Research Tree ที่หลุดมาอยู่ใต้ Canvas หรือ MainPanel ตรงๆ
                    if (cName.Contains("threecolumn") || cName.Contains("lineslayer"))
                    {
#if UNITY_EDITOR
                        if (!Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject))
                        {
                            UnityEditor.PrefabUtility.UnpackPrefabInstance(
                                UnityEditor.PrefabUtility.GetOutermostPrefabInstanceRoot(child.gameObject),
                                UnityEditor.PrefabUnpackMode.Completely,
                                UnityEditor.InteractionMode.AutomatedAction
                            );
                        }
#endif
#if UNITY_EDITOR
                        bool isStillPrefab = !Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject);
#else
                        bool isStillPrefab = false;
#endif
                        if (!isStillPrefab)
                        {
                            if (researchPanel != null)
                            {
                                child.SetParent(researchPanel.transform, false);
                                child.gameObject.SetActive(false);
                            }
                            else
                            {
                                child.gameObject.SetActive(false);
                                if (Application.isPlaying) Destroy(child.gameObject);
                                else DestroyImmediate(child.gameObject);
                            }
                        }
                        continue;
                    }

#if UNITY_EDITOR
                    bool isPrefabNode = !Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject);
#else
                    bool isPrefabNode = false;
#endif
                    if (!isPrefabNode)
                    {
                        child.gameObject.SetActive(false);
                        if (Application.isPlaying)
                            Destroy(child.gameObject);
                        else
                            DestroyImmediate(child.gameObject);
                    }
                    else
                    {
                        Image strayImg = child.GetComponent<Image>();
                        if (strayImg != null) strayImg.enabled = false;
                    }
                    continue;
                }

                // ข) ตรวจสอบ UI Image ที่มีรูปทรงเป็นเส้นบาง (ความกว้างหรือความยาวน้อยกว่า 12px)
                Image img = child.GetComponent<Image>();
                if (img != null && child.GetComponent<Button>() == null)
                {
                    RectTransform rt = child.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        Vector2 size = rt.sizeDelta;
                        bool isThinLine = (size.x > 0f && size.x <= 12f && size.y > 15f) ||
                                          (size.y > 0f && size.y <= 12f && size.x > 15f);

                        bool isDefaultSlicedBg = img.sprite != null && (img.sprite.name == "Background" || img.sprite.name == "UISprite");

                        if (isThinLine || (isDefaultSlicedBg && (child.parent == mainPanel?.transform || child.parent == canvas.transform)))
                        {
                            img.enabled = false;
                            child.gameObject.SetActive(false);
#if UNITY_EDITOR
                            bool isPrefabInstance = !Application.isPlaying && UnityEditor.PrefabUtility.IsPartOfPrefabInstance(child.gameObject);
#else
                            bool isPrefabInstance = false;
#endif
                            if (!isPrefabInstance)
                            {
                                if (Application.isPlaying)
                                    Destroy(child.gameObject);
                                else
                                    DestroyImmediate(child.gameObject);
                            }
                        }
                    }
                }
            }
        }
    }

    public void OpenResearchLab()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (characterSelectPanel != null) characterSelectPanel.SetActive(false);
        if (researchPanel != null)
        {
            researchPanel.SetActive(true);

            // เปิด LinesLayer ทั้งหมดภายใต้ ResearchPanel
            Transform[] rChildren = researchPanel.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < rChildren.Length; i++)
            {
                if (rChildren[i] != null && rChildren[i].name.ToLowerInvariant().Contains("lineslayer"))
                {
                    rChildren[i].gameObject.SetActive(true);
                }
            }
        }
        UpdateRPDisplay();

        if (ResearchTreeManager.Instance != null)
        {
            ResearchTreeManager.Instance.EnsureNodeTreeVisualUI();
            ResearchTreeManager.Instance.RefreshAllNodeTreeVisuals();
            ResearchTreeManager.Instance.SetupResearchBackButton(researchPanel?.transform);
        }
        else if (researchPanel != null)
        {
            SetupFallbackBackButton(researchPanel.transform);
        }

        if (researchPanel != null)
        {
            foreach (ResearchNodeUI node in researchPanel.GetComponentsInChildren<ResearchNodeUI>(true))
                node.RefreshUI();
        }
    }

    private void SetupFallbackBackButton(Transform rPanel)
    {
        if (rPanel == null) return;
        Transform backBtnTr = rPanel.Find("BackButton");
        if (backBtnTr != null)
        {
            backBtnTr.gameObject.SetActive(true);
            backBtnTr.SetAsLastSibling();

            RectTransform rt = backBtnTr.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(120f, 60f); // AnchoredPosition (120, 60)
                rt.sizeDelta = new Vector2(180f, 55f);       // ขนาด 180px x 55px
            }

            Button btn = backBtnTr.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(ShowMainPanel);
            }

            Text txt = backBtnTr.GetComponentInChildren<Text>();
            if (txt != null)
            {
                txt.text = "< BACK";
                txt.fontSize = 22;
                txt.fontStyle = FontStyle.Bold;
                txt.color = Color.white;
                txt.alignment = TextAnchor.MiddleCenter;
            }
        }
    }

    public void OpenCharacterSelect()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (researchPanel != null) researchPanel.SetActive(false);
        if (characterSelectPanel != null)
        {
            characterSelectPanel.SetActive(true);
            CharacterSelectUI selectUI = characterSelectPanel.GetComponent<CharacterSelectUI>();
            if (selectUI != null)
            {
                selectUI.ApplyCyberLabCharacterSelectTheme();
            }
        }
    }

    public void QuitGame()
    {
        Debug.Log("Exiting Game...");
        Application.Quit();
    }
}
