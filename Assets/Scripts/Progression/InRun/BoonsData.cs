using UnityEngine;

/// <summary>
/// ScriptableObject เก็บชุดข้อมูลและไอคอน Sprite 2D ของ Boon (พรวิจัยชีวภาพ) ทั้ง 5 ชนิดในระบบ
/// </summary>
[CreateAssetMenu(fileName = "BoonsData", menuName = "RogueLite/Boons/Boons Database")]
public class BoonsData : ScriptableObject
{
    private static BoonsData cachedInstance;
    public static BoonsData Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<BoonsData>("BoonsData");
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<BoonsData>("Boons/BoonsData");
                }
            }
            return cachedInstance;
        }
    }

    [Header("Boon Sprites (2D Icons)")]
    public Sprite nanoBarrierIcon;
    public Sprite toxicSporeIcon;
    public Sprite flameRingIcon;
    public Sprite staticAuraIcon;
    public Sprite magneticPulseIcon;

    public Sprite GetIcon(BoonItemType type) => type switch
    {
        BoonItemType.NanoBarrier => nanoBarrierIcon,
        BoonItemType.ToxicSpore => toxicSporeIcon,
        BoonItemType.FlameRing => flameRingIcon,
        BoonItemType.StaticAura => staticAuraIcon,
        BoonItemType.MagneticPulse => magneticPulseIcon,
        _ => null
    };

    public Sprite GetIconByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("barrier") || lower.Contains("nano") || lower.Contains("kinetic") || lower.Contains("overdrive"))
            return nanoBarrierIcon;
        if (lower.Contains("toxic") || lower.Contains("spore") || lower.Contains("catalyst"))
            return toxicSporeIcon;
        if (lower.Contains("flame") || lower.Contains("ring") || lower.Contains("plasma") || lower.Contains("accelerator"))
            return flameRingIcon;
        if (lower.Contains("static") || lower.Contains("aura") || lower.Contains("cryo") || lower.Contains("condenser"))
            return staticAuraIcon;
        if (lower.Contains("magnetic") || lower.Contains("pulse") || lower.Contains("aegis") || lower.Contains("nanite"))
            return magneticPulseIcon;
        return null;
    }

    public void SetIcon(BoonItemType type, Sprite icon)
    {
        switch (type)
        {
            case BoonItemType.NanoBarrier: nanoBarrierIcon = icon; break;
            case BoonItemType.ToxicSpore: toxicSporeIcon = icon; break;
            case BoonItemType.FlameRing: flameRingIcon = icon; break;
            case BoonItemType.StaticAura: staticAuraIcon = icon; break;
            case BoonItemType.MagneticPulse: magneticPulseIcon = icon; break;
        }
    }
}
