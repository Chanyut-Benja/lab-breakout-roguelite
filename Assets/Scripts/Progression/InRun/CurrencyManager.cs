using System;
using UnityEngine;

/// <summary>
/// ตัวจัดการค่าเงิน Nano-Credits ในการเล่นรอบปัจจุบัน (In-Run Currency Manager)
/// เชื่อมโยงและยิง Event แบบ Realtime เมื่อได้รับหรือใช้จ่ายเงิน
/// </summary>
public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public static event Action<int> OnCreditsChanged;
    public static event Action<int> OnEvolutionCoreCountChanged;

    public event Action<int> OnCreditsChangedInstance;
    public event Action<int> OnEvolutionCoreCountChangedInstance;

    public int CurrentCredits => GameOverManager.Instance != null ? GameOverManager.Instance.RunCredits : 0;
    public int CurrentEvolutionCores => PlayerInventory.Instance != null ? PlayerInventory.Instance.EvolutionCores : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GameOverManager.OnCreditsChanged -= ForwardCreditsChanged;
            GameOverManager.OnCreditsChanged += ForwardCreditsChanged;
            PlayerInventory.OnEvolutionCoreCountChangedStatic -= ForwardCoreChanged;
            PlayerInventory.OnEvolutionCoreCountChangedStatic += ForwardCoreChanged;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        GameOverManager.OnCreditsChanged -= ForwardCreditsChanged;
        PlayerInventory.OnEvolutionCoreCountChangedStatic -= ForwardCoreChanged;
    }

    private void ForwardCreditsChanged(int credits)
    {
        OnCreditsChanged?.Invoke(credits);
        OnCreditsChangedInstance?.Invoke(credits);
    }

    private void ForwardCoreChanged(int cores)
    {
        OnEvolutionCoreCountChanged?.Invoke(cores);
        OnEvolutionCoreCountChangedInstance?.Invoke(cores);
    }

    public void AddCredits(int amount)
    {
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.AddRunCredits(amount);
        }
    }

    public bool TrySpendCredits(int amount)
    {
        return GameOverManager.Instance != null && GameOverManager.Instance.TrySpendRunCredits(amount);
    }
}
