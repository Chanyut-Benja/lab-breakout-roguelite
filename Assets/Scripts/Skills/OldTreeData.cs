using System.Collections.Generic;
using UnityEngine;

// The original 12-node, 4-direction skill tree (GDD section 9): same nodes, prices and layout as our first version.
// Stored in the shared save (GameData.unlockedSkillNodes) as ids like "N01_top1", so ResearchTreeManager
// keeps handling RP, unlocking and saving; this file only describes the nodes.

public enum OldEffect
{
    None,
    Armor, HpPercent, MovePercent, DamagePercent, AttackSpeedPercent, CritRate, CritDamage,
    EffectDamagePercent, CooldownReduction, PoisonPercent, DashDistance, DashRecharge, IFrame,
    BlastNova, AbsoluteArmor, KineticReflect, JuggernautMomentum, TitanRevitalization,
    MindCollapse, PsionicBarrier, OverchargeNova, VenomCatalyst, TemporalRift,
    PhantomClone, BulletTime, OverclockOverdrive, PhaseShifter, LightningReflexes
}

public class OldTreeNode
{
    public string id, name, description, parent;
    public OldEffect effect;
    public float value;
    public int cost, gx, gy;
    public bool major;
}

public static class OldTreeData
{
    private static readonly Dictionary<int, List<OldTreeNode>> cache = new Dictionary<int, List<OldTreeNode>>();

    public static string CharPrefix(int c) { return "N0" + (c + 1) + "_"; }

    public static List<OldTreeNode> NodesFor(int c)
    {
        c = Mathf.Clamp(c, 0, 2);
        List<OldTreeNode> list;
        if (!cache.TryGetValue(c, out list)) { list = Build(c); cache[c] = list; }
        return list;
    }

    public static OldTreeNode FindByFullId(string fullId)
    {
        for (int c = 0; c < 3; c++)
        {
            string p = CharPrefix(c);
            if (fullId == null || !fullId.StartsWith(p)) continue;
            string id = fullId.Substring(p.Length);
            List<OldTreeNode> n = NodesFor(c);
            for (int i = 0; i < n.Count; i++) if (n[i].id == id) return n[i];
        }
        return null;
    }

    private static OldTreeNode N(string id, string name, string desc, string parent, OldEffect effect,
                               float value, int cost, int gx, int gy, bool major)
    {
        return new OldTreeNode
        {
            id = id, name = name, description = desc, parent = parent, effect = effect,
            value = value, cost = cost, gx = gx, gy = gy, major = major
        };
    }

    private static List<OldTreeNode> Build(int c)
    {
        // Layout and prices are identical for every character (GDD section 9.4)
        List<OldTreeNode> n = new List<OldTreeNode>();

        // ----- Minor nodes: (name, effect, value) per slot -----
        // slots: top1, top2, left, leftdown, right, rightdown, down
        OldEffect[] minorEffect;
        float[] minorValue;
        string[] minorName;
        string[] majorName;
        string[] majorDesc;
        OldEffect[] majorEffect;   // M1..M5

        if (c == 0) // N-01 Vanguard
        {
            minorEffect = new[] { OldEffect.Armor, OldEffect.HpPercent, OldEffect.MovePercent, OldEffect.Armor,
                                  OldEffect.DamagePercent, OldEffect.CritRate, OldEffect.AttackSpeedPercent };
            minorValue = new[] { 5f, 0.10f, 0.05f, 5f, 0.05f, 0.05f, 0.05f };
            majorEffect = new[] { OldEffect.BlastNova, OldEffect.AbsoluteArmor, OldEffect.KineticReflect,
                                  OldEffect.JuggernautMomentum, OldEffect.TitanRevitalization };
            majorName = new[] { "Blast Nova", "Absolute Armor", "Kinetic Reflect", "Juggernaut Momentum", "Titan Revitalization" };
            majorDesc = new[]
            {
                "Nano-Blade hitting 3+ enemies in one swing releases a shockwave (60 dmg, 4m, knockback). 3s cooldown.",
                "Below 30% HP you become invulnerable for 3s. 90s cooldown.",
                "When you are hit, reflect 150% of your Armor as damage to enemies within 2m.",
                "Moving continuously builds up to +20% damage (+4% per second of movement).",
                "Killing a Mini-Boss heals 15% max HP. Killing a Boss heals 30%."
            };
        }
        else if (c == 1) // N-02 Psionic
        {
            minorEffect = new[] { OldEffect.CooldownReduction, OldEffect.EffectDamagePercent, OldEffect.CritRate,
                                  OldEffect.DamagePercent, OldEffect.CritDamage, OldEffect.EffectDamagePercent,
                                  OldEffect.PoisonPercent };
            minorValue = new[] { 0.05f, 0.08f, 0.05f, 0.05f, 0.15f, 0.08f, 0.20f };
            majorEffect = new[] { OldEffect.MindCollapse, OldEffect.PsionicBarrier, OldEffect.OverchargeNova,
                                  OldEffect.VenomCatalyst, OldEffect.TemporalRift };
            majorName = new[] { "Mind Collapse", "Psionic Barrier", "Overcharge Nova", "Venom Catalyst", "Temporal Rift" };
            majorDesc = new[]
            {
                "Enemies stunned by Mind Shock become Vulnerable: +25% damage taken for 4s.",
                "When you are hit below 30% HP: heal 15% of max HP and gain a shield of 10% of max HP (fades after 5s). 40s cooldown.",
                "Every 3rd attack releases a psionic wave (8m) that destroys enemy shots. 10s cooldown.",
                "When a poisoned enemy dies, its poison spreads to the 2 nearest enemies.",
                "Every 50 kills freezes all enemies for 1.5s (bosses 0.75s)."
            };
        }
        else // N-03 Cyber-Swift
        {
            minorEffect = new[] { OldEffect.DashDistance, OldEffect.MovePercent, OldEffect.AttackSpeedPercent,
                                  OldEffect.DashRecharge, OldEffect.MovePercent, OldEffect.IFrame,
                                  OldEffect.AttackSpeedPercent };
            minorValue = new[] { 0.10f, 0.05f, 0.05f, 0.10f, 0.05f, 0.10f, 0.05f };
            majorEffect = new[] { OldEffect.PhantomClone, OldEffect.BulletTime, OldEffect.OverclockOverdrive,
                                  OldEffect.PhaseShifter, OldEffect.LightningReflexes };
            majorName = new[] { "Phantom Clone", "Bullet Time", "Overclock Overdrive", "Phase Shifter", "Lightning Reflexes" };
            majorDesc = new[]
            {
                "Dashing leaves a hologram that explodes after 2s (70 dmg, 3m). (It does not lure enemies yet.)",
                "Dashing through an enemy shot (Perfect Dash) slows all enemies by 50% for 1.5s.",
                "Every 10m you move: +5% fire rate, stacking up to +40%. Fades when you stand still.",
                "+1 Dash charge (3 in total).",
                "Dodging a hit with a Dash: 15% chance your next attack is a guaranteed critical."
            };
        }
        minorName = new string[7];
        for (int i = 0; i < 7; i++) minorName[i] = MinorLabel(minorEffect[i], minorValue[i]);

        // slot order: top1, top2, left, leftdown, right, rightdown, down
        n.Add(N("top1", minorName[0], MinorDesc(minorEffect[0], minorValue[0]), "core", minorEffect[0], minorValue[0], 150, 0, 1, false));
        n.Add(N("top2", minorName[1], MinorDesc(minorEffect[1], minorValue[1]), "top1", minorEffect[1], minorValue[1], 250, 0, 2, false));
        n.Add(N("M1", majorName[0], majorDesc[0], "top2", majorEffect[0], 0f, 500, 0, 3, true));

        n.Add(N("left", minorName[2], MinorDesc(minorEffect[2], minorValue[2]), "core", minorEffect[2], minorValue[2], 150, -1, 0, false));
        n.Add(N("M2", majorName[1], majorDesc[1], "left", majorEffect[1], 0f, 800, -2, 0, true));
        n.Add(N("leftdown", minorName[3], MinorDesc(minorEffect[3], minorValue[3]), "M2", minorEffect[3], minorValue[3], 250, -2, -1, false));
        n.Add(N("M4", majorName[3], majorDesc[3], "leftdown", majorEffect[3], 0f, 1500, -2, -2, true));

        n.Add(N("right", minorName[4], MinorDesc(minorEffect[4], minorValue[4]), "core", minorEffect[4], minorValue[4], 150, 1, 0, false));
        n.Add(N("M3", majorName[2], majorDesc[2], "right", majorEffect[2], 0f, 800, 2, 0, true));
        n.Add(N("rightdown", minorName[5], MinorDesc(minorEffect[5], minorValue[5]), "M3", minorEffect[5], minorValue[5], 250, 2, -1, false));
        n.Add(N("M5", majorName[4], majorDesc[4], "rightdown", majorEffect[4], 0f, 1500, 2, -2, true));

        n.Add(N("down", minorName[6], MinorDesc(minorEffect[6], minorValue[6]), "core", minorEffect[6], minorValue[6], 200, 0, -1, false));
        return n;
    }

    public static string MinorLabel(OldEffect e, float v)
    {
        switch (e)
        {
            case OldEffect.Armor: return "Armor +" + v.ToString("0");
            case OldEffect.HpPercent: return "HP +" + (v * 100f).ToString("0") + "%";
            case OldEffect.MovePercent: return "Move Speed +" + (v * 100f).ToString("0") + "%";
            case OldEffect.DamagePercent: return "Damage +" + (v * 100f).ToString("0") + "%";
            case OldEffect.AttackSpeedPercent: return "Attack Speed +" + (v * 100f).ToString("0") + "%";
            case OldEffect.CritRate: return "Crit Rate +" + (v * 100f).ToString("0") + "%";
            case OldEffect.CritDamage: return "Crit Damage +" + (v * 100f).ToString("0") + "%";
            case OldEffect.EffectDamagePercent: return "Skill Damage +" + (v * 100f).ToString("0") + "%";
            case OldEffect.CooldownReduction: return "Cooldown -" + (v * 100f).ToString("0") + "%";
            case OldEffect.PoisonPercent: return "Poison +" + (v * 100f).ToString("0") + "%";
            case OldEffect.DashDistance: return "Dash Distance +" + (v * 100f).ToString("0") + "%";
            case OldEffect.DashRecharge: return "Dash Recharge -" + (v * 100f).ToString("0") + "%";
            case OldEffect.IFrame: return "Agility +" + v.ToString("0.0") + "s";
            default: return e.ToString();
        }
    }

    public static string MinorDesc(OldEffect e, float v)
    {
        switch (e)
        {
            case OldEffect.Armor: return "Flat damage reduction on every hit you take (at least 20% still gets through).";
            case OldEffect.EffectDamagePercent: return "Boons and special effects deal more damage.";
            case OldEffect.CooldownReduction: return "Weapons fire faster.";
            case OldEffect.PoisonPercent: return "Poison deals more damage.";
            case OldEffect.DashDistance: return "Dash travels further.";
            case OldEffect.DashRecharge: return "Dash charges refill faster.";
            case OldEffect.IFrame: return "Stay invulnerable for a moment after a Dash ends.";
            default: return MinorLabel(e, v) + ".";
        }
    }
}
