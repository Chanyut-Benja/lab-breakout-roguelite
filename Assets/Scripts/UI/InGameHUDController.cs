using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// Dark Sci-Fi In-Game HUD Controller (1920x1080 Full HD Native)
/// 
/// 1. Health & EXP Bars (Top-Left):
///    - HP Bar: Dark grey background, thin border, neon red fill (#FF3344), HP text.
///    - EXP Bar: Slender glowing cyan bar (#00E5FF) directly below HP with level badge.
/// 
/// 2. Room Timer & Floor Tracker (Top-Center):
///    - Crisp countdown timer & floor indicator in white-cyan (#E0F7FA).
/// 
/// 3. Resource HUD Display (Top-Right):
///    - Evolution Core counter: Core Icon + Text (e.g. "x1").
///    - Nano-Credits counter: Credit Icon + Text (realtime live update).
/// 
/// 4. InGameHUDPanel - Item Slots (Bottom-Left):
///    - Weapon Slots: 3 horizontal slots (Lv.1 - Lv.5, Evolved form with ★EVO badge).
///    - Tech Passive Slots: 3 horizontal slots (Lv.1 - Lv.5).
///    - Boon Slots: 3 horizontal slots (Lv.1 - Lv.2 MAX, with pulsing Gold/Purple Resonance Indicator).
/// </summary>
public class InGameHUDController : MonoBehaviour
{
    public static InGameHUDController Instance { get; private set; }

    [Header("Managers & Core References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private WeaponManager weaponManager;
    [SerializeField] private BoonManager boonManager;
    [SerializeField] private CurrencyManager currencyManager;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Top-Left: Health & EXP Elements")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Text hpText;
    [SerializeField] private Slider expSlider;
    [SerializeField] private Text levelText;
    [SerializeField] private Text expValueText;

    [Header("Top-Center: Room & Timer Elements")]
    [SerializeField] private Text timerText;
    [SerializeField] private Text roomText;

    [Header("Top-Right: Resource HUD Display")]
    [SerializeField] private RectTransform resourceRoot;
    [SerializeField] private Image coreIconImage;
    [SerializeField] private TextMeshProUGUI coreCountTextTMP;
    [SerializeField] private Text coreCountTextLegacy;
    [SerializeField] private Image creditIconImage;
    [SerializeField] private TextMeshProUGUI creditCountTextTMP;
    [SerializeField] private Text creditCountTextLegacy;

    [Header("Bottom-Left: InGameHUDPanel Slot Containers")]
    [SerializeField] private RectTransform inGameHUDPanel;
    [SerializeField] private List<HUDSlotUI> weaponSlots = new List<HUDSlotUI>();
    [SerializeField] private List<HUDSlotUI> techSlots = new List<HUDSlotUI>();
    [SerializeField] private List<HUDSlotUI> boonSlots = new List<HUDSlotUI>();

    [Header("Colors (Dark Sci-Fi Theme)")]
    private static readonly Color NeonRedFill = new Color(1.0f, 0.20f, 0.267f, 1f);      // #FF3344
    private static readonly Color NeonCyanFill = new Color(0.0f, 0.90f, 1.0f, 1f);        // #00E5FF
    private static readonly Color WhiteCyanText = new Color(0.88f, 0.97f, 1.0f, 1f);      // #E0F7FA
    private static readonly Color DarkGreyBg = new Color(0.05f, 0.07f, 0.09f, 0.95f);     // #0D1117
    private static readonly Color ThinBorderSlate = new Color(0.16f, 0.21f, 0.29f, 1f);   // #29354A
    private static readonly Color NeonCyanBorder = new Color(0.22f, 0.74f, 0.97f, 1f);    // #38BDF8
    private static readonly Color EmeraldTechBorder = new Color(0.06f, 0.73f, 0.51f, 1f); // #10B981
    private static readonly Color PurpleBoonBorder = new Color(0.66f, 0.33f, 0.97f, 1f);  // #A855F7
    private static readonly Color RadiantGoldEvo = new Color(0.96f, 0.62f, 0.04f, 1f);     // #F59E0B
    private static readonly Color GoldCredits = new Color(1f, 0.820f, 0.400f, 1f);         // #FFD166

    private Font cachedFont;
    private TMP_FontAsset cachedTMPFont;

    private void Awake()
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

        cachedFont = GetDefaultFont();
        cachedTMPFont = GetDefaultTMPFont();
    }

    private void Start()
    {
        ResolveAllReferences();
        ApplyDarkSciFiSkin();
        SubscribeEvents();
        RefreshHUD();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void Update()
    {
        // Periodic safety check for dynamically spawned player or inventory
        if (playerHealth == null || playerInventory == null)
        {
            ResolveAllReferences();
        }
    }

    #region Auto Reference Resolution

    public void ResolveAllReferences()
    {
        if (playerStats == null)
        {
            playerStats = PlayerStats.Instance;
            if (playerStats == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) playerStats = player.GetComponent<PlayerStats>();
            }
        }

        if (playerInventory == null)
        {
            playerInventory = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        }

        if (weaponManager == null)
        {
            weaponManager = WeaponManager.Instance;
            if (weaponManager == null)
            {
                weaponManager = FindFirstObjectByType<WeaponManager>(FindObjectsInactive.Include);
            }
        }

        if (boonManager == null)
        {
            boonManager = BoonManager.Instance;
            if (boonManager == null)
            {
                boonManager = FindFirstObjectByType<BoonManager>(FindObjectsInactive.Include);
            }
        }

        if (currencyManager == null)
        {
            currencyManager = CurrencyManager.Instance;
            if (currencyManager == null)
            {
                currencyManager = FindFirstObjectByType<CurrencyManager>(FindObjectsInactive.Include);
            }
        }

        if (playerHealth == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
        }

        if (playerHealth != null && hpText != null)
        {
            playerHealth.SetHpText(hpText);
        }
    }

    private void FindExistingUIReferences(Canvas canvas)
    {
        if (canvas == null) return;

        if (healthSlider == null)
        {
            Transform hBar = canvas.transform.Find("HealthBar");
            if (hBar != null) healthSlider = hBar.GetComponent<Slider>();
        }

        if (expSlider == null || levelText == null || expValueText == null)
        {
            Transform eBar = canvas.transform.Find("ExpBar");
            if (eBar != null)
            {
                if (expSlider == null) expSlider = eBar.GetComponent<Slider>();
                if (levelText == null)
                {
                    Transform lvl = eBar.Find("LevelText");
                    if (lvl != null) levelText = lvl.GetComponent<Text>();
                }
                if (expValueText == null)
                {
                    Transform expVal = eBar.Find("ExpValueText");
                    if (expVal != null) expValueText = expVal.GetComponent<Text>();
                }
            }
        }

        if (timerText == null)
        {
            Transform tText = canvas.transform.Find("TimerText");
            if (tText != null) timerText = tText.GetComponent<Text>();
        }

        if (roomText == null)
        {
            Transform rText = canvas.transform.Find("RoomText");
            if (rText != null) roomText = rText.GetComponent<Text>();
        }
    }

    #endregion

    #region Event Subscription & Handlers

    private void SubscribeEvents()
    {
        // 1. Weapon Events
        if (weaponManager != null)
        {
            weaponManager.OnWeaponAdded -= HandleWeaponAdded;
            weaponManager.OnWeaponAdded += HandleWeaponAdded;
            weaponManager.OnWeaponUpgraded -= HandleWeaponUpgraded;
            weaponManager.OnWeaponUpgraded += HandleWeaponUpgraded;
            weaponManager.OnWeaponEvolved -= HandleWeaponEvolved;
            weaponManager.OnWeaponEvolved += HandleWeaponEvolved;
        }

        PlayerInventory.OnWeaponAddedStatic -= HandleWeaponAdded;
        PlayerInventory.OnWeaponAddedStatic += HandleWeaponAdded;
        PlayerInventory.OnWeaponUpgradedStatic -= HandleWeaponUpgraded;
        PlayerInventory.OnWeaponUpgradedStatic += HandleWeaponUpgraded;
        PlayerInventory.OnWeaponEvolvedStatic -= HandleWeaponEvolved;
        PlayerInventory.OnWeaponEvolvedStatic += HandleWeaponEvolved;

        // 2. Tech Passive Events
        PlayerInventory.OnTechPassiveAddedStatic -= HandleTechAdded;
        PlayerInventory.OnTechPassiveAddedStatic += HandleTechAdded;
        PlayerInventory.OnTechPassiveUpgradedStatic -= HandleTechUpgraded;
        PlayerInventory.OnTechPassiveUpgradedStatic += HandleTechUpgraded;

        // 3. Boon Events
        if (boonManager != null)
        {
            boonManager.OnBoonAdded -= HandleBoonAdded;
            boonManager.OnBoonAdded += HandleBoonAdded;
            boonManager.OnBoonUpgraded -= HandleBoonUpgraded;
            boonManager.OnBoonUpgraded += HandleBoonUpgraded;
            boonManager.OnBoonResonated -= HandleBoonResonated;
            boonManager.OnBoonResonated += HandleBoonResonated;
        }

        PlayerInventory.OnBoonAddedStatic -= HandleBoonAdded;
        PlayerInventory.OnBoonAddedStatic += HandleBoonAdded;
        PlayerInventory.OnBoonUpgradedStatic -= HandleBoonUpgraded;
        PlayerInventory.OnBoonUpgradedStatic += HandleBoonUpgraded;
        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonated;
        PlayerInventory.OnBoonResonatedStatic += HandleBoonResonated;

        // 4. Currency & Resources Events
        CurrencyManager.OnCreditsChanged -= HandleCreditsChanged;
        CurrencyManager.OnCreditsChanged += HandleCreditsChanged;
        GameOverManager.OnCreditsChanged -= HandleCreditsChanged;
        GameOverManager.OnCreditsChanged += HandleCreditsChanged;

        PlayerInventory.OnEvolutionCoreCountChangedStatic -= HandleEvolutionCoreCountChanged;
        PlayerInventory.OnEvolutionCoreCountChangedStatic += HandleEvolutionCoreCountChanged;
        CurrencyManager.OnEvolutionCoreCountChanged -= HandleEvolutionCoreCountChanged;
        CurrencyManager.OnEvolutionCoreCountChanged += HandleEvolutionCoreCountChanged;

        // 5. Fallback full inventory sync
        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshHUD;
            playerInventory.OnInventoryChanged += RefreshHUD;
        }
    }

    private void UnsubscribeEvents()
    {
        if (weaponManager != null)
        {
            weaponManager.OnWeaponAdded -= HandleWeaponAdded;
            weaponManager.OnWeaponUpgraded -= HandleWeaponUpgraded;
            weaponManager.OnWeaponEvolved -= HandleWeaponEvolved;
        }

        PlayerInventory.OnWeaponAddedStatic -= HandleWeaponAdded;
        PlayerInventory.OnWeaponUpgradedStatic -= HandleWeaponUpgraded;
        PlayerInventory.OnWeaponEvolvedStatic -= HandleWeaponEvolved;

        PlayerInventory.OnTechPassiveAddedStatic -= HandleTechAdded;
        PlayerInventory.OnTechPassiveUpgradedStatic -= HandleTechUpgraded;

        if (boonManager != null)
        {
            boonManager.OnBoonAdded -= HandleBoonAdded;
            boonManager.OnBoonUpgraded -= HandleBoonUpgraded;
            boonManager.OnBoonResonated -= HandleBoonResonated;
        }

        PlayerInventory.OnBoonAddedStatic -= HandleBoonAdded;
        PlayerInventory.OnBoonUpgradedStatic -= HandleBoonUpgraded;
        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonated;

        CurrencyManager.OnCreditsChanged -= HandleCreditsChanged;
        GameOverManager.OnCreditsChanged -= HandleCreditsChanged;

        PlayerInventory.OnEvolutionCoreCountChangedStatic -= HandleEvolutionCoreCountChanged;
        CurrencyManager.OnEvolutionCoreCountChanged -= HandleEvolutionCoreCountChanged;

        if (playerInventory != null)
        {
            playerInventory.OnInventoryChanged -= RefreshHUD;
        }
    }

    private void HandleWeaponAdded(WeaponSlotEntry entry) => RefreshWeaponSlots();
    private void HandleWeaponUpgraded(WeaponSlotEntry entry) => RefreshWeaponSlots();
    private void HandleWeaponEvolved(WeaponSlotEntry entry) => RefreshWeaponSlots();

    private void HandleTechAdded(TechSlotEntry entry) => RefreshTechSlots();
    private void HandleTechUpgraded(TechSlotEntry entry) => RefreshTechSlots();

    private void HandleBoonAdded(BoonSlotEntry entry) => RefreshBoonSlots();
    private void HandleBoonUpgraded(BoonSlotEntry entry) => RefreshBoonSlots();
    private void HandleBoonResonated(BoonSlotEntry entry)
    {
        RefreshBoonSlots();
        if (entry != null)
        {
            HighlightBoonResonance(entry.boonType);
        }
    }

    private void HandleCreditsChanged(int currentCredits) => UpdateCreditsDisplay(currentCredits);
    private void HandleEvolutionCoreCountChanged(int currentCores) => UpdateEvolutionCoresDisplay(currentCores);

    #endregion

    #region Public Refresh Methods

    /// <summary>
    /// เคลียร์หรือรีเฟรชสถานะ HUD ทั้งหมดเมื่อเริ่มรอบใหม่ โหลด Scene หรือมีการเปลี่ยนแปลงของไอเทม
    /// </summary>
    public void RefreshHUD()
    {
        ResolveAllReferences();
        RefreshWeaponSlots();
        RefreshTechSlots();
        RefreshBoonSlots();
        RefreshResourceCounters();
    }

    /// <summary>
    /// Backward-compatibility alias for RefreshHUD()
    /// </summary>
    public void RefreshInventorySlots()
    {
        RefreshHUD();
    }

    public void RefreshWeaponSlots()
    {
        PlayerInventory inv = playerInventory ?? PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        for (int i = 0; i < weaponSlots.Count; i++)
        {
            HUDSlotUI slot = weaponSlots[i];
            if (slot == null) continue;

            if (inv != null && i < inv.Weapons.Count)
            {
                WeaponSlotEntry entry = inv.Weapons[i];
                string nameShort = GetWeaponShortName(entry.weaponType, entry.isEvolved, entry.evolvedForm);
                Sprite wpIcon = entry.isEvolved
                    ? (PlayerInventory.GetEvolvedWeaponIcon(entry.evolvedForm) ?? PlayerInventory.GetWeaponIcon(entry.weaponType))
                    : PlayerInventory.GetWeaponIcon(entry.weaponType);

                slot.SetWeapon(wpIcon, entry.level, entry.isEvolved, nameShort);
            }
            else
            {
                slot.SetEmpty();
            }
        }
    }

    public void RefreshTechSlots()
    {
        PlayerInventory inv = playerInventory ?? PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        for (int i = 0; i < techSlots.Count; i++)
        {
            HUDSlotUI slot = techSlots[i];
            if (slot == null) continue;

            if (inv != null && i < inv.TechPassives.Count)
            {
                TechSlotEntry entry = inv.TechPassives[i];
                string nameShort = GetTechShortName(entry.techType);
                Sprite techIcon = PlayerInventory.GetTechPassiveIcon(entry.techType);

                slot.SetTech(techIcon, entry.level, nameShort);
            }
            else
            {
                slot.SetEmpty();
            }
        }
    }

    public void RefreshBoonSlots()
    {
        PlayerInventory inv = playerInventory ?? PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        for (int i = 0; i < boonSlots.Count; i++)
        {
            HUDSlotUI slot = boonSlots[i];
            if (slot == null) continue;

            if (inv != null && i < inv.Boons.Count)
            {
                BoonSlotEntry entry = inv.Boons[i];
                string nameShort = GetBoonShortName(entry.boonType);
                Sprite boonIcon = PlayerInventory.GetBoonIcon(entry.boonType);

                slot.SetBoon(boonIcon, entry.level, entry.isResonant, nameShort);
            }
            else
            {
                slot.SetEmpty();
            }
        }
    }

    public void HighlightBoonResonance(BoonItemType boonType)
    {
        PlayerInventory inv = playerInventory ?? PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        for (int i = 0; i < boonSlots.Count; i++)
        {
            HUDSlotUI slot = boonSlots[i];
            if (slot == null) continue;

            if (inv != null && i < inv.Boons.Count)
            {
                BoonSlotEntry entry = inv.Boons[i];
                if (entry.boonType == boonType)
                {
                    string nameShort = GetBoonShortName(entry.boonType);
                    Sprite boonIcon = PlayerInventory.GetBoonIcon(entry.boonType);
                    slot.SetBoon(boonIcon, entry.level, true, nameShort);
                    slot.SetResonanceActive(true, new Color(0.85f, 0.45f, 1.0f, 1f));
                    break;
                }
            }
        }
    }

    public void RefreshResourceCounters()
    {
        int credits = 0;
        if (GameOverManager.Instance != null)
        {
            credits = GameOverManager.Instance.RunCredits;
        }
        else if (currencyManager != null)
        {
            credits = currencyManager.CurrentCredits;
        }
        UpdateCreditsDisplay(credits);

        int cores = 0;
        PlayerInventory inv = playerInventory ?? PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            cores = inv.EvolutionCores;
        }
        UpdateEvolutionCoresDisplay(cores);
    }

    public void UpdateCreditsDisplay(int currentCredits)
    {
        string textStr = $"{currentCredits:N0}";
        if (creditCountTextTMP != null)
        {
            creditCountTextTMP.text = textStr;
        }
        if (creditCountTextLegacy != null)
        {
            creditCountTextLegacy.text = textStr;
        }
    }

    public void UpdateEvolutionCoresDisplay(int currentCores)
    {
        string textStr = $"x{currentCores}";
        if (coreCountTextTMP != null)
        {
            coreCountTextTMP.text = textStr;
        }
        if (coreCountTextLegacy != null)
        {
            coreCountTextLegacy.text = textStr;
        }
    }

    #endregion

    #region Layout Construction & Theming (1920x1080 Full HD Native)

    [ContextMenu("Apply Dark Sci-Fi Skin")]
    public void ApplyDarkSciFiSkin()
    {
        cachedFont = GetDefaultFont();
        cachedTMPFont = GetDefaultTMPFont();

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;

        EnsureCanvasScaler(canvas);
        FindExistingUIReferences(canvas);
        SkinHealthBar();
        SkinExpBar();
        SkinRoomTimerAndRoomText();
        BuildResourceHUDDisplay(canvas);
        BuildInGameHUDPanel(canvas);
        RefreshHUD();
    }

    private void EnsureCanvasScaler(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private void SkinHealthBar()
    {
        if (healthSlider == null) return;

        RectTransform barRect = healthSlider.GetComponent<RectTransform>();
        if (barRect != null)
        {
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(35f, -30f);
            barRect.sizeDelta = new Vector2(500f, 46f); // ขยายขนาดให้ใหญ่และชัดเจน
            barRect.localScale = Vector3.one;
        }

        if (healthSlider.handleRect != null)
        {
            healthSlider.handleRect.gameObject.SetActive(false);
        }

        Transform bgTrans = healthSlider.transform.Find("Background");
        if (bgTrans != null)
        {
            Image bgImg = bgTrans.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = DarkGreyBg;
                bgImg.sprite = null;
            }
        }

        Transform borderObj = healthSlider.transform.Find("DarkSciFi_Border");
        if (borderObj == null)
        {
            GameObject borderGo = new GameObject("DarkSciFi_Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(healthSlider.transform, false);
            borderGo.transform.SetAsFirstSibling();
            borderObj = borderGo.transform;
        }
        RectTransform borderRect = borderObj.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2f, -2f);
        borderRect.offsetMax = new Vector2(2f, 2f);
        Image borderImg = borderObj.GetComponent<Image>();
        borderImg.color = ThinBorderSlate;
        borderImg.raycastTarget = false;

        if (healthSlider.fillRect != null)
        {
            Image fillImage = healthSlider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                fillImage.color = NeonRedFill;
                fillImage.sprite = null;
            }
        }

        Transform textObj = healthSlider.transform.Find("HP_Text");
        if (textObj == null)
        {
            GameObject textGo = new GameObject("HP_Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            textGo.transform.SetParent(healthSlider.transform, false);
            textObj = textGo.transform;
        }
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        hpText = textObj.GetComponent<Text>();
        hpText.font = cachedFont ?? GetDefaultFont();
        hpText.fontSize = 28; // ตัวอักษรใหญ่หนา ชัดเจน
        hpText.fontStyle = FontStyle.Bold;
        hpText.alignment = TextAnchor.MiddleCenter;
        hpText.color = Color.white;
        hpText.raycastTarget = false;
        hpText.horizontalOverflow = HorizontalWrapMode.Overflow;
        hpText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void SkinExpBar()
    {
        if (expSlider == null) return;

        RectTransform expRect = expSlider.GetComponent<RectTransform>();
        if (expRect != null)
        {
            expRect.anchorMin = new Vector2(0f, 1f);
            expRect.anchorMax = new Vector2(0f, 1f);
            expRect.pivot = new Vector2(0f, 1f);
            expRect.anchoredPosition = new Vector2(35f, -82f); // อยู่ใต้ HP Bar
            expRect.sizeDelta = new Vector2(500f, 18f);        // แถบ EXP หนาขึ้น ชัดเจน
            expRect.localScale = Vector3.one;
        }

        if (expSlider.handleRect != null)
        {
            expSlider.handleRect.gameObject.SetActive(false);
        }

        Transform bgTrans = expSlider.transform.Find("Background");
        if (bgTrans != null)
        {
            Image bgImg = bgTrans.GetComponent<Image>();
            if (bgImg != null)
            {
                bgImg.color = DarkGreyBg;
                bgImg.sprite = null;
            }
        }

        if (expSlider.fillRect != null)
        {
            Image fillImg = expSlider.fillRect.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.color = NeonCyanFill;
                fillImg.sprite = null;
            }
        }

        if (levelText != null)
        {
            RectTransform lvlRect = levelText.GetComponent<RectTransform>();
            bool isChildOfExp = (lvlRect.parent == expSlider.transform);

            if (isChildOfExp)
            {
                // อยู่ทางขวาของแถบ EXP กึ่งกลางแนวตั้งพอดี
                lvlRect.anchorMin = new Vector2(1f, 0.5f);
                lvlRect.anchorMax = new Vector2(1f, 0.5f);
                lvlRect.pivot = new Vector2(0f, 0.5f);
                lvlRect.anchoredPosition = new Vector2(14f, 0f);
            }
            else
            {
                // ถ้าเป็นลูกของ Canvas โดยตรง
                lvlRect.anchorMin = new Vector2(0f, 1f);
                lvlRect.anchorMax = new Vector2(0f, 1f);
                lvlRect.pivot = new Vector2(0f, 0.5f);
                lvlRect.anchoredPosition = new Vector2(35f + 500f + 14f, -91f);
            }

            lvlRect.sizeDelta = new Vector2(160f, 36f);
            lvlRect.localScale = Vector3.one;

            levelText.font = cachedFont ?? GetDefaultFont();
            levelText.fontSize = 28; // Lv.X ตัวใหญ่ชัดเจน
            levelText.fontStyle = FontStyle.Bold;
            levelText.color = NeonCyanFill;
            levelText.alignment = TextAnchor.MiddleLeft;
        }

        if (expValueText != null)
        {
            RectTransform valRect = expValueText.GetComponent<RectTransform>();
            bool isChildOfExp = (valRect.parent == expSlider.transform);

            if (isChildOfExp)
            {
                // อยู่ใต้แถบ EXP ชิดขวาพอดีกับปลายแถบ
                valRect.anchorMin = new Vector2(0f, 0f);
                valRect.anchorMax = new Vector2(1f, 0f);
                valRect.pivot = new Vector2(1f, 1f);
                valRect.anchoredPosition = new Vector2(0f, -4f);
                valRect.sizeDelta = new Vector2(0f, 22f);
            }
            else
            {
                // ถ้าเป็นลูกของ Canvas โดยตรง
                valRect.anchorMin = new Vector2(0f, 1f);
                valRect.anchorMax = new Vector2(0f, 1f);
                valRect.pivot = new Vector2(1f, 1f);
                valRect.anchoredPosition = new Vector2(35f + 500f, -104f);
                valRect.sizeDelta = new Vector2(500f, 22f);
            }

            valRect.localScale = Vector3.one;

            expValueText.font = cachedFont ?? GetDefaultFont();
            expValueText.fontSize = 16;
            expValueText.fontStyle = FontStyle.Bold;
            expValueText.color = new Color(0.65f, 0.9f, 1f, 0.95f);
            expValueText.alignment = TextAnchor.UpperRight;
        }
    }

    private void SkinRoomTimerAndRoomText()
    {
        if (timerText != null)
        {
            RectTransform tRect = timerText.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 1f);
            tRect.anchorMax = new Vector2(0.5f, 1f);
            tRect.pivot = new Vector2(0.5f, 1f);
            tRect.anchoredPosition = new Vector2(0f, -15f);
            tRect.sizeDelta = new Vector2(800f, 75f);

            timerText.font = cachedFont ?? GetDefaultFont();
            timerText.fontSize = 60; // เวลานับถอยหลังตัวใหญ่สะใจ
            timerText.fontStyle = FontStyle.Bold;
            timerText.color = Color.white;
            timerText.alignment = TextAnchor.MiddleCenter;
        }

        if (roomText != null)
        {
            RectTransform rRect = roomText.GetComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.5f, 1f);
            rRect.anchorMax = new Vector2(0.5f, 1f);
            rRect.pivot = new Vector2(0.5f, 1f);
            rRect.anchoredPosition = new Vector2(0f, -92f);
            rRect.sizeDelta = new Vector2(700f, 40f);

            roomText.font = cachedFont ?? GetDefaultFont();
            roomText.fontSize = 28; // ข้อความบอกชั้น/ห้องใหญ่ขึ้น
            roomText.fontStyle = FontStyle.Bold;
            roomText.color = WhiteCyanText;
            roomText.alignment = TextAnchor.MiddleCenter;
        }
    }

    #endregion

    #region Resource HUD Display (Top-Right)

    private void BuildResourceHUDDisplay(Canvas canvas)
    {
        Transform resObj = canvas.transform.Find("HUD_ResourceDisplay");
        if (resObj == null)
        {
            GameObject resGo = new GameObject("HUD_ResourceDisplay", typeof(RectTransform));
            resGo.transform.SetParent(canvas.transform, false);
            resObj = resGo.transform;
        }

        resourceRoot = resObj.GetComponent<RectTransform>();
        resourceRoot.anchorMin = new Vector2(1f, 1f);
        resourceRoot.anchorMax = new Vector2(1f, 1f);
        resourceRoot.pivot = new Vector2(1f, 1f);
        resourceRoot.anchoredPosition = new Vector2(-35f, -30f);
        resourceRoot.sizeDelta = new Vector2(460f, 52f); // ขยายแถบแสดงทรัพยากรให้กว้างและสูงขึ้น
        resourceRoot.localScale = Vector3.one;

        // 1. Evolution Core Counter (Left Badge)
        Transform coreBox = resourceRoot.Find("CoreBadge");
        if (coreBox == null)
        {
            GameObject go = new GameObject("CoreBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(resourceRoot, false);
            coreBox = go.transform;
        }
        RectTransform coreRect = coreBox.GetComponent<RectTransform>();
        coreRect.anchorMin = new Vector2(0f, 0.5f);
        coreRect.anchorMax = new Vector2(0f, 0.5f);
        coreRect.pivot = new Vector2(0f, 0.5f);
        coreRect.anchoredPosition = new Vector2(0f, 0f);
        coreRect.sizeDelta = new Vector2(195f, 48f); // ขยายกล่อง Core

        Image coreBoxBg = coreBox.GetComponent<Image>();
        coreBoxBg.color = DarkGreyBg;

        // Clean up any old solid "Border" child that covered the background
        Transform oldCoreBorder = coreBox.Find("Border");
        if (oldCoreBorder != null)
        {
            if (Application.isPlaying) Destroy(oldCoreBorder.gameObject);
            else DestroyImmediate(oldCoreBorder.gameObject);
        }

        Outline coreOutline = coreBox.GetComponent<Outline>();
        if (coreOutline == null) coreOutline = coreBox.gameObject.AddComponent<Outline>();
        coreOutline.effectColor = RadiantGoldEvo;
        coreOutline.effectDistance = new Vector2(2f, -2f);

        Transform coreIconTr = coreBox.Find("CoreIcon");
        if (coreIconTr == null)
        {
            GameObject iGo = new GameObject("CoreIcon", typeof(RectTransform), typeof(Image));
            iGo.transform.SetParent(coreBox, false);
            coreIconTr = iGo.transform;
        }
        RectTransform coreIconRt = coreIconTr.GetComponent<RectTransform>();
        coreIconRt.anchorMin = new Vector2(0f, 0.5f);
        coreIconRt.anchorMax = new Vector2(0f, 0.5f);
        coreIconRt.pivot = new Vector2(0f, 0.5f);
        coreIconRt.anchoredPosition = new Vector2(10f, 0f);
        coreIconRt.sizeDelta = new Vector2(32f, 32f); // ไอคอน Core ใหญ่ขึ้น

        coreIconImage = coreIconTr.GetComponent<Image>();
        coreIconImage.sprite = ProceduralSprites.Crystal;
        coreIconImage.color = RadiantGoldEvo;
        coreIconImage.preserveAspect = true;
        coreIconImage.raycastTarget = false;

        Transform coreTextTr = coreBox.Find("CoreText");
        if (coreTextTr == null)
        {
            GameObject tGo = new GameObject("CoreText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(coreBox, false);
            coreTextTr = tGo.transform;
        }
        RectTransform coreTextRt = coreTextTr.GetComponent<RectTransform>();
        coreTextRt.anchorMin = Vector2.zero;
        coreTextRt.anchorMax = Vector2.one;
        coreTextRt.offsetMin = new Vector2(48f, 0f);
        coreTextRt.offsetMax = new Vector2(-8f, 0f);

        coreCountTextTMP = coreTextTr.GetComponent<TextMeshProUGUI>();
        if (coreCountTextTMP != null)
        {
            if (cachedTMPFont != null) coreCountTextTMP.font = cachedTMPFont;
            coreCountTextTMP.fontSize = 24; // ตัวเลข Core ใหญ่ชัดเจน (Font 24 Bold)
            coreCountTextTMP.fontStyle = FontStyles.Bold;
            coreCountTextTMP.color = RadiantGoldEvo;
            coreCountTextTMP.alignment = TextAlignmentOptions.MidlineLeft;
            coreCountTextTMP.text = "x0";
            coreCountTextTMP.raycastTarget = false;
        }

        // 2. Nano-Credits Counter (Right Badge)
        Transform creditBox = resourceRoot.Find("CreditBadge");
        if (creditBox == null)
        {
            GameObject go = new GameObject("CreditBadge", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(resourceRoot, false);
            creditBox = go.transform;
        }
        RectTransform creditRect = creditBox.GetComponent<RectTransform>();
        creditRect.anchorMin = new Vector2(1f, 0.5f);
        creditRect.anchorMax = new Vector2(1f, 0.5f);
        creditRect.pivot = new Vector2(1f, 0.5f);
        creditRect.anchoredPosition = new Vector2(0f, 0f);
        creditRect.sizeDelta = new Vector2(250f, 48f); // ขยายกล่อง Credit

        Image creditBoxBg = creditBox.GetComponent<Image>();
        creditBoxBg.color = DarkGreyBg;

        // Clean up any old solid "Border" child that covered the background
        Transform oldCreditBorder = creditBox.Find("Border");
        if (oldCreditBorder != null)
        {
            if (Application.isPlaying) Destroy(oldCreditBorder.gameObject);
            else DestroyImmediate(oldCreditBorder.gameObject);
        }

        Outline creditOutline = creditBox.GetComponent<Outline>();
        if (creditOutline == null) creditOutline = creditBox.gameObject.AddComponent<Outline>();
        creditOutline.effectColor = NeonCyanBorder;
        creditOutline.effectDistance = new Vector2(2f, -2f);

        Transform creditIconTr = creditBox.Find("CreditIcon");
        if (creditIconTr == null)
        {
            GameObject iGo = new GameObject("CreditIcon", typeof(RectTransform), typeof(Image));
            iGo.transform.SetParent(creditBox, false);
            creditIconTr = iGo.transform;
        }
        RectTransform creditIconRt = creditIconTr.GetComponent<RectTransform>();
        creditIconRt.anchorMin = new Vector2(0f, 0.5f);
        creditIconRt.anchorMax = new Vector2(0f, 0.5f);
        creditIconRt.pivot = new Vector2(0f, 0.5f);
        creditIconRt.anchoredPosition = new Vector2(10f, 0f);
        creditIconRt.sizeDelta = new Vector2(30f, 30f); // ไอคอน Credit ใหญ่ขึ้น

        creditIconImage = creditIconTr.GetComponent<Image>();
        creditIconImage.sprite = ProceduralSprites.CircleDisc;
        creditIconImage.color = GoldCredits;
        creditIconImage.preserveAspect = true;
        creditIconImage.raycastTarget = false;

        Transform creditTextTr = creditBox.Find("CreditText");
        if (creditTextTr == null)
        {
            GameObject tGo = new GameObject("CreditText", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(creditBox, false);
            creditTextTr = tGo.transform;
        }
        RectTransform creditTextRt = creditTextTr.GetComponent<RectTransform>();
        creditTextRt.anchorMin = Vector2.zero;
        creditTextRt.anchorMax = Vector2.one;
        creditTextRt.offsetMin = new Vector2(48f, 0f);
        creditTextRt.offsetMax = new Vector2(-8f, 0f);

        creditCountTextTMP = creditTextTr.GetComponent<TextMeshProUGUI>();
        if (creditCountTextTMP != null)
        {
            if (cachedTMPFont != null) creditCountTextTMP.font = cachedTMPFont;
            creditCountTextTMP.fontSize = 24; // ตัวเลข Credits ใหญ่ชัดเจน (Font 24 Bold)
            creditCountTextTMP.fontStyle = FontStyles.Bold;
            creditCountTextTMP.color = GoldCredits;
            creditCountTextTMP.alignment = TextAlignmentOptions.MidlineLeft;
            creditCountTextTMP.text = "0";
            creditCountTextTMP.raycastTarget = false;
        }
    }

    #endregion

    #region InGameHUDPanel - Item Slots (Bottom-Left)

    private void BuildInGameHUDPanel(Canvas canvas)
    {
        // Clean up legacy container name if present
        Transform legacyContainer = canvas.transform.Find("HUD_InventoryContainer");
        if (legacyContainer != null)
        {
            legacyContainer.gameObject.name = "InGameHUDPanel";
        }

        Transform root = canvas.transform.Find("InGameHUDPanel");
        if (root == null)
        {
            GameObject rootGo = new GameObject("InGameHUDPanel", typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            root = rootGo.transform;
        }

        inGameHUDPanel = root.GetComponent<RectTransform>();
        inGameHUDPanel.anchorMin = new Vector2(0f, 0f);
        inGameHUDPanel.anchorMax = new Vector2(0f, 0f);
        inGameHUDPanel.pivot = new Vector2(0f, 0f);
        inGameHUDPanel.anchoredPosition = new Vector2(35f, 30f);
        inGameHUDPanel.sizeDelta = new Vector2(310f, 350f); // ขยาย Container สำหรับสล็อตขนาดใหญ่
        inGameHUDPanel.localScale = Vector3.one;

        weaponSlots.Clear();
        techSlots.Clear();
        boonSlots.Clear();

        // 1. Weapon Slots Row (Top Row of the cluster, y = 224f)
        Transform weaponRow = inGameHUDPanel.Find("WeaponsRow");
        if (weaponRow == null)
        {
            GameObject rowGo = new GameObject("WeaponsRow", typeof(RectTransform));
            rowGo.transform.SetParent(inGameHUDPanel, false);
            weaponRow = rowGo.transform;
        }
        RectTransform wRowRect = weaponRow.GetComponent<RectTransform>();
        wRowRect.anchorMin = new Vector2(0f, 0f);
        wRowRect.anchorMax = new Vector2(0f, 0f);
        wRowRect.pivot = new Vector2(0f, 0f);
        wRowRect.anchoredPosition = new Vector2(0f, 224f);
        wRowRect.sizeDelta = new Vector2(280f, 106f);

        CreateSectionHeader(weaponRow, "WEAPONS", new Vector2(0f, 82f), NeonCyanBorder);
        for (int i = 0; i < 3; i++)
        {
            HUDSlotUI slot = BuildOrGetSlot(weaponRow, $"WeaponSlot_{i}", i, HUDSlotUI.SlotType.Weapon);
            weaponSlots.Add(slot);
        }

        // 2. Tech Passive Slots Row (Middle Row of the cluster, y = 112f)
        Transform techRow = inGameHUDPanel.Find("TechsRow");
        if (techRow == null)
        {
            GameObject rowGo = new GameObject("TechsRow", typeof(RectTransform));
            rowGo.transform.SetParent(inGameHUDPanel, false);
            techRow = rowGo.transform;
        }
        RectTransform tRowRect = techRow.GetComponent<RectTransform>();
        tRowRect.anchorMin = new Vector2(0f, 0f);
        tRowRect.anchorMax = new Vector2(0f, 0f);
        tRowRect.pivot = new Vector2(0f, 0f);
        tRowRect.anchoredPosition = new Vector2(0f, 112f);
        tRowRect.sizeDelta = new Vector2(280f, 106f);

        CreateSectionHeader(techRow, "TECH PASSIVES", new Vector2(0f, 82f), EmeraldTechBorder);
        for (int i = 0; i < 3; i++)
        {
            HUDSlotUI slot = BuildOrGetSlot(techRow, $"TechSlot_{i}", i, HUDSlotUI.SlotType.TechPassive);
            techSlots.Add(slot);
        }

        // 3. Boon Slots Row (Bottom Row of the cluster, y = 0f)
        Transform boonRow = inGameHUDPanel.Find("BoonsRow");
        if (boonRow == null)
        {
            GameObject rowGo = new GameObject("BoonsRow", typeof(RectTransform));
            rowGo.transform.SetParent(inGameHUDPanel, false);
            boonRow = rowGo.transform;
        }
        RectTransform bRowRect = boonRow.GetComponent<RectTransform>();
        bRowRect.anchorMin = new Vector2(0f, 0f);
        bRowRect.anchorMax = new Vector2(0f, 0f);
        bRowRect.pivot = new Vector2(0f, 0f);
        bRowRect.anchoredPosition = new Vector2(0f, 0f);
        bRowRect.sizeDelta = new Vector2(280f, 106f);

        CreateSectionHeader(boonRow, "RESEARCH BOONS", new Vector2(0f, 82f), PurpleBoonBorder);
        for (int i = 0; i < 3; i++)
        {
            HUDSlotUI slot = BuildOrGetSlot(boonRow, $"BoonSlot_{i}", i, HUDSlotUI.SlotType.Boon);
            boonSlots.Add(slot);
        }
    }

    private void CreateSectionHeader(Transform parent, string title, Vector2 pos, Color color)
    {
        Transform labelTr = parent.Find("HeaderLabel");
        if (labelTr == null)
        {
            GameObject go = new GameObject("HeaderLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            labelTr = go.transform;
        }
        RectTransform rt = labelTr.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(240f, 22f);

        TextMeshProUGUI tmp = labelTr.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            if (cachedTMPFont != null) tmp.font = cachedTMPFont;
            tmp.fontSize = 16; // หัวข้อใหญ่ขึ้น ชัดเจน
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = color;
            tmp.text = title;
            tmp.alignment = TextAlignmentOptions.BottomLeft;
            tmp.raycastTarget = false;
        }
    }

    private HUDSlotUI BuildOrGetSlot(Transform parent, string slotName, int index, HUDSlotUI.SlotType slotType)
    {
        float slotSize = 76f; // ขยายขนาดสล็อตจาก 52f เป็น 76f (~46% ใหญ่ขึ้นมาก)
        float spacing = 12f;
        float xPos = index * (slotSize + spacing);

        Transform slotTr = parent.Find(slotName);
        if (slotTr == null)
        {
            GameObject go = new GameObject(slotName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            slotTr = go.transform;
        }

        RectTransform slotRt = slotTr.GetComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0f, 0f);
        slotRt.anchorMax = new Vector2(0f, 0f);
        slotRt.pivot = new Vector2(0f, 0f);
        slotRt.anchoredPosition = new Vector2(xPos, 0f);
        slotRt.sizeDelta = new Vector2(slotSize, slotSize);

        // 1. Outer Thin Border
        Image borderImage = slotTr.GetComponent<Image>();
        if (borderImage == null) borderImage = slotTr.gameObject.AddComponent<Image>();
        borderImage.color = ThinBorderSlate;
        borderImage.raycastTarget = false;

        // 2. Inner Carbon Fiber Background
        Transform bgTr = slotTr.Find("Bg");
        if (bgTr == null)
        {
            GameObject bgGo = new GameObject("Bg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(slotTr, false);
            bgTr = bgGo.transform;
        }
        RectTransform bgRt = bgTr.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(2f, 2f);
        bgRt.offsetMax = new Vector2(-2f, -2f);

        Image bgImage = bgTr.GetComponent<Image>();
        bgImage.color = DarkGreyBg;
        bgImage.raycastTarget = false;

        // 3. Item Sprite Icon
        Transform iconTr = bgTr.Find("Icon");
        if (iconTr == null)
        {
            GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(bgTr, false);
            iconTr = iconGo.transform;
        }
        iconTr.SetSiblingIndex(0);
        RectTransform iconRt = iconTr.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero;
        iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = new Vector2(4f, 4f);
        iconRt.offsetMax = new Vector2(-4f, -4f);

        Image iconImg = iconTr.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;
        iconImg.gameObject.SetActive(false);

        // 4. Resonance Indicator (Outer Glowing Shimmer Frame)
        Transform resTr = slotTr.Find("ResonanceIndicator");
        if (resTr == null)
        {
            GameObject resGo = new GameObject("ResonanceIndicator", typeof(RectTransform), typeof(Image));
            resGo.transform.SetParent(slotTr, false);
            resTr = resGo.transform;
        }
        RectTransform resRt = resTr.GetComponent<RectTransform>();
        resRt.anchorMin = Vector2.zero;
        resRt.anchorMax = Vector2.one;
        resRt.offsetMin = new Vector2(-4f, -4f);
        resRt.offsetMax = new Vector2(4f, 4f);

        Image resGlowImg = resTr.GetComponent<Image>();
        resGlowImg.color = RadiantGoldEvo;
        resGlowImg.raycastTarget = false;
        resTr.gameObject.SetActive(false);

        // 5. Fallback Name Text
        Transform nameTr = bgTr.Find("NameText");
        if (nameTr == null)
        {
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameGo.transform.SetParent(bgTr, false);
            nameTr = nameGo.transform;
        }
        RectTransform nameRt = nameTr.GetComponent<RectTransform>();
        nameRt.anchorMin = Vector2.zero;
        nameRt.anchorMax = Vector2.one;
        nameRt.offsetMin = new Vector2(3f, 18f);
        nameRt.offsetMax = new Vector2(-3f, -3f);

        TextMeshProUGUI nameTMP = nameTr.GetComponent<TextMeshProUGUI>();
        if (nameTMP != null)
        {
            if (cachedTMPFont != null) nameTMP.font = cachedTMPFont;
            nameTMP.fontSize = 13; // ข้อความย่อใหญ่ขึ้น
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.color = new Color(0.35f, 0.42f, 0.52f, 0.6f);
            nameTMP.text = "[ - ]";
            nameTMP.raycastTarget = false;
        }

        // 6. Level Badge Text (Bottom-Right, TextMeshPro)
        Transform lvlTr = bgTr.Find("LevelText");
        if (lvlTr == null)
        {
            GameObject lvlGo = new GameObject("LevelText", typeof(RectTransform), typeof(TextMeshProUGUI));
            lvlGo.transform.SetParent(bgTr, false);
            lvlTr = lvlGo.transform;
        }
        RectTransform lvlRt = lvlTr.GetComponent<RectTransform>();
        lvlRt.anchorMin = new Vector2(0f, 0f);
        lvlRt.anchorMax = new Vector2(1f, 0f);
        lvlRt.pivot = new Vector2(0.5f, 0f);
        lvlRt.anchoredPosition = new Vector2(-3f, 3f);
        lvlRt.sizeDelta = new Vector2(0f, 22f);

        TextMeshProUGUI lvlTMP = lvlTr.GetComponent<TextMeshProUGUI>();
        if (lvlTMP != null)
        {
            if (cachedTMPFont != null) lvlTMP.font = cachedTMPFont;
            lvlTMP.fontSize = 16; // ป้ายเลเวล "Lv.5" / "MAX" / "★EVO" ใหญ่และเด่นชัดเจน
            lvlTMP.fontStyle = FontStyles.Bold;
            lvlTMP.alignment = TextAlignmentOptions.BottomRight;
            lvlTMP.color = Color.white;
            lvlTMP.text = "";
            lvlTMP.raycastTarget = false;
        }

        // HUDSlotUI Component
        HUDSlotUI slotComponent = slotTr.GetComponent<HUDSlotUI>();
        if (slotComponent == null) slotComponent = slotTr.gameObject.AddComponent<HUDSlotUI>();
        slotComponent.SetupSlot(slotType, index);
        slotComponent.SetReferences(borderImage, bgImage, iconImg, lvlTMP, null, nameTMP, null, resTr.gameObject, resGlowImg);

        return slotComponent;
    }

    #endregion

    #region Helper Short Names & Fonts

    private static string GetWeaponShortName(WeaponType type, bool isEvolved, EvolvedWeaponType evolvedType)
    {
        if (isEvolved)
        {
            return evolvedType switch
            {
                EvolvedWeaponType.ExecutionerMatrix => "MATRIX",
                EvolvedWeaponType.PlagueSporeSpreader => "PLAGUE",
                EvolvedWeaponType.SupernovaCannon => "S-NOVA",
                EvolvedWeaponType.StormCitadel => "STORM",
                EvolvedWeaponType.SingularityCannon => "SINGUL",
                _ => "EVO"
            };
        }

        return type switch
        {
            WeaponType.NanoBlade => "BLADE",
            WeaponType.BioNeedler => "NEEDLE",
            WeaponType.PlasmaBlaster => "PLASMA",
            WeaponType.TeslaOrb => "TESLA",
            WeaponType.GravityGrenadier => "GRAV",
            _ => type.ToString()
        };
    }

    private static string GetTechShortName(TechPassiveType type)
    {
        return type switch
        {
            TechPassiveType.TargetingLens => "LENS",
            TechPassiveType.GeneticExtractor => "EXTRACT",
            TechPassiveType.OverclockChip => "O-CLOCK",
            TechPassiveType.BioFuelTank => "BIO-TANK",
            TechPassiveType.GravitonCapacitor => "GRAV-CAP",
            _ => type.ToString()
        };
    }

    private static string GetBoonShortName(BoonItemType type)
    {
        return type switch
        {
            BoonItemType.NanoBarrier => "BARRIER",
            BoonItemType.ToxicSpore => "SPORE",
            BoonItemType.FlameRing => "FLAME",
            BoonItemType.StaticAura => "AURA",
            BoonItemType.MagneticPulse => "PULSE",
            _ => type.ToString()
        };
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

    public static TMP_FontAsset GetDefaultTMPFont()
    {
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) return font;

        TMP_FontAsset[] allTMPFonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
        if (allTMPFonts != null && allTMPFonts.Length > 0)
        {
            foreach (var f in allTMPFonts)
            {
                if (f != null) return f;
            }
        }

        return null;
    }

    #endregion

    #region Editor Menu Integration

#if UNITY_EDITOR
    [MenuItem("Tools/HUD/Apply Dark Sci-Fi In-Game HUD in GameScene")]
    public static void MenuApplyDarkSciFiSkinInScene()
    {
        string scenePath = "Assets/Scenes/GameScene.unity";
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        Canvas canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            Debug.LogWarning("[InGameHUDController] No Canvas found in GameScene.");
            return;
        }

        InGameHUDController controller = canvas.GetComponent<InGameHUDController>();
        if (controller == null)
        {
            controller = canvas.gameObject.AddComponent<InGameHUDController>();
        }

        controller.ApplyDarkSciFiSkin();
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();
        Debug.Log("[InGameHUDController] Successfully configured and saved In-Game HUD Slots & Resource Display in GameScene.unity!");
    }
#endif

    #endregion
}
