using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class BuildHUDInSceneAndPrefab
{
    [MenuItem("Tools/HUD/Setup HUD Slots and Resource Display in GameScene")]
    public static void Execute()
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
            Debug.LogError("[BuildHUDInSceneAndPrefab] No Canvas found in GameScene!");
            return;
        }

        InGameHUDController controller = canvas.GetComponent<InGameHUDController>();
        if (controller == null)
        {
            controller = canvas.gameObject.AddComponent<InGameHUDController>();
        }

        // Apply complete Dark Sci-Fi Skin and generate slots & resources
        controller.ApplyDarkSciFiSkin();

        // Ensure Prefab Directory
        string prefabDir = "Assets/Prefabs/UI";
        if (!Directory.Exists(prefabDir))
        {
            Directory.CreateDirectory(prefabDir);
            AssetDatabase.Refresh();
        }

        // Create standalone Slot Prefab if not already existing
        string slotPrefabPath = $"{prefabDir}/HUDItemSlotPrefab.prefab";
        Transform slotTrans = canvas.transform.Find("InGameHUDPanel/WeaponsRow/WeaponSlot_0");
        if (slotTrans != null)
        {
            GameObject tempSlot = Object.Instantiate(slotTrans.gameObject);
            tempSlot.name = "HUDItemSlotPrefab";
            HUDSlotUI slotUI = tempSlot.GetComponent<HUDSlotUI>();
            if (slotUI != null)
            {
                slotUI.SetEmpty();
            }

            PrefabUtility.SaveAsPrefabAsset(tempSlot, slotPrefabPath, out bool success);
            Object.DestroyImmediate(tempSlot);

            if (success)
            {
                Debug.Log($"[BuildHUDInSceneAndPrefab] Saved Prefab successfully to {slotPrefabPath}!");
            }
        }

        EditorUtility.SetDirty(canvas.gameObject);
        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        AssetDatabase.SaveAssets();

        Debug.Log("[BuildHUDInSceneAndPrefab] Finished setting up InGameHUDController and GameScene!");
    }
}
