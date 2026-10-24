using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ระบบร้านค้า Cyberpunk Black Market ตามเอกสารบาลานซ์ หัวข้อ 6.2 (ยกเลิกการขายสเตตัสถาวรเดิมทั้งหมด):
/// 1. โครงสร้างหน้าต่างร้านค้า (Main Window):
///    - ขนาด 1000px x 620px จัดกึ่งกลางหน้าจอ
///    - พื้นหลังสีดำอมม่วงเข้ม (#0F0A1C) กรอบสีม่วงนีออน (#9D4EDD) หนา 3px
///    - ด้านบนสุดมีป้ายชื่อ "BLACK MARKET" ขนาด Font 36sp พร้อมแถบแสดง Credits ปัจจุบัน (สีทอง #FFD166 Font 24sp)
/// 2. แผงรายการสินค้า (Item Slots):
///    - จัดเรียงการ์ด 4 ช่องแนวนอน: ยาฮีล 30% HP, Boon ประจำห้อง, ตั๋ว Reroll, และตั๋ว Banish (รวมถึง Overdrive Stim / Reroll Shop เมื่อสุ่ม)
///    - การ์ดสินค้าแต่ละใบ: สีเทาเข้มตัดขอบม่วง (#161322), ชื่อไอเทม (Font 20sp), คำอธิบายผลลัพธ์ (Font 15sp), ราคาเป็นตัวเลขสีทอง (Font 20sp Bold)
///    - หากอยู่ Floor 2 ให้แสดงราคาที่คำนวณตัวคูณ 1.3x แล้วอย่างถูกต้อง
/// 3. ปุ่มและการโต้ตอบ (Buttons & Interactions):
///    - ปุ่ม [BUY] ใต้การ์ดสินค้าแต่ละใบ: สีม่วงสว่าง (#9D4EDD) หากเงินไม่พอซื้อ ให้เปลี่ยนเป็นสีเทาและกดไม่ได้ (interactable = false)
///    - สินค้าที่ซื้อแล้ว: แสดงป้ายทับ "SOLD OUT" ทแยงมุมนีออน
///    - ด้านล่างสุดมีปุ่ม [LEAVE SHOP / CONTINUE] ขนาดใหญ่ชัดเจนตรงกลาง เพื่อปิดหน้าต่างและดำเนินเกมต่อ
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    // --- Cyberpunk Color Constants ---
    public static readonly Color ShopWindowBg = new Color(0.059f, 0.039f, 0.110f, 1f);     // #0F0A1C
    public static readonly Color NeonPurpleBorder = new Color(0.616f, 0.306f, 0.867f, 1f); // #9D4EDD
    public static readonly Color GoldCredits = new Color(1f, 0.820f, 0.400f, 1f);          // #FFD166
    public static readonly Color CardBgDark = new Color(0.086f, 0.075f, 0.133f, 1f);       // #161322
    public static readonly Color BuyButtonPurple = new Color(0.616f, 0.306f, 0.867f, 1f);  // #9D4EDD
    public static readonly Color BuyButtonDisabled = new Color(0.227f, 0.196f, 0.282f, 1f);// #3A3248
    public static readonly Color SoldOutRed = new Color(1f, 0.165f, 0.427f, 1f);           // #FF2A6D

    // --- Balance Pricing Constants ---
    public const int BaseNewBoonPriceFloor1 = 450;
    public const int BaseNewBoonPriceFloor2 = 590;
    public const int BaseHealPodPrice = 80;
    public const int HealPodRepeatIncrement = 20;
    public const int BaseRerollTokenPrice = 120;
    public const int BaseBanishTokenPrice = 150;
    public const int BaseOverdriveStimPrice = 100;
    public const int BaseRerollShopPrice = 40;
    public const float Floor2PriceMultiplier = 1.3f;

    [Header("UI References")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Text titleText;
    [SerializeField] private Text currencyDisplay;
    [SerializeField] private ShopSlotUI[] shopSlots; // รองรับ 4 ช่องสินค้าแนวนอน
    [SerializeField] private Button leaveButton;
    [SerializeField] private Button rerollShopButton;
    [SerializeField] private Text rerollShopButtonText;

    [Header("Shop Catalog (Section 6.2)")]
    [SerializeField] private List<ShopItemData> itemPool = new List<ShopItemData>();

    [Header("Audio")]
    [SerializeField] private AudioClip purchaseSFX;

    private readonly List<ShopItemData> currentStock = new List<ShopItemData>();
    private int healPodPurchasesInCurrentRoom = 0;

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

        InitializeBalancedCatalog();
        ApplyCyberpunkShopTheme();

        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(LeaveShop);
        }

        if (rerollShopButton != null)
        {
            rerollShopButton.onClick.RemoveAllListeners();
            rerollShopButton.onClick.AddListener(TryRerollShopFromButton);
        }
    }

    /// <summary>
    /// ปรับโครงสร้างและธีมหน้าต่าง Black Market ให้ตรงตามดีไซน์ Cyberpunk
    /// </summary>
    [ContextMenu("Apply Cyberpunk Theme")]
    public void ApplyCyberpunkShopTheme()
    {
        Font sciFiFont = InGameHUDController.GetDefaultFont();

        // 1. Main Window Dimensions: 1000px x 620px Centered
        if (shopPanel != null)
        {
            RectTransform panelRect = shopPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.anchoredPosition = Vector2.zero;
                panelRect.sizeDelta = new Vector2(1000f, 620f);
            }

            // Outer border: Neon Purple (#9D4EDD, 3px)
            Image panelImg = shopPanel.GetComponent<Image>();
            if (panelImg == null) panelImg = shopPanel.AddComponent<Image>();
            panelImg.color = NeonPurpleBorder;
            panelImg.raycastTarget = false;

            // Inner background: Dark Purple-Black (#0F0A1C, inset 3px)
            Transform innerBgTrans = shopPanel.transform.Find("ShopWindowInnerBg");
            if (innerBgTrans == null)
            {
                GameObject innerGo = new GameObject("ShopWindowInnerBg", typeof(RectTransform), typeof(Image));
                innerGo.transform.SetParent(shopPanel.transform, false);
                innerGo.transform.SetAsFirstSibling();
                innerBgTrans = innerGo.transform;
            }
            else
            {
                innerBgTrans.SetAsFirstSibling();
            }

            RectTransform innerRect = innerBgTrans.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(3f, 3f);
            innerRect.offsetMax = new Vector2(-3f, -3f);

            Image innerImg = innerBgTrans.GetComponent<Image>();
            innerImg.color = ShopWindowBg;
            innerImg.raycastTarget = false;

            // 2. Header: "BLACK MARKET" (Font 36sp Bold White)
            if (titleText == null)
            {
                Transform titleTrans = shopPanel.transform.Find("TitleText");
                if (titleTrans != null) titleText = titleTrans.GetComponent<Text>();
            }
            if (titleText != null)
            {
                titleText.text = "BLACK MARKET";
                titleText.font = sciFiFont;
                titleText.fontSize = 36;
                titleText.fontStyle = FontStyle.Bold;
                titleText.color = Color.white;
                titleText.alignment = TextAnchor.MiddleCenter;
                titleText.raycastTarget = false;

                RectTransform titleRect = titleText.GetComponent<RectTransform>();
                titleRect.anchorMin = new Vector2(0.5f, 1f);
                titleRect.anchorMax = new Vector2(0.5f, 1f);
                titleRect.pivot = new Vector2(0.5f, 0.5f);
                titleRect.anchoredPosition = new Vector2(0f, -44f);
                titleRect.sizeDelta = new Vector2(500f, 48f);

                Shadow titleShadow = titleText.GetComponent<Shadow>();
                if (titleShadow == null) titleShadow = titleText.gameObject.AddComponent<Shadow>();
                titleShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                titleShadow.effectDistance = new Vector2(2f, -2f);
            }

            // 3. Current Credits Display (Font 24sp Bold Gold #FFD166)
            if (currencyDisplay != null)
            {
                currencyDisplay.font = sciFiFont;
                currencyDisplay.fontSize = 24;
                currencyDisplay.fontStyle = FontStyle.Bold;
                currencyDisplay.color = GoldCredits;
                currencyDisplay.alignment = TextAnchor.MiddleCenter;
                currencyDisplay.raycastTarget = false;

                RectTransform curRect = currencyDisplay.GetComponent<RectTransform>();
                curRect.anchorMin = new Vector2(0.5f, 1f);
                curRect.anchorMax = new Vector2(0.5f, 1f);
                curRect.pivot = new Vector2(0.5f, 0.5f);
                curRect.anchoredPosition = new Vector2(0f, -86f);
                curRect.sizeDelta = new Vector2(400f, 36f);

                Shadow curShadow = currencyDisplay.GetComponent<Shadow>();
                if (curShadow == null) curShadow = currencyDisplay.gameObject.AddComponent<Shadow>();
                curShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                curShadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 4. Leave Button: Prominent [LEAVE SHOP / CONTINUE] centered at bottom
            if (leaveButton != null)
            {
                RectTransform lbRect = leaveButton.GetComponent<RectTransform>();
                lbRect.anchorMin = new Vector2(0.5f, 0f);
                lbRect.anchorMax = new Vector2(0.5f, 0f);
                lbRect.pivot = new Vector2(0.5f, 0.5f);
                lbRect.anchoredPosition = new Vector2(0f, 46f);
                lbRect.sizeDelta = new Vector2(340f, 50f);

                Image lbImg = leaveButton.GetComponent<Image>();
                if (lbImg != null)
                {
                    lbImg.color = new Color32(0x3B, 0x18, 0x5F, 0xFF); // Rich dark purple
                }

                var btnColors = leaveButton.colors;
                btnColors.normalColor = new Color32(0x3B, 0x18, 0x5F, 0xFF);
                btnColors.highlightedColor = NeonPurpleBorder;
                btnColors.pressedColor = new Color32(0x7B, 0x2C, 0xBF, 0xFF);
                btnColors.disabledColor = BuyButtonDisabled;
                btnColors.colorMultiplier = 1f;
                leaveButton.colors = btnColors;

                if (leaveButton.GetComponent<CardHoverEffect>() == null)
                {
                    leaveButton.gameObject.AddComponent<CardHoverEffect>();
                }

                Text lbText = leaveButton.GetComponentInChildren<Text>();
                if (lbText != null)
                {
                    lbText.text = "LEAVE SHOP / CONTINUE";
                    lbText.font = sciFiFont;
                    lbText.fontSize = 20;
                    lbText.fontStyle = FontStyle.Bold;
                    lbText.color = Color.white;
                    lbText.alignment = TextAnchor.MiddleCenter;
                    lbText.raycastTarget = false;
                }
            }
        }

        EnsureFourShopSlots();
    }

    /// <summary>
    /// ตรวจสอบให้แน่ใจว่ามีช่องสินค้าครบ 4 ช่องแนวนอน และจัดวางตำแหน่งอย่างสวยงาม
    /// </summary>
    public void EnsureFourShopSlots()
    {
        if (shopSlots == null || shopSlots.Length == 0) return;

        if (shopSlots.Length < 4)
        {
            var list = new List<ShopSlotUI>(shopSlots);
            while (list.Count < 4)
            {
                if (list[0] != null)
                {
                    GameObject clone = Instantiate(list[0].gameObject, list[0].transform.parent);
                    clone.name = $"ShopSlot_{list.Count}";
                    ShopSlotUI slotUI = clone.GetComponent<ShopSlotUI>();
                    list.Add(slotUI);
                }
                else
                {
                    break;
                }
            }
            shopSlots = list.ToArray();
        }

        // 4 slots arranged horizontally:
        // Window width = 1000px, 4 cards (width 218px, spacing 24px)
        // Center offsets: -363, -121, +121, +363, Y = +10
        float[] xPositions = new float[] { -363f, -121f, 121f, 363f };
        for (int i = 0; i < shopSlots.Length && i < 4; i++)
        {
            if (shopSlots[i] == null) continue;
            RectTransform slotRt = shopSlots[i].GetComponent<RectTransform>();
            if (slotRt != null)
            {
                slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = new Vector2(xPositions[i], 10f);
                slotRt.sizeDelta = new Vector2(218f, 375f);
                slotRt.localScale = Vector3.one;
            }
            shopSlots[i].ApplyCyberpunkTheme();
        }
    }

    /// <summary>
    /// ล้างรายการสเตตัสถาวรแบบเดิมออกทั้งหมด และตั้งค่าสินค้าทั้ง 6 ชนิดตามตารางบาลานซ์ 6.2
    /// </summary>
    private void InitializeBalancedCatalog()
    {
        itemPool.Clear();

        itemPool.Add(new ShopItemData(
            "Heal Pod",
            "Restore 30% of Max HP (+20 Credits per repeat purchase in this room).",
            ShopItemType.HealPod,
            BaseHealPodPrice,
            0.30f));

        ShopItemData newBoonItem = new ShopItemData(
            "New Boon (Lv.1)",
            "Open Bio-Gene selection to acquire or upgrade a Boon.",
            ShopItemType.NewBoon,
            BaseNewBoonPriceFloor1,
            1f);
        newBoonItem.icon = PlayerInventory.GetBoonIcon(BoonItemType.NanoBarrier) ?? (BoonsData.Instance != null ? BoonsData.Instance.nanoBarrierIcon : null);
        itemPool.Add(newBoonItem);

        itemPool.Add(new ShopItemData(
            "Reroll Token",
            "+1 Card Reroll Token for Level-Up or Boon selection.",
            ShopItemType.RerollToken,
            BaseRerollTokenPrice,
            1f));

        itemPool.Add(new ShopItemData(
            "Banish Token",
            "+1 Card Banish Token to remove an unwanted card for the entire run.",
            ShopItemType.BanishToken,
            BaseBanishTokenPrice,
            1f));

        itemPool.Add(new ShopItemData(
            "Overdrive Stim",
            "Temporary Buff: +15% Move Speed & +15% Damage for the NEXT room only (1 Room).",
            ShopItemType.OverdriveStim,
            BaseOverdriveStimPrice,
            0.15f));

        itemPool.Add(new ShopItemData(
            "Reroll Shop",
            "Refresh and roll a new set of Black Market items.",
            ShopItemType.RerollShop,
            BaseRerollShopPrice,
            1f));
    }

    /// <summary>
    /// คำนวณราคาสินค้าตามชั้นปัจจุบัน (Floor 2 คูณ 1.3x, New Boon ชั้น 2 = 590, Heal Pod ซื้อซ้ำ +20 ทุกครั้ง)
    /// </summary>
    public int CalculateEffectivePrice(ShopItemType type)
    {
        int currentFloor = RoomManager.Instance != null ? RoomManager.Instance.CurrentFloor : 1;
        bool isFloor2OrHigher = currentFloor >= 2;
        float floorMultiplier = isFloor2OrHigher ? Floor2PriceMultiplier : 1.0f;

        switch (type)
        {
            case ShopItemType.NewBoon:
                return isFloor2OrHigher ? BaseNewBoonPriceFloor2 : BaseNewBoonPriceFloor1;

            case ShopItemType.HealPod:
                int baseHealCost = BaseHealPodPrice + (healPodPurchasesInCurrentRoom * HealPodRepeatIncrement);
                return Mathf.RoundToInt(baseHealCost * floorMultiplier);

            case ShopItemType.RerollToken:
                return Mathf.RoundToInt(BaseRerollTokenPrice * floorMultiplier);

            case ShopItemType.BanishToken:
                return Mathf.RoundToInt(BaseBanishTokenPrice * floorMultiplier);

            case ShopItemType.OverdriveStim:
                return Mathf.RoundToInt(BaseOverdriveStimPrice * floorMultiplier);

            case ShopItemType.RerollShop:
                return Mathf.RoundToInt(BaseRerollShopPrice * floorMultiplier);

            default:
                return Mathf.RoundToInt(100 * floorMultiplier);
        }
    }

    public void OpenShop()
    {
        Time.timeScale = 0f;
        healPodPurchasesInCurrentRoom = 0;

        InitializeBalancedCatalog();
        ApplyCyberpunkShopTheme();

        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }

        GenerateStock(isReroll: false);
        UpdateShopUI();
    }

    /// <summary>
    /// สุ่มสินค้า 4 ชิ้นสำหรับแผงร้านค้า:
    /// - ครั้งแรกที่เข้าร้าน: วางขาย Heal Pod, New Boon, Reroll Token, Banish Token (หรือทดแทนหาก Boon เต็ม)
    /// - เมื่อ Reroll: สุ่มสินค้า 4 ชิ้นใหม่จากรายการสินค้าทั้งหมด
    /// </summary>
    private void GenerateStock(bool isReroll = false)
    {
        EnsureFourShopSlots();
        currentStock.Clear();

        List<ShopItemData> eligiblePool = BuildEligibleCatalogPool();
        int maxSlots = (shopSlots != null && shopSlots.Length > 0) ? shopSlots.Length : 4;
        List<ShopItemData> selectedItems = new List<ShopItemData>();

        if (!isReroll)
        {
            // ลำดับสินค้าหลัก 4 ชิ้นตามสเปก 6.2: Heal Pod, New Boon, Reroll Token, Banish Token
            ShopItemType[] preferredTypes = new ShopItemType[]
            {
                ShopItemType.HealPod,
                ShopItemType.NewBoon,
                ShopItemType.RerollToken,
                ShopItemType.BanishToken
            };

            foreach (var pType in preferredTypes)
            {
                ShopItemData match = eligiblePool.Find(x => x.itemType == pType);
                if (match != null && selectedItems.Count < maxSlots)
                {
                    selectedItems.Add(match);
                    eligiblePool.Remove(match);
                }
            }
        }

        // เติมเต็มช่องที่เหลือด้วยการสุ่มจาก eligiblePool
        while (selectedItems.Count < maxSlots && eligiblePool.Count > 0)
        {
            int randIdx = Random.Range(0, eligiblePool.Count);
            selectedItems.Add(eligiblePool[randIdx]);
            eligiblePool.RemoveAt(randIdx);
        }

        for (int i = 0; i < maxSlots; i++)
        {
            if (i < selectedItems.Count)
            {
                ShopItemData template = selectedItems[i];
                int dynamicPrice = CalculateEffectivePrice(template.itemType);
                ShopItemData runtimeItem = template.CloneWithPrice(dynamicPrice);
                currentStock.Add(runtimeItem);

                if (shopSlots != null && i < shopSlots.Length && shopSlots[i] != null)
                {
                    shopSlots[i].gameObject.SetActive(true);
                    shopSlots[i].SetupSlot(runtimeItem, i, BuyItem);
                }
            }
            else if (shopSlots != null && i < shopSlots.Length && shopSlots[i] != null)
            {
                shopSlots[i].gameObject.SetActive(false);
            }
        }
    }

    private List<ShopItemData> BuildEligibleCatalogPool()
    {
        List<ShopItemData> pool = new List<ShopItemData>();
        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        bool allBoonsMaxed = inventory != null && inventory.AreAllBoonsMaxed();

        for (int i = 0; i < itemPool.Count; i++)
        {
            ShopItemData item = itemPool[i];
            if (item == null) continue;

            // หาก Boon เต็มครบ 3 ช่องและ Lv.2 Max หมดแล้ว ไม่วางขาย New Boon
            if (item.itemType == ShopItemType.NewBoon && allBoonsMaxed)
                continue;

            pool.Add(item);
        }

        return pool;
    }

    public void BuyItem(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= currentStock.Count) return;
        if (shopSlots != null && slotIndex < shopSlots.Length &&
            shopSlots[slotIndex] != null && shopSlots[slotIndex].IsPurchased) return;

        ShopItemData item = currentStock[slotIndex];
        if (item == null) return;

        int currentPrice = CalculateEffectivePrice(item.itemType);
        item.price = currentPrice;

        GameOverManager run = GameOverManager.Instance;
        if (run == null || !run.TrySpendRunCredits(currentPrice))
        {
            Debug.LogWarning($"[ShopManager] Not enough Nano-Credits! Needed: {currentPrice}, Held: {(run != null ? run.RunCredits : 0)}");
            return;
        }

        PlayPurchaseSound();

        // กรณีซื้อ Reroll Shop: สุ่มสินค้าในร้านใหม่ทันที
        if (item.itemType == ShopItemType.RerollShop)
        {
            GenerateStock(isReroll: true);
            UpdateShopUI();
            Debug.Log($"[ShopManager] Shop rerolled for {currentPrice} Credits!");
            return;
        }

        // กรณีซื้อ Heal Pod: ฟื้น 30% Max HP และอนุญาตให้ซื้อซ้ำในห้องเดิมได้โดยราคา +20 Credits ทุกครั้ง
        if (item.itemType == ShopItemType.HealPod)
        {
            ApplyItemEffect(item);
            healPodPurchasesInCurrentRoom++;

            int nextHealPrice = CalculateEffectivePrice(ShopItemType.HealPod);
            item.price = nextHealPrice;
            if (shopSlots != null && slotIndex < shopSlots.Length && shopSlots[slotIndex] != null)
            {
                shopSlots[slotIndex].SetupSlot(item, slotIndex, BuyItem);
            }

            RefreshAllHealPodPricesInStock();
            UpdateShopUI();
            return;
        }

        ApplyItemEffect(item);

        if (shopSlots != null && slotIndex < shopSlots.Length && shopSlots[slotIndex] != null)
        {
            shopSlots[slotIndex].MarkAsSoldOut();
        }

        UpdateShopUI();
    }

    private void RefreshAllHealPodPricesInStock()
    {
        int updatedHealPrice = CalculateEffectivePrice(ShopItemType.HealPod);
        for (int i = 0; i < currentStock.Count; i++)
        {
            if (currentStock[i] != null && currentStock[i].itemType == ShopItemType.HealPod)
            {
                currentStock[i].price = updatedHealPrice;
                if (shopSlots != null && i < shopSlots.Length && shopSlots[i] != null && !shopSlots[i].IsPurchased)
                {
                    shopSlots[i].SetupSlot(currentStock[i], i, BuyItem);
                }
            }
        }
    }

    /// <summary>
    /// ปุ่มแยกสำหรับกด Reroll Shop โดยตรง (ราคา 40 Credits หรือ 52 Credits ใน Floor 2)
    /// </summary>
    public void TryRerollShopFromButton()
    {
        int rerollCost = CalculateEffectivePrice(ShopItemType.RerollShop);
        GameOverManager run = GameOverManager.Instance;
        if (run == null || !run.TrySpendRunCredits(rerollCost))
        {
            Debug.LogWarning($"[ShopManager] Not enough Nano-Credits to Reroll Shop! Cost: {rerollCost}");
            return;
        }

        PlayPurchaseSound();
        GenerateStock(isReroll: true);
        UpdateShopUI();
        Debug.Log($"[ShopManager] Rerolled shop stock for {rerollCost} Credits!");
    }

    private void ApplyItemEffect(ShopItemData item)
    {
        GameObject player = GameObject.FindWithTag("Player");
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        PlayerStats stats = PlayerStats.Instance != null ? PlayerStats.Instance : (player != null ? player.GetComponent<PlayerStats>() : null);

        switch (item.itemType)
        {
            case ShopItemType.NewBoon:
                // ซ่อนหน้าร้านค้าชั่วคราวเพื่อเปิดหน้าต่างเลือก Boon
                if (shopPanel != null)
                {
                    shopPanel.SetActive(false);
                }

                if (LevelUpManager.Instance != null)
                {
                    LevelUpManager.Instance.ShowBoonSelectionFromShop();
                }
                else if (BoonManager.Instance != null)
                {
                    BoonManager.Instance.ShowBoonSelectionFromShop();
                }
                else
                {
                    PlayerInventory inv = PlayerInventory.GetOrCreate();
                    if (inv != null)
                    {
                        var cards = inv.GenerateWeightedBoonCards(1);
                        if (cards.Count > 0) inv.AddOrUpgradeBoon(cards[0].boonItemType);
                    }
                    OnBoonSelectionClosed();
                }
                break;

            case ShopItemType.HealPod:
                if (health != null)
                {
                    float healAmount = health.MaxHealth * 0.30f;
                    health.Heal(healAmount);
                    Debug.Log($"[ShopManager] Heal Pod restored +{healAmount:F1} HP (30% Max HP).");
                }
                break;

            case ShopItemType.RerollToken:
                if (LevelUpManager.Instance != null)
                {
                    LevelUpManager.Instance.AddRerollTokens(1);
                }
                break;

            case ShopItemType.BanishToken:
                if (LevelUpManager.Instance != null)
                {
                    LevelUpManager.Instance.AddBanishTokens(1);
                }
                break;

            case ShopItemType.OverdriveStim:
                if (stats != null)
                {
                    stats.QueueOverdriveStimForNextRoom(0.15f);
                }
                break;

            case ShopItemType.RerollShop:
                GenerateStock(isReroll: true);
                break;
        }

        Debug.Log($"[ShopManager] Purchased item: {item.itemName} ({item.price} Credits)");
    }

    private void PlayPurchaseSound()
    {
        AudioClip clip = purchaseSFX != null ? purchaseSFX : Resources.Load<AudioClip>("SFX/Purchase");
        if (AudioManager.Instance != null && clip != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }
    }

    public void UpdateShopUI()
    {
        int currentCurrency = GameOverManager.Instance != null ? GameOverManager.Instance.RunCredits : 0;

        if (currencyDisplay != null)
        {
            currencyDisplay.text = $"CREDITS: {currentCurrency}";
            currencyDisplay.color = GoldCredits;
        }

        int rerollShopCost = CalculateEffectivePrice(ShopItemType.RerollShop);
        if (rerollShopButton != null)
        {
            rerollShopButton.interactable = currentCurrency >= rerollShopCost;
        }
        if (rerollShopButtonText != null)
        {
            rerollShopButtonText.text = $"Reroll Shop ({rerollShopCost} Credits)";
        }

        if (shopSlots != null)
        {
            foreach (var slot in shopSlots)
            {
                if (slot != null && slot.gameObject.activeSelf)
                {
                    slot.UpdateAffordability(currentCurrency);
                }
            }
        }
    }

    /// <summary>
    /// ถูกเรียกเมื่อหน้าต่างเลือก Boon ปิดลง เพื่อเปิดหน้าร้าน Black Market กลับมาแสดงผลต่อตามเดิม
    /// </summary>
    public void OnBoonSelectionClosed()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(true);
        }
        Time.timeScale = 0f; // คงการหยุดเวลาขณะอยู่ในร้านค้า
        UpdateShopUI();
        Debug.Log("[ShopManager] Returned to Black Market after Boon selection.");
    }

    public void LeaveShop()
    {
        if (shopPanel != null)
        {
            shopPanel.SetActive(false);
        }

        Time.timeScale = 1f;

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ProceedToNextRoom();
        }
    }
}
