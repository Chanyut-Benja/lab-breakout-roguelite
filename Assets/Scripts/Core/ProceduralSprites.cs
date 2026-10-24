using UnityEngine;

// Simple readable stand-in art. Replace these sprites when final character art is ready.
public static class ProceduralSprites
{
    private static Sprite agent;
    private static Sprite minion;
    private static Sprite boss;
    private static Sprite shot;
    private static Sprite crystal;
    private static Sprite nanoSlash;
    private static Sprite circleDisc;
    private static Sprite circleRing;
    private static Sprite shieldHex;
    private static Material spriteMaterial;

    private static readonly Color32 Ink = new Color32(25, 48, 65, 255);
    private static readonly Color32 Shell = new Color32(246, 252, 255, 255);
    private static readonly Color32 Detail = new Color32(76, 207, 233, 255);
    private static readonly Color32 Shadow = new Color32(132, 159, 174, 255);

    public static Sprite Agent => agent != null ? agent : agent = MakeAgent();
    public static Sprite Minion => minion != null ? minion : minion = MakeMinion();
    public static Sprite Boss => boss != null ? boss : boss = MakeBoss();
    public static Sprite Shot => shot != null ? shot : shot = MakeShot();
    public static Sprite Crystal => crystal != null ? crystal : crystal = MakeCrystal();
    public static Sprite NanoSlash => nanoSlash != null ? nanoSlash : nanoSlash = MakeNanoSlash();
    public static Sprite CircleDisc => circleDisc != null ? circleDisc : circleDisc = MakeCircleDisc();
    public static Sprite CircleRing => circleRing != null ? circleRing : circleRing = MakeCircleRing();
    public static Sprite ShieldHex => shieldHex != null ? shieldHex : shieldHex = MakeShieldHex();

    public static void EnsureVisible(SpriteRenderer renderer, Sprite fallback)
    {
        if (renderer == null) return;
        if (renderer.sprite == null) renderer.sprite = fallback;
        if (renderer.sharedMaterial != null) return;

        if (spriteMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader != null) spriteMaterial = new Material(shader);
        }
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
    }

    private static Sprite MakeAgent()
    {
        var p = Blank();
        Rect(p, 21, 8, 29, 26, Ink); Rect(p, 35, 8, 43, 26, Ink);
        Rect(p, 23, 11, 28, 25, Shell); Rect(p, 36, 11, 41, 25, Shell);
        Disc(p, 32, 34, 17, Ink); Disc(p, 32, 34, 14, Shell);
        Rect(p, 12, 30, 20, 44, Ink); Rect(p, 44, 30, 52, 44, Ink);
        Rect(p, 14, 32, 19, 42, Shell); Rect(p, 45, 32, 50, 42, Shell);
        Disc(p, 32, 48, 13, Ink); Disc(p, 32, 48, 10, Shell);
        Rect(p, 23, 47, 41, 52, Detail); Rect(p, 27, 28, 37, 33, Detail);
        Rect(p, 47, 25, 58, 31, Ink); Rect(p, 49, 27, 56, 29, Detail);
        return Finish(p, "Agent_Prototype");
    }

    private static Sprite MakeMinion()
    {
        var p = Blank();
        Rect(p, 5, 29, 17, 35, Ink); Rect(p, 47, 29, 59, 35, Ink);
        Rect(p, 28, 5, 36, 17, Ink); Rect(p, 28, 47, 36, 59, Ink);
        Disc(p, 32, 32, 22, Ink); Disc(p, 32, 32, 18, Shell);
        Disc(p, 32, 32, 12, Shadow); Disc(p, 32, 32, 7, Detail);
        Rect(p, 12, 29, 20, 35, Shell); Rect(p, 44, 29, 52, 35, Shell);
        return Finish(p, "Minion_Prototype");
    }

    private static Sprite MakeBoss()
    {
        var p = Blank();
        Rect(p, 6, 7, 20, 19, Ink); Rect(p, 44, 7, 58, 19, Ink);
        Rect(p, 7, 44, 20, 57, Ink); Rect(p, 44, 44, 57, 57, Ink);
        Rect(p, 9, 10, 18, 17, Shell); Rect(p, 46, 10, 55, 17, Shell);
        Rect(p, 9, 46, 18, 55, Shell); Rect(p, 46, 46, 55, 55, Shell);
        Disc(p, 32, 32, 25, Ink); Disc(p, 32, 32, 20, Shell);
        Rect(p, 18, 28, 46, 36, Ink); Rect(p, 21, 30, 43, 34, Detail);
        Disc(p, 32, 32, 8, Ink); Disc(p, 32, 32, 5, Shell);
        return Finish(p, "Boss_Prototype");
    }

    private static Sprite MakeShot()
    {
        var p = Blank();
        Rect(p, 5, 25, 54, 39, Ink); Rect(p, 9, 28, 57, 36, Detail);
        Rect(p, 14, 30, 52, 34, Shell);
        return Finish(p, "PulseShot_Prototype");
    }

    private static Sprite MakeCrystal()
    {
        var p = Blank();
        for (int y = 8; y < 56; y++)
        {
            int width = Mathf.RoundToInt((y < 32 ? y - 8 : 56 - y) * 0.63f);
            Rect(p, 32 - width, y, 32 + width + 1, y + 1, Ink);
            if (width > 3) Rect(p, 35 - width, y, 32 + width - 2, y + 1, Detail);
        }
        Rect(p, 29, 19, 33, 43, Shell);
        return Finish(p, "ExpCrystal_Prototype");
    }

    private static Sprite MakeNanoSlash()
    {
        var p = Blank();
        Color32 cyanGlow = new Color32(60, 225, 255, 220);
        Color32 whiteCore = new Color32(245, 253, 255, 255);
        Color32 edgeTint = new Color32(20, 140, 210, 160);

        // วาดคลื่นใบมีดรูปครึ่งวงกลม (Crescent Arc) หันไปทางขวา (+X)
        Vector2 outerCenter = new Vector2(14f, 32f);
        Vector2 innerCenter = new Vector2(6f, 32f);
        float outerRadius = 44f;
        float innerRadius = 32f;

        for (int y = 0; y < 64; y++)
        {
            for (int x = 14; x < 64; x++)
            {
                Vector2 pt = new Vector2(x, y);
                float dOuter = Vector2.Distance(pt, outerCenter);
                float dInner = Vector2.Distance(pt, innerCenter);

                if (dOuter <= outerRadius && dInner >= innerRadius)
                {
                    float t = Mathf.InverseLerp(innerRadius, outerRadius, dOuter);
                    if (t > 0.65f)
                        p[y * 64 + x] = whiteCore;
                    else if (t > 0.25f)
                        p[y * 64 + x] = cyanGlow;
                    else
                        p[y * 64 + x] = edgeTint;
                }
            }
        }
        return Finish(p, "NanoSlash_Prototype");
    }

    private static Color32[] Blank() => new Color32[64 * 64];

    private static void Rect(Color32[] p, int left, int bottom, int right, int top, Color32 color)
    {
        for (int y = Mathf.Max(0, bottom); y < Mathf.Min(64, top); y++)
        for (int x = Mathf.Max(0, left); x < Mathf.Min(64, right); x++)
            p[y * 64 + x] = color;
    }

    private static void Disc(Color32[] p, int cx, int cy, int r, Color32 color)
    {
        for (int y = Mathf.Max(0, cy - r); y < Mathf.Min(64, cy + r); y++)
        for (int x = Mathf.Max(0, cx - r); x < Mathf.Min(64, cx + r); x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) < r * r)
                p[y * 64 + x] = color;
    }

    private static Sprite Finish(Color32[] pixels, string name)
    {
        Texture2D texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.name = name;
        Sprite result = Sprite.Create(texture, new Rect(0, 0, 64, 64),
            new Vector2(0.5f, 0.5f), 64f);
        result.name = name;
        return result;
    }

    private static Sprite MakeCircleDisc()
    {
        int size = 128;
        Color32[] p = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d <= radius)
                {
                    float edgeAlpha = Mathf.Clamp01((radius - d) / 2.5f);
                    p[y * size + x] = new Color(1f, 1f, 1f, edgeAlpha);
                }
            }
        }
        return FinishBilinear(p, size, size, (float)size, "Boon_CircleDisc");
    }

    private static Sprite MakeCircleRing()
    {
        int size = 128;
        Color32[] p = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;
        float innerRadius = outerRadius * 0.82f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d <= outerRadius && d >= innerRadius)
                {
                    float outerAlpha = Mathf.Clamp01((outerRadius - d) / 2.5f);
                    float innerAlpha = Mathf.Clamp01((d - innerRadius) / 2.5f);
                    float alpha = Mathf.Min(outerAlpha, innerAlpha);
                    p[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }
        return FinishBilinear(p, size, size, (float)size, "Boon_CircleRing");
    }

    private static Sprite MakeShieldHex()
    {
        int size = 128;
        Color32[] p = new Color32[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = (size / 2f) - 4f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d <= radius)
                {
                    float edgeProximity = Mathf.Clamp01(1f - Mathf.Abs(d - (radius - 4f)) / 5f);
                    float innerGlow = Mathf.Clamp01(d / radius) * 0.35f;
                    float alpha = Mathf.Clamp01(edgeProximity * 0.9f + innerGlow);
                    p[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
        }
        return FinishBilinear(p, size, size, (float)size, "Boon_ShieldHex");
    }

    private static Sprite FinishBilinear(Color32[] pixels, int w, int h, float ppu, string name)
    {
        Texture2D texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.name = name;
        Sprite result = Sprite.Create(texture, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
        result.name = name;
        return result;
    }
}
