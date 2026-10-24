using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class SetupBoonIconsAndData
{
    static SetupBoonIconsAndData()
    {
        EditorApplication.delayCall += Setup;
    }

    [MenuItem("Tools/Setup Boon Icons")]
    public static void Setup()
    {
        Debug.Log("[SetupBoonIcons] Starting Boon icons setup...");

        string spriteDir = "Assets/Sprites/Icons/Boons";
        string resourceDir = "Assets/Resources/Icons/Boons";

        if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
        if (!Directory.Exists(resourceDir)) Directory.CreateDirectory(resourceDir);
        AssetDatabase.Refresh();

        string[] fileNames = new string[]
        {
            "Icon_NanoBarrier.png",
            "Icon_ToxicSpore.png",
            "Icon_FlameRing.png",
            "Icon_StaticAura.png",
            "Icon_MagneticPulse.png"
        };

        // Ensure files exist in Resources/Icons/Boons/ as well for runtime Resources.Load
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

        // Create or configure BoonsData database asset
        string dbPath = "Assets/Resources/BoonsData.asset";
        BoonsData boonsDb = AssetDatabase.LoadAssetAtPath<BoonsData>(dbPath);
        if (boonsDb == null)
        {
            boonsDb = ScriptableObject.CreateInstance<BoonsData>();
            AssetDatabase.CreateAsset(boonsDb, dbPath);
            Debug.Log("[SetupBoonIcons] Created BoonsData asset at " + dbPath);
        }

        boonsDb.nanoBarrierIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_NanoBarrier.png");
        boonsDb.toxicSporeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_ToxicSpore.png");
        boonsDb.flameRingIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_FlameRing.png");
        boonsDb.staticAuraIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_StaticAura.png");
        boonsDb.magneticPulseIcon = AssetDatabase.LoadAssetAtPath<Sprite>(spriteDir + "/Icon_MagneticPulse.png");

        EditorUtility.SetDirty(boonsDb);
        Debug.Log($"[SetupBoonIcons] Bound icons to BoonsData: NanoBarrier={boonsDb.nanoBarrierIcon != null}, ToxicSpore={boonsDb.toxicSporeIcon != null}, FlameRing={boonsDb.flameRingIcon != null}, StaticAura={boonsDb.staticAuraIcon != null}, MagneticPulse={boonsDb.magneticPulseIcon != null}");

        // Create or update Boon cards in Assets/Data/Upgrades/
        string upgradesDir = "Assets/Data/Upgrades";
        if (!Directory.Exists("Assets/Data")) Directory.CreateDirectory("Assets/Data");
        if (!Directory.Exists(upgradesDir)) Directory.CreateDirectory(upgradesDir);

        (BoonItemType type, string fileName, string title, string desc, Sprite icon)[] boonCards = new[]
        {
            (BoonItemType.NanoBarrier, "Card_NanoBarrier.asset", "NEW: Nano-Barrier (Lv.1)", "สร้างเกราะ 20 ฟื้นทุก 15 วินาที", boonsDb.nanoBarrierIcon),
            (BoonItemType.ToxicSpore, "Card_ToxicSpore.asset", "NEW: Toxic Spore (Lv.1)", "หมอกสปอร์พิษ 15/s รัศมี 3m", boonsDb.toxicSporeIcon),
            (BoonItemType.FlameRing, "Card_FlameRing.asset", "NEW: Flame Ring (Lv.1)", "วงแหวนไฟรอบตัวเผาผลาญ 25/s", boonsDb.flameRingIcon),
            (BoonItemType.StaticAura, "Card_StaticAura.asset", "NEW: Static Aura (Lv.1)", "ผ่าสายฟ้า 1 ตัว ทุกระยะเดิน 5m (ดาเมจ 40)", boonsDb.staticAuraIcon),
            (BoonItemType.MagneticPulse, "Card_MagneticPulse.asset", "NEW: Magnetic Pulse (Lv.1)", "ปล่อยพัลส์แม่เหล็กทุก 4 วินาที สโลว์ศัตรู 30%", boonsDb.magneticPulseIcon)
        };

        foreach (var bc in boonCards)
        {
            string cardPath = $"{upgradesDir}/{bc.fileName}";
            LevelUpUpgradeData card = AssetDatabase.LoadAssetAtPath<LevelUpUpgradeData>(cardPath);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<LevelUpUpgradeData>();
                card.category = UpgradeCategory.Boon;
                card.boonItemType = bc.type;
                card.targetLevel = 1;
                card.upgradeTitle = bc.title;
                card.description = bc.desc;
                card.icon = bc.icon;
                AssetDatabase.CreateAsset(card, cardPath);
                Debug.Log("[SetupBoonIcons] Created Boon Card asset at " + cardPath);
            }
            else
            {
                card.category = UpgradeCategory.Boon;
                card.boonItemType = bc.type;
                card.targetLevel = 1;
                card.upgradeTitle = bc.title;
                card.description = bc.desc;
                card.icon = bc.icon;
                EditorUtility.SetDirty(card);
            }
        }

        AssetDatabase.SaveAssets();
        PlayerInventory.ClearBoonIconCache();
        Debug.Log("[SetupBoonIcons] Successfully completed Boon icons and data setup!");
    }

    private static void ConfigureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning("[SetupBoonIcons] TextureImporter not found for " + path);
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
            Debug.Log("[SetupBoonIcons] Configured TextureImporter for " + path);
        }
    }
}
