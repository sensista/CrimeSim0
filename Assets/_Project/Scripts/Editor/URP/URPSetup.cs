#if UNITY_EDITOR && CRIMESIM_URP
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CrimeSim.EditorTools
{
    /// <summary>
    /// Richtet URP ein: Pipeline-Asset + Renderer anlegen, in Graphics- und Quality-Settings eintragen,
    /// Standard-Materialien unter Assets/_Project nach URP/Lit konvertieren.
    /// Diese Assembly wird nur kompiliert, wenn das URP-Paket installiert ist (CRIMESIM_URP).
    /// </summary>
    public static class URPSetup
    {
        private const string Folder = "Assets/_Project/Settings/URP";
        private const string RendererPath = Folder + "/CrimeSim_URP_Renderer.asset";
        private const string AssetPath = Folder + "/CrimeSim_URP.asset";

        [MenuItem("CrimeSim/URP einrichten")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                Directory.CreateDirectory(Folder);
                AssetDatabase.Refresh();
            }

            // Renderer Data
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            bool createdRenderer = false;
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                createdRenderer = true;
            }

            // Pipeline Asset
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            bool createdPipeline = false;
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, AssetPath);
                createdPipeline = true;
            }
            // Kantenglättung: volle Auflösung + 4x MSAA
            pipeline.renderScale = 1f;
            pipeline.msaaSampleCount = 4;
            // Schatten: hohe Auflösung, weiche Schatten, 4 Kaskaden
            var pso = new SerializedObject(pipeline);
            void SetInt(string n, int v) { var p = pso.FindProperty(n); if (p != null) p.intValue = v; }
            void SetBool(string n, bool v) { var p = pso.FindProperty(n); if (p != null) p.boolValue = v; }
            void SetFloat(string n, float v) { var p = pso.FindProperty(n); if (p != null) p.floatValue = v; }
            SetInt("m_MainLightShadowmapResolution", 4096);
            SetInt("m_AdditionalLightsShadowmapResolution", 4096);
            SetBool("m_AdditionalLightShadowsSupported", true);
            SetFloat("m_ShadowDistance", 30f);
            SetInt("m_ShadowCascadeCount", 4);
            SetBool("m_SoftShadowsSupported", true);
            SetInt("m_SoftShadowQuality", 3);
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();

            // Graphics Settings
            GraphicsSettings.defaultRenderPipeline = pipeline;

            // Alle Quality-Stufen
            int current = QualitySettings.GetQualityLevel();
            int levels = QualitySettings.names.Length;
            for (int i = 0; i < levels; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            // Materialien konvertieren
            int converted = 0, skipped = 0;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogError("[CrimeSim] Shader 'Universal Render Pipeline/Lit' nicht gefunden. Ist URP fertig importiert?");
            }
            else
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/_Project" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null || mat.shader == null || mat.shader.name != "Standard") { skipped++; continue; }

                    Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Vector2 scale = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                    Vector2 offset = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                    float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                    float smooth = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.5f;

                    mat.shader = lit;
                    mat.SetColor("_BaseColor", color);
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", scale);
                    mat.SetTextureOffset("_BaseMap", offset);
                    mat.SetFloat("_Metallic", metallic);
                    mat.SetFloat("_Smoothness", smooth);
                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[CrimeSim] URP eingerichtet:\n" +
                      $"- Pipeline-Asset: {AssetPath} ({(createdPipeline ? "neu" : "vorhanden")})\n" +
                      $"- Renderer: {RendererPath} ({(createdRenderer ? "neu" : "vorhanden")})\n" +
                      $"- Graphics Settings + {levels} Quality-Stufen zugewiesen\n" +
                      $"- Materialien konvertiert: {converted}, unverändert: {skipped}");
        }
    }
}
#endif
