#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CrimeSim.EditorTools
{
    /// <summary>Baut per Menü eine einfache Testszene zum Ausprobieren von Bewegung und Kamera.</summary>
    public static class TestLevelBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/TestLevel.unity";
        private const string MaterialFolder = "Assets/_Project/Art/Materials";
        private const string BodyLayerName = "PlayerBody";

        [MenuItem("CrimeSim/Testlevel erstellen")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            int bodyLayer = EnsureLayer(BodyLayerName);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material ground = GetMaterial("TL_Ground", new Color(0.35f, 0.38f, 0.35f));
            Material building = GetMaterial("TL_Building", new Color(0.6f, 0.6f, 0.65f));
            Material accent = GetMaterial("TL_Accent", new Color(0.8f, 0.45f, 0.2f));
            Material crate = GetMaterial("TL_Crate", new Color(0.55f, 0.4f, 0.25f));
            Material player = GetMaterial("TL_Player", new Color(0.2f, 0.45f, 0.85f));

            // Licht
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // Umgebung
            var env = new GameObject("Environment").transform;
            Block("Ground", env, new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), ground);

            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(env);
            Block("Building_A", buildings, new Vector3(15f, 4f, 10f), new Vector3(8f, 8f, 8f), building);
            Block("Building_B", buildings, new Vector3(-15f, 7f, 12f), new Vector3(10f, 14f, 6f), building);
            Block("Building_C", buildings, new Vector3(20f, 2.5f, -15f), new Vector3(12f, 5f, 10f), building);
            Block("Building_D", buildings, new Vector3(-20f, 10f, -18f), new Vector3(6f, 20f, 6f), building);
            Block("Building_E", buildings, new Vector3(0f, 3f, 30f), new Vector3(20f, 6f, 5f), building);

            // Rampe
            var ramp = Block("Ramp", env, new Vector3(-6f, 1f, -8f), new Vector3(4f, 0.3f, 8f), accent);
            ramp.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);

            // Treppe (Stufenhöhe 0,25 m; CharacterController Step Offset 0,3 m)
            var stairs = new GameObject("Stairs").transform;
            stairs.SetParent(env);
            for (int i = 0; i < 10; i++)
            {
                float h = 0.25f * (i + 1);
                Block("Step_" + i, stairs, new Vector3(6f, h * 0.5f, -5f - i * 0.5f), new Vector3(3f, h, 0.5f), accent);
            }
            Block("StairsTop", stairs, new Vector3(6f, 1.25f, -11.5f), new Vector3(3f, 2.5f, 3f), accent);

            // Kisten
            var crates = new GameObject("Crates").transform;
            crates.SetParent(env);
            Vector3[] cratePos =
            {
                new Vector3(3f, 0.5f, 5f), new Vector3(4.2f, 0.5f, 5.3f), new Vector3(3.6f, 1.5f, 5.1f),
                new Vector3(-4f, 0.5f, 3f), new Vector3(8f, 0.5f, 2f), new Vector3(-9f, 0.5f, 6f)
            };
            for (int i = 0; i < cratePos.Length; i++)
            {
                var c = Block("Crate_" + i, crates, cratePos[i], Vector3.one, crate);
                c.transform.rotation = Quaternion.Euler(0f, i * 17f, 0f);
            }

            // Spieler
            var playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 0.05f, 0f);
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            var playerController = playerGo.AddComponent<CrimeSim.PlayerController>();

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>()); // CharacterController reicht
            body.transform.SetParent(playerGo.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = player;
            if (bodyLayer >= 0) body.layer = bodyLayer;

            var head = new GameObject("HeadAnchor").transform;
            head.SetParent(playerGo.transform, false);
            head.localPosition = new Vector3(0f, 1.6f, 0f);

            // Kamera
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.transform.position = new Vector3(0f, 2.5f, -4f);
            var camController = camGo.AddComponent<CrimeSim.CameraController>();

            var so = new SerializedObject(camController);
            so.FindProperty("target").objectReferenceValue = playerGo.transform;
            so.FindProperty("headAnchor").objectReferenceValue = head;
            int mask = ~0;
            if (bodyLayer >= 0) mask &= ~(1 << bodyLayer);
            so.FindProperty("collisionMask").intValue = mask;
            so.FindProperty("playerBodyLayer").stringValue = BodyLayerName;
            so.ApplyModifiedPropertiesWithoutUndo();

            var pso = new SerializedObject(playerController);
            pso.FindProperty("cameraController").objectReferenceValue = camController;
            pso.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[CrimeSim] Testlevel erstellt: " + ScenePath);
        }

        private static GameObject Block(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static Material GetMaterial(string name, Color color)
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
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Gibt den Layer-Index zurück und legt den Layer bei Bedarf im TagManager an.</summary>
        private static int EnsureLayer(string layerName)
        {
            int existing = LayerMask.NameToLayer(layerName);
            if (existing >= 0) return existing;

            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return -1;
            var tagManager = new SerializedObject(assets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null) return -1;

            for (int i = 8; i < layers.arraySize; i++) // 0-7 sind Unity-Built-in-Layer
            {
                SerializedProperty sp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }
            Debug.LogWarning("[CrimeSim] Kein freier Layer für " + layerName + " gefunden.");
            return -1;
        }
    }
}
#endif
