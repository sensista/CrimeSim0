using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CrimeSim
{
    /// <summary>
    /// Minimaler JSON-Parser (Objekte => Dictionary&lt;string, object&gt;, Arrays => List&lt;object&gt;,
    /// Zahlen => double, außerdem string, bool, null). Für Manifeste, die JsonUtility nicht lesen kann.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) return null;
            int i = 0;
            object v = ParseValue(json, ref i);
            return v;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("JSON: unerwartetes Ende");
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't': i += 4; return true;
                case 'f': i += 5; return false;
                case 'n': i += 4; return null;
                default: return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var d = new Dictionary<string, object>();
            i++; // {
            SkipWs(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                i++; // :
                d[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                char c = s[i++];
                if (c == '}') return d;
                if (c != ',') throw new FormatException("JSON: ',' oder '}' erwartet bei " + i);
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var l = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                char c = s[i++];
                if (c == ']') return l;
                if (c != ',') throw new FormatException("JSON: ',' oder ']' erwartet bei " + i);
            }
        }

        private static string ParseString(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("JSON: '\"' erwartet bei " + i);
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                        i += 4;
                        break;
                    default: sb.Append(e); break; // " \ /
                }
            }
            throw new FormatException("JSON: offener String");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        // Hilfen
        public static float F(object o, float fallback = 0f) => o is double d ? (float)d : o is bool b ? (b ? 1f : 0f) : fallback;
        public static string S(object o) => o as string;
        public static Dictionary<string, object> O(object o) => o as Dictionary<string, object>;
        public static List<object> A(object o) => o as List<object>;
        public static object Get(Dictionary<string, object> d, string key) => d != null && d.TryGetValue(key, out var v) ? v : null;
    }
}
