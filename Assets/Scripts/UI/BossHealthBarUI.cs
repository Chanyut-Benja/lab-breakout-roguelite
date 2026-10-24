using System.Collections;
using Enemies.Bosses;
using Enemies.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Dark Sci-Fi Boss Health Bar UI (Top-Center)
    /// 1. Boss Name Text: Shows boss title and room (e.g., "BRUTE - ROOM 3", "DOCTOR - ROOM 9").
    /// 2. Health Bar: Red-Orange Sci-Fi theme (#FF3344 to #FF6B00), smooth lerp, with current / max HP display.
    /// 3. Enrage Warning Indicator: Icon and pulsing blinking effect when boss enters Enrage state (3 minutes / Phase 3).
    /// 4. Auto-Hide: Fades out and deactivates automatically when the boss is defeated or when switching rooms.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        public static BossHealthBarUI Instance { get; private set; }

        [Header("UI Hierarchy")]
        [SerializeField] private GameObject barContainer;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text hpNumberText;
        [SerializeField] private GameObject enrageWarningRoot;
        [SerializeField] private Text enrageWarningText;

        [Header("Colors (Dark Sci-Fi Theme)")]
        private static readonly Color DarkBgColor = new Color(0.05f, 0.07f, 0.09f, 0.95f);       // #0D1117
        private static readonly Color BorderSlate = new Color(0.19f, 0.24f, 0.32f, 1f);          // #30363D
        private static readonly Color HeaderCyan = new Color(0.0f, 0.90f, 1.0f, 1f);             // #00E5FF
        private static readonly Color NeonOrangeRed = new Color(1.0f, 0.22f, 0.15f, 1f);         // #FF3826
        private static readonly Color EnrageColorPulse1 = new Color(1.0f, 0.15f, 0.15f, 1f);     // Red
        private static readonly Color EnrageColorPulse2 = new Color(1.0f, 0.65f, 0.0f, 1f);      // Bright Orange

        private BossBase activeBoss;
        private float targetHealth;
        private float maxHealth = 100f;
        private float displayedHealth;
        private bool isEnraged = false;
        private Coroutine fadeCoroutine;
        private Font cachedFont;

        public bool IsVisible => barContainer != null && barContainer.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.01f);
        public BossBase ActiveBoss => activeBoss;

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

            if (barContainer == null)
            {
                BuildHierarchy();
            }

            HideImmediate();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        public void SubscribeEvents()
        {
            UnsubscribeEvents();
            BossBase.OnBossSpawned += HandleBossSpawned;
            BossBase.OnBossHealthChanged += HandleBossHealthChanged;
            BossBase.OnBossPhaseChanged += HandleBossPhaseChanged;
            BossBase.OnBossDied += HandleBossDied;
        }

        private void OnDisable()
        {
            UnsubscribeEvents();

            if (activeBoss != null && activeBoss.Health != null)
            {
                activeBoss.Health.OnHealthChanged -= HandleHealthChangedDirect;
                activeBoss.Health.OnDied -= HandleBossDiedDirect;
            }
        }

        public void UnsubscribeEvents()
        {
            BossBase.OnBossSpawned -= HandleBossSpawned;
            BossBase.OnBossHealthChanged -= HandleBossHealthChanged;
            BossBase.OnBossPhaseChanged -= HandleBossPhaseChanged;
            BossBase.OnBossDied -= HandleBossDied;
        }

        private void Update()
        {
            if (activeBoss != null)
            {
                // Verify if boss entered enrage state
                bool currentEnraged = activeBoss.IsEnraged || activeBoss.IsTimeEnraged;
                if (currentEnraged != isEnraged)
                {
                    SetEnragedState(currentEnraged);
                }

                if (activeBoss.Health != null && activeBoss.Health.IsDead && IsVisible)
                {
                    Hide();
                }
            }

            // Smooth health bar interpolation
            if (Mathf.Abs(displayedHealth - targetHealth) > 0.1f)
            {
                displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, Time.deltaTime * 12f);
                UpdateHealthVisuals(displayedHealth, maxHealth);
            }

            // Pulsing Enrage Animation
            if (isEnraged && enrageWarningText != null && enrageWarningRoot != null && enrageWarningRoot.activeInHierarchy)
            {
                float t = Mathf.PingPong(Time.time * 3.5f, 1f);
                enrageWarningText.color = Color.Lerp(EnrageColorPulse1, EnrageColorPulse2, t);
            }
        }

        public void BindBoss(BossBase boss)
        {
            if (boss == null)
            {
                Hide();
                return;
            }

            if (activeBoss != null && activeBoss.Health != null)
            {
                activeBoss.Health.OnHealthChanged -= HandleHealthChangedDirect;
            }

            activeBoss = boss;
            string title = boss.BossConfig != null ? boss.BossConfig.BossTitle : boss.name;
            int room = boss.BossConfig != null ? boss.BossConfig.BossRoomNumber : (RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 3);
            string formattedTitle = $"{title.ToUpper()} - ROOM {room}";

            float curHp = boss.Health != null ? boss.Health.CurrentHealth : boss.DefaultBaseMaxHP;
            float mHp = boss.Health != null ? boss.Health.MaxHealth : boss.DefaultBaseMaxHP;

            if (boss.Health != null)
            {
                boss.Health.OnHealthChanged -= HandleHealthChangedDirect;
                boss.Health.OnHealthChanged += HandleHealthChangedDirect;
                boss.Health.OnDied -= HandleBossDiedDirect;
                boss.Health.OnDied += HandleBossDiedDirect;
            }

            Show(formattedTitle, curHp, mHp);
            SetEnragedState(boss.IsEnraged || boss.IsTimeEnraged);
        }

        private void HandleBossDiedDirect(EnemyHealth h)
        {
            Hide();
        }

        private void HandleHealthChangedDirect(float current, float max)
        {
            HandleBossHealthChanged(current, max);
        }

        private void HandleBossSpawned(string bossTitle, float curHp, float mHp)
        {
            Show(bossTitle, curHp, mHp);
        }

        private void HandleBossHealthChanged(float curHp, float mHp)
        {
            targetHealth = Mathf.Max(0f, curHp);
            maxHealth = Mathf.Max(1f, mHp);

            if (targetHealth <= 0f)
            {
                Hide();
            }
        }

        private void HandleBossPhaseChanged(int phaseNumber)
        {
            if (phaseNumber >= 3)
            {
                SetEnragedState(true);
            }
        }

        private void HandleBossDied(BossData data)
        {
            Hide();
        }

        public void Show(string title, float curHp, float mHp)
        {
            if (barContainer == null) BuildHierarchy();

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            maxHealth = Mathf.Max(1f, mHp);
            targetHealth = Mathf.Clamp(curHp, 0f, maxHealth);
            displayedHealth = targetHealth;

            if (bossNameText != null)
            {
                bossNameText.text = title.ToUpper();
            }

            UpdateHealthVisuals(displayedHealth, maxHealth);
            SetEnragedState(false);

            barContainer.SetActive(true);
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.blocksRaycasts = false;
            }
        }

        public void Hide()
        {
            if (!Application.isPlaying)
            {
                HideImmediate();
                return;
            }
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            fadeCoroutine = StartCoroutine(FadeOutRoutine());
        }

        public void HideImmediate()
        {
            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (barContainer != null) barContainer.SetActive(false);
            activeBoss = null;
            isEnraged = false;
        }

        private IEnumerator FadeOutRoutine()
        {
            if (canvasGroup != null)
            {
                float t = 1f;
                while (t > 0f)
                {
                    t -= Time.deltaTime * 2.5f;
                    canvasGroup.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
            }

            HideImmediate();
            fadeCoroutine = null;
        }

        private void SetEnragedState(bool enraged)
        {
            isEnraged = enraged;
            if (enrageWarningRoot != null)
            {
                enrageWarningRoot.SetActive(enraged);
            }
            if (fillImage != null)
            {
                fillImage.color = enraged ? new Color(1f, 0.1f, 0.1f, 1f) : NeonOrangeRed;
            }
        }

        private void UpdateHealthVisuals(float current, float max)
        {
            if (max <= 0f) return;

            float ratio = Mathf.Clamp01(current / max);
            if (healthSlider != null)
            {
                healthSlider.value = ratio;
            }

            if (hpNumberText != null)
            {
                hpNumberText.text = $"{Mathf.CeilToInt(current):N0} / {Mathf.CeilToInt(max):N0} ({ratio * 100f:F0}%)";
            }
        }

        /// <summary>
        /// Procedural UI construction on Canvas ensuring zero missing dependencies.
        /// </summary>
        public void BuildHierarchy()
        {
            if (barContainer != null) return;

            // 1. Root Container (Top-Center)
            barContainer = new GameObject("BossHealthBarContainer", typeof(RectTransform), typeof(CanvasGroup));
            barContainer.transform.SetParent(transform, false);

            canvasGroup = barContainer.GetComponent<CanvasGroup>();
            RectTransform rootRect = barContainer.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.anchoredPosition = new Vector2(0f, -18f);
            rootRect.sizeDelta = new Vector2(860f, 82f); // ขยายแถบ HP บอสให้ใหญ่ ชัดเจน

            // Background Border Panel
            GameObject bgPanel = new GameObject("BgPanel", typeof(RectTransform), typeof(Image));
            bgPanel.transform.SetParent(barContainer.transform, false);
            RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            Image bgImg = bgPanel.GetComponent<Image>();
            bgImg.color = DarkBgColor;

            // Border Glow Outline
            Outline outline = bgPanel.AddComponent<Outline>();
            outline.effectColor = BorderSlate;
            outline.effectDistance = new Vector2(2f, 2f);

            // 2. Boss Name Text (Top Row)
            GameObject nameObj = new GameObject("BossNameText", typeof(RectTransform), typeof(Text), typeof(Shadow));
            nameObj.transform.SetParent(barContainer.transform, false);
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, -4f);
            nameRect.sizeDelta = new Vector2(-24f, 28f);

            bossNameText = nameObj.GetComponent<Text>();
            bossNameText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            bossNameText.fontSize = 22; // ชื่อบอสตัวใหญ่ ชัดเจน
            bossNameText.fontStyle = FontStyle.Bold;
            bossNameText.alignment = TextAnchor.MiddleCenter;
            bossNameText.color = HeaderCyan;
            bossNameText.text = "BOSS ENCOUNTER";
            bossNameText.raycastTarget = false;

            Shadow nameShadow = nameObj.GetComponent<Shadow>();
            nameShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            nameShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // 3. Health Bar Slider
            GameObject sliderObj = new GameObject("BossSlider", typeof(RectTransform), typeof(Slider));
            sliderObj.transform.SetParent(barContainer.transform, false);
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0f);
            sliderRect.anchorMax = new Vector2(1f, 0f);
            sliderRect.pivot = new Vector2(0.5f, 0f);
            sliderRect.anchoredPosition = new Vector2(0f, 8f);
            sliderRect.sizeDelta = new Vector2(-30f, 36f); // สไลเดอร์หนาขึ้น

            healthSlider = sliderObj.GetComponent<Slider>();
            healthSlider.interactable = false;
            healthSlider.transition = Selectable.Transition.None;
            healthSlider.navigation = new Navigation { mode = Navigation.Mode.None };

            // Slider Background
            GameObject sliderBg = new GameObject("SliderBg", typeof(RectTransform), typeof(Image));
            sliderBg.transform.SetParent(sliderObj.transform, false);
            RectTransform sBgRect = sliderBg.GetComponent<RectTransform>();
            sBgRect.anchorMin = Vector2.zero;
            sBgRect.anchorMax = Vector2.one;
            sBgRect.offsetMin = Vector2.zero;
            sBgRect.offsetMax = Vector2.zero;
            Image sBgImg = sliderBg.GetComponent<Image>();
            sBgImg.color = new Color(0.12f, 0.04f, 0.04f, 0.95f);

            // Slider Fill Area & Fill
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            fillImage = fillObj.GetComponent<Image>();
            fillImage.color = NeonOrangeRed;
            healthSlider.fillRect = fillRect;

            // 4. HP Text inside Slider
            GameObject hpTextObj = new GameObject("HPNumberText", typeof(RectTransform), typeof(Text), typeof(Shadow));
            hpTextObj.transform.SetParent(sliderObj.transform, false);
            RectTransform hpRect = hpTextObj.GetComponent<RectTransform>();
            hpRect.anchorMin = Vector2.zero;
            hpRect.anchorMax = Vector2.one;
            hpRect.offsetMin = Vector2.zero;
            hpRect.offsetMax = Vector2.zero;

            hpNumberText = hpTextObj.GetComponent<Text>();
            hpNumberText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            hpNumberText.fontSize = 18; // ตัวเลข HP บอสใหญ่ขึ้น
            hpNumberText.fontStyle = FontStyle.Bold;
            hpNumberText.alignment = TextAnchor.MiddleCenter;
            hpNumberText.color = Color.white;
            hpNumberText.text = "100 / 100";
            hpNumberText.raycastTarget = false;

            Shadow hpShadow = hpTextObj.GetComponent<Shadow>();
            hpShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
            hpShadow.effectDistance = new Vector2(1.5f, -1.5f);

            // 5. Enrage Warning Banner (Below or Above Bar)
            enrageWarningRoot = new GameObject("EnrageWarningBanner", typeof(RectTransform), typeof(Image));
            enrageWarningRoot.transform.SetParent(barContainer.transform, false);
            RectTransform warnRect = enrageWarningRoot.GetComponent<RectTransform>();
            warnRect.anchorMin = new Vector2(0.5f, 0f);
            warnRect.anchorMax = new Vector2(0.5f, 0f);
            warnRect.pivot = new Vector2(0.5f, 1f);
            warnRect.anchoredPosition = new Vector2(0f, -4f);
            warnRect.sizeDelta = new Vector2(460f, 30f);

            Image warnBg = enrageWarningRoot.GetComponent<Image>();
            warnBg.color = new Color(0.25f, 0.02f, 0.02f, 0.95f);

            GameObject warnTextObj = new GameObject("EnrageText", typeof(RectTransform), typeof(Text));
            warnTextObj.transform.SetParent(enrageWarningRoot.transform, false);
            RectTransform wtRect = warnTextObj.GetComponent<RectTransform>();
            wtRect.anchorMin = Vector2.zero;
            wtRect.anchorMax = Vector2.one;
            wtRect.offsetMin = Vector2.zero;
            wtRect.offsetMax = Vector2.zero;

            enrageWarningText = warnTextObj.GetComponent<Text>();
            enrageWarningText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            enrageWarningText.fontSize = 16;
            enrageWarningText.fontStyle = FontStyle.Bold;
            enrageWarningText.alignment = TextAnchor.MiddleCenter;
            enrageWarningText.color = EnrageColorPulse1;
            enrageWarningText.text = "⚠️ ENRAGED: SPEED & DAMAGE x2 ⚠️";
            enrageWarningText.raycastTarget = false;

            enrageWarningRoot.SetActive(false);
            HideImmediate();
        }

        public static BossHealthBarUI EnsureExists(Canvas canvas)
        {
            if (Instance != null) return Instance;

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }
            if (canvas == null) return null;

            BossHealthBarUI ui = canvas.GetComponentInChildren<BossHealthBarUI>(true);
            if (ui == null)
            {
                GameObject go = new GameObject("BossHealthBarUI");
                go.transform.SetParent(canvas.transform, false);
                ui = go.AddComponent<BossHealthBarUI>();
            }

            ui.BuildHierarchy();
            ui.HideImmediate();
            ui.SubscribeEvents();
            Instance = ui;
            return ui;
        }
    }
}
