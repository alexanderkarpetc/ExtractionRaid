using System;
using System.IO;
using UnityEngine;

namespace Save
{
    public static class SaveManager
    {
        const string FileName = "save.json";

        static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save(SaveData data)
        {
            try
            {
                var json = JsonUtility.ToJson(data, true);
                File.WriteAllText(FilePath, json);
                Debug.Log($"[SaveManager] Saved to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Save failed: {e.Message}");
            }
        }

        public static SaveData Load()
        {
            if (!File.Exists(FilePath))
                return null;

            try
            {
                var json = File.ReadAllText(FilePath);
                var data = JsonUtility.FromJson<SaveData>(json);
                Debug.Log("[SaveManager] Save loaded.");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveManager] Load failed: {e.Message}");
                return null;
            }
        }

        public static bool HasSave() => File.Exists(FilePath);

        public static void Delete()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
                Debug.Log("[SaveManager] Save deleted.");
            }
        }
    }
}
