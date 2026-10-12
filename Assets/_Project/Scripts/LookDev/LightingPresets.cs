using UnityEngine;
using UnityEngine.Rendering;

namespace CrimeSim
{
    /// <summary>
    /// Licht-Stimmungen (Tag / Abend / Nacht): je Stimmung ein Kind-Objekt mit Lichtern und eigenem
    /// Post-Processing-Volume. Set(i) schaltet das passende Kind an und setzt das Umgebungslicht.
    /// Wird vom Editor-Menü erzeugt (StylizedLookBuilder).
    /// </summary>
    public class LightingPresets : MonoBehaviour
    {
        [System.Serializable]
        public class Preset
        {
            public string name;
            public GameObject root;
            public Color ambientSky = Color.gray;
            public Color ambientEquator = Color.gray;
            public Color ambientGround = Color.black;
            public Color background = Color.black;
        }

        [SerializeField] private Preset[] presets = new Preset[0];
        [SerializeField] private int startPreset = 1;
        [SerializeField] private Camera targetCamera;

        public int Count => presets.Length;
        public int Current { get; private set; } = -1;
        public string NameOf(int i) => i >= 0 && i < presets.Length ? presets[i].name : "";

        private void Start() => Set(startPreset);

        public void Set(int index)
        {
            if (presets.Length == 0) return;
            index = Mathf.Clamp(index, 0, presets.Length - 1);
            for (int i = 0; i < presets.Length; i++)
                if (presets[i].root != null) presets[i].root.SetActive(i == index);
            var p = presets[index];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.ambientSky;
            RenderSettings.ambientEquatorColor = p.ambientEquator;
            RenderSettings.ambientGroundColor = p.ambientGround;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera != null) targetCamera.backgroundColor = p.background;
            Current = index;
        }
    }
}
