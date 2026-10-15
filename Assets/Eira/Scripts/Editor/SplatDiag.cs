using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EiraGame.Editor
{
    public static class SplatDiag
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const string LogPath = @"C:\Users\Usuario\AppData\Local\Temp\opencode\splat_diag.txt";

        static readonly StringBuilder sb = new StringBuilder();
        static int frames;
        static int phase;

        static void L(string s) { sb.AppendLine(s); Debug.Log("[SplatDiag] " + s); }

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath);
            frames = 0; phase = 0;
            L("entering play mode...");
            EditorApplication.update += Tick;
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            frames++;
            if (phase == 0)
            {
                if (!Application.isPlaying || frames < 10) return;
                var s = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (s.name != "Level1Scene")
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Level1Scene");
                frames = 0; phase = 1;
                return;
            }
            if (frames < 30) return;
            EditorApplication.update -= Tick;
            try { Report(); } catch (System.Exception ex) { L("EX: " + ex); }
            Write();
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += () => EditorApplication.Exit(0);
        }

        static void Report()
        {
            var camGo = GameObject.Find("MainCamera");
            var cam = camGo != null ? camGo.GetComponent<Camera>() : Camera.main;
            L("camera " + cam.name + " pos " + cam.transform.position + " eul " + cam.transform.eulerAngles +
              " near " + cam.nearClipPlane + " far " + cam.farClipPlane);

            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var roots = new List<GameObject>();
            foreach (var g in scene.GetRootGameObjects()) roots.Add(g);

            var env = GameObject.Find("GaussianEnvironment");
            var compType = System.Type.GetType("GaussianSplatting.Runtime.GaussianSplatRenderer, GaussianSplatting");
            var comp = env.GetComponent(compType);
            var modeF = compType.GetField("m_RenderMode");
            var assetF = compType.GetField("m_Asset");
            var asset = assetF.GetValue(comp);
            var bMinF = asset.GetType().GetProperty("boundsMin");
            var bMaxF = asset.GetType().GetProperty("boundsMax");
            var scF = compType.GetProperty("splatCount");
            L("splatCount=" + scF.GetValue(comp));
            if (bMinF != null) L("asset bounds " + bMinF.GetValue(asset) + " .. " + bMaxF.GetValue(asset));

            foreach (var f in new[] { "m_GpuPosData", "m_GpuOtherData", "m_GpuSHData", "m_GpuColorData", "m_GpuChunks", "m_GpuView", "m_GpuSortKeys" })
            {
                var fi = compType.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                L("  " + f + " = " + (fi?.GetValue(comp) == null ? "NULL" : "ok"));
            }
            foreach (var f in new[] { "m_MatSplats", "m_MatComposite" })
            {
                var fi = compType.GetField(f, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                L("  " + f + " = " + (fi?.GetValue(comp) == null ? "NULL" : "ok"));
            }

            var disabled = new List<GameObject>();
            foreach (var g in roots)
                if (g.name != "GaussianEnvironment" && g.name != "MainCamera" && g.name != "Sun")
                { g.SetActive(false); disabled.Add(g); }

            SetMode(modeF, comp, "Splats");
            L("---- A: cámara actual, modo Splats ----");
            L(RenderAndAnalyze(cam, "A_splats"));

            var savedPos = env.transform.position;
            var savedRot = env.transform.rotation;
            var savedScale = env.transform.localScale;

            var cam2Go = new GameObject("DiagCam");
            var cam2 = cam2Go.AddComponent<Camera>();
            cam2.clearFlags = CameraClearFlags.SolidColor;
            cam2.backgroundColor = new Color(0.1f, 0.1f, 0.1f);
            cam2.nearClipPlane = 0.3f; cam2.farClipPlane = 5000f;
            cam2.cullingMask = ~0;

            var gamePose = new Vector3(2.68f, 2.25f, -4.02f);
            var topPose = new Vector3(61f, 150f, 0f);
            var topRot = Quaternion.Euler(90f, 0f, 0f);

            L("env transform: pos " + env.transform.position + " eul " + env.transform.eulerAngles +
              " lossyScale " + env.transform.lossyScale + " parent " +
              (env.transform.parent != null ? env.transform.parent.name : "(none)"));

            var curPos = new Vector3(101.12f, 3.065f, -2.03f);
            var curRot = Quaternion.Euler(0, 90, 0);
            var curScl = new Vector3(8.588f, 2.155f, 8.377f);

            var tests = new (string, Vector3, Quaternion, Vector3, Vector3, Quaternion)[] {
                ("ID_FULLVIEW_topdown",  Vector3.zero, Quaternion.identity, Vector3.one,
                    new Vector3(-0.24f, 40f, -4.79f), Quaternion.Euler(90f,0f,0f)),
                ("ID_side_+Z",           Vector3.zero, Quaternion.identity, Vector3.one,
                    new Vector3(-0.24f, 2f, -30f), Quaternion.Euler(0f,0f,0f)),
                ("ID_full_side_+X",      Vector3.zero, Quaternion.identity, Vector3.one,
                    new Vector3(-30f, 2f, -4.79f), Quaternion.Euler(0f,90f,0f)),
                ("XF_fullview_topdown",  curPos, curRot, curScl,
                    new Vector3(61f, 150f, 0f), Quaternion.Euler(90f,0f,0f)),
                ("XF_fullside_+Z",       curPos, curRot, curScl,
                    new Vector3(61f, 7.5f, -40f), Quaternion.Euler(0f,0f,0f)),
            };

            SetMode(modeF, comp, "Splats");
            foreach (var t in tests)
            {
                env.transform.position = t.Item2;
                env.transform.rotation = t.Item3;
                env.transform.localScale = t.Item4;
                SetPose(cam2, t.Item5, t.Item6);
                L("---- " + t.Item1 + " ----");
                L(RenderAndAnalyze(cam2, "t_" + t.Item1));
            }
            env.transform.position = savedPos;
            env.transform.rotation = savedRot;
            env.transform.localScale = savedScale;

            SetMode(modeF, comp, "DebugPoints");
            SetPose(cam2, gamePose, Quaternion.Euler(8f, 0f, 0f));
            L("---- DebugPoints @ game pose ----");
            L(RenderAndAnalyze(cam2, "sw_pts_game"));
            SetPose(cam2, topPose, topRot);
            L("---- DebugPoints @ top-down ----");
            L(RenderAndAnalyze(cam2, "sw_pts_top"));
            SetMode(modeF, comp, "Splats");

            env.transform.position = savedPos;
            env.transform.rotation = savedRot;
            env.transform.localScale = savedScale;

            Object.DestroyImmediate(cam2Go);
            SetMode(modeF, comp, "Splats");
            foreach (var g in disabled) g.SetActive(true);
        }

        static void SetPose(Camera c, Vector3 pos, Quaternion rot)
        {
            c.transform.SetPositionAndRotation(pos, rot);
        }

        static void SetMode(System.Reflection.FieldInfo modeF, object comp, string name)
        {
            var t = modeF.FieldType;
            modeF.SetValue(comp, System.Enum.Parse(t, name));
        }

        static string RenderAndAnalyze(Camera cam, string tag)
        {
            int w = 320, h = 180;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;

            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            var pa = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = pa;

            File.WriteAllBytes(@"C:\Users\Usuario\AppData\Local\Temp\opencode\diag_" + tag + ".png", tex.EncodeToPNG());

            var px = tex.GetPixels();
            const float bg = 0.1f;
            int content = 0, dark = 0, bright = 0;
            var lum = new float[px.Length];
            float lmin = 9f, lmax = -9f, lsum = 0f, rmax = 0f, gmax = 0f, bmax = 0f;
            for (int i = 0; i < px.Length; i++)
            {
                float r = px[i].r, g = px[i].g, b = px[i].b;
                float l = 0.2126f * r + 0.7152f * g + 0.0722f * b;
                lum[i] = l;
                float dev = Mathf.Abs(l - bg);
                float sat = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
                if (dev > 0.004f || sat > 0.004f) content++;
                if (l < bg - 0.004f) dark++;
                if (l > bg + 0.004f) bright++;
                if (l < lmin) lmin = l;
                if (l > lmax) lmax = l;
                lsum += l;
                if (r > rmax) rmax = r; if (g > gmax) gmax = g; if (b > bmax) bmax = b;
            }
            int tot = px.Length;
            var outS = new StringBuilder();
            outS.Append($"content {100.0 * content / tot:F2}% dark {100.0 * dark / tot:F2}% bright {100.0 * bright / tot:F2}% " +
                        $"lum min {lmin:F3} max {lmax:F3} mean {lsum / tot:F3} | rmax {rmax:F3} gmax {gmax:F3} bmax {bmax:F3}\n");

            const int cols = 96, rows = 44;
            const string ramp = " .:-=+*#%@";
            for (int ry = 0; ry < rows; ry++)
            {
                for (int rx = 0; rx < cols; rx++)
                {
                    int sx0 = rx * w / cols, sx1 = Mathf.Max(sx0 + 1, (rx + 1) * w / cols);
                    int sy0 = ry * h / rows, sy1 = Mathf.Max(sy0 + 1, (ry + 1) * h / rows);
                    float l = 0, r = 0, g = 0, b = 0; int c = 0;
                    for (int y = sy0; y < sy1; y++)
                        for (int x = sx0; x < sx1; x++)
                        { int idx = y * w + x; l += lum[idx]; r += px[idx].r; g += px[idx].g; b += px[idx].b; c++; }
                    l /= c; r /= c; g /= c; b /= c;
                    float dev = Mathf.Abs(l - bg);
                    float sat = Mathf.Max(r, Mathf.Max(g, b)) - Mathf.Min(r, Mathf.Min(g, b));
                    char ch;
                    if (dev <= 0.004f && sat <= 0.004f) ch = ' ';
                    else if (l > bg) ch = ramp[Mathf.Clamp((int)(dev * 300f + 0.5f) + 1, 1, ramp.Length - 1)];
                    else ch = '#';
                    outS.Append(ch);
                }
                outS.Append('\n');
            }
            Object.DestroyImmediate(tex);
            if (prev == null) { rt.Release(); Object.DestroyImmediate(rt); }
            return outS.ToString();
        }

        static void Probe(Camera cam, float vx, float vy, string label)
        {
            var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0));
            if (Physics.Raycast(ray, out var hit, 2000f))
            {
                var rend = hit.collider.GetComponentInParent<Renderer>();
                L("probe " + label + " -> hit '" + hit.collider.gameObject.name + "' (root '" +
                  hit.collider.transform.root.name + "') shader " +
                  (rend != null && rend.sharedMaterial != null ? rend.sharedMaterial.shader.name : "-") +
                  " dist " + hit.distance.ToString("F1"));
            }
            else L("probe " + label + " -> sin hit");
        }

        static void Write() => File.WriteAllText(LogPath, sb.ToString());
    }
}
