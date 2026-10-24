using System.Collections;
using System.Collections.Generic;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// Main in-game enemy spawner managed by RoomManager.
/// Responsible for 3-phase wave progression, stat scaling, off-screen repositioning, and cleanup between rooms.
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    public static EnemySpawner Instance { get; private set; }

    public enum WavePhase
    {
        Light,      // 0:00 - 0:20 (15% ของศัตรูรวมในห้อง)
        Dense,      // 0:20 - 1:10 (55% ของศัตรูรวมในห้อง)
        FinalSwarm  // 1:10 - 1:30 (30% ของศัตรูรวมในห้อง)
    }

    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] enemyPrefabs;
    [SerializeField] private float spawnInterval = 1.33f;
    [SerializeField] private float minSpawnRadius = 12f;
    [SerializeField] private float maxSpawnRadius = 14f;
    [SerializeField] private float repositionDistanceThreshold = 25f;
    [SerializeField] private int maxEnemiesAtOnce = 40;

    private Transform player;
    private bool isSpawning = false;
    private float roomElapsedTime = 0f;
    private readonly List<GameObject> activeEnemies = new List<GameObject>();
    private readonly List<GameObject> pooledEnemies = new List<GameObject>();
    private Bounds arenaBounds;
    private bool hasArenaBounds;

    public WavePhase CurrentWavePhase => GetWavePhase(roomElapsedTime);
    public float RoomElapsedTime => roomElapsedTime;

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
        CacheArenaBounds();
    }

    private void Start()
    {
        FindPlayer();
    }

    private void Update()
    {
        if (!isSpawning) return;

        roomElapsedTime += Time.deltaTime;
        RepositionOutOfRangeEnemies();
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

    private void FindPlayer()
    {
        if (player != null) return;

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
    }

    public void StartSpawning()
    {
        isSpawning = true;
        roomElapsedTime = 0f;
        StopAllCoroutines();
        StartCoroutine(SpawnRoutine());
    }

    public void StopSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    public void DebugSetElapsedTime(float elapsedSeconds)
    {
        roomElapsedTime = Mathf.Max(0f, elapsedSeconds);
    }

    public WavePhase GetWavePhase(float elapsedSeconds)
    {
        if (elapsedSeconds < 20f)
        {
            return WavePhase.Light;      // 0:00 - 0:20 (20 วินาทีแรก)
        }
        if (elapsedSeconds < 70f)
        {
            return WavePhase.Dense;      // 0:20 - 1:10 (50 วินาทีกลาง)
        }
        return WavePhase.FinalSwarm;     // 1:10 - 1:30 (20 วินาทีท้าย)
    }

    /// <summary>
    /// คำนวณจำนวนศัตรูรวมตลอดทั้งห้องตามตารางบาลานซ์ (EXP ของห้อง ÷ 1.1)
    /// </summary>
    public float GetTotalEnemiesForRoom(int roomNumber)
    {
        int clampedRoom = Mathf.Max(1, roomNumber);
        // ห้อง 1 และ 2 มีศัตรูรวม ~100 ตัว, ห้องถัดไปคำนวณจาก EXP ของห้อง (100 + 10 * ห้อง) ÷ 1.1
        float effectiveRoomExp = clampedRoom <= 2 ? 110f : (100f + 10f * clampedRoom);
        return effectiveRoomExp / 1.1f;
    }

    /// <summary>
    /// คำนวณอัตราเกิด (ตัวต่อวินาที) ตามช่วงเวลาของห้อง (หรืออัตราคงที่ในห้องบอส)
    /// </summary>
    public float GetCurrentSpawnRate()
    {
        int currentRoom = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
        bool isBossRoom = RoomManager.Instance != null && RoomManager.Instance.IsBossRoom;

        if (isBossRoom)
        {
            float roomExp = 100f + 10f * currentRoom;
            float bossExp = currentRoom >= 18 ? 100f : (30f + 10f * (currentRoom / 3));
            float totalMinions = Mathf.Max(1f, (roomExp - bossExp) / 1.1f);
            return totalMinions / 95f;
        }

        float totalEnemies = GetTotalEnemiesForRoom(currentRoom);
        WavePhase phase = GetWavePhase(roomElapsedTime);

        return phase switch
        {
            WavePhase.Light => (totalEnemies * 0.15f) / 20f,      // 0:00 - 0:20 (15% ใน 20 วินาที)
            WavePhase.Dense => (totalEnemies * 0.55f) / 50f,      // 0:20 - 1:10 (55% ใน 50 วินาที)
            WavePhase.FinalSwarm => (totalEnemies * 0.30f) / 20f, // 1:10 - 1:30 (30% ใน 20 วินาที)
            _ => 1f
        };
    }

    public int GetCurrentEnemyCap()
    {
        int currentRoom = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;
        bool isBossRoom = RoomManager.Instance != null && RoomManager.Instance.IsBossRoom;

        int maxCap = Mathf.RoundToInt(40f + 5.3f * (currentRoom - 1));
        if (isBossRoom)
        {
            maxCap /= 2;
        }

        maxEnemiesAtOnce = maxCap;
        return maxCap;
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning)
        {
            activeEnemies.RemoveAll(enemy => enemy == null || !enemy.activeInHierarchy);

            float currentRate = Mathf.Max(0.1f, GetCurrentSpawnRate());
            spawnInterval = 1f / currentRate;

            int currentCap = GetCurrentEnemyCap();
            // ตรวจสอบ Enemy Cap ก่อน Spawn หากแตะหรือเกิน Cap ให้ข้ามรอบนั้นทันทีโดยไม่เก็บคิวไว้เติมทีหลัง
            if (activeEnemies.Count < currentCap && enemyPrefabs != null && enemyPrefabs.Length > 0)
            {
                SpawnEnemyAroundPlayer();
            }

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private Vector3 GetRandomSpawnPositionNearPlayer()
    {
        float radius = Random.Range(minSpawnRadius, maxSpawnRadius);
        Vector2 randomDir = Random.insideUnitCircle.normalized;
        if (randomDir == Vector2.zero) randomDir = Vector2.right;

        Vector3 spawnPosition = player.position + new Vector3(randomDir.x * radius, randomDir.y * radius, 0f);
        if (hasArenaBounds)
        {
            spawnPosition.x = Mathf.Clamp(spawnPosition.x, arenaBounds.min.x + 2f, arenaBounds.max.x - 2f);
            spawnPosition.y = Mathf.Clamp(spawnPosition.y, arenaBounds.min.y + 2f, arenaBounds.max.y - 2f);
        }
        return spawnPosition;
    }

    /// <summary>
    /// หากศัตรูหลุดระยะห่างจากผู้เล่นเกิน 25 เมตร ให้ย้ายกลับมาที่ขอบจอรอบตัวผู้เล่น (12-14 เมตร) โดยคงค่า HP เดิมไว้
    /// </summary>
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
            if (dist > repositionDistanceThreshold)
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

    public GameObject SpawnEnemyAroundPlayer(GameObject specificPrefab = null)
    {
        if (player == null)
        {
            FindPlayer();
            if (player == null) return null;
        }

        Vector3 spawnPosition = GetRandomSpawnPositionNearPlayer();

        GameObject prefabToSpawn = specificPrefab;
        if (prefabToSpawn == null && enemyPrefabs != null && enemyPrefabs.Length > 0)
        {
            prefabToSpawn = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        }

        if (prefabToSpawn == null) return null;

        GameObject newEnemy = GetOrCreateFromPool(prefabToSpawn, spawnPosition);
        if (newEnemy == null) return null;

        ApplyStatScaling(newEnemy);

        activeEnemies.Add(newEnemy);
        return newEnemy;
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

    private void ApplyStatScaling(GameObject enemyObj)
    {
        if (enemyObj == null) return;

        int currentRoom = RoomManager.Instance != null ? RoomManager.Instance.CurrentRoom : 1;

        float hpMultiplier = 1f + 0.25f * (currentRoom - 1);
        float damageMultiplier = 1f + 0.08f * (currentRoom - 1);
        float speedMultiplier = 1f + Mathf.Min(0.02f * (currentRoom - 1), 0.20f);

        EnemyBase enemyBase = enemyObj.GetComponent<EnemyBase>();
        if (enemyBase != null)
        {
            enemyBase.Init(player, hpMultiplier, damageMultiplier, speedMultiplier);
            return;
        }

        EnemyAI enemyAI = enemyObj.GetComponent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.SetScaledStats(hpMultiplier, damageMultiplier, speedMultiplier);
        }
    }

    public void ClearAllEnemies()
    {
        StopSpawning();
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
}
