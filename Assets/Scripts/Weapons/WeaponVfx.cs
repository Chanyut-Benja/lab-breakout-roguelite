using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generates simple sprites at runtime (circle, ring, arc) so the weapon system
/// needs no art assets or prefabs.
/// </summary>
public static partial class WeaponVfx
{
    private static Sprite circle;
    private static Sprite ring;
    private static readonly Dictionary<int, Sprite> arcs = new Dictionary<int, Sprite>();

    public static Sprite Circle
    {
        get { if (circle == null) circle = MakeDisc(64, false); return circle; }
    }

    public static Sprite Ring
    {
        get { if (ring == null) ring = MakeDisc(128, true); return ring; }
    }

    /// <summary>Arc sector facing +X, radius = 1 world unit, opening angle in degrees.</summary>
    public static Sprite Arc(float degrees)
    {
        int key = Mathf.RoundToInt(degrees / 10f) * 10;
        Sprite s;
        if (!arcs.TryGetValue(key, out s) || s == null)
        {
            s = MakeArc(128, key);
            arcs[key] = s;
        }
        return s;
    }

    // Sprite is 1 unit wide
    private static Sprite MakeDisc(int size, bool ringOnly)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a;
                if (ringOnly) a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.9f) * 16f);
                else a = Mathf.Clamp01((1f - d) * 6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // Sprite is 2 units wide (radius 1 unit)
    private static Sprite MakeArc(int size, float degrees)
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
                float dx = (x + 0.5f - r) / r;
                float dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                float a = Mathf.Clamp01((1f - d) * 8f)
                        * Mathf.Clamp01((d - 0.3f) * 8f)
                        * Mathf.Clamp01((half - ang) / 6f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
    }

    /// <summary>Spawns a short-lived sprite that scales and fades out.</summary>
    public static GameObject Spawn(Sprite sprite, Vector2 position, float rotationDeg, Color color,
                                   float startScale, float endScale, float life)
    {
        GameObject go = new GameObject("WeaponVfx");
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 20;
        VfxFade fade = go.AddComponent<VfxFade>();
        fade.Setup(sr, life, startScale, endScale, color);
        return go;
    }

    /// <summary>A stretched, fading line between two points (lightning, lasers, poison threads).</summary>
    public static GameObject Beam(Vector2 from, Vector2 to, float width, Color color, float life)
    {
        Vector2 d = to - from;
        float len = Mathf.Max(0.05f, d.magnitude);
        float rot = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        GameObject go = Spawn(Circle, (from + to) * 0.5f, rot, color, 1f, 1f, life);
        go.GetComponent<VfxFade>().SetAxisScale(new Vector3(len, width, 1f));
        return go;
    }
}

public class VfxFade : MonoBehaviour
{
    private SpriteRenderer sr;
    private float life = 0.2f;
    private float t;
    private float s0 = 1f;
    private float s1 = 1f;
    private Color baseColor = Color.white;
    private Vector3 axisScale = Vector3.one;

    public void SetAxisScale(Vector3 axes)
    {
        axisScale = axes;
        transform.localScale = Vector3.Scale(axisScale, Vector3.one * s0);
    }

    public void Setup(SpriteRenderer renderer, float lifetime, float startScale, float endScale, Color color)
    {
        sr = renderer;
        life = Mathf.Max(0.01f, lifetime);
        s0 = startScale;
        s1 = endScale;
        baseColor = color;
        transform.localScale = Vector3.Scale(axisScale, Vector3.one * s0);
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / life);
        transform.localScale = Vector3.Scale(axisScale, Vector3.one * Mathf.Lerp(s0, s1, k));
        if (sr != null)
        {
            Color c = baseColor;
            c.a = baseColor.a * (1f - k);
            sr.color = c;
        }
        if (k >= 1f) Destroy(gameObject);
    }
}
