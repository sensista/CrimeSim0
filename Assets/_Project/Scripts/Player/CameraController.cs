using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CrimeSim
{
    /// <summary>
    /// Umschaltbare Kamera: Third-Person-Orbit und First-Person (Taste V).
    /// Bleibt dauerhaft im Projekt, u. a. um Animationen zu pruefen.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Ziele")]
        [SerializeField] private Transform target;      // Spieler-Root
        [SerializeField] private Transform headAnchor;  // Kopfposition fuer First Person

        [Header("Third Person")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private float distance = 4f;
        [SerializeField] private float minDistance = 0.5f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;

        [Header("Steuerung")]
        [SerializeField] private float mouseSensitivity = 0.15f;
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 75f;
        [SerializeField] private float fpMinPitch = -85f;
        [SerializeField] private float fpMaxPitch = 85f;
        [SerializeField] private bool lockCursor = true;

        [Header("First Person")]
        [SerializeField] private bool hideBodyInFirstPerson = true;
        [SerializeField] private string playerBodyLayer = "PlayerBody";

        [Header("Uebergang")]
        [SerializeField] private float transitionTime = 0.25f;
        [SerializeField] private bool startInFirstPerson = false;

        public bool IsFirstPerson { get; private set; }
        public float Yaw => yaw;

        private Camera cam;
        private float yaw, pitch;
        private float blend;          // 0 = Third, 1 = First
        private float blendVelocity;
        private float currentDistance;
        private int defaultCullingMask;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam == null) cam = GetComponentInChildren<Camera>();
            if (cam != null) defaultCullingMask = cam.cullingMask;
            IsFirstPerson = startInFirstPerson;
            blend = IsFirstPerson ? 1f : 0f;
            currentDistance = distance;
            if (target != null) yaw = target.eulerAngles.y;
            ApplyCullingMask();
        }

        private void OnEnable()
        {
            if (lockCursor) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        private void LateUpdate()
        {
            if (target == null) return;

            if (TogglePressed()) SetFirstPerson(!IsFirstPerson);

            Vector2 look = ReadLookDelta() * mouseSensitivity;
            yaw += look.x;
            pitch -= look.y;
            float lo = Mathf.Lerp(minPitch, fpMinPitch, blend);
            float hi = Mathf.Lerp(maxPitch, fpMaxPitch, blend);
            pitch = Mathf.Clamp(pitch, lo, hi);

            blend = Mathf.SmoothDamp(blend, IsFirstPerson ? 1f : 0f, ref blendVelocity, transitionTime);
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);

            // Third-Person-Position mit Kollision
            Vector3 pivot = target.position + pivotOffset;
            float wanted = distance;
            if (Physics.SphereCast(pivot, collisionRadius, rot * Vector3.back, out RaycastHit hit,
                    distance, collisionMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(target))
            {
                wanted = Mathf.Max(minDistance, hit.distance);
            }
            currentDistance = wanted < currentDistance
                ? wanted
                : Mathf.Lerp(currentDistance, wanted, 10f * Time.deltaTime);
            Vector3 tpPos = pivot + rot * Vector3.back * currentDistance;

            // First-Person-Position
            Vector3 fpPos = headAnchor != null ? headAnchor.position : pivot;

            transform.SetPositionAndRotation(Vector3.Lerp(tpPos, fpPos, blend), rot);

            // Koerper erst ausblenden, wenn die Kamera fast im Kopf ist
            ApplyCullingMask();
        }

        public void SetFirstPerson(bool firstPerson)
        {
            IsFirstPerson = firstPerson;
        }

        private void ApplyCullingMask()
        {
            if (cam == null) return;
            int layer = LayerMask.NameToLayer(playerBodyLayer);
            if (!hideBodyInFirstPerson || layer < 0) { cam.cullingMask = defaultCullingMask; return; }
            bool hide = IsFirstPerson && blend > 0.9f;
            cam.cullingMask = hide ? defaultCullingMask & ~(1 << layer) : defaultCullingMask;
        }

        private static bool TogglePressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.V);
#else
            return false;
#endif
        }

        private static Vector2 ReadLookDelta()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            // Legacy-Achsen sind bereits skaliert, daher Faktor fuer aehnliches Gefuehl
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }
    }
}
