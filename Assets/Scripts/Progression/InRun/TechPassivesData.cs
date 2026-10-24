using UnityEngine;

/// <summary>
/// ScriptableObject เก็บชุดข้อมูลและไอคอน Sprite 2D ของ Tech Passives ทั้ง 5 ชนิดในระบบ
/// </summary>
[CreateAssetMenu(fileName = "TechPassivesData", menuName = "RogueLite/Tech Passives/Tech Passives Database")]
public class TechPassivesData : ScriptableObject
{
    private static TechPassivesData cachedInstance;
    public static TechPassivesData Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<TechPassivesData>("TechPassivesData");
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<TechPassivesData>("TechPassives/TechPassivesData");
                }
            }
            return cachedInstance;
        }
    }

    [Header("Tech Passive Sprites (2D Icons)")]
    public Sprite targetingLensIcon;
    public Sprite geneticExtractorIcon;
    public Sprite overclockChipIcon;
    public Sprite bioFuelTankIcon;
    public Sprite gravitonCapacitorIcon;

    public Sprite GetIcon(TechPassiveType type) => type switch
    {
        TechPassiveType.TargetingLens => targetingLensIcon,
        TechPassiveType.GeneticExtractor => geneticExtractorIcon,
        TechPassiveType.OverclockChip => overclockChipIcon,
        TechPassiveType.BioFuelTank => bioFuelTankIcon,
        TechPassiveType.GravitonCapacitor => gravitonCapacitorIcon,
        _ => null
    };

    public void SetIcon(TechPassiveType type, Sprite icon)
    {
        switch (type)
        {
            case TechPassiveType.TargetingLens: targetingLensIcon = icon; break;
            case TechPassiveType.GeneticExtractor: geneticExtractorIcon = icon; break;
            case TechPassiveType.OverclockChip: overclockChipIcon = icon; break;
            case TechPassiveType.BioFuelTank: bioFuelTankIcon = icon; break;
            case TechPassiveType.GravitonCapacitor: gravitonCapacitorIcon = icon; break;
        }
    }
}
