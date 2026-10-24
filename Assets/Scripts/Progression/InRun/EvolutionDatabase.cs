using System;
using UnityEngine;

/// <summary>
/// ScriptableObject เก็บชุดข้อมูลและไอคอน Sprite 2D ของอาวุธ Evolved ทั้ง 5 ชนิดในระบบ
/// ตามบาลานซ์ชีททางการ SUBJECT N: Lab Breakout
/// </summary>
[CreateAssetMenu(fileName = "EvolutionDatabase", menuName = "RogueLite/Evolution/Evolution Database")]
public class EvolutionDatabase : ScriptableObject
{
    private static EvolutionDatabase cachedInstance;
    public static EvolutionDatabase Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<EvolutionDatabase>("EvolutionDatabase");
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<EvolutionDatabase>("Evolution/EvolutionDatabase");
                }
            }
            return cachedInstance;
        }
    }

    [Header("Evolved Weapon Sprites (2D Icons)")]
    public Sprite executionerMatrixIcon;
    public Sprite plagueSporeSpreaderIcon;
    public Sprite supernovaCannonIcon;
    public Sprite stormCitadelIcon;
    public Sprite singularityCannonIcon;

    public Sprite GetIcon(EvolvedWeaponType type) => type switch
    {
        EvolvedWeaponType.ExecutionerMatrix => executionerMatrixIcon,
        EvolvedWeaponType.PlagueSporeSpreader => plagueSporeSpreaderIcon,
        EvolvedWeaponType.SupernovaCannon => supernovaCannonIcon,
        EvolvedWeaponType.StormCitadel => stormCitadelIcon,
        EvolvedWeaponType.SingularityCannon => singularityCannonIcon,
        _ => null
    };

    public Sprite GetIconByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("executioner") || lower.Contains("matrix"))
            return executionerMatrixIcon;
        if (lower.Contains("plague") || lower.Contains("spore") || lower.Contains("spreader"))
            return plagueSporeSpreaderIcon;
        if (lower.Contains("supernova") || lower.Contains("cannon") && lower.Contains("plasma"))
            return supernovaCannonIcon;
        if (lower.Contains("citadel") || lower.Contains("storm"))
            return stormCitadelIcon;
        if (lower.Contains("singularity") || lower.Contains("void"))
            return singularityCannonIcon;
        return null;
    }

    public void SetIcon(EvolvedWeaponType type, Sprite icon)
    {
        switch (type)
        {
            case EvolvedWeaponType.ExecutionerMatrix: executionerMatrixIcon = icon; break;
            case EvolvedWeaponType.PlagueSporeSpreader: plagueSporeSpreaderIcon = icon; break;
            case EvolvedWeaponType.SupernovaCannon: supernovaCannonIcon = icon; break;
            case EvolvedWeaponType.StormCitadel: stormCitadelIcon = icon; break;
            case EvolvedWeaponType.SingularityCannon: singularityCannonIcon = icon; break;
        }
    }
}
