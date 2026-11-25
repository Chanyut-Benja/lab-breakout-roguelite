using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Gives every big plain-colour panel (menus, cards, info boxes) a chunky pixel-art frame so it matches the pixel buttons.
/// The frame sprite is drawn in code (dark outline, light top/left edge, dark bottom/right edge, stepped corners) and tinted with the panel's own colour.
/// </summary>
public class PixelPanelAuto : MonoBehaviour
{
    private const float PixelSize = 3f;   // canvas units per sprite pixel
    private static Sprite sprite;
    private static readonly string[] SkipNames = { "Fill", "Bar", "Dim", "Overlay", "Cover", "Fade", "Vignette", "Track", "Handle", "Icon", "Portrait", "Slot", "Row", "Background" };
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("PixelPanelAuto");
        DontDestroyOnLoad(go);
        go.AddComponent<PixelPanelAuto>();
    }

    public static Sprite GetSprite()
    {
        if (sprite != null) return sprite;
        const int n = 10;
        Texture2D t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Point;
        t.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int dx = Mathf.Min(x, n - 1 - x), dy = Mathf.Min(y, n - 1 - y);
                Color c;
                if (dx == 0 && dy == 0) c = Color.clear;                                   // stepped corner
                else if (dx == 0 || dy == 0 || (dx == 1 && dy == 1)) c = new Color(0.05f, 0.04f, 0.07f, 1f);   // outline
                else if (y == n - 2 || x == 1) c = new Color(1f, 1f, 1f, 1f);               // light top / left edge
                else if (y == 1 || x == n - 2) c = new Color(0.62f, 0.62f, 0.66f, 1f);      // dark bottom / right edge
                else c = new Color(0.92f, 0.92f, 0.92f, 1f);                                // face
                t.SetPixel(x, y, c);
            }
        }
        t.Apply();
        sprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(3, 3, 3, 3));
        return sprite;
    }

    private static PixelPanelAuto inst;

    private void Awake() { inst = this; }

    /// <summary>Frame every panel right now (call right after opening a screen).</summary>
    public static void ApplyNow() { if (inst != null) inst.Scan(); }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.4f;
        Scan();
    }

    private void Scan()
    {

        Sprite s = GetSprite();
        foreach (Image img in FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (img == null || img.sprite != null || img.color.a < 0.6f) continue;
            if (img.GetComponent<PixelPanelMark>() != null) continue;
            if (!Eligible(img)) continue;

            img.sprite = s;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = 1f / PixelSize;
            img.gameObject.AddComponent<PixelPanelMark>();
            Outline[] outlines = img.GetComponents<Outline>();      // the old glowing sci-fi outline goes away (Shadow is its base class)
            for (int i = 0; i < outlines.Length; i++) outlines[i].enabled = false;
        }
    }

    private static bool Eligible(Image img)
    {
        if (img.GetComponentInParent<Selectable>() != null) return false;   // buttons, sliders, toggles
        if (img.GetComponent<Mask>() != null || img.GetComponent<RectMask2D>() != null) return false;
        string n = img.name;
        for (int i = 0; i < SkipNames.Length; i++) if (n.Contains(SkipNames[i])) return false;

        RectTransform rt = img.rectTransform;
        Canvas canvas = img.canvas != null ? img.canvas : img.GetComponentInParent<Canvas>(true);
        if (canvas != null) canvas = canvas.rootCanvas;
        float k = canvas != null ? canvas.scaleFactor : 1f;
        float w = rt.rect.width * k, h = rt.rect.height * k;
        if (rt.rect.width < 140f || rt.rect.height < 50f) return false;
        if (w >= Screen.width * 0.95f && h >= Screen.height * 0.95f) return false;   // full-screen dim / backdrop
        return true;
    }
}
