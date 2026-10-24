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
    public static event Action<int> OnRerollCountChanged;
    public static event Action<int> OnBanishCountChanged;

    public event Action<int> OnCreditsChangedInstance;
    public event Action<int> OnEvolutionCoreCountChangedInstance;
    public event Action<int> OnRerollCountChangedInstance;
    public event Action<int> OnBanishCountChangedInstance;

    public int CurrentCredits => GameOverManager.Instance != null ? GameOverManager.Instance.RunCredits : 0;
    public int CurrentEvolutionCores => PlayerInventory.Instance != null ? PlayerInventory.Instance.EvolutionCores : 0;
    public int RerollCount => PlayerInventory.Instance != null ? PlayerInventory.Instance.RerollCount : 0;
    public int BanishCount => PlayerInventory.Instance != null ? PlayerInventory.Instance.BanishCount : 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            GameOverManager.OnCreditsChanged -= ForwardCreditsChanged;
            GameOverManager.OnCreditsChanged += ForwardCreditsChanged;
            PlayerInventory.OnEvolutionCoreCountChangedStatic -= ForwardCoreChanged;
            PlayerInventory.OnRerollCountChangedStatic -= ForwardRerollChanged;
            PlayerInventory.OnBanishCountChangedStatic -= ForwardBanishChanged;
            PlayerInventory.OnEvolutionCoreCountChangedStatic += ForwardCoreChanged;
            PlayerInventory.OnRerollCountChangedStatic += ForwardRerollChanged;
            PlayerInventory.OnBanishCountChangedStatic += ForwardBanishChanged;
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
        PlayerInventory.OnRerollCountChangedStatic -= ForwardRerollChanged;
        PlayerInventory.OnBanishCountChangedStatic -= ForwardBanishChanged;
    }

    private void ForwardCreditsChanged(int credits)
    {
        OnCreditsChanged?.Invoke(credits);
        OnCreditsChangedInstance?.Invoke(credits);
    }

    private void ForwardRerollChanged(int rerolls)
    {
        OnRerollCountChanged?.Invoke(rerolls);
        OnRerollCountChangedInstance?.Invoke(rerolls);
    }

    private void ForwardBanishChanged(int banish)
    {
        OnBanishCountChanged?.Invoke(banish);
        OnBanishCountChangedInstance?.Invoke(banish);
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

    public void AddReroll(int count = 1)
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            inv.AddReroll(count);
        }
    }

    public bool TryUseReroll()
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        return inv != null && inv.TryUseReroll();
    }

    public void AddBanish(int count = 1)
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv != null)
        {
            inv.AddBanish(count);
        }
    }

    public bool TryUseBanish()
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        return inv != null && inv.TryUseBanish();
    }

    public bool TrySpendCredits(int amount)
    {
        return GameOverManager.Instance != null && GameOverManager.Instance.TrySpendRunCredits(amount);
    }
}
