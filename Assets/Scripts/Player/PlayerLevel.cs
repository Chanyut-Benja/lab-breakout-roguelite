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

        if (currentLevel >= maxLevel)
        {
            ConvertExcessExpToCredits(amount);
            OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
            return;
        }

        currentExp += amount;

        while (currentLevel < maxLevel && currentExp >= expToNextLevel)
        {
            currentExp -= expToNextLevel;
            currentLevel++;
            expToNextLevel = CalculateRequiredExp(currentLevel);

            TriggerLevelUp();
        }

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
        return baseExp + (expLinearStep * Mathf.Max(1, level));
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
