using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Enemies.Bosses;

public class RoomManager : MonoBehaviour
{
    public static RoomManager Instance { get; private set; }

    [Header("Room & Floor Settings")]
    [SerializeField] private int currentRoom = 1;
    [SerializeField] private int roomsPerFloor = 9; // 9 ห้องต่อ 1 Floor
    [SerializeField] private int totalFloors = 2;   // 2 Floor = 18 ห้องรวม
    // Room length in seconds. The code value wins over whatever the scene has serialized (set to 90 for the final game).
    public const float RoomSeconds = 90f;
    [SerializeField] private float roomDuration = RoomSeconds;

    [Header("Containment Purge Settings")]
    private const float purgeCountdownDuration = 10f;
    [SerializeField] private float purgeHpDrainPerSecond = 0.05f; // ลดเลือดวินาทีละ 5% ของ Max HP
    [SerializeField] private float purgeGasExposureDuration = 1.0f;

    [Header("Door Prefab & Spawning")]
    [SerializeField] private GameObject roomDoorPrefab;
    [SerializeField] private Transform[] doorSpawnPoints;
    [SerializeField] private Transform playerSpawnPoint;

    [Header("UI References")]
    [SerializeField] private Text timerText;
    [SerializeField] private Text roomText;
    [SerializeField] private GameObject boonUI;
    [SerializeField] private GameObject shopUI;
    [SerializeField] private GameObject bossAlertUI;

    [Header("Boss Settings")]
    [SerializeField] private GameObject[] bossPrefabs; // บอสประจำห้อง 3, 6, 9 ของแต่ละ Floor
    [SerializeField] private Transform bossSpawnPoint;

    private float roomTimer;
    private bool isRoomActive = false;
    private bool isBossFight = false;

    // Containment Purge & Door Transition State
    private bool isWaitingForDoor = false;
    private bool isPurgeActive = false;
    private float purgeCountdownTimer = 0f;
    private float purgeExposureTimer = 0f;
    private readonly List<GameObject> activeDoors = new List<GameObject>();
    private readonly List<RoomRewardType> currentOfferedDoorTypes = new List<RoomRewardType>();

    // Door Weight & Restriction Tracking (Section 6 Balance Rules)
    private bool hasBoonAppearedInRooms1To3 = false;
    private bool medicalPodAppearedLastRoom = false;
    private int medicalPodCountThisFloor = 0;
    private int trackedFloorForDoors = 1;
    private int geneEnhancerCountThisRun = 0;

    private Sprite boundarySprite;
    private Coroutine boundaryNotice;

    // Properties คำนวณ Floor และ Room แสดงผล
    public int CurrentRoom { get => currentRoom; set => currentRoom = value; }
    public bool IsRoomActive { get => isRoomActive; set => isRoomActive = value; }
    public bool IsBossFight { get => isBossFight; set => isBossFight = value; }
    public int MaxRooms => roomsPerFloor * totalFloors;

    public int CurrentFloor => ((currentRoom - 1) / roomsPerFloor) + 1;
    public int CurrentRoomInFloor => ((currentRoom - 1) % roomsPerFloor) + 1;
    public bool IsBossRoom => CurrentRoomInFloor % 3 == 0;
    public bool IsFinalBossRoom => currentRoom == MaxRooms;
    public bool IsPurgeActive => isPurgeActive;

    // Events
    public event Action<int, int> OnRoomStarted;
    public event Action OnRoomClearedEvent;
    public event Action OnBossEncounter;

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

        Time.timeScale = 1f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (GetComponent<DebugCheats>() == null && FindFirstObjectByType<DebugCheats>() == null)
        {
            gameObject.AddComponent<DebugCheats>();
        }
#endif
    }

    private void Start()
    {
        roomDuration = RoomSeconds;
        if (timerText != null)
        {
            timerText.fontSize = 34;
            timerText.color = Color.white;
            MakeReadable(timerText);
            timerText.horizontalOverflow = HorizontalWrapMode.Overflow;
            timerText.verticalOverflow = VerticalWrapMode.Overflow;
        }

        if (roomText != null)
        {
            roomText.color = new Color(0.75f, 0.95f, 1f);
            MakeReadable(roomText);
        }

        // the walls are painted into each room map now (see RoomEnvironment); no extra strips needed
        RoomEnvironment.Ensure();
        StartNewRoom();
    }

    // Dark outline + soft shadow so white/cyan text stays readable over any background.
    private static void MakeReadable(Text t)
    {
        if (t == null) return;
        Outline o = t.GetComponent<Outline>();
        if (o == null) o = t.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.95f);
        o.effectDistance = new Vector2(2f, -2f);
        o.useGraphicAlpha = false;
        t.resizeTextForBestFit = true;
        t.resizeTextMaxSize = t.fontSize;
        t.resizeTextMinSize = Mathf.Max(14, t.fontSize / 2);
    }

    private void CreateBoundaryVisuals()
    {
        GameObject map = GameObject.Find("MapBoundaries");
        if (map == null)
        {
            Debug.LogWarning("[RoomManager] MapBoundaries is missing from the scene.");
            return;
        }

        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply();
        boundarySprite = Sprite.Create(pixel, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        foreach (BoxCollider2D wall in map.GetComponentsInChildren<BoxCollider2D>())
        {
            // Visuals follow the existing physical collider, including its offset and dimensions.
            GameObject strip = new GameObject("Visible_" + wall.name);
            strip.transform.SetParent(wall.transform, false);
            strip.transform.localPosition = new Vector3(wall.offset.x, wall.offset.y, 0f);
            strip.transform.localScale = new Vector3(wall.size.x, wall.size.y, 1f);
            SpriteRenderer stripRenderer = strip.AddComponent<SpriteRenderer>();
            stripRenderer.sprite = boundarySprite;
            stripRenderer.color = new Color(0.13f, 0.69f, 0.82f, 0.8f);
            stripRenderer.sortingOrder = -50;
        }
    }

    public void ShowBoundaryNotice()
    {
        if (roomText == null) return;
        if (boundaryNotice != null) StopCoroutine(boundaryNotice);
        boundaryNotice = StartCoroutine(ShowBoundaryNoticeRoutine());
    }

    private IEnumerator ShowBoundaryNoticeRoutine()
    {
        roomText.text = "MAP LIMIT  |  TURN BACK";
        roomText.color = new Color(1f, 0.85f, 0.3f);
        yield return new WaitForSeconds(1.3f);
        roomText.color = new Color(0.75f, 0.95f, 1f);
        UpdateRoomUI();
        boundaryNotice = null;
    }

    public void StartNewRoom()
    {
        ClearExistingDoors();
        isWaitingForDoor = false;
        isPurgeActive = false;
        isRoomActive = true;
        isBossFight = IsBossRoom;

        if (CurrentFloor != trackedFloorForDoors)
        {
            trackedFloorForDoors = CurrentFloor;
            medicalPodCountThisFloor = 0;
        }

        UpdateRoomUI();

        // load the separate map for this room (floor picture, walls, doorways), then place the player at its entrance
        RoomEnvironment roomEnv = RoomEnvironment.Ensure();
        roomEnv.LoadRoom(currentRoom);
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                player.transform.position = roomEnv.PlayerSpawn;
                Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
                if (playerBody != null) playerBody.linearVelocity = Vector2.zero;
            }
        }

        if (isBossFight)
        {
            StartBossRoom();
        }
        else
        {
            StartNormalRoom();
        }

        OnRoomStarted?.Invoke(currentRoom, CurrentFloor);
    }

    private void StartNormalRoom()
    {
        roomTimer = roomDuration;
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.StartSpawning();
        }
    }

    private void StartBossRoom()
    {
        Debug.Log($"[RoomManager] === Boss Encounter: Floor {CurrentFloor} | Room {CurrentRoomInFloor}/{roomsPerFloor} ===");

        // ในห้องบอส ให้มีลูกน้องเกิดต่อเนื่องโดยใช้ Enemy Cap เป็นครึ่งหนึ่งของห้องปกติ
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.StartSpawning();
        }

        if (bossAlertUI != null)
        {
            bossAlertUI.SetActive(true);
        }

        SpawnBossEntity();
        OnBossEncounter?.Invoke();
    }

    private void SpawnBossEntity()
    {
        int bossIndex = (currentRoom / 3) - 1;
        GameObject player = GameObject.FindWithTag("Player");
        Camera camera = Camera.main;
        float halfHeight = camera != null ? camera.orthographicSize : 10f;
        float halfWidth = camera != null ? halfHeight * camera.aspect : 8f;
        Vector3 offset = new Vector3(Mathf.Min(4f, halfWidth * 0.5f), Mathf.Min(2.5f, halfHeight * 0.4f), 0f);
        Vector3 spawnPos = player != null ? player.transform.position + offset :
            bossSpawnPoint != null ? bossSpawnPoint.position : Vector3.zero;
        // bosses appear in the middle of the room map
        if (RoomEnvironment.Instance != null) spawnPos = RoomEnvironment.Instance.Center;

        GameObject prefab = ResolveBossPrefabForRoom(currentRoom, bossIndex);

        GameObject boss;
        if (prefab != null)
        {
            boss = Instantiate(prefab, spawnPos, Quaternion.identity);
            boss.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"[RoomManager] Boss prefab {bossIndex} missing; creating a playable backup boss.");
            boss = new GameObject("Backup_Boss");
            boss.transform.position = spawnPos;
            boss.transform.localScale = Vector3.one * 2.5f;
            boss.tag = "Enemy";
            boss.AddComponent<SpriteRenderer>();
            Rigidbody2D body = boss.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            boss.AddComponent<BoxCollider2D>();
            EnemyAI backupAI = boss.AddComponent<EnemyAI>();
            backupAI.ConfigureBackupBoss(BossData.GetBalancedMaxHPByRoom(currentRoom), 10f, bossIndex % 2 == 1);
        }

        // The new bosses own health, AI, and the RoomManager defeat event.
        // Attaching legacy EnemyAI here would give them a second health bar/AI.
        BossBase newBoss = boss.GetComponent<BossBase>();
        if (newBoss != null)
        {
            float balancedMaxHP = BossData.GetBalancedMaxHPByRoom(currentRoom);
            float dmgScale = 1f + (currentRoom - 1) * 0.04f;
            newBoss.Init(player != null ? player.transform : null, 1f, dmgScale, 1f);
            if (newBoss.Health != null)
            {
                newBoss.Health.SetExactMaxHealth(balancedMaxHP);
                newBoss.Health.SetExpDrop(BossBase.GetBalancedExpByRoom(currentRoom));
                // the bar was shown with the default HP in Init; show it again with the balanced HP
                if (global::UI.BossHealthBarUI.Instance != null)
                {
                    string title = newBoss.BossConfig != null && !string.IsNullOrEmpty(newBoss.BossConfig.BossTitle) ? newBoss.BossConfig.BossTitle : boss.name;
                    global::UI.BossHealthBarUI.Instance.ShowBossBar(title, balancedMaxHP);
                    global::UI.BossHealthBarUI.Instance.UpdateBossHealth(balancedMaxHP);
                }
            }
        }
        else
        {
            EnemyAI bossAI = boss.GetComponent<EnemyAI>();
            if (bossAI == null)
            {
                bossAI = boss.AddComponent<EnemyAI>();
                bossAI.ConfigureBackupBoss(BossData.GetBalancedMaxHPByRoom(currentRoom), 10f, false);
            }
            bossAI.MarkAsBoss();
        }
        SpriteRenderer visual = boss.GetComponentInChildren<SpriteRenderer>();
        if (visual == null) visual = boss.AddComponent<SpriteRenderer>();
        if (visual.sprite == null) visual.sprite = ProceduralSprites.Boss;
        visual.sortingOrder = 10;
        Debug.Log($"[RoomManager] Spawned boss {boss.name} at {spawnPos} for room {currentRoom} (Base HP: {BossData.GetBalancedMaxHPByRoom(currentRoom):N0}).");
    }

    /// <summary>
    /// ค้นหา Prefab บอสตามลำดับห้องของเอกสารบาลานซ์ 8.3:
    /// ห้อง 3: Brute, ห้อง 6: Architect, ห้อง 9: Doctor, ห้อง 12: Digger, ห้อง 15: Stormcaller, ห้อง 18: Overlord
    /// </summary>
    private GameObject ResolveBossPrefabForRoom(int roomNumber, int bossIndex)
    {
        Type expectedBossType = roomNumber switch
        {
            3 => typeof(BruteBoss),
            6 => typeof(ArchitectBoss),
            9 => typeof(DoctorBoss),
            12 => typeof(DiggerBoss),
            15 => typeof(StormcallerBoss),
            18 => typeof(OverlordBoss),
            _ => null
        };

        if (expectedBossType != null && bossPrefabs != null)
        {
            for (int i = 0; i < bossPrefabs.Length; i++)
            {
                GameObject candidate = bossPrefabs[i];
                if (candidate != null && candidate.GetComponent(expectedBossType) != null)
                {
                    return candidate;
                }
            }
        }

#if UNITY_EDITOR
        string editorPrefabPath = roomNumber switch
        {
            3 => "Assets/Prefabs/Enemy_Boss_Brute.prefab",
            6 => "Assets/Prefabs/Enemy_Boss_Architect.prefab",
            9 => "Assets/Prefabs/Enemy_Boss_Doctor.prefab",
            12 => "Assets/Prefabs/Enemy_Boss_Digger.prefab",
            15 => "Assets/Prefabs/Enemy_Boss_Stormcaller.prefab",
            18 => "Assets/Prefabs/Enemy_Boss_Overlord.prefab",
            _ => null
        };
        if (!string.IsNullOrEmpty(editorPrefabPath))
        {
            GameObject loaded = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPrefabPath);
            if (loaded != null) return loaded;
        }
#endif

        if (bossPrefabs != null && bossIndex >= 0 && bossIndex < bossPrefabs.Length && bossPrefabs[bossIndex] != null)
        {
            return bossPrefabs[bossIndex];
        }

        if (bossPrefabs != null)
        {
            foreach (GameObject candidate in bossPrefabs)
            {
                if (candidate != null) return candidate;
            }
        }

        return null;
    }

    private void Update()
    {
        if (isRoomActive)
        {
            if (!isBossFight && !IsBossRoom)
            {
                roomTimer -= Time.deltaTime;
                UpdateTimerUI(roomTimer);

                if (roomTimer <= 0f)
                {
                    OnRoomCleared();
                }
            }
            else
            {
                if (timerText != null)
                {
                    timerText.text = IsFinalBossRoom ? "DEFEAT FINAL BOSS" : "DEFEAT BOSS";
                }
            }
            return;
        }

        if (isWaitingForDoor)
        {
            UpdateContainmentPurge(Time.deltaTime);
        }
    }

    private void UpdateContainmentPurge(float deltaTime)
    {
        if (!isPurgeActive)
        {
            purgeCountdownTimer -= deltaTime;
            if (timerText != null)
            {
                int remainingSec = Mathf.CeilToInt(Mathf.Max(0f, purgeCountdownTimer));
                timerText.text = $"PICK A DOOR - PURGE IN {remainingSec:00}s";
            }

            if (purgeCountdownTimer <= 0f)
            {
                isPurgeActive = true;
                purgeExposureTimer = purgeGasExposureDuration;
                Debug.LogWarning("[RoomManager] Containment Purge triggered! Poison gas is draining 5% Max HP/s.");
            }
        }
        else
        {
            if (timerText != null)
            {
                timerText.text = "CONTAINMENT PURGE! (-5% HP/s)";
            }

            // แก๊สพิษรั่ว ลดเลือดผู้เล่นวินาทีละ 5% ของ Max HP
            PlayerHealth playerHealth = FindPlayerHealth();
            if (playerHealth != null)
            {
                float drainAmount = playerHealth.MaxHealth * purgeHpDrainPerSecond * deltaTime;
                playerHealth.TakePurgeDamage(drainAmount);

                if (playerHealth.CurrentHealth <= 0f)
                {
                    isWaitingForDoor = false;
                    isPurgeActive = false;
                    return;
                }
            }

            // gas keeps draining until the player enters a door or dies (no auto-teleport)
        }
    }

    private PlayerHealth FindPlayerHealth()
    {
        GameObject player = GameObject.FindWithTag("Player");
        PlayerHealth ph = player != null ? player.GetComponentInParent<PlayerHealth>() : null;
        if (ph == null && player != null) ph = player.GetComponentInChildren<PlayerHealth>();
        if (ph == null) ph = FindFirstObjectByType<PlayerHealth>();
        return ph;
    }

    private void ForceSelectRandomDoor()
    {
        if (!isWaitingForDoor) return;

        // สุ่มเลือกประตูจากที่เปิดอยู่และพาผู้เล่นเข้าอัตโนมัติ
        if (activeDoors.Count > 0)
        {
            int randomIdx = UnityEngine.Random.Range(0, activeDoors.Count);
            GameObject chosenDoorObj = activeDoors[randomIdx];
            if (chosenDoorObj != null)
            {
                RoomDoor roomDoor = chosenDoorObj.GetComponent<RoomDoor>();
                if (roomDoor != null)
                {
                    Debug.Log($"[RoomManager] Containment Purge auto-selected door: {roomDoor.DoorTitle}");
                    roomDoor.EnterDoor();
                    return;
                }
            }
        }

        if (currentOfferedDoorTypes.Count > 0)
        {
            RoomRewardType fallbackType = currentOfferedDoorTypes[UnityEngine.Random.Range(0, currentOfferedDoorTypes.Count)];
            OnDoorSelected(fallbackType);
        }
        else
        {
            ProceedToNextRoom();
        }
    }

    public void OnBossDefeated()
    {
        if (isBossFight)
        {
            Debug.Log($"[RoomManager] Boss in Floor {CurrentFloor} Room {CurrentRoomInFloor} defeated!");
            if (bossAlertUI != null) bossAlertUI.SetActive(false);

            // บันทึกสถิติการปราบบอสสำหรับคำนวณ RP ตอนจบรัน (+75 RP/ตัว, ห้อง 9 +400 RP, ห้อง 18 +1,000 RP)
            if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.RecordBossDefeated(currentRoom);
            }

            // มอบไอเทม Evolution Core อัตโนมัติเมื่อปราบบอสห้อง 3, 6, 9 (และรางวัลบอสห้อง 12, 15 ตามตารางบาลานซ์)
            PlayerInventory inventory = PlayerInventory.GetOrCreate();
            if (inventory != null)
            {
                if (currentRoom == 3 || currentRoom == 6 || currentRoom == 9)
                {
                    inventory.AddEvolutionCore(1);
                }
                else if (currentRoom == 12 || currentRoom == 15)
                {
                    inventory.TryEnhanceBoonOrResonate();
                }
            }

            OnRoomCleared();
        }
    }

    private void OnRoomCleared()
    {
        isRoomActive = false;

        // 1. หยุดการเกิดศัตรูทันที และ Despawn ศัตรูธรรมดาทั้งหมดที่เหลืออยู่บนจอ
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.ClearAllEnemies();
        }
        if (Enemies.RoomWaveSpawner.Instance != null)
        {
            Enemies.RoomWaveSpawner.Instance.StopSpawning();
            Enemies.RoomWaveSpawner.Instance.ClearAllEnemies();
        }
        PurgeLeftoverEnemies();

        // 2. collect every leftover EXP crystal instantly (no EXP is lost when the room ends)
        EXPCrystal.CollectAllNow();

        // 3. มอบรางวัลเครดิตเมื่อเคลียร์ห้อง (ห้องปกติ 10, ห้องบอส 50) และบันทึกสถิติห้องปกติ (+25 RP/ห้อง)
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.AddRunCredits(isBossFight ? 50 : 10);
            if (!isBossFight)
            {
                GameOverManager.Instance.RecordNormalRoomCleared(currentRoom);
            }
        }

        OnRoomClearedEvent?.Invoke();

        if (currentRoom >= MaxRooms)
        {
            Debug.Log("[RoomManager] Final Boss Defeated! Victory!");
            if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.TriggerVictory();
            }
            return;
        }

        // 4. เปิดประตูรางวัล 2 บาน และเริ่มนับถอยหลัง Containment Purge 15 วินาที
        isWaitingForDoor = true;
        isPurgeActive = false;
        purgeCountdownTimer = purgeCountdownDuration;

        SpawnNextRoomDoors();
    }

    /// <summary>Removes every enemy, boss minion and enemy shot still alive when a room ends.</summary>
    private void PurgeLeftoverEnemies()
    {
        Enemies.Core.EnemyBase[] left = FindObjectsByType<Enemies.Core.EnemyBase>(FindObjectsSortMode.None);
        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] == null || !left[i].gameObject.activeInHierarchy) continue;
            left[i].Despawn();
        }
        EnemyProjectile[] shots = FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < shots.Length; i++)
        {
            if (shots[i] != null) Destroy(shots[i].gameObject);
        }
    }

    private bool boonOfferedLastRoom;

    private void SpawnNextRoomDoors()
    {
        currentOfferedDoorTypes.Clear();
        RollTwoRewardDoors(out RoomRewardType firstDoor, out RoomRewardType secondDoor);

        currentOfferedDoorTypes.Add(firstDoor);
        currentOfferedDoorTypes.Add(secondDoor);

        SpawnSingleDoor(firstDoor, RoomDoor.GetDefaultTitle(firstDoor), 0);
        SpawnSingleDoor(secondDoor, RoomDoor.GetDefaultTitle(secondDoor), 1);
    }

    /// <summary>
    /// สุ่มประตูรางวัล 2 บานที่ไม่ซ้ำชนิดกัน ตามค่าน้ำหนักและเงื่อนไขการันตี/ข้อจำกัดในเอกสารบาลานซ์หัวข้อ 6
    /// </summary>
    private void RollTwoRewardDoors(out RoomRewardType doorA, out RoomRewardType doorB)
    {
        List<(RoomRewardType type, float weight)> pool = BuildDoorWeightPool();

        // boon doors only appear every other room: if the last room offered one, it is skipped this room
        if (boonOfferedLastRoom) pool.RemoveAll(item => item.type == RoomRewardType.BioGeneChamber);

        // กฎข้อ 1: การันตีต้องมีประตู BioGeneChamber อย่างน้อย 1 ครั้งในห้อง 1-3
        bool forceBoonDoor = (currentRoom == 3 && !hasBoonAppearedInRooms1To3 && !boonOfferedLastRoom);
        if (forceBoonDoor)
        {
            doorA = RoomRewardType.BioGeneChamber;
            pool.RemoveAll(item => item.type == RoomRewardType.BioGeneChamber);
        }
        else
        {
            RoomRewardType pickedFirst = PickWeightedDoor(pool);
            doorA = pickedFirst;
            pool.RemoveAll(item => item.type == pickedFirst);
        }

        // กฎข้อ 5: สุ่มประตูที่ 2 โดยห้ามซ้ำชนิดกับประตูแรก
        doorB = PickWeightedDoor(pool);

        boonOfferedLastRoom = (doorA == RoomRewardType.BioGeneChamber || doorB == RoomRewardType.BioGeneChamber);

        // อัปเดตสถานะการออกของประตูสำหรับห้องถัดไป
        if (doorA == RoomRewardType.BioGeneChamber || doorB == RoomRewardType.BioGeneChamber)
        {
            if (currentRoom <= 3)
            {
                hasBoonAppearedInRooms1To3 = true;
            }
        }

        if (doorA == RoomRewardType.MedicalPod || doorB == RoomRewardType.MedicalPod)
        {
            medicalPodAppearedLastRoom = true;
            medicalPodCountThisFloor++;
        }
        else
        {
            medicalPodAppearedLastRoom = false;
        }

        if (doorA == RoomRewardType.GeneEnhancer || doorB == RoomRewardType.GeneEnhancer)
        {
            geneEnhancerCountThisRun++;
        }
    }

    private List<(RoomRewardType type, float weight)> BuildDoorWeightPool()
    {
        PlayerInventory inventory = PlayerInventory.Instance;
        bool allBoonsMaxed = inventory != null && inventory.AreAllBoonsMaxed();

        // กฎข้อ 4: ถ้า slot Boon เต็มและทุกตัว Lv.2 แล้ว น้ำหนักประตู Boon เป็น 0 และให้ Credit Vault รับน้ำหนักแทน
        float boonWeight = allBoonsMaxed ? 0f : 28f;
        float creditVaultWeight = allBoonsMaxed ? (22f + 28f) : 22f;

        List<(RoomRewardType type, float weight)> pool = new List<(RoomRewardType type, float weight)>();
        if (boonWeight > 0f)
        {
            pool.Add((RoomRewardType.BioGeneChamber, boonWeight));
        }
        pool.Add((RoomRewardType.BlackMarket, 27f));
        pool.Add((RoomRewardType.CreditVault, creditVaultWeight));

        // กฎข้อ 2: MedicalPod ห้ามเกิดติดกัน 2 ห้อง และไม่เกิน 3 ครั้งต่อชั้น (ถ้า HP < 40% น้ำหนัก x2)
        if (!medicalPodAppearedLastRoom && medicalPodCountThisFloor < 3)
        {
            PlayerHealth health = FindPlayerHealth();
            bool isLowHp = health != null && health.HealthNormalized < 0.40f;
            float medicalWeight = isLowHp ? 36f : 18f;
            pool.Add((RoomRewardType.MedicalPod, medicalWeight));
        }

        // กฎข้อ 3: GeneEnhancer ไม่ออกก่อนห้อง 5 และออกได้ไม่เกิน 2 ครั้งต่อรอบ (และต้องมี Boon ที่อัปเป็น Lv.2 หรือ Resonate ได้)
        bool canEnhanceBoon = inventory == null || inventory.CanGeneEnhancerApply();
        if (currentRoom >= 5 && geneEnhancerCountThisRun < 2 && canEnhanceBoon)
        {
            pool.Add((RoomRewardType.GeneEnhancer, 5f));
        }

        return pool;
    }

    private RoomRewardType PickWeightedDoor(List<(RoomRewardType type, float weight)> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            return RoomRewardType.CreditVault;
        }

        float totalWeight = 0f;
        for (int i = 0; i < pool.Count; i++)
        {
            totalWeight += pool[i].weight;
        }

        if (totalWeight <= 0f)
        {
            return pool[0].type;
        }

        float roll = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        for (int i = 0; i < pool.Count; i++)
        {
            cumulative += pool[i].weight;
            if (roll <= cumulative)
            {
                return pool[i].type;
            }
        }

        return pool[pool.Count - 1].type;
    }

    /// <summary>
    /// Doors always appear in the central zone of the arena grid, on the top or bottom side: far from
    /// the player but never in the player's own row.
    /// </summary>
    private Vector3 ComputeDoorPosition(int pointIndex)
    {
        // reward doors stand in front of the doorways painted into the top wall of the room map
        if (RoomEnvironment.Instance != null) return RoomEnvironment.Instance.GetDoorSlot(pointIndex);

        GameObject player = GameObject.FindWithTag("Player");
        Vector3 ppos = player != null ? player.transform.position : Vector3.zero;

        Vector3 center = Vector3.zero;
        float halfH = 8f;
        GameObject mapRoot = GameObject.Find("MapBoundaries");
        if (mapRoot != null)
        {
            bool have = false; Bounds b = new Bounds();
            foreach (BoxCollider2D wall in mapRoot.GetComponentsInChildren<BoxCollider2D>())
            {
                if (!have) { b = wall.bounds; have = true; } else b.Encapsulate(wall.bounds);
            }
            if (have) { center = b.center; halfH = b.extents.y; }
        }

        // top or bottom band of the central column, whichever is farther from the player's row
        float offsetY = Mathf.Clamp(halfH * 0.45f, 4f, 9f);
        float topY = center.y + offsetY, botY = center.y - offsetY;
        float y = Mathf.Abs(topY - ppos.y) >= Mathf.Abs(botY - ppos.y) ? topY : botY;
        if (Mathf.Abs(y - ppos.y) < 4f) y = ppos.y >= center.y ? botY : topY;   // never the player's row

        float x = center.x + (pointIndex == 0 ? -9f : 9f);   // far apart so the labels never overlap
        return new Vector3(x, y, 0f);
    }

    private void SpawnSingleDoor(RoomRewardType type, string title, int pointIndex)
    {
        Vector3 doorPosition = ComputeDoorPosition(pointIndex);

        if (roomDoorPrefab != null)
        {
            GameObject doorObj = Instantiate(roomDoorPrefab, doorPosition, Quaternion.identity);
            RoomDoor door = doorObj.GetComponent<RoomDoor>();
            if (door != null)
            {
                door.SetupDoor(type, title);
            }
            // light up the doorway in the wall to show it is ready to enter
            if (RoomEnvironment.Instance != null) RoomEnvironment.Instance.OpenDoorway(pointIndex, RoomDoor.GetRewardColor(type));
            activeDoors.Add(doorObj);
        }
        else
        {
            Debug.LogWarning("[RoomManager] RoomDoor Prefab not assigned! Showing reward directly.");
            TriggerRewardDirectly(type);
        }
    }

    public void OnDoorSelected(RoomRewardType rewardType)
    {
        isWaitingForDoor = false;
        isPurgeActive = false;
        ClearExistingDoors();
        TriggerRewardDirectly(rewardType);
    }

    private void TriggerRewardDirectly(RoomRewardType rewardType)
    {
        switch (rewardType)
        {
            case RoomRewardType.BioGeneChamber:
                if (LevelUpManager.Instance != null)
                {
                    LevelUpManager.Instance.ShowBoonSelectionFromDoor();
                }
                else if (boonUI != null)
                {
                    boonUI.SetActive(true);
                }
                else
                {
                    ProceedToNextRoom();
                }
                break;

            case RoomRewardType.BlackMarket:
                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.OpenShop();
                }
                else if (shopUI != null)
                {
                    shopUI.SetActive(true);
                }
                else
                {
                    ProceedToNextRoom();
                }
                break;

            case RoomRewardType.CreditVault:
                // สูตร Credit Vault ตามเอกสาร: 100 + 10 * (ห้อง - 1)
                int vaultCredits = 100 + 10 * (currentRoom - 1);
                if (GameOverManager.Instance != null)
                {
                    GameOverManager.Instance.AddRunCredits(vaultCredits);
                }
                Debug.Log($"[RoomManager] Credit Vault rewarded +{vaultCredits} Nano-Credits!");
                ProceedToNextRoom();
                break;

            case RoomRewardType.MedicalPod:
                // ฟื้น HP 30% ของ Max HP ฟรี
                PlayerHealth playerHealth = FindPlayerHealth();
                if (playerHealth != null)
                {
                    float healAmount = playerHealth.MaxHealth * 0.30f;
                    float hpBefore = playerHealth.CurrentHealth;
                    playerHealth.Heal(healAmount);
                    float gained = playerHealth.CurrentHealth - hpBefore;
                    SkillMajorEffects.FloatText(gained > 0.5f ? "+" + Mathf.RoundToInt(gained) + " HP" : "HP FULL", (Vector2)playerHealth.transform.position + Vector2.up * 1.5f, new Color(0.4f, 1f, 0.5f, 1f));
                    PoisonSmoke.Burst(playerHealth.transform.position, 1.6f, 10, 0.9f);
                    Debug.Log($"[RoomManager] Medical Pod restored {gained:F1} HP (30% Max HP = {healAmount:F1}), now {playerHealth.CurrentHealth:F0}/{playerHealth.MaxHealth:F0}");
                }
                else
                {
                    Debug.LogWarning("[RoomManager] Medical Pod: no PlayerHealth found, nothing healed!");
                }
                ProceedToNextRoom();
                break;

            case RoomRewardType.GeneEnhancer:
                // อัป Boon เป็น Lv.2 ทันที หรือเกิด Resonance ฟรี
                Debug.Log("[RoomManager] Gene Enhancer (Rare) activated!");
                PlayerInventory inventory = PlayerInventory.GetOrCreate();
                if (inventory != null && inventory.TryEnhanceBoonOrResonate())
                {
                    ProceedToNextRoom();
                }
                else if (LevelUpManager.Instance != null)
                {
                    LevelUpManager.Instance.ShowBoonSelectionFromDoor();
                }
                else
                {
                    ProceedToNextRoom();
                }
                break;

            case RoomRewardType.Boss:
                ProceedToNextRoom();
                break;
        }
    }

    /// <summary>Debug: jump to another room (used by DebugCheats [ and ] keys to look at the maps).</summary>
    public void DebugJumpRoom(int delta)
    {
        currentRoom = Mathf.Clamp(currentRoom + delta, 1, MaxRooms);
        isPurgeActive = false;
        isWaitingForDoor = false;
        if (EnemySpawner.Instance != null) EnemySpawner.Instance.StopSpawning();
        PurgeLeftoverEnemies();
        ClearExistingDoors();
        StartNewRoom();
    }

    public void ProceedToNextRoom()
    {
        currentRoom++;
        if (boonUI != null) boonUI.SetActive(false);
        if (shopUI != null) shopUI.SetActive(false);

        StartNewRoom();
    }

    /// <summary>
    /// สำหรับ DebugCheats [L]: ข้ามเวลาในห้องไปที่วินาทีที่กำหนด (เช่น วินาทีที่ 89 จาก 90 วินาที = เหลือ 1 วินาที)
    /// </summary>
    public void DebugSkipToSecond(float targetElapsedSecond = 89f)
    {
        if (!isRoomActive) return;

        if (isBossFight)
        {
            OnBossDefeated();
            return;
        }

        float remaining = Mathf.Max(0.1f, roomDuration - targetElapsedSecond);
        roomTimer = remaining;
        UpdateTimerUI(roomTimer);

        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.DebugSetElapsedTime(targetElapsedSecond);
        }

        Debug.Log($"[RoomManager] Debug skipped room timer to {targetElapsedSecond:F0}s / {roomDuration:F0}s (Remaining: {roomTimer:F1}s).");
    }

    private void ClearExistingDoors()
    {
        if (RoomEnvironment.Instance != null) RoomEnvironment.Instance.LockDoorways();
        foreach (GameObject door in activeDoors)
        {
            if (door != null) Destroy(door);
        }
        activeDoors.Clear();
    }

    private void UpdateRoomUI()
    {
        if (roomText != null)
        {
            roomText.text = $"Floor {CurrentFloor}  |  Room {CurrentRoomInFloor}/{roomsPerFloor}";
        }
    }

    private void UpdateTimerUI(float timeInSeconds)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(Mathf.Max(0, timeInSeconds) / 60);
            int seconds = Mathf.FloorToInt(Mathf.Max(0, timeInSeconds) % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
}
