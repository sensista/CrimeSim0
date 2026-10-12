#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace CrimeSim.EditorTools
{
    /// <summary>Erzeugt die Character-Creator-Szene per Menü.</summary>
    public static class CharacterCreatorSceneBuilder
    {
        [MenuItem("CrimeSim/Character Creator Szene erstellen")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material floor = TestLevelBuilder.GetMaterial("CC_Floor", new Color(0.07f, 0.065f, 0.07f));
            Material pedestalMat = TestLevelBuilder.GetMaterial("CC_Pedestal", new Color(0.16f, 0.15f, 0.16f));

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Floor";
            ground.GetComponent<Renderer>().sharedMaterial = floor;

            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.position = new Vector3(0f, 0.05f, 0f);
            pedestal.transform.localScale = new Vector3(1.4f, 0.05f, 1.4f);
            pedestal.GetComponent<Renderer>().sharedMaterial = pedestalMat;

            var preview = new GameObject("PreviewCharacter");
            preview.transform.position = new Vector3(0f, 0.1f, 0f);
            var applier = preview.AddComponent<CrimeSim.CharacterAppearanceApplier>();
            var appSo = new SerializedObject(applier);
            appSo.FindProperty("bodyModelPrefab").objectReferenceValue = PlayerPrefabBuilder.Build(); // null => Primitives
            appSo.ApplyModifiedPropertiesWithoutUndo();

            // Kamera: Orbit-Kamera, Charakter rechts im Bild, links Platz für die UI
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
            cam.allowMSAA = true;
            var camData = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam);
            camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camData.antialiasingQuality = UnityEngine.Rendering.Universal.AntialiasingQuality.High;
            camData.renderPostProcessing = true;
            // Licht-Stimmungen wie in der Stil-Testszene (Standard: Abend)
            StylizedLookBuilder.BuildLightRigs(cam, 3); // Innenraum warm als Standard
            camGo.transform.position = new Vector3(0f, 1f, 3.5f);
            camGo.transform.rotation = Quaternion.Euler(5f, 180f, 0f);
            var orbit = camGo.AddComponent<CrimeSim.CreatorOrbitCamera>();
            var oso = new SerializedObject(orbit);
            oso.FindProperty("target").objectReferenceValue = preview.transform;
            oso.FindProperty("character").objectReferenceValue = applier;
            oso.ApplyModifiedPropertiesWithoutUndo();

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var uiGo = new GameObject("CharacterCreatorUI");
            var ui = uiGo.AddComponent<CrimeSim.CharacterCreatorUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("preview").objectReferenceValue = applier;
            so.FindProperty("orbitCamera").objectReferenceValue = orbit;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TestLevelBuilder.CreatorScenePath);
            AssetDatabase.SaveAssets();
            TestLevelBuilder.AddScenesToBuildSettings();
            Debug.Log("[CrimeSim] Character Creator Szene erstellt: " + TestLevelBuilder.CreatorScenePath);
        }
    }
}
#endif
