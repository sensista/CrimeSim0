using UnityEngine;

namespace CrimeSim
{
    /// <summary>Feste Merkmale aus dem Character Creator (ändern sich im Spiel nicht von selbst).</summary>
    [System.Serializable]
    public class CharacterAppearance
    {
        public string characterName = "Neuer Charakter";
        [Range(0.85f, 1.15f)] public float heightScale = 1f;
        [Range(0.8f, 1.25f)] public float build = 1f;      // Grundstatur (Knochenbau)
        [Range(0.85f, 1.2f)] public float headSize = 1f;
        public int faceStyle;                               // Platzhalter für spätere Gesichter
        public Color skinColor = new Color(0.87f, 0.69f, 0.55f);
        public int hairStyle = 1;
        public Color hairColor = new Color(0.2f, 0.13f, 0.08f);
        public int topStyle;
        public Color topColor = new Color(0.2f, 0.35f, 0.6f);
        public int bottomStyle;
        public Color bottomColor = new Color(0.15f, 0.15f, 0.2f);
        public int shoesStyle;
        public Color shoesColor = new Color(0.1f, 0.1f, 0.1f);

        public CharacterAppearance Clone() => (CharacterAppearance)MemberwiseClone();
    }

    /// <summary>Veränderliche Körperwerte (Essen, Training, Aktivität wirken später hierauf).</summary>
    [System.Serializable]
    public struct BodyStats
    {
        [Range(0f, 1f)] public float bodyFat;
        [Range(0f, 1f)] public float muscle;

        public BodyStats(float bodyFat, float muscle)
        {
            this.bodyFat = Mathf.Clamp01(bodyFat);
            this.muscle = Mathf.Clamp01(muscle);
        }

        public BodyStats Clamped() => new BodyStats(bodyFat, muscle);
        public static BodyStats Default => new BodyStats(0.3f, 0.3f);
    }

    /// <summary>Inhalt von character.json.</summary>
    [System.Serializable]
    public class CharacterSaveData
    {
        public CharacterAppearance appearance = new CharacterAppearance();
        public BodyStats body = BodyStats.Default;

        public const string FileName = "character";

        public static CharacterSaveData LoadOrDefault()
        {
            if (SaveSystem.TryLoadJson(FileName, out CharacterSaveData data))
            {
                if (data.appearance == null) data.appearance = new CharacterAppearance();
                data.body = data.body.Clamped();
                return data;
            }
            return new CharacterSaveData();
        }

        public void Save() => SaveSystem.SaveJson(this, FileName);
    }

    /// <summary>
    /// Ziel für Körperform-Werte. Der Primitive-Rig skaliert Teile; echte Meshes können
    /// stattdessen Blendshapes setzen (z. B. SkinnedMeshRenderer.SetBlendShapeWeight).
    /// </summary>
    public interface IBodyShapeTarget
    {
        void SetBodyShape(float bodyFat, float muscle);
    }

    /// <summary>Ziel für feste Creator-Werte (z. B. Statur/Größe als Blendshapes am echten Mesh).</summary>
    public interface ICharacterAppearanceTarget
    {
        void SetAppearance(CharacterAppearance appearance);
    }
}
