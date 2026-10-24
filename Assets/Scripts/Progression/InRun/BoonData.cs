using UnityEngine;

public enum BoonType
{
    NanoBarrier = 0,
    ToxicSpore = 1,
    FlameRing = 2,
    StaticAura = 3,
    MagneticPulse = 4,

    // Backward-compatibility aliases
    KineticOverdrive = NanoBarrier,
    ToxicCatalyst = ToxicSpore,
    PlasmaAccelerator = FlameRing,
    CryoCondenser = StaticAura,
    NaniteAegis = MagneticPulse
}

[System.Serializable]
public class BoonData
{
    public string boonTitle;
    [TextArea] public string description;
    public BoonType boonType;
    public BoonItemType boonItemType;
    public bool isSlotBoon = true;
    public bool isResonant = false;
    public ResonantBoonType resonantForm = ResonantBoonType.None;
    public float value;
    public Sprite icon;

    public BoonData() { }

    public BoonData(string title, string desc, BoonType bType, BoonItemType biType, float val = 0f, Sprite iconSprite = null)
    {
        boonTitle = title;
        description = desc;
        boonType = bType;
        boonItemType = biType;
        isSlotBoon = true;
        value = val;
        icon = iconSprite;
    }
}
