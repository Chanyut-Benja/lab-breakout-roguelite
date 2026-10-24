#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Editor Script สำหรับตรวจจับและลบ GameObject ที่ถูกซ่อน (Hidden Objects) 
/// หรือเส้นตกค้าง (LinesLayer, ConnectingLine, LineRenderer ฯลฯ) ใน MainMenuScene
/// </summary>
public static class CleanHiddenObjectsInScene
{
    private const string SCENE_PATH = "Assets/Scenes/MainMenuScene.unity";

    [MenuItem("Tools/Clean Hidden Objects In MainMenuScene")]
    public static void CleanAndSave()
    {
        Debug.Log("==========================================================");
        Debug.Log($"[CleanHiddenObjects] Starting cleanup for: {SCENE_PATH}");

        Scene scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError($"[CleanHiddenObjects] Could not open scene at {SCENE_PATH}");
            return;
        }

        // หา Canvas หลักใน Scene เพื่อใช้ตรวจสอบความถูกต้องของ Hierarchy
        Canvas mainCanvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform mainCanvasTr = mainCanvas != null ? mainCanvas.transform : null;

        // 1. Resources.FindObjectsOfTypeAll<GameObject>() ค้นหาวัตถุทั้งหมดรวม HideFlags ทุกประเภท
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        Debug.Log($"[CleanHiddenObjects] Total objects found by Resources.FindObjectsOfTypeAll: {allObjects.Length}");

        List<GameObject> candidates = new List<GameObject>();

        for (int i = 0; i < allObjects.Length; i++)
        {
            GameObject go = allObjects[i];
            if (go == null) continue;

            // ข้าม Asset ในโปรเจกต์ (Assets/...)
            if (EditorUtility.IsPersistent(go)) continue;

            // วัตถุต้องอยู่ใน MainMenuScene หรือเป็นวัตถุลอยในหน่วยความจำที่ไม่มี Scene ระบุ (!go.scene.IsValid())
            bool inTargetScene = go.scene == scene;
            bool isOrphanInMemory = !go.scene.IsValid();

            if (!inTargetScene && !isOrphanInMemory) continue;

            // ไม่แตะต้อง Editor Utility System พิเศษ
            if ((go.hideFlags & HideFlags.NotEditable) != 0) continue;

            string nameLower = go.name.ToLowerInvariant();

            // เงื่อนไข 1: ชื่อเกี่ยวข้องกับ Line, LinesLayer, ConnectingLine, TreeLine
            bool nameMatches = nameLower.Contains("lineslayer") ||
                               nameLower.Contains("connectingline") ||
                               nameLower.Contains("treeline") ||
                               nameLower.StartsWith("line_") ||
                               nameLower.Contains("_to_") ||
                               nameLower == "line" ||
                               nameLower.Contains("debugline");

            // เงื่อนไข 2: มี LineRenderer
            bool hasLineRenderer = go.GetComponent<LineRenderer>() != null;

            // เงื่อนไข 3: วัตถุที่ถูกซ่อน (HideFlags.HideInHierarchy หรือ HideFlags.DontSave)
            bool isHidden = (go.hideFlags & (HideFlags.HideInHierarchy | HideFlags.DontSave | HideFlags.HideAndDontSave)) != 0;

            // เงื่อนไข 4: มี RectTransform หรือ Graphic แต่ไม่ได้อยู่ใน Canvas/Hierarchy ปกติ
            bool isOrphanUI = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            Graphic graphic = go.GetComponent<Graphic>();
            if (rt != null || graphic != null)
            {
                if (mainCanvasTr != null && !go.transform.IsChildOf(mainCanvasTr))
                {
                    isOrphanUI = true;
                }
            }

            // เงื่อนไข 5: UI Image ที่เป็นเส้นตกค้างแต่หลุดออกมานอกผัง
            bool isSuspiciousLine = nameMatches || hasLineRenderer;
            if (isHidden && (nameMatches || hasLineRenderer || isOrphanUI))
            {
                isSuspiciousLine = true;
            }

            if (isSuspiciousLine || (isOrphanUI && isHidden))
            {
                if (!candidates.Contains(go))
                {
                    candidates.Add(go);
                }
            }
        }

        Debug.Log($"[CleanHiddenObjects] Identified {candidates.Count} candidate GameObject(s) to remove.");

        int deletedCount = 0;
        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject target = candidates[i];
            if (target == null) continue;

            // ปลดล็อค Prefab Instance ก่อนลบเพื่อป้องกันข้อจำกัดของ Unity
            if (PrefabUtility.IsPartOfPrefabInstance(target))
            {
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(target);
                if (root != null)
                {
                    PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }

            string targetName = target.name;
            HideFlags targetFlags = target.hideFlags;
            string targetScene = target.scene.IsValid() ? target.scene.name : "(orphan)";

            try
            {
                Object.DestroyImmediate(target, true);
                deletedCount++;
                Debug.Log($"[CleanHiddenObjects] Destroyed: '{targetName}' [HideFlags: {targetFlags}] [Scene: {targetScene}]");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[CleanHiddenObjects] Failed to destroy '{targetName}': {ex.Message}");
            }
        }

        Debug.Log($"[CleanHiddenObjects] Successfully deleted {deletedCount} GameObject(s).");

        // 3. สั่ง EditorSceneManager.MarkSceneDirty() และ SaveScene
        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CleanHiddenObjects] Scene saved successfully: {saved}");
        Debug.Log("==========================================================");
    }
}
#endif
