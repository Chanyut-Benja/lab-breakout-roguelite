using UnityEngine;

/// <summary>
/// Our projectile look (soft glow halo + fading trail) applied on top of the friend's projectiles.
/// Safe to call more than once on the same object (pooled bullets).
/// </summary>
public static class ProjectileFx
{
    public static void Decorate(GameObject go, Color color, float glowScale = 2.6f, float trailWidth = 0.22f, float trailTime = 0.18f, bool round = false, Color? core = null)
    {
        if (go == null) return;

        Transform existing = go.transform.Find("ProjGlow");
        if (existing != null)
        {
            SpriteRenderer g0 = existing.GetComponent<SpriteRenderer>();
            if (g0 != null) g0.color = new Color(color.r, color.g, color.b, 0.55f);
            TrailRenderer t0 = go.GetComponentInChildren<TrailRenderer>();
            if (t0 != null) t0.Clear();
            return;
        }

        SpriteRenderer main = go.GetComponent<SpriteRenderer>();
        GameObject glow = new GameObject("ProjGlow");
        glow.transform.SetParent(go.transform, false);
        glow.transform.localScale = Vector3.one * glowScale;
        SpriteRenderer sr = glow.AddComponent<SpriteRenderer>();
        sr.sprite = WeaponVfx.Glow;
        sr.color = new Color(color.r, color.g, color.b, 0.55f);
        sr.sortingOrder = (main != null ? main.sortingOrder : 14) - 1;

        // brighten the core a little so the shot reads as glow core + halo
        if (main != null)
        {
            if (round) main.sprite = WeaponVfx.Circle;   // original round shot instead of a capsule
            main.color = Color.Lerp(color, Color.white, 0.35f);
        }
        if (core.HasValue)
        {
            GameObject c = new GameObject("ProjCore");
            c.transform.SetParent(go.transform, false);
            c.transform.localScale = Vector3.one * 0.55f;
            SpriteRenderer cr = c.AddComponent<SpriteRenderer>();
            cr.sprite = WeaponVfx.Circle;
            cr.color = core.Value;
            cr.sortingOrder = (main != null ? main.sortingOrder : 14) + 1;
        }

        WeaponVfx.AddTrail(go, new Color(color.r, color.g, color.b, 0.6f), trailWidth, trailTime);
    }
}
