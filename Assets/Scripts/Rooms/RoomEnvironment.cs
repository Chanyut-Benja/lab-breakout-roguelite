using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Loads the separate hand-drawn room maps (Assets/Resources/Rooms/Room_01.png ... Room_18.png).
///
/// Every room is its own picture (floor, walls, doorways and props are painted into the PNG), all with the
/// same size: 48 x 32 world units (768 x 512 px at 16 px per unit). The scene's old LabFloor_World is hidden
/// and the scene's existing "MapBoundaries" walls are re-shaped to the room size, so the camera clamp, the enemy
/// spawn clamp and the "map limit" notice keep working unchanged.
///
/// Layout shared by all rooms (world units, relative to the room centre):
///   - player enters at the bottom (0, -9.5); the sealed entrance is painted in the bottom wall
///   - two reward doorways in the top wall; the reward doors stand at (-8, 12.5) and (+8, 12.5)
///   - walls are 2 units thick on every side (painted + collider)
/// To change a room, repaint its PNG (keep the same size and keep the doorways where they are).
/// </summary>
public class RoomEnvironment : MonoBehaviour
{
    public const float RoomWidth = 48f;
    public const float RoomHeight = 32f;
    public const float WallThickness = 2f;
    public const float DoorY = 12.5f;
    public const float DoorX = 8f;

    public static RoomEnvironment Instance { get; private set; }

    public static RoomEnvironment Ensure()
    {
        if (Instance != null) return Instance;
        RoomEnvironment existing = FindFirstObjectByType<RoomEnvironment>();
        if (existing != null) { Instance = existing; return existing; }
        GameObject go = new GameObject("RoomEnvironment");
        Instance = go.AddComponent<RoomEnvironment>();
        return Instance;
    }

    // ---------------------------------------------------------------- public info
    public int LoadedRoom { get; private set; }
    public Vector3 Center => center;
    public Vector3 PlayerSpawn => (layout != null && layout.playerSpawn != null) ? layout.playerSpawn.position : center + new Vector3(0f, -9.5f, 0f);
    public Bounds ArenaBounds => new Bounds(center, new Vector3(roomSize.x, roomSize.y, 1f));

    public Vector3 GetDoorSlot(int index)
    {
        index = Mathf.Clamp(index, 0, 1);
        if (layout != null && layout.doorSlots != null && index < layout.doorSlots.Length && layout.doorSlots[index] != null)
            return layout.doorSlots[index].position;
        return center + new Vector3(index == 0 ? -DoorX : DoorX, DoorY, 0f);
    }

    // ---------------------------------------------------------------- state
    private Transform map;
    private RoomLayout layout;          // set when the room comes from a real prefab (Resources/RoomPrefabs)
    private GameObject roomInstance;
    private Vector2 roomSize = new Vector2(RoomWidth, RoomHeight);
    private float wallT = WallThickness;
    private Vector3 center;
    private bool centerKnown;
    private bool oldFloorHidden;

    private SpriteRenderer floorRenderer;
    private SpriteRenderer overlay;
    private Sprite pixel;
    private readonly Dictionary<int, Sprite> spriteCache = new Dictionary<int, Sprite>();

    private Image fadeImage;
    private Coroutine fadeRoutine;

    // light overlay effect (flicker / pulse / flash) per room
    private int fxMode;
    private Color fxColor;
    private float fxTimer, fxFlash;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) { Destroy(gameObject); }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------------------------------------------------------- themes (fx + accent colour only; the look itself is in the PNGs)
    // fx: 0 none, 1 soft flicker, 2 rhythm, 3 red pulse, 4 lightning, 5 mist, 6 glitch
    public static readonly int[] Fx = { 0, 1, 0, 1, 2, 0, 5, 0, 5, 1, 3, 0, 1, 6, 4, 3, 3, 3 };
    public static readonly Color[] Accent =
    {
        new Color(.27f, .82f, 1f), new Color(.47f, .84f, 1f), new Color(1f, .80f, .16f),
        new Color(.39f, .59f, 1f), new Color(.35f, .92f, 1f), new Color(.24f, .92f, 1f),
        new Color(.43f, 1f, .63f), new Color(.47f, 1f, .70f), new Color(.39f, 1f, .51f),
        new Color(1f, .55f, .20f), new Color(1f, .59f, .12f), new Color(.90f, .63f, .35f),
        new Color(.31f, .63f, 1f), new Color(1f, .27f, .31f), new Color(.75f, .43f, 1f),
        new Color(.86f, .31f, .47f), new Color(1f, .20f, .31f), new Color(1f, .77f, .24f)
    };

    // ---------------------------------------------------------------- doorways (top wall)
    // locked: dark shutter with a red bar. open: glowing in the reward colour + a name plate on the floor.
    private readonly SpriteRenderer[] lockShade = new SpriteRenderer[2];
    private readonly SpriteRenderer[] lockBar = new SpriteRenderer[2];
    private readonly SpriteRenderer[] glow = new SpriteRenderer[2];
    private readonly SpriteRenderer[] glowLight = new SpriteRenderer[2];
    private readonly SpriteRenderer[] plate = new SpriteRenderer[2];
    private readonly bool[] doorOpen = new bool[2];
    private readonly Color[] doorColor = new Color[2];
    private Sprite softCircle;

    private void BuildDoorways()
    {
        if (glow[0] != null) return;
        FindSceneObjects();   // makes sure the room centre is known before the doorways are placed
        for (int i = 0; i < 2; i++)
        {
            float x = i == 0 ? -DoorX : DoorX;
            Transform root = new GameObject("Doorway_" + i).transform;
            root.SetParent(transform, false);
            root.position = center + new Vector3(x, RoomHeight * 0.5f - 1f, 0f);

            lockShade[i] = MakePart(root, "Shutter", 0f, 0f, 4.6f, 2f, GetPixel(), new Color(0.05f, 0.06f, 0.08f, 0.94f), -88);
            lockBar[i] = MakePart(root, "ShutterBar", 0f, -0.75f, 4.6f, 0.16f, GetPixel(), new Color(1f, 0.25f, 0.2f, 0.9f), -87);
            glow[i] = MakePart(root, "Glow", 0f, 0f, 4.2f, 1.8f, GetPixel(), Color.white, -87);
            glowLight[i] = MakePart(root, "Light", 0f, -2.2f, 9f, 6f, GetSoftCircle(), Color.white, -89);
            plate[i] = MakePart(root, "Plate", 0f, -3.9f, 7.4f, 1.2f, GetPixel(), new Color(0.03f, 0.04f, 0.06f, 0.78f), -60);
        }
    }

    private SpriteRenderer MakePart(Transform parent, string partName, float x, float y, float w, float h, Sprite sprite, Color c, int order)
    {
        GameObject g = new GameObject(partName);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = new Vector3(x, y, 0f);
        g.transform.localScale = new Vector3(w, h, 1f);
        SpriteRenderer sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = c;
        sr.sortingOrder = order;
        return sr;
    }

    private Sprite GetSoftCircle()
    {
        if (softCircle != null) return softCircle;
        const int n = 64;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;
        float r = n * 0.5f;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }
        t.Apply();
        softCircle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return softCircle;
    }

    /// <summary>All doorways sealed (room in progress).</summary>
    public void LockDoorways()
    {
        if (layout != null)
        {
            for (int i = 0; i < 2; i++) { doorOpen[i] = false; if (layout.doorways != null && layout.doorways[i] != null) layout.doorways[i].SetLocked(); }
            return;
        }
        BuildDoorways();
        for (int i = 0; i < 2; i++)
        {
            doorOpen[i] = false;
            lockShade[i].enabled = true;
            lockBar[i].enabled = true;
            glow[i].enabled = false;
            glowLight[i].enabled = false;
            plate[i].enabled = false;
        }
    }

    /// <summary>Open one doorway: it glows in the reward colour so the player knows it is ready to enter.</summary>
    public void OpenDoorway(int index, Color color)
    {
        index = Mathf.Clamp(index, 0, 1);
        if (layout != null)
        {
            if (layout.doorways != null && layout.doorways[index] != null) layout.doorways[index].SetOpen(color);
            return;
        }
        BuildDoorways();
        doorOpen[index] = true;
        doorColor[index] = color;
        lockShade[index].enabled = false;
        lockBar[index].enabled = false;
        glow[index].enabled = true;
        glowLight[index].enabled = true;
        plate[index].enabled = true;
    }

    // ---------------------------------------------------------------- loading
    public void LoadRoom(int room)
    {
        room = Mathf.Clamp(room, 1, 18);
        LoadedRoom = room;

        FindSceneObjects();

        // a real room prefab (editable in Unity) wins over the plain picture
        if (roomInstance != null) { roomInstance.SetActive(false); Destroy(roomInstance); roomInstance = null; }
        layout = null;
        GameObject prefab = Resources.Load<GameObject>("RoomPrefabs/Room_" + room.ToString("00"));
        if (prefab != null)
        {
            roomInstance = Instantiate(prefab, center, Quaternion.identity, transform);
            roomInstance.name = prefab.name;
            layout = roomInstance.GetComponent<RoomLayout>();
            if (layout == null) { Destroy(roomInstance); roomInstance = null; }
        }

        if (layout != null)
        {
            roomSize = layout.size;
            wallT = layout.wallThickness;
            ShapeWalls();
            if (floorRenderer != null) floorRenderer.enabled = false;
            if (overlay != null && overlay.gameObject.name == "RoomLight") overlay.enabled = false;
            overlay = layout.lightOverlay;
            fxMode = layout.fxMode;
            fxColor = layout.accent;
            fxTimer = 0f;
            fxFlash = 0f;
            if (overlay != null) overlay.color = new Color(fxColor.r, fxColor.g, fxColor.b, 0f);
            LockDoorways();
            StartFade();
            return;
        }

        roomSize = new Vector2(RoomWidth, RoomHeight);
        wallT = WallThickness;
        ShapeWalls();
        if (floorRenderer != null) floorRenderer.enabled = true;

        // floor picture
        Sprite s = GetRoomSprite(room);
        if (floorRenderer == null)
        {
            GameObject f = new GameObject("RoomFloor");
            f.transform.SetParent(transform, false);
            floorRenderer = f.AddComponent<SpriteRenderer>();
            floorRenderer.sortingOrder = -100;
        }
        floorRenderer.sprite = s;
        floorRenderer.color = Color.white;
        floorRenderer.transform.position = center;
        float sx = s.bounds.size.x > 0.01f ? RoomWidth / s.bounds.size.x : 1f;
        float sy = s.bounds.size.y > 0.01f ? RoomHeight / s.bounds.size.y : 1f;
        floorRenderer.transform.localScale = new Vector3(sx, sy, 1f);

        // light overlay
        if (overlay == null)
        {
            GameObject o = new GameObject("RoomLight");
            o.transform.SetParent(transform, false);
            overlay = o.AddComponent<SpriteRenderer>();
            overlay.sprite = GetPixel();
            overlay.sortingOrder = -30;
        }
        overlay.enabled = true;
        overlay.transform.position = center;
        overlay.transform.localScale = new Vector3(RoomWidth, RoomHeight, 1f);
        fxMode = Fx[room - 1];
        fxColor = Accent[room - 1];
        fxTimer = 0f;
        fxFlash = 0f;
        overlay.color = new Color(fxColor.r, fxColor.g, fxColor.b, 0f);

        LockDoorways();
        StartFade();
    }

    private void FindSceneObjects()
    {
        if (map == null)
        {
            GameObject m = GameObject.Find("MapBoundaries");
            if (m == null)
            {
                m = new GameObject("MapBoundaries");
                m.transform.position = Vector3.zero;
            }
            map = m.transform;
        }

        if (!centerKnown)
        {
            center = map.position;
            centerKnown = true;
        }

        if (!oldFloorHidden)
        {
            GameObject oldFloor = GameObject.Find("LabFloor_World");
            if (oldFloor != null) oldFloor.SetActive(false);
            oldFloorHidden = true;
        }

        // the old thin cyan strips created by RoomManager are not needed: the walls are painted into the map
        for (int i = map.childCount - 1; i >= 0; i--)
        {
            Transform wall = map.GetChild(i);
            for (int c = wall.childCount - 1; c >= 0; c--)
            {
                if (wall.GetChild(c).name.StartsWith("Visible_")) Destroy(wall.GetChild(c).gameObject);
            }
        }
    }

    private void ShapeWalls()
    {
        float W = roomSize.x, H = roomSize.y;
        float hw = W * 0.5f, hh = H * 0.5f, t = wallT;
        SetWall("Wall_Top", new Vector2(0f, hh - t * 0.5f), new Vector2(W, t));
        SetWall("Wall_Bottom", new Vector2(0f, -hh + t * 0.5f), new Vector2(W, t));
        SetWall("Wall_Left", new Vector2(-hw + t * 0.5f, 0f), new Vector2(t, H));
        SetWall("Wall_Right", new Vector2(hw - t * 0.5f, 0f), new Vector2(t, H));
    }

    private void SetWall(string wallName, Vector2 offset, Vector2 size)
    {
        Transform w = map.Find(wallName);
        if (w == null)
        {
            GameObject go = new GameObject(wallName);
            go.transform.SetParent(map, false);
            w = go.transform;
        }
        w.localPosition = Vector3.zero;
        w.localRotation = Quaternion.identity;
        w.localScale = Vector3.one;
        BoxCollider2D box = w.GetComponent<BoxCollider2D>();
        if (box == null) box = w.gameObject.AddComponent<BoxCollider2D>();
        box.isTrigger = false;
        box.offset = offset;
        box.size = size;
    }

    private Sprite GetRoomSprite(int room)
    {
        Sprite cached;
        if (spriteCache.TryGetValue(room, out cached) && cached != null) return cached;

        string key = "Rooms/Room_" + room.ToString("00");
        Sprite s = Resources.Load<Sprite>(key);
        if (s == null)
        {
            Texture2D tex = Resources.Load<Texture2D>(key);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width / RoomWidth);
            }
        }
        else if (s.texture != null)
        {
            s.texture.filterMode = FilterMode.Point;
        }

        if (s == null)
        {
            Debug.LogWarning("[RoomEnvironment] Missing map image Resources/" + key + ".png - using a plain floor.");
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, new Color(0.18f, 0.22f, 0.30f, 1f));
            t.Apply();
            s = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f / RoomWidth);
        }

        spriteCache[room] = s;
        return s;
    }

    private Sprite GetPixel()
    {
        if (pixel != null) return pixel;
        Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        t.SetPixel(0, 0, Color.white);
        t.Apply();
        pixel = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        return pixel;
    }

    // ---------------------------------------------------------------- fade between rooms
    private void StartFade()
    {
        if (fadeImage == null)
        {
            GameObject canvasGo = new GameObject("RoomFade");
            canvasGo.transform.SetParent(transform, false);
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 4000;

            GameObject imgGo = new GameObject("Black");
            imgGo.transform.SetParent(canvasGo.transform, false);
            fadeImage = imgGo.AddComponent<Image>();
            fadeImage.color = Color.black;
            fadeImage.raycastTarget = false;
            RectTransform rt = fadeImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeIn());
    }

    private IEnumerator FadeIn()
    {
        const float hold = 0.12f, duration = 0.45f;
        fadeImage.color = Color.black;
        float t = 0f;
        while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }
        t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, 0f);
        fadeRoutine = null;
    }

    // ---------------------------------------------------------------- ambient light
    private void Update()
    {
        // pulse the glow of any open doorway
        for (int i = 0; i < 2; i++)
        {
            if (!doorOpen[i] || glow[i] == null) continue;
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f + i);
            Color dc = doorColor[i];
            glow[i].color = new Color(Mathf.Lerp(dc.r, 1f, 0.25f), Mathf.Lerp(dc.g, 1f, 0.25f), Mathf.Lerp(dc.b, 1f, 0.25f), 0.60f + 0.30f * k);
            glowLight[i].color = new Color(dc.r, dc.g, dc.b, 0.22f + 0.14f * k);
        }

        if (overlay == null) return;

        float time = Time.time;
        float a = 0f;
        switch (fxMode)
        {
            case 1: a = Mathf.Max(0f, Mathf.PerlinNoise(time * 3f, LoadedRoom * 1.7f) - 0.68f) * 0.35f; break;
            case 2: a = Mathf.Repeat(time, 1.3f) < 0.14f ? 0.12f : 0f; break;
            case 3: a = 0.03f + 0.06f * (0.5f + 0.5f * Mathf.Sin(time * 1.6f)); break;
            case 4:
                fxTimer -= Time.deltaTime;
                if (fxTimer <= 0f) { fxFlash = 0.30f; fxTimer = Random.Range(1.4f, 4.2f); }
                fxFlash = Mathf.MoveTowards(fxFlash, 0f, Time.deltaTime * 1.4f);
                a = fxFlash;
                break;
            case 5: a = 0.06f + 0.02f * Mathf.Sin(time * 0.8f); break;
            case 6:
                fxTimer -= Time.deltaTime;
                if (fxTimer <= 0f) { fxFlash = Random.value < 0.45f ? 0.12f : 0f; fxTimer = Random.Range(0.08f, 0.4f); }
                a = fxFlash;
                break;
        }
        Color c = fxMode == 4 ? Color.Lerp(fxColor, Color.white, 0.5f) : fxColor;
        c.a = a;
        overlay.color = c;
    }
}
