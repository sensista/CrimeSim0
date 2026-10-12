using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CrimeSim
{
    /// <summary>
    /// Stil-Testszene: Figur-Regler im Inspector (auch im Play-Modus live) und Kameraansichten.
    /// Taste 1 = Ganzkörper (Orbit-Kamera), Taste 2 = Gesicht (Nahaufnahme), Taste 3 = Halbtotale.
    /// </summary>
    public class LookDevController : MonoBehaviour
    {
        [SerializeField] private MPFBShapeDriver figure;
        [SerializeField] private CreatorOrbitCamera orbit;

        [Header("Figur (live)")]
        [Range(0f, 1f)] public float gender = 0f;
        [Range(0f, 1f)] public float muscle = 0.5f;
        [Range(0f, 1f)] public float weight = 0.5f;
        [Tooltip("Alter-Regler; unter ca. 0.5 werden Figuren jünger als 25 – für Erwachsene bei 0.5 oder höher lassen.")]
        [Range(0.5f, 1f)] public float age = 0.5f;
        public Color skinColor = new Color(0.87f, 0.69f, 0.55f);

        [Header("Gesichts-Kamera")]
        [SerializeField] private float faceDistance = 0.55f;
        [SerializeField] private float faceFov = 28f;

        private enum View { Body, Face, Half }
        private View view = View.Body;
        private Transform head;
        private Camera cam;
        private float defaultFov;
        private float lg = -1, lm = -1, lw = -1, la = -1;
        private Color lc;

        private void Start()
        {
            if (orbit != null) { cam = orbit.GetComponent<Camera>(); defaultFov = cam.fieldOfView; }
            if (figure != null)
                foreach (var t in figure.GetComponentsInChildren<Transform>(true))
                    if (t.name == "head") { head = t; break; }
            Push(true);
        }

        private void Push(bool force)
        {
            if (figure == null) return;
            if (!force && gender == lg && muscle == lm && weight == lw && age == la && skinColor == lc) return;
            figure.SetValue("gender", gender);
            figure.SetValue("muscle", muscle);
            figure.SetValue("weight", weight);
            figure.SetValue("age", Mathf.Max(0.5f, age));
            figure.SkinColor = skinColor;
            lg = gender; lm = muscle; lw = weight; la = age; lc = skinColor;
        }

        private void Update()
        {
            Push(false);
            if (Pressed(1)) SetView(View.Body);
            if (Pressed(2)) SetView(View.Face);
            if (Pressed(3)) SetView(View.Half);
        }

        private void SetView(View v)
        {
            view = v;
            if (orbit == null) return;
            orbit.enabled = v == View.Body;
            if (v == View.Body) { cam.fieldOfView = defaultFov; orbit.ResetView(); }
        }

        private void LateUpdate()
        {
            if (view == View.Body || cam == null || figure == null) return;
            Transform root = figure.transform;
            Vector3 focus = head != null ? head.position + root.up * 0.06f : root.position + Vector3.up * 1.6f;
            if (view == View.Half) focus = Vector3.Lerp(focus, root.position, 0.3f);
            float dist = view == View.Face ? faceDistance : faceDistance * 3.2f;
            cam.fieldOfView = faceFov;
            Vector3 dir = Quaternion.AngleAxis(-18f, Vector3.up) * root.forward;
            cam.transform.position = focus + dir * dist + Vector3.up * 0.02f;
            cam.transform.rotation = Quaternion.LookRotation(focus - cam.transform.position, Vector3.up);
        }

        private static bool Pressed(int n)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return false;
            return n == 1 ? kb.digit1Key.wasPressedThisFrame : n == 2 ? kb.digit2Key.wasPressedThisFrame : kb.digit3Key.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(n == 1 ? KeyCode.Alpha1 : n == 2 ? KeyCode.Alpha2 : KeyCode.Alpha3);
#else
            return false;
#endif
        }
    }
}
