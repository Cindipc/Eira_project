using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    // Diagnostico visual del Nivel 1 tras el swap del paisaje.
    //   Eira/Nivel 1/Diagnostico visual
    public static class PaisajeDiag
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const string OutDir = @"C:\Users\Usuario\AppData\Local\Temp\opencode\paisaje";
        const string LogPath = @"C:\Users\Usuario\AppData\Local\Temp\opencode\paisaje_diag.txt";

        static readonly StringBuilder sb = new StringBuilder();
        static void L(string s) { sb.AppendLine(s); Debug.Log("[PaisajeDiag] " + s); }

        [MenuItem("Eira/Nivel 1/Diagnostico visual", false, 40)]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Activar una luz para que el render no salga negro (todas las
            // "Sun" estan inactivas en la escena).
            foreach (var g in scene.GetRootGameObjects())
                if (g.name.StartsWith("Sun"))
                    g.SetActive(true);

            // ---- Estructura ----
            var roots = new List<GameObject>();
            foreach (var g in scene.GetRootGameObjects()) roots.Add(g);

            L("ROOTS (" + roots.Count + ")");
            foreach (var g in roots)
                L("  " + g.name + "  pos " + F(g.transform.position) +
                  "  active " + g.activeSelf +
                  (g.GetComponent<Renderer>() != null || g.GetComponentInChildren<Renderer>() != null
                      ? "  bounds " + B(Bounds(g)) : ""));

            foreach (var name in new[] { "PaisajeNivel1_Environment", "FuturisticCity_Environment", "LabAbandonado", "Gameplay" })
            {
                var go = FindByName(scene, name);
                L(go == null ? ("MISSING: " + name) : (name + " bounds " + B(Bounds(go))));
            }

            // ---- Render ----
            var camGo = new GameObject("DiagCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.03f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 6000f;
            cam.cullingMask = ~0;
            cam.fieldOfView = 55f;

            var poses = new (string, Vector3, Vector3)[]
            {
                ("topdown",  new Vector3(60f, 180f, 0f),  new Vector3(90f, 0f, 0f)),
                ("game_start", new Vector3(-2f, 4f, 0f),   new Vector3(6f, 90f, 0f)),
                ("side_negz", new Vector3(60f, 25f, -140f), new Vector3(6f, 0f, 0f)),
                ("iso",       new Vector3(-40f, 70f, -70f), new Vector3(28f, 45f, 0f)),
                ("low_along_x", new Vector3(0f, 8f, 0f),    new Vector3(0f, 90f, 0f)),
                ("nivel_end_bosque", new Vector3(100f, 4f, 2f), new Vector3(0f, -73f, 0f)),
                ("bosque_aereo", new Vector3(60f, 40f, 12f), new Vector3(90f, 0f, 0f)),
            };

            foreach (var p in poses)
            {
                cam.transform.SetPositionAndRotation(p.Item2, Quaternion.Euler(p.Item3));
                string ascii = Render(cam, p.Item1);
                L("---- " + p.Item1 + " ----");
                L(ascii);
            }

            Object.DestroyImmediate(camGo);
            File.WriteAllText(LogPath, sb.ToString());
            Debug.Log("[PaisajeDiag]\n" + sb);
        }

        static GameObject FindByName(UnityEngine.SceneManagement.Scene sc, string n)
        {
            foreach (var g in sc.GetRootGameObjects())
                if (g.name == n) return g;
            return null;
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool init = false; var b = new Bounds();
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static string F(Vector3 v) => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0") + ")";
        static string B(Bounds b) => "c" + F(b.center) + " s" + F(b.size);

        static string Render(Camera cam, string tag)
        {
            int w = 640, h = 360;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pa = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = pa;

            File.WriteAllBytes(Path.Combine(OutDir, tag + ".png"), tex.EncodeToPNG());

            var px = tex.GetPixels();
            var sb2 = new StringBuilder();
            const int cols = 110, rows = 46;
            float bgL = 0.2126f * 0.02f + 0.7152f * 0.02f + 0.0722f * 0.03f;
            const string ramp = " .:-=+*#%@";
            for (int ry = rows - 1; ry >= 0; ry--)
            {
                for (int rx = 0; rx < cols; rx++)
                {
                    int sx0 = rx * w / cols, sx1 = Mathf.Max(sx0 + 1, (rx + 1) * w / cols);
                    int sy0 = ry * h / rows, sy1 = Mathf.Max(sy0 + 1, (ry + 1) * h / rows);
                    float r = 0, g = 0, b = 0; int c = 0;
                    for (int y = sy0; y < sy1; y++)
                        for (int x = sx0; x < sx1; x++)
                        { int idx = y * w + x; r += px[idx].r; g += px[idx].g; b += px[idx].b; c++; }
                    r /= c; g /= c; b /= c;
                    float l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                    float sat = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
                    float dev = Mathf.Abs(l - bgL);
                    char ch;
                    if (dev <= 0.006f && sat <= 0.006f) ch = ' ';
                    else if (l > bgL) ch = ramp[Mathf.Clamp((int)(dev * 320f + 0.5f) + 1, 1, ramp.Length - 1)];
                    else ch = '#';
                    sb2.Append(ch);
                }
                sb2.Append('\n');
            }

            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            return sb2.ToString();
        }
    }
}
