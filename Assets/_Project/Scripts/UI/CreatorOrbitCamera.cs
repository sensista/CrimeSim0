using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CrimeSim
{
    /// <summary>
    /// Orbit-Kamera für den Character Creator.
    /// Mittlere Maustaste: drehen. Shift + mittlere Maustaste: vertikal verschieben. Mausrad: Zoom.
    /// Der Charakter wird rechts von der Bildmitte gezeigt, damit das Menü links ihn nicht verdeckt.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CreatorOrbitCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Optional: für Größenanpassung der Höhengrenzen (heightScale).")]
        [SerializeField] private CharacterAppearanceApplier character;

        [Header("Start")]
        [SerializeField] private float startYaw = 180f;   // Kamera vor dem Charakter (Charakter schaut +Z)
        [SerializeField] private float startPitch = 5f;
        [SerializeField] private float startHeight = 0.95f;
        [SerializeField] private float frameHeight = 2.1f; // sichtbare Höhe beim Start (m)

        [Header("Grenzen")]
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float minDistance = 0.6f;
        [SerializeField] private float maxDistance = 6f;
        [SerializeField] private float minHeight = 0.1f;  // Füße
        [SerializeField] private float maxHeight = 1.75f; // Kopf (bei heightScale 1)

        [Header("Steuerung")]
        [SerializeField] private float orbitSpeed = 0.25f;
        [SerializeField] private float panSpeed = 0.004f;
        [SerializeField] private float zoomSpeed = 0.15f;
        [SerializeField] private float smoothTime = 0.12f;
        [Tooltip("Charakter-Position horizontal im Bild: 0 = Mitte, 1 = rechter Rand.")]
        [Range(0f, 0.8f)] [SerializeField] private float screenOffset = 0.3f;

        private Camera cam;
        private float yaw, pitch, distance, height;
        private float curYaw, curPitch, curDistance, curHeight;
        private float vYaw, vPitch, vDistance, vHeight;
        private bool dragging;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ResetView(true);
        }

        /// <summary>Startansicht wiederherstellen (ganzer Charakter sichtbar).</summary>
        public void ResetView() => ResetView(false);

        public void ResetView(bool instant)
        {
            yaw = startYaw;
            pitch = startPitch;
            height = startHeight * HeightScale;
            float halfFov = (cam != null ? cam.fieldOfView : 40f) * 0.5f * Mathf.Deg2Rad;
            distance = Mathf.Clamp(frameHeight * HeightScale * 0.5f / Mathf.Tan(halfFov), minDistance, maxDistance);
            if (instant)
            {
                curYaw = yaw; curPitch = pitch; curDistance = distance; curHeight = height;
                vYaw = vPitch = vDistance = vHeight = 0f;
                ApplyTransform();
            }
        }

        private float HeightScale => character != null ? character.Appearance.heightScale : 1f;

        private void LateUpdate()
        {
            if (target == null) return;
            HandleInput();

            curYaw = Mathf.SmoothDampAngle(curYaw, yaw, ref vYaw, smoothTime);
            curPitch = Mathf.SmoothDamp(curPitch, pitch, ref vPitch, smoothTime);
            curDistance = Mathf.SmoothDamp(curDistance, distance, ref vDistance, smoothTime);
            curHeight = Mathf.SmoothDamp(curHeight, height, ref vHeight, smoothTime);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (target == null) return;
            Quaternion rot = Quaternion.Euler(curPitch, curYaw, 0f);
            Vector3 pivot = target.position + Vector3.up * curHeight;
            Vector3 pos = pivot - rot * Vector3.forward * curDistance;

            // Kamera nach links schieben, damit der Charakter rechts im Bild steht
            float aspect = cam != null ? cam.aspect : 16f / 9f;
            float halfFov = (cam != null ? cam.fieldOfView : 40f) * 0.5f * Mathf.Deg2Rad;
            float halfWidth = curDistance * Mathf.Tan(halfFov) * aspect;
            pos -= rot * Vector3.right * (screenOffset * halfWidth);

            transform.SetPositionAndRotation(pos, rot);
        }

        private void HandleInput()
        {
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool middleDown, middleHeld, shift;
            Vector2 delta;
            float scroll;
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null) return;
            middleDown = mouse.middleButton.wasPressedThisFrame;
            middleHeld = mouse.middleButton.isPressed;
            delta = mouse.delta.ReadValue();
            scroll = mouse.scroll.ReadValue().y / 120f;
            shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
#elif ENABLE_LEGACY_INPUT_MANAGER
            middleDown = Input.GetMouseButtonDown(2);
            middleHeld = Input.GetMouseButton(2);
            delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
            scroll = Input.mouseScrollDelta.y;
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#else
            middleDown = middleHeld = shift = false;
            delta = Vector2.zero;
            scroll = 0f;
#endif
            // Ziehen nur starten, wenn der Zeiger nicht über der UI ist
            if (middleDown) dragging = !overUI;
            if (!middleHeld) dragging = false;

            if (dragging)
            {
                if (shift)
                {
                    height -= delta.y * panSpeed * curDistance;
                }
                else
                {
                    yaw += delta.x * orbitSpeed;
                    pitch -= delta.y * orbitSpeed;
                }
            }

            if (!overUI && Mathf.Abs(scroll) > 0.001f)
                distance *= 1f - scroll * zoomSpeed;

            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            height = Mathf.Clamp(height, minHeight * HeightScale, maxHeight * HeightScale);
        }
    }
}
