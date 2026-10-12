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

            Material floor = TestLevelBuilder.GetMaterial("CC_Floor", new Color(0.18f, 0.18f, 0.2f));
            Material pedestalMat = TestLevelBuilder.GetMaterial("CC_Pedestal", new Color(0.5f, 0.5f, 0.55f));

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);

            var fillGo = new GameObject("Fill Light");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 8f;
            fill.intensity = 1.5f;
            fillGo.transform.position = new Vector3(-1.5f, 2f, 2.5f);

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

            // Kamera: Orbit-Kamera, Charakter rechts im Bild, links Platz für die UI
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
            cam.fieldOfView = 40f;
            camGo.AddComponent<AudioListener>();
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
