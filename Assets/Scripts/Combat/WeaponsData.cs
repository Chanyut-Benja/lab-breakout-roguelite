using UnityEngine;

/// <summary>
/// ScriptableObject เก็บชุดข้อมูลและไอคอน Sprite 2D ของอาวุธทั้ง 5 ชนิดในระบบ
/// </summary>
[CreateAssetMenu(fileName = "WeaponsData", menuName = "RogueLite/Weapons/Weapons Data Database")]
public class WeaponsData : ScriptableObject
{
    private static WeaponsData cachedInstance;
    public static WeaponsData Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<WeaponsData>("WeaponsData");
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<WeaponsData>("Weapons/WeaponsData");
                }
            }
            return cachedInstance;
        }
    }

    [Header("Weapon Sprites (2D Icons)")]
    public Sprite nanoBladeIcon;
    public Sprite bioNeedlerIcon;
    public Sprite plasmaBlasterIcon;
    public Sprite teslaOrbIcon;
    public Sprite gravityGrenadierIcon;

    public Sprite GetIcon(WeaponType type) => type switch
    {
        WeaponType.NanoBlade => nanoBladeIcon,
        WeaponType.BioNeedler => bioNeedlerIcon,
        WeaponType.PlasmaBlaster => plasmaBlasterIcon,
        WeaponType.TeslaOrb => teslaOrbIcon,
        WeaponType.GravityGrenadier => gravityGrenadierIcon,
        _ => null
    };

    public void SetIcon(WeaponType type, Sprite icon)
    {
        switch (type)
        {
            case WeaponType.NanoBlade: nanoBladeIcon = icon; break;
            case WeaponType.BioNeedler: bioNeedlerIcon = icon; break;
            case WeaponType.PlasmaBlaster: plasmaBlasterIcon = icon; break;
            case WeaponType.TeslaOrb: teslaOrbIcon = icon; break;
            case WeaponType.GravityGrenadier: gravityGrenadierIcon = icon; break;
        }
    }
}
