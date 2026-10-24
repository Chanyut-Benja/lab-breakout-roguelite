using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Combat;
using Enemies.Core;
using Enemies.Types;
using UnityEngine;

namespace Enemies
{
    /// <summary>
    /// RoomWaveSpawner (Balance Sheet Section 8):
    /// Controls the 90-second room wave cycle across 3 active phases according to the design sheet:
    /// - 0:00 - 0:20 (Phase 1): Light wave (15% of total room enemies)
    /// - 0:20 - 1:10 (Phase 2): Dense wave (55% of total room enemies)
    /// - 1:10 - 1:30 (Phase 3): Final Swarm wave (30% of total room enemies)
    /// - Enemy Cap: MaxCap = Mathf.RoundToInt(40 + 5.3f * (currentRoom - 1)). Skips spawn if active >= MaxCap (no queuing).
    /// - Spawns unlocked enemies by weight per room (Room 1: Scrap 40 / Crawler 25; Room 2+: Sentinel 15; Room 4+: Guard 10; Room 5+: Kamikaze 10).
    /// - Mini-Boss: Spawns Breach Warden / Siege Walker (HP 1,500, Drop EXP 20) at 01:00 in specified rooms.
    /// - Reposition: Moves enemies > 25m away back to 12-14m around player (preserving HP).
    /// - At 90s: Stops spawner, clears all remaining enemies, vacuums all EXP gems & credits, and opens 2 reward doors.
    /// </summary>
    public class RoomWaveSpawner : MonoBehaviour
    {
        public static RoomWaveSpawner Instance { get; private set; }

        public enum WavePhase
        {
            Phase1_Light = 1,      // 0:00 - 0:20 (15% of total enemies)
            Phase2_Dense = 2,      // 0:20 - 1:10 (55% of total enemies)
            Phase3_FinalSwarm = 3, // 1:10 - 1:30 (30% of total enemies)
            Completed = 4          // 90s+: Finished, stop spawner & open doors
        }

        [Header("Room Timing (Balance Sheet Section 8)")]
        [SerializeField] private float totalRoomDuration = 90.0f;
        [SerializeField] private float currentElapsedTime = 0f;

        [Header("Enemy Prefabs")]
        [SerializeField] private GameObject scrapDronePrefab;
        [SerializeField] private GameObject mutantCrawlerPrefab;
        [SerializeField] private GameObject sentinelBotPrefab;
        [SerializeField] private GameObject armoredGuardPrefab;
        [SerializeField] private GameObject kamikazeDronePrefab;
        [SerializeField] private GameObject breachWardenPrefab;
        [SerializeField] private GameObject siegeWalkerPrefab;

        [Header("Spawn Settings")]
        [SerializeField] private float minSpawnRadius = 12f;
        [SerializeField] private float maxSpawnRadius = 14f;
        [SerializeField] private float repositionThreshold = 25f;
        [SerializeField] private int maxEnemiesAtOnce = 40;

        private Transform player;
        private bool isSpawning = false;
        private WavePhase currentPhase = WavePhase.Phase1_Light;
        private Coroutine waveCoroutine;
        private bool hasSpawnedMiniBossThisRoom = false;

        private readonly List<GameObject> activeEnemies = new List<GameObject>();
        private readonly List<GameObject> pooledEnemies = new List<GameObject>();
        private Bounds arenaBounds;
        private bool hasArenaBounds;

        // Events
        public event Action OnRoomCleared;
        public static event Action OnRoomClearedStatic;
        public event Action<WavePhase> OnPhaseChanged;
        public event Action<float> OnTimerTick;

        // Public Accessors
        public float TotalDuration => totalRoomDuration;
        public float ElapsedTime => currentElapsedTime;
        public float RemainingTime => Mathf.Max(0f, totalRoomDuration - currentElapsedTime);
        public bool IsSpawning => isSpawning;
        public WavePhase CurrentPhase => currentPhase;
        public IReadOnlyList<GameObject> ActiveEnemies => activeEnemies;
        public int MaxEnemiesAtOnce => maxEnemiesAtOnce;

        // Prefab Properties for Inspector/Script hooks
        public GameObject ScrapDronePrefab { get => scrapDronePrefab; set => scrapDronePrefab = value; }
        public GameObject MutantCrawlerPrefab { get => mutantCrawlerPrefab; set => mutantCrawlerPrefab = value; }
        public GameObject SentinelBotPrefab { get => sentinelBotPrefab; set => sentinelBotPrefab = value; }
        public GameObject ArmoredGuardPrefab { get => armoredGuardPrefab; set => armoredGuardPrefab = value; }
        public GameObject KamikazeDronePrefab { get => kamikazeDronePrefab; set => kamikazeDronePrefab = value; }
        public GameObject BreachWardenPrefab { get => breachWardenPrefab; set => breachWardenPrefab = value; }
        public GameObject SiegeWalkerPrefab { get => siegeWalkerPrefab; set => siegeWalkerPrefab = value; }

        // Backwards compatibility aliases for Crawler/Guard/Spitter
        public GameObject CrawlerPrefab { get => mutantCrawlerPrefab; set => mutantCrawlerPrefab = value; }
        public GameObject GuardPrefab { get => armoredGuardPrefab; set => armoredGuardPrefab = value; }
        public GameObject SpitterPrefab { get => sentinelBotPrefab; set => sentinelBotPrefab = value; }

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

            CacheArenaBounds();
            AutoResolvePrefabs();
        }

        private void Start()
        {
            FindPlayer();

            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnRoomStarted -= HandleRoomStarted;
                RoomManager.Instance.OnRoomStarted += HandleRoomStarted;
            }
        }

        private void OnDestroy()
        {
            if (RoomManager.Instance != null)
            {
                RoomManager.Instance.OnRoomStarted -= HandleRoomStarted;
            }
        }

        private void HandleRoomStarted(int room, int floor)
        {
            if (RoomManager.Instance != null && RoomManager.Instance.IsBossRoom)
            {
                // Boss rooms are handled by BossBase and Boss encounters
                return;
            }

            StartSpawning();
        }

        private void Update()
        {
            if (!isSpawning) return;

            currentElapsedTime += Time.deltaTime;
            OnTimerTick?.Invoke(currentElapsedTime);

            WavePhase newPhase = EvaluatePhase(currentElapsedTime);
            if (newPhase != currentPhase)
            {
                currentPhase = newPhase;
                OnPhaseChanged?.Invoke(currentPhase);
                Debug.Log($"[RoomWaveSpawner] Advanced to Wave Phase: {currentPhase} at {currentElapsedTime:F1}s");
            }

            // Check Mini-Boss spawn at 01:00 (60s)
            CheckMiniBossSpawn();

            // Reposition enemies that wander too far away (> 25m)
            RepositionOutOfRangeEnemies();

            // 90s completion check
            if (currentElapsedTime >= totalRoomDuration)
            {
                HandleRoomCompleted();
            }
        }

        public WavePhase EvaluatePhase(float time)
        {
            if (time < 20f) return WavePhase.Phase1_Light;      // 0:00 - 0:20 (Phase 1)
            if (time < 70f) return WavePhase.Phase2_Dense;      // 0:20 - 1:10 (Phase 2)
            if (time < 90f) return WavePhase.Phase3_FinalSwarm; // 1:10 - 1:30 (Phase 3)
            return WavePhase.Completed;                         // 90s+: Complete
        }

        public void StartSpawning()
        {
            isSpawning = true;
            currentElapsedTime = 0f;
            currentPhase = WavePhase.Phase1_Light;
            hasSpawnedMiniBossThisRoom = false;

            if (waveCoroutine != null) StopCoroutine(waveCoroutine);
            waveCoroutine = StartCoroutine(WaveRoutine());

            Debug.Log("[RoomWaveSpawner] 90-Second Room Wave Spawner Started!");
        }

        public void Stop()
        {
            StopSpawning();
        }

        public void StopSpawning()
        {
            isSpawning = false;
            if (waveCoroutine != null)
            {
                StopCoroutine(waveCoroutine);
                waveCoroutine = null;
            }
            Debug.Log("[RoomWaveSpawner] Spawner stopped.");
        }

        public void DebugSetElapsedTime(float timeInSeconds)
        {
            currentElapsedTime = Mathf.Max(0f, timeInSeconds);
            currentPhase = EvaluatePhase(currentElapsedTime);
        }

        /// <summary>
        /// คำนวณจำนวนศัตรูรวมตลอดทั้งห้องตามตารางบาลานซ์ (EXP ของห้อง ÷ 1.1)
        /// ห้อง 1 และ 2 มีศัตรูรวม ~100 ตัว, ห้องถัดไปคำนวณจาก EXP ของห้อง (100 + 10 * ห้อง) ÷ 1.1
        /// </summary>
        public float GetTotalEnemiesForRoom(int roomNumber)
        {
            int clampedRoom = Mathf.Max(1, roomNumber);
            float effectiveRoomExp = clampedRoom <= 2 ? 110f : (100f + 10f * clampedRoom);
            return effectiveRoomExp / 1.1f;
        }

        /// <summary>
        /// คำนวณอัตราเกิด (ตัวต่อวินาที) ตามช่วงเวลาของห้องตามตารางหน้า 13
        /// - 0:00 - 0:20: (ศัตรูรวม * 15%) / 20s
        /// - 0:20 - 1:10: (ศัตรูรวม * 55%) / 50s
        /// - 1:10 - 1:30: (ศัตรูรวม * 30%) / 20s
        /// </summary>
        public float GetCurrentSpawnRate()
        {
            int currentRoom = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
            float totalEnemies = GetTotalEnemiesForRoom(currentRoom);

            if (currentElapsedTime < 20f)
            {
                return (totalEnemies * 0.15f) / 20f;
            }
            if (currentElapsedTime < 70f)
            {
                return (totalEnemies * 0.55f) / 50f;
            }
            return (totalEnemies * 0.30f) / 20f;
        }

        public int GetDynamicEnemyCap()
        {
            int room = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
            maxEnemiesAtOnce = EnemyScaling.GetMaxCap(room);
            return maxEnemiesAtOnce;
        }

        private IEnumerator WaveRoutine()
        {
            while (isSpawning && currentElapsedTime < totalRoomDuration)
            {
                activeEnemies.RemoveAll(e => e == null || !e.activeInHierarchy);

                float currentRate = Mathf.Max(0.1f, GetCurrentSpawnRate());
                float spawnInterval = 1f / currentRate;
                int currentCap = GetDynamicEnemyCap();

                // หากศัตรูบนจอ >= MaxCap ให้ข้ามการเสกทันที (ไม่สะสมคิว)
                if (activeEnemies.Count < currentCap)
                {
                    GameObject chosenPrefab = PickEnemyPrefabForRoom();
                    if (chosenPrefab != null)
                    {
                        SpawnEnemy(chosenPrefab);
                    }
                }

                yield return new WaitForSeconds(spawnInterval);
            }
        }

        /// <summary>
        /// สุ่มชนิดศัตรูตามน้ำหนักและการปลดล็อคตามห้อง (Balance Sheet หน้า 14-15):
        /// - ห้อง 1: Scrap Drone (40), Mutant Crawler (25)
        /// - ห้อง 2+: ปลดล็อค Sentinel Bot (15)
        /// - ห้อง 4+: ปลดล็อค Armored Guard (10)
        /// - ห้อง 5+: ปลดล็อค Kamikaze Drone (10)
        /// สุ่มเฉพาะชนิดที่เปิดใช้แล้วในห้องนั้น โดยนำน้ำหนักมาเทียบกัน
        /// </summary>
        public GameObject PickEnemyPrefabForRoom()
        {
            int room = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
            bool isFloor2 = room >= 10;

            float scrapWeight = 40f;
            float crawlerWeight = 25f;
            float sentinelWeight = room >= 2 ? 15f : 0f;
            float guardWeight = room >= 4 ? 10f : 0f;
            float kamikazeWeight = room >= 5 ? 10f : 0f;

            float totalWeight = scrapWeight + crawlerWeight + sentinelWeight + guardWeight + kamikazeWeight;
            float roll = UnityEngine.Random.Range(0f, totalWeight);

            if (roll < scrapWeight)
            {
                return scrapDronePrefab;
            }
            roll -= scrapWeight;

            if (roll < crawlerWeight)
            {
                return mutantCrawlerPrefab;
            }
            roll -= crawlerWeight;

            if (sentinelWeight > 0f && roll < sentinelWeight)
            {
                return sentinelBotPrefab;
            }
            roll -= sentinelWeight;

            if (guardWeight > 0f && roll < guardWeight)
            {
                return armoredGuardPrefab;
            }
            roll -= guardWeight;

            if (kamikazeWeight > 0f && roll < kamikazeWeight)
            {
                return kamikazeDronePrefab;
            }

            return scrapDronePrefab;
        }

        /// <summary>
        /// ตรวจสอบการเกิดของ Mini-Boss ที่เวลา 01:00 (60 วินาที):
        /// - ห้อง 2, 4, 5, 7, 8: Breach Warden (HP 1,500, Drop EXP 20)
        /// - ห้อง 10, 11, 13, 14, 16, 17: Siege Walker (HP 1,500, Drop EXP 20)
        /// นับรวมในเพดานศัตรู
        /// </summary>
        private void CheckMiniBossSpawn()
        {
            if (hasSpawnedMiniBossThisRoom || currentElapsedTime < 60f) return;

            int room = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
            bool isFloor1MiniBossRoom = (room == 2 || room == 4 || room == 5 || room == 7 || room == 8);
            bool isFloor2MiniBossRoom = (room == 10 || room == 11 || room == 13 || room == 14 || room == 16 || room == 17);

            if (isFloor1MiniBossRoom || isFloor2MiniBossRoom)
            {
                hasSpawnedMiniBossThisRoom = true;
                GameObject miniBossPrefab = isFloor2MiniBossRoom ? (siegeWalkerPrefab ?? breachWardenPrefab) : breachWardenPrefab;
                if (miniBossPrefab != null)
                {
                    GameObject miniBossObj = SpawnEnemy(miniBossPrefab);
                    Debug.Log($"[RoomWaveSpawner] Spawned Mini-Boss for Room {room} at 01:00! Target: {miniBossObj?.name}");
                }
            }
        }

        public GameObject SpawnEnemy(GameObject prefabToSpawn)
        {
            if (player == null)
            {
                FindPlayer();
                if (player == null) return null;
            }

            if (prefabToSpawn == null)
            {
                prefabToSpawn = scrapDronePrefab;
                if (prefabToSpawn == null) return null;
            }

            Vector3 spawnPos = GetRandomSpawnPositionNearPlayer();
            GameObject enemyObj = GetOrCreateFromPool(prefabToSpawn, spawnPos);
            if (enemyObj == null) return null;

            int room = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
            EnemyScaling.ApplyScaling(enemyObj, room, player);

            activeEnemies.Add(enemyObj);
            return enemyObj;
        }

        private GameObject GetOrCreateFromPool(GameObject prefab, Vector3 position)
        {
            for (int i = pooledEnemies.Count - 1; i >= 0; i--)
            {
                GameObject pooled = pooledEnemies[i];
                if (pooled != null && !pooled.activeInHierarchy && pooled.name.StartsWith(prefab.name))
                {
                    pooledEnemies.RemoveAt(i);
                    pooled.transform.position = position;
                    pooled.SetActive(true);
                    return pooled;
                }
            }

            GameObject newObj = Instantiate(prefab, position, Quaternion.identity);
            EnemyBase enemyBase = newObj.GetComponent<EnemyBase>();
            if (enemyBase != null)
            {
                enemyBase.OnDespawnRequested = (eb) => ReturnToPool(eb.gameObject);
            }

            return newObj;
        }

        private void ReturnToPool(GameObject enemyObj)
        {
            if (enemyObj == null) return;
            activeEnemies.Remove(enemyObj);
            pooledEnemies.Add(enemyObj);
            enemyObj.SetActive(false);
        }

        private Vector3 GetRandomSpawnPositionNearPlayer()
        {
            float radius = UnityEngine.Random.Range(minSpawnRadius, maxSpawnRadius);
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            if (randomDir == Vector2.zero) randomDir = Vector2.right;

            Vector3 pos = player.position + new Vector3(randomDir.x * radius, randomDir.y * radius, 0f);
            if (hasArenaBounds)
            {
                pos.x = Mathf.Clamp(pos.x, arenaBounds.min.x + 2f, arenaBounds.max.x - 2f);
                pos.y = Mathf.Clamp(pos.y, arenaBounds.min.y + 2f, arenaBounds.max.y - 2f);
            }
            return pos;
        }

        private void RepositionOutOfRangeEnemies()
        {
            if (player == null)
            {
                FindPlayer();
                if (player == null) return;
            }

            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject enemy = activeEnemies[i];
                if (enemy == null || !enemy.activeInHierarchy)
                {
                    activeEnemies.RemoveAt(i);
                    continue;
                }

                float dist = Vector2.Distance(enemy.transform.position, player.position);
                if (dist > repositionThreshold)
                {
                    Vector3 newPos = GetRandomSpawnPositionNearPlayer();
                    enemy.transform.position = newPos;

                    Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector2.zero;
                    }
                }
            }
        }

        /// <summary>
        /// เมื่อเวลาครบ 90 วินาที:
        /// 1. หยุดการเสกทันที
        /// 2. ทำลาย/สลายศัตรูที่เหลือบนจอ
        /// 3. ดูดเม็ด EXP Gem ทั้งหมดในสนามเข้าหาตัวผู้เล่นอัตโนมัติ
        /// 4. เรียก Event เพื่อเปิดประตูห้อง
        /// </summary>
        private void HandleRoomCompleted()
        {
            Debug.Log("[RoomWaveSpawner] 90 Seconds Reached! Executing Spawner Stop, Screen Clear & Room Complete.");

            // 1. หยุดการเสกทันที
            StopSpawning();
            currentPhase = WavePhase.Completed;

            // 2. ทำลาย/สลายศัตรูที่เหลือบนจอ
            ClearAllEnemies();

            // 3. ดูดเม็ด EXP Gem ทั้งหมดในสนามเข้าหาตัวผู้เล่นอัตโนมัติ (และเครดิต)
            EXPCrystal.VacuumAllActiveCrystals();
            CreditPickup.VacuumAllActiveCredits();

            // 4. เรียก Event เปิดประตูห้อง
            OnRoomCleared?.Invoke();
            OnRoomClearedStatic?.Invoke();

            TriggerRoomManagerDoorOpening();
        }

        private void TriggerRoomManagerDoorOpening()
        {
            if (RoomManager.Instance != null && RoomManager.Instance.IsRoomActive)
            {
                MethodInfo clearMethod = typeof(RoomManager).GetMethod("OnRoomCleared", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (clearMethod != null)
                {
                    clearMethod.Invoke(RoomManager.Instance, null);
                }
            }
        }

        public void ClearAllEnemies()
        {
            for (int i = activeEnemies.Count - 1; i >= 0; i--)
            {
                GameObject enemy = activeEnemies[i];
                if (enemy != null)
                {
                    EnemyBase eb = enemy.GetComponent<EnemyBase>();
                    if (eb != null)
                    {
                        eb.Despawn();
                    }
                    else
                    {
                        Destroy(enemy);
                    }
                }
            }
            activeEnemies.Clear();
        }

        private void FindPlayer()
        {
            if (player != null) return;
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }

        private void CacheArenaBounds()
        {
            GameObject map = GameObject.Find("MapBoundaries");
            if (map != null)
            {
                foreach (BoxCollider2D wall in map.GetComponentsInChildren<BoxCollider2D>())
                {
                    if (!hasArenaBounds) { arenaBounds = wall.bounds; hasArenaBounds = true; }
                    else arenaBounds.Encapsulate(wall.bounds);
                }
            }
        }

        private void AutoResolvePrefabs()
        {
            if (scrapDronePrefab == null)
            {
                scrapDronePrefab = Resources.Load<GameObject>("Prefabs/Enemy_Walker") ??
                                   Resources.Load<GameObject>("Prefabs/Enemy_Minion");
                if (scrapDronePrefab == null) scrapDronePrefab = CreateFallbackPrefab<ScrapDroneEnemy>("ScrapDrone_Fallback", new Color(0.75f, 0.78f, 0.82f, 1f), 20f, 1);
            }

            if (mutantCrawlerPrefab == null)
            {
                mutantCrawlerPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Runner");
                if (mutantCrawlerPrefab == null) mutantCrawlerPrefab = CreateFallbackPrefab<MutantCrawlerEnemy>("MutantCrawler_Fallback", new Color(1f, 0.45f, 0.35f, 1f), 15f, 1);
            }

            if (sentinelBotPrefab == null)
            {
                sentinelBotPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Ranged");
                if (sentinelBotPrefab == null) sentinelBotPrefab = CreateFallbackPrefab<SentinelBotEnemy>("SentinelBot_Fallback", new Color(0.3f, 0.9f, 0.5f, 1f), 35f, 1);
            }

            if (armoredGuardPrefab == null)
            {
                armoredGuardPrefab = Resources.Load<GameObject>("Prefabs/Enemy_Walker");
                if (armoredGuardPrefab == null) armoredGuardPrefab = CreateFallbackPrefab<ArmoredGuardEnemy>("ArmoredGuard_Fallback", new Color(0.35f, 0.55f, 0.80f, 1f), 120f, 2);
            }

            if (kamikazeDronePrefab == null)
            {
                kamikazeDronePrefab = CreateFallbackKamikazePrefab("KamikazeDrone_Fallback");
            }

            if (breachWardenPrefab == null)
            {
                breachWardenPrefab = CreateFallbackPrefab<BreachWardenEnemy>("BreachWarden_Fallback", new Color(0.95f, 0.25f, 0.25f, 1f), 1500f, 20);
            }

            if (siegeWalkerPrefab == null)
            {
                siegeWalkerPrefab = CreateFallbackPrefab<SiegeWalkerEnemy>("SiegeWalker_Fallback", new Color(0.85f, 0.35f, 0.20f, 1f), 1500f, 20);
            }
        }

        private GameObject CreateFallbackPrefab<T>(string name, Color color, float hp, int expDrop) where T : EnemyBase
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            go.tag = "Enemy";

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Minion;
            sr.color = color;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = hp >= 1000f ? 1.0f : (hp >= 100f ? 0.65f : 0.5f);

            EnemyHealth eh = go.AddComponent<EnemyHealth>();
            eh.SetExactMaxHealth(hp);
            eh.SetExpDrop(expDrop);

            go.AddComponent<T>();

            DontDestroyOnLoad(go);
            return go;
        }

        private GameObject CreateFallbackKamikazePrefab(string name)
        {
            GameObject go = new GameObject(name);
            go.SetActive(false);
            go.tag = "Enemy";

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ProceduralSprites.Minion;
            sr.color = new Color(1f, 0.2f, 0.2f, 1f);
            go.transform.localScale = Vector3.one * 0.8f;

            Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;

            EnemyHealth eh = go.AddComponent<EnemyHealth>();
            eh.SetExactMaxHealth(10f);
            eh.SetExpDrop(1);

            go.AddComponent<MutantCrawlerEnemy>(); // Base movement
            go.AddComponent<KamikazeDrone>();

            DontDestroyOnLoad(go);
            return go;
        }
    }
}
