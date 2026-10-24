using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ตัวจัดการระบบอาวุธ (In-Run Weapon Manager)
/// ทำหน้าที่เป็น Facade เชื่อมโยงข้อมูลอาวุธใน PlayerInventory และ EvolutionManager
/// พร้อมยิง Events ประจำระบบอาวุธ
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    public event Action<WeaponSlotEntry> OnWeaponAdded;
    public event Action<WeaponSlotEntry> OnWeaponUpgraded;
    public event Action<WeaponSlotEntry> OnWeaponEvolved;

    public static event Action<WeaponSlotEntry> OnWeaponAddedStatic;
    public static event Action<WeaponSlotEntry> OnWeaponUpgradedStatic;
    public static event Action<WeaponSlotEntry> OnWeaponEvolvedStatic;

    public IReadOnlyList<WeaponSlotEntry> EquippedWeapons => PlayerInventory.Instance != null ? PlayerInventory.Instance.Weapons : null;

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
    }

    private void Start()
    {
        HookInventory();
    }

    private void OnDestroy()
    {
        UnhookInventory();
    }

    public void HookInventory()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnWeaponAdded -= HandleWeaponAdded;
            PlayerInventory.Instance.OnWeaponAdded += HandleWeaponAdded;

            PlayerInventory.Instance.OnWeaponUpgraded -= HandleWeaponUpgraded;
            PlayerInventory.Instance.OnWeaponUpgraded += HandleWeaponUpgraded;

            PlayerInventory.Instance.OnWeaponEvolved -= HandleWeaponEvolved;
            PlayerInventory.Instance.OnWeaponEvolved += HandleWeaponEvolved;
        }
    }

    public void UnhookInventory()
    {
        if (PlayerInventory.Instance != null)
        {
            PlayerInventory.Instance.OnWeaponAdded -= HandleWeaponAdded;
            PlayerInventory.Instance.OnWeaponUpgraded -= HandleWeaponUpgraded;
            PlayerInventory.Instance.OnWeaponEvolved -= HandleWeaponEvolved;
        }
    }

    private void HandleWeaponAdded(WeaponSlotEntry slot)
    {
        OnWeaponAdded?.Invoke(slot);
        OnWeaponAddedStatic?.Invoke(slot);
    }

    private void HandleWeaponUpgraded(WeaponSlotEntry slot)
    {
        OnWeaponUpgraded?.Invoke(slot);
        OnWeaponUpgradedStatic?.Invoke(slot);
    }

    private void HandleWeaponEvolved(WeaponSlotEntry slot)
    {
        OnWeaponEvolved?.Invoke(slot);
        OnWeaponEvolvedStatic?.Invoke(slot);
    }

    public bool EquipOrUpgradeWeapon(WeaponType type)
    {
        return PlayerInventory.Instance != null && PlayerInventory.Instance.AddOrUpgradeWeapon(type);
    }

    #region Evolution Facade & Queries

    /// <summary>
    /// ดึงข้อมูลสูตรการ Evolve ตาม WeaponType จาก EvolutionManager
    /// </summary>
    public static EvolvedWeaponData GetEvolutionRecipe(WeaponType weapon)
    {
        return EvolutionManager.GetRecipe(weapon);
    }

    /// <summary>
    /// ดึงข้อมูลสูตรการ Evolve ตาม EvolvedWeaponType จาก EvolutionManager
    /// </summary>
    public static EvolvedWeaponData GetEvolutionRecipe(EvolvedWeaponType evolvedType)
    {
        return EvolutionManager.GetRecipe(evolvedType);
    }

    /// <summary>
    /// ตรวจสอบว่าอาวุธชนิดนี้ในตัวผู้เล่นสามารถรวมร่างได้หรือไม่
    /// </summary>
    public bool CanEvolve(WeaponType weaponType)
    {
        PlayerInventory inv = PlayerInventory.Instance ?? PlayerInventory.GetOrCreate();
        if (inv == null) return false;

        WeaponSlotEntry slot = inv.GetWeaponSlot(weaponType);
        if (slot == null) return false;

        EvolutionManager evo = EvolutionManager.Instance ?? EvolutionManager.GetOrCreate();
        return evo != null && evo.CanEvolve(slot, inv);
    }

    /// <summary>
    /// สั่งตรวจสอบการ Evolution อัตโนมัติทันที
    /// </summary>
    public void CheckEvolutions()
    {
        EvolutionManager evo = EvolutionManager.Instance ?? EvolutionManager.GetOrCreate();
        if (evo != null)
        {
            evo.CheckAndTriggerEvolutions();
        }
    }

    #endregion
}
