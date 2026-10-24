using System;
using UnityEngine;

/// <summary>
/// Component for managing player EXP, levels, and level-up events.
/// Works together with and synchronizes with PlayerLevel.
/// </summary>
public class PlayerEXP : MonoBehaviour
{
    public static PlayerEXP Instance { get; private set; }

    public PlayerLevel LevelManager => PlayerLevel.Instance;

    public int CurrentEXP
    {
        get => LevelManager != null ? LevelManager.CurrentExp : 0;
        set
        {
            if (LevelManager != null)
            {
                int diff = value - LevelManager.CurrentExp;
                if (diff > 0) LevelManager.AddExperience(diff);
            }
        }
    }

    public int RequiredEXP => LevelManager != null ? LevelManager.ExpToNextLevel : 16;
    public int CurrentLevel => LevelManager != null ? LevelManager.CurrentLevel : 1;

    public static event Action OnLevelUp;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }
    }

    private void OnEnable()
    {
        PlayerLevel.OnLevelUpTriggered += HandleLevelUp;
    }

    private void OnDisable()
    {
        PlayerLevel.OnLevelUpTriggered -= HandleLevelUp;
    }

    private void HandleLevelUp()
    {
        OnLevelUp?.Invoke();
    }

    public void AddEXP(int amount)
    {
        if (PlayerLevel.Instance != null)
        {
            PlayerLevel.Instance.AddExperience(amount);
        }
    }
}
