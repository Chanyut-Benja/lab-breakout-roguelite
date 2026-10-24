using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor script สำหรับสร้างและติดตั้ง EvolutionPopupPanel ใน GameScene
/// และบันทึกเป็น Prefab ที่ Assets/Prefabs/EvolutionPopupPanel.prefab
/// </summary>
public static class SetupEvolutionPopup
{
    [MenuItem("Tools/HUD/Setup Evolution Popup Panel in GameScene")]
    public static void Setup()
    {
        string scenePath = "Assets/Scenes/GameScene.unity";
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        if (canvas == null)
        {
            Debug.LogError("[SetupEvolutionPopup] No Canvas found in GameScene!");
            return;
        }

        // 1. ตรวจสอบ EvolutionPopupUI
        EvolutionPopupUI popup = canvas.GetComponentInChildren<EvolutionPopupUI>(true);
        if (popup == null)
        {
            GameObject popupGo = new GameObject("EvolutionPopupUI");
            popupGo.transform.SetParent(canvas.transform, false);
            popup = popupGo.AddComponent<EvolutionPopupUI>();
        }

        // 2. สร้างโครงสร้าง UI Panel
        popup.BuildPopupHierarchy();

        // 3. บันทึกเป็น Prefab
        Transform panelTrans = canvas.transform.Find("EvolutionPopupPanel");
        if (panelTrans != null)
        {
            if (!Directory.Exists("Assets/Prefabs"))
            {
                Directory.CreateDirectory("Assets/Prefabs");
                AssetDatabase.Refresh();
            }

            string prefabPath = "Assets/Prefabs/EvolutionPopupPanel.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(panelTrans.gameObject, prefabPath, InteractionMode.AutomatedAction);
            Debug.Log($"[SetupEvolutionPopup] Saved Prefab at: {prefabPath}");
        }

        // 4. บันทึก Scene
        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[SetupEvolutionPopup] Successfully set up and saved EvolutionPopupPanel in GameScene.unity!");
    }

    [MenuItem("Tools/HUD/Setup All HUD and Evolution Popup")]
    public static void SetupAll()
    {
        InGameHUDController.MenuApplyDarkSciFiSkinInScene();
        Setup();
        Debug.Log("[SetupEvolutionPopup] Successfully updated both HUD and Evolution Popup!");
    }
}
