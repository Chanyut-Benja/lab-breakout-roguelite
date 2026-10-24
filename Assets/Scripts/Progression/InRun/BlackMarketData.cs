using UnityEngine;

/// <summary>
/// ScriptableObject เก็บชุดข้อมูลและไอคอน Sprite 2D ของสินค้าใน Black Market ทั้ง 5 ชนิด
/// </summary>
[CreateAssetMenu(fileName = "BlackMarketData", menuName = "RogueLite/Shop/Black Market Database")]
public class BlackMarketData : ScriptableObject
{
    private static BlackMarketData cachedInstance;
    public static BlackMarketData Instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = Resources.Load<BlackMarketData>("BlackMarketData");
                if (cachedInstance == null)
                {
                    cachedInstance = Resources.Load<BlackMarketData>("Shop/BlackMarketData");
                }
            }
            return cachedInstance;
        }
    }

    [Header("Black Market Item Sprites (2D Icons)")]
    public Sprite healCapsuleIcon;
    public Sprite rerollCardsIcon;
    public Sprite banishCardIcon;
    public Sprite combatStimIcon;
    public Sprite rerollBoonIcon;

    public Sprite GetIcon(ShopItemType type) => type switch
    {
        ShopItemType.HealPod => healCapsuleIcon,
        ShopItemType.RerollToken => rerollCardsIcon,
        ShopItemType.BanishToken => banishCardIcon,
        ShopItemType.OverdriveStim => combatStimIcon,
        ShopItemType.RerollShop => rerollBoonIcon,
        _ => null
    };

    public Sprite GetIconByName(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("heal") || lower.Contains("capsule") || lower.Contains("pod") || lower.Contains("hp"))
            return healCapsuleIcon;
        if (lower.Contains("reroll cards") || lower.Contains("reroll token") || (lower.Contains("reroll") && !lower.Contains("boon") && !lower.Contains("shop")))
            return rerollCardsIcon;
        if (lower.Contains("banish"))
            return banishCardIcon;
        if (lower.Contains("combat") || lower.Contains("stim") || lower.Contains("overdrive"))
            return combatStimIcon;
        if (lower.Contains("reroll boon") || lower.Contains("reroll shop") || lower.Contains("boon"))
            return rerollBoonIcon;
        return null;
    }

    public void SetIcon(ShopItemType type, Sprite icon)
    {
        switch (type)
        {
            case ShopItemType.HealPod: healCapsuleIcon = icon; break;
            case ShopItemType.RerollToken: rerollCardsIcon = icon; break;
            case ShopItemType.BanishToken: banishCardIcon = icon; break;
            case ShopItemType.OverdriveStim: combatStimIcon = icon; break;
            case ShopItemType.RerollShop: rerollBoonIcon = icon; break;
        }
    }
}
