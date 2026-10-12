#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CrimeSim.EditorTools
{
    /// <summary>
    /// Import-Einstellungen für die MPFB-Spielerfigur (Art/Models/Player/PlayerBase.fbx):
    /// Humanoid (Create From This Model), Hips = pelvis, explizite Zuordnung der Unreal-Knochennamen,
    /// Blendshapes an, Normalen importieren, Blendshape-Normalen berechnen, max. 4 Gewichte, keine Animation.
    /// Hinweis: Die Ruhepose ist eine A-Pose. Humanoid funktioniert damit; falls Animationen Arme schief
    /// zeigen, im Avatar-Konfigurator "Pose → Enforce T-Pose" ausführen und übernehmen.
    /// </summary>
    public class MPFBPlayerImporter : AssetPostprocessor
    {
        public const string ModelPath = "Assets/_Project/Art/Models/Player/PlayerBase.fbx";
        public const string ManifestPath = "Assets/_Project/Art/Models/Player/PlayerBase.shapes.json";

        private static readonly Dictionary<HumanBodyBones, string> Map = new Dictionary<HumanBodyBones, string>
        {
            { HumanBodyBones.Hips, "pelvis" },
            { HumanBodyBones.Spine, "spine_01" },
            { HumanBodyBones.Chest, "spine_02" },
            { HumanBodyBones.UpperChest, "spine_03" },
            { HumanBodyBones.Neck, "neck_01" },
            { HumanBodyBones.Head, "head" },
            { HumanBodyBones.LeftShoulder, "clavicle_l" },
            { HumanBodyBones.LeftUpperArm, "upperarm_l" },
            { HumanBodyBones.LeftLowerArm, "lowerarm_l" },
            { HumanBodyBones.LeftHand, "hand_l" },
            { HumanBodyBones.RightShoulder, "clavicle_r" },
            { HumanBodyBones.RightUpperArm, "upperarm_r" },
            { HumanBodyBones.RightLowerArm, "lowerarm_r" },
            { HumanBodyBones.RightHand, "hand_r" },
            { HumanBodyBones.LeftUpperLeg, "thigh_l" },
            { HumanBodyBones.LeftLowerLeg, "calf_l" },
            { HumanBodyBones.LeftFoot, "foot_l" },
            { HumanBodyBones.LeftToes, "ball_l" },
            { HumanBodyBones.RightUpperLeg, "thigh_r" },
            { HumanBodyBones.RightLowerLeg, "calf_r" },
            { HumanBodyBones.RightFoot, "foot_r" },
            { HumanBodyBones.RightToes, "ball_r" },
            { HumanBodyBones.LeftThumbProximal, "thumb_01_l" },
            { HumanBodyBones.LeftThumbIntermediate, "thumb_02_l" },
            { HumanBodyBones.LeftThumbDistal, "thumb_03_l" },
            { HumanBodyBones.LeftIndexProximal, "index_01_l" },
            { HumanBodyBones.LeftIndexIntermediate, "index_02_l" },
            { HumanBodyBones.LeftIndexDistal, "index_03_l" },
            { HumanBodyBones.LeftMiddleProximal, "middle_01_l" },
            { HumanBodyBones.LeftMiddleIntermediate, "middle_02_l" },
            { HumanBodyBones.LeftMiddleDistal, "middle_03_l" },
            { HumanBodyBones.LeftRingProximal, "ring_01_l" },
            { HumanBodyBones.LeftRingIntermediate, "ring_02_l" },
            { HumanBodyBones.LeftRingDistal, "ring_03_l" },
            { HumanBodyBones.LeftLittleProximal, "pinky_01_l" },
            { HumanBodyBones.LeftLittleIntermediate, "pinky_02_l" },
            { HumanBodyBones.LeftLittleDistal, "pinky_03_l" },
            { HumanBodyBones.RightThumbProximal, "thumb_01_r" },
            { HumanBodyBones.RightThumbIntermediate, "thumb_02_r" },
            { HumanBodyBones.RightThumbDistal, "thumb_03_r" },
            { HumanBodyBones.RightIndexProximal, "index_01_r" },
            { HumanBodyBones.RightIndexIntermediate, "index_02_r" },
            { HumanBodyBones.RightIndexDistal, "index_03_r" },
            { HumanBodyBones.RightMiddleProximal, "middle_01_r" },
            { HumanBodyBones.RightMiddleIntermediate, "middle_02_r" },
            { HumanBodyBones.RightMiddleDistal, "middle_03_r" },
            { HumanBodyBones.RightRingProximal, "ring_01_r" },
            { HumanBodyBones.RightRingIntermediate, "ring_02_r" },
            { HumanBodyBones.RightRingDistal, "ring_03_r" },
            { HumanBodyBones.RightLittleProximal, "pinky_01_r" },
            { HumanBodyBones.RightLittleIntermediate, "pinky_02_r" },
            { HumanBodyBones.RightLittleDistal, "pinky_03_r" },
        };

        private static bool IsPlayerModel(string path) =>
            path.Replace('\\', '/').EndsWith("Art/Models/Player/PlayerBase.fbx", System.StringComparison.OrdinalIgnoreCase);

        private static readonly HashSet<string> pendingReimport = new HashSet<string>();

        private void OnPreprocessModel()
        {
            if (!IsPlayerModel(assetPath)) return;
            var mi = (ModelImporter)assetImporter;

            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.bakeAxisConversion = false;
            mi.importBlendShapes = true;
            mi.importVisibility = false;
            mi.importCameras = false;
            mi.importLights = false;
            // Normalen selbst glatt berechnen (MPFB-Export wirkt sonst facettiert)
            mi.importNormals = ModelImporterNormals.Calculate;
            mi.importBlendShapeNormals = ModelImporterNormals.Calculate;
            mi.normalCalculationMode = ModelImporterNormalCalculationMode.AreaAndAngleWeighted;
            mi.normalSmoothingAngle = 180f;
            mi.importTangents = ModelImporterTangents.CalculateMikk;
            mi.skinWeights = ModelImporterSkinWeights.Standard; // 4 Einflüsse pro Vertex
            mi.optimizeMeshVertices = true;
            mi.optimizeMeshPolygons = true;
            mi.isReadable = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // Materialien setzt der Prefab-Builder
            mi.importAnimation = false;

            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.optimizeGameObjects = false; // Knochen müssen für MPFBShapeDriver erreichbar bleiben

            // Explizite Zuordnung. Das Skelett (SkeletonBone[]) kennt Unity erst nach dem ersten Import;
            // ist es noch leer, läuft die Auto-Zuordnung und wir importieren danach einmal neu.
            HumanDescription desc = mi.humanDescription;
            if (desc.skeleton == null || desc.skeleton.Length == 0)
            {
                pendingReimport.Add(assetPath);
                return;
            }
            var human = new List<HumanBone>();
            foreach (var kv in Map)
            {
                var hb = new HumanBone
                {
                    humanName = HumanTrait.BoneName[(int)kv.Key],
                    boneName = kv.Value,
                };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }
            desc.human = human.ToArray();
            desc.hasTranslationDoF = false;
            desc.upperArmTwist = 0.5f;
            desc.lowerArmTwist = 0.5f;
            desc.upperLegTwist = 0.5f;
            desc.lowerLegTwist = 0.5f;
            desc.armStretch = 0.05f;
            desc.legStretch = 0.05f;
            desc.feetSpacing = 0f;
            mi.humanDescription = desc;
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!IsPlayerModel(assetPath)) return;
            if (!pendingReimport.Remove(assetPath)) return;
            string path = assetPath;
            EditorApplication.delayCall += () =>
            {
                var mi = AssetImporter.GetAtPath(path) as ModelImporter;
                if (mi == null || mi.humanDescription.skeleton == null || mi.humanDescription.skeleton.Length == 0)
                {
                    Debug.LogWarning("[CrimeSim] PlayerBase.fbx: Humanoid-Skelett noch leer – Auto-Zuordnung bleibt aktiv.");
                    return;
                }
                Debug.Log("[CrimeSim] PlayerBase.fbx: wende explizite Humanoid-Zuordnung an (Re-Import).");
                mi.SaveAndReimport();
            };
        }
    }
}
#endif
