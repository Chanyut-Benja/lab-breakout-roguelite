using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Dark Cyber-Lab Pause Menu Manager.
/// ควบคุมการเปิด/ปิดหน้าต่าง Pause เมื่อกด ESC, จัดการ Time.timeScale,
/// ป้องกันการเปิดซ้อนทับ LevelUp และ GameOver, และควบคุมปุ่ม Resume, Restart, Main Menu.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("Pause Panel References")]
    [FormerlySerializedAs("pauseMenuUI")]
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject pauseMenuUI; // Backup fallback for legacy scene references

    [Header("Menu Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Panel Background & Title (Optional)")]
    [SerializeField] private Text titleText;

    private bool isPaused = false;
    private int lastToggleFrame = -1;

    public bool IsPaused => isPaused;
    public GameObject PauseMenuPanel => pauseMenuPanel;

    // Dark Sci-Fi Color Palette
    private static readonly Color BgDarkOverlay = new Color(0.02f, 0.03f, 0.05f, 0.88f);
    private static readonly Color NeonCyan = new Color(0f, 0.90f, 1f, 1f);            // #00E5FF
    private static readonly Color NeonAmber = new Color(1f, 0.75f, 0.10f, 1f);        // #FFBF1A
    private static readonly Color NeonRed = new Color(1f, 0.25f, 0.30f, 1f);          // #FF404D
    private static readonly Color SlateBorder = new Color(0.15f, 0.22f, 0.32f, 1f);    // #263852
    private static readonly Color BtnDarkBg = new Color(0.05f, 0.07f, 0.11f, 0.95f);   // #0D121C

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

        ResolveUIReferences();
    }

    private void Start()
    {
        ResolveUIReferences();

        // เมื่อเริ่มซีน บังคับเวลาเดินปกติและซ่อน Pause Panel
        if (!isPaused)
        {
            Time.timeScale = 1f;
            if (pauseMenuPanel != null) pauseMenuPanel.SetActive(false);
            if (pauseMenuUI != null && pauseMenuUI != pauseMenuPanel) pauseMenuUI.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (Time.frameCount == lastToggleFrame) return;
        lastToggleFrame = Time.frameCount;

        if (isPaused)
        {
            Resume();
        }
        else
        {
            if (IsBlockingScreenOpen())
            {
                Debug.Log("[PauseManager] Cannot pause: LevelUp, GameOver, or blocking dialogue is active.");
                return;
            }

            Pause();
        }
    }

    public void Pause()
    {
        if (IsBlockingScreenOpen()) return;

        isPaused = true;
        Time.timeScale = 0f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.isPaused = true;
        }

        ResolveUIReferences();

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.transform.SetAsLastSibling();
            pauseMenuPanel.SetActive(true);
        }

        Debug.Log("[PauseManager] Game Paused (Time.timeScale = 0).");
    }

    public void Resume()
    {
        OnResumeClicked();
    }

    public void OnResumeClicked()
    {
        isPaused = false;
        Time.timeScale = 1f;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.isPaused = false;
        }

        if (pauseMenuPanel != null)
        {
            pauseMenuPanel.SetActive(false);
        }
        if (pauseMenuUI != null && pauseMenuUI != pauseMenuPanel)
        {
            pauseMenuUI.SetActive(false);
        }

        Debug.Log("[PauseManager] Game Resumed (Time.timeScale = 1).");
    }

    public void OnRestartClicked()
    {
        Debug.Log("[PauseManager] Restarting run...");
        Time.timeScale = 1f;
        isPaused = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRunState();
        }

        ClearInRunData();

        int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;
        if (activeSceneIndex >= 0)
        {
            SceneManager.LoadScene(activeSceneIndex);
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

    public void OnMainMenuClicked()
    {
        Debug.Log("[PauseManager] Returning to Main Menu...");
        Time.timeScale = 1f;
        isPaused = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRunState();
        }

        ClearInRunData();
        SceneManager.LoadScene("MainMenuScene");
    }

    public void RestartGame() => OnRestartClicked();
    public void GoToMainMenu() => OnMainMenuClicked();

    private void ClearInRunData()
    {
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.ResetRunData();
        }

        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.ClearBoonsForTesting();
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRunState();
        }
    }

    public bool IsBlockingScreenOpen()
    {
        if (GameOverManager.Instance != null)
        {
            if (GameOverManager.Instance.IsGameOverActive || !GameOverManager.Instance.IsRunActive)
                return true;
        }

        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            return true;
        }

        if (LevelUpUI.Instance != null && LevelUpUI.Instance.IsOpen)
        {
            return true;
        }

        if (IsNamedPanelActiveInScene("GameOverPanel") ||
            IsNamedPanelActiveInScene("VictoryPanel") ||
            IsNamedPanelActiveInScene("LevelUpPanel") ||
            IsNamedPanelActiveInScene("ShopPanel") ||
            IsNamedPanelActiveInScene("BoonPanel"))
        {
            return true;
        }

        return false;
    }

    private bool IsNamedPanelActiveInScene(string panelName)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform[] children = canvases[i].GetComponentsInChildren<Transform>(true);
            for (int c = 0; c < children.Length; c++)
            {
                if (children[c] != null && children[c].gameObject.activeInHierarchy &&
                    string.Equals(children[c].name, panelName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public void ResolveUIReferences()
    {
        if (pauseMenuPanel == null)
        {
            if (pauseMenuUI != null)
            {
                pauseMenuPanel = pauseMenuUI;
            }
            else
            {
                pauseMenuPanel = FindPanelInScene("PausedPanel") ?? FindPanelInScene("PauseMenuPanel");
            }
        }

        if (pauseMenuPanel == null) return;

        GameObject secondaryPanel = null;
        if (pauseMenuPanel.name == "PausedPanel")
        {
            secondaryPanel = FindPanelInScene("PauseMenuPanel");
        }
        else if (pauseMenuPanel.name == "PauseMenuPanel")
        {
            secondaryPanel = FindPanelInScene("PausedPanel");
        }

        if (resumeButton == null)
        {
            resumeButton = CreateSciFiButton("ResumeButton", pauseMenuPanel.transform, new Vector2(0f, 70f), "RESUME", NeonCyan, OnResumeClicked);
        }
        else
        {
            RectTransform rRt = resumeButton.GetComponent<RectTransform>();
            if (rRt != null)
            {
                rRt.anchoredPosition = new Vector2(0f, 70f);
                rRt.sizeDelta = new Vector2(160f, 30f); 
            }
        }

        if (restartButton == null)
        {
            restartButton = FindButtonInPanel(pauseMenuPanel, "Restart");
            if (restartButton == null && secondaryPanel != null)
            {
                restartButton = FindButtonInPanel(secondaryPanel, "Restart");
                if (restartButton != null)
                {
                    restartButton.transform.SetParent(pauseMenuPanel.transform, false);
                }
            }
        }

        if (mainMenuButton == null)
        {
            mainMenuButton = FindButtonInPanel(pauseMenuPanel, "MainMenu") ?? FindButtonInPanel(pauseMenuPanel, "Menu");
            if (mainMenuButton == null && secondaryPanel != null)
            {
                mainMenuButton = FindButtonInPanel(secondaryPanel, "MainMenu") ?? FindButtonInPanel(secondaryPanel, "Menu");
                if (mainMenuButton != null)
                {
                    mainMenuButton.transform.SetParent(pauseMenuPanel.transform, false);
                }
            }
        }

        if (secondaryPanel != null && secondaryPanel != pauseMenuPanel)
        {
            secondaryPanel.SetActive(false);
        }

        // ปรับพิกัดแกน Y เป็น 70f เพื่อให้อยู่เหนือ MainMenuButton (ที่อยู่ 0f) ไม่ทับกัน
        if (resumeButton == null)
        {
            resumeButton = CreateSciFiButton("ResumeButton", pauseMenuPanel.transform, new Vector2(0f, 70f), "RESUME", NeonCyan, OnResumeClicked);
        }
        else
        {
            RectTransform rRt = resumeButton.GetComponent<RectTransform>();
            if (rRt != null) rRt.anchoredPosition = new Vector2(0f, 70f);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick.RemoveListener(OnResumeClicked);
            resumeButton.onClick.AddListener(OnResumeClicked);
            ConfigureButtonGraphic(resumeButton, NeonCyan);
        }

        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(OnRestartClicked);
            restartButton.onClick.AddListener(OnRestartClicked);
            ConfigureButtonGraphic(restartButton, NeonAmber);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
            ConfigureButtonGraphic(mainMenuButton, NeonRed);
        }

        ConfigurePanelOverlay(pauseMenuPanel);
    }

    private GameObject FindPanelInScene(string name)
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Transform[] children = canvases[i].GetComponentsInChildren<Transform>(true);
            for (int c = 0; c < children.Length; c++)
            {
                if (children[c] != null && string.Equals(children[c].name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return children[c].gameObject;
                }
            }
        }
        return null;
    }

    private Button FindButtonInPanel(GameObject panel, string keyword)
    {
        if (panel == null) return null;
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return buttons[i];
            }
        }
        return null;
    }

    private void ConfigurePanelOverlay(GameObject panel)
    {
        if (panel == null) return;

        RectTransform rt = panel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        Image img = panel.GetComponent<Image>();
        if (img != null)
        {
            img.color = BgDarkOverlay;
            img.raycastTarget = true;
        }

        Transform titleTr = panel.transform.Find("PAUSED") ?? panel.transform.Find("TitleText") ?? panel.transform.Find("Title");
        if (titleTr != null)
        {
            RectTransform tRt = titleTr.GetComponent<RectTransform>();
            if (tRt != null)
            {
                tRt.anchorMin = new Vector2(0.5f, 0.5f);
                tRt.anchorMax = new Vector2(0.5f, 0.5f);
                tRt.pivot = new Vector2(0.5f, 0.5f);
                tRt.anchoredPosition = new Vector2(0f, 130f);
                tRt.sizeDelta = new Vector2(400f, 60f);
            }

            Text txt = titleTr.GetComponent<Text>();
            if (txt != null)
            {
                txt.font = InGameHUDController.GetDefaultFont();
                txt.fontSize = 42;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = Color.white;
                txt.text = "SYSTEM PAUSED";
                txt.raycastTarget = false;

                Outline outline = titleTr.GetComponent<Outline>();
                if (outline == null) outline = titleTr.gameObject.AddComponent<Outline>();
                outline.effectColor = NeonCyan;
                outline.effectDistance = new Vector2(2f, -2f);
            }
        }
    }

    private void ConfigureButtonGraphic(Button btn, Color accentColor)
    {
        if (btn == null) return;

        Image img = btn.GetComponent<Image>();
        if (img != null)
        {
            img.color = BtnDarkBg;
            img.raycastTarget = true;
        }

        Outline outline = btn.GetComponent<Outline>();
        if (outline == null) outline = btn.gameObject.AddComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        if (btn.GetComponent<CardHoverEffect>() == null)
        {
            btn.gameObject.AddComponent<CardHoverEffect>();
        }

        Text txt = btn.GetComponentInChildren<Text>(true);
        if (txt != null)
        {
            txt.font = InGameHUDController.GetDefaultFont();
            txt.fontSize = 20;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.raycastTarget = false;
        }
    }

    private Button CreateSciFiButton(string name, Transform parent, Vector2 anchoredPos, string label, Color accentColor, UnityEngine.Events.UnityAction onClickAction)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline), typeof(CardHoverEffect));
        btnObj.transform.SetParent(parent, false);

        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(240f, 48f);

        Image img = btnObj.GetComponent<Image>();
        img.color = BtnDarkBg;
        img.raycastTarget = true;

        Outline outline = btnObj.GetComponent<Outline>();
        outline.effectColor = accentColor;
        outline.effectDistance = new Vector2(2f, -2f);

        Button btn = btnObj.GetComponent<Button>();
        if (onClickAction != null)
        {
            btn.onClick.AddListener(onClickAction);
        }

        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textObj.GetComponent<Text>();
        txt.font = InGameHUDController.GetDefaultFont();
        txt.fontSize = 20;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = Color.white;
        txt.text = label;
        txt.raycastTarget = false;

        return btn;
    }
}