using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacter", menuName = "RogueLite/Character Data")]
public class CharacterData : ScriptableObject
{
    [Header("Identity")]
    public string characterId = "Subject_02";
    public string characterName = "Subject N-02: Psionic";
    [TextArea(2, 4)] public string description = "Mind Shock: Critical hits Stun enemies for 1.0s. Starts with Bio-Needler (Lv.1).";
    public Sprite characterIcon;
    public RuntimeAnimatorController animatorController;

    [Header("Base Stats")]
    public float baseMaxHealth = 100f;
    public float baseMoveSpeed = 4.0f;
    public float baseDamage = 15f;
    public float baseFireInterval = 0.70f;
    public float basePickupRadius = 4f;

    [Header("Starting Weapon & Passive")]
    public WeaponType startingWeapon = WeaponType.BioNeedler;
    public bool hasMindShockPassive = false;
    public bool hasOverclockDashPassive = false;
    public int maxDashCharges = 1;

    [Header("Unlock Status")]
    public bool isUnlockedByDefault = true;
    public int unlockResearchCost = 0;
}
