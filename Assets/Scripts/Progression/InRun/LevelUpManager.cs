using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("Upgrade Database")]
    [SerializeField] private List<LevelUpUpgradeData> upgradePool = new List<LevelUpUpgradeData>();

    [Header("UI References")]
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Text levelUpTitleText;
    [SerializeField] private Text[] cardTitleTexts;       // 3 การ์ด
    [SerializeField] private Text[] cardDescriptionTexts; // 3 การ์ด
    [SerializeField] private Image[] cardIconImages;      // 3 การ์ด (Optional)
    [SerializeField] private Button[] cardButtons;        // 3 การ์ด

    [Header("Dark Sci-Fi Skin Elements")]
    [SerializeField] private Image[] cardBorderImages;
    [SerializeField] private Text[] cardLevelBadgeTexts;

    public static readonly Color32 CardBgDark = new Color32(0x12, 0x18, 0x20, 0xFF);          // #121820
    public static readonly Color32 WeaponBorderCyan = new Color32(0x00, 0xE5, 0xFF, 0xFF);    // #00E5FF Turquoise
    public static readonly Color32 TechBorderAmber = new Color32(0xFF, 0xB3, 0x00, 0xFF);     // #FFB300 Amber
    public static readonly Color32 BoonBorderPurple = new Color32(0xD5, 0x00, 0xF9, 0xFF);    // #D500F9 Neon Purple
    public static readonly Color32 NextLevelGreen = new Color32(0x00, 0xE6, 0x76, 0xFF);      // #00E676
    public static readonly Color32 DescriptionGrey = new Color32(0xE0, 0xE0, 0xE0, 0xFF);     // #E0E0E0

    [Header("Reroll & Banish Token Controls (Section 6.2 & 7)")]
    [SerializeField] private int rerollTokens = 1;        // เริ่มรันมี Reroll 1 ครั้งฟรีตามเอกสาร
    [SerializeField] private int banishTokens = 0;
    [SerializeField] private Button rerollButton;
    [SerializeField] private Text rerollButtonText;
    [SerializeField] private Button banishModeButton;
    [SerializeField] private Text banishButtonText;

    private readonly Queue<int> pendingLevels = new Queue<int>();
    private readonly List<LevelUpUpgradeData> currentOfferedUpgrades = new List<LevelUpUpgradeData>();
    private bool isSelecting = false;
    private bool isFromDoor = false; // ตัวแปรเช็กว่าเปิดการ์ดมาจากประตูหรือไม่
    private bool isFromShop = false;
    private bool isBanishModeActive = false;

    public int RerollTokens => rerollTokens;
    public int BanishTokens => banishTokens;
    public bool IsBanishModeActive => isBanishModeActive;

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

        InitializeDefaultPoolIfEmpty();
        ApplyDarkSciFiLayout();
        EnsureTopSortingOrder();
        SetupCardButtonListeners();
    }

    private void InitializeDefaultPoolIfEmpty()
    {
        if (upgradePool.Count == 0)
        {
            // Weapons (4.1)
            upgradePool.Add(LevelUpUpgradeData.CreateWeaponCard(WeaponType.NanoBlade, 1, "NEW: Nano-Blade (Lv.1)", PlayerInventory.GetWeaponUpgradeDescription(WeaponType.NanoBlade, 1)));
            upgradePool.Add(LevelUpUpgradeData.CreateWeaponCard(WeaponType.BioNeedler, 1, "NEW: Bio-Needler (Lv.1)", PlayerInventory.GetWeaponUpgradeDescription(WeaponType.BioNeedler, 1)));
            upgradePool.Add(LevelUpUpgradeData.CreateWeaponCard(WeaponType.PlasmaBlaster, 1, "NEW: Plasma Blaster (Lv.1)", PlayerInventory.GetWeaponUpgradeDescription(WeaponType.PlasmaBlaster, 1)));
            upgradePool.Add(LevelUpUpgradeData.CreateWeaponCard(WeaponType.TeslaOrb, 1, "NEW: Tesla Orb (Lv.1)", PlayerInventory.GetWeaponUpgradeDescription(WeaponType.TeslaOrb, 1)));
            upgradePool.Add(LevelUpUpgradeData.CreateWeaponCard(WeaponType.GravityGrenadier, 1, "NEW: Gravity Grenadier (Lv.1)", PlayerInventory.GetWeaponUpgradeDescription(WeaponType.GravityGrenadier, 1)));

            // Tech Passives ทั้ง 5 ชนิดตามบาลานซ์ 4.2 (Max Lv.5)
            upgradePool.Add(TechPassiveData.CreateUpgradeCard(TechPassiveType.TargetingLens, 1));
            upgradePool.Add(TechPassiveData.CreateUpgradeCard(TechPassiveType.GeneticExtractor, 1));
            upgradePool.Add(TechPassiveData.CreateUpgradeCard(TechPassiveType.OverclockChip, 1));
            upgradePool.Add(TechPassiveData.CreateUpgradeCard(TechPassiveType.BioFuelTank, 1));
            upgradePool.Add(TechPassiveData.CreateUpgradeCard(TechPassiveType.GravitonCapacitor, 1));
        }
    }

    private void SetupCardButtonListeners()
    {
        if (cardButtons != null)
        {
            for (int i = 0; i < cardButtons.Length; i++)
            {
                int index = i;
                if (cardButtons[i] != null)
                {
                    cardButtons[i].onClick.RemoveAllListeners();
                    cardButtons[i].onClick.AddListener(() => SelectUpgrade(index));
                }
            }
        }

        if (rerollButton != null)
        {
            rerollButton.onClick.RemoveAllListeners();
            rerollButton.onClick.AddListener(UseRerollToken);
        }

        if (banishModeButton != null)
        {
            banishModeButton.onClick.RemoveAllListeners();
            banishModeButton.onClick.AddListener(ToggleBanishMode);
        }
    }

    public void AddRerollTokens(int amount = 1)
    {
        if (amount <= 0) return;
        rerollTokens += amount;
        UpdateTokenButtonsUI();
        Debug.Log($"[LevelUpManager] Added +{amount} Reroll Token! (Total Reroll Tokens: {rerollTokens})");
    }

    public void AddBanishTokens(int amount = 1)
    {
        if (amount <= 0) return;
        banishTokens += amount;
        UpdateTokenButtonsUI();
        Debug.Log($"[LevelUpManager] Added +{amount} Banish Token! (Total Banish Tokens: {banishTokens})");
    }

    public void UseRerollToken()
    {
        if (!isSelecting || rerollTokens <= 0) return;

        rerollTokens--;
        isBanishModeActive = false;

        if (isFromDoor || isFromShop)
        {
            GenerateBoonCardsOrFallback();
        }
        else
        {
            GenerateLevelUpCards();
        }

        UpdateTokenButtonsUI();
        Debug.Log($"[LevelUpManager] Used 1 Reroll Token. Remaining Reroll Tokens: {rerollTokens}");
    }

    public void ToggleBanishMode()
    {
        if (!isSelecting || banishTokens <= 0) return;

        isBanishModeActive = !isBanishModeActive;
        UpdateTokenButtonsUI();
    }

    private void UpdateTokenButtonsUI()
    {
        if (rerollButton != null)
        {
            rerollButton.interactable = rerollTokens > 0;
        }
        if (rerollButtonText != null)
        {
            rerollButtonText.text = $"Reroll ({rerollTokens})";
        }

        if (banishModeButton != null)
        {
            banishModeButton.interactable = banishTokens > 0;
        }
        if (banishButtonText != null)
        {
            banishButtonText.text = isBanishModeActive
                ? $"BANISH: PICK CARD ({banishTokens})"
                : $"Banish ({banishTokens})";
        }
    }

    public void EnsureTopSortingOrder()
    {
        if (levelUpPanel == null) return;
        levelUpPanel.transform.SetAsLastSibling();
        Canvas panelCanvas = levelUpPanel.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = levelUpPanel.AddComponent<Canvas>();
        }
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 100;

        if (levelUpPanel.GetComponent<GraphicRaycaster>() == null)
        {
            levelUpPanel.AddComponent<GraphicRaycaster>();
        }
    }

    /// <summary>
    /// เรียกใช้โดย RoomManager เมื่อผู้เล่นเดินเข้าประตู Bio-Gene Chamber (Boon)
    /// </summary>
    public void ShowBoonSelectionFromDoor()
    {
        EnsureTopSortingOrder();
        isFromDoor = true;
        isFromShop = false;

        if (!GenerateBoonCardsOrFallback())
        {
            // กรณีที่ Boon ทั้ง 3 ช่องเต็ม Lv.2 Max แล้ว ให้มอบเครดิตแทนและไปห้องถัดไป
            isFromDoor = false;
            GameOverManager.Instance?.AddRunCredits(50);
            RoomManager.Instance?.ProceedToNextRoom();
            return;
        }

        isSelecting = true;
        Time.timeScale = 0f;

        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(true);
        }

        if (levelUpTitleText != null)
        {
            levelUpTitleText.text = "BIO-GENE CHAMBER (BOON)";
        }
    }

    public void ShowBoonSelectionFromShop()
    {
        EnsureTopSortingOrder();
        isFromShop = true;
        isFromDoor = false;

        if (!GenerateBoonCardsOrFallback())
        {
            isFromShop = false;
            ShopManager.Instance?.OnBoonSelectionClosed();
            return;
        }

        isSelecting = true;
        Time.timeScale = 0f;
        if (levelUpPanel != null) levelUpPanel.SetActive(true);
        if (levelUpTitleText != null) levelUpTitleText.text = "BLACK MARKET BOON";
    }

    /// <summary>
    /// เพิ่มคิวเลเวลอัป เมื่อเก็บ EXP ทะลุหลอด
    /// </summary>
    public void QueueLevelUp(int newLevel)
    {
        pendingLevels.Enqueue(newLevel);

        if (!isSelecting)
        {
            ShowNextLevelUp();
        }
    }

    private void ShowNextLevelUp()
    {
        while (pendingLevels.Count > 0)
        {
            int targetLevel = pendingLevels.Dequeue();

            // สุ่มการ์ดอาวุธและ Tech Passive ตามกฎ Slot (3 Weapons, 3 Techs) และน้ำหนัก x3
            if (!GenerateLevelUpCards())
            {
                // กฎหัวข้อ 7 ข้อ 5: ทุกอย่างเต็มแล้ว -> แปลงเป็น Nano-Credits แทนการเปิดหน้าการ์ดว่าง
                GameOverManager.Instance?.AddRunCredits(25);
                Debug.Log($"[LevelUpManager] All Weapon & Tech slots maxed at Level {targetLevel}. Awarded +25 Nano-Credits.");
                continue;
            }

            EnsureTopSortingOrder();
            isSelecting = true;
            Time.timeScale = 0f; // หยุดเวลาสไตล์ Vampire Survivors

            if (levelUpPanel != null)
            {
                levelUpPanel.SetActive(true);
            }

            if (levelUpTitleText != null)
            {
                levelUpTitleText.text = $"LEVEL UP! (Level {targetLevel})";
            }
            return;
        }

        CloseLevelUpWindow();
    }

    private bool GenerateLevelUpCards()
    {
        currentOfferedUpgrades.Clear();

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory != null)
        {
            List<LevelUpUpgradeData> weightedCards = inventory.GenerateWeightedLevelUpCards(3);
            if (weightedCards.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < weightedCards.Count; i++)
            {
                currentOfferedUpgrades.Add(weightedCards[i]);
            }

            BindCardsToUI();
            return true;
        }

        GenerateLegacyCards();
        return currentOfferedUpgrades.Count > 0;
    }

    private bool GenerateBoonCardsOrFallback()
    {
        currentOfferedUpgrades.Clear();

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory != null)
        {
            List<LevelUpUpgradeData> boonCards = inventory.GenerateWeightedBoonCards(3);
            if (boonCards.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < boonCards.Count; i++)
            {
                currentOfferedUpgrades.Add(boonCards[i]);
            }

            BindCardsToUI();
            return true;
        }

        GenerateLegacyCards();
        return currentOfferedUpgrades.Count > 0;
    }

    private void GenerateLegacyCards()
    {
        currentOfferedUpgrades.Clear();
        List<LevelUpUpgradeData> tempPool = new List<LevelUpUpgradeData>(upgradePool);

        for (int i = 0; i < 3; i++)
        {
            if (tempPool.Count == 0) break;

            int randomIndex = Random.Range(0, tempPool.Count);
            LevelUpUpgradeData selectedUpgrade = tempPool[randomIndex];
            currentOfferedUpgrades.Add(selectedUpgrade);
            tempPool.RemoveAt(randomIndex);
        }

        BindCardsToUI();
    }

    [ContextMenu("Apply Dark Sci-Fi Skin")]
    public void ContextApplyDarkSciFiSkin()
    {
        ApplyDarkSciFiLayout();
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(gameObject);
        if (levelUpPanel != null) UnityEditor.EditorUtility.SetDirty(levelUpPanel);
        if (cardButtons != null)
        {
            foreach (var b in cardButtons)
            {
                if (b != null) UnityEditor.EditorUtility.SetDirty(b.gameObject);
            }
        }
#endif
    }

    public void ApplyDarkSciFiLayout()
    {
        Font sciFiFont = InGameHUDController.GetDefaultFont();

        // 1. LevelUpPanel Full-Screen Dark Overlay
        if (levelUpPanel != null)
        {
            RectTransform pRect = levelUpPanel.GetComponent<RectTransform>();
            if (pRect != null)
            {
                pRect.anchorMin = Vector2.zero;
                pRect.anchorMax = Vector2.one;
                pRect.offsetMin = Vector2.zero;
                pRect.offsetMax = Vector2.zero;
                pRect.localScale = Vector3.one;
            }

            Image panelImg = levelUpPanel.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color32(0x06, 0x0A, 0x10, 0xEE);
                panelImg.sprite = null;
                panelImg.raycastTarget = false;
            }
        }

        // 2. Title Text Header
        if (levelUpTitleText != null)
        {
            RectTransform tRect = levelUpTitleText.GetComponent<RectTransform>();
            if (tRect != null)
            {
                tRect.anchorMin = new Vector2(0.5f, 0.5f);
                tRect.anchorMax = new Vector2(0.5f, 0.5f);
                tRect.pivot = new Vector2(0.5f, 0.5f);
                tRect.anchoredPosition = new Vector2(0f, 320f);
                tRect.sizeDelta = new Vector2(900f, 60f);
                tRect.localScale = Vector3.one;
            }

            levelUpTitleText.font = sciFiFont;
            levelUpTitleText.fontSize = 32;
            levelUpTitleText.fontStyle = FontStyle.Bold;
            levelUpTitleText.color = WeaponBorderCyan;
            levelUpTitleText.alignment = TextAnchor.MiddleCenter;
            levelUpTitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            levelUpTitleText.verticalOverflow = VerticalWrapMode.Overflow;
            levelUpTitleText.raycastTarget = false;

            Shadow shadow = levelUpTitleText.GetComponent<Shadow>();
            if (shadow == null) shadow = levelUpTitleText.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            shadow.effectDistance = new Vector2(2f, -2f);
        }

        // 3. 3 Cards Layout: 320px width, 480px height, 35px spacing
        if (cardButtons == null || cardButtons.Length == 0) return;

        if (cardBorderImages == null || cardBorderImages.Length != cardButtons.Length)
        {
            cardBorderImages = new Image[cardButtons.Length];
        }
        if (cardLevelBadgeTexts == null || cardLevelBadgeTexts.Length != cardButtons.Length)
        {
            cardLevelBadgeTexts = new Text[cardButtons.Length];
        }

        float cardWidth = 320f;
        float cardHeight = 480f;
        float spacing = 35f;

        for (int i = 0; i < cardButtons.Length; i++)
        {
            Button btn = cardButtons[i];
            if (btn == null) continue;

            // Positioning: -355, 0, +355
            float xPos = (i - 1) * (cardWidth + spacing);

            RectTransform cardRect = btn.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.anchoredPosition = new Vector2(xPos, -15f);
            cardRect.sizeDelta = new Vector2(cardWidth, cardHeight);
            cardRect.localScale = Vector3.one;

            // Outer Border Image (3px border effect via outer image + inner background)
            Image borderImg = btn.GetComponent<Image>();
            if (borderImg == null) borderImg = btn.gameObject.AddComponent<Image>();
            borderImg.sprite = null;
            borderImg.color = WeaponBorderCyan;
            borderImg.raycastTarget = true;
            btn.targetGraphic = borderImg;
            cardBorderImages[i] = borderImg;

            // Hover effect (1.05x scale)
            if (btn.GetComponent<CardHoverEffect>() == null)
            {
                btn.gameObject.AddComponent<CardHoverEffect>();
            }

            // Inner Background: #121820 inset by 3px on all sides
            Transform innerBgTrans = btn.transform.Find("CardInnerBg");
            if (innerBgTrans == null)
            {
                GameObject bgGo = new GameObject("CardInnerBg", typeof(RectTransform), typeof(Image));
                bgGo.transform.SetParent(btn.transform, false);
                bgGo.transform.SetAsFirstSibling();
                innerBgTrans = bgGo.transform;
            }
            else
            {
                innerBgTrans.SetAsFirstSibling();
            }

            RectTransform innerBgRect = innerBgTrans.GetComponent<RectTransform>();
            innerBgRect.anchorMin = Vector2.zero;
            innerBgRect.anchorMax = Vector2.one;
            innerBgRect.offsetMin = new Vector2(3f, 3f);
            innerBgRect.offsetMax = new Vector2(-3f, -3f);

            Image innerBgImg = innerBgTrans.GetComponent<Image>();
            innerBgImg.sprite = null;
            innerBgImg.color = CardBgDark; // #121820
            innerBgImg.raycastTarget = false;

            // Icon Frame (104x104px placeholder frame)
            Transform iconFrameTrans = btn.transform.Find("IconSlotFrame");
            if (iconFrameTrans == null)
            {
                GameObject frameGo = new GameObject("IconSlotFrame", typeof(RectTransform), typeof(Image));
                frameGo.transform.SetParent(btn.transform, false);
                iconFrameTrans = frameGo.transform;
            }
            RectTransform frameRect = iconFrameTrans.GetComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0.5f, 0.5f);
            frameRect.anchorMax = new Vector2(0.5f, 0.5f);
            frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.anchoredPosition = new Vector2(0f, 65f);
            frameRect.sizeDelta = new Vector2(104f, 104f);

            Image frameImg = iconFrameTrans.GetComponent<Image>();
            frameImg.sprite = null;
            frameImg.color = new Color(0.14f, 0.19f, 0.26f, 0.85f);
            frameImg.raycastTarget = false;

            // Title Text: 24sp Bold Center
            if (cardTitleTexts != null && i < cardTitleTexts.Length && cardTitleTexts[i] != null)
            {
                Text titleText = cardTitleTexts[i];
                RectTransform titleRect = titleText.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.5f, 0.5f);
                titleRect.anchorMax = new Vector2(0.5f, 0.5f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, 175f);
                titleRect.sizeDelta = new Vector2(280f, 60f);

                titleText.font = sciFiFont;
                titleText.fontSize = 24;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
                titleText.verticalOverflow = VerticalWrapMode.Overflow;
                titleText.raycastTarget = false;

                Shadow shadow = titleText.GetComponent<Shadow>();
                if (shadow == null) shadow = titleText.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                shadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // Icon Image: 100x100px Center
            if (cardIconImages == null || cardIconImages.Length < 3)
            {
                cardIconImages = new Image[3];
            }
            if (cardIconImages[i] == null)
            {
                Transform iconTr = btn.transform.Find("Icon");
                if (iconTr == null) iconTr = iconFrameTrans.Find("IconImage");
                if (iconTr == null)
                {
                    GameObject iconGo = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
                    iconGo.transform.SetParent(iconFrameTrans, false);
                    iconTr = iconGo.transform;
                }
                cardIconImages[i] = iconTr.GetComponent<Image>();
            }

            if (cardIconImages[i] != null)
            {
                Image iconImg = cardIconImages[i];
                iconImg.transform.SetSiblingIndex(iconFrameTrans.GetSiblingIndex() + 1);
                RectTransform iconRect = iconImg.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.anchoredPosition = new Vector2(0f, 65f);
                iconRect.sizeDelta = new Vector2(100f, 100f);
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
            }

            // Description Text: 18sp #E0E0E0 Center
            if (cardDescriptionTexts != null && i < cardDescriptionTexts.Length && cardDescriptionTexts[i] != null)
            {
                Text descText = cardDescriptionTexts[i];
                RectTransform descRect = descText.GetComponent<RectTransform>();
                descRect.anchorMin = new Vector2(0.5f, 0.5f);
                descRect.anchorMax = new Vector2(0.5f, 0.5f);
                descRect.pivot = new Vector2(0.5f, 0.5f);
                descRect.anchoredPosition = new Vector2(0f, -50f);
                descRect.sizeDelta = new Vector2(280f, 130f);

                descText.font = sciFiFont;
                descText.fontSize = 18;
                descText.fontStyle = FontStyle.Normal;
                descText.color = DescriptionGrey;
                descText.alignment = TextAnchor.MiddleCenter;
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Overflow;
                descText.raycastTarget = false;
            }

            // Next Level Badge: 16sp Bold #00E676 Bottom
            Transform badgeTrans = btn.transform.Find("NextLevelBadge");
            if (badgeTrans == null)
            {
                GameObject badgeGo = new GameObject("NextLevelBadge", typeof(RectTransform), typeof(Text), typeof(Shadow));
                badgeGo.transform.SetParent(btn.transform, false);
                badgeTrans = badgeGo.transform;
            }
            RectTransform badgeRect = badgeTrans.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.5f, 0.5f);
            badgeRect.anchorMax = new Vector2(0.5f, 0.5f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.anchoredPosition = new Vector2(0f, -195f);
            badgeRect.sizeDelta = new Vector2(280f, 36f);

            Text badgeText = badgeTrans.GetComponent<Text>();
            badgeText.font = sciFiFont;
            badgeText.fontSize = 16;
            badgeText.fontStyle = FontStyle.Bold;
            badgeText.color = NextLevelGreen;
            badgeText.alignment = TextAnchor.MiddleCenter;
            badgeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            badgeText.verticalOverflow = VerticalWrapMode.Overflow;
            badgeText.raycastTarget = false;

            Shadow bShadow = badgeTrans.GetComponent<Shadow>();
            if (bShadow == null) bShadow = badgeTrans.gameObject.AddComponent<Shadow>();
            bShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            bShadow.effectDistance = new Vector2(1f, -1f);

            cardLevelBadgeTexts[i] = badgeText;
        }

        // 4. Token Buttons Position
        if (rerollButton != null)
        {
            RectTransform rBtnRect = rerollButton.GetComponent<RectTransform>();
            if (rBtnRect != null)
            {
                rBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
                rBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
                rBtnRect.pivot = new Vector2(0.5f, 0.5f);
                rBtnRect.anchoredPosition = new Vector2(-120f, -295f);
                rBtnRect.sizeDelta = new Vector2(180f, 44f);
            }
        }
        if (banishModeButton != null)
        {
            RectTransform bBtnRect = banishModeButton.GetComponent<RectTransform>();
            if (bBtnRect != null)
            {
                bBtnRect.anchorMin = new Vector2(0.5f, 0.5f);
                bBtnRect.anchorMax = new Vector2(0.5f, 0.5f);
                bBtnRect.pivot = new Vector2(0.5f, 0.5f);
                bBtnRect.anchoredPosition = new Vector2(120f, -295f);
                bBtnRect.sizeDelta = new Vector2(180f, 44f);
            }
        }
    }

    private void BindCardsToUI()
    {
        ApplyDarkSciFiLayout();

        int slotCount = 3;
        if (cardButtons != null && cardButtons.Length > 0)
        {
            slotCount = cardButtons.Length;
        }

        for (int i = 0; i < slotCount; i++)
        {
            bool hasCard = i < currentOfferedUpgrades.Count && currentOfferedUpgrades[i] != null;

            if (cardButtons != null && i < cardButtons.Length && cardButtons[i] != null)
            {
                cardButtons[i].gameObject.SetActive(hasCard);
            }

            if (!hasCard) continue;

            LevelUpUpgradeData selectedUpgrade = currentOfferedUpgrades[i];

            // 1. Card Rarity / Category Border Color
            Color32 borderColor = selectedUpgrade.category switch
            {
                UpgradeCategory.Weapon => WeaponBorderCyan,     // #00E5FF
                UpgradeCategory.TechPassive => TechBorderAmber, // #FFB300
                UpgradeCategory.Boon => BoonBorderPurple,       // #D500F9
                _ => WeaponBorderCyan
            };

            if (cardBorderImages != null && i < cardBorderImages.Length && cardBorderImages[i] != null)
            {
                cardBorderImages[i].color = borderColor;
            }
            else if (cardButtons != null && i < cardButtons.Length && cardButtons[i] != null)
            {
                Image bImg = cardButtons[i].GetComponent<Image>();
                if (bImg != null) bImg.color = borderColor;
            }

            // 2. Title Text (24sp Bold)
            if (cardTitleTexts != null && i < cardTitleTexts.Length && cardTitleTexts[i] != null)
            {
                cardTitleTexts[i].text = selectedUpgrade.upgradeTitle;
                cardTitleTexts[i].fontSize = 24;
                cardTitleTexts[i].fontStyle = FontStyle.Bold;
                cardTitleTexts[i].color = Color.white;
                cardTitleTexts[i].alignment = TextAnchor.MiddleCenter;
                cardTitleTexts[i].raycastTarget = false;
            }

            // 3. Description Text (18sp #E0E0E0)
            if (cardDescriptionTexts != null && i < cardDescriptionTexts.Length && cardDescriptionTexts[i] != null)
            {
                cardDescriptionTexts[i].text = selectedUpgrade.description;
                cardDescriptionTexts[i].fontSize = 18;
                cardDescriptionTexts[i].color = DescriptionGrey;
                cardDescriptionTexts[i].alignment = TextAnchor.MiddleCenter;
                cardDescriptionTexts[i].raycastTarget = false;
            }

            // 4. Icon (100x100)
            Sprite iconSprite = selectedUpgrade.icon;
            if (iconSprite == null && selectedUpgrade.category == UpgradeCategory.Weapon)
            {
                iconSprite = PlayerInventory.GetWeaponIcon(selectedUpgrade.weaponType);
            }
            else if (iconSprite == null && selectedUpgrade.category == UpgradeCategory.TechPassive)
            {
                iconSprite = PlayerInventory.GetTechPassiveIcon(selectedUpgrade.techType);
            }
            else if (iconSprite == null && selectedUpgrade.category == UpgradeCategory.Boon)
            {
                iconSprite = PlayerInventory.GetBoonIcon(selectedUpgrade.boonItemType);
            }

            if (cardIconImages != null && i < cardIconImages.Length && cardIconImages[i] != null)
            {
                cardIconImages[i].sprite = iconSprite;
                cardIconImages[i].color = Color.white;
                cardIconImages[i].preserveAspect = true;
                cardIconImages[i].raycastTarget = false;
                cardIconImages[i].gameObject.SetActive(iconSprite != null);
            }

            // 5. Next Level Badge (16sp #00E676, e.g. Lv.2 -> Lv.3)
            int currentLv = 0;
            PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
            if (inv != null)
            {
                if (selectedUpgrade.category == UpgradeCategory.Weapon)
                    currentLv = inv.GetWeaponLevel(selectedUpgrade.weaponType);
                else if (selectedUpgrade.category == UpgradeCategory.TechPassive)
                    currentLv = inv.GetTechLevel(selectedUpgrade.techType);
                else if (selectedUpgrade.category == UpgradeCategory.Boon)
                    currentLv = inv.GetBoonLevel(selectedUpgrade.boonItemType);
            }

            string badgeString = currentLv > 0
                ? $"Lv.{currentLv}  ➔  Lv.{selectedUpgrade.targetLevel}"
                : $"NEW  ➔  Lv.{selectedUpgrade.targetLevel}";

            if (cardLevelBadgeTexts != null && i < cardLevelBadgeTexts.Length && cardLevelBadgeTexts[i] != null)
            {
                cardLevelBadgeTexts[i].text = badgeString;
                cardLevelBadgeTexts[i].color = NextLevelGreen;
                cardLevelBadgeTexts[i].fontSize = 16;
                cardLevelBadgeTexts[i].fontStyle = FontStyle.Bold;
                cardLevelBadgeTexts[i].raycastTarget = false;
            }
        }

        UpdateTokenButtonsUI();
    }

    public void ApplyChosenUpgrade(LevelUpUpgradeData upgradeData)
    {
        if (upgradeData == null) return;

        int index = currentOfferedUpgrades.IndexOf(upgradeData);
        if (index >= 0)
        {
            SelectUpgrade(index);
            return;
        }

        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ApplyLevelUpgrade(upgradeData);
        }
        else
        {
            PlayerInventory inv = PlayerInventory.GetOrCreate();
            if (inv != null)
            {
                switch (upgradeData.category)
                {
                    case UpgradeCategory.Weapon:
                        inv.AddOrUpgradeWeapon(upgradeData.weaponType);
                        break;
                    case UpgradeCategory.TechPassive:
                        inv.AddOrUpgradeTech(upgradeData.techType);
                        break;
                    case UpgradeCategory.Boon:
                        inv.AddOrUpgradeBoon(upgradeData.boonItemType);
                        break;
                }
            }
        }

        CloseLevelUpWindow(true);
    }

    /// <summary>
    /// ทำงานเมื่อผู้เล่นคลิกเลือกการ์ดใบที่ 0, 1 หรือ 2
    /// </summary>
    public void SelectUpgrade(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= currentOfferedUpgrades.Count) return;

        LevelUpUpgradeData chosen = currentOfferedUpgrades[cardIndex];
        if (chosen == null) return;

        // หากเปิดโหมด Banish อยู่ ให้ตัดการ์ดใบนั้นทิ้งตลอดรันโดยไม่เสียสิทธิ์เลือกการ์ดในรอบนี้
        if (isBanishModeActive && banishTokens > 0)
        {
            banishTokens--;
            isBanishModeActive = false;

            PlayerInventory.GetOrCreate()?.BanishCard(chosen);
            currentOfferedUpgrades[cardIndex] = null;
            BindCardsToUI();
            Debug.Log($"[LevelUpManager] Banished card '{chosen.upgradeTitle}'. Remaining Banish Tokens: {banishTokens}");
            return;
        }

        // Apply stat / slot item ไปยัง PlayerStats & PlayerInventory
        if (PlayerStats.Instance != null)
        {
            PlayerStats.Instance.ApplyLevelUpgrade(chosen);
        }
        else
        {
            PlayerInventory inv = PlayerInventory.GetOrCreate();
            if (inv != null && chosen != null)
            {
                switch (chosen.category)
                {
                    case UpgradeCategory.Weapon:
                        inv.AddOrUpgradeWeapon(chosen.weaponType);
                        break;
                    case UpgradeCategory.TechPassive:
                        inv.AddOrUpgradeTech(chosen.techType);
                        break;
                    case UpgradeCategory.Boon:
                        inv.AddOrUpgradeBoon(chosen.boonItemType);
                        break;
                }
            }
        }

        // ถ้ายังมี Level ใน Queue ค้างอยู่ (จากการเก็บ EXP) ให้เปิดหน้าต่างต่อ
        if (!isFromDoor && !isFromShop && pendingLevels.Count > 0)
        {
            ShowNextLevelUp();
        }
        else
        {
            bool returnToShop = isFromShop;
            CloseLevelUpWindow(!returnToShop);
            isFromShop = false;

            if (returnToShop)
            {
                ShopManager.Instance?.OnBoonSelectionClosed();
            }

            // เช็กสถานะ: ถ้าเปิดมาจากประตู ให้สั่งย้ายเข้าสู่ห้องถัดไป
            if (isFromDoor)
            {
                isFromDoor = false; // รีเซ็ตค่ากลับ
                if (RoomManager.Instance != null)
                {
                    RoomManager.Instance.ProceedToNextRoom(); // สั่งย้ายเข้าสู่ห้องถัดไปเฉพาะการเลือกผ่านประตู
                }
            }
            // ถ้าเป็นการ Level Up ปกติกลางห้อง เกมจะปลดล็อกเวลาและเล่นต่อในห้องเดิม
        }
    }

    private void CloseLevelUpWindow(bool resumeGame = true)
    {
        isSelecting = false;
        if (levelUpPanel != null)
        {
            levelUpPanel.SetActive(false);
        }
        if (resumeGame) Time.timeScale = 1f;
    }
}
