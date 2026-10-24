using System;
using UnityEngine;
using Enemies.Bosses;
using Enemies.Core;

/// <summary>
/// Safe controller for Boss Debugging and Scene Hazard cleanup.
/// Strictly protects Player GameObject and its components from any Despawn / Destroy calls.
/// </summary>
public class BossDebugController : MonoBehaviour
{
    public static BossDebugController Instance { get; private set; }

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
    }

    /// <summary>
    /// Safely clears active bosses and minions without touching the Player.
    /// Only destroys objects tagged 'Enemy', 'Boss', or 'Minion'.
    /// </summary>
    public static void ClearAllEnemiesSafe()
    {
        // 1. Clear Bosses
        BossBase[] bosses = UnityEngine.Object.FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        for (int i = 0; i < bosses.Length; i++)
        {
            if (bosses[i] == null) continue;
            GameObject go = bosses[i].gameObject;
            if (IsPlayerObject(go)) continue;

            SafeDestroy(go);
        }

        // 2. Clear Spawner Minions
        if (EnemySpawner.Instance != null)
        {
            EnemySpawner.Instance.ClearAllEnemies();
        }

        // 3. Clear Tagged Enemies ("Enemy", "Boss", "Minion")
        string[] safeTags = new string[] { "Enemy", "Boss", "Minion" };
        for (int t = 0; t < safeTags.Length; t++)
        {
            try
            {
                GameObject[] taggedObjs = GameObject.FindGameObjectsWithTag(safeTags[t]);
                for (int i = 0; i < taggedObjs.Length; i++)
                {
                    GameObject obj = taggedObjs[i];
                    if (obj == null || IsPlayerObject(obj)) continue;
                    SafeDestroy(obj);
                }
            }
            catch
            {
                // Tag may not be defined in ProjectSettings; safely ignore
            }
        }
    }

    /// <summary>
    /// Checks if a GameObject belongs to the Player.
    /// </summary>
    public static bool IsPlayerObject(GameObject obj)
    {
        if (obj == null) return false;
        if (obj.CompareTag("Player") || obj.name == "Player" || obj.name.StartsWith("Player")) return true;
        if (obj.GetComponent<PlayerController>() != null || obj.GetComponent<PlayerHealth>() != null) return true;
        return false;
    }

    private static void SafeDestroy(GameObject obj)
    {
        if (obj == null || IsPlayerObject(obj)) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
        else UnityEngine.Object.DestroyImmediate(obj);
    }
}
