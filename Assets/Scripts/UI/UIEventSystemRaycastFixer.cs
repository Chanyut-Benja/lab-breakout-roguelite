using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// UI EventSystem & Raycast Target Fixer
/// 1. Ensures each Scene has an active EventSystem with a functional StandaloneInputModule (auto-created if missing).
/// 2. Ensures every Canvas in the Scene has an enabled GraphicRaycaster.
/// 3. Automatically sets raycastTarget = false on all full-screen Panels, Backgrounds, Overlays, and non-interactive Text/Icons
///    so that buttons and interactive elements below are never blocked from receiving mouse clicks.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class UIEventSystemRaycastFixer : MonoBehaviour
{
    private static UIEventSystemRaycastFixer instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitBeforeSceneLoad()
    {
        EnsurePersistentInstance();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void InitAfterSceneLoad()
    {
        EnsurePersistentInstance();
        ApplyAllFixes();
    }

    private static void EnsurePersistentInstance()
    {
        if (instance == null)
        {
            GameObject fixerGo = new GameObject("[UIEventSystemRaycastFixer]");
            instance = fixerGo.AddComponent<UIEventSystemRaycastFixer>();
            DontDestroyOnLoad(fixerGo);
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAllFixes();
    }

    private void Start()
    {
        ApplyAllFixes();
    }

    private float nextPeriodicScanTime = 0f;

    private void Update()
    {
        // Periodic lightweight check (every 1s) to catch newly activated dynamic panels (LevelUp, Shop, etc.)
        if (Time.unscaledTime >= nextPeriodicScanTime)
        {
            nextPeriodicScanTime = Time.unscaledTime + 1.0f;
            EnsureEventSystemWithInputModule();
            EnsureGraphicRaycasters();
            FixRaycastBlockers();
        }
    }

    public static void ApplyAllFixes()
    {
        EnsureEventSystemWithInputModule();
        EnsureGraphicRaycasters();
        FixRaycastBlockers();
    }

    #region 1. EventSystem & Input Module

    /// <summary>
    /// ตรวจสอบว่าใน Scene มี EventSystem พร้อม Input Module ที่ถูกต้องหรือไม่ ถ้าไม่มีให้สร้างขึ้นมาอัตโนมัติ
    /// </summary>
    public static void EnsureEventSystemWithInputModule()
    {
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            es = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
        }

        if (es == null)
        {
            GameObject esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
            esObj.AddComponent<StandaloneInputModule>();
            Debug.Log("[UIEventSystemRaycastFixer] Auto-created missing EventSystem with StandaloneInputModule.");
        }
        else
        {
            if (!es.gameObject.activeSelf)
            {
                es.gameObject.SetActive(true);
            }
            if (!es.enabled)
            {
                es.enabled = true;
            }

            // ตรวจสอบ Input Module
            BaseInputModule currentModule = es.GetComponent<BaseInputModule>();
            StandaloneInputModule standalone = es.GetComponent<StandaloneInputModule>();

            // ตรวจสอบว่าเป็น InputSystemUIInputModule ที่ ActionsAsset เป็น null หรือไม่
            MonoBehaviour[] allModules = es.GetComponents<MonoBehaviour>();
            for (int i = 0; i < allModules.Length; i++)
            {
                MonoBehaviour m = allModules[i];
                if (m == null) continue;

                string typeName = m.GetType().Name;
                if (typeName == "InputSystemUIInputModule")
                {
                    // ถ้าใช้ InputSystemUIInputModule แต่ไม่มี StandaloneInputModule หรือ actions เป็น null ให้ปิดแล้วใส่ StandaloneInputModule
                    m.enabled = false;
                }
            }

            if (standalone == null)
            {
                standalone = es.gameObject.AddComponent<StandaloneInputModule>();
                Debug.Log("[UIEventSystemRaycastFixer] Added StandaloneInputModule to existing EventSystem.");
            }

            if (!standalone.enabled)
            {
                standalone.enabled = true;
            }
        }
    }

    #endregion

    #region 2. GraphicRaycaster on All Canvases

    /// <summary>
    /// ตรวจสอบ Canvas ทุกตัวในฉากให้มี GraphicRaycaster ติดตั้งและเปิดใช้งานอยู่เสมอ
    /// </summary>
    public static void EnsureGraphicRaycasters()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas c = canvases[i];
            if (c == null) continue;

            GraphicRaycaster raycaster = c.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = c.gameObject.AddComponent<GraphicRaycaster>();
                Debug.Log($"[UIEventSystemRaycastFixer] Attached missing GraphicRaycaster to Canvas: '{c.name}'.");
            }

            if (!raycaster.enabled)
            {
                raycaster.enabled = true;
            }
        }
    }

    #endregion

    #region 3. Disable raycastTarget on Non-Interactive Panels/Backgrounds/Texts

    /// <summary>
    /// ตรวจสอบ UI Image ที่เป็น Background หรือ Panel กว้างเต็มจอ รวมถึง Text บนปุ่ม ให้เซต raycastTarget = false เพื่อไม่ให้บังการคลิก
    /// </summary>
    public static void FixRaycastBlockers()
    {
        Graphic[] allGraphics = Object.FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allGraphics.Length; i++)
        {
            Graphic g = allGraphics[i];
            if (g == null) continue;

            GameObject go = g.gameObject;

            // 1. ถ้า GameObject มี Selectable component (Button, Toggle, Slider, Scrollbar, InputField) -> ต้องรับคลิกได้
            Selectable selectable = go.GetComponent<Selectable>();
            if (selectable != null)
            {
                g.raycastTarget = true;
                continue;
            }

            // 2. ถ้าเป็น Text/TextMeshPro ทั้งหมด -> ควรปิด raycastTarget เสมอ เพื่อไม่ให้ตัวหนังสือบล็อกปุ่มที่อยู่ด้านหลัง
            if (g is Text)
            {
                if (g.raycastTarget)
                {
                    g.raycastTarget = false;
                }
                continue;
            }

            // 3. ถ้าเป็น Image / RawImage
            if (g is Image || g is RawImage)
            {
                string nameLower = go.name.ToLowerInvariant();

                // กฎตรวจสอบ Panel / Background / Container / Overlay / Border / Slot
                bool isPotentialBlocker =
                    nameLower.Contains("panel") ||
                    nameLower.Contains("background") ||
                    nameLower.Contains("bg") ||
                    nameLower.Contains("overlay") ||
                    nameLower.Contains("root") ||
                    nameLower.Contains("container") ||
                    nameLower.Contains("border") ||
                    nameLower.Contains("frame") ||
                    nameLower.Contains("fill") ||
                    nameLower.Contains("slot") ||
                    nameLower.Contains("icon") ||
                    nameLower.Contains("image_") ||
                    nameLower.Contains("backdrop");

                // ถ้าขนาดกว้างหรือสูงมาก (เป็นพาเนลใหญ่หรือครอบเต็มจอ) และไม่มีปุ่ม
                RectTransform rt = g.rectTransform;
                if (rt != null)
                {
                    Vector2 size = rt.rect.size;
                    if (size.x >= 400f && size.y >= 300f)
                    {
                        isPotentialBlocker = true;
                    }
                }

                if (isPotentialBlocker)
                {
                    if (g.raycastTarget)
                    {
                        g.raycastTarget = false;
                    }
                }
            }
        }
    }

    #endregion

    #region Editor Menu

#if UNITY_EDITOR
    [MenuItem("Tools/UI/Fix All EventSystems, Raycasters and RaycastTargets")]
    public static void EditorFixAllScenes()
    {
        string[] scenePaths = new string[]
        {
            "Assets/Scenes/MainMenuScene.unity",
            "Assets/Scenes/GameScene.unity"
        };

        foreach (string path in scenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EnsureEventSystemWithInputModule();
            EnsureGraphicRaycasters();
            FixRaycastBlockers();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[UIEventSystemRaycastFixer] Cleaned and saved: {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[UIEventSystemRaycastFixer] All scenes have been fixed and saved successfully!");
    }
#endif

    #endregion
}
