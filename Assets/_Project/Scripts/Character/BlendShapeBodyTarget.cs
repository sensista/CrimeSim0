using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimeSim
{
    /// <summary>
    /// Setzt Blendshapes (Shape Keys aus MPFB/Blender) auf SkinnedMeshRenderern anhand von
    /// Fett, Muskel und Creator-Werten. Zuordnung über Blendshape-Namen, Gewichte 0-100.
    /// </summary>
    public class BlendShapeBodyTarget : MonoBehaviour, IBodyShapeTarget, ICharacterAppearanceTarget
    {
        public enum BodyParameter { BodyFat, Muscle, Build, Height, HeadSize }

        [Serializable]
        public class Mapping
        {
            [Tooltip("Exakter Blendshape-Name, z. B. 'fat_high' oder 'muscle_high'.")]
            public string blendShapeName;
            public BodyParameter parameter;
            [Tooltip("Normierter Wert (0-1) => Gewicht. X: Parameterwert 0-1, Y: Gewicht 0-100.")]
            public AnimationCurve weightCurve = AnimationCurve.Linear(0f, 0f, 1f, 100f);
        }

        [Tooltip("Leer = alle SkinnedMeshRenderer in den Kindern.")]
        [SerializeField] private SkinnedMeshRenderer[] renderers;
        [SerializeField] private List<Mapping> mappings = new List<Mapping>
        {
            new Mapping { blendShapeName = "fat", parameter = BodyParameter.BodyFat },
            new Mapping { blendShapeName = "muscle", parameter = BodyParameter.Muscle },
        };
        [SerializeField] private bool logMissingBlendShapes = true;

        // Normierungsbereiche der Creator-Werte (wie in CharacterAppearance)
        private static readonly Vector2 BuildRange = new Vector2(0.8f, 1.25f);
        private static readonly Vector2 HeightRange = new Vector2(0.85f, 1.15f);
        private static readonly Vector2 HeadRange = new Vector2(0.85f, 1.2f);

        private readonly float[] values = new float[5];
        private bool initialized;

        private void Awake() => Init();

        private void Init()
        {
            if (initialized) return;
            initialized = true;
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            values[(int)BodyParameter.Build] = 0.5f;
            values[(int)BodyParameter.Height] = 0.5f;
            values[(int)BodyParameter.HeadSize] = 0.5f;

            if (!logMissingBlendShapes) return;
            foreach (var m in mappings)
            {
                bool found = false;
                foreach (var r in renderers)
                    if (r != null && r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex(m.blendShapeName) >= 0) found = true;
                if (!found) Debug.LogWarning("[CrimeSim] Blendshape nicht gefunden: " + m.blendShapeName, this);
            }
        }

        public void SetBodyShape(float bodyFat, float muscle)
        {
            Init();
            values[(int)BodyParameter.BodyFat] = Mathf.Clamp01(bodyFat);
            values[(int)BodyParameter.Muscle] = Mathf.Clamp01(muscle);
            ApplyWeights();
        }

        public void SetAppearance(CharacterAppearance appearance)
        {
            if (appearance == null) return;
            Init();
            values[(int)BodyParameter.Build] = Mathf.InverseLerp(BuildRange.x, BuildRange.y, appearance.build);
            values[(int)BodyParameter.Height] = Mathf.InverseLerp(HeightRange.x, HeightRange.y, appearance.heightScale);
            values[(int)BodyParameter.HeadSize] = Mathf.InverseLerp(HeadRange.x, HeadRange.y, appearance.headSize);
            ApplyWeights();
        }

        private void ApplyWeights()
        {
            foreach (var r in renderers)
            {
                if (r == null || r.sharedMesh == null) continue;
                foreach (var m in mappings)
                {
                    if (string.IsNullOrEmpty(m.blendShapeName)) continue;
                    int index = r.sharedMesh.GetBlendShapeIndex(m.blendShapeName);
                    if (index < 0) continue;
                    float w = m.weightCurve != null ? m.weightCurve.Evaluate(values[(int)m.parameter]) : values[(int)m.parameter] * 100f;
                    r.SetBlendShapeWeight(index, Mathf.Clamp(w, 0f, 100f));
                }
            }
        }
    }
}
