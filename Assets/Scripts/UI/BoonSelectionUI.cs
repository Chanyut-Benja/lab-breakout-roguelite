using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI controller for Bio-Gene Chamber Boon selection.
/// Manages the 3 genetic boon cards (Kinetic, Thermal, Shock, Psionic, Cybernetic).
/// Guarantees that card buttons are responsive, raycasts are not blocked,
/// and handles Reroll button integration with PlayerInventory / Black Market.
/// </summary>
public class BoonSelectionUI : MonoBehaviour
{
    public static BoonSelectionUI Instance { get; private set; }

    [Header("Panel References")]
    [SerializeField] private GameObject boonPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private TMP_Text tmpTitleText;

    [Header("Card Buttons (3 Cards)")]
    [SerializeField] private Button[] cardButtons;

    [Header("Card Content UI Elements")]
    [SerializeField] private Text[] cardTitleTexts;
    [SerializeField] private Text[] cardDescTexts;
    [SerializeField] private Image[] cardIconImages;

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
        if (boonPanel == null)
        {
            boonPanel = gameObject;
        }

        if (titleText == null)
        {
            Transform t = transform.Find("TitleText") ?? transform.Find("LevelUpTitle") ?? transform.Find("Title");
            if (t != null) titleText = t.GetComponent<Text>();
        }

        if (tmpTitleText == null)
        {
            Transform t = transform.Find("TitleText") ?? transform.Find("LevelUpTitle") ?? transform.Find("Title");
            if (t != null) tmpTitleText = t.GetComponent<TMP_Text>();
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

        int count = cardButtons != null ? cardButtons.Length : 0;
        if (cardTitleTexts == null || cardTitleTexts.Length < count) cardTitleTexts = new Text[count];
        if (cardDescTexts == null || cardDescTexts.Length < count) cardDescTexts = new Text[count];
        if (cardIconImages == null || cardIconImages.Length < count) cardIconImages = new Image[count];

        for (int i = 0; i < count; i++)
        {
            if (cardButtons[i] == null) continue;
            Transform btnTr = cardButtons[i].transform;

            // Resolve Icon Image
            if (cardIconImages[i] == null)
            {
                Transform iconTr = btnTr.Find("Icon") ?? btnTr.Find("IconImage");
                if (iconTr == null)
                {
                    foreach (Image img in btnTr.GetComponentsInChildren<Image>(true))
                    {
                        if (img.gameObject != cardButtons[i].gameObject && !img.name.ToLowerInvariant().Contains("border"))
                        {
                            iconTr = img.transform;
                            break;
                        }
                    }
                }
                if (iconTr != null)
                {
                    cardIconImages[i] = iconTr.GetComponent<Image>();
                }
            }

            // Resolve Title Text
            if (cardTitleTexts[i] == null)
            {
                Transform titleTr = btnTr.Find("Title") ?? btnTr.Find("TitleText") ?? btnTr.Find("NameText");
                if (titleTr == null)
                {
                    Text[] txts = btnTr.GetComponentsInChildren<Text>(true);
                    if (txts.Length > 0) titleTr = txts[0].transform;
                }
                if (titleTr != null)
                {
                    cardTitleTexts[i] = titleTr.GetComponent<Text>();
                }
            }

            // Resolve Desc Text
            if (cardDescTexts[i] == null)
            {
                Transform descTr = btnTr.Find("Description") ?? btnTr.Find("DescText") ?? btnTr.Find("DescriptionText");
                if (descTr == null)
                {
                    Text[] txts = btnTr.GetComponentsInChildren<Text>(true);
                    if (txts.Length > 1) descTr = txts[1].transform;
                }
                if (descTr != null)
                {
                    cardDescTexts[i] = descTr.GetComponent<Text>();
                }
            }
        }

        EnsureRerollButtonExists();
    }

    private void EnsureRerollButtonExists()
    {
        if (rerollButton == null)
        {
            Transform panelTr = boonPanel != null ? boonPanel.transform : transform;
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
            Transform panelTr = boonPanel != null ? boonPanel.transform : transform;
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
        if (boonPanel != null)
        {
            boonPanel.SetActive(true);
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
            Debug.LogWarning("[BoonSelectionUI] Reroll failed: RerollCount is 0.");
            RefreshUI();
        }
    }

    public void RerollChoices()
    {
        if (BoonManager.Instance != null)
        {
            BoonManager.Instance.RerollChoices();
        }
        else if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.RerollCards();
        }
        else
        {
            PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
            if (inv != null)
            {
                List<LevelUpUpgradeData> newBoons = inv.GenerateWeightedBoonCards(3);
                for (int i = 0; i < newBoons.Count && i < cardButtons.Length; i++)
                {
                    SetupCard(i, newBoons[i]);
                }
            }
        }
    }

    /// <summary>
    /// Binds and updates visual elements for a Boon card using LevelUpUpgradeData
    /// </summary>
    public void SetupCard(int index, LevelUpUpgradeData upgradeData, System.Action onSelected = null)
    {
        if (index < 0 || cardButtons == null || index >= cardButtons.Length || upgradeData == null) return;
        ResolveReferences();
        BindCardButton(index, upgradeData, onSelected);

        if (cardTitleTexts != null && index < cardTitleTexts.Length && cardTitleTexts[index] != null)
            cardTitleTexts[index].text = upgradeData.upgradeTitle;

        if (cardDescTexts != null && index < cardDescTexts.Length && cardDescTexts[index] != null)
            cardDescTexts[index].text = upgradeData.description;

        if (cardIconImages != null && index < cardIconImages.Length && cardIconImages[index] != null)
        {
            Sprite s = upgradeData.icon ?? PlayerInventory.GetBoonIcon(upgradeData.boonItemType);
            cardIconImages[index].sprite = s;
            cardIconImages[index].color = Color.white;
            cardIconImages[index].preserveAspect = true;
            cardIconImages[index].raycastTarget = false;
            cardIconImages[index].gameObject.SetActive(s != null);
        }
    }

    /// <summary>
    /// Binds and updates visual elements for a Boon card using BoonData
    /// </summary>
    public void SetupCard(int index, BoonData boonData, System.Action onSelected = null)
    {
        if (index < 0 || cardButtons == null || index >= cardButtons.Length || boonData == null) return;
        ResolveReferences();
        BindCardButton(index, boonData, onSelected);

        if (cardTitleTexts != null && index < cardTitleTexts.Length && cardTitleTexts[index] != null)
            cardTitleTexts[index].text = boonData.boonTitle;

        if (cardDescTexts != null && index < cardDescTexts.Length && cardDescTexts[index] != null)
            cardDescTexts[index].text = boonData.description;

        if (cardIconImages != null && index < cardIconImages.Length && cardIconImages[index] != null)
        {
            Sprite s = boonData.icon ?? PlayerInventory.GetBoonIcon(boonData.boonItemType);
            cardIconImages[index].sprite = s;
            cardIconImages[index].color = Color.white;
            cardIconImages[index].preserveAspect = true;
            cardIconImages[index].raycastTarget = false;
            cardIconImages[index].gameObject.SetActive(s != null);
        }
    }

    /// <summary>
    /// Binds card buttons and ensures listeners and raycasts are correctly configured for LevelUpUpgradeData.
    /// </summary>
    public void BindCardButton(int index, LevelUpUpgradeData upgradeData, System.Action onSelected)
    {
        if (cardButtons == null || index < 0 || index >= cardButtons.Length || cardButtons[index] == null) return;

        Button btn = cardButtons[index];
        btn.interactable = true;

        Image btnImg = btn.GetComponent<Image>();
        if (btnImg != null)
        {
            btnImg.raycastTarget = true;
        }

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
            OnCardSelected(upgradeData);
            onSelected?.Invoke();
        });
    }

    /// <summary>
    /// Binds card buttons and ensures listeners and raycasts are correctly configured.
    /// </summary>
    public void BindCardButton(int index, BoonData boonData, System.Action onSelected)
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
            OnCardSelected(boonData);
            onSelected?.Invoke();
        });
    }

    /// <summary>
    /// Handler when a Boon card is selected
    /// </summary>
    public void OnCardSelected(BoonData boon)
    {
        if (boon == null) return;

        if (BoonManager.Instance != null)
        {
            BoonManager.Instance.ApplyChosenBoon(boon);
        }
        else
        {
            PlayerInventory.GetOrCreate()?.AddOrUpgradeBoon(boon.boonItemType);
            if (boonPanel != null) boonPanel.SetActive(false);
            Time.timeScale = 1f;
            RoomManager.Instance?.ProceedToNextRoom();
        }
    }

    public void OnCardSelected(LevelUpUpgradeData upgradeData)
    {
        if (upgradeData == null) return;

        if (BoonManager.Instance != null)
        {
            BoonManager.Instance.ApplyChosenUpgradeData(upgradeData);
        }
        else if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.ApplyChosenUpgrade(upgradeData);
        }
        else
        {
            PlayerInventory.GetOrCreate()?.AddOrUpgradeBoon(upgradeData.boonItemType);
            if (boonPanel != null) boonPanel.SetActive(false);
            Time.timeScale = 1f;
            RoomManager.Instance?.ProceedToNextRoom();
        }
    }

    public void SelectBoonCard(int cardIndex)
    {
        if (BoonManager.Instance != null)
        {
            BoonManager.Instance.SelectBoon(cardIndex);
        }
        else if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.SelectUpgrade(cardIndex);
        }
    }
}
