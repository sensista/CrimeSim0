#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace CrimeSim.EditorTools
{
    /// <summary>
    /// Stil-Look (halb-realistisch, weich gemalt):
    /// - "CrimeSim/Stil auf Spielerfigur anwenden": Stil-Materialien erzeugen und dem Spieler-Prefab zuweisen.
    /// - "CrimeSim/Stil Testszene erstellen": Look-Dev-Szene mit Figur, Lichtern und Post-Processing.
    /// </summary>
    public static class StylizedLookBuilder
    {
        private const string MatFolder = "Assets/_Project/Art/Materials/Player/Stylized";
        private const string CharTexFolder = "Assets/_Project/Art/Textures/Character";
        private const string ModelTexFolder = "Assets/_Project/Art/Models/Player/Textures";
        private const string BackdropTex = "Assets/_Project/Art/Textures/LookDev/backdrop_gradient.png";
        private const string ScenePath = "Assets/_Project/Scenes/StilTest.unity";
        private const string VolumePath = "Assets/_Project/Settings/LookDev_Volume.asset";
        private const string FemaleSkin = CharTexFolder + "/skin_painted_female.png";
        private const string MaleSkin = CharTexFolder + "/skin_painted_male.png";
        private static readonly Color DefaultSkin = new Color(0.87f, 0.69f, 0.55f);

        // ------------------------------------------------------------ Materialien

        [MenuItem("CrimeSim/Stil auf Spielerfigur anwenden")]
        public static void ApplyToPlayerMenu()
        {
            if (ApplyToPlayer())
                Debug.Log("[CrimeSim] Stil-Materialien auf " + PlayerPrefabBuilder.PrefabPath + " angewendet.");
        }

        public static bool ApplyToPlayer()
        {
            if (!CheckShaders()) return false;
            var prefabAsset = PlayerPrefabBuilder.GetOrBuild();
            if (prefabAsset == null)
            {
                Debug.LogError("[CrimeSim] Spieler-Prefab fehlt (PlayerBase.fbx?). Zuerst 'CrimeSim/Spielerfigur Prefab erstellen'.");
                return false;
            }
            FixTextureImport(FemaleSkin, true);
            FixTextureImport(MaleSkin, true);

            var femaleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(FemaleSkin);
            var maleTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MaleSkin);

            var skin = SkinMaterial(femaleTex != null ? femaleTex : LoadModelTex("skin_diffuse.png"));
            var eyes = EyeMaterial();
            var brows = HairLikeMaterial("Stil_Augenbrauen", LoadModelTex("eyebrow001.png"), new Color(0.22f, 0.15f, 0.11f), 0.3f);
            var lashes = HairLikeMaterial("Stil_Wimpern", LoadModelTex("eyelashes01.png"), new Color(0.06f, 0.05f, 0.05f), 0.35f);
            var hair = HairLikeMaterial("Stil_Haare", null, new Color(0.25f, 0.16f, 0.1f), 0.4f);

            string path = PlayerPrefabBuilder.PrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    Material m = null;
                    if (n == "body") m = skin;
                    else if (n == "eyes" || n.StartsWith("eye") && !n.Contains("brow") && !n.Contains("lash")) m = eyes;
                    else if (n.Contains("brow")) m = brows;
                    else if (n.Contains("lash")) m = lashes;
                    else if (n.Contains("hair")) m = hair;
                    if (m == null) continue;
                    r.sharedMaterial = m;
                    r.shadowCastingMode = (n.Contains("brow") || n.Contains("lash") || n.StartsWith("eye"))
                        ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    count++;
                }

                var driver = root.GetComponentInChildren<MPFBShapeDriver>(true);
                if (driver != null)
                {
                    var so = new SerializedObject(driver);
                    var f = so.FindProperty("femaleSkinTexture");
                    var mm = so.FindProperty("maleSkinTexture");
                    if (f != null) f.objectReferenceValue = femaleTex;
                    if (mm != null) mm.objectReferenceValue = maleTex;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[CrimeSim] {count} Renderer auf Stil-Materialien umgestellt.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool CheckShaders()
        {
            foreach (var s in new[] { "CrimeSim/StylizedSkin", "CrimeSim/StylizedEye", "CrimeSim/StylizedHair" })
            {
                var sh = Shader.Find(s);
                if (sh == null)
                {
                    Debug.LogError("[CrimeSim] Shader '" + s + "' nicht gefunden oder fehlerhaft. Bitte Konsole auf Shader-Fehler prüfen.");
                    return false;
                }
            }
            return true;
        }

        private static Texture2D LoadModelTex(string file) => AssetDatabase.LoadAssetAtPath<Texture2D>(ModelTexFolder + "/" + file);

        private static void FixTextureImport(string path, bool srgb)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool changed = false;
            if (ti.sRGBTexture != srgb) { ti.sRGBTexture = srgb; changed = true; }
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; changed = true; }
            if (ti.mipmapEnabled != true) { ti.mipmapEnabled = true; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        private static Material GetOrCreate(string name, string shader)
        {
            if (!AssetDatabase.IsValidFolder(MatFolder))
            {
                Directory.CreateDirectory(MatFolder);
                AssetDatabase.Refresh();
            }
            string p = MatFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
            var sh = Shader.Find(shader);
            if (mat == null)
            {
                mat = new Material(sh) { name = name };
                AssetDatabase.CreateAsset(mat, p);
            }
            else
            {
                // Bestehendes Material auf die aktuellen Shader-Standardwerte zurücksetzen
                var fresh = new Material(sh);
                mat.shader = sh;
                mat.CopyPropertiesFromMaterial(fresh);
                mat.shaderKeywords = fresh.shaderKeywords;
                mat.renderQueue = -1;
                Object.DestroyImmediate(fresh);
            }
            return mat;
        }

        private static Material SkinMaterial(Texture2D tex)
        {
            var m = GetOrCreate("Stil_Haut", "CrimeSim/StylizedSkin");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", DefaultSkin);
            m.SetColor("_ToneReference", DefaultSkin);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material EyeMaterial()
        {
            var m = GetOrCreate("Stil_Augen", "CrimeSim/StylizedEye");
            var tex = LoadModelTex("brown_eye.png");
            if (tex != null) m.SetTexture("_BaseMap", tex);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material HairLikeMaterial(string name, Texture2D tex, Color color, float cutoff)
        {
            var m = GetOrCreate(name, "CrimeSim/StylizedHair");
            if (tex != null)
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
                if (ti != null && !ti.alphaIsTransparency) { ti.alphaIsTransparency = true; ti.SaveAndReimport(); }
                m.SetTexture("_BaseMap", tex);
            }
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Cutoff", cutoff);
            m.SetFloat("_AlphaClip", 1f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------ Testszene

        [MenuItem("CrimeSim/Stil Testszene erstellen")]
        public static void BuildScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!ApplyToPlayer()) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabBuilder.PrefabPath);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.07f, 0.075f, 0.1f);
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0.045f, 0.05f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.018f, 0.016f);
            RenderSettings.skybox = null;

            // Figur
            var fig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            fig.name = "Figur";
            fig.transform.position = Vector3.zero;
            var driver = fig.GetComponentInChildren<MPFBShapeDriver>();

            // Stimmung: dunkel, viele weiche indirekte Lichtquellen (Low-Key)
            MakeLight("Key Light (warm, gedimmt)", LightType.Directional, new Color(1f, 0.82f, 0.64f), 0.45f,
                Quaternion.Euler(30f, 140f, 0f), Vector3.zero, LightShadows.Soft);
            MakeLight("Lampe warm (links vorne)", LightType.Point, new Color(1f, 0.6f, 0.3f), 1.4f,
                Quaternion.identity, new Vector3(-1.1f, 1.5f, 1.0f), LightShadows.Soft).range = 3.5f;
            MakeLight("Neon kühl (rechts)", LightType.Point, new Color(0.35f, 0.6f, 1f), 1.0f,
                Quaternion.identity, new Vector3(1.4f, 1.7f, 0.4f), LightShadows.None).range = 3.5f;
            MakeLight("Neon magenta (hinten rechts)", LightType.Point, new Color(0.9f, 0.3f, 0.7f), 0.6f,
                Quaternion.identity, new Vector3(1.2f, 2.0f, -1.0f), LightShadows.None).range = 3f;
            MakeLight("Boden-Bounce warm", LightType.Point, new Color(0.9f, 0.55f, 0.35f), 0.35f,
                Quaternion.identity, new Vector3(0f, 0.15f, 0.8f), LightShadows.None).range = 2.5f;
            MakeLight("Rim Light (dezent)", LightType.Spot, new Color(1f, 0.78f, 0.6f), 0.8f,
                Quaternion.identity, new Vector3(-1.0f, 2.1f, -1.4f), LightShadows.None, new Vector3(0f, 1.4f, 0f));

            // Hintergrund
            var bdTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BackdropTex);
            var bdMat = GetOrCreate("LookDev_Backdrop", "Universal Render Pipeline/Unlit");
            if (bdTex != null) bdMat.SetTexture("_BaseMap", bdTex);
            bdMat.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(bdMat);
            var backdrop = GameObject.CreatePrimitive(PrimitiveType.Quad);
            backdrop.name = "Backdrop";
            Object.DestroyImmediate(backdrop.GetComponent<Collider>());
            backdrop.transform.position = new Vector3(0f, 1.3f, -2.5f);
            backdrop.transform.localScale = new Vector3(8f, 5f, 1f);
            backdrop.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // Vorderseite zur Kamera (+Z)
            var bdr = backdrop.GetComponent<Renderer>();
            bdr.sharedMaterial = bdMat;
            bdr.shadowCastingMode = ShadowCastingMode.Off;

            var floorMat = GetOrCreate("LookDev_Floor", "Universal Render Pipeline/Lit");
            floorMat.SetColor("_BaseColor", new Color(0.09f, 0.085f, 0.095f));
            floorMat.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(floorMat);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;

            // Kamera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.04f, 0.06f);
            cam.fieldOfView = 35f;
            cam.nearClipPlane = 0.05f;
            camGo.AddComponent<AudioListener>();
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.transform.position = new Vector3(0f, 1f, 3.5f);
            camGo.transform.rotation = Quaternion.Euler(5f, 180f, 0f);
            var orbit = camGo.AddComponent<CreatorOrbitCamera>();
            var oso = new SerializedObject(orbit);
            oso.FindProperty("target").objectReferenceValue = fig.transform;
            var off = oso.FindProperty("screenOffset");
            if (off != null) off.floatValue = 0f;
            oso.ApplyModifiedPropertiesWithoutUndo();

            // Post-Processing
            var volGo = new GameObject("Post Processing");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = BuildProfile();

            // Steuerung
            var ctrlGo = new GameObject("LookDev");
            var ctrl = ctrlGo.AddComponent<LookDevController>();
            var cso = new SerializedObject(ctrl);
            cso.FindProperty("figure").objectReferenceValue = driver;
            cso.FindProperty("orbit").objectReferenceValue = orbit;
            cso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = ctrlGo;
            Debug.Log("[CrimeSim] Stil-Testszene erstellt: " + ScenePath +
                      "\nPlay drücken. Taste 1 = Ganzkörper (Mittelmaus drehen, Rad zoomen), 2 = Gesicht, 3 = Halbtotale. " +
                      "Regler für Geschlecht/Muskeln/Gewicht/Hautfarbe am Objekt 'LookDev'.");
        }

        private static Light MakeLight(string name, LightType type, Color color, float intensity, Quaternion rot,
            Vector3 pos, LightShadows shadows, Vector3? lookAt = null)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows;
            go.transform.position = pos;
            go.transform.rotation = rot;
            if (lookAt.HasValue) go.transform.LookAt(lookAt.Value);
            if (type == LightType.Spot) { l.range = 4f; l.spotAngle = 35f; l.innerSpotAngle = 15f; }
            return l;
        }

        private static VolumeProfile BuildProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumePath);
            if (profile == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(VolumePath));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumePath);
            }

            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.65f);

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var ca = Get<ColorAdjustments>(profile);
            ca.postExposure.Override(0f);
            ca.contrast.Override(5f);
            ca.saturation.Override(4f);
            ca.colorFilter.Override(new Color(1f, 0.97f, 0.93f));

            var vig = Get<Vignette>(profile);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var c)) return c;
            c = profile.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }
    }
}
#endif
