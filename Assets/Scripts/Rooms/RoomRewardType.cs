public enum RoomRewardType
{
    BioGeneChamber = 0, // สุ่ม Boon 1 ใน 3 (น้ำหนัก 28)
    BlackMarket = 1,    // ร้านค้า Black Market (น้ำหนัก 27)
    CreditVault = 2,    // ได้ Nano-Credits ก้อนใหญ่ไปซื้อในร้าน (น้ำหนัก 22)
    MedicalPod = 3,     // ฟื้น HP 30% ฟรี (น้ำหนัก 18)
    GeneEnhancer = 4,   // อัป Boon เป็น Lv.2 ทันที หรือเกิด Resonance ฟรี (Rare - น้ำหนัก 5)
    Boss = 5,           // ห้องบอส (ทุกๆ 3 ห้อง)

    // Legacy Aliases สำหรับความเข้ากันได้ย้อนหลังกับโค้ดและ Prefab เดิม
    Boon = BioGeneChamber,
    Shop = BlackMarket,
    ResearchPoints = CreditVault
}
