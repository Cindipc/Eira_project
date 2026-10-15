using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    public static class ValidatePack
    {
        const string ScenePath = EiraPaths.Scenes + "/Level1Scene.unity";
        const string ReportFile = @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_pack_validate.log";

        [MenuItem("Eira/Nivel 1/Auditar pack gaussiano")]
        public static void Validate()
        {
            var sb = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sb.AppendLine("escena: " + scene.name + " roots: " + scene.rootCount);

            var env = GameObject.Find("GaussianEnvironment");
            if (env == null) sb.AppendLine("GaussianEnvironment: NO");
            else
            {
                sb.AppendLine("env pos: " + env.transform.position + " scale: " + env.transform.localScale + " euler: " + env.transform.eulerAngles);
                var mc = env.GetComponent<MeshCollider>();
                if (mc != null && mc.sharedMesh != null)
                {
                    var b = mc.sharedMesh.bounds;
                    sb.AppendLine("collider mesh local: center=" + b.center + " size=" + b.size + " verts=" + mc.sharedMesh.vertexCount);
                    sb.AppendLine("collider world: min=" + mc.bounds.min + " max=" + mc.bounds.max + " size=" + mc.bounds.size);
                }
                else sb.AppendLine("MeshCollider: NO/MESH NULL");
                foreach (var c in env.GetComponentsInChildren<Component>(true))
                    if (c != null && c.GetType().Name == "GaussianSplatRenderer")
                        sb.AppendLine("renderer: " + c.GetType().FullName);
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            sb.AppendLine(player != null ? ("player: " + player.name + " pos=" + player.transform.position) : "player: NO");

            foreach (var go in scene.GetRootGameObjects())
            {
                var rs = go.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                bool init = false; Bounds b = new Bounds();
                foreach (var r in rs) { if (r is ParticleSystemRenderer) continue; if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds); }
                if (init) sb.AppendLine("bounds[" + go.name + "] min=" + b.min + " max=" + b.max + " size=" + b.size);
            }

            sb.AppendLine("-- roots --");
            foreach (var go in scene.GetRootGameObjects()) sb.AppendLine("root: " + go.name);

            System.IO.File.WriteAllText(ReportFile, sb.ToString());
            Debug.Log("ValidatePack:\n" + sb);
        }
    }
}
