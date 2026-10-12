using System;
using System.Collections.Generic;
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
    /// Character Creator (UGUI, ohne TextMeshPro). Mit MPFB-Spielerfigur: Tabs aus dem Shape-Manifest,
    /// Suche, Zufall, Vorlagen, Zurücksetzen pro Tab, Rückgängig (Strg+Z). Tabs werden erst beim
    /// ersten Öffnen aufgebaut. Ohne MPFB-Modell: einfache Regler für die Platzhalter-Figur.
    /// </summary>
    public class CharacterCreatorUI : MonoBehaviour
    {
        [SerializeField] private CharacterAppearanceApplier preview;
        [SerializeField] private CreatorOrbitCamera orbitCamera;
        [SerializeField] private string gameSceneName = "TestLevel";
        [SerializeField] private float panelWidth = 440f;

        private static readonly Color[] SkinColors =
        {
            new Color(1f, 0.9f, 0.82f), new Color(0.98f, 0.85f, 0.75f), new Color(0.92f, 0.74f, 0.6f),
            new Color(0.86f, 0.67f, 0.52f), new Color(0.78f, 0.58f, 0.42f), new Color(0.65f, 0.45f, 0.3f),
            new Color(0.53f, 0.36f, 0.24f), new Color(0.42f, 0.28f, 0.18f), new Color(0.32f, 0.21f, 0.14f),
            new Color(0.24f, 0.16f, 0.11f)
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

        private enum TabKind { Body, Modifiers, Skin, HairClothes }

        private class Tab
        {
            public string title;
            public TabKind kind;
            public string[] groups;      // MPFB-Gruppen (nur Modifiers)
            public GameObject container; // lazy
            public Action refresh;
            public Button button;
        }

        private readonly List<Tab> tabs = new List<Tab>
        {
            new Tab { title = "Körper", kind = TabKind.Body },
            new Tab { title = "Kopf", kind = TabKind.Modifiers, groups = new[] { "head", "neck", "forehead" } },
            new Tab { title = "Gesicht", kind = TabKind.Modifiers, groups = new[] { "eyes", "eyebrows", "nose", "mouth", "ears", "chin", "cheek" } },
            new Tab { title = "Torso", kind = TabKind.Modifiers, groups = new[] { "torso", "breast", "stomach", "hip", "pelvis", "buttocks" } },
            new Tab { title = "Arme", kind = TabKind.Modifiers, groups = new[] { "arms", "hands" } },
            new Tab { title = "Beine", kind = TabKind.Modifiers, groups = new[] { "legs", "feet" } },
            new Tab { title = "Asymmetrie", kind = TabKind.Modifiers, groups = new[] { "asym" } },
            new Tab { title = "Haut", kind = TabKind.Skin },
            new Tab { title = "Haare/Kleidung", kind = TabKind.HairClothes },
        };

        private struct Snapshot
        {
            public Dictionary<string, float> values;
            public BodyStats body;
            public Color skin;
            public CharacterAppearance appearance;
        }

        private CharacterAppearance data;
        private BodyStats body;
        private MPFBShapeDriver driver;
        private Font font;
        private DefaultControls.Resources res;
        private Transform panel;          // Wurzel der Tab-Container (Scroll-Content)
        private GameObject searchContainer;
        private Action searchRefresh;
        private Tab current;
        private readonly Stack<Snapshot> undo = new Stack<Snapshot>();
        private float lastChangeTime = -10f;
        private Text undoLabel;
        private const int MaxUndo = 60;

        private void Start()
        {
            if (preview == null) preview = FindFirstObjectByType<CharacterAppearanceApplier>();
            if (orbitCamera == null) orbitCamera = FindFirstObjectByType<CreatorOrbitCamera>();

            data = preview != null ? preview.Appearance.Clone() : new CharacterAppearance();
            body = preview != null ? preview.Body : BodyStats.Default;
            driver = preview != null ? preview.MPFB : null;
            if (driver != null && !driver.IsReady) driver = null;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            res = new DefaultControls.Resources();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            EnsureEventSystem();
            BuildUI();
            SelectTab(tabs[0]);
        }

        private void Update()
        {
            bool ctrl = false, z = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null) { ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed; z = kb.zKey.wasPressedThisFrame; }
#elif ENABLE_LEGACY_INPUT_MANAGER
            ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            z = Input.GetKeyDown(KeyCode.Z);
#endif
            if (ctrl && z) Undo();
        }

        // ---------------------------------------------------------------- Werte

        private float Get(string id)
        {
            if (id == "weight") return body.bodyFat;
            if (id == "muscle") return body.muscle;
            return driver != null ? driver.GetValue(id) : 0f;
        }

        private void Set(string id, float v)
        {
            PushUndoIfIdle();
            if (id == "weight" || id == "muscle")
            {
                if (id == "weight") body.bodyFat = Mathf.Clamp01(v); else body.muscle = Mathf.Clamp01(v);
                if (preview != null) preview.SetBodyShape(body.bodyFat, body.muscle);
                return;
            }
            if (driver != null) driver.SetValue(id, v);
        }

        private Snapshot Take() => new Snapshot
        {
            values = driver != null ? driver.Snapshot() : null,
            body = body,
            skin = driver != null ? driver.SkinColor : data.skinColor,
            appearance = data.Clone(),
        };

        private void PushUndo()
        {
            if (undo.Count >= MaxUndo)
            {
                // ältesten Eintrag verwerfen
                var arr = undo.ToArray();
                undo.Clear();
                for (int i = arr.Length - 2; i >= 0; i--) undo.Push(arr[i]);
            }
            undo.Push(Take());
            UpdateUndoLabel();
        }

        private void PushUndoIfIdle()
        {
            if (Time.unscaledTime - lastChangeTime > 0.75f) PushUndo();
            lastChangeTime = Time.unscaledTime;
        }

        private void Undo()
        {
            if (undo.Count == 0) return;
            var s = undo.Pop();
            body = s.body;
            data = s.appearance;
            if (driver != null)
            {
                driver.Restore(s.values);
                driver.SkinColor = s.skin;
            }
            data.skinColor = s.skin;
            if (preview != null)
            {
                if (driver != null) preview.SetBodyShape(body.bodyFat, body.muscle);
                else preview.Apply(data.Clone(), body);
            }
            lastChangeTime = -10f;
            UpdateUndoLabel();
            RefreshAll();
        }

        private void UpdateUndoLabel()
        {
            if (undoLabel != null) undoLabel.text = "Rückgängig (" + undo.Count + ")";
        }

        private void SetSkin(Color c)
        {
            PushUndo();
            data.skinColor = c;
            if (driver != null) driver.SkinColor = c;
            else if (preview != null) preview.Apply(data.Clone(), body);
        }

        // ---------------------------------------------------------------- Aktionen

        private void Randomize()
        {
            PushUndo();
            var r = new System.Random();
            Func<float, float, float> F = (a0, b0) => Mathf.Lerp(a0, b0, (float)r.NextDouble());
            Func<float> Gauss = () => // Box-Muller
            {
                double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
                return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
            };

            body = new BodyStats(F(0.15f, 0.75f), F(0.15f, 0.75f));
            data.skinColor = SkinColors[r.Next(SkinColors.Length)];
            if (driver != null)
            {
                driver.ResetAll();
                driver.SetValue("gender", r.NextDouble() < 0.5 ? F(0f, 0.2f) : F(0.8f, 1f));
                driver.SetValue("age", F(0.5f, 0.62f));
                driver.SetValue("height", F(0.35f, 0.65f));
                driver.SetValue("proportions", F(0.4f, 0.75f));
                float a = F(0f, 1f), b = F(0f, 1f), c = F(0f, 1f), sum = Mathf.Max(0.01f, a + b + c);
                driver.SetValue("african", a / sum);
                driver.SetValue("asian", b / sum);
                driver.SetValue("caucasian", c / sum);
                driver.SetValue("cupsize", F(0.3f, 0.7f));
                driver.SetValue("firmness", F(0.3f, 0.7f));
                foreach (var sl in driver.Manifest.sliders)
                {
                    if (sl.group == "asym") continue;
                    if (sl.Bipolar)
                    {
                        if (r.NextDouble() < 0.35) driver.SetValue(sl.id, Mathf.Clamp(Gauss() * 0.25f, -0.5f, 0.5f));
                    }
                    else if (r.NextDouble() < 0.12) driver.SetValue(sl.id, F(0f, 0.4f));
                }
                driver.SkinColor = data.skinColor;
                preview.SetBodyShape(body.bodyFat, body.muscle);
            }
            else
            {
                data.heightScale = F(0.9f, 1.1f);
                data.build = F(0.85f, 1.15f);
                data.headSize = F(0.92f, 1.08f);
                data.hairStyle = r.Next(StyleCount(CharacterSlot.Hair));
                data.hairColor = HairColors[r.Next(HairColors.Length)];
                data.topStyle = r.Next(StyleCount(CharacterSlot.Top));
                data.topColor = ClothColors[r.Next(ClothColors.Length)];
                data.bottomStyle = r.Next(StyleCount(CharacterSlot.Bottom));
                data.bottomColor = ClothColors[r.Next(ClothColors.Length)];
                data.shoesStyle = r.Next(StyleCount(CharacterSlot.Shoes));
                data.shoesColor = ClothColors[r.Next(ClothColors.Length)];
                if (preview != null) preview.Apply(data.Clone(), body);
            }
            RefreshAll();
        }

        private void Preset(float fat, float muscle, float proportions)
        {
            PushUndo();
            body = new BodyStats(fat, muscle);
            if (driver != null) driver.SetValue("proportions", proportions);
            if (preview != null) preview.SetBodyShape(body.bodyFat, body.muscle);
            RefreshAll();
        }

        private void ResetTab(Tab tab)
        {
            PushUndo();
            switch (tab.kind)
            {
                case TabKind.Body:
                    body = BodyStats.Default;
                    if (driver != null)
                        foreach (var id in MPFBShapeDriver.MacroIds) driver.SetValue(id, MPFBShapeDriver.DefaultValue(id));
                    else { data.heightScale = 1f; data.build = 1f; data.headSize = 1f; }
                    if (preview != null)
                    {
                        if (driver != null) preview.SetBodyShape(body.bodyFat, body.muscle);
                        else preview.Apply(data.Clone(), body);
                    }
                    break;
                case TabKind.Modifiers:
                    if (driver != null)
                        foreach (var sl in driver.Manifest.sliders)
                            if (Array.IndexOf(tab.groups, sl.group) >= 0) driver.SetValue(sl.id, 0f);
                    break;
                case TabKind.Skin:
                    data.skinColor = new CharacterAppearance().skinColor;
                    if (driver != null) driver.SkinColor = data.skinColor;
                    else if (preview != null) preview.Apply(data.Clone(), body);
                    break;
            }
            RefreshAll();
        }

        private void Finish()
        {
            if (string.IsNullOrWhiteSpace(data.characterName)) data.characterName = "Neuer Charakter";
            if (driver != null) driver.WriteTo(data);
            new CharacterSaveData { appearance = data.Clone(), body = body.Clamped() }.Save();
            if (Application.CanStreamedLevelBeLoaded(gameSceneName)) SceneManager.LoadScene(gameSceneName);
            else Debug.LogError("[CrimeSim] Szene '" + gameSceneName + "' ist nicht in den Build Settings.");
        }

        private int StyleCount(CharacterSlot slot) =>
            CharacterAppearanceApplier.StyleCount(slot, preview != null ? preview.Library : null);

        private static int Wrap(int v, int count) => count <= 0 ? 0 : ((v % count) + count) % count;

        private void RefreshAll()
        {
            foreach (var t in tabs) t.refresh?.Invoke();
            searchRefresh?.Invoke();
        }

        // ---------------------------------------------------------------- Tabs

        private void SelectTab(Tab tab)
        {
            current = tab;
            if (tab.container == null) BuildTab(tab);
            foreach (var t in tabs)
            {
                if (t.container != null) t.container.SetActive(t == tab && searchContainer == null);
                if (t.button != null) t.button.GetComponent<Image>().color = t == tab ? new Color(0.85f, 0.55f, 0.2f) : Color.white;
            }
            tab.refresh?.Invoke();
        }

        private GameObject NewContainer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(panel, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = 4f;
            v.childControlHeight = true;
            v.childControlWidth = true;
            v.childForceExpandHeight = false;
            v.childForceExpandWidth = true;
            return go;
        }

        private void BuildTab(Tab tab)
        {
            tab.container = NewContainer("Tab_" + tab.title);
            Transform c = tab.container.transform;
            Action refresh = null;

            var top = Row(32f, c);
            MakeButton(top, "Tab zurücksetzen", () => ResetTab(tab));

            switch (tab.kind)
            {
                case TabKind.Body: BuildBodyTab(c, ref refresh); break;
                case TabKind.Modifiers: BuildModifierTab(tab, c, ref refresh); break;
                case TabKind.Skin: BuildSkinTab(c); break;
                case TabKind.HairClothes: BuildHairClothesTab(c, ref refresh); break;
            }
            tab.refresh = refresh;
            FixFonts(tab.container);
        }

        private void BuildBodyTab(Transform c, ref Action refresh)
        {
            if (driver == null)
            {
                SliderRow(c, "Größe", 0.85f, 1.15f, () => data.heightScale, v => { data.heightScale = v; ApplyPrimitive(); }, ref refresh);
                SliderRow(c, "Statur", 0.8f, 1.25f, () => data.build, v => { data.build = v; ApplyPrimitive(); }, ref refresh);
                SliderRow(c, "Kopfgröße", 0.85f, 1.2f, () => data.headSize, v => { data.headSize = v; ApplyPrimitive(); }, ref refresh);
                SliderRow(c, "Körperfett", 0f, 1f, () => body.bodyFat, v => Set("weight", v), ref refresh);
                SliderRow(c, "Muskeln", 0f, 1f, () => body.muscle, v => Set("muscle", v), ref refresh);
                return;
            }
            var m = driver.Manifest;
            Header(c, "Grundform");
            MacroRow(c, "Geschlecht (w ↔ m)", "gender", 0f, 1f, v => v < 0.5f ? "weibl. " + v.ToString("0.00") : "männl. " + v.ToString("0.00"), ref refresh);
            MacroRow(c, "Alter", "age", 0.5f, 1f, v => Mathf.RoundToInt(25f + (v - 0.5f) * 130f) + " J.", ref refresh);
            MacroRow(c, "Größe", "height", 0f, 1f, v =>
            {
                float h = v < 0.5f ? Mathf.Lerp(m.minHeight, m.neutralHeight, v * 2f) : Mathf.Lerp(m.neutralHeight, m.maxHeight, v * 2f - 1f);
                return Mathf.RoundToInt(h * 100f) + " cm";
            }, ref refresh);
            MacroRow(c, "Körperfett", "weight", 0f, 1f, null, ref refresh);
            MacroRow(c, "Muskeln", "muscle", 0f, 1f, null, ref refresh);
            MacroRow(c, "Proportionen", "proportions", 0f, 1f, null, ref refresh);

            Header(c, "Vorlagen");
            var row = Row(32f, c);
            MakeButton(row, "Schmächtig", () => Preset(0.15f, 0.15f, 0.5f));
            MakeButton(row, "Kräftig", () => Preset(0.65f, 0.7f, 0.55f));
            var row2 = Row(32f, c);
            MakeButton(row2, "Sportlich", () => Preset(0.3f, 0.85f, 0.7f));
            MakeButton(row2, "Übergewichtig", () => Preset(0.95f, 0.35f, 0.4f));

            Header(c, "Herkunft (wird normalisiert)");
            MacroRow(c, "Afrikanisch", "african", 0f, 1f, null, ref refresh);
            MacroRow(c, "Asiatisch", "asian", 0f, 1f, null, ref refresh);
            MacroRow(c, "Europäisch", "caucasian", 0f, 1f, null, ref refresh);

            Header(c, "Brust (wirkt bei weiblicher Form)");
            MacroRow(c, "Körbchengröße", "cupsize", 0f, 1f, null, ref refresh);
            MacroRow(c, "Festigkeit", "firmness", 0f, 1f, null, ref refresh);
        }

        private void BuildModifierTab(Tab tab, Transform c, ref Action refresh)
        {
            if (driver == null)
            {
                Label("Nur mit MPFB-Spielerfigur verfügbar (CrimeSim → Spielerfigur Prefab erstellen).", 15, FontStyle.Italic, c)
                    .GetComponent<LayoutElement>().preferredHeight = 44f;
                return;
            }
            foreach (var group in tab.groups)
            {
                bool header = false;
                foreach (var sl in driver.Manifest.sliders)
                {
                    if (sl.group != group) continue;
                    if (!header) { Header(c, sl.category); header = true; }
                    ModifierRow(c, sl, ref refresh);
                }
            }
        }

        private void BuildSkinTab(Transform c)
        {
            Header(c, "Hautfarbe");
            var row = Row(34f, c);
            for (int i = 0; i < SkinColors.Length; i++)
            {
                Color color = SkinColors[i];
                var b = MakeButton(row, "", () => SetSkin(color));
                b.GetComponent<Image>().color = color;
                if (i == 4) row = Row(34f, c);
            }
            Label("Textur: MakeHuman-Hauttextur (CC0), mit der Farbe eingefärbt.", 14, FontStyle.Italic, c);
        }

        private void BuildHairClothesTab(Transform c, ref Action refresh)
        {
            if (driver != null)
            {
                Label("Haare und Kleidung folgen als MPFB-Assets (CC0) mit denselben Shape Keys.\nAktuell: Unterwäsche passend zum Geschlecht.", 15, FontStyle.Italic, c)
                    .GetComponent<LayoutElement>().preferredHeight = 60f;
                return;
            }
            ColorRow(c, "Haarfarbe", HairColors, col => data.hairColor = col);
            StyleRow(c, "Frisur", CharacterSlot.Hair, HairNames, () => data.hairStyle, v => data.hairStyle = v, ref refresh);
            StyleRow(c, "Oberteil", CharacterSlot.Top, TopNames, () => data.topStyle, v => data.topStyle = v, ref refresh);
            ColorRow(c, "Farbe Oberteil", ClothColors, col => data.topColor = col);
            StyleRow(c, "Hose", CharacterSlot.Bottom, BottomNames, () => data.bottomStyle, v => data.bottomStyle = v, ref refresh);
            ColorRow(c, "Farbe Hose", ClothColors, col => data.bottomColor = col);
            StyleRow(c, "Schuhe", CharacterSlot.Shoes, ShoesNames, () => data.shoesStyle, v => data.shoesStyle = v, ref refresh);
            ColorRow(c, "Farbe Schuhe", ClothColors, col => data.shoesColor = col);
        }

        private void ApplyPrimitive()
        {
            PushUndoIfIdle();
            if (preview != null) preview.Apply(data.Clone(), body);
        }

        // ---------------------------------------------------------------- Suche

        private void OnSearch(string text)
        {
            if (searchContainer != null) { Destroy(searchContainer); searchContainer = null; searchRefresh = null; }
            text = text != null ? text.Trim() : "";
            if (text.Length < 2 || driver == null)
            {
                if (current != null) SelectTab(current);
                return;
            }
            foreach (var t in tabs) if (t.container != null) t.container.SetActive(false);
            searchContainer = NewContainer("Suche");
            Transform c = searchContainer.transform;
            Action refresh = null;
            int count = 0;
            string q = text.ToLowerInvariant();
            foreach (var sl in driver.Manifest.sliders)
            {
                string hay = (sl.category + " " + sl.label + " " + sl.id).ToLowerInvariant();
                if (!hay.Contains(q)) continue;
                ModifierRow(c, sl, ref refresh, true);
                if (++count >= 80) break;
            }
            if (count == 0) Label("Keine Treffer.", 16, FontStyle.Italic, c);
            searchRefresh = refresh;
            refresh?.Invoke();
            FixFonts(searchContainer);
        }

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

            // Panel links, volle Höhe: oben feste Leiste, Mitte scrollbarer Tab-Inhalt, unten Buttons
            var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            panelGo.transform.SetParent(canvasGo.transform, false);
            var prt = (RectTransform)panelGo.transform;
            prt.anchorMin = new Vector2(0f, 0f);
            prt.anchorMax = new Vector2(0f, 1f);
            prt.pivot = new Vector2(0f, 0.5f);
            prt.sizeDelta = new Vector2(panelWidth, 0f);
            prt.anchoredPosition = Vector2.zero;
            panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);
            var pv = panelGo.GetComponent<VerticalLayoutGroup>();
            pv.padding = new RectOffset(12, 12, 12, 12);
            pv.spacing = 6f;
            pv.childControlHeight = true;
            pv.childControlWidth = true;
            pv.childForceExpandHeight = false;
            pv.childForceExpandWidth = true;
            Transform root = panelGo.transform;

            Label("Character Creator", 26, FontStyle.Bold, root);

            var nameRow = Row(36f, root);
            var nl = Label("Name", 18, FontStyle.Normal, nameRow);
            nl.GetComponent<LayoutElement>().preferredWidth = 70f;
            var input = DefaultControls.CreateInputField(res).GetComponent<InputField>();
            input.transform.SetParent(nameRow, false);
            input.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            input.text = data.characterName;
            input.characterLimit = 24;
            input.onEndEdit.AddListener(v => data.characterName = v);

            // Tab-Leiste
            var tabBar = new GameObject("Tabs", typeof(RectTransform), typeof(GridLayoutGroup));
            tabBar.transform.SetParent(root, false);
            var grid = tabBar.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2((panelWidth - 24f - 8f) / 3f, 30f);
            grid.spacing = new Vector2(4f, 4f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            int rows = Mathf.CeilToInt(tabs.Count / 3f);
            var tle = tabBar.AddComponent<LayoutElement>();
            tle.preferredHeight = rows * 34f;
            tle.minHeight = rows * 34f;
            foreach (var t in tabs)
            {
                Tab tab = t;
                tab.button = MakeButton(tabBar.transform, tab.title, () =>
                {
                    if (searchField != null && searchField.text.Length > 0) searchField.text = "";
                    SelectTab(tab);
                });
            }

            // Suche + Werkzeuge
            var searchRow = Row(34f, root);
            searchField = DefaultControls.CreateInputField(res).GetComponent<InputField>();
            searchField.transform.SetParent(searchRow, false);
            searchField.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            var ph = searchField.placeholder as Text;
            if (ph != null) ph.text = "Regler suchen (z. B. Nase, Kinn) …";
            searchField.onValueChanged.AddListener(OnSearch);

            var tools = Row(34f, root);
            MakeButton(tools, "Zufall", Randomize);
            var ub = MakeButton(tools, "Rückgängig (0)", Undo);
            undoLabel = ub.GetComponentInChildren<Text>();
            MakeButton(tools, "Ansicht", () => { if (orbitCamera != null) orbitCamera.ResetView(); });

            // Scrollbereich
            var scrollGo = new GameObject("Scroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(root, false);
            scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.03f);
            var sle = scrollGo.AddComponent<LayoutElement>();
            sle.flexibleHeight = 1f;
            sle.minHeight = 100f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
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
            vlg.padding = new RectOffset(4, 10, 4, 4);
            vlg.spacing = 4f;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.viewport = vrt;
            scroll.content = crt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            panel = contentGo.transform;

            var bottom = Row(44f, root);
            MakeButton(bottom, "Fertig", Finish);

            var help = Label("Mittlere Maustaste: drehen · Shift + mittlere: hoch/runter · Mausrad: Zoom · Strg+Z: rückgängig", 13, FontStyle.Italic, root);
            help.GetComponent<LayoutElement>().preferredHeight = 34f;
            help.horizontalOverflow = HorizontalWrapMode.Wrap;

            FixFonts(canvasGo);
        }

        private InputField searchField;

        private Text Label(string text, int size, FontStyle style, Transform parent)
        {
            var go = DefaultControls.CreateText(res);
            go.name = "Label";
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = size + 8f;
            return t;
        }

        private void Header(Transform parent, string text)
        {
            var t = Label(text, 18, FontStyle.Bold, parent);
            t.color = new Color(1f, 0.8f, 0.5f);
            t.GetComponent<LayoutElement>().preferredHeight = 30f;
        }

        private Transform Row(float height, Transform parent)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childControlHeight = true;
            h.childControlWidth = true;
            h.childForceExpandHeight = true;
            h.childForceExpandWidth = true;
            return go.transform;
        }

        private void MacroRow(Transform parent, string title, string id, float min, float max, Func<float, string> format, ref Action refresh)
        {
            SliderRow(parent, title, min, max, () => Get(id), v => Set(id, v), ref refresh, format);
        }

        private void ModifierRow(Transform parent, MPFBManifest.Slider sl, ref Action refresh, bool withCategory = false)
        {
            string title = withCategory ? sl.category + ": " + sl.label : sl.label;
            float min = sl.Bipolar ? -1f : 0f;
            string id = sl.id;
            SliderRow(parent, title, min, 1f, () => driver.GetValue(id), v => Set(id, v), ref refresh,
                v => (v > 0.005f && sl.Bipolar ? "+" : "") + v.ToString("0.00"));
        }

        private void SliderRow(Transform parent, string title, float min, float max, Func<float> get, Action<float> set,
            ref Action refresh, Func<float, string> format = null)
        {
            var row = Row(28f, parent);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var label = Label(title, 15, FontStyle.Normal, row);
            var lle = label.GetComponent<LayoutElement>();
            lle.preferredWidth = 170f;
            lle.minWidth = 170f;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 15;

            var sliderGo = DefaultControls.CreateSlider(res);
            sliderGo.transform.SetParent(row, false);
            var sle = sliderGo.AddComponent<LayoutElement>();
            sle.flexibleWidth = 1f;
            sle.minWidth = 100f;
            var slider = sliderGo.GetComponent<Slider>();
            TintSlider(sliderGo);
            slider.minValue = min;
            slider.maxValue = max;

            var value = Label("", 13, FontStyle.Normal, row);
            var vle = value.GetComponent<LayoutElement>();
            vle.preferredWidth = 64f;
            vle.minWidth = 64f;
            value.alignment = TextAnchor.MiddleRight;
            value.horizontalOverflow = HorizontalWrapMode.Overflow;

            Action<float> ShowValue = v => value.text = format != null ? format(v) : v.ToString("0.00");
            slider.SetValueWithoutNotify(get());
            ShowValue(slider.value);
            slider.onValueChanged.AddListener(v => { set(v); ShowValue(v); });
            refresh += () => { slider.SetValueWithoutNotify(get()); ShowValue(slider.value); };
        }

        private void ColorRow(Transform parent, string title, Color[] colors, Action<Color> set)
        {
            Label(title, 16, FontStyle.Normal, parent);
            var row = Row(28f, parent);
            foreach (var c in colors)
            {
                Color color = c;
                var b = MakeButton(row, "", () => { PushUndo(); set(color); if (preview != null) preview.Apply(data.Clone(), body); });
                b.GetComponent<Image>().color = color;
            }
        }

        private void StyleRow(Transform parent, string title, CharacterSlot slot, string[] names, Func<int> get, Action<int> set, ref Action refresh)
        {
            var row = Row(32f, parent);
            var label = Label(title, 16, FontStyle.Normal, row);
            label.GetComponent<LayoutElement>().preferredWidth = 110f;
            MakeButton(row, "<", () => { PushUndo(); set(Wrap(get() - 1, StyleCount(slot))); ApplyPrimitive(); RefreshAll(); });
            var value = Label("", 16, FontStyle.Bold, row);
            value.alignment = TextAnchor.MiddleCenter;
            MakeButton(row, ">", () => { PushUndo(); set(Wrap(get() + 1, StyleCount(slot))); ApplyPrimitive(); RefreshAll(); });
            Action r = () =>
            {
                int i = get();
                value.text = i >= 0 && i < names.Length ? names[i] : "Stil " + (i + 1);
            };
            r();
            refresh += r;
        }

        private Button MakeButton(Transform parent, string text, Action onClick)
        {
            var go = DefaultControls.CreateButton(res);
            go.transform.SetParent(parent, false);
            go.AddComponent<LayoutElement>();
            var t = go.GetComponentInChildren<Text>();
            if (t != null) { t.text = text; t.font = font; t.fontSize = 15; }
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
