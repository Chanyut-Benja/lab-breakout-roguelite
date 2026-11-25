using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

/// <summary>Puts the Pixeloid font on every text in the game (uGUI Text, TextMeshPro, world TextMesh). Fonts live in Resources/Fonts.</summary>
public class PixelFontAuto : MonoBehaviour
{
    private Font regular;
    private TMP_FontAsset tmpRegular;
    private float nextScan;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        GameObject go = new GameObject("PixelFontAuto");
        DontDestroyOnLoad(go);
        go.AddComponent<PixelFontAuto>();
    }

    private bool tmpFailed;

    private TMP_FontAsset MakeTmp(Font f)
    {
        if (f == null || tmpFailed) return null;
        try
        {
            TMP_FontAsset a = TMP_FontAsset.CreateFontAsset(f, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (a == null) { tmpFailed = true; return null; }
            TMP_FontAsset def = TMP_Settings.instance != null ? TMP_Settings.defaultFontAsset : null;
            if (def != null && def != a)
            {
                if (a.fallbackFontAssetTable == null) a.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
                a.fallbackFontAssetTable.Add(def);   // glyphs the pixel font lacks (e.g. Thai)
            }
            return a;
        }
        catch (System.Exception e) { tmpFailed = true; Debug.LogWarning("[PixelFont] TMP font asset failed: " + e.Message); return null; }
    }

    private static PixelFontAuto inst;

    private void Awake() { inst = this; }

    /// <summary>Restyle every text right now (call right after opening a screen so the old font never shows).</summary>
    public static void ApplyNow() { if (inst != null) inst.Scan(); }

    private void Update()
    {
        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + 0.35f;
        Scan();
    }

    private void Scan()
    {

        if (regular == null) regular = Resources.Load<Font>("Fonts/PixeloidSans");
        if (regular == null) return;
        if (tmpRegular == null) tmpRegular = MakeTmp(regular);

        foreach (Text t in FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font != regular) t.font = regular;
        }

        foreach (TMP_Text t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (tmpRegular != null && t.font != tmpRegular) t.font = tmpRegular;
        }

        foreach (TextMesh t in FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.font == regular) continue;
            t.font = regular;
            MeshRenderer mr = t.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = regular.material;
        }
    }
}
