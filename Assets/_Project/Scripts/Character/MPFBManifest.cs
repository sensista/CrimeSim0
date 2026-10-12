using System.Collections.Generic;
using UnityEngine;

namespace CrimeSim
{
    /// <summary>
    /// Gelesenes PlayerBase.shapes.json (aus Tools/MPFB/build_player.py).
    /// Koordinaten im JSON sind Blender-Weltkoordinaten (Z oben, Figur schaut -Y).
    /// Umrechnung nach Unity siehe <see cref="BlenderToUnity"/>.
    /// </summary>
    public class MPFBManifest
    {
        public class Shape
        {
            public int index;
            public string name, type, category, group, slider, direction, pair, displayName;
            // Makro-Metadaten (NaN = nicht gesetzt)
            public float gridGender = float.NaN, gridMuscle = float.NaN, gridWeight = float.NaN;
            public float gender = float.NaN, age = float.NaN, cupsize = float.NaN, firmness = float.NaN, value = float.NaN;
            public string race;
            /// <summary>Knochenkopf-Verschiebung (Blender-Koordinaten, m) bei Gewicht 1.</summary>
            public readonly List<KeyValuePair<string, Vector3>> jointOffsets = new List<KeyValuePair<string, Vector3>>();
        }

        /// <summary>Ein Creator-Regler für ein Modifier-Target-Paar (oder ein einzelnes Target).</summary>
        public class Slider
        {
            public string id;          // eindeutiger Schlüssel in character.json
            public string group;       // MPFB-Gruppe (nose, mouth, ...)
            public string category;    // deutscher Gruppenname aus dem Manifest
            public string label;       // deutscher Anzeigename
            public Shape negative;     // kann null sein (dann Bereich 0..1)
            public Shape positive;
            public bool Bipolar => negative != null && positive != null;
        }

        public readonly List<Shape> shapes = new List<Shape>();
        public readonly Dictionary<string, Shape> byName = new Dictionary<string, Shape>();
        public readonly List<Slider> sliders = new List<Slider>();
        public readonly Dictionary<string, Slider> sliderById = new Dictionary<string, Slider>();
        public readonly Dictionary<string, Vector3> boneHeadsNeutral = new Dictionary<string, Vector3>();
        public float neutralHeight = 1.66f, minHeight = 1.3f, maxHeight = 2.36f;

        /// <summary>
        /// Blender (x, y, z) => Unity (-x, z, -y).
        /// Begründung: Der Blender-FBX-Export mit axis_forward=-Z, axis_up=Y dreht -90° um X:
        /// (x, y, z) => FBX (x, z, -y). Unity liest FBX (rechtshändig) linkshändig ein und spiegelt X:
        /// FBX (x, y, z) => Unity (-x, y, z). Ergebnis: Unity = (-x, z, -y). Kontrolle: Die Figur schaut
        /// in Blender nach -Y, in Unity also nach +Z; ihre linke Seite (Blender +X) liegt in Unity bei -X.
        /// MPFBShapeDriver prüft das zur Laufzeit gegen die importierten Knochen (boneHeadsNeutral).
        /// </summary>
        public static Vector3 BlenderToUnity(Vector3 b) => new Vector3(-b.x, b.z, -b.y);

        private static readonly Dictionary<int, MPFBManifest> cache = new Dictionary<int, MPFBManifest>();

        public static MPFBManifest Load(TextAsset json)
        {
            if (json == null) return null;
            int key = json.GetInstanceID();
            if (cache.TryGetValue(key, out var m)) return m;
            m = Parse(json.text);
            cache[key] = m;
            return m;
        }

        public static MPFBManifest Parse(string text)
        {
            var root = MiniJson.O(MiniJson.Parse(text));
            var m = new MPFBManifest();
            m.neutralHeight = MiniJson.F(MiniJson.Get(root, "neutralHeight_m"), 1.66f);
            var ht = MiniJson.O(MiniJson.Get(root, "heightTotal_m"));
            m.minHeight = MiniJson.F(MiniJson.Get(ht, "height_0"), m.neutralHeight * 0.8f);
            m.maxHeight = MiniJson.F(MiniJson.Get(ht, "height_1"), m.neutralHeight * 1.2f);

            var heads = MiniJson.O(MiniJson.Get(root, "boneHeadsNeutral"));
            if (heads != null)
                foreach (var kv in heads) m.boneHeadsNeutral[kv.Key] = Vec(kv.Value);

            var list = MiniJson.A(MiniJson.Get(root, "shapes"));
            if (list != null)
            {
                foreach (var o in list)
                {
                    var d = MiniJson.O(o);
                    var s = new Shape
                    {
                        index = m.shapes.Count,
                        name = MiniJson.S(MiniJson.Get(d, "name")),
                        type = MiniJson.S(MiniJson.Get(d, "type")),
                        category = MiniJson.S(MiniJson.Get(d, "category")),
                        group = MiniJson.S(MiniJson.Get(d, "group")),
                        slider = MiniJson.S(MiniJson.Get(d, "slider")),
                        direction = MiniJson.S(MiniJson.Get(d, "direction")),
                        pair = MiniJson.S(MiniJson.Get(d, "pair")),
                        displayName = MiniJson.S(MiniJson.Get(d, "displayName")),
                        race = MiniJson.S(MiniJson.Get(d, "race")),
                        gender = MiniJson.F(MiniJson.Get(d, "gender"), float.NaN),
                        age = MiniJson.F(MiniJson.Get(d, "age"), float.NaN),
                        cupsize = MiniJson.F(MiniJson.Get(d, "cupsize"), float.NaN),
                        firmness = MiniJson.F(MiniJson.Get(d, "firmness"), float.NaN),
                        value = MiniJson.F(MiniJson.Get(d, "value"), float.NaN),
                    };
                    var grid = MiniJson.O(MiniJson.Get(d, "grid"));
                    if (grid != null)
                    {
                        s.gridGender = MiniJson.F(MiniJson.Get(grid, "gender"));
                        s.gridMuscle = MiniJson.F(MiniJson.Get(grid, "muscle"));
                        s.gridWeight = MiniJson.F(MiniJson.Get(grid, "weight"));
                    }
                    var jo = MiniJson.O(MiniJson.Get(d, "jointOffsets"));
                    if (jo != null)
                        foreach (var kv in jo) s.jointOffsets.Add(new KeyValuePair<string, Vector3>(kv.Key, Vec(kv.Value)));
                    if (string.IsNullOrEmpty(s.name)) continue;
                    m.shapes.Add(s);
                    m.byName[s.name] = s;
                }
            }
            m.BuildSliders();
            return m;
        }

        private static Vector3 Vec(object o)
        {
            var a = MiniJson.A(o);
            if (a == null || a.Count < 3) return Vector3.zero;
            return new Vector3(MiniJson.F(a[0]), MiniJson.F(a[1]), MiniJson.F(a[2]));
        }

        private static string Side(string targetName)
        {
            if (targetName.StartsWith("l-")) return "l";
            if (targetName.StartsWith("r-")) return "r";
            return "";
        }

        private void BuildSliders()
        {
            foreach (var s in shapes)
            {
                if (s.type != "modifier" || string.IsNullOrEmpty(s.slider)) continue;
                string side = Side(s.name);
                string id = side.Length > 0 ? side + "-" + s.slider : s.slider;
                if (!sliderById.TryGetValue(id, out var sl))
                {
                    sl = new Slider { id = id, group = s.group, category = s.category };
                    sliderById[id] = sl;
                    sliders.Add(sl);
                }
                if (s.direction == "negative") sl.negative = s; else sl.positive = s;
            }
            foreach (var sl in sliders)
            {
                // Nur eine "negative" Seite ohne Gegenstück: als einseitigen Regler behandeln
                if (sl.positive == null && sl.negative != null) { sl.positive = sl.negative; sl.negative = null; }
                sl.label = MakeLabel(sl);
            }
        }

        private static string MakeLabel(Slider sl)
        {
            string n = sl.positive != null ? sl.positive.displayName : sl.id;
            if (string.IsNullOrEmpty(n)) n = sl.id;
            if (sl.Bipolar)
            {
                // letztes Richtungswort (+, −, hoch, runter, vor, zurück, innen, außen ...) entfernen
                int cut = n.LastIndexOf(' ');
                string last = cut > 0 ? n.Substring(cut + 1) : "";
                if (last == "+" || last == "−" || last == "hoch" || last == "runter" || last == "vor" || last == "zurück"
                    || last == "innen" || last == "außen" || last == "rechts" || last == "links" && sl.group == "asym")
                    n = n.Substring(0, cut);
            }
            // erstes Wort ist meist der Gruppenname ("Nase ...") – im Tab ohnehin sichtbar
            if (!string.IsNullOrEmpty(sl.category) && n.StartsWith(sl.category + " ") && n.Length > sl.category.Length + 1)
                n = n.Substring(sl.category.Length + 1);
            return char.ToUpperInvariant(n[0]) + n.Substring(1);
        }
    }
}
