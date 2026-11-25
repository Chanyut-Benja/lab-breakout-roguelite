using System.Collections.Generic;
using UnityEngine;

// ============================================================================
//  Richer weapon VFX built from code (GDD: "Projectile behaviour and VFX of every line").
//  Soft glows, trails, sparks, jagged lightning, bubbles, shockwaves and swirls.
//  Everything is generated at runtime - no art assets or prefabs.
// ============================================================================
public static partial class WeaponVfx
{
    private static Sprite glow, bubble, hexBubble;
    private static readonly Dictionary<int, Sprite> crescents = new Dictionary<int, Sprite>();
    private static Material lineMat;

    // ------------------------------------------------------------------ sprites
    /// <summary>Soft radial glow (bright centre, fades to nothing).</summary>
    public static Sprite Glow
    {
        get { if (glow == null) glow = MakeGlow(64); return glow; }
    }

    /// <summary>Translucent bubble with a bright rim (shields, poison bubbles). hex = honeycomb pattern.</summary>
    public static Sprite Bubble(bool hex)
    {
        if (hex) { if (hexBubble == null) hexBubble = MakeBubble(128, true); return hexBubble; }
        if (bubble == null) bubble = MakeBubble(128, false);
        return bubble;
    }

    /// <summary>Thick crescent that tapers to sharp tips (blade waves). Faces +X, radius 1 unit at scale 1.</summary>
    public static Sprite Crescent(float degrees)
    {
        int key = Mathf.RoundToInt(degrees / 10f) * 10;
        Sprite s;
        if (!crescents.TryGetValue(key, out s) || s == null)
        {
            s = MakeCrescent(128, key);
            crescents[key] = s;
        }
        return s;
    }

    private static readonly Dictionary<int, Sprite> thinRings = new Dictionary<int, Sprite>();
    private static readonly Dictionary<int, Sprite> bladeCrescents = new Dictionary<int, Sprite>();

    /// <summary>Thin circle outline, 1 unit wide at scale 1. thickness = fraction of the radius (e.g. 0.03).</summary>
    public static Sprite ThinRing(float thickness)
    {
        int key = Mathf.RoundToInt(thickness * 1000f);
        Sprite s;
        if (!thinRings.TryGetValue(key, out s) || s == null)
        {
            int size = 256;
            Texture2D tex = NewTex(size);
            float r = size * 0.5f;
            float half = Mathf.Max(0.004f, thickness * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((half - Mathf.Abs(d - (1f - half - 0.01f))) / 0.008f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            thinRings[key] = s;
        }
        return s;
    }

    /// <summary>Slim curved sword-wave: bulges forward (+X), both tips end in sharp points.</summary>
    public static Sprite BladeCrescent(float degrees)
    {
        int key = Mathf.RoundToInt(degrees / 10f) * 10;
        Sprite s;
        if (!bladeCrescents.TryGetValue(key, out s) || s == null)
        {
            int size = 256;
            Texture2D tex = NewTex(size);
            float r = size * 0.5f;
            float half = key * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                    float t = Mathf.Clamp01(ang / half);
                    float inner = Mathf.Lerp(0.70f, 1.0f, Mathf.Pow(t, 0.8f));   // slim in the middle, meets the outer edge at the tips
                    float outer = Mathf.Lerp(1.0f, 0.99f, t);
                    float a = Mathf.Clamp01((outer - d) * 28f) * Mathf.Clamp01((d - inner) * 28f) * (ang <= half ? 1f : 0f);
                    float body = Mathf.Clamp01((d - inner) / Mathf.Max(0.02f, outer - inner));
                    a *= Mathf.Lerp(0.65f, 1f, body);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
            bladeCrescents[key] = s;
        }
        return s;
    }

    private static Sprite MakeGlow(int size)
    {
        Texture2D tex = NewTex(size);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static float HexEdge(float x, float y, float size)
    {
        float q = (0.57735f * x - 0.33333f * y) / size;
        float r = (0.66667f * y) / size;
        float cx = q, cz = r, cy = -cx - cz;
        float rx = Mathf.Round(cx), ry = Mathf.Round(cy), rz = Mathf.Round(cz);
        float ax = Mathf.Abs(rx - cx), ay = Mathf.Abs(ry - cy), az = Mathf.Abs(rz - cz);
        if (ax > ay && ax > az) rx = -ry - rz;
        else if (ay > az) ry = -rx - rz;
        else rz = -rx - ry;
        float fq = cx - rx, fr = cz - rz;
        float px = size * (1.73205f * fq + 0.86603f * fr);
        float py = size * (1.5f * fr);
        float dist = Mathf.Sqrt(px * px + py * py) / size;
        return Mathf.Clamp01((dist - 0.72f) * 7f);
    }

    private static Sprite MakeBubble(int size, bool hex)
    {
        Texture2D tex = NewTex(size);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1f) { tex.SetPixel(x, y, Color.clear); continue; }

                float rim = Mathf.Exp(-Mathf.Pow((d - 0.93f) * 12f, 2f));
                float fill = 0.10f + 0.10f * d * d;
                float hl = Mathf.Clamp01(1f - ((dx + 0.38f) * (dx + 0.38f) + (dy - 0.38f) * (dy - 0.38f)) * 10f) * 0.35f;
                float a = fill + rim * 0.85f + hl;
                if (hex) a += HexEdge(dx, dy, 0.22f) * 0.22f * (1f - d * 0.4f);
                a *= Mathf.Clamp01((1f - d) * 25f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private static Sprite MakeCrescent(int size, float degrees)
    {
        Texture2D tex = NewTex(size);
        float r = size * 0.5f;
        float half = degrees * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - r) / r, dy = (y + 0.5f - r) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dy, dx) * Mathf.Rad2Deg);
                float t = Mathf.Clamp01(ang / half);
                float inner = Mathf.Lerp(0.38f, 0.97f, Mathf.Pow(t, 1.4f));   // thick in the middle, sharp tips
                float a = Mathf.Clamp01((1f - d) * 10f) * Mathf.Clamp01((d - inner) * 10f) * Mathf.Clamp01((half - ang) / 4f);
                float body = Mathf.Clamp01((d - inner) / Mathf.Max(0.05f, 1f - inner));
                a *= Mathf.Lerp(0.5f, 1f, body);                                // brighter leading edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
    }

    private static Texture2D NewTex(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        return tex;
    }

    // ------------------------------------------------------------------ helpers
    public static Material LineMaterial
    {
        get
        {
            if (lineMat == null) lineMat = new Material(Shader.Find("Sprites/Default"));
            return lineMat;
        }
    }

    /// <summary>Adds a fading trail (child object) behind a moving object. Returns the trail object.</summary>
    public static GameObject AddTrail(GameObject owner, Color color, float width, float time)
    {
        GameObject go = new GameObject("Trail");
        go.transform.SetParent(owner.transform, false);
        TrailRenderer tr = go.AddComponent<TrailRenderer>();
        tr.material = LineMaterial;
        tr.time = time;
        tr.startWidth = width;
        tr.endWidth = 0f;
        tr.minVertexDistance = 0.04f;
        tr.numCapVertices = 3;
        tr.sortingOrder = 14;
        tr.autodestruct = false;
        Gradient g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(0f, 1f) });
        tr.colorGradient = g;
        return go;
    }

    public static void Flash(Vector2 position, Color color, float size, float life)
    {
        Spawn(Glow, position, 0f, color, size * 0.5f, size, life);
    }

    /// <summary>Expanding ring with a soft glow behind it.</summary>
    public static void Shockwave(Vector2 position, Color color, float maxDiameter, float life)
    {
        Spawn(Ring, position, 0f, color, maxDiameter * 0.15f, maxDiameter, life);
        Spawn(Glow, position, 0f, new Color(color.r, color.g, color.b, color.a * 0.35f),
              maxDiameter * 0.2f, maxDiameter * 0.9f, life);
    }

    /// <summary>A few small particles flying out in every direction.</summary>
    public static void Burst(Vector2 position, Color color, int count, float speed, float life, float size)
    {
        // a real Unity Particle System (built at runtime, destroys itself when it finishes)
        GameObject go = new GameObject("VfxBurst");
        go.transform.position = position;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.duration = 0.2f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.2f);
        main.startColor = color;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;
        main.maxParticles = Mathf.Max(16, count * 2);

        ParticleSystem.EmissionModule em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(count, 1, 200)) });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
        drag.enabled = true;
        drag.dampen = 0.1f;
        drag.limit = new ParticleSystem.MinMaxCurve(speed * 0.5f);

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.material = BurstMaterial();
        psr.sortingOrder = 21;
        ps.Play();
    }

    private static Material burstMaterial;
    private static Material BurstMaterial()
    {
        if (burstMaterial == null)
        {
            Shader sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            burstMaterial = new Material(sh);
            if (Glow != null) burstMaterial.mainTexture = Glow.texture;
        }
        return burstMaterial;
    }

    /// <summary>One particle: velocity, drag, upward acceleration, fades and shrinks.</summary>
    public static GameObject Particle(Vector2 position, Vector2 velocity, Color color, float life, float size,
                                      float drag, float accelY, Sprite sprite)
    {
        GameObject go = new GameObject("VfxParticle");
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = 21;
        VfxParticle p = go.AddComponent<VfxParticle>();
        p.Setup(sr, velocity, life, size, drag, accelY, color);
        return go;
    }

    /// <summary>Jagged lightning bolt with a white core and optional side branches.</summary>
    public static void Lightning(Vector2 from, Vector2 to, Color color, float width, float life, int branches)
    {
        Bolt(from, to, color, width, life);
        Bolt(from, to, new Color(1f, 1f, 1f, color.a), width * 0.35f, life);

        Vector2 d = to - from;
        for (int b = 0; b < branches; b++)
        {
            Vector2 start = Vector2.Lerp(from, to, Random.Range(0.25f, 0.75f));
            Vector2 side = new Vector2(-d.y, d.x).normalized * Random.Range(-1f, 1f) * Mathf.Min(2.5f, d.magnitude * 0.4f);
            Bolt(start, start + d.normalized * d.magnitude * 0.25f + side, color, width * 0.55f, life);
        }
    }

    private static void Bolt(Vector2 from, Vector2 to, Color color, float width, float life)
    {
        GameObject go = new GameObject("Bolt");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = LineMaterial;
        lr.useWorldSpace = true;
        lr.sortingOrder = 22;
        lr.numCapVertices = 2;

        Vector2 d = to - from;
        float len = Mathf.Max(0.1f, d.magnitude);
        int seg = Mathf.Clamp(Mathf.RoundToInt(len * 2.2f), 4, 16);
        Vector2 perp = new Vector2(-d.y, d.x).normalized;
        float jag = Mathf.Min(0.6f, len * 0.12f);

        lr.positionCount = seg + 1;
        for (int i = 0; i <= seg; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)seg);
            if (i > 0 && i < seg) p += perp * Random.Range(-jag, jag);
            lr.SetPosition(i, p);
        }
        VfxLineFade f = go.AddComponent<VfxLineFade>();
        f.Setup(lr, life, color, width);
    }

    /// <summary>Blade shards that fly out of a point in a full circle (Blade Shield Matrix).</summary>
    public static void BladeRing(Vector2 origin, float radius, Color color, int count)
    {
        float life = 0.35f;
        for (int i = 0; i < count; i++)
        {
            float ang = 360f * i / count;
            Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
            GameObject go = Spawn(Crescent(50f), origin + dir * 0.6f, ang, color, 0.9f, 1.3f, life);
            VfxFly fly = go.AddComponent<VfxFly>();
            fly.Setup(dir * (radius / life));
        }
        Shockwave(origin, new Color(color.r, color.g, color.b, 0.7f), radius * 2f, life);
    }
}

/// <summary>Small particle: moves, slows down, fades and shrinks.</summary>
public class VfxParticle : MonoBehaviour
{
    private SpriteRenderer sr;
    private Vector2 velocity;
    private float life, t, size, drag, accelY;
    private Color color;

    public void Setup(SpriteRenderer renderer, Vector2 v, float lifetime, float startSize, float dragAmount, float up, Color c)
    {
        sr = renderer;
        velocity = v;
        life = Mathf.Max(0.05f, lifetime);
        size = startSize;
        drag = dragAmount;
        accelY = up;
        color = c;
        transform.localScale = Vector3.one * size;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / life;
        if (k >= 1f) { Destroy(gameObject); return; }

        velocity.y += accelY * Time.deltaTime;
        velocity *= Mathf.Max(0f, 1f - drag * Time.deltaTime);
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.localScale = Vector3.one * size * (1f - 0.6f * k);
        Color c = color;
        c.a = color.a * (1f - k);
        sr.color = c;
    }
}

/// <summary>Moves an object in a straight line (blade shards, crescents).</summary>
public class VfxFly : MonoBehaviour
{
    private Vector2 velocity;
    public void Setup(Vector2 v) { velocity = v; }
    private void Update() { transform.position += (Vector3)(velocity * Time.deltaTime); }
}

/// <summary>Spins an object (accretion rings of a black hole). Optional lifetime.</summary>
public class VfxSpin : MonoBehaviour
{
    private float degPerSec;
    private float life;
    public void Setup(float speed, float lifetime) { degPerSec = speed; life = lifetime; }
    private void Update()
    {
        transform.Rotate(0f, 0f, degPerSec * Time.deltaTime);
        if (life > 0f)
        {
            life -= Time.deltaTime;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}

/// <summary>Fades a LineRenderer out (lightning bolts).</summary>
public class VfxLineFade : MonoBehaviour
{
    private LineRenderer lr;
    private float life, t, width;
    private Color color;

    public void Setup(LineRenderer line, float lifetime, Color c, float w)
    {
        lr = line;
        life = Mathf.Max(0.02f, lifetime);
        color = c;
        width = w;
        lr.startWidth = width;
        lr.endWidth = width * 0.6f;
        lr.startColor = color;
        lr.endColor = color;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float k = t / life;
        if (k >= 1f) { Destroy(gameObject); return; }
        Color c = color;
        c.a = color.a * (1f - k);
        lr.startColor = c;
        lr.endColor = c;
        lr.startWidth = width * (1f - 0.5f * k);
        lr.endWidth = width * 0.6f * (1f - k);
    }
}
