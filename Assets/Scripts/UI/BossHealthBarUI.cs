using System.Collections;
using Enemies.Bosses;
using Enemies.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Dark Sci-Fi Boss Health Bar UI (Top-Center)
    /// Attached to BossHealthPanel on the main Canvas.
    /// 1. Guaranteed parenting to the root Canvas (never attached to popup panels like LevelUpPanel).
    /// 2. Positioned at Top-Center (below room/objective text).
    /// 3. Main GameObject (BossHealthPanel) ALWAYS remains Active to ensure script execution.
    /// 4. Bar visuals are managed via child GameObject (barRoot / barContainer).
    /// 5. Health Bar: Red-Orange Sci-Fi theme (#FF3344 to #FF6B00), smooth lerp, with current / max HP display.
    /// 6. Phase Markers: Visual divider notches and indicators at 66% (Phase 2) and 33% (Phase 3 / Enrage).
    /// 7. Enrage Warning Indicator: Icon and pulsing blinking effect when boss enters Enrage state (3 minutes / Phase 3).
    /// 8. Auto-Hide: Fades out and deactivates child barRoot automatically when the boss is defeated.
    /// </summary>
    public class BossHealthBarUI : MonoBehaviour
    {
        private static BossHealthBarUI instance;
        public static BossHealthBarUI Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
                    if (instance == null)
                    {
                        instance = FindFirstObjectByType<BossHealthBarUI>();
                    }
                    if (instance == null)
                    {
                        instance = EnsureExists();
                    }
                }
                return instance;
            }
            private set => instance = value;
        }

        [Header("UI Hierarchy")]
        [SerializeField] private GameObject barRoot;       // Child GameObject controlling visibility
        [SerializeField] private GameObject barContainer;  // Compatibility alias pointing to barRoot
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Image fillImage;
        [SerializeField] private Text hpNumberText;
        [SerializeField] private GameObject enrageWarningRoot;
        [SerializeField] private Text enrageWarningText;

        [Header("Phase Markers (66% and 33%)")]
        [SerializeField] private GameObject phaseMarker66;
        [SerializeField] private GameObject phaseMarker33;

        [Header("Colors (Dark Sci-Fi Theme)")]
        private static readonly Color DarkBgColor = new Color(0.05f, 0.07f, 0.09f, 0.95f);       // #0D1117
        private static readonly Color BorderSlate = new Color(0.19f, 0.24f, 0.32f, 1f);          // #30363D
        private static readonly Color HeaderCyan = new Color(0.0f, 0.90f, 1.0f, 1f);             // #00E5FF
        private static readonly Color NeonOrangeRed = new Color(1.0f, 0.22f, 0.15f, 1f);         // #FF3826
        private static readonly Color EnrageColorPulse1 = new Color(1.0f, 0.15f, 0.15f, 1f);     // Red
        private static readonly Color EnrageColorPulse2 = new Color(1.0f, 0.65f, 0.0f, 1f);      // Bright Orange
        private static readonly Color Marker66Color = new Color(0.20f, 0.85f, 1.0f, 0.95f);      // Cyan
        private static readonly Color Marker33Color = new Color(1.00f, 0.35f, 0.20f, 0.95f);     // Neon Red/Orange

        private BossBase activeBoss;
        private float targetHealth;
        private float maxHealth = 100f;
        private float displayedHealth;
        private bool isEnraged = false;
        private Coroutine fadeCoroutine;
        private Font cachedFont;
        private bool isRoomManagerSubscribed = false;

        public bool IsVisible => barRoot != null && barRoot.activeSelf && (canvasGroup == null || canvasGroup.alpha > 0.01f);
        public BossBase ActiveBoss => activeBoss;
        public GameObject BarRoot => barRoot;

        private void Awake()
        {
            // Ensure main GameObject stays ACTIVE
            gameObject.SetActive(true);

            if (instance == null)
            {
                instance = this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            cachedFont = InGameHUDController.GetDefaultFont();

            EnsureMainCanvasParent();

            if (barRoot == null && barContainer == null)
            {
                BuildHierarchy();
            }
            else
            {
                if (barRoot == null) barRoot = barContainer;
                if (barContainer == null) barContainer = barRoot;
                EnsurePhaseMarkers();
            }

            HideImmediate();
        }

        private void OnEnable()
        {
            SubscribeEvents();
            SubscribeRoomManager();
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
            UnsubscribeRoomManager();

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

        private void SubscribeRoomManager()
        {
            if (isRoomManagerSubscribed) return;
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnRoomStarted += HandleRoomStarted;
                isRoomManagerSubscribed = true;
            }
        }

        private void UnsubscribeRoomManager()
        {
            if (isRoomManagerSubscribed && RoomManager.Instance != null)
            {
                RoomManager.Instance.OnRoomStarted -= HandleRoomStarted;
            }
            isRoomManagerSubscribed = false;
        }

        private void HandleRoomStarted(int room, int floor)
        {
            // ซ่อนแถบเลือดเมื่ออยู่นอกห้องบอส (เปิดเฉพาะในห้อง 3, 6, 9, 12, 15, 18)
            if (!IsBossRoom(room))
            {
                HideImmediate();
            }
        }

        /// <summary>
        /// ตรวจสอบว่าห้องที่ระบุเป็นห้องบอสหรือไม่ (ห้อง 3, 6, 9, 12, 15, 18)
        /// </summary>
        public static bool IsBossRoom(int roomNumber)
        {
            return roomNumber == 3 || roomNumber == 6 || roomNumber == 9 ||
                   roomNumber == 12 || roomNumber == 15 || roomNumber == 18;
        }

        private void Update()
        {
            // Main GameObject must always remain active
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            // Subscribe to RoomManager if it was initialized later
            if (!isRoomManagerSubscribed && RoomManager.Instance != null)
            {
                SubscribeRoomManager();
            }

            // ซ่อนแถบเลือดเมื่ออยู่นอกห้องบอสเฉพาะเมื่อไม่มีบอสต่อสู้จริง
            if (activeBoss == null && RoomManager.Instance != null && !IsBossRoom(RoomManager.Instance.CurrentRoom))
            {
                if (IsVisible)
                {
                    HideImmediate();
                }
                return;
            }

            if (activeBoss != null)
            {
                // Verify if boss entered enrage state
                bool currentEnraged = activeBoss.IsEnraged || activeBoss.IsTimeEnraged;
                if (currentEnraged != isEnraged)
                {
                    SetEnrageState(currentEnraged);
                }

                if (activeBoss.Health != null && activeBoss.Health.IsDead && IsVisible)
                {
                    HideBossBar();
                }
            }

            // Smooth health bar interpolation using unscaledDeltaTime
            if (Mathf.Abs(displayedHealth - targetHealth) > 0.1f)
            {
                displayedHealth = Mathf.Lerp(displayedHealth, targetHealth, Time.unscaledDeltaTime * 12f);
                UpdateHealthVisuals(displayedHealth, maxHealth);
            }

            // Pulsing Enrage Animation using unscaledTime
            if (isEnraged && enrageWarningText != null && enrageWarningRoot != null && enrageWarningRoot.activeInHierarchy)
            {
                float t = Mathf.PingPong(Time.unscaledTime * 3.5f, 1f);
                enrageWarningText.color = Color.Lerp(EnrageColorPulse1, EnrageColorPulse2, t);
            }
        }

        #region Public Boss API (Requirement 1 & 2)

        /// <summary>
        /// เมื่อบอส Spawn: เรียก BossHealthBarUI.Instance.ShowBossBar(bossName, maxHp);
        /// </summary>
        public void ShowBossBar(string bossName, float maxHp)
        {
            EnsureMainCanvasParent();
            gameObject.SetActive(true);
            Show(bossName, maxHp, maxHp);
        }

        /// <summary>
        /// เมื่อบอสรับดาเมจ (TakeDamage): เรียก BossHealthBarUI.Instance.UpdateBossHealth(currentHp);
        /// </summary>
        public void UpdateBossHealth(float currentHp)
        {
            targetHealth = Mathf.Max(0f, currentHp);
            if (targetHealth <= 0f)
            {
                HideBossBar();
            }
        }

        /// <summary>
        /// เมื่อบอสเข้าสู่สภาวะ Enrage (ครบ 3 นาที หรือ Phase 3): เรียก BossHealthBarUI.Instance.SetEnrageState(true);
        /// </summary>
        public void SetEnrageState(bool enraged)
        {
            SetEnragedState(enraged);
        }

        /// <summary>
        /// เมื่อบอสตาย: เรียก BossHealthBarUI.Instance.HideBossBar();
        /// </summary>
        public void HideBossBar()
        {
            HideImmediate();   // gone the moment the boss dies (no fade-out)
        }

        #endregion

        public void BindBoss(BossBase boss)
        {
            if (boss == null)
            {
                HideBossBar();
                return;
            }

            EnsureMainCanvasParent();

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

            ShowBossBar(formattedTitle, mHp);
            UpdateBossHealth(curHp);
            SetEnrageState(boss.IsEnraged || boss.IsTimeEnraged);
        }

        private void HandleBossDiedDirect(EnemyHealth h)
        {
            HideBossBar();
        }

        private void HandleHealthChangedDirect(float current, float max)
        {
            HandleBossHealthChanged(current, max);
        }

        private void HandleBossSpawned(string bossTitle, float curHp, float mHp)
        {
            ShowBossBar(bossTitle, mHp);
            UpdateBossHealth(curHp);
        }

        private void HandleBossHealthChanged(float curHp, float mHp)
        {
            maxHealth = Mathf.Max(1f, mHp);
            UpdateBossHealth(curHp);
        }

        private void HandleBossPhaseChanged(int phaseNumber)
        {
            if (phaseNumber >= 3)
            {
                SetEnrageState(true);
            }
        }

        private void HandleBossDied(BossData data)
        {
            HideBossBar();
        }

        public void Show(string title, float curHp, float mHp)
        {
            EnsureMainCanvasParent();

            // Ensure main GameObject stays active
            gameObject.SetActive(true);

            if (barRoot == null) BuildHierarchy();
            EnsurePhaseMarkers();

            if (fadeCoroutine != null) StopCoroutine(fadeCoroutine);

            maxHealth = Mathf.Max(1f, mHp);
            targetHealth = Mathf.Clamp(curHp, 0f, maxHealth);
            displayedHealth = targetHealth;

            if (bossNameText != null)
            {
                bossNameText.text = title.ToUpper();
            }

            UpdateHealthVisuals(displayedHealth, maxHealth);
            SetEnrageState(false);

            if (barRoot != null)
            {
                barRoot.SetActive(true);
            }
            if (barContainer != null && barContainer != barRoot)
            {
                barContainer.SetActive(true);
            }

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

            // Only deactivate the child barRoot, never the main GameObject!
            if (barRoot != null) barRoot.SetActive(false);
            if (barContainer != null && barContainer != gameObject) barContainer.SetActive(false);

            activeBoss = null;
            isEnraged = false;

            // Keep main GameObject active
            gameObject.SetActive(true);
        }

        private IEnumerator FadeOutRoutine()
        {
            if (canvasGroup != null)
            {
                float t = 1f;
                while (t > 0f)
                {
                    t -= Time.unscaledDeltaTime * 2.5f;
                    canvasGroup.alpha = Mathf.Clamp01(t);
                    yield return null;
                }
            }

            HideImmediate();
            fadeCoroutine = null;
        }

        public void SetEnragedState(bool enraged)
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
        /// Procedural UI construction on the main Canvas ensuring zero missing dependencies.
        /// The main GameObject (BossHealthPanel) stays ACTIVE and is anchored at Top-Center (below room/timer texts).
        /// Child GameObject (barRoot) contains all visual elements and handles show/hide.
        /// </summary>
        public void BuildHierarchy()
        {
            EnsureMainCanvasParent();
            gameObject.SetActive(true);

            if (barRoot == null)
            {
                Transform existing = transform.Find("BarRoot");
                if (existing == null) existing = transform.Find("BossHealthBarContainer");
                if (existing != null)
                {
                    barRoot = existing.gameObject;
                }
                else
                {
                    barRoot = new GameObject("BarRoot", typeof(RectTransform), typeof(CanvasGroup));
                    barRoot.transform.SetParent(transform, false);
                }
            }

            barContainer = barRoot;
            canvasGroup = barRoot.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = barRoot.AddComponent<CanvasGroup>();

            // barRoot spans the full area of BossHealthPanel
            RectTransform rootRect = barRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            // Background Border Panel
            Transform existingBg = barRoot.transform.Find("BgPanel");
            GameObject bgPanel;
            if (existingBg != null)
            {
                bgPanel = existingBg.gameObject;
            }
            else
            {
                bgPanel = new GameObject("BgPanel", typeof(RectTransform), typeof(Image));
                bgPanel.transform.SetParent(barRoot.transform, false);
                RectTransform bgRect = bgPanel.GetComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                Image bgImg = bgPanel.GetComponent<Image>();
                bgImg.color = DarkBgColor;

                Outline outline = bgPanel.AddComponent<Outline>();
                outline.effectColor = BorderSlate;
                outline.effectDistance = new Vector2(2f, 2f);
            }

            // 2. Boss Name Text (Top Row)
            Transform existingName = barRoot.transform.Find("BossNameText");
            if (existingName != null)
            {
                bossNameText = existingName.GetComponent<Text>();
            }
            else
            {
                GameObject nameObj = new GameObject("BossNameText", typeof(RectTransform), typeof(Text), typeof(Shadow));
                nameObj.transform.SetParent(barRoot.transform, false);
                RectTransform nameRect = nameObj.GetComponent<RectTransform>();
                nameRect.anchorMin = new Vector2(0f, 1f);
                nameRect.anchorMax = new Vector2(1f, 1f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, -4f);
                nameRect.sizeDelta = new Vector2(-24f, 28f);

                bossNameText = nameObj.GetComponent<Text>();
                bossNameText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
                bossNameText.fontSize = 22;
                bossNameText.fontStyle = FontStyle.Bold;
                bossNameText.alignment = TextAnchor.MiddleCenter;
                bossNameText.color = HeaderCyan;
                bossNameText.text = "BOSS ENCOUNTER";
                bossNameText.raycastTarget = false;

                Shadow nameShadow = nameObj.GetComponent<Shadow>();
                nameShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                nameShadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 3. Health Bar Slider
            Transform existingSlider = barRoot.transform.Find("BossSlider");
            GameObject sliderObj;
            if (existingSlider != null)
            {
                sliderObj = existingSlider.gameObject;
                healthSlider = sliderObj.GetComponent<Slider>();
            }
            else
            {
                sliderObj = new GameObject("BossSlider", typeof(RectTransform), typeof(Slider));
                sliderObj.transform.SetParent(barRoot.transform, false);
                RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
                sliderRect.anchorMin = new Vector2(0f, 0f);
                sliderRect.anchorMax = new Vector2(1f, 0f);
                sliderRect.pivot = new Vector2(0.5f, 0f);
                sliderRect.anchoredPosition = new Vector2(0f, 8f);
                sliderRect.sizeDelta = new Vector2(-30f, 36f);

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
                hpNumberText.fontSize = 18;
                hpNumberText.fontStyle = FontStyle.Bold;
                hpNumberText.alignment = TextAnchor.MiddleCenter;
                hpNumberText.color = Color.white;
                hpNumberText.text = "100 / 100";
                hpNumberText.raycastTarget = false;

                Shadow hpShadow = hpTextObj.GetComponent<Shadow>();
                hpShadow.effectColor = new Color(0f, 0f, 0f, 0.95f);
                hpShadow.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 5. Phase Markers (ขีดแบ่งเฟสที่ 66% และ 33%)
            EnsurePhaseMarkers();

            // 6. Enrage Warning Banner (Below Bar)
            Transform existingWarn = barRoot.transform.Find("EnrageWarningBanner");
            if (existingWarn != null)
            {
                enrageWarningRoot = existingWarn.gameObject;
                enrageWarningText = enrageWarningRoot.GetComponentInChildren<Text>();
            }
            else
            {
                enrageWarningRoot = new GameObject("EnrageWarningBanner", typeof(RectTransform), typeof(Image));
                enrageWarningRoot.transform.SetParent(barRoot.transform, false);
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
            }

            HideImmediate();
        }

        /// <summary>
        /// ตรวจสอบและสร้างมาร์กเกอร์บอกขีดแบ่งเฟสที่ 66% (Phase 2) และ 33% (Phase 3 Enrage)
        /// </summary>
        public void EnsurePhaseMarkers()
        {
            if (healthSlider == null) return;

            Transform parent = healthSlider.transform;

            if (phaseMarker66 == null)
            {
                Transform existing = parent.Find("PhaseMarker_66");
                if (existing != null)
                {
                    phaseMarker66 = existing.gameObject;
                }
                else
                {
                    phaseMarker66 = CreatePhaseMarker(parent, 0.66f, "PhaseMarker_66", "66%", Marker66Color);
                }
            }

            if (phaseMarker33 == null)
            {
                Transform existing = parent.Find("PhaseMarker_33");
                if (existing != null)
                {
                    phaseMarker33 = existing.gameObject;
                }
                else
                {
                    phaseMarker33 = CreatePhaseMarker(parent, 0.33f, "PhaseMarker_33", "33%", Marker33Color);
                }
            }

            // Ensure markers and HP text render in front of fill
            if (phaseMarker66 != null) phaseMarker66.transform.SetAsLastSibling();
            if (phaseMarker33 != null) phaseMarker33.transform.SetAsLastSibling();
            if (hpNumberText != null) hpNumberText.transform.SetAsLastSibling();
        }

        private GameObject CreatePhaseMarker(Transform parent, float normalizedX, string markerName, string label, Color markerColor)
        {
            GameObject markerObj = new GameObject(markerName, typeof(RectTransform), typeof(Image));
            markerObj.transform.SetParent(parent, false);

            RectTransform rect = markerObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(normalizedX, 0f);
            rect.anchorMax = new Vector2(normalizedX, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(3f, 8f); // ขีดแบ่งกว้าง 3px ยื่นหัวท้าย 4px เพื่อความชัดเจน

            Image img = markerObj.GetComponent<Image>();
            img.color = markerColor;
            img.raycastTarget = false;

            Outline outline = markerObj.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1f, 1f);

            // Notch indicator บนยอดขีด
            GameObject notchObj = new GameObject("Notch", typeof(RectTransform), typeof(Image));
            notchObj.transform.SetParent(markerObj.transform, false);
            RectTransform nRect = notchObj.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.5f, 1f);
            nRect.anchorMax = new Vector2(0.5f, 1f);
            nRect.pivot = new Vector2(0.5f, 0f);
            nRect.anchoredPosition = new Vector2(0f, 2f);
            nRect.sizeDelta = new Vector2(6f, 6f);
            Image nImg = notchObj.GetComponent<Image>();
            nImg.color = markerColor;
            nImg.raycastTarget = false;

            // ตัวหนังสือระบุ % เฟส
            GameObject labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
            labelObj.transform.SetParent(markerObj.transform, false);
            RectTransform lRect = labelObj.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0.5f, 1f);
            lRect.anchorMax = new Vector2(0.5f, 1f);
            lRect.pivot = new Vector2(0.5f, 0f);
            lRect.anchoredPosition = new Vector2(0f, 7f);
            lRect.sizeDelta = new Vector2(40f, 16f);

            Text txt = labelObj.GetComponent<Text>();
            txt.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            txt.fontSize = 11;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = markerColor;
            txt.text = label;
            txt.raycastTarget = false;

            Shadow sh = labelObj.GetComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.95f);
            sh.effectDistance = new Vector2(1f, -1f);

            return markerObj;
        }

        /// <summary>
        /// ค้นหา Root Canvas ของเกมอย่างแม่นยำ (ไม่เลือก Canvas ย่อยของ LevelUpPanel หรือ Popup อื่น)
        /// </summary>
        public static Canvas GetMainCanvas()
        {
            if (InGameHUDController.Instance != null)
            {
                Canvas hudCanvas = InGameHUDController.Instance.GetComponentInParent<Canvas>();
                if (hudCanvas != null && hudCanvas.isRootCanvas)
                {
                    return hudCanvas;
                }
            }

            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo != null)
            {
                Canvas c = canvasGo.GetComponent<Canvas>();
                if (c != null && c.isRootCanvas) return c;
            }

            Canvas[] allCanvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Canvas c in allCanvases)
            {
                if (c == null) continue;
                string n = c.gameObject.name.ToLower();
                if (n.Contains("levelup") || n.Contains("pause") || n.Contains("shop") || n.Contains("boon") || n.Contains("gameover"))
                    continue;
                if (c.isRootCanvas)
                    return c;
            }

            return Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// การันตีว่า BossHealthPanel ผูกอยู่ใต้ Root Canvas และมี RectTransform ที่ Top-Center อย่างถูกต้อง
        /// </summary>
        public void EnsureMainCanvasParent()
        {
            Canvas mainCanvas = GetMainCanvas();
            if (mainCanvas != null && transform.parent != mainCanvas.transform)
            {
                transform.SetParent(mainCanvas.transform, false);
            }

            RectTransform panelRect = GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 1f);
                panelRect.anchorMax = new Vector2(0.5f, 1f);
                panelRect.pivot = new Vector2(0.5f, 1f);
                panelRect.anchoredPosition = new Vector2(0f, -135f); // อยู่ใต้ TimerText และ RoomText อย่างลงตัว
                panelRect.sizeDelta = new Vector2(860f, 82f);
            }
        }

        /// <summary>
        /// ค้นหาหรือสร้าง BossHealthBarUI บน Canvas โดยให้ GameObject หลักเปิด Active เสมอ และผูกใต้ Root Canvas
        /// </summary>
        public static BossHealthBarUI EnsureExists(Canvas canvas = null)
        {
            if (canvas == null)
            {
                canvas = GetMainCanvas();
            }

            if (instance != null && instance.gameObject != null)
            {
                instance.EnsureMainCanvasParent();
                instance.gameObject.SetActive(true);
                return instance;
            }

            // 1. ค้นหา BossHealthBarUI ในซีนก่อน (รวม inactive)
            BossHealthBarUI existingUI = FindFirstObjectByType<BossHealthBarUI>(FindObjectsInactive.Include);
            if (existingUI != null)
            {
                existingUI.EnsureMainCanvasParent();
                existingUI.gameObject.SetActive(true);
                instance = existingUI;
                return instance;
            }

            if (canvas == null) return null;

            // 2. ค้นหา BossHealthPanel บน Canvas
            Transform panelTrans = canvas.transform.Find("BossHealthPanel");
            if (panelTrans == null)
            {
                foreach (Transform child in canvas.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "BossHealthPanel")
                    {
                        panelTrans = child;
                        break;
                    }
                }
            }

            BossHealthBarUI ui = null;
            if (panelTrans != null)
            {
                panelTrans.gameObject.SetActive(true);
                ui = panelTrans.GetComponent<BossHealthBarUI>();
                if (ui == null)
                {
                    ui = panelTrans.gameObject.AddComponent<BossHealthBarUI>();
                }
            }
            else
            {
                GameObject go = new GameObject("BossHealthPanel", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                go.SetActive(true);
                ui = go.AddComponent<BossHealthBarUI>();
            }

            ui.EnsureMainCanvasParent();
            ui.BuildHierarchy();
            ui.HideImmediate();
            ui.SubscribeEvents();
            ui.SubscribeRoomManager();
            instance = ui;
            return ui;
        }
    }
}
