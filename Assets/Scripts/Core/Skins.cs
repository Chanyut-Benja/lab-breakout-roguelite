using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loads the hand-drawn character/enemy sprites from Resources/Skins and puts them on top of the
/// (invisible) prototype renderer. Frames are named {set}_{index}.png (e.g. zombie_0, zombie_1).
/// </summary>
public static class SkinLibrary
{
    private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    // size multiplier (relative to the old prototype sprite) and whether the art looks to the LEFT
    private struct SetInfo { public float size; public bool facesLeft; public float width; public SetInfo(float s, bool l, float w = 1f) { size = s; facesLeft = l; width = w; } }
    private static readonly Dictionary<string, SetInfo> info = new Dictionary<string, SetInfo>
    {
        { "zombie",   new SetInfo(1.30f, true)  },
        { "ghost",    new SetInfo(1.30f, true) },
        { "blob",     new SetInfo(1.15f, false) },
        { "disc",     new SetInfo(1.00f, true) },
        { "dog",      new SetInfo(1.30f, true)  },
        { "sentinel", new SetInfo(1.35f, true) },
        { "wheelbot", new SetInfo(1.40f, true) },
        { "brute",    new SetInfo(1.55f, false) },
        { "architect", new SetInfo(1.89f, false) },   // +30%
        { "turret",   new SetInfo(1.00f, false) },
        { "doctor",   new SetInfo(1.35f, false) },
        { "digger",   new SetInfo(1.50f, false) },
        { "stormcaller", new SetInfo(1.70f, false) },
        { "overlord", new SetInfo(2.20f, false) },
        { "player1",  new SetInfo(1.87f, true) },   // -10%
        { "player2",  new SetInfo(2.02f, true, 1.3f) },
        { "player3",  new SetInfo(1.95f, true, 1.3f) },
    };

    private static Sprite credit, shot;

    /// <summary>The green 'N' nano-credit icon.</summary>
    public static Sprite CreditSprite()
    {
        if (credit != null) return credit;
        Texture2D t = Resources.Load<Texture2D>("Skins/credit_0");
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        return credit = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 40f);
    }

    /// <summary>The orange two-dot projectile fired by enemies (points to the right).</summary>
    public static Sprite ProjectileSprite()
    {
        if (shot != null) return shot;
        Texture2D t = Resources.Load<Texture2D>("Skins/shot_0");
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        return shot = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 8f);
    }

    private static Sprite[] shotFrames;

    /// <summary>The two orange dots of shot_0 as separate frames; enemy projectiles alternate between them so they blink.</summary>
    public static Sprite[] ProjectileFrames()
    {
        if (shotFrames != null) return shotFrames;
        Texture2D t = Resources.Load<Texture2D>("Skins/shot_0");
        if (t == null) return null;
        t.filterMode = FilterMode.Point;
        int half = Mathf.Max(1, t.height);                       // each dot is a height x height square
        int gap = Mathf.Max(0, t.width - 2 * half);              // empty columns between the dots
        shotFrames = new Sprite[]
        {
            Sprite.Create(t, new Rect(0, 0, half, t.height), new Vector2(0.5f, 0.5f), 8f),
            Sprite.Create(t, new Rect(half + gap, 0, half, t.height), new Vector2(0.5f, 0.5f), 8f)
        };
        return shotFrames;
    }

    private static Sprite logo;

    /// <summary>Adds the game logo image to a UI parent (replaces the plain title text).</summary>
    public static GameObject AddLogo(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        if (parent == null) return null;
        Transform old = parent.Find("GameLogo");
        GameObject go = old != null ? old.gameObject : new GameObject("GameLogo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (logo == null)
        {
            Texture2D t = Resources.Load<Texture2D>("Skins/logo_0");
            if (t == null) { if (old == null) { if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go); } return null; }
            t.filterMode = FilterMode.Point;
            logo = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.sprite = logo; img.preserveAspect = true; img.raycastTarget = false;
        go.transform.SetAsFirstSibling();
        return go;
    }

    private static Sprite cover;

    private static void RemoveChildren(Transform parent, string childName)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform c = parent.GetChild(i);
            if (c.name != childName) continue;
            if (Application.isPlaying) { c.gameObject.SetActive(false); Object.Destroy(c.gameObject); }
            else Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>
    /// Game cover as the background of a panel (Resources/Skins/cover_0.png). The picture fills the screen height and
    /// is anchored to the LEFT edge, so only the right side is cropped on narrower screens. Does nothing if the file is missing.
    /// </summary>
    public static GameObject AddBackground(Transform parent, float dim = 0f)
    {
        if (parent == null) return null;
        RemoveChildren(parent, "CoverBackground");   // remove every old copy (the menu setup also runs in the editor)
        RemoveChildren(parent, "CoverDim");
        if (cover == null)
        {
            Texture2D t = Resources.Load<Texture2D>("Skins/cover_0");
            if (t == null) return null;
            t.filterMode = FilterMode.Bilinear;
            cover = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        GameObject go = new GameObject("CoverBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0f);
        r.anchorMax = new Vector2(0f, 1f);          // full height, stuck to the left edge
        r.pivot = new Vector2(0f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        Image img = go.GetComponent<Image>();
        img.sprite = cover; img.raycastTarget = false;
        AspectRatioFitter fit = go.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
        fit.aspectRatio = cover.rect.width / cover.rect.height;
        go.transform.SetAsFirstSibling();

        if (dim > 0f)
        {
            GameObject d = new GameObject("CoverDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            d.transform.SetParent(parent, false);
            RectTransform dr = d.GetComponent<RectTransform>();
            dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one; dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
            Image di = d.GetComponent<Image>();
            di.color = new Color(0f, 0f, 0f, dim); di.raycastTarget = false;
            d.transform.SetSiblingIndex(1);
        }
        return go;
    }

    public static Sprite[] Frames(string set)
    {
        if (cache.TryGetValue(set, out Sprite[] cached)) return cached;
        List<Sprite> list = new List<Sprite>();
        for (int i = 0; i < 8; i++)
        {
            Texture2D tex = Resources.Load<Texture2D>("Skins/" + set + "_" + i);
            if (tex == null) break;
            tex.filterMode = FilterMode.Point;
            list.Add(Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f));
        }
        Sprite[] arr = list.ToArray();
        cache[set] = arr;
        if (arr.Length == 0) Debug.LogWarning("[Skins] No sprites found for set '" + set + "' in Resources/Skins");
        return arr;
    }

    /// <summary>Default art set for an enemy prefab/object name.</summary>
    public static string SetForName(string n)
    {
        n = n.ToLowerInvariant();
        if (n.Contains("siege") || n.Contains("breach")) return "wheelbot";
        if (n.Contains("walker") || n.Contains("scrapdrone")) return "zombie";
        if (n.Contains("runner") || n.Contains("mutantcrawler")) return "dog";
        if (n.Contains("ranged") || n.Contains("sentinel")) return "sentinel";
        if (n.Contains("dasher") || n.Contains("kamikaze")) return "disc";
        if (n.Contains("burrower")) return "ghost";
        if (n.Contains("minion")) return "blob";
        if (n.Contains("armoredguard")) return "wheelbot";
        return null;
    }

    /// <summary>Skin a regular enemy by its name (does nothing if it already has one).</summary>
    public static void ApplyEnemy(GameObject go)
    {
        if (go == null || go.GetComponent<SkinAnimator>() != null) return;
        string set = SetForName(go.name);
        if (set != null) Apply(go, set);
    }

    public static SkinAnimator Apply(GameObject go, string set, float sizeOverride = -1f)
    {
        if (go == null) return null;
        Sprite[] frames = Frames(set);
        if (frames == null || frames.Length == 0) return null;

        SkinAnimator anim = go.GetComponent<SkinAnimator>();
        SpriteRenderer root = anim != null ? anim.root : FindRoot(go);
        if (root == null) return null;

        if (anim == null)
        {
            anim = go.AddComponent<SkinAnimator>();
            anim.root = root;
            Vector3 size = root.bounds.size;
            float baseWorld = Mathf.Max(size.x, size.y);
            if (baseWorld < 0.1f) baseWorld = 0.64f;
            anim.baseWorldSize = baseWorld;

            GameObject child = new GameObject("Skin");
            child.transform.SetParent(go.transform, false);
            anim.skin = child.AddComponent<SpriteRenderer>();
            anim.skin.sortingLayerID = root.sortingLayerID;
            anim.skin.sortingOrder = root.sortingOrder;
            anim.skin.sharedMaterial = root.sharedMaterial;
            if (anim.skin.sharedMaterial == null) ProceduralSprites.EnsureVisible(anim.skin, frames[0]);
        }

        SetInfo si = info.TryGetValue(set, out SetInfo found) ? found : new SetInfo(1f, false);
        anim.frames = frames;
        anim.facesLeft = si.facesLeft;
        anim.baseColor = root.color;
        anim.ResetTimer();

        float px = Mathf.Max(frames[0].rect.width, frames[0].rect.height);
        float lossy = Mathf.Abs(go.transform.lossyScale.x);
        if (lossy < 0.0001f) lossy = 1f;
        float s = (anim.baseWorldSize * (sizeOverride > 0f ? sizeOverride : si.size) / (px / 100f)) / lossy;
        anim.skin.transform.localScale = new Vector3(s * si.width, s, 1f);
        anim.baseScale = anim.skin.transform.localScale;
        anim.wobble = frames.Length == 1;
        anim.heavyWalk = false;   // bosses walk like the dog / Vanguard: plain frame swapping while moving
        anim.worldSize = anim.baseWorldSize * (sizeOverride > 0f ? sizeOverride : si.size);
        anim.skin.sprite = frames[0];
        root.enabled = false;
        return anim;
    }

    private static SpriteRenderer FindRoot(GameObject go)
    {
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) return sr;
        foreach (SpriteRenderer c in go.GetComponentsInChildren<SpriteRenderer>(true))
            if (c.gameObject.name != "Skin") return c;
        return null;
    }
}

/// <summary>Flips between the 2-3 frames of a skin while the object moves, and mirrors facing/colour of the hidden root renderer.</summary>
public class SkinAnimator : MonoBehaviour
{
    public SpriteRenderer root;
    public SpriteRenderer skin;
    public Sprite[] frames;
    public bool facesLeft;
    public float baseWorldSize = 1f;
    public Color baseColor = Color.white;
    public float framesPerSecond = 6f;
    public bool heavyWalk;              // bosses: stomping walk cycle (hop, sway, squash, frames swap per step)
    public float worldSize = 1f;        // final on-screen height, used for the hop height
    public bool wobble;                 // single-frame characters (the Brute) rock and bob while walking instead
    public Vector3 baseScale = Vector3.one;
    private float wobbleClock, moveBlend, lastSpeed, stretch, moveHold;

    private Vector3 last;
    private float timer;
    private int index;
    private bool playerFlip;

    public void ResetTimer() { timer = 0f; index = 0; last = transform.position; }
    private void OnEnable() { last = transform.position; }

    private void LateUpdate()
    {
        if (skin == null || root == null) return;

        // colour effects (hurt flash, burrow fade) are written to the hidden root renderer; mirror them as a ratio to its base colour
        Color c = root.color;
        skin.color = new Color(
            baseColor.r > 0.01f ? Mathf.Clamp01(c.r / baseColor.r) : c.r,
            baseColor.g > 0.01f ? Mathf.Clamp01(c.g / baseColor.g) : c.g,
            baseColor.b > 0.01f ? Mathf.Clamp01(c.b / baseColor.b) : c.b,
            baseColor.a > 0.01f ? Mathf.Clamp01(c.a / baseColor.a) : c.a);

        float dt = Time.deltaTime;
        Vector3 p = transform.position;
        Vector3 d = p - last;
        last = p;
        if (dt <= 0f) return;
        float speed = d.magnitude / dt;
        // physics-driven characters (the player) only change position on fixed steps, so some frames have d == 0: hold the 'moving' state briefly
        if (speed > 0.15f) moveHold = 0.12f; else moveHold -= dt;
        bool moving = moveHold > 0f;

        // ---- procedural motion for EVERY character (player, bosses, enemies) ----
        bool isPlayer = gameObject.CompareTag("Player");
        moveBlend = Mathf.MoveTowards(moveBlend, moving ? 1f : 0f, dt * 7f);
        float accel = (speed - lastSpeed) / dt;
        lastSpeed = speed;
        stretch = 0f;   // stretch when starting, squash when stopping

        float stepRate = heavyWalk ? Mathf.Clamp(speed * 2.4f, 4f, 10f) : Mathf.Clamp(speed * 3.2f, 7f, 15f);
        wobbleClock += dt * Mathf.Lerp(heavyWalk ? 1.6f : 2.2f, stepRate, moveBlend);
        float sn = Mathf.Sin(wobbleClock);
        float hop = Mathf.Abs(sn);
        float land = 1f - hop;
        float lossyW = Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x));

        float hopH = worldSize * (heavyWalk ? 0.07f : wobble ? 0.05f : 0f) / lossyW;
        float tiltDeg = heavyWalk ? 5.5f : wobble ? 3.5f : 0f;
        float leanDeg = 0f;
        float squash = heavyWalk ? 0.05f : wobble ? 0.02f : 0f;
        float breathe = heavyWalk ? 0.015f : wobble ? 0.012f : 0f;

        float lean = moving ? -Mathf.Clamp(d.x / dt / 4f, -1f, 1f) * leanDeg : 0f;
        float idleSway = Mathf.Sin(wobbleClock * 0.6f) * (heavyWalk ? 0.6f : 0f) * (1f - moveBlend);

        Vector3 pos = new Vector3(0f, hop * hopH * moveBlend, 0f);
        float rot = sn * tiltDeg * moveBlend + lean + idleSway;
        float sx = 1f + squash * 0.7f * land * moveBlend - stretch * 0.5f + Mathf.Sin(wobbleClock) * 0.0f;
        float sy = 1f - squash * land * moveBlend + stretch + Mathf.Sin(wobbleClock) * breathe * (1f - moveBlend);

        Transform st = skin.transform;
        st.localPosition = pos;
        st.localRotation = Quaternion.Euler(0f, 0f, rot);
        st.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, 1f);

        if (frames != null && frames.Length > 1)
        {
            if (heavyWalk) skin.sprite = moving ? frames[((int)(wobbleClock / Mathf.PI)) % frames.Length] : frames[0];
            else if (moving)
            {
                timer += dt * framesPerSecond * Mathf.Clamp(speed / 2.5f, 0.7f, 2.2f);   // same frame flipping for every character (player included)
                index = (int)timer % frames.Length;
                skin.sprite = frames[index];
            }
            else { timer = 0f; index = 0; skin.sprite = frames[0]; }
        }

        // facing: enemies flip the root renderer themselves; the player has none, so use the aim / movement direction
        bool flip;
        if (isPlayer)
        {
            // face the A / D key being pressed (independent of the mouse aim); keep the last side when idle
            float hx = Input.GetAxisRaw("Horizontal");
            if (hx > 0.1f) playerFlip = false;
            else if (hx < -0.1f) playerFlip = true;
            flip = playerFlip;
        }
        else flip = root.flipX;
        skin.flipX = flip ^ facesLeft;
    }
}
