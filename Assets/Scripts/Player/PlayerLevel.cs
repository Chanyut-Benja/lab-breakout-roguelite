using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    public static PlayerLevel Instance { get; private set; }

    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int maxLevel = 30;
    [SerializeField] private int baseExp = 10;
    [SerializeField] private int expLinearStep = 6;
    [SerializeField] private float excessExpToCreditRate = 0.5f;

    [Header("SFX")]
    [SerializeField] private AudioClip levelUpSFX;

    private int currentExp = 0;
    private int expToNextLevel = 16;
    private float excessCreditBuffer = 0f;

    [Header("Per-room EXP budget (balance)")]
    [SerializeField] private float roomBudgetScale = 0.85f;
    [SerializeField] private float overBudgetFactor = 0.15f;
    private static readonly int[] RoomExpBudget =
        { 150, 200, 300, 450, 600, 800, 1000, 1200, 1400, 1600, 1500, 1600, 1650, 1650, 1700, 1700, 400, 400 };
    // Hard cap: the highest level you can be in each room (cheat [U] bypasses it). Lv.30 only opens at room 17.
    private static readonly int[] LevelCapByRoom =
        { 5, 7, 9, 11, 13, 15, 17, 19, 21, 23, 25, 26, 27, 28, 29, 29, 30, 30 };
    private int CurrentLevelCap()
    {
        RoomManager rm = RoomManager.Instance;
        if (bypassBudget || rm == null) return maxLevel;
        return LevelCapByRoom[Mathf.Clamp(rm.CurrentRoom, 1, LevelCapByRoom.Length) - 1];
    }
    private bool bypassBudget;
    /// <summary>Adds EXP without the per-room budget (debug cheats, rewards).</summary>
    public void AddExperienceRaw(int amount) { bypassBudget = true; AddExperience(amount); bypassBudget = false; }
    private int budgetRoom = -1;
    private float budgetGained;
    private float expCarry;

    // Once a room has paid out its EXP budget, the rest of that room's EXP is cut,
    // so a denser swarm or bigger drops cannot make the player level up faster.
    private int ApplyBudget(int amount)
    {
        RoomManager rm = RoomManager.Instance;
        if (rm == null) return amount;
        int room = Mathf.Clamp(rm.CurrentRoom, 1, RoomExpBudget.Length);
        if (room != budgetRoom) { budgetRoom = room; budgetGained = 0f; }
        float budget = RoomExpBudget[room - 1] * roomBudgetScale;
        float factor = budgetGained >= budget ? overBudgetFactor : 1f;

        // EXP per kill is sized so the room's whole budget is paid out over the kills we expect in it:
        // a denser swarm gives less EXP per enemy, so levelling speed does not depend on spawn count.
        float perKillScale = 1f;
        if (Enemies.RoomWaveSpawner.Instance != null)
        {
            float expected = Enemies.RoomWaveSpawner.Instance.ExpectedKills(room);
            perKillScale = Mathf.Clamp((budget / expected) / 1.5f, 0.05f, 60f);   // 1.5 = average raw EXP of one kill
        }
        float scaled = amount * perKillScale;
        budgetGained += scaled;
        expCarry += scaled * factor;
        int give = Mathf.FloorToInt(expCarry);
        expCarry -= give;
        return give;
    }

    // Events
    public static event Action OnLevelUpTriggered;
    public event Action<int, int, int> OnExpChanged; // (currentExp, expToNextLevel, currentLevel)
    public event Action<int> OnLevelUp;              // (newLevel)

    public int CurrentLevel => currentLevel;
    public int MaxLevel => maxLevel;
    public int CurrentExp => currentExp;
    public int ExpToNextLevel => expToNextLevel;
    public float ExpNormalized => currentLevel >= maxLevel ? 1f : (expToNextLevel > 0 ? (float)currentExp / expToNextLevel : 0f);

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

    private void Start()
    {
        expToNextLevel = CalculateRequiredExp(currentLevel);
        OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0) return;
        if (!bypassBudget) amount = ApplyBudget(amount);
        if (amount <= 0) return;

        if (currentLevel >= maxLevel)
        {
            ConvertExcessExpToCredits(amount);
            OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
            return;
        }

        currentExp += amount;

        int cap = CurrentLevelCap();
        while (currentLevel < maxLevel && currentLevel < cap && currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            expToNextLevel = CalculateRequiredExp(currentLevel);

            TriggerLevelUp();
        }

        if (currentLevel < maxLevel && currentLevel >= cap && currentExp >= expToNextLevel)
            currentExp = expToNextLevel - 1;   // at this room's level cap: bar stays just below full until a later room opens the next level

        if (currentLevel >= maxLevel && currentExp > 0)
        {
            int overflowExp = currentExp;
            currentExp = 0;
            ConvertExcessExpToCredits(overflowExp);
        }

        OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
    }

    private void ConvertExcessExpToCredits(int excessExp)
    {
        if (excessExp <= 0) return;

        excessCreditBuffer += excessExp * excessExpToCreditRate;
        int creditsToAward = Mathf.FloorToInt(excessCreditBuffer);
        if (creditsToAward > 0)
        {
            excessCreditBuffer -= creditsToAward;
            if (GameOverManager.Instance != null)
            {
                GameOverManager.Instance.AddRunCredits(creditsToAward);
            }
        }
    }

    private int CalculateRequiredExp(int level)
    {
        // สูตรเชิงเส้น: EXP จากเลเวล L ไป L+1 = 10 + (6 * L)
        int L = Mathf.Max(1, level);
        return baseExp + (expLinearStep * L) + Mathf.RoundToInt(1.4f * L * L);   // curve steepens so weapons don't cap by room 7
    }

    private void TriggerLevelUp()
    {
        Debug.Log($"[PlayerLevel] LEVEL UP! Reached Level {currentLevel}");

        AudioClip clip = levelUpSFX != null ? levelUpSFX : Resources.Load<AudioClip>("SFX/LevelUp");
        if (AudioManager.Instance != null && clip != null)
        {
            AudioManager.Instance.PlaySFX(clip);
        }

        OnLevelUp?.Invoke(currentLevel);
        OnLevelUpTriggered?.Invoke();

        if (LevelUpManager.Instance != null)
        {
            LevelUpManager.Instance.QueueLevelUp(currentLevel);
        }
    }
}
