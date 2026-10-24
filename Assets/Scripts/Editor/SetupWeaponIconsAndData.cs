using UnityEngine;
using UnityEditor;
using System.IO;

public static class SetupWeaponIconsAndData
{
    [MenuItem("Tools/Setup Weapon Icons")]
    public static void Setup()
    {
        Debug.Log("[SetupWeaponIcons] Starting weapon icons setup...");

        string spriteDir = "Assets/Sprites/Icons/Weapons";
        string resourceDir = "Assets/Resources/Icons/Weapons";

        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.Refresh();
        }
        if (!Directory.Exists(resourceDir))
        {
            Directory.CreateDirectory(resourceDir);
            AssetDatabase.Refresh();
        }

        string[] fileNames = new string[]
        {
            "Icon_NanoBlade.png",
            "Icon_BioNeedler.png",
            "Icon_PlasmaBlaster.png",
            "Icon_TeslaOrb.png",
            "Icon_GravityGrenadier.png"
        };

        // Ensure files exist in Resources/Icons/Weapons/ as well for runtime Resources.Load
        foreach (var file in fileNames)
        {
            string srcPath = Path.Combine(spriteDir, file);
            string dstPath = Path.Combine(resourceDir, file);

            if (File.Exists(srcPath))
            {
                File.Copy(srcPath, dstPath, true);
            }
        }

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Configure Texture Importers for both directories
        string[] directoriesToProcess = new string[] { spriteDir, resourceDir };
        foreach (var dir in directoriesToProcess)
        {
            foreach (var file in fileNames)
            {
                string assetPath = dir + "/" + file;
                ConfigureSpriteImporter(assetPath);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Create or configure WeaponsData
        string weaponsDataPath = "Assets/Resources/WeaponsData.asset";
        WeaponsData weaponsData = AssetDatabase.LoadAssetAtPath<WeaponsData>(weaponsDataPath);
        if (weaponsData == null)
        {
            weaponsData = ScriptableObject.CreateInstance<WeaponsData>();
            AssetDatabase.CreateAsset(weaponsData, weaponsDataPath);
            Debug.Log("[SetupWeaponIcons] Created WeaponsData asset at " + weaponsDataPath);
        }

        weaponsData.nanoBladeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_NanoBlade.png");
        weaponsData.bioNeedlerIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_BioNeedler.png");
        weaponsData.plasmaBlasterIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_PlasmaBlaster.png");
        weaponsData.teslaOrbIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_TeslaOrb.png");
        weaponsData.gravityGrenadierIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_GravityGrenadier.png");

        EditorUtility.SetDirty(weaponsData);
        Debug.Log($"[SetupWeaponIcons] Bound icons to WeaponsData: Nano={weaponsData.nanoBladeIcon != null}, Bio={weaponsData.bioNeedlerIcon != null}, Plasma={weaponsData.plasmaBlasterIcon != null}, Tesla={weaponsData.teslaOrbIcon != null}, Gravity={weaponsData.gravityGrenadierIcon != null}");

        // Also search and bind to any existing WeaponData ScriptableObjects
        string[] allGuids = AssetDatabase.FindAssets("t:ScriptableObject");
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var obj = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (obj == null) continue;

            if (obj is BioNeedlerData bnd)
            {
                bnd.icon = weaponsData.bioNeedlerIcon;
                EditorUtility.SetDirty(bnd);
                Debug.Log("[SetupWeaponIcons] Updated BioNeedlerData at " + path);
            }
            else if (obj is TeslaOrbData tod)
            {
                tod.icon = weaponsData.teslaOrbIcon;
                EditorUtility.SetDirty(tod);
                Debug.Log("[SetupWeaponIcons] Updated TeslaOrbData at " + path);
            }
            else if (obj is PlasmaBlasterData pbd)
            {
                pbd.icon = weaponsData.plasmaBlasterIcon;
                EditorUtility.SetDirty(pbd);
                Debug.Log("[SetupWeaponIcons] Updated PlasmaBlasterData at " + path);
            }
            else if (obj is GravityGrenadierData ggd)
            {
                ggd.icon = weaponsData.gravityGrenadierIcon;
                EditorUtility.SetDirty(ggd);
                Debug.Log("[SetupWeaponIcons] Updated GravityGrenadierData at " + path);
            }
            else if (obj is NanoBladeData nbd)
            {
                nbd.icon = weaponsData.nanoBladeIcon;
                EditorUtility.SetDirty(nbd);
                Debug.Log("[SetupWeaponIcons] Updated NanoBladeData at " + path);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupWeaponIcons] Successfully completed weapon icons and data setup!");
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[SetupWeaponIcons] TextureImporter not found for " + path);
            return;
        }

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteGenerateFallbackPhysicsShape)
        {
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (importer.alphaIsTransparency != true)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[SetupWeaponIcons] Configured TextureImporter for " + path);
        }
    }
}
