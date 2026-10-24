using UnityEngine;
using UnityEditor;
using System.IO;

public static class SetupTechPassiveIconsAndData
{
    [MenuItem("Tools/Setup Tech Passive Icons")]
    public static void Setup()
    {
        Debug.Log("[SetupTechPassiveIcons] Starting Tech Passive icons setup...");

        string spriteDir = "Assets/Sprites/Icons/TechPassives";
        string resourceDir = "Assets/Resources/Icons/TechPassives";

        if (!Directory.Exists("Assets/Resources"))
        {
            Directory.CreateDirectory("Assets/Resources");
        }
        if (!Directory.Exists(resourceDir))
        {
            Directory.CreateDirectory(resourceDir);
        }
        if (!Directory.Exists("Assets/Resources/TechPassives"))
        {
            Directory.CreateDirectory("Assets/Resources/TechPassives");
        }
        AssetDatabase.Refresh();

        string[] fileNames = new string[]
        {
            "Icon_TargetingLens.png",
            "Icon_GeneticExtractor.png",
            "Icon_OverclockChip.png",
            "Icon_BioFuelTank.png",
            "Icon_GravitonCapacitor.png"
        };

        // Ensure files exist in Resources/Icons/TechPassives/ as well for runtime Resources.Load
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

        // Create or configure TechPassivesData database asset
        string dbPath = "Assets/Resources/TechPassivesData.asset";
        TechPassivesData techDb = AssetDatabase.LoadAssetAtPath<TechPassivesData>(dbPath);
        if (techDb == null)
        {
            techDb = ScriptableObject.CreateInstance<TechPassivesData>();
            AssetDatabase.CreateAsset(techDb, dbPath);
            Debug.Log("[SetupTechPassiveIcons] Created TechPassivesData asset at " + dbPath);
        }

        techDb.targetingLensIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_TargetingLens.png");
        techDb.geneticExtractorIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_GeneticExtractor.png");
        techDb.overclockChipIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_OverclockChip.png");
        techDb.bioFuelTankIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_BioFuelTank.png");
        techDb.gravitonCapacitorIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_GravitonCapacitor.png");

        EditorUtility.SetDirty(techDb);
        Debug.Log($"[SetupTechPassiveIcons] Bound icons to TechPassivesData: TargetingLens={techDb.targetingLensIcon != null}, GeneticExtractor={techDb.geneticExtractorIcon != null}, OverclockChip={techDb.overclockChipIcon != null}, BioFuelTank={techDb.bioFuelTankIcon != null}, GravitonCapacitor={techDb.gravitonCapacitorIcon != null}");

        // Create or update individual TechPassiveData ScriptableObjects
        SetupIndividualTechData(TechPassiveType.TargetingLens, "Targeting Lens", "TargetingLensData", WeaponType.NanoBlade, techDb.targetingLensIcon, 0.05f, 0.20f, "Increases Critical Hit Rate and Critical Damage.");
        SetupIndividualTechData(TechPassiveType.GeneticExtractor, "Genetic Extractor", "GeneticExtractorData", WeaponType.BioNeedler, techDb.geneticExtractorIcon, 0.25f, 0.10f, "Increases Pickup Radius and RP Bonus.");
        SetupIndividualTechData(TechPassiveType.OverclockChip, "Overclock Chip", "OverclockChipData", WeaponType.PlasmaBlaster, techDb.overclockChipIcon, 0.08f, 0f, "Reduces weapon cooldowns (Cooldown Reduction).");
        SetupIndividualTechData(TechPassiveType.BioFuelTank, "Bio-Fuel Tank", "BioFuelTankData", WeaponType.TeslaOrb, techDb.bioFuelTankIcon, 0.10f, 0f, "Increases attack area size of weapons.");
        SetupIndividualTechData(TechPassiveType.GravitonCapacitor, "Graviton Capacitor", "GravitonCapacitorData", WeaponType.GravityGrenadier, techDb.gravitonCapacitorIcon, 0.08f, 0f, "Increases operative movement speed.");

        // Also scan and bind any existing TechPassiveData instances in the project
        string[] allGuids = AssetDatabase.FindAssets("t:TechPassiveData");
        foreach (var guid in allGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var tpd = AssetDatabase.LoadAssetAtPath<TechPassiveData>(path);
            if (tpd != null)
            {
                Sprite icon = techDb.GetIcon(tpd.techType);
                if (icon != null)
                {
                    tpd.icon = icon;
                    EditorUtility.SetDirty(tpd);
                    Debug.Log($"[SetupTechPassiveIcons] Updated icon in TechPassiveData asset at {path}");
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[SetupTechPassiveIcons] Successfully completed Tech Passive icons and data setup!");
    }

    private static void SetupIndividualTechData(TechPassiveType type, string name, string assetName, WeaponType matchingWeapon, Sprite icon, float pBonus, float sBonus, string desc)
    {
        string path = $"Assets/Resources/TechPassives/{assetName}.asset";
        TechPassiveData asset = AssetDatabase.LoadAssetAtPath<TechPassiveData>(path);
        if (asset == null)
        {
            asset = ScriptableObject.CreateInstance<TechPassiveData>();
            AssetDatabase.CreateAsset(asset, path);
        }

        asset.techType = type;
        asset.techName = name;
        asset.matchingWeapon = matchingWeapon;
        asset.icon = icon;
        asset.primaryBonusPerLevel = pBonus;
        asset.secondaryBonusPerLevel = sBonus;
        asset.description = desc;

        EditorUtility.SetDirty(asset);
        Debug.Log($"[SetupTechPassiveIcons] Configured TechPassiveData asset at {path}");
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[SetupTechPassiveIcons] TextureImporter not found for " + path);
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

        if (importer.textureCompression != TextureImporterCompression.Compressed)
        {
            importer.textureCompression = TextureImporterCompression.Compressed;
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
            Debug.Log("[SetupTechPassiveIcons] Configured TextureImporter for " + path);
        }
    }
}
