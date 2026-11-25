using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Loads a scene asynchronously behind a fade + loading bar (Scene Management requirement:
/// several scenes with a transition / loading screen). The overlay is built at runtime and survives the scene change.
/// </summary>
public class SceneLoader : MonoBehaviour
{
    private static SceneLoader instance;
    private CanvasGroup group;
    private Image bar;
    private Text label;
    private bool busy;

    public static void Load(string sceneName)
    {
        Time.timeScale = 1f;
        if (instance == null)
        {
            GameObject go = new GameObject("SceneLoader");
            instance = go.AddComponent<SceneLoader>();
            DontDestroyOnLoad(go);
            instance.Setup();
        }
        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(sceneName));
    }

    // Uses the prefab Resources/UI/LoadingScreen (made by Tools > Build UI Panels) if it exists, otherwise builds in code.
    private void Setup()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/LoadingScreen");
        GameObject root;
        if (prefab != null)
        {
            root = Instantiate(prefab, transform);
            root.name = "LoadingCanvas";
        }
        else root = Build(transform);
        Bind(root);
    }

    // Finds the parts of the loading screen by name.
    private void Bind(GameObject root)
    {
        group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        label = root.transform.Find("Label").GetComponent<Text>();
        bar = root.transform.Find("BarFrame/BarFill").GetComponent<Image>();
        if (bar.sprite == null)
        {
            Texture2D tex = new Texture2D(2, 2);
            tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
            tex.Apply();
            bar.sprite = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
        }
        bar.type = Image.Type.Filled;
        bar.fillMethod = Image.FillMethod.Horizontal;
        bar.fillAmount = 0f;
    }

#if UNITY_EDITOR
    /// <summary>Editor only: builds the loading screen hierarchy so it can be saved as a prefab.</summary>
    public static GameObject CreateForEditor()
    {
        var host = new GameObject("SceneLoader");
        Build(host.transform);
        return host;
    }
#endif

    private static GameObject Build(Transform parent)
    {
        GameObject root = new GameObject("LoadingCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(parent, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        CanvasGroup g = root.GetComponent<CanvasGroup>();
        g.alpha = 0f;
        g.blocksRaycasts = false;
        Transform transform = root.transform;

        GameObject bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        RectTransform brt = bg.GetComponent<RectTransform>();
        brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.02f, 0.03f, 0.06f, 1f);

        GameObject tgo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        tgo.transform.SetParent(transform, false);
        RectTransform trt = tgo.GetComponent<RectTransform>();
        trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.sizeDelta = new Vector2(600f, 60f);
        trt.anchoredPosition = new Vector2(0f, 30f);
        Text lbl = tgo.GetComponent<Text>();
        lbl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        lbl.fontSize = 34;
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.color = new Color(0.6f, 0.9f, 1f, 1f);
        lbl.text = "LOADING...";

        GameObject frame = new GameObject("BarFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(transform, false);
        RectTransform frt = frame.GetComponent<RectTransform>();
        frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
        frt.sizeDelta = new Vector2(420f, 14f);
        frt.anchoredPosition = new Vector2(0f, -20f);
        frame.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);

        GameObject fill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(frame.transform, false);
        RectTransform rt = fill.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.35f, 0.9f, 1f, 1f);
        return root;
    }

    private IEnumerator Run(string sceneName)
    {
        busy = true;
        group.blocksRaycasts = true;
        bar.fillAmount = 0f;

        // fade to black (unscaled time: works while paused)
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            group.alpha = t / 0.35f;
            yield return null;
        }
        group.alpha = 1f;

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = false;
        float shown = 0f;
        float minTime = 0.6f, elapsed = 0f;
        while (op.progress < 0.9f || elapsed < minTime)
        {
            elapsed += Time.unscaledDeltaTime;
            shown = Mathf.MoveTowards(shown, Mathf.Clamp01(op.progress / 0.9f), Time.unscaledDeltaTime * 1.5f);
            bar.fillAmount = shown;
            yield return null;
        }
        bar.fillAmount = 1f;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;

        Time.timeScale = 1f;
        for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
        {
            group.alpha = 1f - t / 0.35f;
            yield return null;
        }
        group.alpha = 0f;
        group.blocksRaycasts = false;
        busy = false;
    }
}
