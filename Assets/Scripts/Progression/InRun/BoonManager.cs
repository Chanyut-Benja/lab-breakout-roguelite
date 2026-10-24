using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BoonManager : MonoBehaviour
{
    public static BoonManager Instance { get; private set; }

    public event System.Action<BoonSlotEntry> OnBoonAdded;
    public event System.Action<BoonSlotEntry> OnBoonUpgraded;
    public event System.Action<BoonSlotEntry> OnBoonResonated;

    public static event System.Action<BoonSlotEntry> OnBoonAddedStatic;
    public static event System.Action<BoonSlotEntry> OnBoonUpgradedStatic;
    public static event System.Action<BoonSlotEntry> OnBoonResonatedStatic;

    [Header("Boon Database (5 New Boons)")]
    [SerializeField] private List<BoonData> boonPool = new List<BoonData>();

    [Header("UI Card References")]
    [SerializeField] private GameObject boonPanel;
    [SerializeField] private Text[] cardTitleTexts; // 3 Cards
    [SerializeField] private Text[] cardDescTexts;  // 3 Cards
    [SerializeField] private Image[] cardIconImages; // 3 Cards

    private readonly List<LevelUpUpgradeData> currentOfferedSlotBoons = new List<LevelUpUpgradeData>();
    private readonly List<BoonData> currentOfferedBoons = new List<BoonData>();
    private bool isFromShop = false;

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

        HookInventoryEvents();
        InitializeNewBoonPool();
    }

    private void OnDestroy()
    {
        UnhookInventoryEvents();
    }

    private void HookInventoryEvents()
    {
        PlayerInventory.OnBoonAddedStatic -= HandleBoonAdded;
        PlayerInventory.OnBoonAddedStatic += HandleBoonAdded;
        PlayerInventory.OnBoonUpgradedStatic -= HandleBoonUpgraded;
        PlayerInventory.OnBoonUpgradedStatic += HandleBoonUpgraded;
        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonated;
        PlayerInventory.OnBoonResonatedStatic += HandleBoonResonated;
    }

    private void UnhookInventoryEvents()
    {
        PlayerInventory.OnBoonAddedStatic -= HandleBoonAdded;
        PlayerInventory.OnBoonUpgradedStatic -= HandleBoonUpgraded;
        PlayerInventory.OnBoonResonatedStatic -= HandleBoonResonated;
    }

    private void HandleBoonAdded(BoonSlotEntry entry)
    {
        OnBoonAdded?.Invoke(entry);
        OnBoonAddedStatic?.Invoke(entry);
    }

    private void HandleBoonUpgraded(BoonSlotEntry entry)
    {
        OnBoonUpgraded?.Invoke(entry);
        OnBoonUpgradedStatic?.Invoke(entry);
    }

    private void HandleBoonResonated(BoonSlotEntry entry)
    {
        OnBoonResonated?.Invoke(entry);
        OnBoonResonatedStatic?.Invoke(entry);
    }

    /// <summary>
    /// กำหนดชุด Boon ใหม่ 5 ชนิด (Max Lv. 2) ตามเอกสารบาลานซ์:
    /// - Kinetic Overdrive: เพิ่ม Atk Spd +15%/+30%, ปลดล็อก Resonance กับ Nano-Blade Evolved
    /// - Toxic Catalyst: เพิ่ม DoT Dmg +25%/+50%, ขยายผลพิษระเบิดกับ Bio-Needler Evolved
    /// - Plasma Accelerator: เพิ่ม Projectile Speed +30% และรัศมีระเบิด, Resonance กับ Plasma Blaster Evolved
    /// - Cryo Condenser: ศัตรูติด Slow 20%/35% และเพิ่ม Freeze Duration, Resonance กับสายน้ำแข็ง
    /// - Nanite Aegis: เพิ่ม Armor +8/+15 และ HP Regen 1.5/s, Resonance ปล่อยคลื่นสะท้อนความเสียหาย
    /// </summary>
    public void InitializeNewBoonPool()
    {
        boonPool.Clear();
        boonPool.AddRange(GetDefaultNewBoonList());
    }

    public static List<BoonData> GetDefaultNewBoonList()
    {
        return new List<BoonData>
        {
            new BoonData(
                "Nano-Barrier",
                "สร้างเกราะ 20 ฟื้นทุก 15 วินาที",
                BoonType.NanoBarrier,
                BoonItemType.NanoBarrier,
                20f,
                PlayerInventory.GetBoonIcon(BoonItemType.NanoBarrier)),
            new BoonData(
                "Toxic Spore",
                "หมอกสปอร์พิษ 15/s รัศมี 3m",
                BoonType.ToxicSpore,
                BoonItemType.ToxicSpore,
                15f,
                PlayerInventory.GetBoonIcon(BoonItemType.ToxicSpore)),
            new BoonData(
                "Flame Ring",
                "วงแหวนไฟรอบตัวเผาผลาญ 25/s",
                BoonType.FlameRing,
                BoonItemType.FlameRing,
                25f,
                PlayerInventory.GetBoonIcon(BoonItemType.FlameRing)),
            new BoonData(
                "Static Aura",
                "ผ่าสายฟ้า 1 ตัว ทุกระยะเดิน 5m (ดาเมจ 40)",
                BoonType.StaticAura,
                BoonItemType.StaticAura,
                40f,
                PlayerInventory.GetBoonIcon(BoonItemType.StaticAura)),
            new BoonData(
                "Magnetic Pulse",
                "ปล่อยพัลส์แม่เหล็กทุก 4 วินาที สโลว์ศัตรู 30%",
                BoonType.MagneticPulse,
                BoonItemType.MagneticPulse,
                30f,
                PlayerInventory.GetBoonIcon(BoonItemType.MagneticPulse))
        };
    }

    public void ResolveReferences()
    {
        if (boonPanel == null)
        {
#if UNITY_2023_1_OR_NEWER
            var ui = FindFirstObjectByType<BoonSelectionUI>();
#else
            var ui = FindObjectOfType<BoonSelectionUI>();
#endif
            if (ui != null) boonPanel = ui.gameObject;
        }

        if (boonPanel != null)
        {
            if (cardIconImages == null || cardIconImages.Length < 3 || cardTitleTexts == null || cardDescTexts == null)
            {
                Button[] buttons = boonPanel.GetComponentsInChildren<Button>(true);
                List<Button> cardBtns = new List<Button>();
                foreach (var b in buttons)
                {
                    string bName = b.name.ToLowerInvariant();
                    if (!bName.Contains("reroll") && !bName.Contains("banish") && !bName.Contains("close") && !bName.Contains("exit"))
                        cardBtns.Add(b);
                }

                int c = Mathf.Min(3, cardBtns.Count);
                if (cardTitleTexts == null || cardTitleTexts.Length < c) cardTitleTexts = new Text[c];
                if (cardDescTexts == null || cardDescTexts.Length < c) cardDescTexts = new Text[c];
                if (cardIconImages == null || cardIconImages.Length < c) cardIconImages = new Image[c];

                for (int i = 0; i < c; i++)
                {
                    Transform btnTr = cardBtns[i].transform;
                    if (cardIconImages[i] == null)
                    {
                        Transform iconTr = btnTr.Find("Icon") ?? btnTr.Find("IconImage");
                        if (iconTr != null) cardIconImages[i] = iconTr.GetComponent<Image>();
                    }
                    if (cardTitleTexts[i] == null)
                    {
                        Transform tTr = btnTr.Find("Title") ?? btnTr.Find("TitleText");
                        if (tTr != null) cardTitleTexts[i] = tTr.GetComponent<Text>();
                    }
                    if (cardDescTexts[i] == null)
                    {
                        Transform dTr = btnTr.Find("Description") ?? btnTr.Find("DescText");
                        if (dTr != null) cardDescTexts[i] = dTr.GetComponent<Text>();
                    }
                }
            }
        }
    }

    public void EnsureTopSortingOrder()
    {
        if (boonPanel == null) return;
        boonPanel.transform.SetAsLastSibling();
        Canvas panelCanvas = boonPanel.GetComponent<Canvas>() ?? boonPanel.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 100;
        if (boonPanel.GetComponent<GraphicRaycaster>() == null)
        {
            boonPanel.AddComponent<GraphicRaycaster>();
        }
    }

    public void ShowBoonSelectionFromShop()
    {
        isFromShop = true;
        ShowBoonSelection();
    }

    public void ShowBoonSelection()
    {
        ResolveReferences();
        EnsureTopSortingOrder();
        Time.timeScale = 0f;
        if (boonPanel != null) boonPanel.SetActive(true);

        currentOfferedSlotBoons.Clear();
        currentOfferedBoons.Clear();

        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory != null)
        {
            List<LevelUpUpgradeData> weightedBoons = inventory.GenerateWeightedBoonCards(3);
            for (int i = 0; i < weightedBoons.Count; i++)
            {
                currentOfferedSlotBoons.Add(weightedBoons[i]);

                if (cardTitleTexts != null && i < cardTitleTexts.Length && cardTitleTexts[i] != null)
                    cardTitleTexts[i].text = weightedBoons[i].upgradeTitle;

                if (cardDescTexts != null && i < cardDescTexts.Length && cardDescTexts[i] != null)
                    cardDescTexts[i].text = weightedBoons[i].description;

                if (cardIconImages != null && i < cardIconImages.Length && cardIconImages[i] != null)
                {
                    Sprite s = weightedBoons[i].icon ?? PlayerInventory.GetBoonIcon(weightedBoons[i].boonItemType);
                    cardIconImages[i].sprite = s;
                    cardIconImages[i].color = Color.white;
                    cardIconImages[i].preserveAspect = true;
                    cardIconImages[i].gameObject.SetActive(s != null);
                }
            }

            if (currentOfferedSlotBoons.Count > 0)
            {
                return;
            }
        }

        // Fallback: ดึงเฉพาะ Boon ใหม่ทั้ง 5 ตัวนี้เท่านั้น
        if (boonPool.Count == 0)
        {
            InitializeNewBoonPool();
        }

        List<BoonData> tempPool = new List<BoonData>(boonPool);
        for (int i = 0; i < 3; i++)
        {
            if (tempPool.Count == 0) break;

            int randomIndex = Random.Range(0, tempPool.Count);
            BoonData selectedBoon = tempPool[randomIndex];
            currentOfferedBoons.Add(selectedBoon);
            tempPool.RemoveAt(randomIndex);

            if (cardTitleTexts != null && i < cardTitleTexts.Length && cardTitleTexts[i] != null)
                cardTitleTexts[i].text = selectedBoon.boonTitle;

            if (cardDescTexts != null && i < cardDescTexts.Length && cardDescTexts[i] != null)
                cardDescTexts[i].text = selectedBoon.description;

            if (cardIconImages != null && i < cardIconImages.Length && cardIconImages[i] != null)
            {
                Sprite s = selectedBoon.icon ?? PlayerInventory.GetBoonIcon(selectedBoon.boonItemType);
                cardIconImages[i].sprite = s;
                cardIconImages[i].color = Color.white;
                cardIconImages[i].preserveAspect = true;
                cardIconImages[i].gameObject.SetActive(s != null);
            }
        }
    }

    public void ApplyChosenBoon(BoonData boon)
    {
        if (boon == null) return;
        ApplyBoonEffect(boon);

        if (boonPanel != null) boonPanel.SetActive(false);

        if (isFromShop)
        {
            isFromShop = false;
            Time.timeScale = 0f;
            ShopManager.Instance?.OnBoonSelectionClosed();
            return;
        }

        Time.timeScale = 1f;

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ProceedToNextRoom();
        }
    }

    public void ApplyChosenUpgradeData(LevelUpUpgradeData upgradeData)
    {
        if (upgradeData == null) return;
        PlayerInventory.GetOrCreate()?.AddOrUpgradeBoon(upgradeData.boonItemType);

        if (boonPanel != null) boonPanel.SetActive(false);

        if (isFromShop)
        {
            isFromShop = false;
            Time.timeScale = 0f;
            ShopManager.Instance?.OnBoonSelectionClosed();
            return;
        }

        Time.timeScale = 1f;

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ProceedToNextRoom();
        }
    }

    public void SelectBoon(int index)
    {
        if (currentOfferedSlotBoons.Count > 0)
        {
            if (index < 0 || index >= currentOfferedSlotBoons.Count) return;
            LevelUpUpgradeData chosenSlotBoon = currentOfferedSlotBoons[index];
            PlayerInventory.GetOrCreate()?.AddOrUpgradeBoon(chosenSlotBoon.boonItemType);
        }
        else
        {
            if (index < 0 || index >= currentOfferedBoons.Count) return;
            BoonData chosenBoon = currentOfferedBoons[index];
            ApplyBoonEffect(chosenBoon);
        }

        if (boonPanel != null) boonPanel.SetActive(false);

        if (isFromShop)
        {
            isFromShop = false;
            Time.timeScale = 0f;
            ShopManager.Instance?.OnBoonSelectionClosed();
            return;
        }

        Time.timeScale = 1f;

        if (RoomManager.Instance != null)
        {
            RoomManager.Instance.ProceedToNextRoom();
        }
    }

    private void ApplyBoonEffect(BoonData boon)
    {
        if (boon == null) return;

        PlayerStats stats = PlayerStats.Instance;
        if (stats == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null) stats = player.GetComponent<PlayerStats>();
        }

        if (stats != null)
        {
            stats.ApplyBoon(boon);
        }

        Debug.Log($"[BoonManager] Applied Boon: {boon.boonTitle} (+{boon.value})");
    }
}
