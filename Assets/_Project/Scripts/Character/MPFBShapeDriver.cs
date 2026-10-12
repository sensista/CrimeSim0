using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrimeSim
{
    /// <summary>
    /// Steuert die MPFB-Spielerfigur (PlayerBase.fbx) über die Shape Keys aus PlayerBase.shapes.json.
    /// - Makros: gender, age, muscle, weight, proportions, cupsize, firmness, african/asian/caucasian, height
    /// - Modifier: ein Regler je Paar (decr/incr) im Bereich -1..1, einzelne Targets 0..1
    /// - Gender × Muskel × Gewicht: trilineare Mischung der 26 Gitter-Shapes (Mitte = Basis)
    /// - Skelett folgt über Knochen-Offsets (inkl. Bindpose-Korrektur), Größe über Uniform-Scale
    /// Gilt für Body und alle angehängten SkinnedMeshRenderer (Augen, Brauen, Wimpern, Zähne, Unterwäsche).
    /// </summary>
    [DisallowMultipleComponent]
    public class MPFBShapeDriver : MonoBehaviour, IBodyShapeTarget, ICharacterAppearanceTarget
    {
        public static readonly string[] MacroIds =
            { "gender", "age", "muscle", "weight", "height", "proportions", "cupsize", "firmness", "african", "asian", "caucasian" };

        [SerializeField] private TextAsset manifestJson;
        [Tooltip("Wird für die Körpergröße uniform skaliert. Leer = dieses Objekt.")]
        [SerializeField] private Transform modelRoot;
        [Tooltip("Optional: wird jedes Frame auf Augenhöhe am head-Knochen gesetzt.")]
        [SerializeField] private Transform headAnchor;
        [SerializeField] private Vector3 headAnchorOffset = new Vector3(0f, 0.07f, 0.09f);
        [Tooltip("Knochen den Körperformen folgen lassen (Geschlecht, Proportionen, Gewicht ...).")]
        [SerializeField] private bool adjustSkeleton = true;
        [Tooltip("CharacterController im Parent an die Körpergröße anpassen.")]
        [SerializeField] private bool fitCharacterController = true;
        [SerializeField] private string bodyRendererName = "Body";
        [Tooltip("Sichtbar bei gender < 0.5 (weiblich).")]
        [SerializeField] private string[] femaleOnly = { "Underwear_Panties", "Underwear_Top" };
        [Tooltip("Sichtbar bei gender >= 0.5 (männlich).")]
        [SerializeField] private string[] maleOnly = { "Underwear_Briefs" };
        [SerializeField] private Color skinColor = new Color(0.87f, 0.69f, 0.55f);
        [Tooltip("Optional: Hauttextur für weibliche Figuren (gender < 0.5). Leer = Material-Textur.")]
        [SerializeField] private Texture femaleSkinTexture;
        [Tooltip("Optional: Hauttextur für männliche Figuren (gender >= 0.5). Leer = Material-Textur.")]
        [SerializeField] private Texture maleSkinTexture;
        [Tooltip("Form-Detail Normal Map weiblich/männlich (gebacken aus der jeweiligen Standardform).")]
        [SerializeField] private Texture femaleNormalMap;
        [SerializeField] private Texture maleNormalMap;
        [Tooltip("Detail-Map (R Kavität, G Lippen) weiblich/männlich.")]
        [SerializeField] private Texture femaleDetailMap;
        [SerializeField] private Texture maleDetailMap;

        public MPFBManifest Manifest { get; private set; }
        public bool IsReady => Manifest != null;
        public float CurrentHeight { get; private set; }
        public event Action OnApplied;

        private readonly Dictionary<string, float> values = new Dictionary<string, float>();
        private float[] weights;           // je Manifest-Shape 0..1
        private bool dirty = true;
        private bool initialized;

        private class RendererMap
        {
            public SkinnedMeshRenderer r;
            public int[] index;            // Manifest-Shape => Blendshape-Index (-1 = fehlt)
            public float[] last;
            public Mesh ownMesh;           // Kopie für eigene Bindposes
        }
        private readonly List<RendererMap> maps = new List<RendererMap>();
        private SkinnedMeshRenderer body;

        // Skelett
        private Transform[] bones;                 // nach Tiefe sortiert (Eltern zuerst)
        private Vector3[] restLocalPos;
        private Quaternion[] restLocalRot;
        private Vector3[] restModelPos;            // Knochenkopf im modelRoot-Raum (ohne Skalierung)
        private Vector3[] targetLocalPos;
        private Dictionary<string, int> boneIndex;
        private int[][] shapeBoneIdx;               // je Shape: Knochenindizes
        private Vector3[][] shapeBoneOff;           // je Shape: Offsets in Unity-Modellraum
        private Vector3[] boneOffset;
        private Transform headBone;
        private Func<Vector3, Vector3> convert = MPFBManifest.BlenderToUnity;
        private bool skeletonValid;
        private CharacterController controller;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private int lastSkinGender = -1;
        private static readonly int HideMaskId = Shader.PropertyToID("_HideMask");
        private Vector4 extraHide = Vector4.zero;

        /// <summary>Kleidungssystem: Körperbereich (Kanal 0..3 der Body-Hide-Maske) zusätzlich ausblenden.</summary>
        public void SetBodyHide(int channel, bool hidden)
        {
            if (channel < 0 || channel > 2) return;
            extraHide[channel] = hidden ? 1f : 0f;
            ApplySkinColor();
        } // -1 unbekannt, 0 weiblich, 1 männlich

        // ------------------------------------------------------------ öffentliche API

        public static float DefaultValue(string id)
        {
            switch (id)
            {
                case "african": case "asian": case "caucasian": return 1f / 3f;
                case "gender": case "age": case "muscle": case "weight": case "height":
                case "proportions": case "cupsize": case "firmness": return 0.5f;
                // Stil-Standard: etwas größere Augen, vollere, geschlossene Lippen
                case "l-eye-scale-decr-incr": case "r-eye-scale-decr-incr": return 0.3f;
                case "mouth-upperlip-volume-decr-incr": return 0f;
                case "mouth-lowerlip-volume-decr-incr": return 0f;
                default: return 0f;
            }
        }

        /// <summary>
        /// Zusätzliche Standardwerte für weibliche Gesichter (feiner, klar erwachsen ~25).
        /// Werden mit der Weiblichkeit (1 - gender) überblendet; Männer bleiben unverändert.
        /// Liste auch in Tools/MPFB/female_face_defaults.json.
        /// </summary>
        public static float FemaleDefaultOffset(string id)
        {
            switch (id)
            {
                case "chin-width-decr-incr": return -0.4f;
                case "chin-prominent-decr-incr": return -0.25f;
                case "chin-height-decr-incr": return -0.15f;
                case "chin-bones-decr-incr": return -0.3f;
                case "head-oval": return 0.4f;
                case "l-cheek-bones-decr-incr": return 0.35f;
                case "r-cheek-bones-decr-incr": return 0.35f;
                case "l-cheek-trans-down-up": return 0.2f;
                case "r-cheek-trans-down-up": return 0.2f;
                case "l-eye-scale-decr-incr": return 0.25f;
                case "r-eye-scale-decr-incr": return 0.25f;
                case "l-eye-corner2-down-up": return 0.25f;
                case "r-eye-corner2-down-up": return 0.25f;
                case "nose-scale-horiz-decr-incr": return -0.3f;
                case "nose-width1-decr-incr": return -0.25f;
                case "nose-point-width-decr-incr": return -0.35f;
                case "nose-volume-decr-incr": return -0.15f;
                case "eyebrows-trans-backward-forward": return -0.3f;
                case "forehead-trans-backward-forward": return -0.15f;
                case "mouth-cupidsbow-decr-incr": return 0.3f;
                case "mouth-upperlip-volume-decr-incr": return -0.05f;
                case "neck-scale-horiz-decr-incr": return -0.35f;
                case "neck-scale-depth-decr-incr": return -0.2f;
                case "neck-scale-vert-decr-incr": return 0.15f;
                case "neck-double-decr-incr": return -0.3f;
                case "l-cheek-volume-decr-incr": return 0.05f;
                case "r-cheek-volume-decr-incr": return 0.05f;
                case "l-upperarm-shoulder-muscle-decr-incr": return -0.3f;
                case "r-upperarm-shoulder-muscle-decr-incr": return -0.3f;
                case "measure-neck-height-decr-incr": return 0.15f;
                case "l-eye-bag-decr-incr": return -0.3f;
                case "r-eye-bag-decr-incr": return -0.3f;
                case "l-eye-height2-decr-incr": return 0.15f;
                case "r-eye-height2-decr-incr": return 0.15f;
                case "nose-nostrils-width-decr-incr": return -0.3f;
                case "nose-scale-vert-decr-incr": return -0.1f;
                case "nose-point-down-up": return 0.15f;
                case "mouth-angles-down-up": return 0.15f;
                case "mouth-laugh-lines-in-out": return -0.3f;
                case "eyebrows-trans-down-up": return 0.1f;
                case "eyebrows-angle-down-up": return 0.15f;
                case "head-scale-horiz-decr-incr": return -0.08f;
                case "head-invertedtriangular": return 0.25f;
                case "head-fat-decr-incr": return -0.2f;
                case "head-age-decr-incr": return -0.15f;
                case "mouth-lowerlip-volume-decr-incr": return -0.15f;
                case "mouth-scale-horiz-decr-incr": return -0.05f;
                default: return 0f;
            }
        }

        public float GetValue(string id)
        {
            if (values.TryGetValue(id, out var v)) return v;
            float d = DefaultValue(id);
            if (id == "gender") return d;
            float off = FemaleDefaultOffset(id);
            if (off == 0f) return d;
            float g = values.TryGetValue("gender", out var gv) ? gv : DefaultValue("gender");
            return d + off * (1f - Mathf.Clamp01(g / 0.5f)); // voll bei g=0, 0 ab g=0.5
        }

        public void SetValue(string id, float value)
        {
            values[id] = value;
            dirty = true;
        }

        public Dictionary<string, float> Snapshot() => new Dictionary<string, float>(values);

        public void Restore(Dictionary<string, float> snapshot)
        {
            values.Clear();
            if (snapshot != null) foreach (var kv in snapshot) values[kv.Key] = kv.Value;
            dirty = true;
        }

        public void ResetAll() { values.Clear(); dirty = true; }

        public Color SkinColor
        {
            get => skinColor;
            set { skinColor = value; ApplySkinColor(); }
        }

        public void SetHeadAnchor(Transform anchor) => headAnchor = anchor;

        /// <summary>Werte in eine Appearance schreiben (für character.json). weight/muscle kommen aus BodyStats.</summary>
        public void WriteTo(CharacterAppearance appearance)
        {
            if (appearance == null) return;
            appearance.mpfb = new List<MPFBValue>();
            foreach (var kv in values)
            {
                if (kv.Key == "weight" || kv.Key == "muscle") continue;
                if (Mathf.Abs(kv.Value - DefaultValue(kv.Key)) < 0.0005f) continue;
                appearance.mpfb.Add(new MPFBValue(kv.Key, kv.Value));
            }
            appearance.skinColor = skinColor;
        }

        // IBodyShapeTarget: Fett/Muskel aus BodyCondition => MPFB weight/muscle
        public void SetBodyShape(float bodyFat, float muscle)
        {
            values["weight"] = Mathf.Clamp01(bodyFat);
            values["muscle"] = Mathf.Clamp01(muscle);
            dirty = true;
        }

        // ICharacterAppearanceTarget
        public void SetAppearance(CharacterAppearance appearance)
        {
            if (appearance == null) return;
            float w = GetValue("weight"), m = GetValue("muscle");
            values.Clear();
            values["weight"] = w;
            values["muscle"] = m;
            if (appearance.mpfb != null)
                foreach (var v in appearance.mpfb)
                    if (!string.IsNullOrEmpty(v.id) && v.id != "weight" && v.id != "muscle") values[v.id] = v.value;
            skinColor = appearance.skinColor;
            ApplySkinColor();
            dirty = true;
        }

        /// <summary>Sofort anwenden (sonst automatisch in LateUpdate).</summary>
        public void ApplyNow()
        {
            Init();
            if (!IsReady) return;
            Apply();
        }

        // ------------------------------------------------------------ Lebenszyklus

        private void Awake() => Init();

        private void Init()
        {
            if (initialized) return;
            initialized = true;
            if (modelRoot == null) modelRoot = transform;
            Manifest = MPFBManifest.Load(manifestJson);
            if (Manifest == null)
            {
                Debug.LogWarning("[CrimeSim] MPFBShapeDriver: kein Manifest (PlayerBase.shapes.json) gesetzt.", this);
                return;
            }
            weights = new float[Manifest.shapes.Count];
            mpb = new MaterialPropertyBlock();
            controller = fitCharacterController ? GetComponentInParent<CharacterController>() : null;

            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                var map = new RendererMap { r = r, index = new int[Manifest.shapes.Count], last = new float[Manifest.shapes.Count] };
                for (int i = 0; i < Manifest.shapes.Count; i++)
                {
                    map.index[i] = r.sharedMesh.GetBlendShapeIndex(Manifest.shapes[i].name);
                    map.last[i] = -1f;
                }
                maps.Add(map);
                if (r.name == bodyRendererName) body = r;
            }
            InitSkeleton();
            ApplySkinColor();
        }

        private void LateUpdate()
        {
            if (!IsReady) return;
            if (dirty) Apply();
            if (adjustSkeleton && skeletonValid)
                for (int i = 0; i < bones.Length; i++) bones[i].localPosition = targetLocalPos[i];
            if (headAnchor != null && headBone != null)
            {
                float s = modelRoot.lossyScale.y;
                headAnchor.position = headBone.position + modelRoot.rotation * (headAnchorOffset * s);
            }
        }

        private void OnDestroy()
        {
            foreach (var m in maps)
                if (m.ownMesh != null) Destroy(m.ownMesh);
        }

        // ------------------------------------------------------------ Gewichte

        private static void Axis3(float v, out float w0, out float w05, out float w1)
        {
            v = Mathf.Clamp01(v);
            if (v <= 0.5f) { w0 = 1f - 2f * v; w05 = 2f * v; w1 = 0f; }
            else { w0 = 0f; w05 = 2f - 2f * v; w1 = 2f * v - 1f; }
        }

        private static float AxisWeight(float point, float w0, float w05, float w1) =>
            point < 0.25f ? w0 : point < 0.75f ? w05 : w1;

        private void ComputeWeights()
        {
            float g = GetValue("gender"), age = GetValue("age"), mu = GetValue("muscle"), we = GetValue("weight");
            float cup = GetValue("cupsize"), firm = GetValue("firmness"), prop = GetValue("proportions");
            float ra = Mathf.Max(0f, GetValue("african")), rs = Mathf.Max(0f, GetValue("asian")), rc = Mathf.Max(0f, GetValue("caucasian"));
            float rsum = ra + rs + rc;
            if (rsum < 1e-4f) { ra = rs = rc = 1f / 3f; rsum = 1f; }
            ra /= rsum; rs /= rsum; rc /= rsum;

            Axis3(g, out float g0, out float g05, out float g1);
            Axis3(mu, out float m0, out float m05, out float m1);
            Axis3(we, out float w0, out float w05, out float w1);
            Axis3(cup, out float c0, out float c05, out float c1);
            Axis3(firm, out float f0, out float f05, out float f1);
            // Alter: 0.5 = 25 J. (Basis), 0.75 ≈ 57 J., 1 = 90 J. Jünger als 25 wird nicht unterstützt.
            float a = Mathf.Clamp(age, 0.5f, 1f);
            float a75 = a <= 0.75f ? (a - 0.5f) / 0.25f : (1f - a) / 0.25f;
            float a100 = a <= 0.75f ? 0f : (a - 0.75f) / 0.25f;
            float female = Mathf.Clamp01(1f - g); // Brust-Shapes sind am weiblichen Körper gebacken

            var shapes = Manifest.shapes;
            for (int i = 0; i < shapes.Count; i++)
            {
                var s = shapes[i];
                float w = 0f;
                switch (s.type)
                {
                    case "macro_grid":
                        w = AxisWeight(s.gridGender, g0, g05, g1) * AxisWeight(s.gridMuscle, m0, m05, m1) * AxisWeight(s.gridWeight, w0, w05, w1);
                        break;
                    case "macro_age":
                        w = (s.age < 0.9f ? a75 : a100) * AxisWeight(s.gender, g0, g05, g1);
                        break;
                    case "macro_race":
                        float r = s.race == "african" ? ra : s.race == "asian" ? rs : rc;
                        w = r * (s.gender < 0.5f ? 1f - Mathf.Clamp01(g) : Mathf.Clamp01(g));
                        break;
                    case "macro_breast":
                        w = AxisWeight(s.cupsize, c0, c05, c1) * AxisWeight(s.firmness, f0, f05, f1) * female;
                        break;
                    case "macro_proportions":
                        w = s.value < 0.5f ? Mathf.Clamp01((0.5f - prop) * 2f) : Mathf.Clamp01((prop - 0.5f) * 2f);
                        break;
                }
                weights[i] = w;
            }

            // Modifier
            foreach (var sl in Manifest.sliders)
            {
                float v = GetValue(sl.id);
                if (sl.Bipolar)
                {
                    v = Mathf.Clamp(v, -1f, 1f);
                    weights[sl.negative.index] = v < 0f ? -v : 0f;
                    weights[sl.positive.index] = v > 0f ? v : 0f;
                }
                else if (sl.positive != null)
                    weights[sl.positive.index] = Mathf.Clamp01(v);
            }
        }

        private void Apply()
        {
            dirty = false;
            ComputeWeights();

            foreach (var map in maps)
            {
                if (map.r == null) continue;
                for (int i = 0; i < weights.Length; i++)
                {
                    int bi = map.index[i];
                    if (bi < 0) continue;
                    float w = weights[i] * 100f;
                    if (Mathf.Abs(map.last[i] - w) < 0.01f) continue;
                    map.r.SetBlendShapeWeight(bi, w);
                    map.last[i] = w;
                }
            }

            // Größe über Uniform-Scale (Höhe ist bewusst kein Blendshape)
            float hv = Mathf.Clamp01(GetValue("height"));
            float h = hv < 0.5f
                ? Mathf.Lerp(Manifest.minHeight, Manifest.neutralHeight, hv * 2f)
                : Mathf.Lerp(Manifest.neutralHeight, Manifest.maxHeight, hv * 2f - 1f);
            float scale = h / Manifest.neutralHeight;
            if (modelRoot != null) modelRoot.localScale = Vector3.one * scale;
            CurrentHeight = h;

            if (adjustSkeleton && skeletonValid) UpdateSkeleton();

            if (controller != null)
            {
                float ch = Mathf.Max(1f, h + 0.05f);
                if (Mathf.Abs(controller.height - ch) > 0.005f)
                {
                    controller.height = ch;
                    controller.center = new Vector3(controller.center.x, ch * 0.5f, controller.center.z);
                    controller.radius = Mathf.Clamp(0.2f * scale, 0.25f, 0.45f);
                }
            }

            bool male = GetValue("gender") >= 0.5f;
            if (lastSkinGender != (male ? 1 : 0))
            {
                lastSkinGender = male ? 1 : 0;
                ApplySkinColor();
            }
            foreach (var map in maps)
            {
                if (map.r == null) continue;
                if (Array.IndexOf(femaleOnly, map.r.name) >= 0) map.r.enabled = !male;
                else if (Array.IndexOf(maleOnly, map.r.name) >= 0) map.r.enabled = male;
            }
            OnApplied?.Invoke();
        }

        private void ApplySkinColor()
        {
            if (body == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            body.GetPropertyBlock(mpb);
            mpb.SetColor(BaseColorId, skinColor);
            mpb.SetColor(ColorId, skinColor);
            Texture skinTex = GetValue("gender") >= 0.5f ? maleSkinTexture : femaleSkinTexture;
            if (skinTex != null)
            {
                Material m = body.sharedMaterial;
                if (m != null && m.HasProperty(BaseMapId)) mpb.SetTexture(BaseMapId, skinTex);
                if (m != null && m.HasProperty(MainTexId)) mpb.SetTexture(MainTexId, skinTex);
            }
            bool male = GetValue("gender") >= 0.5f;
            Texture nrm = male ? maleNormalMap : femaleNormalMap;
            Texture det = male ? maleDetailMap : femaleDetailMap;
            if (nrm != null) mpb.SetTexture("_BumpMap", nrm);
            if (det != null) mpb.SetTexture("_DetailMap", det);
            // Unterwäsche: Körper darunter ausblenden (R Höschen, G Top, B Slip)
            Vector4 hide = GetValue("gender") >= 0.5f ? new Vector4(0f, 0f, 1f, 0f) : new Vector4(1f, 1f, 0f, 0f);
            hide = Vector4.Max(hide, extraHide);
            mpb.SetVector(HideMaskId, hide);
            body.SetPropertyBlock(mpb);
        }

        // ------------------------------------------------------------ Skelett

        private void InitSkeleton()
        {
            skeletonValid = false;
            var all = new Dictionary<string, Transform>();
            foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                if (!all.ContainsKey(t.name)) all[t.name] = t;

            var list = new List<Transform>();
            foreach (var name in Manifest.boneHeadsNeutral.Keys)
                if (all.TryGetValue(name, out var t)) list.Add(t);
            if (list.Count < 20)
            {
                Debug.LogWarning("[CrimeSim] MPFBShapeDriver: Skelett nicht gefunden – nur Blendshapes aktiv.", this);
                return;
            }
            list.Sort((x, y) => Depth(x).CompareTo(Depth(y)));
            bones = list.ToArray();
            all.TryGetValue("head", out headBone);

            int n = bones.Length;
            restLocalPos = new Vector3[n];
            restLocalRot = new Quaternion[n];
            restModelPos = new Vector3[n];
            targetLocalPos = new Vector3[n];
            boneOffset = new Vector3[n];
            boneIndex = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                restLocalPos[i] = bones[i].localPosition;
                restLocalRot[i] = bones[i].localRotation;
                targetLocalPos[i] = restLocalPos[i];
                Vector3 p = modelRoot.InverseTransformPoint(bones[i].position);
                restModelPos[i] = p;
                boneIndex[bones[i].name] = i;
            }

            // Umrechnung Blender => Unity prüfen: Kandidaten gegen die importierten Knochenköpfe testen
            Func<Vector3, Vector3>[] candidates =
            {
                MPFBManifest.BlenderToUnity,
                b => new Vector3(b.x, b.z, b.y),
                b => new Vector3(b.x, b.z, -b.y),
                b => new Vector3(-b.x, b.z, b.y),
            };
            string[] names = { "(-x, z, -y)", "(x, z, y)", "(x, z, -y)", "(-x, z, y)" };
            float best = float.MaxValue; int bestIdx = 0;
            for (int c = 0; c < candidates.Length; c++)
            {
                float err = 0f; int cnt = 0;
                foreach (var kv in Manifest.boneHeadsNeutral)
                {
                    if (!boneIndex.TryGetValue(kv.Key, out int bi)) continue;
                    err = Mathf.Max(err, (candidates[c](kv.Value) - restModelPos[bi]).magnitude);
                    cnt++;
                }
                if (cnt > 0 && err < best) { best = err; bestIdx = c; }
            }
            convert = candidates[bestIdx];
            if (best > 0.03f)
            {
                Debug.LogWarning($"[CrimeSim] MPFBShapeDriver: Knochen passen nicht zum Manifest (Fehler {best * 100f:0.0} cm). Skelett-Anpassung aus.", this);
                return;
            }
            if (bestIdx != 0)
                Debug.LogWarning("[CrimeSim] MPFBShapeDriver: unerwartete Achsen-Umrechnung " + names[bestIdx] + " wird verwendet.", this);
            else
                Debug.Log($"[CrimeSim] MPFB-Skelett geprüft: Umrechnung {names[0]}, max. Abweichung {best * 1000f:0.0} mm.", this);

            shapeBoneIdx = new int[Manifest.shapes.Count][];
            shapeBoneOff = new Vector3[Manifest.shapes.Count][];
            for (int i = 0; i < Manifest.shapes.Count; i++)
            {
                var jo = Manifest.shapes[i].jointOffsets;
                var idx = new List<int>(); var off = new List<Vector3>();
                foreach (var kv in jo)
                    if (boneIndex.TryGetValue(kv.Key, out int bi)) { idx.Add(bi); off.Add(convert(kv.Value)); }
                shapeBoneIdx[i] = idx.ToArray();
                shapeBoneOff[i] = off.ToArray();
            }

            // Eigene Mesh-Kopien, damit Bindposes angepasst werden können (nur zur Laufzeit)
            if (Application.isPlaying)
                foreach (var m in maps)
                {
                    m.ownMesh = Instantiate(m.r.sharedMesh);
                    m.ownMesh.name = m.r.sharedMesh.name + " (MPFB)";
                    m.r.sharedMesh = m.ownMesh;
                }
            skeletonValid = true;
        }

        private static int Depth(Transform t)
        {
            int d = 0;
            while (t.parent != null) { d++; t = t.parent; }
            return d;
        }

        private void UpdateSkeleton()
        {
            int n = bones.Length;
            for (int i = 0; i < n; i++) boneOffset[i] = Vector3.zero;
            for (int s = 0; s < weights.Length; s++)
            {
                float w = weights[s];
                if (w <= 0f) continue;
                var idx = shapeBoneIdx[s];
                var off = shapeBoneOff[s];
                for (int k = 0; k < idx.Length; k++) boneOffset[idx[k]] += off[k] * w;
            }

            // Ruhepose herstellen (aktuelle Animation sichern), Köpfe verschieben, Bindposes neu berechnen
            var savedRot = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                savedRot[i] = bones[i].localRotation;
                bones[i].localRotation = restLocalRot[i];
                bones[i].localPosition = restLocalPos[i];
            }
            for (int i = 0; i < n; i++)
            {
                bones[i].position = modelRoot.TransformPoint(restModelPos[i] + boneOffset[i]);
                targetLocalPos[i] = bones[i].localPosition;
            }
            if (Application.isPlaying)
            {
                foreach (var m in maps)
                {
                    if (m.ownMesh == null || m.r == null) continue;
                    var rb = m.r.bones;
                    var bp = m.ownMesh.bindposes;
                    if (rb == null || bp.Length != rb.Length) continue;
                    Matrix4x4 meshToWorld = m.r.transform.localToWorldMatrix;
                    for (int b = 0; b < rb.Length; b++)
                        if (rb[b] != null) bp[b] = rb[b].worldToLocalMatrix * meshToWorld;
                    m.ownMesh.bindposes = bp;
                }
            }
            for (int i = 0; i < n; i++) bones[i].localRotation = savedRot[i];
        }
    }
}
