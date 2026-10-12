using System.IO;
using UnityEngine;

namespace CrimeSim
{
    [System.Serializable]
    public class SaveData
    {
        public int money;
        public Vector3 playerPosition;
    }

    public static class SaveSystem
    {
        private static string PathFor(string slot) =>
            Path.Combine(Application.persistentDataPath, slot + ".json");

        public static void Save(SaveData data, string slot = "save0")
        {
            File.WriteAllText(PathFor(slot), JsonUtility.ToJson(data, true));
        }

        public static SaveData Load(string slot = "save0")
        {
            string path = PathFor(slot);
            if (!File.Exists(path)) return new SaveData();
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        }

        public static bool Exists(string slot = "save0") => File.Exists(PathFor(slot));

        /// <summary>Speichert beliebige JsonUtility-kompatible Daten unter persistentDataPath/fileName.json.</summary>
        public static void SaveJson<T>(T data, string fileName)
        {
            File.WriteAllText(PathFor(fileName), JsonUtility.ToJson(data, true));
        }

        /// <summary>Lädt Daten; gibt false zurück, wenn die Datei fehlt oder ungültig ist.</summary>
        public static bool TryLoadJson<T>(string fileName, out T data)
        {
            data = default;
            string path = PathFor(fileName);
            if (!File.Exists(path)) return false;
            try
            {
                data = JsonUtility.FromJson<T>(File.ReadAllText(path));
                return data != null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CrimeSim] Speicherdatei ungültig: " + path + "\n" + e.Message);
                return false;
            }
        }
    }
}
