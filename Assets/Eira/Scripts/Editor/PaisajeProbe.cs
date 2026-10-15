using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    // Sonda para localizar los arboles (vegetacion) dentro del paisaje y ver
    // que suelo hay.  Menu: Eira/Nivel 1/Sonda paisaje.
    public static class PaisajeProbe
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const string OutPath = @"C:\Users\Usuario\AppData\Local\Temp\opencode\paisaje_probe.txt";

        static readonly StringBuilder sb = new StringBuilder();
        static void L(string s) { sb.AppendLine(s); }

        [MenuItem("Eira/Nivel 1/Sonda paisaje", false, 41)]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            GameObject land = null;
            var cols = Object.FindObjectsByType<Collider>(FindObjectsInactive.Include);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i].name == "PaisajeNivel1_Environment" ||
                    (cols[i].transform.parent != null &&
                     cols[i].transform.root.name == "PaisajeNivel1_Environment"))
                    land = cols[i].transform.root.gameObject;
            if (land == null)
                foreach (var g in scene.GetRootGameObjects())
                    if (g.name.StartsWith("Paisaje")) land = g;

            L("LANDSCAPE: " + (land == null ? "NOT FOUND" : land.name));
            if (land == null) { File.WriteAllText(OutPath, sb.ToString()); return; }

            // --- Jerarquia de renderers / materiales / submesh ---
            var rends = land.GetComponentsInChildren<MeshRenderer>(true);
            L("RENDERERS: " + rends.Length);
            foreach (var r in rends)
            {
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                var mats = r.sharedMaterials;
                L("  [" + Path(r.transform) + "] bounds c" + F(r.bounds.center) +
                  " s" + F(r.bounds.size) + " submeshes=" + (mesh != null ? mesh.subMeshCount : 0) +
                  " mats=[" + MatNames(mats) + "]");

                if (mesh != null && mesh.subMeshCount > 1 && mesh.vertexCount < 400000)
                    for (int s = 0; s < mesh.subMeshCount; s++)
                        L("      sub" + s + " bounds" + B(SubBounds(mesh, r, s)) +
                          " mat=" + (s < mats.Length && mats[s] != null ? mats[s].name : "?"));
            }

            // --- Colliders del paisaje ---
            var landCols = land.GetComponentsInChildren<Collider>(true);
            L("COLLIDERS: " + landCols.Length);
            foreach (var c in landCols)
                L("  " + c.GetType().Name + " bounds" + B(c.bounds) +
                  " trigger=" + c.isTrigger);

            // --- Mapa de altura del terreno (rejilla) ---
            var b0 = new Bounds();
            bool init = false;
            foreach (var r in rends) { if (!init) { b0 = r.bounds; init = true; } else b0.Encapsulate(r.bounds); }
            L("TOTAL bounds c" + F(b0.center) + " s" + F(b0.size));

            const int N = 18;
            L("HEIGHTMAP (" + N + "x" + N + ") filas=z de max->min, cols=x de min->max; '..'=sin malla");
            float x0 = b0.min.x, x1 = b0.max.x, z0 = b0.min.z, z1 = b0.max.z;
            for (int iz = N - 1; iz >= 0; iz--)
            {
                var row = new StringBuilder("  ");
                for (int ix = 0; ix < N; ix++)
                {
                    float x = Mathf.Lerp(x0, x1, (ix + 0.5f) / N);
                    float z = Mathf.Lerp(z0, z1, (iz + 0.5f) / N);
                    float y = Surface(land, x, z);
                    row.Append(float.IsNaN(y) ? "  .. " : " " + y.ToString("00") + " ");
                }
                L(row.ToString());
            }

            // --- Alturas en el eje del pasillo (z=0) ---
            L("PERFIL z=0 (x de -3 a 150):");
            var pr = new StringBuilder();
            for (float x = -3f; x <= 150f; x += 5f)
            {
                float y = Surface(land, x, 0f);
                pr.Append("x" + x.ToString("0") + "=" + (float.IsNaN(y) ? "--" : y.ToString("0.0")) + "  ");
            }
            L(pr.ToString());

            File.WriteAllText(OutPath, sb.ToString());
            Debug.Log("[PaisajeProbe]\n" + sb);
        }

        static float Surface(GameObject land, float x, float z)
        {
            var ray = new Ray(new Vector3(x, 4000f, z), Vector3.down);
            var hits = Physics.RaycastAll(ray, 8000f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.NaN;
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.root != land.transform.root) continue;
                if (float.IsNaN(best) || h.point.y > best) best = h.point.y;
            }
            return best;
        }

        static Bounds SubBounds(Mesh mesh, MeshRenderer r, int sub)
        {
            var verts = mesh.vertices;
            var tri = mesh.GetTriangles(sub);
            if (tri.Length == 0) return new Bounds();
            var b = new Bounds(r.transform.TransformPoint(verts[tri[0]]), Vector3.zero);
            for (int i = 0; i < tri.Length; i++)
                b.Encapsulate(r.transform.TransformPoint(verts[tri[i]]));
            return b;
        }

        static string Path(Transform t)
        {
            var s = t.name;
            while (t.parent != null && t.parent != t.root.parent) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static string MatNames(Material[] mats)
        {
            var s = "";
            for (int i = 0; i < mats.Length; i++) s += (i > 0 ? "," : "") + (mats[i] != null ? mats[i].name : "null");
            return s;
        }

        static string F(Vector3 v) => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0") + ")";
        static string B(Bounds b) => " c" + F(b.center) + " s" + F(b.size);
    }
}
