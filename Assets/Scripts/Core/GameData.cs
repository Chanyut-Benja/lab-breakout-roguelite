using System;
using System.Collections.Generic;

[Serializable]
public class GameData
{
    public int researchPoints = 0;

    // Legacy Global Upgrade Levels (kept for backward compatibility)
    public int hpUpgradeLevel = 0;
    public int speedUpgradeLevel = 0;
    public int damageUpgradeLevel = 0;
    public int attackRateUpgradeLevel = 0;

    // Character-Specific Skill Tree Branch Levels (4 Focus Branches per Character, Max Lv.6: 150->200->250->500->800->1500 RP)
    // N-01 Vanguard: [0] Armor, [1] Reflect, [2] HP Regen, [3] Knockback
    public int[] vanguardBranchLevels = new int[4];

    // N-02 Psionic: [0] Crit Chance, [1] DoT Dmg, [2] Stun Duration, [3] AOE
    public int[] psionicBranchLevels = new int[4];

    // N-03 Cyber-Swift: [0] Move Speed, [1] Dash Cooldown, [2] Attack Speed, [3] I-Frame Duration
    public int[] cyberSwiftBranchLevels = new int[4];

    // Unlocked Individual Skill Tree Node IDs (Core + Minor + Major Nodes across N-01, N-02, N-03)
    public List<string> unlockedSkillNodes = new List<string>();

    public void EnsureInitialized()
    {
        if (vanguardBranchLevels == null || vanguardBranchLevels.Length < 4)
            vanguardBranchLevels = new int[4];
        if (psionicBranchLevels == null || psionicBranchLevels.Length < 4)
            psionicBranchLevels = new int[4];
        if (cyberSwiftBranchLevels == null || cyberSwiftBranchLevels.Length < 4)
            cyberSwiftBranchLevels = new int[4];
        if (unlockedSkillNodes == null)
            unlockedSkillNodes = new List<string>();

        // Core Nodes ของทั้ง 3 ตัวละครจะถูกปลดล็อกเริ่มต้นเสมอ (0 RP)
        EnsureCoreNodeUnlocked("N01_Core");
        EnsureCoreNodeUnlocked("N02_Core");
        EnsureCoreNodeUnlocked("N03_Core");
    }

    private void EnsureCoreNodeUnlocked(string coreNodeId)
    {
        if (!unlockedSkillNodes.Contains(coreNodeId))
        {
            unlockedSkillNodes.Add(coreNodeId);
        }
    }
}
