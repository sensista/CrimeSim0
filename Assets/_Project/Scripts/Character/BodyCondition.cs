using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CrimeSim
{
    /// <summary>
    /// Veränderlicher Körperzustand (Fett/Muskel) des Spielers. Später gekoppelt an Essen, Training, Aktivität.
    /// Änderungen werden weich auf die Optik übertragen und in character.json gespeichert.
    /// </summary>
    public class BodyCondition : MonoBehaviour
    {
        [SerializeField] private CharacterAppearanceApplier applier;
        [Tooltip("Zusätzliche Ziele, die IBodyShapeTarget implementieren (z. B. Blendshape-Meshes).")]
        [SerializeField] private MonoBehaviour[] extraTargets;
        [SerializeField] private BodyStats stats = new BodyStats(0.3f, 0.3f);
        [SerializeField] private bool loadFromSave = true;
        [SerializeField] private bool saveOnChange = true;
        [Tooltip("Geschwindigkeit der sichtbaren Anpassung (Anteil pro Sekunde).")]
        [SerializeField] private float visualLerpSpeed = 2f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugKeys = true;
        [SerializeField] private float debugStep = 0.1f;

        public BodyStats Stats => stats;
        public float BodyFat => stats.bodyFat;
        public float Muscle => stats.muscle;

        /// <summary>Wird nach jeder Änderung der Zielwerte ausgelöst.</summary>
        public event Action<BodyStats> OnChanged;

        private BodyStats visual;

        private void Awake()
        {
            if (applier == null) applier = GetComponentInChildren<CharacterAppearanceApplier>();
            if (loadFromSave) stats = CharacterSaveData.LoadOrDefault().body;
            stats = stats.Clamped();
            visual = stats;
        }

        private void Start() => PushVisual();

        public void AddFat(float delta) => ApplyChange(delta, 0f);
        public void AddMuscle(float delta) => ApplyChange(0f, delta);

        public void ApplyChange(float fatDelta, float muscleDelta)
        {
            SetStats(new BodyStats(stats.bodyFat + fatDelta, stats.muscle + muscleDelta));
        }

        public void SetStats(BodyStats newStats, bool instant = false)
        {
            stats = newStats.Clamped();
            if (instant) { visual = stats; PushVisual(); }
            if (saveOnChange) Save();
            OnChanged?.Invoke(stats);
        }

        public void Save()
        {
            var data = CharacterSaveData.LoadOrDefault();
            if (applier != null) data.appearance = applier.Appearance;
            data.body = stats;
            data.Save();
        }

        private void Update()
        {
            HandleDebugKeys();

            if (!Mathf.Approximately(visual.bodyFat, stats.bodyFat) || !Mathf.Approximately(visual.muscle, stats.muscle))
            {
                float step = visualLerpSpeed * Time.deltaTime;
                visual.bodyFat = Mathf.MoveTowards(visual.bodyFat, stats.bodyFat, step);
                visual.muscle = Mathf.MoveTowards(visual.muscle, stats.muscle, step);
                PushVisual();
            }
        }

        private void PushVisual()
        {
            if (applier != null) applier.SetBodyShape(visual.bodyFat, visual.muscle);
            if (extraTargets == null) return;
            foreach (var mb in extraTargets)
                if (mb is IBodyShapeTarget t && !ReferenceEquals(mb, applier)) t.SetBodyShape(visual.bodyFat, visual.muscle);
        }

        private void HandleDebugKeys()
        {
            if (!enableDebugKeys) return;
            float fat = 0f, mus = 0f;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) fat += debugStep;
            if (kb.f2Key.wasPressedThisFrame) fat -= debugStep;
            if (kb.f3Key.wasPressedThisFrame) mus += debugStep;
            if (kb.f4Key.wasPressedThisFrame) mus -= debugStep;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) fat += debugStep;
            if (Input.GetKeyDown(KeyCode.F2)) fat -= debugStep;
            if (Input.GetKeyDown(KeyCode.F3)) mus += debugStep;
            if (Input.GetKeyDown(KeyCode.F4)) mus -= debugStep;
#endif
            if (fat != 0f || mus != 0f)
            {
                ApplyChange(fat, mus);
                Debug.Log($"[CrimeSim] Körper: Fett {stats.bodyFat:0.00}, Muskel {stats.muscle:0.00}");
            }
        }
    }
}
