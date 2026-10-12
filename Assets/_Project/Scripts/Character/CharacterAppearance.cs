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

        [Tooltip("MPFB-Reglerwerte (Makros wie gender/age/height und Modifier wie nose-scale-vert-decr-incr).")]
        public System.Collections.Generic.List<MPFBValue> mpfb = new System.Collections.Generic.List<MPFBValue>();

        public CharacterAppearance Clone()
        {
            var c = (CharacterAppearance)MemberwiseClone();
            c.mpfb = new System.Collections.Generic.List<MPFBValue>();
            if (mpfb != null) foreach (var v in mpfb) c.mpfb.Add(new MPFBValue(v.id, v.value));
            return c;
        }

        public bool TryGetMPFB(string id, out float value)
        {
            if (mpfb != null)
                foreach (var v in mpfb)
                    if (v.id == id) { value = v.value; return true; }
            value = 0f;
            return false;
        }

        public void SetMPFB(string id, float value)
        {
            if (mpfb == null) mpfb = new System.Collections.Generic.List<MPFBValue>();
            foreach (var v in mpfb)
                if (v.id == id) { v.value = value; return; }
            mpfb.Add(new MPFBValue(id, value));
        }
    }

    [System.Serializable]
    public class MPFBValue
    {
        public string id;
        public float value;
        public MPFBValue() { }
        public MPFBValue(string id, float value) { this.id = id; this.value = value; }
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
        public const int CurrentVersion = 2;
        [Tooltip("1 = Platzhalter-Rig, 2 = MPFB-Regler (appearance.mpfb).")]
        public int version; // fehlt in alten Dateien => 0
        public CharacterAppearance appearance = new CharacterAppearance();
        public BodyStats body = BodyStats.Default;

        public const string FileName = "character";

        /// <summary>Alte Dateien (ohne version-Feld => 0) auf das MPFB-Format heben.</summary>
        public void Migrate()
        {
            if (appearance == null) appearance = new CharacterAppearance();
            if (version >= CurrentVersion) return;
            // Größe: heightScale bezog sich auf ~1,8 m. MPFB-Höhe: 0 = 1,30 m, 0,5 = 1,66 m, 1 = 2,37 m (Manifest)
            if (!appearance.TryGetMPFB("height", out _))
            {
                float h = 1.8f * appearance.heightScale;
                float v = h < 1.66f ? 0.5f * Mathf.InverseLerp(1.30f, 1.66f, h) : 0.5f + 0.5f * Mathf.InverseLerp(1.66f, 2.37f, h);
                appearance.SetMPFB("height", Mathf.Clamp01(v));
            }
            // Statur (Knochenbau) grob auf Proportionen abbilden
            if (!appearance.TryGetMPFB("proportions", out _))
                appearance.SetMPFB("proportions", Mathf.Clamp01(Mathf.InverseLerp(0.8f, 1.25f, appearance.build)));
            version = CurrentVersion;
        }

        public static CharacterSaveData LoadOrDefault()
        {
            if (SaveSystem.TryLoadJson(FileName, out CharacterSaveData data))
            {
                data.Migrate();
                data.body = data.body.Clamped();
                return data;
            }
            return new CharacterSaveData { version = CurrentVersion };
        }

        public void Save()
        {
            version = CurrentVersion;
            SaveSystem.SaveJson(this, FileName);
        }
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
