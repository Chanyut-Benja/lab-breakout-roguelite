using UnityEngine;

public class CharacterSelectionManager : MonoBehaviour
{
    public static CharacterSelectionManager Instance { get; private set; }

    [System.Serializable]
    public class CharacterData
    {
        public int characterIndex = 0;
        public string characterID = "Subject01";
        public string characterName = "N-01 Vanguard";
        public Sprite characterSprite;
        public Color characterColor = Color.red;
        public int maxHP = 200;
        public float speed = 4.0f;
        public float damage = 25f;
        public WeaponType startingWeapon = WeaponType.NanoBlade;
        public bool hasMindShockPassive = false;
        public bool hasOverclockDashPassive = false;
        public int maxDashCharges = 1;
    }

    [Header("Current Selected Character")]
    public CharacterData currentSelectedCharacter = new CharacterData();

    // Property สำหรับรองรับการเรียก CurrentSelectedCharacter
    public CharacterData CurrentSelectedCharacter => currentSelectedCharacter;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // บันทึกข้าม Scene
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public static bool IsPsionicCharacter(int index, string idOrName = "")
    {
        if (!string.IsNullOrEmpty(idOrName))
        {
            string lower = idOrName.ToLowerInvariant();
            if (lower.Contains("02") || lower.Contains("psionic") || lower.Contains("2"))
                return true;
            if (lower.Contains("01") || lower.Contains("vanguard") || lower.Contains("03") || lower.Contains("cyber"))
                return false;
        }
        return index == 1;
    }

    public static bool IsCyberSwiftCharacter(int index, string idOrName = "")
    {
        if (!string.IsNullOrEmpty(idOrName))
        {
            string lower = idOrName.ToLowerInvariant();
            if (lower.Contains("03") || lower.Contains("cyber") || lower.Contains("swift") || lower.Contains("3"))
                return true;
            if (lower.Contains("01") || lower.Contains("vanguard") || lower.Contains("02") || lower.Contains("psionic"))
                return false;
        }
        return index == 2;
    }

    /// <summary>
    /// กำหนดอาวุธเริ่มต้นตามตัวละครในตารางบาลานซ์หัวข้อ 3:
    /// N-01 Vanguard -> Nano-Blade
    /// N-02 Psionic -> Bio-Needler
    /// N-03 Cyber-Swift -> Plasma Blaster
    /// </summary>
    public static WeaponType ResolveStartingWeapon(int index, string idOrName = "")
    {
        if (!string.IsNullOrEmpty(idOrName))
        {
            string lower = idOrName.ToLowerInvariant();
            if (lower.Contains("02") || lower.Contains("psionic") || lower.Contains("2"))
                return WeaponType.BioNeedler;
            if (lower.Contains("03") || lower.Contains("cyber") || lower.Contains("swift") || lower.Contains("3"))
                return WeaponType.PlasmaBlaster;
            if (lower.Contains("01") || lower.Contains("vanguard") || lower.Contains("1"))
                return WeaponType.NanoBlade;
        }

        return index switch
        {
            0 => WeaponType.NanoBlade,     // N-01 Vanguard
            1 => WeaponType.BioNeedler,    // N-02 Psionic
            2 => WeaponType.PlasmaBlaster, // N-03 Cyber-Swift
            _ => WeaponType.NanoBlade
        };
    }

    /// <summary>
    /// คืนค่าสเตตัสมาตรฐานจากตารางบาลานซ์หัวข้อ 3 สำหรับแต่ละตัวละคร
    /// </summary>
    public static void GetBalancedCharacterStats(WeaponType weapon, out int balancedHP, out float balancedSpeed, out float balancedDamage)
    {
        switch (weapon)
        {
            case WeaponType.BioNeedler: // N-02 Psionic
                balancedHP = 100;
                balancedSpeed = 4.0f;
                balancedDamage = 15f;
                break;
            case WeaponType.PlasmaBlaster: // N-03 Cyber-Swift
                balancedHP = 130;
                balancedSpeed = 4.8f;
                balancedDamage = 10f;
                break;
            case WeaponType.NanoBlade: // N-01 Vanguard
            default:
                balancedHP = 200;
                balancedSpeed = 4.0f;
                balancedDamage = 25f;
                break;
        }
    }

    public void SetSelectedCharacter(int index, Sprite sprite, Color color, int hp, float speed, float damage)
    {
        WeaponType resolvedWeapon = ResolveStartingWeapon(index);
        SetSelectedCharacter(index, sprite, color, hp, speed, damage, resolvedWeapon, $"Subject0{index + 1}", "");
    }

    public void SetSelectedCharacter(
        int index,
        Sprite sprite,
        Color color,
        int hp,
        float speed,
        float damage,
        WeaponType startingWeapon,
        string characterId = "",
        string characterName = "")
    {
        if (currentSelectedCharacter == null)
        {
            currentSelectedCharacter = new CharacterData();
        }

        string combinedId = $"{characterId} {characterName}";
        bool isPsionic = IsPsionicCharacter(index, combinedId) || startingWeapon == WeaponType.BioNeedler;
        bool isCyberSwift = !isPsionic && (IsCyberSwiftCharacter(index, combinedId) || startingWeapon == WeaponType.PlasmaBlaster);

        if (isPsionic)
        {
            hp = 100;
            speed = 4.0f;
            damage = 15f;
            startingWeapon = WeaponType.BioNeedler;
        }
        else if (isCyberSwift)
        {
            hp = 130;
            speed = 4.8f;
            damage = 10f;
            startingWeapon = WeaponType.PlasmaBlaster;
        }

        currentSelectedCharacter.characterIndex = index;
        currentSelectedCharacter.characterID = string.IsNullOrEmpty(characterId) ? $"Subject0{index + 1}" : characterId;
        currentSelectedCharacter.characterName = string.IsNullOrEmpty(characterName) ? $"Subject N-0{index + 1}" : characterName;
        currentSelectedCharacter.characterSprite = sprite;
        currentSelectedCharacter.characterColor = color;
        currentSelectedCharacter.maxHP = hp;
        currentSelectedCharacter.speed = speed;
        currentSelectedCharacter.damage = damage;
        currentSelectedCharacter.startingWeapon = startingWeapon;
        currentSelectedCharacter.hasMindShockPassive = isPsionic;
        currentSelectedCharacter.hasOverclockDashPassive = isCyberSwift;
        currentSelectedCharacter.maxDashCharges = isCyberSwift ? 2 : 1;

        Debug.Log($"[CharacterSelectionManager] Selected Character #{index} ({currentSelectedCharacter.characterName}) | Starting Weapon: {PlayerInventory.GetWeaponName(startingWeapon)} (Lv.1) | MindShock: {isPsionic} | OverclockDash: {isCyberSwift} (Dash Charges: {currentSelectedCharacter.maxDashCharges}) | HP: {hp}, Speed: {speed}");
    }
}
