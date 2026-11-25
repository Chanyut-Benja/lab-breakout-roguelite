using System.Collections.Generic;
using Enemies.Bosses;
using Enemies.Core;
using UnityEngine;

// ============================================================================
//  Bridge between the Major skill-tree effects (SkillMajorEffects) and the shared code base.
//  The unlock system stays in ResearchTreeManager; this file only answers "is this Major active?"
//  and gives the effects a few small combat helpers.
// ============================================================================

public enum SkillEffect
{
    None,
    BlastNova, AbsoluteArmor, KineticReflect, JuggernautMomentum, TitanRevitalization,
    MindCollapse, PsionicBarrier, OverchargeNova, VenomCatalyst, TemporalRift,
    PhantomClone, BulletTime, OverclockOverdrive, PhaseShifter, LightningReflexes
}

public static class SkillTreeRuntime
{
    public static int CharacterIndex
    {
        get { return PlayerStats.Instance != null ? (int)PlayerStats.Instance.MetaBonuses.character : 0; }
    }

    /// <summary>True when the Major node is unlocked. Effects that ResearchTreeManager / PlayerHealth already
    /// implement themselves (Kinetic Reflect, Juggernaut, Mind Collapse, Phase Shifter) return false here so they never double up.</summary>
    public static bool Has(SkillEffect effect)
    {
        if (PlayerStats.Instance == null) return false;
        CharacterMetaBonuses m = PlayerStats.Instance.MetaBonuses;
        switch (effect)
        {
            case SkillEffect.BlastNova: return m.hasBlastNovaMajor;
            case SkillEffect.AbsoluteArmor: return m.hasAbsoluteArmorMajor;
            case SkillEffect.TitanRevitalization: return m.hasTitanRevitalizationMajor;
            case SkillEffect.PsionicBarrier: return m.hasPsionicBarrierMajor;
            case SkillEffect.OverchargeNova: return m.hasOverchargeNovaMajor;
            case SkillEffect.VenomCatalyst: return m.hasVenomCatalystMajor;
            case SkillEffect.TemporalRift: return m.hasTemporalRiftMajor;
            case SkillEffect.PhantomClone: return m.hasPhantomCloneMajor;
            case SkillEffect.BulletTime: return m.hasBulletTimeMajor;
            case SkillEffect.OverclockOverdrive: return m.hasOverclockOverdriveMajor;
            case SkillEffect.LightningReflexes: return m.hasLightningReflexesMajor;
            default: return false;
        }
    }
}

/// <summary>Small combat helpers used by the Major effects and the new weapon behaviours.</summary>
public static class CombatUtil
{
    public static float DamageMult { get { return PlayerStats.Instance != null ? PlayerStats.Instance.DamageMultiplier : 1f; } }
    public static float AreaMult { get { return PlayerStats.Instance != null ? PlayerStats.Instance.AreaSizeMultiplier : 1f; } }

    private static GameObject Root(Collider2D c)
    {
        return c.attachedRigidbody != null ? c.attachedRigidbody.gameObject : c.gameObject;
    }

    /// <summary>Fills 'result' with one collider per enemy within the radius and returns the count.</summary>
    public static int QueryEnemies(Vector2 center, float radius, List<Collider2D> result)
    {
        result.Clear();
        Collider2D[] all = Physics2D.OverlapCircleAll(center, radius);
        HashSet<int> seen = new HashSet<int>();
        for (int i = 0; i < all.Length; i++)
        {
            Collider2D c = all[i];
            if (c == null || c.isTrigger && c.GetComponentInParent<EnemyBase>() == null) continue;
            if (c.CompareTag("Player")) continue;
            EnemyBase eb = c.GetComponentInParent<EnemyBase>();
            if (eb == null) continue;
            EnemyHealth eh = eb.GetComponent<EnemyHealth>();
            if (eh != null && eh.IsDead) continue;
            if (!seen.Add(eb.gameObject.GetInstanceID())) continue;
            result.Add(c);
        }
        return result.Count;
    }

    public static bool IsBoss(Collider2D c)
    {
        return c != null && c.GetComponentInParent<BossBase>() != null;
    }

    public static bool IsDead(Collider2D c)
    {
        if (c == null) return true;
        EnemyHealth eh = c.GetComponentInParent<EnemyHealth>();
        return eh != null && eh.IsDead;
    }

    public static void DealDamage(Collider2D c, float amount, Vector2 knockback)
    {
        if (c == null || amount <= 0f) return;
        IDamageable d = c.GetComponentInParent<IDamageable>();
        if (d == null || c.CompareTag("Player")) return;
        if (knockback == Vector2.zero) d.TakeDamage(amount); else d.TakeDamage(amount, knockback);
    }

    /// <summary>Slow by a fraction (1 = frozen) for a number of seconds. Bosses ignore full freezes.</summary>
    public static void Slow(Collider2D c, float fraction, float seconds)
    {
        if (c == null) return;
        if (fraction >= 0.99f)
        {
            if (!IsBoss(c)) PlayerStats.ApplyStunToTarget(Root(c), seconds);
            return;
        }
        EnemyHealth eh = c.GetComponentInParent<EnemyHealth>();
        if (eh != null) eh.ApplySlow(fraction, seconds);
    }
}

/// <summary>Remembers how many times Venom Catalyst has already passed a poison along (max 2 hops).</summary>
public class VenomCarrier : MonoBehaviour
{
    public int chain;
}

/// <summary>Hit sparks shared by every weapon (called from PlayerStats.TryApplyMindShockOnCrit, which all weapons already call after a hit).</summary>
public static class CombatFx
{
    private static int frameBudget;
    private static int lastFrame = -1;

    public static void Hit(GameObject target, bool crit)
    {
        if (target == null) return;
        if (Time.frameCount != lastFrame) { lastFrame = Time.frameCount; frameBudget = 6; }
        if (frameBudget-- <= 0) return;

        Vector2 p = target.transform.position;
        if (crit) WeaponVfx.Burst(p, new Color(1f, 0.9f, 0.3f, 1f), 7, 5.5f, 0.3f, 0.16f);
        else WeaponVfx.Burst(p, new Color(1f, 1f, 1f, 0.9f), 3, 3.5f, 0.2f, 0.1f);
    }
}

/// <summary>Aiming and firing rules shared by the weapons.</summary>
public static class AimSettings
{
    /// <summary>True = weapons aim at the mouse (the player never turns). False = old auto-target.</summary>
    public static bool MouseAim = true;

    /// <summary>No shooting once the room is cleared (while the doors are open) so bullets do not clutter the screen.</summary>
    public static bool CombatPaused
    {
        get { return RoomManager.Instance != null && !RoomManager.Instance.IsRoomActive; }
    }

    public static Vector2 MouseWorld(Vector2 fallbackFrom)
    {
        Camera c = Camera.main;
        if (c == null) return fallbackFrom + Vector2.right;
        Vector3 m = c.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(m.x, m.y);
    }
}
