using System.Collections.Generic;
using UnityEngine;

namespace CrimeSim
{
    /// <summary>
    /// Baut aus CharacterAppearance + BodyStats einen modularen Platzhalter-Charakter aus Primitives.
    /// Feste Merkmale (Größe, Statur, Haut, Haare, Kleidung) => Rebuild().
    /// Fett/Muskel => SetBodyShape() verändert nur Skalierungen (später durch Blendshapes ersetzbar).
    /// </summary>
    public class CharacterAppearanceApplier : MonoBehaviour, IBodyShapeTarget
    {
        // Anzahl der Platzhalter-Stile pro Slot
        public const int HairStyleCount = 5;   // 0 Glatze, 1 kurz, 2 lang, 3 Irokese, 4 Dutt
        public const int TopStyleCount = 3;    // 0 T-Shirt, 1 Langarm, 2 Tanktop
        public const int BottomStyleCount = 2; // 0 lange Hose, 1 Shorts
        public const int ShoesStyleCount = 3;  // 0 Sneaker, 1 Stiefel, 2 barfuß

        [Tooltip("Optional: echtes Körpermodell (z. B. MPFB-FBX als Humanoid mit BlendShapeBodyTarget). Gesetzt = Primitives werden nicht erzeugt.")]
        [SerializeField] private GameObject bodyModelPrefab;
        [SerializeField] private CharacterPartLibrary library;
        [Tooltip("Optional. Leer = Standardmaterial der Unity-Primitives (passt zur aktiven Render Pipeline).")]
        [SerializeField] private Material baseMaterial;
        [Tooltip("Optional. Wird auf Kopfhöhe des Charakters gesetzt (First-Person-Kamera).")]
        [SerializeField] private Transform headAnchor;
        [SerializeField] private bool loadSavedOnAwake = true;
        [SerializeField] private bool hideOwnRenderer = true;
        [SerializeField] private CharacterAppearance appearance = new CharacterAppearance();
        [SerializeField] private BodyStats body = new BodyStats(0.3f, 0.3f);

        public CharacterAppearance Appearance
        {
            get
            {
                if (MPFB != null) MPFB.WriteTo(appearance);
                return appearance;
            }
        }

        /// <summary>Gesetzt, wenn das MPFB-Spielermodell verwendet wird (sonst Platzhalter-Primitives).</summary>
        public MPFBShapeDriver MPFB { get; private set; }
        public bool HasBodyModel => bodyModelPrefab != null;
        public BodyStats Body => body;
        public CharacterPartLibrary Library => library;

        private enum Part
        {
            Head, Torso, Belly, UpperArmL, UpperArmR, LowerArmL, LowerArmR,
            UpperLegL, UpperLegR, LowerLegL, LowerLegR, FootL, FootR, Count
        }

        private const string RigName = "Rig";
        private Transform rig;
        private readonly Transform[] parts = new Transform[(int)Part.Count];
        private readonly List<IBodyShapeTarget> extraShapeTargets = new List<IBodyShapeTarget>();
        private readonly List<ICharacterAppearanceTarget> appearanceTargets = new List<ICharacterAppearanceTarget>();
        private static MaterialPropertyBlock mpb;
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake()
        {
            if (hideOwnRenderer)
            {
                var own = GetComponent<Renderer>();
                if (own != null) own.enabled = false;
            }
            if (loadSavedOnAwake)
            {
                var data = CharacterSaveData.LoadOrDefault();
                appearance = data.appearance;
                body = data.body;
            }
            Rebuild();
        }

        public void Apply(CharacterAppearance newAppearance)
        {
            appearance = newAppearance ?? new CharacterAppearance();
            if (MPFB != null)
            {
                // MPFB-Modell nicht neu instanziieren (teuer) – nur Werte übernehmen
                MPFB.SetAppearance(appearance);
                MPFB.SetBodyShape(body.bodyFat, body.muscle);
                return;
            }
            Rebuild();
        }

        public void Apply(CharacterAppearance newAppearance, BodyStats newBody)
        {
            body = newBody.Clamped();
            Apply(newAppearance);
        }

        public static int StyleCount(CharacterSlot slot, CharacterPartLibrary lib)
        {
            int baseCount = slot == CharacterSlot.Hair ? HairStyleCount
                : slot == CharacterSlot.Top ? TopStyleCount
                : slot == CharacterSlot.Bottom ? BottomStyleCount
                : ShoesStyleCount;
            return lib != null ? Mathf.Max(baseCount, lib.Count(slot)) : baseCount;
        }

        // ---------------------------------------------------------------- Aufbau

        public void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name != RigName) continue;
                child.name = RigName + "_Old";
                child.gameObject.SetActive(false);
                DestroySafe(child.gameObject);
            }
            extraShapeTargets.Clear();
            appearanceTargets.Clear();
            System.Array.Clear(parts, 0, parts.Length);
            MPFB = null;

            rig = new GameObject(RigName).transform;
            rig.SetParent(transform, false);
            // MPFB-Modell skaliert sich selbst (Regler "height"); Primitives über heightScale
            rig.localScale = bodyModelPrefab != null ? Vector3.one : Vector3.one * appearance.heightScale;

            if (bodyModelPrefab != null)
            {
                var model = Instantiate(bodyModelPrefab, rig, false);
                model.name = bodyModelPrefab.name;
                MPFB = model.GetComponentInChildren<MPFBShapeDriver>();
                if (MPFB != null && headAnchor != null) MPFB.SetHeadAnchor(headAnchor);
                CollectShapeTargets(model);
                BuildHairOnModel(model);
                SetLayerRecursive(rig, gameObject.layer);
                foreach (var t in appearanceTargets) t.SetAppearance(appearance);
                SetBodyShape(body.bodyFat, body.muscle);
                if (MPFB != null) MPFB.ApplyNow();
                return;
            }

            var a = appearance;
            Color skin = a.skinColor;
            Color upperArm = a.topStyle == 2 ? skin : a.topColor;
            Color lowerArm = a.topStyle == 1 ? a.topColor : skin;
            Color lowerLeg = a.bottomStyle == 1 ? skin : a.bottomColor;
            Color foot = a.shoesStyle == 2 ? skin : a.shoesColor;

            parts[(int)Part.Head] = Create(PrimitiveType.Sphere, "Head", rig, skin);
            parts[(int)Part.Torso] = Create(PrimitiveType.Cube, "Torso", rig, a.topColor);
            parts[(int)Part.Belly] = Create(PrimitiveType.Sphere, "Belly", rig, a.topColor);
            parts[(int)Part.UpperArmL] = Create(PrimitiveType.Cube, "UpperArm_L", rig, upperArm);
            parts[(int)Part.UpperArmR] = Create(PrimitiveType.Cube, "UpperArm_R", rig, upperArm);
            parts[(int)Part.LowerArmL] = Create(PrimitiveType.Cube, "LowerArm_L", rig, lowerArm);
            parts[(int)Part.LowerArmR] = Create(PrimitiveType.Cube, "LowerArm_R", rig, lowerArm);
            parts[(int)Part.UpperLegL] = Create(PrimitiveType.Cube, "UpperLeg_L", rig, a.bottomColor);
            parts[(int)Part.UpperLegR] = Create(PrimitiveType.Cube, "UpperLeg_R", rig, a.bottomColor);
            parts[(int)Part.LowerLegL] = Create(PrimitiveType.Cube, "LowerLeg_L", rig, lowerLeg);
            parts[(int)Part.LowerLegR] = Create(PrimitiveType.Cube, "LowerLeg_R", rig, lowerLeg);
            parts[(int)Part.FootL] = Create(PrimitiveType.Cube, "Foot_L", rig, foot);
            parts[(int)Part.FootR] = Create(PrimitiveType.Cube, "Foot_R", rig, foot);

            Transform head = parts[(int)Part.Head];
            // Augen (Gesicht-Platzhalter), Kopf-Lokalraum: Radius 0,5
            Color eye = new Color(0.08f, 0.08f, 0.08f);
            Local(Create(PrimitiveType.Sphere, "Eye_L", head, eye), new Vector3(-0.18f, 0.08f, 0.45f), Vector3.one * 0.12f);
            Local(Create(PrimitiveType.Sphere, "Eye_R", head, eye), new Vector3(0.18f, 0.08f, 0.45f), Vector3.one * 0.12f);

            BuildHair(head);
            AttachLibraryPart(CharacterSlot.Top, a.topStyle, a.topColor, new Vector3(0f, 1.175f, 0f));
            AttachLibraryPart(CharacterSlot.Bottom, a.bottomStyle, a.bottomColor, new Vector3(0f, 0.9f, 0f));
            AttachLibraryPart(CharacterSlot.Shoes, a.shoesStyle, a.shoesColor, Vector3.zero);

            SetLayerRecursive(rig, gameObject.layer);
            foreach (var t in appearanceTargets) t.SetAppearance(appearance);
            SetBodyShape(body.bodyFat, body.muscle);
        }

        /// <summary>Haar-Prefab aus der Bibliothek an den Humanoid-Kopfknochen hängen (falls vorhanden).</summary>
        private void BuildHairOnModel(GameObject model)
        {
            GameObject prefab = library != null ? library.Get(CharacterSlot.Hair, appearance.hairStyle) : null;
            if (prefab == null) return;
            var animator = model.GetComponentInChildren<Animator>();
            Transform headBone = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            var inst = Instantiate(prefab, headBone != null ? headBone : model.transform, false);
            Colorize(inst, appearance.hairColor);
            CollectShapeTargets(inst);
        }

        private void BuildHair(Transform head)
        {
            var a = appearance;
            GameObject prefab = library != null ? library.Get(CharacterSlot.Hair, a.hairStyle) : null;
            if (prefab != null)
            {
                var inst = Instantiate(prefab, head, false);
                Colorize(inst, a.hairColor);
                CollectShapeTargets(inst);
                return;
            }

            Color c = a.hairColor;
            switch (a.hairStyle)
            {
                case 1:
                    Local(Create(PrimitiveType.Sphere, "Hair", head, c), new Vector3(0f, 0.12f, -0.03f), new Vector3(1.06f, 0.85f, 1.08f));
                    break;
                case 2:
                    Local(Create(PrimitiveType.Sphere, "Hair", head, c), new Vector3(0f, 0.12f, -0.03f), new Vector3(1.06f, 0.85f, 1.08f));
                    Local(Create(PrimitiveType.Cube, "Hair_Long", head, c), new Vector3(0f, -0.25f, -0.3f), new Vector3(0.95f, 0.9f, 0.35f));
                    break;
                case 3:
                    Local(Create(PrimitiveType.Cube, "Hair_Mohawk", head, c), new Vector3(0f, 0.45f, 0f), new Vector3(0.18f, 0.35f, 1f));
                    break;
                case 4:
                    Local(Create(PrimitiveType.Sphere, "Hair", head, c), new Vector3(0f, 0.12f, -0.03f), new Vector3(1.06f, 0.85f, 1.08f));
                    Local(Create(PrimitiveType.Sphere, "Hair_Bun", head, c), new Vector3(0f, 0.5f, -0.35f), Vector3.one * 0.45f);
                    break;
                // 0 = keine Haare
            }
        }

        private void AttachLibraryPart(CharacterSlot slot, int index, Color color, Vector3 localPos)
        {
            if (library == null) return;
            GameObject prefab = library.Get(slot, index);
            if (prefab == null) return;
            var inst = Instantiate(prefab, rig, false);
            inst.transform.localPosition = localPos;
            Colorize(inst, color);
            CollectShapeTargets(inst);
        }

        private void CollectShapeTargets(GameObject go)
        {
            foreach (var t in go.GetComponentsInChildren<IBodyShapeTarget>(true))
                extraShapeTargets.Add(t);
            foreach (var t in go.GetComponentsInChildren<ICharacterAppearanceTarget>(true))
                appearanceTargets.Add(t);
        }

        // ---------------------------------------------------------------- Körperform

        /// <summary>Fett/Muskel 0..1 visuell umsetzen (ohne Neuaufbau).</summary>
        public void SetBodyShape(float bodyFat, float muscle)
        {
            body = new BodyStats(bodyFat, muscle);
            foreach (var t in extraShapeTargets) t?.SetBodyShape(body.bodyFat, body.muscle);
            if (rig == null || parts[0] == null) return; // echtes Modell: nur Blendshapes

            float fat = body.bodyFat, mus = body.muscle;
            float w = appearance.build;
            float hs = appearance.headSize;

            float torsoX = 0.36f * w * Mathf.Lerp(0.85f, 1.3f, fat) * Mathf.Lerp(0.92f, 1.25f, mus); // Schultern
            float torsoZ = 0.2f * w * Mathf.Lerp(0.85f, 1.45f, fat) * Mathf.Lerp(0.95f, 1.1f, mus);
            Set(Part.Torso, new Vector3(0f, 1.175f, 0f), new Vector3(torsoX, 0.55f, torsoZ));
            Set(Part.Belly, new Vector3(0f, 1.02f, torsoZ * 0.1f + 0.06f * fat),
                new Vector3(torsoX * 0.9f, 0.38f, torsoZ * (0.7f + 0.8f * fat)));

            float armT = 0.09f * w * Mathf.Lerp(0.8f, 1.4f, fat) * Mathf.Lerp(0.85f, 1.45f, mus);
            float armX = torsoX * 0.5f + armT * 0.5f + 0.01f;
            Set(Part.UpperArmL, new Vector3(-armX, 1.3f, 0f), new Vector3(armT, 0.3f, armT));
            Set(Part.UpperArmR, new Vector3(armX, 1.3f, 0f), new Vector3(armT, 0.3f, armT));
            Set(Part.LowerArmL, new Vector3(-armX, 1.0f, 0f), new Vector3(armT * 0.9f, 0.3f, armT * 0.9f));
            Set(Part.LowerArmR, new Vector3(armX, 1.0f, 0f), new Vector3(armT * 0.9f, 0.3f, armT * 0.9f));

            float legT = 0.13f * w * Mathf.Lerp(0.8f, 1.35f, fat) * Mathf.Lerp(0.9f, 1.3f, mus);
            float legX = Mathf.Max(legT * 0.5f + 0.01f, torsoX * 0.25f);
            Set(Part.UpperLegL, new Vector3(-legX, 0.675f, 0f), new Vector3(legT, 0.45f, legT));
            Set(Part.UpperLegR, new Vector3(legX, 0.675f, 0f), new Vector3(legT, 0.45f, legT));
            Set(Part.LowerLegL, new Vector3(-legX, 0.265f, 0f), new Vector3(legT * 0.85f, 0.37f, legT * 0.85f));
            Set(Part.LowerLegR, new Vector3(legX, 0.265f, 0f), new Vector3(legT * 0.85f, 0.37f, legT * 0.85f));

            Vector3 footSize; float footY;
            switch (appearance.shoesStyle)
            {
                case 1: footSize = new Vector3(legT, 0.18f, 0.27f); footY = 0.09f; break;          // Stiefel
                case 2: footSize = new Vector3(legT * 0.8f, 0.06f, 0.22f); footY = 0.03f; break;   // barfuß
                default: footSize = new Vector3(legT * 0.9f, 0.08f, 0.26f); footY = 0.04f; break;  // Sneaker
            }
            Set(Part.FootL, new Vector3(-legX, footY, 0.05f), footSize);
            Set(Part.FootR, new Vector3(legX, footY, 0.05f), footSize);

            float headD = 0.25f * hs;
            Vector3 headPos = new Vector3(0f, 1.5f + headD * 0.5f, 0f);
            Set(Part.Head, headPos, Vector3.one * headD);
            if (headAnchor != null) headAnchor.position = rig.TransformPoint(headPos + new Vector3(0f, 0.02f, 0.05f));
        }

        // ---------------------------------------------------------------- Hilfsfunktionen

        private void Set(Part p, Vector3 pos, Vector3 scale)
        {
            Transform t = parts[(int)p];
            if (t == null) return;
            t.localPosition = pos;
            t.localScale = scale;
        }

        private static void Local(Transform t, Vector3 pos, Vector3 scale)
        {
            t.localPosition = pos;
            t.localScale = scale;
        }

        private Transform Create(PrimitiveType type, string name, Transform parent, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) DestroySafe(col);
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<Renderer>();
            if (baseMaterial != null) r.sharedMaterial = baseMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            SetColor(r, color);
            return go.transform;
        }

        private static void Colorize(GameObject go, Color color)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) SetColor(r, color);
        }

        private static void SetColor(Renderer r, Color color)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor(ColorId, color);      // Built-in Standard
            mpb.SetColor(BaseColorId, color);  // URP/Lit
            r.SetPropertyBlock(mpb);
        }

        private static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        private static void DestroySafe(Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
