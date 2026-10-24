using UnityEngine;
using UnityEditor;
using System.IO;

public static class SetupEvolutionIconsAndData
{
    [MenuItem("Tools/Setup Evolution Icons and Data")]
    public static void Setup()
    {
        Debug.Log("[SetupEvolutionIcons] Starting Evolution weapon icons setup...");

        string spriteDir = "Assets/Sprites/Icons/Evolution";
        string resourceDir = "Assets/Resources/Icons/Evolution";

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
            "Icon_ExecutionerMatrix.png",
            "Icon_PlagueSporeSpreader.png",
            "Icon_SupernovaCannon.png",
            "Icon_StormCitadel.png",
            "Icon_SingularityCannon.png"
        };

        // Ensure files exist in Resources/Icons/Evolution/ as well for runtime Resources.Load
        foreach (var file in fileNames)
        {
            string srcPath = Path.Combine(spriteDir, file);
            string dstPath = Path.Combine(resourceDir, file);

            if (File.Exists(srcPath) && !File.Exists(dstPath))
            {
                File.Copy(srcPath, dstPath, true);
            }
            else if (File.Exists(dstPath) && !File.Exists(srcPath))
            {
                File.Copy(dstPath, srcPath, true);
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

        // Create or configure EvolutionDatabase ScriptableObject
        string evoDataPath = "Assets/Resources/EvolutionDatabase.asset";
        EvolutionDatabase evoData = AssetDatabase.LoadAssetAtPath<EvolutionDatabase>(evoDataPath);
        if (evoData == null)
        {
            evoData = ScriptableObject.CreateInstance<EvolutionDatabase>();
            AssetDatabase.CreateAsset(evoData, evoDataPath);
            Debug.Log("[SetupEvolutionIcons] Created EvolutionDatabase asset at " + evoDataPath);
        }

        evoData.executionerMatrixIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_ExecutionerMatrix.png");
        evoData.plagueSporeSpreaderIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_PlagueSporeSpreader.png");
        evoData.supernovaCannonIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_SupernovaCannon.png");
        evoData.stormCitadelIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_StormCitadel.png");
        evoData.singularityCannonIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_SingularityCannon.png");

        EditorUtility.SetDirty(evoData);
        Debug.Log($"[SetupEvolutionIcons] Bound icons to EvolutionDatabase: Executioner={evoData.executionerMatrixIcon != null}, Plague={evoData.plagueSporeSpreaderIcon != null}, Supernova={evoData.supernovaCannonIcon != null}, Storm={evoData.stormCitadelIcon != null}, Singularity={evoData.singularityCannonIcon != null}");

        // Also check if any EvolutionManager in open scene needs updating
        EvolutionManager mgr = Object.FindFirstObjectByType<EvolutionManager>(FindObjectsInactive.Include);
        if (mgr != null)
        {
            mgr.InitializeDatabase();
            EditorUtility.SetDirty(mgr);
            Debug.Log("[SetupEvolutionIcons] Updated EvolutionManager in active scene.");
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupEvolutionIcons] Successfully completed Evolution icons and database setup!");
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[SetupEvolutionIcons] TextureImporter not found for " + path);
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

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log("[SetupEvolutionIcons] Configured TextureImporter for " + path);
        }
    }
}
