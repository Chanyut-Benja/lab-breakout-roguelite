using UnityEngine;

/// <summary>
/// ประเภทสินค้าในร้านค้า Black Market ตามเอกสารบาลานซ์ 6.2 (ยกเลิกการขายสเตตัสถาวรเดิมทั้งหมด)
/// </summary>
public enum ShopItemType
{
    NewBoon = 0,       // New Boon (Lv.1): ราคา 450 Credits (ชั้น 2: 590)
    HealPod = 1,       // Heal Pod / Heal Capsule (ฟื้น 30% Max HP): ราคา 80 Credits (ซื้อซ้ำในห้องเดิม +20 ทุกครั้ง)
    HealCapsule = 1,
    RerollToken = 2,   // Reroll Token / Reroll Cards (+1 สิทธิ์สุ่มการ์ดใหม่): ราคา 120 Credits
    RerollCards = 2,
    BanishToken = 3,   // Banish Token / Banish Card (+1 สิทธิ์ตัดการ์ดทิ้งตลอดรัน): ราคา 150 Credits
    BanishCard = 3,
    OverdriveStim = 4, // Overdrive Stim / Combat Stim (บัฟ Move Speed +15%, Dmg +15% เฉพาะห้องถัดไป 1 ห้อง): ราคา 100 Credits
    CombatStim = 4,
    RerollShop = 5,    // Reroll Shop / Reroll Boon (เปลี่ยนสินค้าในร้าน / เปลี่ยน Boon ที่ร้านเสนอ): ราคา 40 Credits
    RerollBoon = 5
}

[System.Serializable]
public class ShopItemData
{
    public string itemName;
    [TextArea(2, 3)] public string description;
    public ShopItemType itemType;
    public int price = 80;
    public float value = 0.30f;
    public Sprite icon;

    public ShopItemData() { }

    public ShopItemData(string name, string desc, ShopItemType type, int cost, float val)
    {
        itemName = name;
        description = desc;
        itemType = type;
        price = cost;
        value = val;
    }

    public ShopItemData CloneWithPrice(int overridePrice, string overrideDescription = null)
    {
        return new ShopItemData(
            itemName,
            string.IsNullOrEmpty(overrideDescription) ? description : overrideDescription,
            itemType,
            overridePrice,
            value)
        {
            icon = this.icon
        };
    }
}
