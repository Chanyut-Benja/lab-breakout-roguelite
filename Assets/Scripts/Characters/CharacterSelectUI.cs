using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Character Select UI: จัดการหน้าเลือกตัวละคร 3 ตัว (N-01, N-02, N-03) ในธีม Dark Cyber-Lab มินิมอล
/// - Title "OPERATIVE SELECTION": ขนาด Font 44sp ตัวหนา
/// - การ์ดตัวละคร 3 ใบขนาดใหญ่เต็มตา: กว้าง 420px สูง 680px (ระยะห่าง 30px)
/// - Font ภายในการ์ด: ชื่อตัวละคร 30sp, สเตตัส 22sp, คำอธิบายสกิล 18sp
/// - ปุ่ม [SELECT / DEPLOY]: สูง 64px กว้าง 380px เต็มการ์ด Font 22sp
/// - ขยายปุ่ม [<], [>], และ [< BACK] ให้ใหญ่ขึ้นตามสัดส่วน
/// </summary>
public class CharacterSelectUI : MonoBehaviour
{
    // Character Accent Colors
    public static readonly Color VanguardGreen = new Color(0f, 0.9f, 0.46f, 1f);     // #00E676 (Green)
    public static readonly Color PsionicRed = new Color(1f, 0.2f, 0.4f, 1f);          // #FF3366 (Red/Crimson)
    public static readonly Color CyberSwiftAmber = new Color(1f, 0.7f, 0f, 1f);       // #FFB300 (Amber/Orange-Yellow)
    public static readonly Color CardBgDark = new Color(0.06f, 0.08f, 0.11f, 1f);    // #10141D
    public static readonly Color BoxContainerBg = new Color(0.08f, 0.11f, 0.16f, 0.95f); // #151D2A
    public static readonly Color NeonCyan = new Color(0f, 0.9f, 1f, 1f);              // #00E5FF

    [Header("Legacy Slot References (Backward Compatibility)")]
    [SerializeField] private Image leftSlotImage;
    [SerializeField] private Image centerSlotImage;
    [SerializeField] private Image rightSlotImage;
    [SerializeField] private Text characterNameText;
    [SerializeField] private Text statsText;
    [SerializeField] private Button selectOrUnlockButton;
    [SerializeField] private Text buttonText;
    [SerializeField] private Text currentRPText;

    private int currentIndex = 0;

    private void Awake()
    {
        ApplyCyberLabCharacterSelectTheme();
    }

    private void OnEnable()
    {
        ApplyCyberLabCharacterSelectTheme();
    }

    private void Start()
    {
        ApplyCyberLabCharacterSelectTheme();
    }

    /// <summary>
    /// ปรับโครงสร้างหน้าเลือกตัวละคร 3 ตัวแนวนอน (420x680px) ธีม Dark Cyber-Lab มินิมอล
    /// </summary>
    [ContextMenu("Apply Character Select Theme")]
    public void ApplyCyberLabCharacterSelectTheme()
    {
        Font sciFiFont = InGameHUDController.GetDefaultFont();

        // 1. ปิด RaycastTarget บน Panel พื้นหลัง
        Image panelImg = GetComponent<Image>();
        if (panelImg != null)
        {
            panelImg.raycastTarget = false;
        }

        // 2. ปรับแต่ง Title ด้านบน (Font 44sp ตัวหนา)
        SetupTitleText(sciFiFont);

        // 3. ปรับแต่งปุ่ม [< BACK] ให้ใหญ่ขึ้น (200x60px)
        SetupBackButton(sciFiFont);

        // 4. ปรับแต่งปุ่ม [<] และ [>] ให้ใหญ่ขึ้น (64x64px)
        SetupNavArrowButtons(sciFiFont);

        // 5. ซ่อน UI เก่าแบบ Carousel ที่ไม่ใช้งาน
        HideLegacyCarouselUI();

        // 6. สร้างหรืออัปเดตการ์ดตัวละคร 3 ใบแนวนอนขนาด 420x680px
        SetupAllThreeCharacterCards(sciFiFont);
    }

    private void SetupTitleText(Font font)
    {
        Transform titleTrans = transform.Find("TitleText (Chracter Select)");
        if (titleTrans == null) titleTrans = transform.Find("TitleText");

        if (titleTrans != null)
        {
            RectTransform tRect = titleTrans.GetComponent<RectTransform>();
            if (tRect != null)
            {
                tRect.anchorMin = new Vector2(0.5f, 1f);
                tRect.anchorMax = new Vector2(0.5f, 1f);
                tRect.pivot = new Vector2(0.5f, 0.5f);
                tRect.anchoredPosition = new Vector2(0f, -45f);
                tRect.sizeDelta = new Vector2(800f, 60f);
            }

            Text tText = titleTrans.GetComponent<Text>();
            if (tText != null)
            {
                tText.text = "OPERATIVE SELECTION";
                tText.font = font;
                tText.fontSize = 44; // Font 44sp ตัวหนา
                tText.fontStyle = FontStyle.Bold;
                tText.color = Color.white;
                tText.alignment = TextAnchor.MiddleCenter;
                tText.raycastTarget = false;

                Outline outline = titleTrans.GetComponent<Outline>();
                if (outline == null) outline = titleTrans.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0.85f, 1f, 0.8f); // Neon cyan outline
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            }
        }
    }

    private void SetupBackButton(Font font)
    {
        Transform backTrans = transform.Find("BackButton");
        if (backTrans != null)
        {
            RectTransform bRect = backTrans.GetComponent<RectTransform>();
            if (bRect != null)
            {
                bRect.anchorMin = new Vector2(0f, 0f);
                bRect.anchorMax = new Vector2(0f, 0f);
                bRect.pivot = new Vector2(0f, 0f);
                bRect.anchoredPosition = new Vector2(60f, 45f); // มุมซ้ายล่าง
                bRect.sizeDelta = new Vector2(200f, 60f);       // ขยายใหญ่ขึ้นตามสัดส่วน
            }

            Image bImg = backTrans.GetComponent<Image>();
            if (bImg != null)
            {
                bImg.color = new Color(0.06f, 0.08f, 0.12f, 1f); // Dark pure black-blue
                bImg.sprite = null;
            }

            Outline outline = backTrans.GetComponent<Outline>();
            if (outline == null) outline = backTrans.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f); // Cyan border
            outline.effectDistance = new Vector2(2f, -2f);

            if (backTrans.GetComponent<CardHoverEffect>() == null)
            {
                backTrans.gameObject.AddComponent<CardHoverEffect>();
            }

            Button btn = backTrans.GetComponent<Button>();
            if (btn != null)
            {
                var colors = btn.colors;
                colors.normalColor = new Color(0.06f, 0.08f, 0.12f, 1f);
                colors.highlightedColor = NeonCyan;
                colors.pressedColor = new Color(0f, 0.6f, 0.8f, 1f);
                colors.colorMultiplier = 1f;
                btn.colors = colors;

                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    if (MainMenuManager.Instance != null)
                    {
                        MainMenuManager.Instance.ShowMainPanel();
                    }
                    else
                    {
                        gameObject.SetActive(false);
                    }
                });
            }

            Text bText = backTrans.GetComponentInChildren<Text>();
            if (bText != null)
            {
                bText.text = "< BACK";
                bText.font = font;
                bText.fontSize = 24; // Font 24sp ตัวหนา
                bText.fontStyle = FontStyle.Bold;
                bText.color = Color.white;
                bText.alignment = TextAnchor.MiddleCenter;
                bText.raycastTarget = false;
            }
        }
    }

    private void SetupNavArrowButtons(Font font)
    {
        // ปุ่ม [<] LeftButton
        Transform leftTrans = transform.Find("LeftButton");
        if (leftTrans == null) leftTrans = transform.Find("PreviousButton");
        if (leftTrans != null)
        {
            leftTrans.gameObject.SetActive(true);
            StyleNavArrowButton(leftTrans, "<", new Vector2(-720f, -10f), font, PreviousCharacter);
        }

        // ปุ่ม [>] RightButton
        Transform rightTrans = transform.Find("RightButton");
        if (rightTrans == null) rightTrans = transform.Find("NextButton");
        if (rightTrans != null)
        {
            rightTrans.gameObject.SetActive(true);
            StyleNavArrowButton(rightTrans, ">", new Vector2(720f, -10f), font, NextCharacter);
        }
    }

    private void StyleNavArrowButton(Transform trans, string symbol, Vector2 pos, Font font, UnityEngine.Events.UnityAction onClick)
    {
        RectTransform rt = trans.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(64f, 64f); // ขยายปุ่มเป็น 64x64px
            rt.localScale = Vector3.one;
        }

        Image img = trans.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            img.sprite = null;
        }

        Outline outline = trans.GetComponent<Outline>();
        if (outline == null) outline = trans.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0.85f, 1f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        if (trans.GetComponent<CardHoverEffect>() == null)
        {
            trans.gameObject.AddComponent<CardHoverEffect>();
        }

        Button btn = trans.GetComponent<Button>();
        if (btn != null)
        {
            var colors = btn.colors;
            colors.normalColor = new Color(0.06f, 0.08f, 0.12f, 1f);
            colors.highlightedColor = NeonCyan;
            colors.pressedColor = new Color(0f, 0.6f, 0.8f, 1f);
            colors.colorMultiplier = 1f;
            btn.colors = colors;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }

        Text text = trans.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = symbol;
            text.font = font;
            text.fontSize = 32; // Font 32sp คมชัด
            text.fontStyle = FontStyle.Bold;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }
    }

    private void HideLegacyCarouselUI()
    {
        string[] legacyNames = new string[]
        {
            "SelectOrUnlockButton",
            "LeftSlotImage", "CenterSlotImage", "RightSlotImage",
            "CharacterNameText", "StatsText", "CurrentRPText"
        };

        foreach (string name in legacyNames)
        {
            Transform t = transform.Find(name);
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }

        if (leftSlotImage != null) leftSlotImage.gameObject.SetActive(false);
        if (centerSlotImage != null) centerSlotImage.gameObject.SetActive(false);
        if (rightSlotImage != null) rightSlotImage.gameObject.SetActive(false);
        if (selectOrUnlockButton != null) selectOrUnlockButton.gameObject.SetActive(false);
        if (characterNameText != null) characterNameText.gameObject.SetActive(false);
        if (statsText != null) statsText.gameObject.SetActive(false);
        if (currentRPText != null) currentRPText.gameObject.SetActive(false);
    }

    private void SetupAllThreeCharacterCards(Font font)
    {
        // 3 ใบขนาดกว้าง 420px สูง 680px ระยะห่าง 30px (ตำแหน่ง X = -450, 0, +450)
        // Card 0: N-01 Vanguard (Green)
        SetupSingleCharacterCard(
            0,
            "N-01 Vanguard",
            "MELEE TANK",
            VanguardGreen,
            "Nano-Blade",
            200,
            15,
            4.0f,
            "Kinetic Feedback",
            "Reflects melee damage & high durability.",
            "Standard Dash",
            "1 Dash Charge",
            new Vector2(-450f, -10f),
            font
        );

        // Card 1: N-02 Psionic (Red)
        SetupSingleCharacterCard(
            1,
            "N-02 Psionic",
            "CRITICAL SPECIALIST",
            PsionicRed,
            "Bio-Needler",
            100,
            0,
            4.0f,
            "Mind Shock",
            "Critical hits Stun enemies for 1.0s.",
            "Standard Dash",
            "1 Dash Charge",
            new Vector2(0f, -10f),
            font
        );

        // Card 2: N-03 Cyber-Swift (Amber/Orange)
        SetupSingleCharacterCard(
            2,
            "N-03 Cyber-Swift",
            "SPEED OPERATIVE",
            CyberSwiftAmber,
            "Plasma Blaster",
            130,
            5,
            4.8f,
            "Overclock Dash",
            "+20% Move Speed & extended I-Frame.",
            "Dual Overclock Dash",
            "2 Dash Charges",
            new Vector2(450f, -10f),
            font
        );
    }

    private void SetupSingleCharacterCard(
        int index,
        string characterName,
        string roleTag,
        Color accentColor,
        string startingWeapon,
        int hp,
        int armor,
        float speed,
        string passiveName,
        string passiveDesc,
        string dashName,
        string dashDesc,
        Vector2 anchoredPosition,
        Font font)
    {
        string cardName = $"CharacterCard_{index}";
        Transform cardTrans = transform.Find(cardName);
        if (cardTrans == null)
        {
            GameObject cardGo = new GameObject(cardName, typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(transform, false);
            cardTrans = cardGo.transform;
        }

        cardTrans.gameObject.SetActive(true);

        // 1. Card Root: กว้าง 420px สูง 680px
        RectTransform cardRt = cardTrans.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = anchoredPosition;
        cardRt.sizeDelta = new Vector2(420f, 680f); // 420x680px
        cardRt.localScale = Vector3.one;

        // Outer Border (3px accent color)
        Image borderImg = cardTrans.GetComponent<Image>();
        borderImg.sprite = null;
        borderImg.color = accentColor;
        borderImg.raycastTarget = false;

        // 2. Card Inner Background
        Transform innerBgTrans = cardTrans.Find("CardInnerBg");
        if (innerBgTrans == null)
        {
            GameObject bgGo = new GameObject("CardInnerBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(cardTrans, false);
            bgGo.transform.SetAsFirstSibling();
            innerBgTrans = bgGo.transform;
        }
        else
        {
            innerBgTrans.SetAsFirstSibling();
        }

        RectTransform innerRt = innerBgTrans.GetComponent<RectTransform>();
        innerRt.anchorMin = Vector2.zero;
        innerRt.anchorMax = Vector2.one;
        innerRt.offsetMin = new Vector2(3f, 3f);
        innerRt.offsetMax = new Vector2(-3f, -3f);

        Image innerImg = innerBgTrans.GetComponent<Image>();
        innerImg.sprite = null;
        innerImg.color = CardBgDark;
        innerImg.raycastTarget = true;

        // Smooth Hover Animation (1.05x)
        if (cardTrans.GetComponent<CardHoverEffect>() == null)
        {
            cardTrans.gameObject.AddComponent<CardHoverEffect>();
        }

        // 3. Role Badge (Top Pill)
        Transform badgeTrans = cardTrans.Find("RoleBadge");
        if (badgeTrans == null)
        {
            GameObject badgeGo = new GameObject("RoleBadge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(cardTrans, false);
            badgeTrans = badgeGo.transform;
        }
        RectTransform badgeRt = badgeTrans.GetComponent<RectTransform>();
        badgeRt.anchorMin = new Vector2(0.5f, 0.5f);
        badgeRt.anchorMax = new Vector2(0.5f, 0.5f);
        badgeRt.pivot = new Vector2(0.5f, 0.5f);
        badgeRt.anchoredPosition = new Vector2(0f, 295f);
        badgeRt.sizeDelta = new Vector2(220f, 32f);

        Image badgeImg = badgeTrans.GetComponent<Image>();
        badgeImg.sprite = null;
        badgeImg.color = accentColor;
        badgeImg.raycastTarget = false;

        Transform badgeTextTrans = badgeTrans.Find("Text");
        if (badgeTextTrans == null)
        {
            GameObject btGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btGo.transform.SetParent(badgeTrans, false);
            badgeTextTrans = btGo.transform;
        }
        Text badgeTxt = badgeTextTrans.GetComponent<Text>();
        badgeTxt.text = roleTag;
        badgeTxt.font = font;
        badgeTxt.fontSize = 14;
        badgeTxt.fontStyle = FontStyle.Bold;
        badgeTxt.color = new Color(0.04f, 0.06f, 0.08f, 1f);
        badgeTxt.alignment = TextAnchor.MiddleCenter;
        badgeTxt.raycastTarget = false;
        RectTransform btRt = badgeTextTrans.GetComponent<RectTransform>();
        btRt.anchorMin = Vector2.zero;
        btRt.anchorMax = Vector2.one;
        btRt.sizeDelta = Vector2.zero;

        // 4. Character Name (Font 30sp Bold)
        Transform nameTrans = cardTrans.Find("NameText");
        if (nameTrans == null)
        {
            GameObject nameGo = new GameObject("NameText", typeof(RectTransform), typeof(Text));
            nameGo.transform.SetParent(cardTrans, false);
            nameTrans = nameGo.transform;
        }
        RectTransform nameRt = nameTrans.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0.5f, 0.5f);
        nameRt.anchorMax = new Vector2(0.5f, 0.5f);
        nameRt.pivot = new Vector2(0.5f, 0.5f);
        nameRt.anchoredPosition = new Vector2(0f, 245f);
        nameRt.sizeDelta = new Vector2(390f, 48f);

        Text nText = nameTrans.GetComponent<Text>();
        nText.text = characterName;
        nText.font = font;
        nText.fontSize = 30; // Font 30sp
        nText.fontStyle = FontStyle.Bold;
        nText.color = Color.white;
        nText.alignment = TextAnchor.MiddleCenter;
        nText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nText.raycastTarget = false;

        Shadow nShadow = nameTrans.GetComponent<Shadow>();
        if (nShadow == null) nShadow = nameTrans.gameObject.AddComponent<Shadow>();
        nShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        nShadow.effectDistance = new Vector2(2f, -2f);

        // 5. Character Icon Frame & Sprite (110x110)
        Transform iconFrameTrans = cardTrans.Find("IconFrame");
        if (iconFrameTrans == null)
        {
            GameObject iconFrameGo = new GameObject("IconFrame", typeof(RectTransform), typeof(Image));
            iconFrameGo.transform.SetParent(cardTrans, false);
            iconFrameTrans = iconFrameGo.transform;
        }
        RectTransform iconFrameRt = iconFrameTrans.GetComponent<RectTransform>();
        iconFrameRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconFrameRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconFrameRt.pivot = new Vector2(0.5f, 0.5f);
        iconFrameRt.anchoredPosition = new Vector2(0f, 150f);
        iconFrameRt.sizeDelta = new Vector2(110f, 110f);

        Image frameImg = iconFrameTrans.GetComponent<Image>();
        frameImg.sprite = null;
        frameImg.color = new Color(0.12f, 0.16f, 0.22f, 0.9f);
        frameImg.raycastTarget = false;

        Outline frameOutline = iconFrameTrans.GetComponent<Outline>();
        if (frameOutline == null) frameOutline = iconFrameTrans.gameObject.AddComponent<Outline>();
        frameOutline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.7f);
        frameOutline.effectDistance = new Vector2(2f, -2f);

        Transform spriteTrans = iconFrameTrans.Find("IconSprite");
        if (spriteTrans == null)
        {
            GameObject spriteGo = new GameObject("IconSprite", typeof(RectTransform), typeof(Image));
            spriteGo.transform.SetParent(iconFrameTrans, false);
            spriteTrans = spriteGo.transform;
        }
        RectTransform spriteRt = spriteTrans.GetComponent<RectTransform>();
        spriteRt.anchorMin = new Vector2(0.5f, 0.5f);
        spriteRt.anchorMax = new Vector2(0.5f, 0.5f);
        spriteRt.pivot = new Vector2(0.5f, 0.5f);
        spriteRt.anchoredPosition = Vector2.zero;
        spriteRt.sizeDelta = new Vector2(95f, 95f);

        Image spriteImg = spriteTrans.GetComponent<Image>();
        WeaponType startWp = CharacterSelectionManager.ResolveStartingWeapon(index, characterName);
        Sprite wpIcon = PlayerInventory.GetWeaponIcon(startWp);
        if (wpIcon != null)
        {
            spriteImg.sprite = wpIcon;
            spriteImg.color = Color.white;
        }
        else
        {
            spriteImg.sprite = ProceduralSprites.Agent;
            spriteImg.color = accentColor;
        }
        spriteImg.preserveAspect = true;
        spriteImg.raycastTarget = false;

        // 6. Starting Weapon (Font 20sp Bold Cyan)
        Transform weaponTrans = cardTrans.Find("WeaponText");
        if (weaponTrans == null)
        {
            GameObject weaponGo = new GameObject("WeaponText", typeof(RectTransform), typeof(Text));
            weaponGo.transform.SetParent(cardTrans, false);
            weaponTrans = weaponGo.transform;
        }
        RectTransform weaponRt = weaponTrans.GetComponent<RectTransform>();
        weaponRt.anchorMin = new Vector2(0.5f, 0.5f);
        weaponRt.anchorMax = new Vector2(0.5f, 0.5f);
        weaponRt.pivot = new Vector2(0.5f, 0.5f);
        weaponRt.anchoredPosition = new Vector2(0f, 75f);
        weaponRt.sizeDelta = new Vector2(380f, 30f);

        Text wText = weaponTrans.GetComponent<Text>();
        wText.text = $"Starting Weapon: {startingWeapon}";
        wText.font = font;
        wText.fontSize = 20;
        wText.fontStyle = FontStyle.Bold;
        wText.color = NeonCyan;
        wText.alignment = TextAnchor.MiddleCenter;
        wText.raycastTarget = false;

        // 7. Divider Line
        Transform dividerTrans = cardTrans.Find("Divider");
        if (dividerTrans == null)
        {
            GameObject divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(cardTrans, false);
            dividerTrans = divGo.transform;
        }
        RectTransform divRt = dividerTrans.GetComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0.5f, 0.5f);
        divRt.anchorMax = new Vector2(0.5f, 0.5f);
        divRt.pivot = new Vector2(0.5f, 0.5f);
        divRt.anchoredPosition = new Vector2(0f, 50f);
        divRt.sizeDelta = new Vector2(360f, 2f);

        Image divImg = dividerTrans.GetComponent<Image>();
        divImg.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.45f);
        divImg.raycastTarget = false;

        // 8. Core Stats Box (Font 22sp Bold)
        Transform statsBoxTrans = cardTrans.Find("StatsBox");
        if (statsBoxTrans == null)
        {
            GameObject boxGo = new GameObject("StatsBox", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(cardTrans, false);
            statsBoxTrans = boxGo.transform;
        }
        RectTransform statsBoxRt = statsBoxTrans.GetComponent<RectTransform>();
        statsBoxRt.anchorMin = new Vector2(0.5f, 0.5f);
        statsBoxRt.anchorMax = new Vector2(0.5f, 0.5f);
        statsBoxRt.pivot = new Vector2(0.5f, 0.5f);
        statsBoxRt.anchoredPosition = new Vector2(0f, -15f);
        statsBoxRt.sizeDelta = new Vector2(380f, 90f);

        Image sbImg = statsBoxTrans.GetComponent<Image>();
        sbImg.sprite = null;
        sbImg.color = BoxContainerBg;
        sbImg.raycastTarget = false;

        Outline sbOutline = statsBoxTrans.GetComponent<Outline>();
        if (sbOutline == null) sbOutline = statsBoxTrans.gameObject.AddComponent<Outline>();
        sbOutline.effectColor = new Color(0.2f, 0.28f, 0.38f, 0.5f);
        sbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Transform statsTextTrans = statsBoxTrans.Find("Text");
        if (statsTextTrans == null)
        {
            GameObject stGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            stGo.transform.SetParent(statsBoxTrans, false);
            statsTextTrans = stGo.transform;
        }
        RectTransform stRt = statsTextTrans.GetComponent<RectTransform>();
        stRt.anchorMin = Vector2.zero;
        stRt.anchorMax = Vector2.one;
        stRt.offsetMin = new Vector2(14f, 6f);
        stRt.offsetMax = new Vector2(-14f, -6f);

        Text stText = statsTextTrans.GetComponent<Text>();
        stText.text = $"<b>MAX HP:</b>  {hp}      <b>ARMOR:</b>  {armor}\n<b>MOVE SPEED:</b>  {speed:F1}";
        stText.font = font;
        stText.fontSize = 22; // Font 22sp
        stText.fontStyle = FontStyle.Bold;
        stText.color = new Color(0.9f, 0.95f, 1f, 0.98f);
        stText.alignment = TextAnchor.MiddleLeft;
        stText.lineSpacing = 1.15f;
        stText.raycastTarget = false;

        // 9. Passive & Dash Summary Box (Font 18sp)
        Transform passiveBoxTrans = cardTrans.Find("PassiveBox");
        if (passiveBoxTrans == null)
        {
            GameObject pbGo = new GameObject("PassiveBox", typeof(RectTransform), typeof(Image));
            pbGo.transform.SetParent(cardTrans, false);
            passiveBoxTrans = pbGo.transform;
        }
        RectTransform pbRt = passiveBoxTrans.GetComponent<RectTransform>();
        pbRt.anchorMin = new Vector2(0.5f, 0.5f);
        pbRt.anchorMax = new Vector2(0.5f, 0.5f);
        pbRt.pivot = new Vector2(0.5f, 0.5f);
        pbRt.anchoredPosition = new Vector2(0f, -135f);
        pbRt.sizeDelta = new Vector2(380f, 115f);

        Image pbImg = passiveBoxTrans.GetComponent<Image>();
        pbImg.sprite = null;
        pbImg.color = BoxContainerBg;
        pbImg.raycastTarget = false;

        Outline pbOutline = passiveBoxTrans.GetComponent<Outline>();
        if (pbOutline == null) pbOutline = passiveBoxTrans.gameObject.AddComponent<Outline>();
        pbOutline.effectColor = new Color(0.2f, 0.28f, 0.38f, 0.5f);
        pbOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Transform pbTextTrans = passiveBoxTrans.Find("Text");
        if (pbTextTrans == null)
        {
            GameObject pbtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            pbtGo.transform.SetParent(passiveBoxTrans, false);
            pbTextTrans = pbtGo.transform;
        }
        RectTransform pbtRt = pbTextTrans.GetComponent<RectTransform>();
        pbtRt.anchorMin = Vector2.zero;
        pbtRt.anchorMax = Vector2.one;
        pbtRt.offsetMin = new Vector2(14f, 6f);
        pbtRt.offsetMax = new Vector2(-14f, -6f);

        Text pbText = pbTextTrans.GetComponent<Text>();
        pbText.text = $"<b>Passive: {passiveName}</b>\n<color=#B0BEC5>{passiveDesc}</color>\n<b>Dash: {dashName}</b> (<color=#80DEEA>{dashDesc}</color>)";
        pbText.font = font;
        pbText.fontSize = 18; // Font 18sp
        pbText.color = Color.white;
        pbText.alignment = TextAnchor.MiddleLeft;
        pbText.lineSpacing = 1.15f;
        pbText.raycastTarget = false;

        // 10. [SELECT / DEPLOY] Button (ขยายกว้าง 380px สูง 64px เต็มการ์ด Font 22sp)
        Transform deployBtnTrans = cardTrans.Find("DeployButton");
        if (deployBtnTrans == null)
        {
            GameObject btnGo = new GameObject("DeployButton", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(cardTrans, false);
            deployBtnTrans = btnGo.transform;
        }
        RectTransform btnRt = deployBtnTrans.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0.5f);
        btnRt.anchorMax = new Vector2(0.5f, 0.5f);
        btnRt.pivot = new Vector2(0.5f, 0.5f);
        btnRt.anchoredPosition = new Vector2(0f, -265f);
        btnRt.sizeDelta = new Vector2(380f, 64f); // กว้าง 380px สูง 64px เต็มการ์ด

        Image bImg = deployBtnTrans.GetComponent<Image>();
        bImg.sprite = null;
        bImg.color = accentColor;

        Button btn = deployBtnTrans.GetComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = accentColor;
        colors.highlightedColor = Color.white;
        colors.pressedColor = accentColor * 0.75f;
        colors.colorMultiplier = 1f;
        btn.colors = colors;

        if (deployBtnTrans.GetComponent<CardHoverEffect>() == null)
        {
            deployBtnTrans.gameObject.AddComponent<CardHoverEffect>();
        }

        Transform btnTextTrans = deployBtnTrans.Find("Text");
        if (btnTextTrans == null)
        {
            GameObject btGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            btGo.transform.SetParent(deployBtnTrans, false);
            btnTextTrans = btGo.transform;
        }
        RectTransform btextRt = btnTextTrans.GetComponent<RectTransform>();
        btextRt.anchorMin = Vector2.zero;
        btextRt.anchorMax = Vector2.one;
        btextRt.sizeDelta = Vector2.zero;

        Text btnText = btnTextTrans.GetComponent<Text>();
        btnText.text = "SELECT / DEPLOY";
        btnText.font = font;
        btnText.fontSize = 22; // Font 22sp
        btnText.fontStyle = FontStyle.Bold;
        btnText.color = new Color(0.04f, 0.06f, 0.08f, 1f); // Dark bold text on bright neon button
        btnText.alignment = TextAnchor.MiddleCenter;
        btnText.raycastTarget = false;

        btn.onClick.RemoveAllListeners();
        int capturedIndex = index;
        btn.onClick.AddListener(() => SelectAndDeployCharacter(capturedIndex));
    }

    /// <summary>
    /// บันทึกและส่งค่าตัวละครที่เลือกไปยัง GameScene ทันที
    /// </summary>
    public void SelectAndDeployCharacter(int index)
    {
        Sprite sprite = ProceduralSprites.Agent;
        Color color = index switch
        {
            0 => VanguardGreen,
            1 => PsionicRed,
            2 => CyberSwiftAmber,
            _ => Color.white
        };

        string charId = index switch
        {
            0 => "Subject01",
            1 => "Subject02",
            2 => "Subject03",
            _ => $"Subject0{index + 1}"
        };

        string charName = index switch
        {
            0 => "N-01 Vanguard",
            1 => "N-02 Psionic",
            2 => "N-03 Cyber-Swift",
            _ => $"Subject N-0{index + 1}"
        };

        WeaponType weapon = CharacterSelectionManager.ResolveStartingWeapon(index, $"{charId} {charName}");
        CharacterSelectionManager.GetBalancedCharacterStats(weapon, out int hp, out float spd, out float dmg);

        // ตรวจสอบ Instance ของ CharacterSelectionManager
        if (CharacterSelectionManager.Instance == null)
        {
            GameObject csmGo = new GameObject("CharacterSelectionManager", typeof(CharacterSelectionManager));
        }

        if (CharacterSelectionManager.Instance != null)
        {
            CharacterSelectionManager.Instance.SetSelectedCharacter(
                index,
                sprite,
                color,
                hp,
                spd,
                dmg,
                weapon,
                charId,
                charName
            );
        }

        // บันทึกตัวเลือกและปลดล็อคตัวละคร
        PlayerPrefs.SetInt("SelectedCharacterIndex", index);
        PlayerPrefs.SetInt("Unlocked_" + charId, 1);
        PlayerPrefs.Save();

        Debug.Log($"[CharacterSelectUI] Selected and Deploying: {charName} (#{index}) with Starting Weapon: {PlayerInventory.GetWeaponName(weapon)}");
        SceneManager.LoadScene(MainMenuManager.Instance != null ? MainMenuManager.Instance.GameSceneName : "GameScene");
    }

    // --- Backward Compatible Helper Methods ---
    public void NextCharacter()
    {
        currentIndex = (currentIndex + 1) % 3;
        Debug.Log($"[CharacterSelectUI] Next Character Index: {currentIndex}");
    }

    public void PreviousCharacter()
    {
        currentIndex = (currentIndex - 1 + 3) % 3;
        Debug.Log($"[CharacterSelectUI] Previous Character Index: {currentIndex}");
    }

    public void OnSelectOrUnlockPressed() => SelectAndDeployCharacter(currentIndex);
}
