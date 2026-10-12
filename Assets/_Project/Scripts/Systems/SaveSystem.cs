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
    }
}
