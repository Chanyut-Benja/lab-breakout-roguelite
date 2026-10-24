using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Dark Sci-Fi UI Manager for Game Over and Victory Summary Panels.
/// Features:
/// 1. Summary Window (950x650): Centered, dark grey-black (#10141B), neon red (#FF3344) on defeat or neon gold (#FFD166) on victory.
/// 2. Header Title: "OPERATIVE TERMINATED" or "EXTRACTION SUCCESSFUL" in 48sp Bold.
/// 3. Combat Stats (2 Columns, Font 20sp): Rooms Cleared, Bosses Defeated, Total Kills, Surviving Time.
/// 4. RP Calculation Box: Neon cyan border (#00E5FF), detailed breakdown (Normal rooms x25, Bosses x75, Floor Clear Bonus, Extractor multiplier),
///    large 32sp bold net RP earned, and current accumulated RP in vault.
/// 5. Navigation Buttons (280x60, Font 22sp Bold): [DEPLOY AGAIN] and [MAIN MENU] with CardHoverEffect.
/// </summary>
public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }
    public static event Action<int> OnCreditsChanged;

    [Header("UI Panels")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;

    [Header("Text References (Legacy / Inspector)")]
    [SerializeField] private Text gameOverRPText;
    [SerializeField] private Text victoryRPText;

    [Header("Run Telemetry & Stats")]
    private int collectedRP = 0;          // แต้ม RP รวมที่ได้รับเมื่อจบรัน
    private int inRunBonusRP = 0;         // แต้มพิเศษที่เก็บได้ระหว่างรัน (ก่อนคูณตอนจบ)
    private int normalRoomsCleared = 0;    // เคลียร์ห้องปกติ = 25 RP/ห้อง
    private int bossesDefeated = 0;        // ชนะบอส = 75 RP/ตัว
    private bool floor1Completed = false;  // โบนัสจบ Floor 1 (ห้อง 9) = +400 RP
    private bool floor2Completed = false;  // โบนัสจบ Floor 2 (ห้อง 18 / ชนะเกม) = +1,000 RP
    private bool hasFinalizedRunRP = false;
    private int runCredits = 0;

    private int totalEnemiesKilled = 0;    // จำนวนศัตรูทั้งหมดที่กำจัดได้ในรันนี้
    private float runElapsedTime = 0f;     // เวลาที่รอดชีวิตในรันนี้ (วินาที)
    private bool isRunActive = true;       // สถานะว่ารันยังดำเนินอยู่หรือไม่

    public int CollectedRP => collectedRP;
    public int NormalRoomsCleared => normalRoomsCleared;
    public int BossesDefeated => bossesDefeated;
    public bool Floor1Completed => floor1Completed;
    public bool Floor2Completed => floor2Completed;
    public int RunCredits => runCredits;
    public int TotalEnemiesKilled => totalEnemiesKilled;
    public float RunElapsedTime => runElapsedTime;

    // Dark Sci-Fi Color Palette
    private static readonly Color BgDarkOverlay = new Color(0.02f, 0.03f, 0.05f, 0.88f);
    private static readonly Color WindowDarkBg = new Color(0.063f, 0.078f, 0.106f, 0.98f);  // #10141B
    private static readonly Color DefeatNeonRed = new Color(1.0f, 0.20f, 0.267f, 1f);        // #FF3344
    private static readonly Color VictoryNeonGold = new Color(1.0f, 0.82f, 0.40f, 1f);       // #FFD166
    private static readonly Color CyanNeonAccent = new Color(0.0f, 0.90f, 1.0f, 1f);         // #00E5FF
    private static readonly Color BoxDarkBg = new Color(0.035f, 0.063f, 0.094f, 0.96f);      // #091018
    private static readonly Color SlateBorderColor = new Color(0.12f, 0.16f, 0.23f, 1f);     // #1E293B
    private static readonly Color TextMuted = new Color(0.60f, 0.67f, 0.75f, 1f);            // #94A3B8
    private static readonly Color TextWhiteCyan = new Color(0.88f, 0.97f, 1.0f, 1f);         // #E0F7FA

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Configure full-screen panel overlays
        ConfigurePanelOverlay(gameOverPanel);
        ConfigurePanelOverlay(victoryPanel);
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

        Image panelImg = panel.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = BgDarkOverlay;
            panelImg.raycastTarget = true; // Blocks clicks from falling through to game
        }
    }

    private void Update()
    {
        if (isRunActive && Time.timeScale > 0f)
        {
            runElapsedTime += Time.deltaTime;
        }
    }

    /// <summary>
    /// บันทึกการสังหารศัตรู
    /// </summary>
    public void RecordEnemyKilled()
    {
        if (!isRunActive) return;
        totalEnemiesKilled++;
    }

    /// <summary>
    /// บันทึกการเคลียร์ห้องปกติ (+25 RP/ห้อง เมื่อคิดตอนจบรัน)
    /// </summary>
    public void RecordNormalRoomCleared(int roomNumber)
    {
        normalRoomsCleared++;
        Debug.Log($"[GameOverManager] Normal Room {roomNumber} cleared! Total Normal Rooms Cleared: {normalRoomsCleared} (+{normalRoomsCleared * ResearchTreeManager.RP_PER_NORMAL_ROOM} Base RP)");
    }

    /// <summary>
    /// บันทึกการชนะบอส (+75 RP/ตัว, ห้อง 9 ได้โบนัสจบ Floor 1 +400 RP, ห้อง 18 ได้โบนัสจบ Floor 2 +1,000 RP)
    /// </summary>
    public void RecordBossDefeated(int roomNumber)
    {
        bossesDefeated++;
        if (roomNumber == 9)
        {
            floor1Completed = true;
        }
        else if (roomNumber >= 18)
        {
            floor2Completed = true;
        }

        Debug.Log($"[GameOverManager] Boss in Room {roomNumber} defeated! Total Bosses Defeated: {bossesDefeated} | Floor1Bonus: {floor1Completed} | Floor2Bonus: {floor2Completed}");
    }

    /// <summary>
    /// สั่งเพิ่ม Research Points พิเศษในระหว่างเล่น (เช่น เก็บเม็ด RP) โดยจะนำไปรวมคำนวณตอนจบรัน
    /// </summary>
    public void AddResearchPoints(int amount)
    {
        if (amount <= 0) return;
        inRunBonusRP += amount;
        Debug.Log($"[GameOverManager] Queued +{amount} In-Run Bonus RP (Total In-Run Bonus: {inRunBonusRP})");
    }

    public void AddRunCredits(int amount)
    {
        if (amount > 0)
        {
            runCredits += amount;
            OnCreditsChanged?.Invoke(runCredits);
        }
    }

    public bool TrySpendRunCredits(int amount)
    {
        if (amount < 0 || runCredits < amount) return false;
        runCredits -= amount;
        OnCreditsChanged?.Invoke(runCredits);
        return true;
    }

    /// <summary>
    /// คำนวณและบันทึก RP หลังจบรันตามเอกสารบาลานซ์ 9.4 และ 1.3:
    /// - เคลียร์ห้องปกติ = 25 RP/ห้อง
    /// - ชนะบอส = 75 RP/ตัว
    /// - โบนัสจบ Floor 1 = +400 RP, Floor 2 (ชนะเกม) = +1,000 RP
    /// - นำผลรวมคูณโบนัสจาก Genetic Extractor (ถ้ามี)
    /// - การันตีขั้นต่ำ 50 RP เสมอแม้ตายห้องแรก
    /// </summary>
    private int FinalizeAndSaveEndOfRunRP(bool isVictory)
    {
        if (hasFinalizedRunRP) return collectedRP;
        hasFinalizedRunRP = true;
        isRunActive = false;

        if (isVictory)
        {
            floor1Completed = true;
            floor2Completed = true;
        }

        float rpMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.RPBonusMultiplier : 1f;

        collectedRP = ResearchTreeManager.CalculateRunResearchPoints(
            normalRoomsCleared,
            bossesDefeated,
            floor1Completed,
            floor2Completed,
            rpMultiplier,
            inRunBonusRP);

        SaveSystem.AddResearchPoints(collectedRP);

        Debug.Log($"[GameOverManager] Run Ended (Victory={isVictory}) | NormalRooms={normalRoomsCleared} (x25), Bosses={bossesDefeated} (x75), Floor1={floor1Completed} (+400), Floor2={floor2Completed} (+1000), GeneticExtractor={rpMultiplier:F2}x => Final Awarded RP: +{collectedRP} (Min 50 Guaranteed)");
        return collectedRP;
    }

    public void TriggerGameOver()
    {
        int finalRP = FinalizeAndSaveEndOfRunRP(false);
        int totalVaultRP = SaveSystem.GetResearchPoints();
        Time.timeScale = 0f;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            ApplyDarkSciFiSummaryTheme(gameOverPanel, false, finalRP, totalVaultRP);
            if (gameOverRPText != null)
                gameOverRPText.text = $"Research Points Gained: +{finalRP} RP";
        }

        Debug.Log($"[GameOverManager] Game Over Triggered! Total RP Gained: {finalRP}");
    }

    [ContextMenu("Test Trigger Victory")]
    public void TriggerVictory()
    {
        int finalRP = FinalizeAndSaveEndOfRunRP(true);
        int totalVaultRP = SaveSystem.GetResearchPoints();
        Time.timeScale = 0f;

        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            ApplyDarkSciFiSummaryTheme(victoryPanel, true, finalRP, totalVaultRP);
            if (victoryRPText != null)
                victoryRPText.text = $"Research Points Gained: +{finalRP} RP";
        }

        Debug.Log($"[GameOverManager] Victory Triggered! Total RP Gained: {finalRP}");
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenuScene");
    }

    public static Font GetDefaultFont()
    {
        try
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) return font;
        }
        catch { }

        try
        {
            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) return font;
        }
        catch { }

        Font[] allFonts = Resources.FindObjectsOfTypeAll<Font>();
        if (allFonts != null && allFonts.Length > 0)
        {
            foreach (var f in allFonts)
            {
                if (f != null) return f;
            }
        }
        return null;
    }

    /// <summary>
    /// Builds or updates the Dark Sci-Fi Summary Window inside targetPanel
    /// </summary>
    public void ApplyDarkSciFiSummaryTheme(GameObject targetPanel, bool isVictory, int finalRP, int totalVaultRP)
    {
        if (targetPanel == null) return;

        Font font = GetDefaultFont();

        // Ensure targetPanel is full-screen dark backdrop
        RectTransform panelRect = targetPanel.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image panelImg = targetPanel.GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.color = BgDarkOverlay;
            panelImg.raycastTarget = true;
        }

        // Hide old legacy direct children (Title, RPText, RestartButton, MainMenuButton)
        for (int i = 0; i < targetPanel.transform.childCount; i++)
        {
            Transform child = targetPanel.transform.GetChild(i);
            if (child.name != "SummaryWindow")
            {
                child.gameObject.SetActive(false);
            }
        }

        // Find or create SummaryWindow
        Transform winTrans = targetPanel.transform.Find("SummaryWindow");
        if (winTrans != null)
        {
            if (Application.isPlaying)
            {
                Destroy(winTrans.gameObject);
            }
            else
            {
                DestroyImmediate(winTrans.gameObject);
            }
        }

        // 1. Summary Window Root (950 x 650)
        GameObject windowGo = new GameObject("SummaryWindow", typeof(RectTransform), typeof(Image));
        windowGo.transform.SetParent(targetPanel.transform, false);
        RectTransform winRect = windowGo.GetComponent<RectTransform>();
        winRect.anchorMin = new Vector2(0.5f, 0.5f);
        winRect.anchorMax = new Vector2(0.5f, 0.5f);
        winRect.pivot = new Vector2(0.5f, 0.5f);
        winRect.anchoredPosition = Vector2.zero;
        winRect.sizeDelta = new Vector2(950f, 650f);

        Image borderImg = windowGo.GetComponent<Image>();
        borderImg.color = isVictory ? VictoryNeonGold : DefeatNeonRed;
        borderImg.raycastTarget = false;

        // 2. Inner Background (offset 3px for sleek neon border)
        GameObject innerBgGo = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
        innerBgGo.transform.SetParent(windowGo.transform, false);
        RectTransform innerRect = innerBgGo.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(3f, 3f);
        innerRect.offsetMax = new Vector2(-3f, -3f);

        Image innerImg = innerBgGo.GetComponent<Image>();
        innerImg.color = WindowDarkBg;
        innerImg.raycastTarget = false;

        // 3. Header Title (Font 48sp Bold)
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(innerBgGo.transform, false);
        RectTransform titleRect = titleGo.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -24f);
        titleRect.sizeDelta = new Vector2(900f, 56f);

        Text titleText = titleGo.GetComponent<Text>();
        titleText.font = font;
        titleText.fontSize = 48;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = isVictory ? VictoryNeonGold : DefeatNeonRed;
        titleText.text = isVictory ? "EXTRACTION SUCCESSFUL" : "OPERATIVE TERMINATED";
        titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.raycastTarget = false;

        // 4. Subtitle Tag (Font 13sp Bold)
        GameObject subGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
        subGo.transform.SetParent(innerBgGo.transform, false);
        RectTransform subRect = subGo.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 1f);
        subRect.anchorMax = new Vector2(0.5f, 1f);
        subRect.pivot = new Vector2(0.5f, 1f);
        subRect.anchoredPosition = new Vector2(0f, -80f);
        subRect.sizeDelta = new Vector2(900f, 22f);

        Text subText = subGo.GetComponent<Text>();
        subText.font = font;
        subText.fontSize = 13;
        subText.fontStyle = FontStyle.Bold;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = isVictory ? new Color(0.40f, 0.90f, 1.0f, 0.90f) : new Color(1.0f, 0.50f, 0.55f, 0.90f);
        subText.text = isVictory ? "[ STATUS: MISSION ACCOMPLISHED // FACILITY EVACUATED ]" : "[ STATUS: CRITICAL FAILURE // BIOMETRIC SIGNAL TERMINATED ]";
        subText.raycastTarget = false;

        // 5. Combat Stats Box (Font 20sp, 2 Columns)
        GameObject statsBoxGo = new GameObject("CombatStatsBox", typeof(RectTransform), typeof(Image));
        statsBoxGo.transform.SetParent(innerBgGo.transform, false);
        RectTransform statsBoxRect = statsBoxGo.GetComponent<RectTransform>();
        statsBoxRect.anchorMin = new Vector2(0.5f, 1f);
        statsBoxRect.anchorMax = new Vector2(0.5f, 1f);
        statsBoxRect.pivot = new Vector2(0.5f, 1f);
        statsBoxRect.anchoredPosition = new Vector2(0f, -112f);
        statsBoxRect.sizeDelta = new Vector2(870f, 96f);

        Image statsBorderImg = statsBoxGo.GetComponent<Image>();
        statsBorderImg.color = SlateBorderColor;
        statsBorderImg.raycastTarget = false;

        GameObject statsInnerGo = new GameObject("StatsInnerBg", typeof(RectTransform), typeof(Image));
        statsInnerGo.transform.SetParent(statsBoxGo.transform, false);
        RectTransform statsInnerRect = statsInnerGo.GetComponent<RectTransform>();
        statsInnerRect.anchorMin = Vector2.zero;
        statsInnerRect.anchorMax = Vector2.one;
        statsInnerRect.offsetMin = new Vector2(1.5f, 1.5f);
        statsInnerRect.offsetMax = new Vector2(-1.5f, -1.5f);

        Image statsInnerImg = statsInnerGo.GetComponent<Image>();
        statsInnerImg.color = new Color(0.04f, 0.055f, 0.082f, 0.92f);
        statsInnerImg.raycastTarget = false;

        // Combat Stats Header Label
        GameObject statsTagGo = new GameObject("StatsTag", typeof(RectTransform), typeof(Text));
        statsTagGo.transform.SetParent(statsInnerGo.transform, false);
        RectTransform statsTagRect = statsTagGo.GetComponent<RectTransform>();
        statsTagRect.anchorMin = new Vector2(0f, 1f);
        statsTagRect.anchorMax = new Vector2(0f, 1f);
        statsTagRect.pivot = new Vector2(0f, 1f);
        statsTagRect.anchoredPosition = new Vector2(20f, -6f);
        statsTagRect.sizeDelta = new Vector2(400f, 18f);

        Text statsTagText = statsTagGo.GetComponent<Text>();
        statsTagText.font = font;
        statsTagText.fontSize = 11;
        statsTagText.fontStyle = FontStyle.Bold;
        statsTagText.color = new Color(0.45f, 0.55f, 0.65f, 1f);
        statsTagText.text = "COMBAT TELEMETRY & SURVIVAL RECORD";
        statsTagText.alignment = TextAnchor.MiddleLeft;
        statsTagText.raycastTarget = false;

        // Column 1 (Rooms Cleared & Bosses Defeated)
        GameObject col1Go = new GameObject("Col1Text", typeof(RectTransform), typeof(Text));
        col1Go.transform.SetParent(statsInnerGo.transform, false);
        RectTransform col1Rect = col1Go.GetComponent<RectTransform>();
        col1Rect.anchorMin = new Vector2(0f, 1f);
        col1Rect.anchorMax = new Vector2(0f, 1f);
        col1Rect.pivot = new Vector2(0f, 1f);
        col1Rect.anchoredPosition = new Vector2(24f, -28f);
        col1Rect.sizeDelta = new Vector2(400f, 58f);

        Text col1Text = col1Go.GetComponent<Text>();
        col1Text.font = font;
        col1Text.fontSize = 20;
        col1Text.fontStyle = FontStyle.Normal;
        col1Text.alignment = TextAnchor.MiddleLeft;
        col1Text.color = TextWhiteCyan;
        col1Text.supportRichText = true;
        col1Text.raycastTarget = false;
        col1Text.text = $"Rooms Cleared:   <color=#00E5FF><b>{normalRoomsCleared}</b></color>\nBosses Defeated:   <color=#FFD166><b>{bossesDefeated}</b></color>";

        // Column 2 (Total Kills & Surviving Time)
        GameObject col2Go = new GameObject("Col2Text", typeof(RectTransform), typeof(Text));
        col2Go.transform.SetParent(statsInnerGo.transform, false);
        RectTransform col2Rect = col2Go.GetComponent<RectTransform>();
        col2Rect.anchorMin = new Vector2(0f, 1f);
        col2Rect.anchorMax = new Vector2(0f, 1f);
        col2Rect.pivot = new Vector2(0f, 1f);
        col2Rect.anchoredPosition = new Vector2(460f, -28f);
        col2Rect.sizeDelta = new Vector2(400f, 58f);

        int totalSec = Mathf.FloorToInt(runElapsedTime);
        int mins = totalSec / 60;
        int secs = totalSec % 60;

        Text col2Text = col2Go.GetComponent<Text>();
        col2Text.font = font;
        col2Text.fontSize = 20;
        col2Text.fontStyle = FontStyle.Normal;
        col2Text.alignment = TextAnchor.MiddleLeft;
        col2Text.color = TextWhiteCyan;
        col2Text.supportRichText = true;
        col2Text.raycastTarget = false;
        col2Text.text = $"Total Kills:   <color=#FF5566><b>{totalEnemiesKilled}</b></color>\nSurviving Time:   <color=#FFFFFF><b>{mins:D2}:{secs:D2}</b></color>";

        // 6. RP Calculation Box (Highlighted with Cyan Neon Border #00E5FF)
        GameObject rpBoxGo = new GameObject("RPCalculationBox", typeof(RectTransform), typeof(Image));
        rpBoxGo.transform.SetParent(innerBgGo.transform, false);
        RectTransform rpBoxRect = rpBoxGo.GetComponent<RectTransform>();
        rpBoxRect.anchorMin = new Vector2(0.5f, 1f);
        rpBoxRect.anchorMax = new Vector2(0.5f, 1f);
        rpBoxRect.pivot = new Vector2(0.5f, 1f);
        rpBoxRect.anchoredPosition = new Vector2(0f, -220f);
        rpBoxRect.sizeDelta = new Vector2(870f, 222f);

        Image rpBorderImg = rpBoxGo.GetComponent<Image>();
        rpBorderImg.color = CyanNeonAccent;
        rpBorderImg.raycastTarget = false;

        GameObject rpInnerGo = new GameObject("RPInnerBg", typeof(RectTransform), typeof(Image));
        rpInnerGo.transform.SetParent(rpBoxGo.transform, false);
        RectTransform rpInnerRect = rpInnerGo.GetComponent<RectTransform>();
        rpInnerRect.anchorMin = Vector2.zero;
        rpInnerRect.anchorMax = Vector2.one;
        rpInnerRect.offsetMin = new Vector2(2f, 2f);
        rpInnerRect.offsetMax = new Vector2(-2f, -2f);

        Image rpInnerImg = rpInnerGo.GetComponent<Image>();
        rpInnerImg.color = BoxDarkBg;
        rpInnerImg.raycastTarget = false;

        // RP Box Header Tag
        GameObject rpTagGo = new GameObject("RPTag", typeof(RectTransform), typeof(Text));
        rpTagGo.transform.SetParent(rpInnerGo.transform, false);
        RectTransform rpTagRect = rpTagGo.GetComponent<RectTransform>();
        rpTagRect.anchorMin = new Vector2(0f, 1f);
        rpTagRect.anchorMax = new Vector2(0f, 1f);
        rpTagRect.pivot = new Vector2(0f, 1f);
        rpTagRect.anchoredPosition = new Vector2(20f, -8f);
        rpTagRect.sizeDelta = new Vector2(500f, 20f);

        Text rpTagText = rpTagGo.GetComponent<Text>();
        rpTagText.font = font;
        rpTagText.fontSize = 12;
        rpTagText.fontStyle = FontStyle.Bold;
        rpTagText.color = CyanNeonAccent;
        rpTagText.text = "RESEARCH POINTS (RP) ACQUISITION & BREAKDOWN";
        rpTagText.alignment = TextAnchor.MiddleLeft;
        rpTagText.raycastTarget = false;

        // RP Breakdown Rows
        int normalRP = normalRoomsCleared * ResearchTreeManager.RP_PER_NORMAL_ROOM;
        int bossRP = bossesDefeated * ResearchTreeManager.RP_PER_BOSS_KILL;
        int floorRP = (floor1Completed ? ResearchTreeManager.RP_BONUS_FLOOR_1 : 0) + (floor2Completed ? ResearchTreeManager.RP_BONUS_FLOOR_2 : 0);
        float rpMultiplier = PlayerStats.Instance != null ? PlayerStats.Instance.RPBonusMultiplier : 1f;

        CreateBreakdownRow(rpInnerGo.transform, font, -32f, $"Normal Rooms Cleared ({normalRoomsCleared} × 25 RP)", $"+{normalRP} RP", TextMuted, new Color(0.22f, 0.74f, 0.97f, 1f));
        CreateBreakdownRow(rpInnerGo.transform, font, -55f, $"Bosses Defeated ({bossesDefeated} × 75 RP)", $"+{bossRP} RP", TextMuted, new Color(0.99f, 0.83f, 0.30f, 1f));

        string floorClearDesc = (floor1Completed && floor2Completed) ? "Floor Clear Bonus (Floor 1 +400, Floor 2 +1,000)" :
                                floor1Completed ? "Floor Clear Bonus (Floor 1 Completed)" : "Floor Clear Bonus (None)";
        Color floorValColor = floorRP > 0 ? new Color(0.20f, 0.83f, 0.60f, 1f) : new Color(0.40f, 0.45f, 0.52f, 1f);
        CreateBreakdownRow(rpInnerGo.transform, font, -78f, floorClearDesc, $"+{floorRP} RP", TextMuted, floorValColor);

        // Multiplier & In-Run line
        string modifierDesc = (rpMultiplier > 1.001f ? $"Genetic Extractor Multiplier ({rpMultiplier:F2}x)" : "Standard Multiplier (1.00x)") + (inRunBonusRP > 0 ? " + In-Run Crystals" : "");
        string modifierVal = (rpMultiplier > 1.001f ? $"×{rpMultiplier:F2}" : "") + (inRunBonusRP > 0 ? $" (+{inRunBonusRP} RP)" : (rpMultiplier <= 1.001f ? "1.00x" : ""));
        CreateBreakdownRow(rpInnerGo.transform, font, -101f, modifierDesc, modifierVal, TextMuted, new Color(0.65f, 0.55f, 0.98f, 1f));

        // Subtle Divider Line
        GameObject dividerGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerGo.transform.SetParent(rpInnerGo.transform, false);
        RectTransform divRect = dividerGo.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0.5f, 1f);
        divRect.anchorMax = new Vector2(0.5f, 1f);
        divRect.pivot = new Vector2(0.5f, 1f);
        divRect.anchoredPosition = new Vector2(0f, -127f);
        divRect.sizeDelta = new Vector2(830f, 1f);

        Image divImg = dividerGo.GetComponent<Image>();
        divImg.color = new Color(0.0f, 0.90f, 1.0f, 0.25f);
        divImg.raycastTarget = false;

        // Total Net RP Earned (Font 32sp Bold Neon Cyan)
        GameObject totalRpGo = new GameObject("TotalNetRP", typeof(RectTransform), typeof(Text));
        totalRpGo.transform.SetParent(rpInnerGo.transform, false);
        RectTransform totalRpRect = totalRpGo.GetComponent<RectTransform>();
        totalRpRect.anchorMin = new Vector2(0f, 1f);
        totalRpRect.anchorMax = new Vector2(1f, 1f);
        totalRpRect.pivot = new Vector2(0.5f, 1f);
        totalRpRect.anchoredPosition = new Vector2(0f, -132f);
        totalRpRect.sizeDelta = new Vector2(-40f, 44f);

        Text totalRpText = totalRpGo.GetComponent<Text>();
        totalRpText.font = font;
        totalRpText.fontSize = 24;
        totalRpText.fontStyle = FontStyle.Bold;
        totalRpText.alignment = TextAnchor.MiddleLeft;
        totalRpText.color = Color.white;
        totalRpText.supportRichText = true;
        totalRpText.raycastTarget = false;
        totalRpText.text = $"TOTAL NET RP EARNED:   <color=#00E5FF><size=32>+{finalRP} RP</size></color>";

        // Link legacy text if available
        if (isVictory) victoryRPText = totalRpText;
        else gameOverRPText = totalRpText;

        // Current Total Vault Balance (Font 18sp Bold Gold)
        GameObject vaultRpGo = new GameObject("VaultBalance", typeof(RectTransform), typeof(Text));
        vaultRpGo.transform.SetParent(rpInnerGo.transform, false);
        RectTransform vaultRpRect = vaultRpGo.GetComponent<RectTransform>();
        vaultRpRect.anchorMin = new Vector2(0f, 1f);
        vaultRpRect.anchorMax = new Vector2(1f, 1f);
        vaultRpRect.pivot = new Vector2(0.5f, 1f);
        vaultRpRect.anchoredPosition = new Vector2(0f, -178f);
        vaultRpRect.sizeDelta = new Vector2(-40f, 28f);

        Text vaultRpText = vaultRpGo.GetComponent<Text>();
        vaultRpText.font = font;
        vaultRpText.fontSize = 18;
        vaultRpText.fontStyle = FontStyle.Bold;
        vaultRpText.alignment = TextAnchor.MiddleLeft;
        vaultRpText.color = VictoryNeonGold;
        vaultRpText.raycastTarget = false;
        vaultRpText.text = $"CURRENT RESEARCH VAULT BALANCE:   {totalVaultRP} RP";

        // 7. Navigation Buttons (Width 280, Height 60, Font 22sp Bold)
        // Positioned at Y = 46 from bottom (Y = -272 from center), with 40px gap between them (Center X = -160 and +160)
        Color restartBorderColor = isVictory ? VictoryNeonGold : DefeatNeonRed;
        Color restartBgColor = isVictory ? new Color(0.12f, 0.10f, 0.04f, 1f) : new Color(0.14f, 0.03f, 0.05f, 1f);
        CreateSciFiButton("RestartButton", innerBgGo.transform, new Vector2(-160f, 48f), new Vector2(280f, 60f),
            "DEPLOY AGAIN", restartBorderColor, restartBgColor, Color.white, font, RestartGame);

        Color menuBorderColor = new Color(0.22f, 0.74f, 0.97f, 1f); // #38BDF8
        Color menuBgColor = new Color(0.06f, 0.09f, 0.16f, 1f);     // #0F172A
        CreateSciFiButton("MainMenuButton", innerBgGo.transform, new Vector2(160f, 48f), new Vector2(280f, 60f),
            "MAIN MENU", menuBorderColor, menuBgColor, TextWhiteCyan, font, GoToMainMenu);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(targetPanel);
            if (!string.IsNullOrEmpty(targetPanel.scene.path))
            {
                EditorSceneManager.MarkSceneDirty(targetPanel.scene);
            }
        }
#endif
    }

    private void CreateBreakdownRow(Transform parent, Font font, float yPos, string leftText, string rightText, Color leftColor, Color rightColor)
    {
        GameObject rowGo = new GameObject("BreakdownRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);
        RectTransform rect = rowGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, yPos);
        rect.sizeDelta = new Vector2(-40f, 22f);

        // Left Label
        GameObject leftGo = new GameObject("LeftLabel", typeof(RectTransform), typeof(Text));
        leftGo.transform.SetParent(rowGo.transform, false);
        RectTransform leftRect = leftGo.GetComponent<RectTransform>();
        leftRect.anchorMin = new Vector2(0f, 0f);
        leftRect.anchorMax = new Vector2(0.75f, 1f);
        leftRect.offsetMin = Vector2.zero;
        leftRect.offsetMax = Vector2.zero;

        Text lText = leftGo.GetComponent<Text>();
        lText.font = font;
        lText.fontSize = 17;
        lText.fontStyle = FontStyle.Normal;
        lText.alignment = TextAnchor.MiddleLeft;
        lText.color = leftColor;
        lText.text = leftText;
        lText.raycastTarget = false;

        // Right Value
        GameObject rightGo = new GameObject("RightValue", typeof(RectTransform), typeof(Text));
        rightGo.transform.SetParent(rowGo.transform, false);
        RectTransform rightRect = rightGo.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.75f, 0f);
        rightRect.anchorMax = new Vector2(1f, 1f);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;

        Text rText = rightGo.GetComponent<Text>();
        rText.font = font;
        rText.fontSize = 17;
        rText.fontStyle = FontStyle.Bold;
        rText.alignment = TextAnchor.MiddleRight;
        rText.color = rightColor;
        rText.text = rightText;
        rText.raycastTarget = false;
    }

    private GameObject CreateSciFiButton(string name, Transform parent, Vector2 anchoredPos, Vector2 sizeDelta,
        string label, Color borderColor, Color bgColor, Color textColor, Font font, UnityEngine.Events.UnityAction onClickAction)
    {
        // 1. Button Container with Border & CardHoverEffect
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(CardHoverEffect));
        btnObj.transform.SetParent(parent, false);
        RectTransform rect = btnObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;

        Image borderImg = btnObj.GetComponent<Image>();
        borderImg.color = borderColor;
        borderImg.raycastTarget = true; // Enables click detection on whole button surface

        // 2. Inner Background Body (2px inset)
        GameObject innerBody = new GameObject("ButtonBody", typeof(RectTransform), typeof(Image));
        innerBody.transform.SetParent(btnObj.transform, false);
        RectTransform innerRect = innerBody.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2f, 2f);
        innerRect.offsetMax = new Vector2(-2f, -2f);

        Image bodyImg = innerBody.GetComponent<Image>();
        bodyImg.color = bgColor;
        bodyImg.raycastTarget = false;

        // Button Component setup
        Button btn = btnObj.GetComponent<Button>();
        btn.targetGraphic = borderImg;
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        cb.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        cb.selectedColor = Color.white;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        if (onClickAction != null)
        {
            btn.onClick.AddListener(onClickAction);
        }

        // 3. Label Text (Font 22sp Bold)
        GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(innerBody.transform, false);
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text txt = textObj.GetComponent<Text>();
        txt.font = font ?? GetDefaultFont();
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.text = label;
        txt.raycastTarget = false;

        return btnObj;
    }

    [ContextMenu("Apply Dark Sci-Fi Theme (Both Panels)")]
    public void ApplyDarkSciFiThemeInEditor()
    {
        int sampleRP = 275;
        int vaultRP = SaveSystem.GetResearchPoints();
        if (gameOverPanel != null)
        {
            ApplyDarkSciFiSummaryTheme(gameOverPanel, false, sampleRP, vaultRP);
        }
        if (victoryPanel != null)
        {
            ApplyDarkSciFiSummaryTheme(victoryPanel, true, sampleRP + 1400, vaultRP);
        }
        Debug.Log("[GameOverManager] Dark Sci-Fi Theme applied to GameOverPanel and VictoryPanel!");
    }
}
