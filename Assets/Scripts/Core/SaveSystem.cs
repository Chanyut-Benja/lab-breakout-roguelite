using System.IO;
using UnityEngine;

public static class SaveSystem
{
    private const string RP_PREFS_KEY = "Meta_ResearchPoints";
    private const string UNLOCKED_NODES_PREFS_KEY = "Meta_UnlockedSkillNodes";
    private static string SavePath => Application.persistentDataPath + "/saveData.json";

    public static void SaveGame(GameData data)
    {
        if (data == null) data = new GameData();
        data.EnsureInitialized();

        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SavePath, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[SaveSystem] Failed writing save file: {ex.Message}");
        }

        // ซิงค์ลง PlayerPrefs ควบคู่กันเสมอ
        PlayerPrefs.SetInt(RP_PREFS_KEY, data.researchPoints);
        PlayerPrefs.SetString(UNLOCKED_NODES_PREFS_KEY, string.Join(",", data.unlockedSkillNodes));
        SaveBranchArrayToPrefs("N01", data.vanguardBranchLevels);
        SaveBranchArrayToPrefs("N02", data.psionicBranchLevels);
        SaveBranchArrayToPrefs("N03", data.cyberSwiftBranchLevels);
        PlayerPrefs.Save();
    }

    public static GameData LoadGame()
    {
        GameData data = null;

        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                data = JsonUtility.FromJson<GameData>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SaveSystem] Failed reading save file: {ex.Message}");
            }
        }

        if (data == null)
        {
            data = new GameData();
            if (PlayerPrefs.HasKey(RP_PREFS_KEY))
            {
                data.researchPoints = PlayerPrefs.GetInt(RP_PREFS_KEY, 0);
            }
        }

        data.EnsureInitialized();

        // ซิงค์ข้อมูลที่อาจบันทึกไว้ใน PlayerPrefs
        LoadBranchArrayFromPrefs("N01", data.vanguardBranchLevels);
        LoadBranchArrayFromPrefs("N02", data.psionicBranchLevels);
        LoadBranchArrayFromPrefs("N03", data.cyberSwiftBranchLevels);

        string prefsNodes = PlayerPrefs.GetString(UNLOCKED_NODES_PREFS_KEY, "");
        if (!string.IsNullOrEmpty(prefsNodes))
        {
            string[] split = prefsNodes.Split(',');
            for (int i = 0; i < split.Length; i++)
            {
                string nodeId = split[i].Trim();
                if (!string.IsNullOrEmpty(nodeId) && !data.unlockedSkillNodes.Contains(nodeId))
                {
                    data.unlockedSkillNodes.Add(nodeId);
                }
            }
        }

        return data;
    }

    private static void SaveBranchArrayToPrefs(string prefix, int[] levels)
    {
        if (levels == null) return;
        for (int i = 0; i < levels.Length; i++)
        {
            PlayerPrefs.SetInt($"Meta_{prefix}_Branch_{i}", levels[i]);
        }
    }

    private static void LoadBranchArrayFromPrefs(string prefix, int[] levels)
    {
        if (levels == null) return;
        for (int i = 0; i < levels.Length; i++)
        {
            int prefsVal = PlayerPrefs.GetInt($"Meta_{prefix}_Branch_{i}", 0);
            if (prefsVal > levels[i])
            {
                levels[i] = prefsVal;
            }
        }
    }

    public static void AddResearchPoints(int amount)
    {
        GameData data = LoadGame();
        data.researchPoints = Mathf.Max(0, data.researchPoints + amount);
        SaveGame(data);

        Debug.Log($"[SaveSystem] Added {amount} RP. Total RP: {data.researchPoints}");
    }

    public static int GetResearchPoints()
    {
        GameData data = LoadGame();
        return data.researchPoints;
    }
}
