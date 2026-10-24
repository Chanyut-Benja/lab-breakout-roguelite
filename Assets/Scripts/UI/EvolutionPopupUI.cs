using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ป๊อปอัปแจ้งเตือนการรวมร่างอาวุธขั้นสุดยอด (Evolution Popup UI)
/// ปรากฏขึ้นกลางหน้าจอชั่วขณะพร้อมแอนิเมชัน Fade-In / Scale-Up และ Fade-Out
/// แสดงชื่ออาวุธใหม่, ไอคอนขนาดใหญ่, สูตรคู่ผสม, และคำอธิบายความสามารถ
/// 
/// ผูกเข้ากับ UnityEvent OnWeaponEvolved(EvolvedWeaponData evolvedData)
/// หรือ C# event OnWeaponEvolved ของ EvolutionManager เพื่อแสดงผลอัตโนมัติ
/// </summary>
public class EvolutionPopupUI : MonoBehaviour
{
    public static EvolutionPopupUI Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject popupPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Text headerText;
    [SerializeField] private Image weaponIconImage;
    [SerializeField] private Image iconBorderImage;
    [SerializeField] private Text evolvedTitleText;
    [SerializeField] private Text recipeSubtitleText;
    [SerializeField] private Text descriptionText;

    [Header("Settings")]
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private bool briefSlowMotion = true;
    [SerializeField] private float slowMotionScale = 0.3f;
    [SerializeField] private float slowMotionDuration = 0.4f;

    private readonly Queue<EvolvedWeaponData> popupQueue = new Queue<EvolvedWeaponData>();
    private readonly HashSet<EvolvedWeaponType> queuedTypes = new HashSet<EvolvedWeaponType>();
    private Coroutine currentSequence;
    private Font cachedFont;

    // Dark Sci-Fi Palette
    private static readonly Color RadiantGold = new Color(1f, 0.75f, 0.05f, 1f);       // #FFBF0D
    private static readonly Color HeaderCyan = new Color(0f, 0.90f, 1f, 1f);           // #00E5FF
    private static readonly Color SubtitleCyan = new Color(0.85f, 0.95f, 1f, 0.9f);    // #D9F2FF
    private static readonly Color DescGrey = new Color(0.78f, 0.84f, 0.90f, 0.85f);    // Slate Light

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

        cachedFont = InGameHUDController.GetDefaultFont();

        if (popupPanel == null)
        {
            BuildPopupHierarchy();
        }

        if (popupPanel != null)
        {
            popupPanel.SetActive(false);
        }
    }

    private void Start()
    {
        HookEvolutionEvent();
    }

    private void OnDestroy()
    {
        UnhookEvolutionEvent();
    }

    private void HookEvolutionEvent()
    {
        EvolutionManager evo = EvolutionManager.Instance ?? EvolutionManager.GetOrCreate();
        if (evo != null)
        {
            evo.OnWeaponEvolved -= EnqueuePopup;
            evo.OnWeaponEvolved += EnqueuePopup;

            evo.onWeaponEvolvedUnityEvent.RemoveListener(OnWeaponEvolved);
            evo.onWeaponEvolvedUnityEvent.AddListener(OnWeaponEvolved);
        }
    }

    private void UnhookEvolutionEvent()
    {
        if (EvolutionManager.Instance != null)
        {
            EvolutionManager.Instance.OnWeaponEvolved -= EnqueuePopup;
            EvolutionManager.Instance.onWeaponEvolvedUnityEvent.RemoveListener(OnWeaponEvolved);
        }
    }

    /// <summary>
    /// เมธอดสำหรับ UnityEvent: OnWeaponEvolved(EvolvedWeaponData evolvedData)
    /// </summary>
    public void OnWeaponEvolved(EvolvedWeaponData evolvedData)
    {
        EnqueuePopup(evolvedData);
    }

    public static void Show(EvolvedWeaponData data)
    {
        if (Instance == null)
        {
            EnsureInstanceExists();
        }

        if (Instance != null)
        {
            Instance.EnqueuePopup(data);
        }
    }

    public static void EnsureInstanceExists()
    {
        if (Instance != null) return;

        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject popupGo = new GameObject("EvolutionPopupUI");
        popupGo.transform.SetParent(canvas.transform, false);
        Instance = popupGo.AddComponent<EvolutionPopupUI>();
    }

    public void EnqueuePopup(EvolvedWeaponData data)
    {
        if (data == null) return;

        // ป้องกันการยัดคิวซ้ำซ้อนในรอบเดียวกัน
        if (queuedTypes.Contains(data.evolvedType)) return;
        queuedTypes.Add(data.evolvedType);

        popupQueue.Enqueue(data);
        if (currentSequence == null)
        {
            currentSequence = StartCoroutine(ProcessQueueRoutine());
        }
    }

    private IEnumerator ProcessQueueRoutine()
    {
        while (popupQueue.Count > 0)
        {
            EvolvedWeaponData data = popupQueue.Dequeue();
            yield return StartCoroutine(PlayEvolutionAnimation(data));
            queuedTypes.Remove(data.evolvedType);
            yield return new WaitForSecondsRealtime(0.2f);
        }

        currentSequence = null;
    }

    private IEnumerator PlayEvolutionAnimation(EvolvedWeaponData data)
    {
        if (popupPanel == null)
        {
            BuildPopupHierarchy();
        }

        if (popupPanel == null) yield break;

        // 1. ตั้งค่าข้อมูลลง UI
        if (headerText != null)
        {
            headerText.text = "WEAPON EVOLUTION COMPLETED!";
        }

        if (evolvedTitleText != null)
        {
            evolvedTitleText.text = data.evolvedName.ToUpper();
        }

        if (recipeSubtitleText != null)
        {
            recipeSubtitleText.text = data.recipeText;
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        Sprite icon = data.icon != null ? data.icon : PlayerInventory.GetEvolvedWeaponIcon(data.evolvedType);
        if (icon == null) icon = PlayerInventory.GetWeaponIcon(data.baseWeaponType);
        if (weaponIconImage != null)
        {
            weaponIconImage.sprite = icon;
            weaponIconImage.gameObject.SetActive(icon != null);
        }

        // 2. เล่นเสียงแจ้งเตือน
        if (AudioManager.Instance != null)
        {
            if (AudioManager.Instance.expSFX != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.expSFX);
            }
            else if (AudioManager.Instance.doorSFX != null)
            {
                AudioManager.Instance.PlaySFX(AudioManager.Instance.doorSFX);
            }
        }

        // 3. เริ่มต้น Fade-In / Scale-Up
        popupPanel.SetActive(true);
        RectTransform panelRect = popupPanel.GetComponent<RectTransform>();
        if (panelRect != null) panelRect.localScale = Vector3.one * 0.7f;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        float originalTimeScale = Time.timeScale;
        if (briefSlowMotion && originalTimeScale > 0.1f)
        {
            Time.timeScale = slowMotionScale; // สโลว์โมชันสั้นๆ เพิ่มความทรงพลังและไฮป์ตามสไตล์ Roguelite
        }

        float inDuration = 0.35f;
        float elapsed = 0f;

        while (elapsed < inDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / inDuration);
            // Ease-Out Back Effect
            float scale = Mathf.Lerp(0.7f, 1.05f, Mathf.Sin(t * Mathf.PI * 0.5f));
            if (panelRect != null) panelRect.localScale = Vector3.one * scale;
            if (canvasGroup != null) canvasGroup.alpha = t;
            yield return null;
        }

        if (panelRect != null) panelRect.localScale = Vector3.one;
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // รอจนหมดช่วงสโลว์โมชันสั้นๆ แล้วคืนค่าความเร็วเวลาเดิมให้เกมเล่นต่ออย่างลื่นไหล
        if (briefSlowMotion)
        {
            if (slowMotionDuration > inDuration)
            {
                yield return new WaitForSecondsRealtime(slowMotionDuration - inDuration);
            }
            Time.timeScale = originalTimeScale;
        }

        // 4. แสดงค้างไว้กลางหน้าจอ (รวมเวลา 2-3 วินาที)
        float remainingDisplay = Mathf.Max(0.5f, displayDuration - inDuration);
        yield return new WaitForSecondsRealtime(remainingDisplay);

        // 5. Fade-Out & Scale-Down
        float outDuration = 0.35f;
        elapsed = 0f;

        while (elapsed < outDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / outDuration);
            float scale = Mathf.Lerp(1.0f, 0.85f, t);
            if (panelRect != null) panelRect.localScale = Vector3.one * scale;
            if (canvasGroup != null) canvasGroup.alpha = 1f - t;
            yield return null;
        }

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        popupPanel.SetActive(false);
    }

    #region Procedural UI Hierarchy Builder

    [ContextMenu("Rebuild Popup Hierarchy")]
    public void BuildPopupHierarchy()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null) return;

        if (cachedFont == null) cachedFont = InGameHUDController.GetDefaultFont();

        // 1. Root Panel "EvolutionPopupPanel" ภายใต้ Canvas กลาง
        Transform existing = canvas.transform.Find("EvolutionPopupPanel");
        GameObject panelGo;
        if (existing == null)
        {
            panelGo = new GameObject("EvolutionPopupPanel", typeof(RectTransform), typeof(CanvasGroup));
            panelGo.transform.SetParent(canvas.transform, false);
        }
        else
        {
            panelGo = existing.gameObject;
        }

        popupPanel = panelGo;
        canvasGroup = panelGo.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = panelGo.AddComponent<CanvasGroup>();
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        RectTransform rootRt = panelGo.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = Vector2.zero; // จัดให้อยู่กึ่งกลางหน้าจออย่างสมดุล ชัดเจน
        rootRt.sizeDelta = new Vector2(680f, 380f);

        // 2. Background Box (Dark Slate with Glowing Border)
        Image bgImg = panelGo.GetComponent<Image>();
        if (bgImg == null) bgImg = panelGo.AddComponent<Image>();
        bgImg.color = new Color(0.06f, 0.08f, 0.12f, 0.95f); // #0F141E
        bgImg.raycastTarget = false;

        // Border
        Transform borderTrans = panelGo.transform.Find("Border");
        if (borderTrans == null)
        {
            GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(panelGo.transform, false);
            borderTrans = borderGo.transform;
            borderTrans.SetAsFirstSibling();
        }
        RectTransform borderRt = borderTrans.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(-2.5f, -2.5f);
        borderRt.offsetMax = new Vector2(2.5f, 2.5f);
        Image borderImage = borderTrans.GetComponent<Image>();
        borderImage.color = RadiantGold;
        borderImage.raycastTarget = false;

        // 3. Header Text "WEAPON EVOLUTION COMPLETED!"
        Transform headerTrans = panelGo.transform.Find("HeaderText");
        if (headerTrans == null)
        {
            GameObject hGo = new GameObject("HeaderText", typeof(RectTransform), typeof(Text), typeof(Shadow));
            hGo.transform.SetParent(panelGo.transform, false);
            headerTrans = hGo.transform;
        }
        RectTransform headerRt = headerTrans.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -20f);
        headerRt.sizeDelta = new Vector2(0f, 46f);

        headerText = headerTrans.GetComponent<Text>();
        headerText.font = cachedFont;
        headerText.fontSize = 30;
        headerText.fontStyle = FontStyle.Bold;
        headerText.color = HeaderCyan;
        headerText.alignment = TextAnchor.MiddleCenter;
        headerText.text = "WEAPON EVOLUTION COMPLETED!";
        headerText.raycastTarget = false;

        Shadow hShadow = headerTrans.GetComponent<Shadow>();
        hShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        hShadow.effectDistance = new Vector2(2f, -2f);

        // 4. Large Icon Container (Left side)
        Transform iconBoxTrans = panelGo.transform.Find("IconBox");
        if (iconBoxTrans == null)
        {
            GameObject ibGo = new GameObject("IconBox", typeof(RectTransform), typeof(Image));
            ibGo.transform.SetParent(panelGo.transform, false);
            iconBoxTrans = ibGo.transform;
        }
        RectTransform iconBoxRt = iconBoxTrans.GetComponent<RectTransform>();
        iconBoxRt.anchorMin = new Vector2(0f, 0.5f);
        iconBoxRt.anchorMax = new Vector2(0f, 0.5f);
        iconBoxRt.pivot = new Vector2(0.5f, 0.5f);
        iconBoxRt.anchoredPosition = new Vector2(95f, -25f);
        iconBoxRt.sizeDelta = new Vector2(130f, 130f);

        iconBorderImage = iconBoxTrans.GetComponent<Image>();
        iconBorderImage.color = RadiantGold;
        iconBorderImage.raycastTarget = false;

        // Icon Inner Bg
        Transform iconBgTrans = iconBoxTrans.Find("IconInnerBg");
        if (iconBgTrans == null)
        {
            GameObject bgGo = new GameObject("IconInnerBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(iconBoxTrans, false);
            iconBgTrans = bgGo.transform;
        }
        RectTransform ibgRt = iconBgTrans.GetComponent<RectTransform>();
        ibgRt.anchorMin = Vector2.zero;
        ibgRt.anchorMax = Vector2.one;
        ibgRt.offsetMin = new Vector2(2f, 2f);
        ibgRt.offsetMax = new Vector2(-2f, -2f);
        Image ibgImg = iconBgTrans.GetComponent<Image>();
        ibgImg.color = new Color(0.04f, 0.06f, 0.09f, 0.98f);
        ibgImg.raycastTarget = false;

        // Icon Sprite Image (ช่องแสดงไอคอนขนาดใหญ่ของอาวุธ Evolved ใหม่)
        Transform iconImgTrans = iconBgTrans.Find("WeaponIcon");
        if (iconImgTrans == null)
        {
            GameObject iGo = new GameObject("WeaponIcon", typeof(RectTransform), typeof(Image));
            iGo.transform.SetParent(iconBgTrans, false);
            iconImgTrans = iGo.transform;
        }
        RectTransform imgRt = iconImgTrans.GetComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = new Vector2(8f, 8f);
        imgRt.offsetMax = new Vector2(-8f, -8f);

        weaponIconImage = iconImgTrans.GetComponent<Image>();
        weaponIconImage.preserveAspect = true;
        weaponIconImage.raycastTarget = false;

        // 5. Right Content Area (Title, Recipe, Description)
        float textLeftOffset = 185f;
        float textRightOffset = 30f;

        // Evolved Name (e.g. EXECUTIONER MATRIX)
        Transform titleTrans = panelGo.transform.Find("EvolvedTitle");
        if (titleTrans == null)
        {
            GameObject tGo = new GameObject("EvolvedTitle", typeof(RectTransform), typeof(Text), typeof(Shadow));
            tGo.transform.SetParent(panelGo.transform, false);
            titleTrans = tGo.transform;
        }
        RectTransform titleRt = titleTrans.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0f, 1f);
        titleRt.anchoredPosition = new Vector2(textLeftOffset, -78f);
        titleRt.sizeDelta = new Vector2(-textLeftOffset - textRightOffset, 42f);

        evolvedTitleText = titleTrans.GetComponent<Text>();
        evolvedTitleText.font = cachedFont;
        evolvedTitleText.fontSize = 28;
        evolvedTitleText.fontStyle = FontStyle.Bold;
        evolvedTitleText.color = RadiantGold;
        evolvedTitleText.alignment = TextAnchor.MiddleLeft;
        evolvedTitleText.text = "EXECUTIONER MATRIX";
        evolvedTitleText.raycastTarget = false;

        Shadow tShadow = titleTrans.GetComponent<Shadow>();
        tShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
        tShadow.effectDistance = new Vector2(2f, -2f);

        // Recipe Subtitle (e.g. Nano-Blade Lv.5 + Targeting Lens)
        Transform recipeTrans = panelGo.transform.Find("RecipeSubtitle");
        if (recipeTrans == null)
        {
            GameObject rGo = new GameObject("RecipeSubtitle", typeof(RectTransform), typeof(Text));
            rGo.transform.SetParent(panelGo.transform, false);
            recipeTrans = rGo.transform;
        }
        RectTransform recipeRt = recipeTrans.GetComponent<RectTransform>();
        recipeRt.anchorMin = new Vector2(0f, 1f);
        recipeRt.anchorMax = new Vector2(1f, 1f);
        recipeRt.pivot = new Vector2(0f, 1f);
        recipeRt.anchoredPosition = new Vector2(textLeftOffset, -126f);
        recipeRt.sizeDelta = new Vector2(-textLeftOffset - textRightOffset, 28f);

        recipeSubtitleText = recipeTrans.GetComponent<Text>();
        recipeSubtitleText.font = cachedFont;
        recipeSubtitleText.fontSize = 19;
        recipeSubtitleText.fontStyle = FontStyle.Bold;
        recipeSubtitleText.color = SubtitleCyan;
        recipeSubtitleText.alignment = TextAnchor.MiddleLeft;
        recipeSubtitleText.text = "Nano-Blade Lv.5 + Targeting Lens";
        recipeSubtitleText.raycastTarget = false;

        // Description Text
        Transform descTrans = panelGo.transform.Find("DescriptionText");
        if (descTrans == null)
        {
            GameObject dGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(Text));
            dGo.transform.SetParent(panelGo.transform, false);
            descTrans = dGo.transform;
        }
        RectTransform descRt = descTrans.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.pivot = new Vector2(0f, 1f);
        descRt.offsetMin = new Vector2(textLeftOffset, 30f);
        descRt.offsetMax = new Vector2(-textRightOffset, -165f);

        descriptionText = descTrans.GetComponent<Text>();
        descriptionText.font = cachedFont;
        descriptionText.fontSize = 18;
        descriptionText.color = DescGrey;
        descriptionText.alignment = TextAnchor.UpperLeft;
        descriptionText.text = "ดาเมจ 130 • คริติคอล 100% • ใบมีดซิกแซกตัดทะลุศัตรูทั้งจอ ทุก 0.5 วินาที";
        descriptionText.raycastTarget = false;
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;

        popupPanel.SetActive(false);
    }

    #endregion
}
