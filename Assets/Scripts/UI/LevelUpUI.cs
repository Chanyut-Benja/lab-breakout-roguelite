using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controller for Level Up UI.
/// Manages Weapon & Tech Passive selection cards when the player levels up.
/// Guarantees that card buttons are responsive, raycasts are not blocked,
/// and handles Reroll button integration with PlayerInventory / Black Market.
/// </summary>
public class LevelUpUI : MonoBehaviour
{
    public static LevelUpUI Instance { get; private set; }

    [Header("Panel Reference")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private TMP_Text tmpTitleText;

    public GameObject LevelUpPanel => levelUpPanel;
    public bool IsOpen => (levelUpPanel != null && levelUpPanel.activeInHierarchy) || gameObject.activeInHierarchy;

    [Header("Card Buttons (3 Cards)")]
    [SerializeField] private Button[] cardButtons;

    [Header("Reroll Controls")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private TextMeshProUGUI rerollText;
    [SerializeField] private Text legacyRerollText;

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

        ResolveReferences();
        SetupRerollButton();
    }

    private void OnEnable()
    {
        ResolveReferences();
        RefreshUI();
    }

    public void ResolveReferences()
    {
        if (levelUpPanel == null)
        {
            levelUpPanel = gameObject;
        }

        if (cardButtons == null || cardButtons.Length == 0)
        {
            List<Button> buttons = new List<Button>();
            foreach (Button b in GetComponentsInChildren<Button>(true))
            {
                string bName = b.name.ToLowerInvariant();
                if (!bName.Contains("reroll") && !bName.Contains("banish") && !bName.Contains("close") && !bName.Contains("exit"))
                {
                    buttons.Add(b);
                }
            }
            if (buttons.Count >= 3)
            {
                cardButtons = buttons.GetRange(0, 3).ToArray();
            }
        }

        EnsureRerollButtonExists();
    }

    private void EnsureRerollButtonExists()
    {
        if (rerollButton == null)
        {
            Transform panelTr = levelUpPanel != null ? levelUpPanel.transform : transform;
            foreach (Button b in panelTr.GetComponentsInChildren<Button>(true))
            {
                if (b.name.ToLowerInvariant().Contains("reroll"))
                {
                    rerollButton = b;
                    break;
                }
            }
        }

        if (rerollButton != null)
        {
            if (rerollText == null)
            {
                rerollText = rerollButton.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (legacyRerollText == null)
            {
                legacyRerollText = rerollButton.GetComponentInChildren<Text>(true);
            }
        }
        else
        {
            // Dynamically create a Cyberpunk-styled Reroll button if not yet configured in scene
            Transform panelTr = levelUpPanel != null ? levelUpPanel.transform : transform;
            if (panelTr != null)
            {
                GameObject btnGo = new GameObject("RerollButton", typeof(RectTransform), typeof(Image), typeof(Button));
                btnGo.transform.SetParent(panelTr, false);

                RectTransform rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -280f);
                rt.sizeDelta = new Vector2(220f, 48f);

                Image img = btnGo.GetComponent<Image>();
                img.color = new Color32(0x9D, 0x4E, 0xDD, 0xFF); // Cyberpunk Neon Purple

                rerollButton = btnGo.GetComponent<Button>();

                GameObject textGo = new GameObject("RerollText", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(btnGo.transform, false);

                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = Vector2.zero;
                textRt.offsetMax = Vector2.zero;

                rerollText = textGo.GetComponent<TextMeshProUGUI>();
                rerollText.text = "REROLL (0)";
                rerollText.fontSize = 20;
                rerollText.fontStyle = FontStyles.Bold;
                rerollText.alignment = TextAlignmentOptions.Center;
                rerollText.color = Color.white;
                rerollText.raycastTarget = false;
            }
        }
    }

    private void SetupRerollButton()
    {
        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(OnRerollButtonClicked);
        }
    }

    public void Open()
    {
        OpenUI();
    }

    public void OpenUI()
    {
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
        }
        else
        {
            gameObject.SetActive(true);
        }
        RefreshUI();
    }

    public void RefreshUI()
    {
        ResolveReferences();
        SetupRerollButton();

        int rerollCount = GetCurrentRerollCount();

        if (rerollButton != null)
        {
            if (rerollCount > 0)
            {
                rerollButton.gameObject.SetActive(true);
                rerollButton.interactable = true;
            }
            else
            {
                rerollButton.interactable = false;
            }
        }

        string displayText = "REROLL (" + rerollCount + ")";
        if (rerollText != null)
        {
            rerollText.text = displayText;
        }
        if (legacyRerollText != null)
        {
            legacyRerollText.text = displayText;
        }

        if (cardButtons != null)
        {
            for (int i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] != null)
                {
                    LevelUpCardUI cardUI = cardButtons[i].GetComponent<LevelUpCardUI>();
                    if (cardUI != null)
                    {
                        cardUI.UpdateBanishDisplay();
                    }
                }
            }
        }
    }

    public int GetCurrentBanishCount()
    {
        if (PlayerInventory.Instance != null)
            return PlayerInventory.Instance.BanishCount;

        var inv = PlayerInventory.GetOrCreate();
        if (inv != null)
            return inv.BanishCount;

        if (CurrencyManager.Instance != null)
            return CurrencyManager.Instance.BanishCount;

        return 0;
    }

    public void ReplaceCardAt(int index)
    {
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ReplaceCardAt(index);
        }
    }

    private int GetCurrentRerollCount()
    {
        if (PlayerInventory.Instance != null)
            return PlayerInventory.Instance.RerollCount;

        var inv = PlayerInventory.GetOrCreate();
        if (inv != null)
            return inv.RerollCount;

        if (CurrencyManager.Instance != null)
            return CurrencyManager.Instance.RerollCount;

        return 0;
    }

    private void OnRerollButtonClicked()
    {
        bool used = false;
        if (PlayerInventory.Instance != null)
        {
            used = PlayerInventory.Instance.TryUseReroll();
        }
        else if (PlayerInventory.GetOrCreate() != null)
        {
            used = PlayerInventory.GetOrCreate().TryUseReroll();
        }
        else if (CurrencyManager.Instance != null)
        {
            used = CurrencyManager.Instance.TryUseReroll();
        }

        if (used)
        {
            RerollChoices();
            RefreshUI();
        }
        else
        {
            Debug.LogWarning("[LevelUpUI] Reroll failed: RerollCount is 0.");
            RefreshUI();
        }
    }

    public void RerollChoices()
    {
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.RerollCards();
        }
        else
        {
            PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
            if (inv != null)
            {
                List<LevelUpUpgradeData> newCards = inv.GenerateWeightedLevelUpCards(3);
                for (int i = 0; i < newCards.Count && i < cardButtons.Length; i++)
                {
                    BindCardButton(i, newCards[i], null);
                }
            }
        }
    }

    /// <summary>
    /// Binds card buttons and ensures listeners and raycasts are correctly configured.
    /// </summary>
    public void BindCardButton(int index, LevelUpUpgradeData data, System.Action onSelected)
    {
        if (cardButtons == null || index < 0 || index >= cardButtons.Length || cardButtons[index] == null) return;

        Button btn = cardButtons[index];
        btn.interactable = true;

        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.raycastTarget = true;
        }

        // Disable raycastTarget on all child graphics so they do not block mouse clicks
        foreach (Graphic g in btn.GetComponentsInChildren<Graphic>(true))
        {
            if (g.gameObject != btn.gameObject)
            {
                g.raycastTarget = false;
            }
        }

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            OnCardSelected(data);
            onSelected?.Invoke();
        });

        LevelUpCardUI cardUI = btn.GetComponent<LevelUpCardUI>();
        if (cardUI == null) cardUI = btn.gameObject.AddComponent<LevelUpCardUI>();
        cardUI.Setup(index, data, (idx) =>
        {
            OnCardSelected(data);
            onSelected?.Invoke();
        }, (idx) => ReplaceCardAt(idx));
    }

    public void OnCardSelected(LevelUpUpgradeData upgradeData)
    {
        if (upgradeData == null) return;

        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ApplyChosenUpgrade(upgradeData);
        }
    }

    public void OnCardSelected(int index)
    {
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.SelectUpgrade(index);
        }
    }
}

/// <summary>
/// Controller สำหรับการ์ดอัปเกรดแต่ละใบในหน้าต่าง Level Up / Boon Selection
/// มีปุ่ม Banish ขนาดเล็ก (banishButton) ประจำแต่ละการ์ด แสดงเฉพาะเมื่อ BanishCount > 0
/// เมื่อกดจะตัดการ์ดนั้นทิ้งตลอดรัน และสุ่มการ์ดใหม่มาแทนที่ทันที
/// </summary>
public class LevelUpCardUI : MonoBehaviour
{
    [Header("Card Index & Data")]
    [SerializeField] private int cardIndex = 0;
    [SerializeField] private LevelUpUpgradeData cardData;

    [Header("UI Elements")]
    [SerializeField] private Button cardButton;
    [SerializeField] private Button banishButton;
    [SerializeField] private Text banishText;
    [SerializeField] private TMP_Text tmpBanishText;

    private System.Action<int> onCardSelected;
    private System.Action<int> onCardBanished;

    public int CardIndex => cardIndex;
    public LevelUpUpgradeData CardData => cardData;
    public Button BanishButton => banishButton;

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        UpdateBanishDisplay();
    }

    public void Setup(int index, LevelUpUpgradeData data, System.Action<int> onSelected, System.Action<int> onBanished)
    {
        cardIndex = index;
        cardData = data;
        onCardSelected = onSelected;
        onCardBanished = onBanished;

        EnsureReferences();
        SetupCardButton();
        SetupBanishButton();
        UpdateBanishDisplay();
    }

    public void EnsureReferences()
    {
        if (cardButton == null)
        {
            cardButton = GetComponent<Button>();
        }

        if (banishButton == null)
        {
            Transform bTr = transform.Find("BanishButton");
            if (bTr != null)
            {
                banishButton = bTr.GetComponent<Button>();
            }
        }

        if (banishButton == null)
        {
            CreateDynamicBanishButton();
        }
        else
        {
            if (banishText == null)
            {
                banishText = banishButton.GetComponentInChildren<Text>(true);
            }
            if (tmpBanishText == null)
            {
                tmpBanishText = banishButton.GetComponentInChildren<TMP_Text>(true);
            }
        }
    }

    private void CreateDynamicBanishButton()
    {
        GameObject btnGo = new GameObject("BanishButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(transform, false);
        btnGo.transform.SetAsLastSibling();

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-10f, -10f);
        rt.sizeDelta = new Vector2(105f, 30f);

        Image img = btnGo.GetComponent<Image>();
        img.color = new Color32(0xE6, 0x1E, 0x4D, 0xFF); // Cyberpunk Neon Crimson
        img.raycastTarget = true;

        banishButton = btnGo.GetComponent<Button>();
        var colors = banishButton.colors;
        colors.normalColor = new Color32(0xE6, 0x1E, 0x4D, 0xFF);
        colors.highlightedColor = new Color32(0xFF, 0x47, 0x7E, 0xFF);
        colors.pressedColor = new Color32(0x99, 0x0F, 0x2E, 0xFF);
        colors.disabledColor = new Color32(0x40, 0x10, 0x1C, 0xAA);
        banishButton.colors = colors;

        GameObject textGo = new GameObject("BanishText", typeof(RectTransform), typeof(Text), typeof(Shadow));
        textGo.transform.SetParent(btnGo.transform, false);

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        banishText = textGo.GetComponent<Text>();
        banishText.font = InGameHUDController.GetDefaultFont();
        banishText.fontSize = 12;
        banishText.fontStyle = FontStyle.Bold;
        banishText.alignment = TextAnchor.MiddleCenter;
        banishText.color = Color.white;
        banishText.raycastTarget = false;
        banishText.text = "BANISH";

        Shadow s = textGo.GetComponent<Shadow>();
        s.effectColor = new Color(0f, 0f, 0f, 0.9f);
        s.effectDistance = new Vector2(1f, -1f);
    }

    private void SetupCardButton()
    {
        if (cardButton != null)
        {
            cardButton.onClick.RemoveAllListeners();
            cardButton.onClick.AddListener(OnCardButtonClicked);
        }
    }

    private void SetupBanishButton()
    {
        if (banishButton != null)
        {
            banishButton.onClick.RemoveAllListeners();
            banishButton.onClick.AddListener(OnBanishButtonClicked);
        }
    }

    private void OnCardButtonClicked()
    {
        if (cardData == null) return;
        onCardSelected?.Invoke(cardIndex);
    }

    /// <summary>
    /// เมื่อกดปุ่ม Banish บนการ์ด:
    /// 1. เรียก TryUseBanish()
    /// 2. เรียก UpgradeManager.Instance.BanishItem(cardData.Id)
    /// 3. สุ่มไอเทมใหม่ที่ไม่ซ้ำและไม่อยู่ใน Banish List มาแทนที่การ์ดใบนั้นทันที
    /// </summary>
    public void OnBanishButtonClicked()
    {
        if (cardData == null) return;

        int banishCount = GetCurrentBanishCount();
        if (banishCount <= 0) return;

        // 1. เรียก TryUseBanish()
        bool used = false;
        if (PlayerInventory.Instance != null)
        {
            used = PlayerInventory.Instance.TryUseBanish();
        }
        else if (PlayerInventory.GetOrCreate() != null)
        {
            used = PlayerInventory.GetOrCreate().TryUseBanish();
        }
        else if (CurrencyManager.Instance != null)
        {
            used = CurrencyManager.Instance.TryUseBanish();
        }

        if (!used)
        {
            Debug.LogWarning("[LevelUpCardUI] TryUseBanish returned false!");
            UpdateBanishDisplay();
            return;
        }

        // 2. เรียก UpgradeManager.Instance.BanishItem(cardData.Id)
        string banishedId = cardData.Id;
        if (UpgradeManager.Instance != null)
        {
            UpgradeManager.Instance.BanishItem(banishedId);
        }
        else if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.BanishItem(banishedId);
        }

        Debug.Log($"[LevelUpCardUI] Banished card '{cardData.upgradeTitle}' (ID: {banishedId}). Remaining Banish: {GetCurrentBanishCount()}");

        // 3. สุ่มไอเทมใหม่ที่ไม่ซ้ำและไม่อยู่ใน Banish List มาแทนที่การ์ดใบนั้นทันที
        if (onCardBanished != null)
        {
            onCardBanished.Invoke(cardIndex);
        }
        else if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ReplaceCardAt(cardIndex);
        }

        // อัปเดตการแสดงผลของ UI ทุกการ์ด
        if (LevelUpUI.Instance != null)
        {
            LevelUpUI.Instance.RefreshUI();
        }
        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.RefreshAllCardUIs();
        }
    }

    /// <summary>
    /// อัปเดตการแสดงผล: แสดงปุ่ม Banish เฉพาะเมื่อ BanishCount > 0 (พร้อมแสดงข้อความจำนวนสิทธิ์คงเหลือ)
    /// </summary>
    public void UpdateBanishDisplay()
    {
        int banishCount = GetCurrentBanishCount();
        bool showBanish = banishCount > 0 && cardData != null;

        if (banishButton != null)
        {
            banishButton.gameObject.SetActive(showBanish);
            banishButton.interactable = showBanish;
            banishButton.transform.SetAsLastSibling();
        }

        string displayText = $"BANISH ({banishCount})";
        if (banishText != null)
        {
            banishText.text = displayText;
        }
        if (tmpBanishText != null)
        {
            tmpBanishText.text = displayText;
        }
    }

    private int GetCurrentBanishCount()
    {
        if (PlayerInventory.Instance != null)
            return PlayerInventory.Instance.BanishCount;

        var inv = PlayerInventory.GetOrCreate();
        if (inv != null)
            return inv.BanishCount;

        if (CurrencyManager.Instance != null)
            return CurrencyManager.Instance.BanishCount;

        return 0;
    }
}
