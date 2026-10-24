using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dark Sci-Fi / Cyberpunk Black Market card slot.
/// Manages card dimensions (218x375), 3px purple border, 20sp item title, 15sp description,
/// 20sp bold gold price, and interactive bright purple / disabled grey buy button with SOLD OUT overlay.
/// </summary>
public class ShopSlotUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Text titleText;
    [SerializeField] private Text descriptionText;
    [SerializeField] private Text priceText;
    [SerializeField] private Image iconImage;
    [SerializeField] private Button buyButton;
    [SerializeField] private Text buyButtonText;
    [SerializeField] private GameObject soldOutOverlay;

    private int mySlotIndex = -1;
    private ShopItemData myItem;
    private bool isPurchased = false;

    public void SetupSlot(ShopItemData item, int slotIndex, System.Action<int> onBuyCallback)
    {
        myItem = item;
        mySlotIndex = slotIndex;
        isPurchased = false;

        ApplyCyberpunkTheme();

        if (titleText != null)
        {
            titleText.text = item.itemName;
            titleText.raycastTarget = false;
        }
        if (descriptionText != null)
        {
            descriptionText.text = item.description;
            descriptionText.raycastTarget = false;
        }
        if (priceText != null)
        {
            priceText.text = $"{item.price} Credits";
            priceText.raycastTarget = false;
        }

        if (iconImage != null)
        {
            Sprite s = item.icon;
            if (s == null)
            {
                if (item.itemType == ShopItemType.NewBoon)
                {
                    s = PlayerInventory.GetBoonIcon(BoonItemType.NanoBarrier) ?? (BoonsData.Instance != null ? BoonsData.Instance.nanoBarrierIcon : null);
                }
                else if (BlackMarketData.Instance != null)
                {
                    s = BlackMarketData.Instance.GetIcon(item.itemType);
                }
            }
            iconImage.sprite = s;
            iconImage.color = Color.white;
            iconImage.raycastTarget = false;
            iconImage.preserveAspect = true;
            iconImage.gameObject.SetActive(s != null);
        }

        if (soldOutOverlay != null)
        {
            soldOutOverlay.SetActive(false);
        }

        if (buyButtonText != null)
        {
            buyButtonText.text = "BUY";
            buyButtonText.color = Color.white;
            buyButtonText.raycastTarget = false;
        }

        if (buyButton != null)
        {
            buyButton.interactable = true;
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(() => onBuyCallback?.Invoke(mySlotIndex));
        }
    }

    public void ApplyCyberpunkTheme()
    {
        Font sciFiFont = InGameHUDController.GetDefaultFont();

        // 1. Card Dimensions: 218 x 375
        RectTransform cardRect = GetComponent<RectTransform>();
        if (cardRect != null)
        {
            cardRect.sizeDelta = new Vector2(218f, 375f);
            cardRect.localScale = Vector3.one;
        }

        // 2. Card Outer Border Image (#9D4EDD Neon Purple, 3px)
        Image borderImg = GetComponent<Image>();
        if (borderImg == null) borderImg = gameObject.AddComponent<Image>();
        borderImg.sprite = null;
        borderImg.color = ShopManager.NeonPurpleBorder; // #9D4EDD
        borderImg.raycastTarget = false;

        // 3. Card Inner Background (#161322 Dark Grey-Purple, inset 3px)
        Transform innerBgTrans = transform.Find("CardInnerBg");
        if (innerBgTrans == null)
        {
            GameObject bgGo = new GameObject("CardInnerBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(transform, false);
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
        innerBgImg.color = ShopManager.CardBgDark; // #161322
        innerBgImg.raycastTarget = false;

        // 4. Hover effect: 1.04x scale on card hover
        if (GetComponent<CardHoverEffect>() == null)
        {
            gameObject.AddComponent<CardHoverEffect>();
        }

        // 5. Title Text: 20sp Bold, White, Center
        if (titleText != null)
        {
            RectTransform tRect = titleText.GetComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.5f, 0.5f);
            tRect.anchorMax = new Vector2(0.5f, 0.5f);
            tRect.pivot = new Vector2(0.5f, 0.5f);
            tRect.anchoredPosition = new Vector2(0f, 142f);
            tRect.sizeDelta = new Vector2(195f, 48f);

            titleText.font = sciFiFont;
            titleText.fontSize = 20;
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

        // 6. Icon Frame & Icon Image: 64x64 at (0, 75)
        Transform iconFrameTrans = transform.Find("IconSlotFrame");
        if (iconFrameTrans == null)
        {
            GameObject frameGo = new GameObject("IconSlotFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(transform, false);
            iconFrameTrans = frameGo.transform;
        }
        RectTransform frameRect = iconFrameTrans.GetComponent<RectTransform>();
        frameRect.anchorMin = new Vector2(0.5f, 0.5f);
        frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.pivot = new Vector2(0.5f, 0.5f);
        frameRect.anchoredPosition = new Vector2(0f, 75f);
        frameRect.sizeDelta = new Vector2(68f, 68f);

        Image frameImg = iconFrameTrans.GetComponent<Image>();
        frameImg.sprite = null;
        frameImg.color = new Color32(0x26, 0x1C, 0x3B, 0xC0);
        frameImg.raycastTarget = false;

        if (iconImage != null)
        {
            iconImage.transform.SetSiblingIndex(iconFrameTrans.GetSiblingIndex() + 1);
            RectTransform iRect = iconImage.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0.5f, 0.5f);
            iRect.anchorMax = new Vector2(0.5f, 0.5f);
            iRect.pivot = new Vector2(0.5f, 0.5f);
            iRect.anchoredPosition = new Vector2(0f, 75f);
            iRect.sizeDelta = new Vector2(60f, 60f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
        }

        // 7. Description Text: 15sp, #C8C2D8, Center
        if (descriptionText != null)
        {
            RectTransform dRect = descriptionText.GetComponent<RectTransform>();
            dRect.anchorMin = new Vector2(0.5f, 0.5f);
            dRect.anchorMax = new Vector2(0.5f, 0.5f);
            dRect.pivot = new Vector2(0.5f, 0.5f);
            dRect.anchoredPosition = new Vector2(0f, -12f);
            dRect.sizeDelta = new Vector2(195f, 96f);

            descriptionText.font = sciFiFont;
            descriptionText.fontSize = 15;
            descriptionText.fontStyle = FontStyle.Normal;
            descriptionText.color = new Color32(0xC8, 0xC2, 0xD8, 0xFF);
            descriptionText.alignment = TextAnchor.MiddleCenter;
            descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
            descriptionText.raycastTarget = false;
        }

        // 8. Price Text: 20sp Bold, Gold/Orange #FFD166, Center
        if (priceText != null)
        {
            RectTransform prRect = priceText.GetComponent<RectTransform>();
            prRect.anchorMin = new Vector2(0.5f, 0.5f);
            prRect.anchorMax = new Vector2(0.5f, 0.5f);
            prRect.pivot = new Vector2(0.5f, 0.5f);
            prRect.anchoredPosition = new Vector2(0f, -88f);
            prRect.sizeDelta = new Vector2(195f, 30f);

            priceText.font = sciFiFont;
            priceText.fontSize = 20;
            priceText.fontStyle = FontStyle.Bold;
            priceText.color = ShopManager.GoldCredits; // #FFD166
            priceText.alignment = TextAnchor.MiddleCenter;
            priceText.horizontalOverflow = HorizontalWrapMode.Overflow;
            priceText.verticalOverflow = VerticalWrapMode.Overflow;
            priceText.raycastTarget = false;

            Shadow prShadow = priceText.GetComponent<Shadow>();
            if (prShadow == null) prShadow = priceText.gameObject.AddComponent<Shadow>();
            prShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            prShadow.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // 9. Buy Button: (170 x 42) at (0, -145)
        if (buyButton != null)
        {
            RectTransform bRect = buyButton.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 0.5f);
            bRect.anchorMax = new Vector2(0.5f, 0.5f);
            bRect.pivot = new Vector2(0.5f, 0.5f);
            bRect.anchoredPosition = new Vector2(0f, -145f);
            bRect.sizeDelta = new Vector2(170f, 42f);

            Image bImg = buyButton.GetComponent<Image>();
            if (bImg != null)
            {
                bImg.sprite = null;
                bImg.color = ShopManager.BuyButtonPurple;
            }

            var colors = buyButton.colors;
            colors.normalColor = ShopManager.BuyButtonPurple;
            colors.highlightedColor = new Color32(0xC7, 0x7D, 0xFF, 0xFF);
            colors.pressedColor = new Color32(0x7B, 0x2C, 0xBF, 0xFF);
            colors.disabledColor = ShopManager.BuyButtonDisabled;
            colors.colorMultiplier = 1f;
            buyButton.colors = colors;
        }

        if (buyButtonText != null)
        {
            buyButtonText.font = sciFiFont;
            buyButtonText.fontSize = 16;
            buyButtonText.fontStyle = FontStyle.Bold;
            buyButtonText.color = Color.white;
            buyButtonText.alignment = TextAnchor.MiddleCenter;
            buyButtonText.raycastTarget = false;
        }

        // 10. Sold Out Overlay
        if (soldOutOverlay != null)
        {
            RectTransform soRect = soldOutOverlay.GetComponent<RectTransform>();
            soRect.anchorMin = new Vector2(0.5f, 0.5f);
            soRect.anchorMax = new Vector2(0.5f, 0.5f);
            soRect.pivot = new Vector2(0.5f, 0.5f);
            soRect.anchoredPosition = Vector2.zero;
            soRect.sizeDelta = new Vector2(200f, 42f);
            soRect.localEulerAngles = new Vector3(0f, 0f, 25f); // Sleek diagonal banner

            Text soText = soldOutOverlay.GetComponent<Text>();
            if (soText != null)
            {
                soText.font = sciFiFont;
                soText.fontSize = 24;
                soText.fontStyle = FontStyle.Bold;
                soText.color = ShopManager.SoldOutRed; // #FF2A6D Neon Pink-Red
                soText.alignment = TextAnchor.MiddleCenter;
                soText.horizontalOverflow = HorizontalWrapMode.Overflow;
                soText.verticalOverflow = VerticalWrapMode.Overflow;
                soText.raycastTarget = false;

                Shadow soShadow = soldOutOverlay.GetComponent<Shadow>();
                if (soShadow == null) soShadow = soldOutOverlay.AddComponent<Shadow>();
                soShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                soShadow.effectDistance = new Vector2(2f, -2f);
            }
        }
    }

    public void UpdateAffordability(int currentCurrency)
    {
        if (isPurchased)
        {
            if (buyButton != null) buyButton.interactable = false;
            if (buyButtonText != null) buyButtonText.text = "SOLD OUT";
            return;
        }

        if (buyButton != null && myItem != null)
        {
            bool canAfford = currentCurrency >= myItem.price;
            buyButton.interactable = canAfford;

            if (buyButtonText != null)
            {
                buyButtonText.text = canAfford ? "BUY" : "LACK CREDITS";
                buyButtonText.color = canAfford ? Color.white : new Color(0.7f, 0.7f, 0.7f, 0.5f);
            }
        }
    }

    public void MarkAsSoldOut()
    {
        isPurchased = true;

        if (soldOutOverlay != null)
        {
            soldOutOverlay.SetActive(true);
            soldOutOverlay.transform.SetAsLastSibling();
        }

        if (buyButtonText != null)
        {
            buyButtonText.text = "SOLD OUT";
            buyButtonText.color = new Color32(0xFF, 0x44, 0x66, 0xFF);
        }

        if (buyButton != null)
        {
            buyButton.interactable = false;
        }
    }

    public bool IsPurchased => isPurchased;
    public ShopItemData Item => myItem;
}
