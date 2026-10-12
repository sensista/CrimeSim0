#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrimeSim.EditorTools
{
    /// <summary>Erzeugt das MPFB-Spieler-Prefab (Modell + MPFBShapeDriver + URP-Materialien).</summary>
    public static class PlayerPrefabBuilder
    {
        public const string PrefabPath = "Assets/_Project/Prefabs/Player/PlayerMPFB.prefab";
        private const string TextureFolder = "Assets/_Project/Art/Models/Player/Textures";
        private const string MaterialFolder = "Assets/_Project/Art/Materials/Player";

        [MenuItem("CrimeSim/Spielerfigur Prefab erstellen")]
        public static void BuildMenu()
        {
            var p = Build();
            if (p != null)
            {
                Selection.activeObject = p;
                EditorGUIUtility.PingObject(p);
            }
        }

        /// <summary>Vorhandenes Prefab liefern oder neu bauen. null, wenn PlayerBase.fbx fehlt.</summary>
        public static GameObject GetOrBuild()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return existing != null ? existing : Build();
        }

        public static GameObject Build()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MPFBPlayerImporter.ModelPath);
            var manifest = AssetDatabase.LoadAssetAtPath<TextAsset>(MPFBPlayerImporter.ManifestPath);
            if (model == null || manifest == null)
            {
                Debug.LogWarning("[CrimeSim] PlayerBase.fbx oder PlayerBase.shapes.json fehlt – Platzhalter-Figur wird verwendet.");
                return null;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "PlayerMPFB";
            try
            {
                var anim = inst.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    if (anim.avatar == null || !anim.avatar.isHuman)
                        Debug.LogWarning("[CrimeSim] PlayerBase.fbx hat (noch) keinen gültigen Humanoid-Avatar. Rig-Tab im Import prüfen.");
                }

                var driver = inst.AddComponent<MPFBShapeDriver>();
                var so = new SerializedObject(driver);
                so.FindProperty("manifestJson").objectReferenceValue = manifest;
                so.FindProperty("modelRoot").objectReferenceValue = inst.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                foreach (var r in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    r.sharedMaterial = MaterialFor(r.name);
                    r.updateWhenOffscreen = true; // Bounds ändern sich mit Shapes/Größe
                    r.shadowCastingMode = r.name == "Eyelashes" || r.name == "Eyebrows"
                        ? UnityEngine.Rendering.ShadowCastingMode.Off
                        : UnityEngine.Rendering.ShadowCastingMode.On;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
                var prefab = PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
                Debug.Log("[CrimeSim] Spieler-Prefab erstellt: " + PrefabPath);
                return prefab;
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private static Material MaterialFor(string rendererName)
        {
            switch (rendererName)
            {
                case "Body": return GetMat("PlayerSkin", "skin_diffuse.png", Color.white, false, 0.35f);
                case "Eyes": return GetMat("PlayerEyes", "brown_eye.png", Color.white, false, 0.8f);
                case "Eyebrows": return GetMat("PlayerEyebrows", "eyebrow001.png", Color.white, true, 0.1f);
                case "Eyelashes": return GetMat("PlayerEyelashes", "eyelashes01.png", Color.white, true, 0.1f);
                case "Teeth": return GetMat("PlayerTeeth", "teeth.png", Color.white, false, 0.6f);
                case "Underwear_Panties": return GetMat("PlayerUnderwearPanties", "f_panties_01_diffuse.png", Color.white, true, 0.3f);
                case "Underwear_Top": return GetMat("PlayerUnderwearTop", "f_top_01_diffuse.png", Color.white, true, 0.3f);
                case "Underwear_Briefs": return GetMat("PlayerUnderwearBriefs", "sport_briefs01.png", Color.white, false, 0.3f);
                default: return GetMat("PlayerDefault", null, new Color(0.6f, 0.6f, 0.6f), false, 0.3f);
            }
        }

        private static Material GetMat(string name, string texture, Color color, bool alphaClip, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                Directory.CreateDirectory(MaterialFolder);
                AssetDatabase.Refresh();
            }
            string path = MaterialFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            Texture2D tex = texture != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/" + texture) : null;
            if (tex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (alphaClip)
            {
                if (mat.HasProperty("_AlphaClip")) mat.SetFloat("_AlphaClip", 1f);
                if (mat.HasProperty("_Cutoff")) mat.SetFloat("_Cutoff", 0.4f);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // beidseitig
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
#endif
