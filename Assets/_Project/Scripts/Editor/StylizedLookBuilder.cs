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
        private const string FemaleSkin = CharTexFolder + "/skin_painted_female.png";
        private const string BlushMask = CharTexFolder + "/blush_mask.png";
        private const string AOMap = CharTexFolder + "/skin_ao.png";
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
            var brows = HairLikeMaterial("Stil_Augenbrauen", LoadModelTex("eyebrow001.png"), new Color(0.30f, 0.22f, 0.17f), 0.3f);
            var lashes = HairLikeMaterial("Stil_Wimpern", LoadModelTex("eyelashes01.png"), new Color(0.16f, 0.12f, 0.10f), 0.35f);
            var hair = HairLikeMaterial("Stil_Haare", null, new Color(0.25f, 0.16f, 0.1f), 0.4f);

            string path = PlayerPrefabBuilder.PrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int count = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    string n = r.name.ToLowerInvariant();
                    // Mund ist geschlossen und es gibt noch keine Sprech-Animation: Zähne/Zahnfleisch ausblenden,
                    // sonst scheinen sie als graue, gezackte Linie durch den Lippenspalt.
                    if (n == "teeth") { r.enabled = false; continue; }
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
                    void SetTex(string prop, string file, bool normal)
                    {
                        string tp = CharTexFolder + "/" + file;
                        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                        if (ti != null && (ti.sRGBTexture || (normal && ti.textureType != TextureImporterType.NormalMap) || ti.wrapMode != TextureWrapMode.Clamp))
                        {
                            if (normal) ti.textureType = TextureImporterType.NormalMap; else ti.sRGBTexture = false;
                            ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();
                        }
                        var sp = so.FindProperty(prop);
                        if (sp != null) sp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
                    }
                    SetTex("femaleNormalMap", "skin_normal_female.png", true);
                    SetTex("maleNormalMap", "skin_normal_male.png", true);
                    SetTex("femaleDetailMap", "skin_detail_female.png", false);
                    SetTex("maleDetailMap", "skin_detail_male.png", false);
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
            FixTextureImport(BlushMask, false);
            var blush = AssetDatabase.LoadAssetAtPath<Texture2D>(BlushMask);
            if (blush != null) m.SetTexture("_BlushMask", blush);
            FixTextureImport(AOMap, false);
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(AOMap);
            if (ao != null) m.SetTexture("_AOMap", ao);
            void Tex(string file, string prop, bool repeat)
            {
                string path = CharTexFolder + "/" + file;
                FixTextureImport(path, false);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null && repeat && ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport(); }
                if (ti != null && !repeat && ti.wrapMode != TextureWrapMode.Clamp) { ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (t != null && m.HasProperty(prop)) m.SetTexture(prop, t);
            }
            Tex("skin_variation.png", "_VariationMap", false);
            Tex("skin_thickness.png", "_ThicknessMap", false);
            Tex("skin_detail_female.png", "_DetailMap", false);
            {
                string np = CharTexFolder + "/skin_normal_female.png";
                var ni = AssetImporter.GetAtPath(np) as TextureImporter;
                if (ni != null && ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.wrapMode = TextureWrapMode.Clamp; ni.SaveAndReimport(); }
                var nt = AssetDatabase.LoadAssetAtPath<Texture2D>(np);
                if (nt != null) m.SetTexture("_BumpMap", nt);
            }
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
            m.SetFloat("_TexColorInfluence", 0.15f); // Brauen/Wimpern: Farbe aus Material, Textur nur für Alpha -> nicht pechschwarz
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
            RenderSettings.skybox = null;

            // Figur
            var fig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            fig.name = "Figur";
            fig.transform.position = Vector3.zero;
            var driver = fig.GetComponentInChildren<MPFBShapeDriver>();

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
            camData.antialiasingQuality = AntialiasingQuality.High;
            cam.allowMSAA = true;
            camGo.transform.position = new Vector3(0f, 1f, 3.5f);
            camGo.transform.rotation = Quaternion.Euler(5f, 180f, 0f);
            var orbit = camGo.AddComponent<CreatorOrbitCamera>();
            var oso = new SerializedObject(orbit);
            oso.FindProperty("target").objectReferenceValue = fig.transform;
            var off = oso.FindProperty("screenOffset");
            if (off != null) off.floatValue = 0f;
            oso.ApplyModifiedPropertiesWithoutUndo();

            // Licht-Stimmungen (Tag / Abend / Nacht) inkl. Post-Processing
            BuildLightRigs(cam, 3);

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

        /// <summary>
        /// Baut die drei Licht-Stimmungen unter einem "Licht"-Objekt mit LightingPresets.
        /// Nur das Hauptlicht wirft Schatten (+ ein Spot bei Tag), damit der Schatten-Atlas reicht.
        /// </summary>
        public static LightingPresets BuildLightRigs(Camera cam, int start)
        {
            var rootGo = new GameObject("Licht (Tag/Abend/Nacht/Innenraum)");
            var lp = rootGo.AddComponent<LightingPresets>();
            Vector3 face = new Vector3(0f, 1.5f, 0f);

            // Tag: weich, hell, neutral-warm
            var tag = Rig(rootGo.transform, "Tag", "LookDev_Tag", 0.25f, 6f, 0.18f, new Color(1f, 0.99f, 0.97f));
            MakeLight("Sonne/Key (vorne links)", LightType.Directional, new Color(1f, 0.93f, 0.84f), 1.0f,
                Quaternion.Euler(25f, 155f, 0f), Vector3.zero, LightShadows.Soft, null, tag);
            MakeLight("Himmel-Fill (kühl)", LightType.Directional, new Color(0.7f, 0.8f, 1f), 0.35f,
                Quaternion.Euler(15f, 215f, 0f), Vector3.zero, LightShadows.None, null, tag);
            MakeLight("Rim", LightType.Spot, new Color(1f, 0.9f, 0.8f), 0.8f,
                Quaternion.identity, new Vector3(0.9f, 2.1f, -1.4f), LightShadows.None, face, tag);

            // Abend: warm, gedämpft, weiches Key von vorne links (wie Referenz)
            var abend = Rig(rootGo.transform, "Abend", "LookDev_Abend", 0f, 4f, 0.28f, new Color(1f, 0.96f, 0.9f));
            MakeLight("Key warm (vorne links)", LightType.Directional, new Color(1f, 0.8f, 0.62f), 0.55f,
                Quaternion.Euler(22f, 150f, 0f), Vector3.zero, LightShadows.Soft, null, abend);
            MakeLight("Lampe warm", LightType.Point, new Color(1f, 0.62f, 0.35f), 1.2f,
                Quaternion.identity, new Vector3(-1.0f, 1.6f, 1.1f), LightShadows.None, null, abend).range = 3.5f;
            MakeLight("Fill kühl (rechts)", LightType.Point, new Color(0.45f, 0.6f, 1f), 0.5f,
                Quaternion.identity, new Vector3(1.4f, 1.6f, 0.6f), LightShadows.None, null, abend).range = 3.5f;
            MakeLight("Boden-Bounce", LightType.Point, new Color(0.9f, 0.55f, 0.35f), 0.3f,
                Quaternion.identity, new Vector3(0f, 0.2f, 0.8f), LightShadows.None, null, abend).range = 2.5f;
            MakeLight("Rim dezent", LightType.Spot, new Color(1f, 0.78f, 0.6f), 0.7f,
                Quaternion.identity, new Vector3(-1.0f, 2.1f, -1.4f), LightShadows.None, face, abend);

            // Nacht: dunkel, Neon-Akzente
            var nacht = Rig(rootGo.transform, "Nacht", "LookDev_Nacht", -0.1f, 2f, 0.35f, new Color(0.93f, 0.95f, 1f));
            MakeLight("Mond/Key (gedimmt)", LightType.Directional, new Color(0.75f, 0.8f, 1f), 0.25f,
                Quaternion.Euler(30f, 150f, 0f), Vector3.zero, LightShadows.Soft, null, nacht);
            MakeLight("Straßenlampe warm", LightType.Point, new Color(1f, 0.6f, 0.3f), 1.3f,
                Quaternion.identity, new Vector3(-1.1f, 1.8f, 0.9f), LightShadows.None, null, nacht).range = 3.5f;
            MakeLight("Neon blau", LightType.Point, new Color(0.3f, 0.55f, 1f), 1.0f,
                Quaternion.identity, new Vector3(1.4f, 1.7f, 0.4f), LightShadows.None, null, nacht).range = 3.5f;
            MakeLight("Neon magenta", LightType.Point, new Color(0.9f, 0.3f, 0.7f), 0.7f,
                Quaternion.identity, new Vector3(1.2f, 2.0f, -1.0f), LightShadows.None, null, nacht).range = 3f;

            // Innenraum warm (Standard für Creator/Look-Dev): weiche Stehlampe vorne links, warmer Bounce
            // von rechts unten gegen harte Nasenschatten, schwaches kühles Fenster, dezentes Rim
            var innen = Rig(rootGo.transform, "Innenraum", "LookDev_Innenraum", 0.1f, 6f, 0.22f, new Color(1f, 0.97f, 0.92f));
            var lamp = MakeLight("Stehlampe (Key, vorne links)", LightType.Spot, new Color(1f, 0.72f, 0.45f), 3.0f,
                Quaternion.identity, new Vector3(-0.9f, 1.75f, 1.0f), LightShadows.Soft, face, innen);
            lamp.range = 5f; lamp.spotAngle = 80f; lamp.innerSpotAngle = 40f; lamp.shadowNormalBias = 0.3f;
            MakeLight("Bounce warm (rechts unten)", LightType.Point, new Color(1f, 0.7f, 0.5f), 0.6f,
                Quaternion.identity, new Vector3(0.8f, 1.1f, 0.9f), LightShadows.None, null, innen).range = 3f;
            MakeLight("Fenster kühl (links)", LightType.Point, new Color(0.7f, 0.8f, 1f), 0.35f,
                Quaternion.identity, new Vector3(-1.6f, 1.6f, -0.2f), LightShadows.None, null, innen).range = 3.5f;
            MakeLight("Rim warm", LightType.Spot, new Color(1f, 0.75f, 0.55f), 0.6f,
                Quaternion.identity, new Vector3(0.7f, 2.0f, -1.2f), LightShadows.None, face, innen);

            var so = new SerializedObject(lp);
            var arr = so.FindProperty("presets");
            arr.arraySize = 4;
            void P(int i, string n, GameObject r, Color sky, Color eq, Color gr, Color bg)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("name").stringValue = n;
                e.FindPropertyRelative("root").objectReferenceValue = r;
                e.FindPropertyRelative("ambientSky").colorValue = sky;
                e.FindPropertyRelative("ambientEquator").colorValue = eq;
                e.FindPropertyRelative("ambientGround").colorValue = gr;
                e.FindPropertyRelative("background").colorValue = bg;
            }
            P(0, "Tag", tag.gameObject, new Color(0.42f, 0.45f, 0.52f), new Color(0.32f, 0.3f, 0.3f), new Color(0.12f, 0.1f, 0.09f), new Color(0.2f, 0.21f, 0.24f));
            P(1, "Abend", abend.gameObject, new Color(0.1f, 0.1f, 0.13f), new Color(0.08f, 0.065f, 0.065f), new Color(0.03f, 0.025f, 0.02f), new Color(0.05f, 0.045f, 0.055f));
            P(2, "Nacht", nacht.gameObject, new Color(0.05f, 0.055f, 0.08f), new Color(0.035f, 0.03f, 0.045f), new Color(0.01f, 0.01f, 0.012f), new Color(0.02f, 0.02f, 0.03f));
            P(3, "Innenraum", innen.gameObject, new Color(0.14f, 0.12f, 0.11f), new Color(0.12f, 0.09f, 0.07f), new Color(0.05f, 0.035f, 0.025f), new Color(0.06f, 0.05f, 0.045f));
            so.FindProperty("startPreset").intValue = start;
            so.FindProperty("targetCamera").objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            lp.Set(start);
            return lp;
        }

        private static Transform Rig(Transform parent, string name, string profileName, float exposure, float saturation, float vignette, Color filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = BuildProfile("Assets/_Project/Settings/" + profileName + ".asset", exposure, saturation, vignette, filter);
            return go.transform;
        }

        private static Light MakeLight(string name, LightType type, Color color, float intensity, Quaternion rot,
            Vector3 pos, LightShadows shadows, Vector3? lookAt = null, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.shadows = shadows;
            go.transform.position = pos;
            go.transform.rotation = rot;
            if (lookAt.HasValue) go.transform.LookAt(lookAt.Value);
            if (shadows != LightShadows.None)
            {
                l.shadowBias = 0.08f;
                l.shadowNormalBias = 0.6f;
                l.shadowStrength = 0.85f;
                l.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
            }
            if (type == LightType.Spot) { l.range = 4f; l.spotAngle = 35f; l.innerSpotAngle = 15f; }
            return l;
        }

        private static VolumeProfile BuildProfile(string path, float exposure, float saturation, float vignette, Color filter)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var bloom = Get<Bloom>(profile);
            bloom.intensity.Override(0.12f);
            bloom.threshold.Override(1.1f);
            bloom.scatter.Override(0.65f);

            var tone = Get<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var ca = Get<ColorAdjustments>(profile);
            ca.postExposure.Override(exposure);
            ca.contrast.Override(5f);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(filter);

            var vig = Get<Vignette>(profile);
            vig.intensity.Override(vignette);
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
