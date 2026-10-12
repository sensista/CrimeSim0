using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace CrimeSim
{
    /// <summary>
    /// Character Creator mit zur Laufzeit erzeugter UGUI-Oberfläche (ohne TextMeshPro).
    /// Mausziehen außerhalb der UI dreht die Vorschau.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        [SerializeField] private CharacterAppearanceApplier preview;
        [SerializeField] private CreatorOrbitCamera orbitCamera;
        [SerializeField] private string gameSceneName = "TestLevel";
        [SerializeField] private float panelWidth = 360f;

        private static readonly Color[] SkinColors =
        {
            new Color(0.98f, 0.85f, 0.75f), new Color(0.92f, 0.74f, 0.6f), new Color(0.82f, 0.62f, 0.46f),
            new Color(0.65f, 0.45f, 0.3f), new Color(0.45f, 0.3f, 0.2f), new Color(0.3f, 0.2f, 0.13f)
        };
        private static readonly Color[] HairColors =
        {
            new Color(0.08f, 0.06f, 0.05f), new Color(0.3f, 0.18f, 0.1f), new Color(0.55f, 0.35f, 0.18f),
            new Color(0.9f, 0.78f, 0.5f), new Color(0.6f, 0.2f, 0.1f), new Color(0.75f, 0.75f, 0.75f)
        };
        private static readonly Color[] ClothColors =
        {
            new Color(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.9f, 0.9f), new Color(0.7f, 0.15f, 0.15f),
            new Color(0.2f, 0.35f, 0.65f), new Color(0.25f, 0.45f, 0.25f), new Color(0.55f, 0.45f, 0.3f),
            new Color(0.85f, 0.65f, 0.15f), new Color(0.45f, 0.25f, 0.55f)
        };
        private static readonly string[] HairNames = { "Glatze", "Kurz", "Lang", "Irokese", "Dutt" };
        private static readonly string[] TopNames = { "T-Shirt", "Langarm", "Tanktop" };
        private static readonly string[] BottomNames = { "Lange Hose", "Shorts" };
        private static readonly string[] ShoesNames = { "Sneaker", "Stiefel", "Barfuß" };

        private CharacterAppearance data;
        private BodyStats body;
        private Font font;
        private DefaultControls.Resources res;
        private Transform panel;
        private Action refreshUI;

        private void Start()
        {
            if (preview == null) preview = FindFirstObjectByType<CharacterAppearanceApplier>();
            if (orbitCamera == null) orbitCamera = FindFirstObjectByType<CreatorOrbitCamera>();

            data = preview != null ? preview.Appearance.Clone() : new CharacterAppearance();
            body = preview != null ? preview.Body : BodyStats.Default;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            res = new DefaultControls.Resources();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
            BuildUI();
            refreshUI?.Invoke();
        }

        // ---------------------------------------------------------------- Aktionen

        private void ApplyAppearance()
        {
            if (preview != null) preview.Apply(data.Clone(), body);
            refreshUI?.Invoke();
        }

        private void ApplyBody()
        {
            if (preview != null) preview.SetBodyShape(body.bodyFat, body.muscle);
        }

        private void Randomize()
        {
            var r = new System.Random();
            float F(float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());
            data.heightScale = F(0.9f, 1.1f);
            data.build = F(0.85f, 1.15f);
            data.headSize = F(0.92f, 1.08f);
            data.skinColor = SkinColors[r.Next(SkinColors.Length)];
            data.hairStyle = r.Next(StyleCount(CharacterSlot.Hair));
            data.hairColor = HairColors[r.Next(HairColors.Length)];
            data.topStyle = r.Next(StyleCount(CharacterSlot.Top));
            data.topColor = ClothColors[r.Next(ClothColors.Length)];
            data.bottomStyle = r.Next(StyleCount(CharacterSlot.Bottom));
            data.bottomColor = ClothColors[r.Next(ClothColors.Length)];
            data.shoesStyle = r.Next(StyleCount(CharacterSlot.Shoes));
            data.shoesColor = ClothColors[r.Next(ClothColors.Length)];
            body = new BodyStats(F(0.1f, 0.7f), F(0.1f, 0.7f));
            ApplyAppearance();
        }

        private void Finish()
        {
            if (string.IsNullOrWhiteSpace(data.characterName)) data.characterName = "Neuer Charakter";
            new CharacterSaveData { appearance = data.Clone(), body = body.Clamped() }.Save();
            if (Application.CanStreamedLevelBeLoaded(gameSceneName)) SceneManager.LoadScene(gameSceneName);
            else Debug.LogError("[CrimeSim] Szene '" + gameSceneName + "' ist nicht in den Build Settings.");
        }

        private int StyleCount(CharacterSlot slot) =>
            CharacterAppearanceApplier.StyleCount(slot, preview != null ? preview.Library : null);

        private static int Wrap(int v, int count) => count <= 0 ? 0 : ((v % count) + count) % count;

        // ---------------------------------------------------------------- UI-Aufbau

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindFirstObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("CharacterCreatorCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel: linker Rand, volle Höhe, feste Breite, scrollbar
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panelGo.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(panelWidth, 0f);
            prt.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(panelGo.transform, false);
            var vrt = (RectTransform)viewportGo.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var crt = (RectTransform)contentGo.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = Vector2.zero;
            crt.anchoredPosition = Vector2.zero;
            contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(14, 14, 14, 14);
            vlg.spacing = 5f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var scroll = panelGo.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            panel = contentGo.transform;

            Label("Character Creator", 28, FontStyle.Bold);

            Label("Name", 20);
            var input = DefaultControls.CreateInputField(res).GetComponent<InputField>();
            Place(input.gameObject, 40f);
            input.text = data.characterName;
            input.characterLimit = 24;
            input.onEndEdit.AddListener(v => data.characterName = v);

            SliderRow("Größe", 0.85f, 1.15f, () => data.heightScale, v => { data.heightScale = v; ApplyAppearance(); });
            SliderRow("Statur", 0.8f, 1.25f, () => data.build, v => { data.build = v; ApplyAppearance(); });
            SliderRow("Kopfgröße", 0.85f, 1.2f, () => data.headSize, v => { data.headSize = v; ApplyAppearance(); });
            SliderRow("Körperfett", 0f, 1f, () => body.bodyFat, v => { body.bodyFat = v; ApplyBody(); });
            SliderRow("Muskeln", 0f, 1f, () => body.muscle, v => { body.muscle = v; ApplyBody(); });

            ColorRow("Hautfarbe", SkinColors, c => data.skinColor = c);
            StyleRow("Frisur", CharacterSlot.Hair, HairNames, () => data.hairStyle, v => data.hairStyle = v);
            ColorRow("Haarfarbe", HairColors, c => data.hairColor = c);
            StyleRow("Oberteil", CharacterSlot.Top, TopNames, () => data.topStyle, v => data.topStyle = v);
            ColorRow("Farbe Oberteil", ClothColors, c => data.topColor = c);
            StyleRow("Hose", CharacterSlot.Bottom, BottomNames, () => data.bottomStyle, v => data.bottomStyle = v);
            ColorRow("Farbe Hose", ClothColors, c => data.bottomColor = c);
            StyleRow("Schuhe", CharacterSlot.Shoes, ShoesNames, () => data.shoesStyle, v => data.shoesStyle = v);
            ColorRow("Farbe Schuhe", ClothColors, c => data.shoesColor = c);

            var row = Row(50f);
            MakeButton(row, "Zufall", Randomize);
            MakeButton(row, "Fertig", Finish);

            var viewRow = Row(40f);
            MakeButton(viewRow, "Ansicht zurücksetzen", () => { if (orbitCamera != null) orbitCamera.ResetView(); });
            var help = Label("Mittlere Maustaste: drehen\nShift + mittlere Maustaste: hoch/runter\nMausrad: Zoom", 14, FontStyle.Italic);
            help.GetComponent<LayoutElement>().preferredHeight = 64f;

            // InputField-Name bei Zufall nicht ändern; restliche Werte über refreshUI aktualisieren
            FixFonts(canvasGo);
        }

        private Text Label(string text, int size, FontStyle style = FontStyle.Normal, Transform parent = null)
        {
            var go = DefaultControls.CreateText(res);
            go.name = "Label";
            go.transform.SetParent(parent != null ? parent : panel, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size + 8f;
            return t;
        }

        private void Place(GameObject go, float height, Transform parent = null)
        {
            go.transform.SetParent(parent != null ? parent : panel, false);
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
        }

        private Transform Row(float height)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            Place(go, height);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            return go.transform;
        }

        private void SliderRow(string title, float min, float max, Func<float> get, Action<float> set)
        {
            var row = Row(30f);
            var label = Label(title, 18, FontStyle.Normal, row);
            label.GetComponent<LayoutElement>().preferredWidth = 110f;
            label.GetComponent<LayoutElement>().flexibleWidth = 0f;

            var sliderGo = DefaultControls.CreateSlider(res);
            sliderGo.transform.SetParent(row, false);
            sliderGo.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var slider = sliderGo.GetComponent<Slider>();
            TintSlider(sliderGo);
            slider.minValue = min;
            slider.maxValue = max;
            slider.SetValueWithoutNotify(get());
            slider.onValueChanged.AddListener(v => set(v));
            refreshUI += () => slider.SetValueWithoutNotify(get());
        }

        private void ColorRow(string title, Color[] colors, Action<Color> set)
        {
            Label(title, 18);
            var row = Row(30f);
            foreach (var c in colors)
            {
                Color color = c;
                var b = MakeButton(row, "", () => { set(color); ApplyAppearance(); });
                b.GetComponent<Image>().color = color;
            }
        }

        private void StyleRow(string title, CharacterSlot slot, string[] names, Func<int> get, Action<int> set)
        {
            var row = Row(36f);
            var label = Label(title, 18, FontStyle.Normal, row);
            label.GetComponent<LayoutElement>().preferredWidth = 110f;
            MakeButton(row, "<", () => { set(Wrap(get() - 1, StyleCount(slot))); ApplyAppearance(); });
            var value = Label("", 18, FontStyle.Bold, row);
            value.alignment = TextAnchor.MiddleCenter;
            MakeButton(row, ">", () => { set(Wrap(get() + 1, StyleCount(slot))); ApplyAppearance(); });
            refreshUI += () =>
            {
                int i = get();
                value.text = i >= 0 && i < names.Length ? names[i] : "Stil " + (i + 1);
            };
        }

        private Button MakeButton(Transform parent, string text, Action onClick)
        {
            var go = DefaultControls.CreateButton(res);
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>();
            var t = go.GetComponentInChildren<Text>();
            if (t != null) { t.text = text; t.font = font; t.fontSize = 17; }
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => onClick());
            return b;
        }

        private static void TintSlider(GameObject sliderGo)
        {
            foreach (var img in sliderGo.GetComponentsInChildren<Image>(true))
            {
                if (img.name == "Background") img.color = new Color(0.25f, 0.25f, 0.25f);
                else if (img.name == "Fill") img.color = new Color(0.85f, 0.55f, 0.2f);
                else if (img.name == "Handle") img.color = Color.white;
            }
        }

        private void FixFonts(GameObject root)
        {
            foreach (var t in root.GetComponentsInChildren<Text>(true))
                if (t.font == null) t.font = font;
        }
    }
}
