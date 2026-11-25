using UnityEngine;

/// <summary>
/// Soft green smoke for poison effects (Epidemic Outbreak explosions, Plague Spore clouds).
/// Smoke = soft round puffs that grow, drift and fade out. Pale green so it never hides the enemies.
/// </summary>
public static class PoisonSmoke
{
    /// <summary>A puff of smoke: many puffs spread inside the radius and grow outward.</summary>
    public static void Burst(Vector2 center, float radius, int puffs = 14, float life = 0.95f)
    {
        for (int i = 0; i < puffs; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            Vector2 pos = center + dir * radius * 0.45f;
            Puff(pos, radius * Random.Range(0.55f, 0.85f), life, dir * radius * Random.Range(0.25f, 0.6f));
        }
        // a few small spores flying out
        WeaponVfx.Burst(center, new Color(0.6f, 1f, 0.6f, 0.8f), 8, radius * 1.4f, 0.6f, 0.14f);
    }

    /// <summary>One smoke puff (soft disc) that swells and fades. 'size' = start diameter.</summary>
    public static void Puff(Vector2 pos, float size, float life, Vector2? drift = null)
    {
        float tint = Random.Range(0.0f, 0.25f);
        Color c = new Color(Mathf.Lerp(0.45f, 0.75f, tint), Mathf.Lerp(0.90f, 1f, tint), Mathf.Lerp(0.45f, 0.75f, tint), Random.Range(0.26f, 0.38f));
        GameObject go = WeaponVfx.Spawn(WeaponVfx.Glow, pos, Random.Range(0f, 360f), c, size, size * Random.Range(1.5f, 2.0f), life * Random.Range(0.85f, 1.15f));
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 16;
        Vector2 v = drift.HasValue ? drift.Value : Random.insideUnitCircle * 0.5f + Vector2.up * 0.25f;
        go.AddComponent<SmokeDrift>().velocity = v;
    }
}

/// <summary>Slowly moving smoke puff (slows down by itself).</summary>
public class SmokeDrift : MonoBehaviour
{
    public Vector2 velocity;

    private void Update()
    {
        transform.position += (Vector3)(velocity * Time.deltaTime);
        velocity *= Mathf.Max(0f, 1f - 2.2f * Time.deltaTime);
    }
}
