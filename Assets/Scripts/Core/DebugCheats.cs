using Enemies.Bosses;
using Enemies.Core;
using UnityEngine;

/// <summary>
/// สคริปต์ปุ่มลัดสำหรับทดสอบระบบเกม (ทำงานเฉพาะใน Unity Editor / Debug Mode เท่านั้น):
/// - กด [U]: ให้ EXP พอเลเวลอัป 1 เลเวลทันที
/// - กด [I]: ปรับอาวุธทุกชิ้นในตัวเป็น Lv.5 และ Tech ทุกชิ้นเป็น Lv.1 ทันที
/// - กด [O]: เสก Evolution Core เข้าตัวผู้เล่น 1 ชิ้น
/// - กด [P]: เสก Evolution Core + อาวุธ Lv.5 + Tech ตรงสายครบสูตรเพื่อ Trigger Evolution ทันที
/// - กด [K]: ฆ่าศัตรูทั้งหมดบนจอทันที (ล้างเวฟ)
/// - กด [L]: ข้ามเวลาในห้องไปที่วินาทีที่ 89 ทันทีเพื่อทดสอบตัดจบห้อง
/// </summary>
public class DebugCheats : MonoBehaviour
{
    public static DebugCheats Instance { get; private set; }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoAttachDebugCheats()
    {
        if (!Debug.isDebugBuild && !Application.isEditor) return;
        if (Instance != null) return;

        // แนบเข้ากับ Player หรือ RoomManager อัตโนมัติ
        GameObject host = null;
        if (PlayerStats.Instance != null)
        {
            host = PlayerStats.Instance.gameObject;
        }
        else if (RoomManager.Instance != null)
        {
            host = RoomManager.Instance.gameObject;
        }
        else
        {
            host = GameObject.FindWithTag("Player");
        }

        if (host != null)
        {
            if (host.GetComponent<DebugCheats>() == null)
            {
                host.AddComponent<DebugCheats>();
            }
        }
        else
        {
            GameObject fallbackObj = new GameObject("DebugCheats_AutoHost");
            fallbackObj.AddComponent<DebugCheats>();
        }
    }
#endif

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
        }
    }

    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!Application.isEditor && !Debug.isDebugBuild) return;

        // [U]: ให้ EXP พอเลเวลอัป 1 เลเวลทันที
        if (Input.GetKeyDown(KeyCode.U))
        {
            CheatGrantOneLevelUp();
        }

        // [I]: ปรับอาวุธทุกชิ้นในตัวเป็น Lv.5 และ Tech ทุกชิ้นเป็น Lv.1 ทันที
        if (Input.GetKeyDown(KeyCode.I))
        {
            CheatMaxWeaponsAndUnlockTechs();
        }

        // [O]: เสก Evolution Core เข้าตัวผู้เล่น 1 ชิ้น
        if (Input.GetKeyDown(KeyCode.O))
        {
            CheatGrantEvolutionCore();
        }

        // [P]: เสก Evolution Core + อาวุธ Lv.5 + Tech ตรงสายครบสูตรเพื่อ Trigger Evolution ทันที
        if (Input.GetKeyDown(KeyCode.P))
        {
            CheatTriggerInstantEvolution();
        }

        // [K]: ฆ่าศัตรูทั้งหมดบนจอทันที (ล้างเวฟ)
        if (Input.GetKeyDown(KeyCode.K))
        {
            CheatKillAllEnemiesOnScreen();
        }

        // [L]: ข้ามเวลาในห้องไปที่วินาทีที่ 89 ทันทีเพื่อทดสอบตัดจบห้อง
        if (Input.GetKeyDown(KeyCode.L))
        {
            CheatSkipRoomTimeTo89Seconds();
        }
#endif
    }

    /// <summary>
    /// กด [U]: ให้ EXP พอเลเวลอัป 1 เลเวลทันที
    /// </summary>
    public void CheatGrantOneLevelUp()
    {
        PlayerLevel playerLevel = PlayerLevel.Instance;
        if (playerLevel == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) playerLevel = playerObj.GetComponent<PlayerLevel>();
        }

        if (playerLevel == null)
        {
            Debug.LogWarning("[DebugCheats] [U] PlayerLevel component not found.");
            return;
        }

        int expNeeded = Mathf.Max(1, playerLevel.ExpToNextLevel - playerLevel.CurrentExp);
        Debug.Log($"[DebugCheats] [U] Granting +{expNeeded} EXP for immediate Level Up (Current Level: {playerLevel.CurrentLevel}).");
        playerLevel.AddExperience(expNeeded);
    }

    /// <summary>
    /// กด [I]: ปรับอาวุธทุกชิ้นในตัวเป็น Lv.5 และ Tech ทุกชิ้นเป็น Lv.1 ทันที
    /// </summary>
    public void CheatMaxWeaponsAndUnlockTechs()
    {
        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory == null)
        {
            Debug.LogWarning("[DebugCheats] [I] PlayerInventory not found.");
            return;
        }

        // หากผู้เล่นยังไม่มี Tech ติดตัวเลย ให้เพิ่ม Tech ที่ตรงกับอาวุธในตัว (สูงสุด 3 ช่อง) เป็น Lv.1 ทันที
        var weapons = inventory.Weapons;
        for (int i = 0; i < weapons.Count; i++)
        {
            TechPassiveType matchingTech = PlayerInventory.GetMatchingTech(weapons[i].weaponType);
            if (inventory.GetTechSlot(matchingTech) == null && !inventory.IsTechSlotsFull)
            {
                inventory.AddOrUpgradeTech(matchingTech);
            }
        }

        inventory.DebugSetAllHeldWeaponsLv5AndTechsLv1();
        Debug.Log("[DebugCheats] [I] All equipped Weapons set to Lv.5 and all equipped Tech Passives set to Lv.1+!");
    }

    /// <summary>
    /// กด [O]: เสก Evolution Core เข้าตัวผู้เล่น 1 ชิ้น
    /// </summary>
    public void CheatGrantEvolutionCore()
    {
        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory == null)
        {
            Debug.LogWarning("[DebugCheats] [O] PlayerInventory not found.");
            return;
        }

        inventory.AddEvolutionCore(1);
        Debug.Log($"[DebugCheats] [O] Spawned +1 Evolution Core! (Held Cores: {inventory.EvolutionCores})");
    }

    /// <summary>
    /// กด [P]: เสก Evolution Core + อาวุธ Lv.5 + Tech ตรงสายครบสูตรเพื่อ Trigger Evolution ทันที
    /// </summary>
    public void CheatTriggerInstantEvolution()
    {
        PlayerInventory inventory = PlayerInventory.GetOrCreate();
        if (inventory == null)
        {
            Debug.LogWarning("[DebugCheats] [P] PlayerInventory not found.");
            return;
        }

        inventory.DebugTriggerInstantEvolution();
        Debug.Log("[DebugCheats] [P] Triggered Instant Weapon Evolution (Weapon Lv.5 + Matching Tech Lv.1 + Evolution Core)!");
    }

    /// <summary>
    /// กด [K]: ฆ่าศัตรูทั้งหมดบนจอทันที (ล้างเวฟ)
    /// </summary>
    public void CheatKillAllEnemiesOnScreen()
    {
        int killedCount = 0;

        // 1. ฆ่าศัตรูระบบใหม่ (EnemyHealth / BossBase)
        EnemyHealth[] allEnemyHealths = FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None);
        for (int i = 0; i < allEnemyHealths.Length; i++)
        {
            EnemyHealth eh = allEnemyHealths[i];
            if (eh != null && eh.gameObject.activeInHierarchy && !eh.IsDead)
            {
                eh.IsInvulnerable = false;
                eh.TakeDamage(999999f);
                killedCount++;
            }
        }

        // 2. ฆ่าศัตรูระบบเดิม (EnemyAI)
        EnemyAI[] allLegacyEnemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < allLegacyEnemies.Length; i++)
        {
            EnemyAI ai = allLegacyEnemies[i];
            if (ai != null && ai.gameObject.activeInHierarchy)
            {
                ai.TakeDamage(999999f);
                killedCount++;
            }
        }

        // 3. หากยังมีบอสที่ค้างอยู่ ให้เรียก TakeDamage ซ้ำ
        BossBase[] allBosses = FindObjectsByType<BossBase>(FindObjectsSortMode.None);
        for (int i = 0; i < allBosses.Length; i++)
        {
            BossBase boss = allBosses[i];
            if (boss != null && boss.gameObject.activeInHierarchy && boss.Health != null && !boss.Health.IsDead)
            {
                boss.Health.IsInvulnerable = false;
                boss.Health.TakeDamage(999999f);
                killedCount++;
            }
        }

        Debug.Log($"[DebugCheats] [K] Wiped all active enemies on screen! (Total eliminated: {killedCount})");
    }

    /// <summary>
    /// กด [L]: ข้ามเวลาในห้องไปที่วินาทีที่ 89 ทันทีเพื่อทดสอบตัดจบห้อง
    /// </summary>
    public void CheatSkipRoomTimeTo89Seconds()
    {
        if (RoomManager.Instance == null)
        {
            Debug.LogWarning("[DebugCheats] [L] RoomManager.Instance not found.");
            return;
        }

        RoomManager.Instance.DebugSkipToSecond(89f);
        Debug.Log("[DebugCheats] [L] Skipped room time to 01:29 (89s elapsed / 1s remaining)!");
    }
}
