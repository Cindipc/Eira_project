using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EiraGame.Editor
{
    // Lee el FBX binario (sin recursar en las mallas, que son enormes) y vuelca
    // la relacion material <- textura para poder asignarlas a mano cuando el
    // importador de Unity no las enlaza.
    public static class FbxMaterialMap
    {
        const string Fbx =
            @"C:\Users\Usuario\Downloads\paisaje\source\пейзаж ск.fbx";

        const string OutFile =
            @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_fbxmap.txt";

        static byte[] B;
        static readonly Dictionary<long, string> Names =
            new Dictionary<long, string>();
        static readonly Dictionary<long, string> Classes =
            new Dictionary<long, string>();
        static readonly Dictionary<long, string> RelFile =
            new Dictionary<long, string>();
        static readonly List<string> Links = new List<string>();

        [MenuItem("Eira/Nivel 1/Volcar mapa de materiales FBX", false, 40)]
        public static void Dump()
        {
            B = File.ReadAllBytes(Fbx);

            var tops = new List<string>();
            try
            {
                int p = 27;
                while (p < B.Length)
                {
                    if (!Header(ref p, out var end, out var np,
                                out var pl, out var name)) break;

                    tops.Add(name + " n=" + np + " l=" + pl +
                             " end=" + end + " p=" + p);

                    if (name == "Objects") Objects(ref p, end);
                    else if (name == "Connections") Connections(ref p, end);

                    if ((int)end <= p) break;
                    p = (int)end;
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(OutFile, "ERROR: " + ex);
                Debug.LogError("[Eira] FbxMaterialMap: " + ex);
                return;
            }

            var r = new StringBuilder();
            r.AppendLine("objetos: " + Names.Count +
                         " · rutas: " + RelFile.Count +
                         " · enlaces: " + Links.Count);
            r.AppendLine();
            var hist = new Dictionary<string, int>();
            foreach (var c in Classes.Values)
                hist[c] = hist.TryGetValue(c, out var n) ? n + 1 : 1;

            r.AppendLine("--- muestras ---");
            foreach (var s in Samples) r.AppendLine(s);
            r.AppendLine();
            r.AppendLine("--- clases ---");
            foreach (var kv in hist) r.AppendLine(kv.Key + " = " + kv.Value);
            r.AppendLine();
            r.AppendLine("--- top ---");
            foreach (var t in tops) r.AppendLine(t);
            r.AppendLine();
            r.AppendLine("--- rutas ---");
            foreach (var kv in RelFile)
                r.AppendLine(IdName(kv.Key) + " : " + kv.Value);
            r.AppendLine();
            r.AppendLine("--- enlaces ---");
            foreach (var l in Links) r.AppendLine(l);

            File.WriteAllText(OutFile, r.ToString());
            Debug.Log("[Eira] FbxMaterialMap:\n" + r);
        }

        static void Objects(ref int p, uint end)
        {
            while (p < end)
            {
                int start = p;
                if (!Header(ref p, out var cend, out var np,
                            out var pl, out var name)) break;

                long id = 0;
                string oname = "", klass = "";
                var raw = new StringBuilder();
                for (int i = 0; i < np; i++)
                {
                    var v = Prop(ref p);
                    raw.Append(" [").Append(v == null ? "?" :
                        v is string ? Clean(v as string) : v.ToString())
                        .Append(']');
                    if (i == 0 && v is long l) id = l;
                    else if (i == 1) oname = v as string ?? "";
                    else if (i == 2) klass = v as string ?? "";
                }

                if (Samples.Count < 40)
                    Samples.Add(name + ":" + raw);

                klass = name;

                if (klass.Length > 0)
                {
                    Names[id] = Trim(oname);
                    Classes[id] = klass;

                    if (klass == "Texture" || klass == "Video")
                        TextureProps(id, ref p, cend);
                }

                if (cend <= start) break;
                p = (int)cend;
            }
        }

        static void TextureProps(long id, ref int p, uint end)
        {
            while (p < end)
            {
                int start = p;
                if (!Header(ref p, out var cend, out var np,
                            out var pl, out var name)) break;

                if (name == "Properties70" || name == "Properties60")
                {
                    while (p < cend)
                    {
                        int ps = p;
                        if (!Header(ref p, out var pend, out var pnp,
                                    out var ppl, out var pname)) break;

                        if (pname == "P")
                        {
                            string key = null, val = null;
                            for (int i = 0; i < pnp; i++)
                            {
                                var v = Prop(ref p);
                                if (i == 0) key = v as string;
                                else if (v is string s) val = s;
                            }
                            if (key == "RelativeFilename" && val != null)
                                RelFile[id] = val;
                        }

                        if (pend <= ps) break;
                        p = (int)pend;
                    }
                }
                else if (name == "RelativeFilename" || name == "Filename")
                {
                    string val = null;
                    for (int i = 0; i < np; i++)
                    {
                        var v = Prop(ref p);
                        if (v is string s && s.Length > 0) val = s;
                    }
                    if (val != null) RelFile[id] = val;
                }

                if (cend <= start) break;
                p = (int)cend;
            }
        }

        static void Connections(ref int p, uint end)
        {
            while (p < end)
            {
                int start = p;
                if (!Header(ref p, out var cend, out var np,
                            out var pl, out var name)) break;

                if (name == "C")
                {
                    string kind = "";
                    long child = 0, parent = 0;
                    string propName = "";
                    var all = new List<object>();
                    for (int i = 0; i < np; i++) all.Add(Prop(ref p));

                    if (all.Count >= 3)
                    {
                        kind = all[0] as string ?? "";
                        if (all[1] is long c) child = c;
                        if (all[2] is long q) parent = q;
                        if (all.Count >= 4) propName = all[3] as string ?? "";
                    }

                    string ccl = Classes.TryGetValue(child, out var cc) ? cc : "?";
                    string pcl = Classes.TryGetValue(parent, out var pc) ? pc : "?";
                    bool involved = ccl == "Texture" || ccl == "Video" ||
                                    pcl == "Texture" || pcl == "Video";

                    if (involved)
                    {
                        Links.Add(
                            "[" + kind + (propName.Length > 0 ? "/" + propName : "") +
                            "]  " + IdName(child) + "(" + ccl + ")" +
                            "  ->  " + IdName(parent) + "(" + pcl + ")");
                    }
                }

                if (cend <= start) break;
                p = (int)cend;
            }
        }

        static readonly List<string> Samples = new List<string>();

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
                sb.Append(ch < 32 ? '.' : ch);
            return sb.ToString();
        }

        static string Trim(string s)
        {
            int i = s.IndexOf("..", StringComparison.Ordinal);
            return i > 0 ? s.Substring(0, i) : s;
        }

        static string IdName(long id)
        {
            return Names.TryGetValue(id, out var n) ? n : ("#" + id);
        }

        static bool Header(ref int p, out uint end, out uint numProps,
                           out uint propLen, out string name)
        {
            end = ReadU32(ref p);
            name = null;
            numProps = propLen = 0;
            if (end == 0) return false;

            numProps = ReadU32(ref p);
            propLen = ReadU32(ref p);
            byte nameLen = B[p++];
            name = Encoding.ASCII.GetString(B, p, nameLen);
            p += nameLen;
            return true;
        }

        static object Prop(ref int p)
        {
            char t = (char)B[p++];
            switch (t)
            {
                case 'Y': { var v = BitConverter.ToInt16(B, p); p += 2; return (int)v; }
                case 'C': { var v = B[p++]; return v != 0; }
                case 'I': { var v = BitConverter.ToInt32(B, p); p += 4; return v; }
                case 'F': { var v = BitConverter.ToSingle(B, p); p += 4; return v; }
                case 'D': { var v = BitConverter.ToDouble(B, p); p += 8; return v; }
                case 'L': { var v = BitConverter.ToInt64(B, p); p += 8; return v; }
                case 'S':
                    {
                        uint len = ReadU32(ref p);
                        var s = Encoding.UTF8.GetString(B, p, (int)len);
                        p += (int)len;
                        return s;
                    }
                case 'R':
                    {
                        uint len = ReadU32(ref p);
                        p += (int)len;
                        return null;
                    }
                default:
                    throw new Exception("prop 0x" +
                        ((int)(byte)t).ToString("X2") + " @" + (p - 1));
            }
        }

        static uint ReadU32(ref int p)
        {
            uint v = BitConverter.ToUInt32(B, p);
            p += 4;
            return v;
        }
    }
}
