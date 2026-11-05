using System.Globalization;
using System.IO;
using UnityEngine;

namespace SurgiTwinXR.Utilities
{
    /// <summary>
    /// Minimal streaming JSON helpers. Values are written straight to a TextWriter so
    /// large trajectory logs never have to be built as one giant string.
    /// Numbers always use the invariant culture (no locale decimal commas).
    /// </summary>
    public static class JsonText
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static void String(TextWriter w, string value)
        {
            if (value == null)
            {
                w.Write("null");
                return;
            }

            w.Write('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': w.Write("\\\""); break;
                    case '\\': w.Write("\\\\"); break;
                    case '\n': w.Write("\\n"); break;
                    case '\r': w.Write("\\r"); break;
                    case '\t': w.Write("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            w.Write("\\u");
                            w.Write(((int)c).ToString("x4", Inv));
                        }
                        else
                        {
                            w.Write(c);
                        }
                        break;
                }
            }
            w.Write('"');
        }

        public static void Number(TextWriter w, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                w.Write('0');
                return;
            }
            w.Write(value.ToString("0.######", Inv));
        }

        public static void Number(TextWriter w, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                w.Write('0');
                return;
            }
            w.Write(value.ToString("0.######", Inv));
        }

        public static void Number(TextWriter w, int value) => w.Write(value.ToString(Inv));

        public static void Bool(TextWriter w, bool value) => w.Write(value ? "true" : "false");

        public static void Vector(TextWriter w, Vector3 v)
        {
            w.Write('[');
            Number(w, v.x); w.Write(',');
            Number(w, v.y); w.Write(',');
            Number(w, v.z);
            w.Write(']');
        }

        public static void Quat(TextWriter w, Quaternion q)
        {
            w.Write('[');
            Number(w, q.x); w.Write(',');
            Number(w, q.y); w.Write(',');
            Number(w, q.z); w.Write(',');
            Number(w, q.w);
            w.Write(']');
        }

        public static void Key(TextWriter w, string key)
        {
            String(w, key);
            w.Write(':');
        }
    }
}
