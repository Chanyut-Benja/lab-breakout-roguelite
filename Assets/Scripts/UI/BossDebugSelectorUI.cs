using System;
using Enemies.Bosses;
using Enemies.Core;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Debug Boss Selector: Floating testing panel for spawning and evaluating all 6 bosses.
    /// Toggleable via F1 key or a corner button on the Canvas.
    /// 
    /// Boss Selection Options:
    /// [1. Brute (Rm 3)]      - Base HP: 12,000
    /// [2. Architect (Rm 6)]  - Base HP: 30,000
    /// [3. Doctor (Rm 9)]     - Base HP: 60,000
    /// [4. Digger (Rm 12)]    - Base HP: 80,000
    /// [5. Stormcaller (Rm 15)]- Base HP: 110,000
    /// [6. Overlord (Rm 18)]  - Base HP: 160,000
    /// </summary>
    public class BossDebugSelectorUI : MonoBehaviour
    {
        public static BossDebugSelectorUI Instance { get; private set; }

        [Header("UI Hierarchy")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Button toggleButton;
        [SerializeField] private Transform buttonsContainer;

        private Font cachedFont;
        private bool isPanelOpen = false;

        // Dark Sci-Fi Palette
        private static readonly Color DarkBg = new Color(0.06f, 0.08f, 0.11f, 0.96f);         // #0F141C
        private static readonly Color BorderCyan = new Color(0.15f, 0.70f, 0.85f, 1f);        // #26B3D9
        private static readonly Color BtnNormalBg = new Color(0.12f, 0.17f, 0.24f, 1f);       // #1F2B3D
        private static readonly Color BtnHoverBg = new Color(0.18f, 0.32f, 0.45f, 1f);        // #2E5273
        private static readonly Color TextWhite = new Color(0.95f, 0.98f, 1.0f, 1f);
        private static readonly Color TextYellow = new Color(1.0f, 0.85f, 0.25f, 1f);

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

            if (panelRoot == null)
            {
                BuildHierarchy();
            }

            SetPanelVisible(false);
        }

        private void Update()
        {
            // Toggle with F1 key
            if (Input.GetKeyDown(KeyCode.F1))
            {
                TogglePanel();
            }
        }

        public void TogglePanel()
        {
            SetPanelVisible(!isPanelOpen);
        }

        public void SetPanelVisible(bool visible)
        {
            isPanelOpen = visible;
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        /// <summary>
        /// Selects and spawns the boss for the specified room.
        /// Cleans previous boss, minions, and hazards immediately, then binds BossHealthBarUI.
        /// </summary>
        public BossBase SpawnBoss(int roomNumber)
        {
            Debug.Log($"[BossDebugSelector] Spawning Boss for Room {roomNumber}...");

            // 1. Despawn any existing bosses and minions
            DespawnAllCurrentBossesAndMinions();

            // 2. Clear all previous boss hazards (Acid pools, Turrets, Lasers, Telegraph lines)
            BossBase.ClearAllBossHazards();

            // 3. Determine Spawn Position
            GameObject player = GameObject.FindWithTag("Player");
            Vector3 spawnPos = Vector3.zero;
            if (player != null)
            {
                spawnPos = player.transform.position + new Vector3(3.5f, 2.0f, 0f);
            }

            // 4. Update RoomManager room number if present
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.CurrentRoom = roomNumber;
                RoomManager.Instance.IsBossFight = true;
                RoomManager.Instance.IsRoomActive = true;
            }

            // 5. Instantiate or resolve the requested boss
            GameObject bossObj = CreateBossInstanceForRoom(roomNumber, spawnPos);
            if (bossObj == null)
            {
                Debug.LogError($"[BossDebugSelector] Failed to create boss instance for Room {roomNumber}!");
                return null;
            }

            BossBase newBoss = bossObj.GetComponent<BossBase>();
            if (newBoss != null)
            {
                float baseMaxHP = BossData.GetBalancedMaxHPByRoom(roomNumber);
                float dmgScale = 1f + (roomNumber - 1) * 0.08f;
                newBoss.Init(player != null ? player.transform : null, 1f, dmgScale, 1f);

                if (newBoss.Health != null)
                {
                    newBoss.Health.SetExactMaxHealth(baseMaxHP);
                    newBoss.Health.SetExpDrop(BossBase.GetBalancedExpByRoom(roomNumber));
                }

                // 6. Bind Boss Health Bar UI immediately
                if (BossHealthBarUI.Instance != null)
                {
                    BossHealthBarUI.Instance.BindBoss(newBoss);
                }

                newBoss.NotifyBossSpawned();
            }

            Debug.Log($"[BossDebugSelector] Successfully spawned {newBoss?.name} for Room {roomNumber}!");
            return newBoss;
        }

        private void DespawnAllCurrentBossesAndMinions()
        {
            BossDebugController.ClearAllEnemiesSafe();
        }

        private GameObject CreateBossInstanceForRoom(int roomNumber, Vector3 position)
        {
            Type bossType = roomNumber switch
            {
                3 => typeof(BruteBoss),
                6 => typeof(ArchitectBoss),
                9 => typeof(DoctorBoss),
                12 => typeof(DiggerBoss),
                15 => typeof(StormcallerBoss),
                18 => typeof(OverlordBoss),
                _ => typeof(BruteBoss)
            };

            string prefabPath = roomNumber switch
            {
                3 => "Assets/Prefabs/Enemy_Boss_Brute.prefab",
                6 => "Assets/Prefabs/Enemy_Boss_Architect.prefab",
                9 => "Assets/Prefabs/Enemy_Boss_Doctor.prefab",
                12 => "Assets/Prefabs/Enemy_Boss_Digger.prefab",
                15 => "Assets/Prefabs/Enemy_Boss_Stormcaller.prefab",
                18 => "Assets/Prefabs/Enemy_Boss_Overlord.prefab",
                _ => null
            };

            GameObject loadedPrefab = null;
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(prefabPath))
            {
                loadedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            }
#endif

            if (loadedPrefab != null)
            {
                GameObject bossObj = Instantiate(loadedPrefab, position, Quaternion.identity);
                bossObj.SetActive(true);
                return bossObj;
            }

            // Fallback: Create dynamic boss GameObject with configured components
            string bossName = roomNumber switch
            {
                3 => "Boss_Brute",
                6 => "Boss_Architect",
                9 => "Boss_Doctor",
                12 => "Boss_Digger",
                15 => "Boss_Stormcaller",
                18 => "Boss_Overlord",
                _ => "Boss_Custom"
            };

            GameObject fallbackObj = new GameObject(bossName);
            fallbackObj.transform.position = position;
            fallbackObj.transform.localScale = Vector3.one * 2.2f;
            fallbackObj.tag = "Enemy";

            SpriteRenderer sr = fallbackObj.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Boss;
            sr.color = roomNumber switch
            {
                3 => new Color(1f, 0.4f, 0.3f),
                6 => new Color(0.2f, 0.7f, 1f),
                9 => new Color(0.3f, 1f, 0.4f),
                12 => new Color(0.9f, 0.6f, 0.2f),
                15 => new Color(0.8f, 0.4f, 1f),
                18 => new Color(1f, 0.2f, 0.2f),
                _ => Color.white
            };
            sr.sortingOrder = 10;

            Rigidbody2D rb = fallbackObj.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            BoxCollider2D col = fallbackObj.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.8f, 1.8f);

            fallbackObj.AddComponent(bossType);
            fallbackObj.SetActive(true);

            return fallbackObj;
        }

        public void TriggerEnrageActiveBoss()
        {
            BossBase active = FindFirstObjectByType<BossBase>();
            if (active != null)
            {
                active.TriggerTimeLimitEnrage();
                Debug.Log($"[BossDebugSelector] Forced Enrage triggered on {active.name}!");
            }
        }

        public void KillActiveBoss()
        {
            BossBase active = FindFirstObjectByType<BossBase>();
            if (active != null && active.Health != null)
            {
                active.Health.TakeDamage(active.Health.CurrentHealth + 1000f);
                Debug.Log($"[BossDebugSelector] Killed active boss {active.name}!");
            }
        }

        public void BuildHierarchy()
        {
            if (panelRoot != null) return;

            // 1. Floating Toggle Button (Top-Right)
            GameObject toggleBtnObj = new GameObject("BossTestToggleButton", typeof(RectTransform), typeof(Image), typeof(Button));
            toggleBtnObj.transform.SetParent(transform, false);
            RectTransform tbRect = toggleBtnObj.GetComponent<RectTransform>();
            tbRect.anchorMin = new Vector2(1f, 1f);
            tbRect.anchorMax = new Vector2(1f, 1f);
            tbRect.pivot = new Vector2(1f, 1f);
            tbRect.anchoredPosition = new Vector2(-15f, -15f);
            tbRect.sizeDelta = new Vector2(120f, 36f);

            Image tbImg = toggleBtnObj.GetComponent<Image>();
            tbImg.color = new Color(0.10f, 0.16f, 0.24f, 0.90f);

            Outline tbOutline = toggleBtnObj.AddComponent<Outline>();
            tbOutline.effectColor = BorderCyan;
            tbOutline.effectDistance = new Vector2(1.5f, 1.5f);

            toggleButton = toggleBtnObj.GetComponent<Button>();
            toggleButton.onClick.AddListener(TogglePanel);

            GameObject tbTextObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            tbTextObj.transform.SetParent(toggleBtnObj.transform, false);
            RectTransform tbtRect = tbTextObj.GetComponent<RectTransform>();
            tbtRect.anchorMin = Vector2.zero;
            tbtRect.anchorMax = Vector2.one;
            tbtRect.offsetMin = Vector2.zero;
            tbtRect.offsetMax = Vector2.zero;

            Text tbText = tbTextObj.GetComponent<Text>();
            tbText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            tbText.fontSize = 12;
            tbText.fontStyle = FontStyle.Bold;
            tbText.alignment = TextAnchor.MiddleCenter;
            tbText.color = BorderCyan;
            tbText.text = "⚙️ BOSS [F1]";

            // 2. Main Floating Panel
            panelRoot = new GameObject("BossDebugPanel", typeof(RectTransform), typeof(Image));
            panelRoot.transform.SetParent(transform, false);
            RectTransform pRect = panelRoot.GetComponent<RectTransform>();
            pRect.anchorMin = new Vector2(1f, 1f);
            pRect.anchorMax = new Vector2(1f, 1f);
            pRect.pivot = new Vector2(1f, 1f);
            pRect.anchoredPosition = new Vector2(-15f, -60f);
            pRect.sizeDelta = new Vector2(340f, 440f);

            Image pImg = panelRoot.GetComponent<Image>();
            pImg.color = DarkBg;

            Outline pOutline = panelRoot.AddComponent<Outline>();
            pOutline.effectColor = BorderCyan;
            pOutline.effectDistance = new Vector2(2f, 2f);

            // Panel Header
            GameObject headerObj = new GameObject("Header", typeof(RectTransform), typeof(Text));
            headerObj.transform.SetParent(panelRoot.transform, false);
            RectTransform hRect = headerObj.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -8f);
            hRect.sizeDelta = new Vector2(-20f, 28f);

            Text hText = headerObj.GetComponent<Text>();
            hText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            hText.fontSize = 15;
            hText.fontStyle = FontStyle.Bold;
            hText.alignment = TextAnchor.MiddleCenter;
            hText.color = TextYellow;
            hText.text = "⚡ DEBUG BOSS SELECTOR";

            // Subtitle
            GameObject subObj = new GameObject("Subtitle", typeof(RectTransform), typeof(Text));
            subObj.transform.SetParent(panelRoot.transform, false);
            RectTransform sRect = subObj.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = new Vector2(0f, -34f);
            sRect.sizeDelta = new Vector2(-20f, 20f);

            Text sText = subObj.GetComponent<Text>();
            sText.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            sText.fontSize = 11;
            sText.alignment = TextAnchor.MiddleCenter;
            sText.color = new Color(0.7f, 0.85f, 0.95f, 0.8f);
            sText.text = "Spawn & evaluate boss mechanics:";

            // Buttons Vertical Container
            GameObject containerObj = new GameObject("ButtonsContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
            containerObj.transform.SetParent(panelRoot.transform, false);
            RectTransform cRect = containerObj.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0f, 0f);
            cRect.anchorMax = new Vector2(1f, 1f);
            cRect.offsetMin = new Vector2(14f, 50f);
            cRect.offsetMax = new Vector2(-14f, -60f);

            VerticalLayoutGroup vlg = containerObj.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            buttonsContainer = containerObj.transform;

            // 6 Boss Buttons
            CreateBossSpawnButton("1. Brute (Rm 3) - 12k HP", 3);
            CreateBossSpawnButton("2. Architect (Rm 6) - 30k HP", 6);
            CreateBossSpawnButton("3. Doctor (Rm 9) - 60k HP", 9);
            CreateBossSpawnButton("4. Digger (Rm 12) - 80k HP", 12);
            CreateBossSpawnButton("5. Stormcaller (Rm 15) - 110k HP", 15);
            CreateBossSpawnButton("6. Overlord (Rm 18) - 160k HP", 18);

            // Bottom Quick Actions: Enrage & Kill
            GameObject actionsRow = new GameObject("ActionsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            actionsRow.transform.SetParent(panelRoot.transform, false);
            RectTransform arRect = actionsRow.GetComponent<RectTransform>();
            arRect.anchorMin = new Vector2(0f, 0f);
            arRect.anchorMax = new Vector2(1f, 0f);
            arRect.pivot = new Vector2(0.5f, 0f);
            arRect.anchoredPosition = new Vector2(0f, 10f);
            arRect.sizeDelta = new Vector2(-28f, 32f);

            HorizontalLayoutGroup hlg = actionsRow.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = true;

            CreateActionButton(actionsRow.transform, "⚡ ENRAGE", new Color(0.9f, 0.4f, 0.1f), TriggerEnrageActiveBoss);
            CreateActionButton(actionsRow.transform, "💀 KILL", new Color(0.85f, 0.15f, 0.15f), KillActiveBoss);
        }

        private void CreateBossSpawnButton(string label, int roomNumber)
        {
            GameObject btnObj = new GameObject($"Btn_Room_{roomNumber}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            btnObj.transform.SetParent(buttonsContainer, false);

            LayoutElement le = btnObj.GetComponent<LayoutElement>();
            le.preferredHeight = 36f;

            Image img = btnObj.GetComponent<Image>();
            img.color = BtnNormalBg;

            Button btn = btnObj.GetComponent<Button>();
            ColorBlock cb = btn.colors;
            cb.normalColor = BtnNormalBg;
            cb.highlightedColor = BtnHoverBg;
            cb.pressedColor = BorderCyan;
            btn.colors = cb;
            btn.onClick.AddListener(() => SpawnBoss(roomNumber));

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 0f);
            tr.offsetMax = new Vector2(-10f, 0f);

            Text txt = textObj.GetComponent<Text>();
            txt.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.color = TextWhite;
            txt.text = label;
        }

        private void CreateActionButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action)
        {
            GameObject btnObj = new GameObject($"Btn_{label}", typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.GetComponent<Image>();
            img.color = color;

            Button btn = btnObj.GetComponent<Button>();
            btn.onClick.AddListener(action);

            GameObject textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform tr = textObj.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            Text txt = textObj.GetComponent<Text>();
            txt.font = cachedFont ?? InGameHUDController.GetDefaultFont();
            txt.fontSize = 12;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.text = label;
        }

        public static BossDebugSelectorUI EnsureExists(Canvas canvas)
        {
            if (Instance != null) return Instance;

            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            }
            if (canvas == null) return null;

            BossDebugSelectorUI ui = canvas.GetComponentInChildren<BossDebugSelectorUI>(true);
            if (ui == null)
            {
                GameObject go = new GameObject("BossDebugSelectorUI");
                go.transform.SetParent(canvas.transform, false);
                ui = go.AddComponent<BossDebugSelectorUI>();
            }

            ui.BuildHierarchy();
            Instance = ui;
            return ui;
        }
    }
}
