using System.Collections.Generic;
using UnityEngine;

/// <summary>A soft cone / fan sprite for sword slashes (faces +X, radius 1 at scale 1, bright toward the rim).</summary>
public static partial class WeaponVfx
{
    private static readonly Dictionary<int, Sprite> cones = new Dictionary<int, Sprite>();

    public static Sprite Cone(float degrees)
    {
        int key = Mathf.RoundToInt(degrees / 10f) * 10;
        Sprite s;
        if (!cones.TryGetValue(key, out s) || s == null)
        {
            s = MakeCone(128, key);
            cones[key] = s;
        }
        return s;
    }

    private static Sprite MakeCone(int size, float degrees)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        float half = degrees * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                float inside = Mathf.Clamp01((half - ang) / 3f) * Mathf.Clamp01((1f - d) * 12f);
                float fill = Mathf.Lerp(0.05f, 0.55f, d * d);                 // faint near the player, brighter outwards
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d - 0.93f) * 14f);   // bright curved edge
                float a = inside * Mathf.Clamp01(fill + rim * 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
    }
}
