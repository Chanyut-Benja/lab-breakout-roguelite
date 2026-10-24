using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.IO;
using System.Collections.Generic;

public static class SetupGameSceneHierarchy
{
    public static GameObject FindInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindRecursive(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    public static Transform FindRecursive(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var res = FindRecursive(root.GetChild(i), name);
            if (res != null) return res;
        }
        return null;
    }

    [MenuItem("Tools/Setup GameScene Hierarchy")]
    public static void Setup()
    {
        string scenePath = "Assets/Scenes/GameScene.unity";
        var activeScene = EditorSceneManager.GetActiveScene();
        if (activeScene.path != scenePath)
        {
            activeScene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }

        // 1. Ensure Upgrade Assets Directory & Create 10 LevelUpUpgradeData ScriptableObjects
        string upgradeFolder = "Assets/Data/Upgrades";
        if (!AssetDatabase.IsValidFolder(upgradeFolder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Data"))
            {
                AssetDatabase.CreateFolder("Assets", "Data");
            }
            AssetDatabase.CreateFolder("Assets/Data", "Upgrades");
        }

        List<LevelUpUpgradeData> upgradeAssets = new List<LevelUpUpgradeData>();

        // 5 Weapons
        (WeaponType weapon, string assetName, string title, string desc)[] weapons = new[]
        {
            (WeaponType.NanoBlade, "Card_NanoBlade.asset", "NEW: Nano-Blade (Lv.1)", "Swings a high-frequency nano-blade dealing 25 damage in a 120° arc."),
            (WeaponType.BioNeedler, "Card_BioNeedler.asset", "NEW: Bio-Needler (Lv.1)", "Fires rapid biological spines applying poison stacks."),
            (WeaponType.PlasmaBlaster, "Card_PlasmaBlaster.asset", "NEW: Plasma Blaster (Lv.1)", "Launches high-energy plasma bolts causing area explosions."),
            (WeaponType.TeslaOrb, "Card_TeslaOrb.asset", "NEW: Tesla Orb (Lv.1)", "Orbits player discharging electricity chains to nearby foes."),
            (WeaponType.GravityGrenadier, "Card_GravityGrenadier.asset", "NEW: Gravity Grenadier (Lv.1)", "Hurls singularity grenades pulling enemies inward before exploding.")
        };

        foreach (var w in weapons)
        {
            string path = $"{upgradeFolder}/{w.assetName}";
            LevelUpUpgradeData asset = AssetDatabase.LoadAssetAtPath<LevelUpUpgradeData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelUpUpgradeData>();
                asset.category = UpgradeCategory.Weapon;
                asset.weaponType = w.weapon;
                asset.targetLevel = 1;
                asset.upgradeTitle = w.title;
                asset.description = w.desc;
                AssetDatabase.CreateAsset(asset, path);
            }
            upgradeAssets.Add(asset);
        }

        // 5 Tech Passives
        (TechPassiveType tech, string assetName, string title, string desc)[] techs = new[]
        {
            (TechPassiveType.TargetingLens, "Card_TargetingLens.asset", "Targeting Lens (Lv.1)", "Optimizes optical targeting sensors. Critical hit chance +5%."),
            (TechPassiveType.GeneticExtractor, "Card_GeneticExtractor.asset", "Genetic Extractor (Lv.1)", "Synthesizes enemy essence into vitality. Life steal +2%."),
            (TechPassiveType.OverclockChip, "Card_OverclockChip.asset", "Overclock Chip (Lv.1)", "Overclocks weapon firing mechanisms. Attack speed +10%."),
            (TechPassiveType.BioFuelTank, "Card_BioFuelTank.asset", "Bio-Fuel Tank (Lv.1)", "Converts biomass into kinetic acceleration. Move speed +10%."),
            (TechPassiveType.GravitonCapacitor, "Card_GravitonCapacitor.asset", "Graviton Capacitor (Lv.1)", "Expands gravitational collection radius. Pickup radius +25%.")
        };

        foreach (var t in techs)
        {
            string path = $"{upgradeFolder}/{t.assetName}";
            LevelUpUpgradeData asset = AssetDatabase.LoadAssetAtPath<LevelUpUpgradeData>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LevelUpUpgradeData>();
                asset.category = UpgradeCategory.TechPassive;
                asset.techType = t.tech;
                asset.targetLevel = 1;
                asset.upgradeTitle = t.title;
                asset.description = t.desc;
                AssetDatabase.CreateAsset(asset, path);
            }
            upgradeAssets.Add(asset);
        }

        AssetDatabase.SaveAssets();

        // 2. Locate and setup Player GameObject
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj == null) playerObj = FindInScene(activeScene, "Player");
        if (playerObj == null)
        {
            Debug.LogError("[SetupGameSceneHierarchy] Player not found in scene!");
            return;
        }
        playerObj.tag = "Player";

        if (playerObj.GetComponent<PlayerStats>() == null) playerObj.AddComponent<PlayerStats>();
        if (playerObj.GetComponent<PlayerHealth>() == null) playerObj.AddComponent<PlayerHealth>();
        if (playerObj.GetComponent<PlayerController>() == null) playerObj.AddComponent<PlayerController>();
        if (playerObj.GetComponent<PlayerLevel>() == null) playerObj.AddComponent<PlayerLevel>();
        if (playerObj.GetComponent<PlayerInventory>() == null) playerObj.AddComponent<PlayerInventory>();
        if (playerObj.GetComponent<PlayerManualShooter>() == null) playerObj.AddComponent<PlayerManualShooter>();

        // 3. Locate or create RoomManager GameObject
        GameObject roomManagerGo = FindInScene(activeScene, "RoomManager");
        if (roomManagerGo == null)
        {
            roomManagerGo = new GameObject("RoomManager");
            Undo.RegisterCreatedObjectUndo(roomManagerGo, "Create RoomManager");
        }
        RoomManager rm = roomManagerGo.GetComponent<RoomManager>();
        if (rm == null) rm = roomManagerGo.AddComponent<RoomManager>();

        SerializedObject rmSo = new SerializedObject(rm);

        Text timerText = null;
        Text roomText = null;
        TMP_Text tmpTimerText = null;
        TMP_Text tmpRoomText = null;

        GameObject timerGo = FindInScene(activeScene, "TimerText");
        if (timerGo != null)
        {
            timerText = timerGo.GetComponent<Text>();
            tmpTimerText = timerGo.GetComponent<TMP_Text>();
        }
        GameObject roomGo = FindInScene(activeScene, "RoomText");
        if (roomGo != null)
        {
            roomText = roomGo.GetComponent<Text>();
            tmpRoomText = roomGo.GetComponent<TMP_Text>();
        }

        rmSo.FindProperty("timerText").objectReferenceValue = timerText;
        rmSo.FindProperty("roomText").objectReferenceValue = roomText;
        var pTmpTimer = rmSo.FindProperty("tmpTimerText");
        if (pTmpTimer != null) pTmpTimer.objectReferenceValue = tmpTimerText;
        var pTmpRoom = rmSo.FindProperty("tmpRoomText");
        if (pTmpRoom != null) pTmpRoom.objectReferenceValue = tmpRoomText;

        Transform door0 = FindInScene(activeScene, "DoorSpawnPoint_0")?.transform;
        Transform door1 = FindInScene(activeScene, "DoorSpawnPoint_1")?.transform;
        Transform playerSpawn = FindInScene(activeScene, "PlayerSpawnPoint")?.transform;
        Transform bossSpawn = FindInScene(activeScene, "BossSpawnPoint")?.transform;

        SerializedProperty doorPointsProp = rmSo.FindProperty("doorSpawnPoints");
        doorPointsProp.arraySize = 2;
        doorPointsProp.GetArrayElementAtIndex(0).objectReferenceValue = door0;
        doorPointsProp.GetArrayElementAtIndex(1).objectReferenceValue = door1;

        rmSo.FindProperty("playerSpawnPoint").objectReferenceValue = playerSpawn;
        rmSo.FindProperty("bossSpawnPoint").objectReferenceValue = bossSpawn;

        GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/RoomDoor.prefab");
        rmSo.FindProperty("roomDoorPrefab").objectReferenceValue = doorPrefab;

        GameObject boonUI = FindInScene(activeScene, "LevelUpPanel");
        GameObject shopUI = FindInScene(activeScene, "ShopPanel");
        rmSo.FindProperty("boonUI").objectReferenceValue = boonUI;
        rmSo.FindProperty("shopUI").objectReferenceValue = shopUI;

        string[] bossPaths = new string[]
        {
            "Assets/Prefabs/Enemy_Boss_Brute.prefab",
            "Assets/Prefabs/Enemy_Boss_Architect.prefab",
            "Assets/Prefabs/Enemy_Boss_Doctor.prefab",
            "Assets/Prefabs/Enemy_Boss_Digger.prefab",
            "Assets/Prefabs/Enemy_Boss_Stormcaller.prefab",
            "Assets/Prefabs/Enemy_Boss_Overlord.prefab"
        };
        SerializedProperty bossPrefabsProp = rmSo.FindProperty("bossPrefabs");
        bossPrefabsProp.arraySize = bossPaths.Length;
        for (int i = 0; i < bossPaths.Length; i++)
        {
            GameObject bossObj = AssetDatabase.LoadAssetAtPath<GameObject>(bossPaths[i]);
            if (bossObj == null)
            {
                bossObj = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemy_Boss_{i + 1}.prefab");
            }
            bossPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = bossObj;
        }

        rmSo.ApplyModifiedProperties();

        // 4. Locate or create EnemySpawner GameObject
        GameObject enemySpawnerGo = FindInScene(activeScene, "EnemySpawner");
        if (enemySpawnerGo == null)
        {
            enemySpawnerGo = new GameObject("EnemySpawner");
            Undo.RegisterCreatedObjectUndo(enemySpawnerGo, "Create EnemySpawner");
        }
        EnemySpawner spawner = enemySpawnerGo.GetComponent<EnemySpawner>();
        if (spawner == null) spawner = enemySpawnerGo.AddComponent<EnemySpawner>();

        SerializedObject spawnerSo = new SerializedObject(spawner);
        var playerTransProp = spawnerSo.FindProperty("playerTransform");
        if (playerTransProp != null) playerTransProp.objectReferenceValue = playerObj.transform;

        string[] enemyPrefabPaths = new string[]
        {
            "Assets/Prefabs/Enemy_Walker.prefab",
            "Assets/Prefabs/Enemy_Runner.prefab",
            "Assets/Prefabs/Enemy_Ranged.prefab",
            "Assets/Prefabs/Enemy_Burrower.prefab",
            "Assets/Prefabs/Enemy_Dasher.prefab"
        };
        SerializedProperty enemyPrefabsProp = spawnerSo.FindProperty("enemyPrefabs");
        enemyPrefabsProp.arraySize = enemyPrefabPaths.Length;
        for (int i = 0; i < enemyPrefabPaths.Length; i++)
        {
            GameObject ePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(enemyPrefabPaths[i]);
            enemyPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = ePrefab;
        }

        spawnerSo.FindProperty("maxEnemiesAtOnce").intValue = 40;
        spawnerSo.FindProperty("minSpawnRadius").floatValue = 12f;
        spawnerSo.FindProperty("maxSpawnRadius").floatValue = 14f;
        spawnerSo.FindProperty("repositionDistanceThreshold").floatValue = 25f;
        spawnerSo.FindProperty("spawnInterval").floatValue = 1.33f;

        spawnerSo.ApplyModifiedProperties();

        // 5. Configure GameManager and Progression Managers
        GameObject gmObj = FindInScene(activeScene, "GameManager");
        if (gmObj == null) gmObj = new GameObject("GameManager");

        if (gmObj.GetComponent<GameManager>() == null) gmObj.AddComponent<GameManager>();

        RoomManager duplicateRm = gmObj.GetComponent<RoomManager>();
        if (duplicateRm != null) Object.DestroyImmediate(duplicateRm);

        EnemySpawner duplicateSpawner = gmObj.GetComponent<EnemySpawner>();
        if (duplicateSpawner != null) Object.DestroyImmediate(duplicateSpawner);

        // PauseManager
        PauseManager pm = gmObj.GetComponent<PauseManager>();
        if (pm == null) pm = gmObj.AddComponent<PauseManager>();
        GameObject pauseMenuPanel = FindInScene(activeScene, "PauseMenuPanel");
        if (pauseMenuPanel == null) pauseMenuPanel = FindInScene(activeScene, "PausedPanel");

        SerializedObject pmSo = new SerializedObject(pm);
        pmSo.FindProperty("pauseMenuUI").objectReferenceValue = pauseMenuPanel;
        pmSo.ApplyModifiedProperties();

        // LevelUpManager
        LevelUpManager lum = gmObj.GetComponent<LevelUpManager>();
        if (lum == null) lum = gmObj.AddComponent<LevelUpManager>();

        SerializedObject lumSo = new SerializedObject(lum);
        SerializedProperty poolProp = lumSo.FindProperty("upgradePool");
        poolProp.arraySize = upgradeAssets.Count;
        for (int i = 0; i < upgradeAssets.Count; i++)
        {
            poolProp.GetArrayElementAtIndex(i).objectReferenceValue = upgradeAssets[i];
        }

        GameObject levelUpPanelGo = FindInScene(activeScene, "LevelUpPanel");
        lumSo.FindProperty("levelUpPanel").objectReferenceValue = levelUpPanelGo;

        if (levelUpPanelGo != null)
        {
            Transform titleTrans = levelUpPanelGo.transform.Find("TitleText");
            if (titleTrans == null) titleTrans = levelUpPanelGo.transform.Find("LevelUpTitle");
            if (titleTrans != null)
            {
                lumSo.FindProperty("levelUpTitleText").objectReferenceValue = titleTrans.GetComponent<Text>();
            }

            List<Button> cardBtns = new List<Button>();
            foreach (Button b in levelUpPanelGo.GetComponentsInChildren<Button>(true))
            {
                if (!b.name.ToLower().Contains("reroll") && !b.name.ToLower().Contains("banish"))
                {
                    cardBtns.Add(b);
                }
            }

            if (cardBtns.Count >= 3)
            {
                SerializedProperty btnsProp = lumSo.FindProperty("cardButtons");
                SerializedProperty titlesProp = lumSo.FindProperty("cardTitleTexts");
                SerializedProperty descsProp = lumSo.FindProperty("cardDescriptionTexts");
                SerializedProperty iconsProp = lumSo.FindProperty("cardIconImages");

                btnsProp.arraySize = 3;
                titlesProp.arraySize = 3;
                descsProp.arraySize = 3;
                iconsProp.arraySize = 3;

                for (int i = 0; i < 3; i++)
                {
                    btnsProp.GetArrayElementAtIndex(i).objectReferenceValue = cardBtns[i];
                    Text[] texts = cardBtns[i].GetComponentsInChildren<Text>(true);
                    if (texts.Length > 0) titlesProp.GetArrayElementAtIndex(i).objectReferenceValue = texts[0];
                    if (texts.Length > 1) descsProp.GetArrayElementAtIndex(i).objectReferenceValue = texts[1];
                    Transform iconTrans = cardBtns[i].transform.Find("Icon");
                    if (iconTrans != null) iconsProp.GetArrayElementAtIndex(i).objectReferenceValue = iconTrans.GetComponent<Image>();
                }
            }
        }
        lumSo.ApplyModifiedProperties();

        // BoonManager
        BoonManager bm = gmObj.GetComponent<BoonManager>();
        if (bm == null) bm = gmObj.AddComponent<BoonManager>();
        SerializedObject bmSo = new SerializedObject(bm);
        bmSo.FindProperty("boonPanel").objectReferenceValue = levelUpPanelGo;
        bmSo.ApplyModifiedProperties();

        // 6. Ensure EventSystem has StandaloneInputModule
        GameObject eventSystemGo = FindInScene(activeScene, "EventSystem");
        if (eventSystemGo == null)
        {
            eventSystemGo = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(eventSystemGo, "Create EventSystem");
        }
        EventSystem es = eventSystemGo.GetComponent<EventSystem>();
        if (es == null) es = eventSystemGo.AddComponent<EventSystem>();
        es.enabled = true;

        StandaloneInputModule sim = eventSystemGo.GetComponent<StandaloneInputModule>();
        if (sim == null) sim = eventSystemGo.AddComponent<StandaloneInputModule>();
        sim.enabled = true;

        EditorSceneManager.MarkSceneDirty(activeScene);
        EditorSceneManager.SaveScene(activeScene);
        Debug.Log("[SetupGameSceneHierarchy] GameScene hierarchy and full pipeline configured and saved successfully!");
    }
}
