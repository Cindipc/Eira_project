using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.Editor
{
    // Reemplaza el escenario actual del Nivel 1 por un modelo 3D importado (.glb).
    // Copia el modelo a Assets/Eira/Environment, desactiva la escenografía vieja,
    // instancia el modelo dentro de "Escenografia", le calcula escala/colliders
    // y reubica las luces de zona.
    public static class ReplaceScenario
    {
        public const string SourceGlb = @"C:\Users\Usuario\Downloads\mapshalo_2campaigne3_smoke_and_mirrors.glb";
        public const string TargetPath = EiraPaths.Env + "/mapshalo_2campaigne3_smoke_and_mirrors.glb";

        // Huella horizontal objetivo (en unidades de escena) para escalar el mapa.
        public static float TargetFootprint = 100f;

        const string ReportFile = @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_replace_map.log";

        [MenuItem("Eira/Reemplazar Escenario (mapshalo)")]
        public static void Execute()
        {
            var report = new System.Text.StringBuilder();

            // 1) Importar el modelo al proyecto.
            Directory.CreateDirectory(EiraPaths.Env);
            File.Copy(SourceGlb, TargetPath, true);

            // 2) Abrir/cargar Level1Scene.
            var scene = EditorSceneManager.OpenScene(EiraPaths.Scenes + "/Level1Scene.unity", OpenSceneMode.Single);

            // 3) Desactivar el escenario actual.
            var esc = GameObject.Find("Escenografia");
            var env = GameObject.Find("GaussianEnvironment");
            if (env != null) env.SetActive(false);
            if (esc != null)
                foreach (var child in esc.transform.Cast<Transform>().ToArray())
                    child.gameObject.SetActive(false);
            if (esc == null)
                esc = new GameObject("Escenografia");

            // 4) Construir el modelo con el importador GLB propio, medir y escalar.
            Mesh[] builtMeshes;
            var instance = GlbSceneImporter.BuildFromFile(TargetPath, "mapshalo_2campaigne3_smoke_and_mirrors", out builtMeshes);
            var native = MeasureBounds(instance);
            float s = 1f;
            if (native.size.x > 0.0001f || native.size.z > 0.0001f)
                s = TargetFootprint / Mathf.Max(native.size.x, native.size.z);
            GlbSceneImporter.SaveMeshesAsAssets(builtMeshes, EiraPaths.Env + "/MapMeshes");
            instance.transform.SetParent(esc.transform, false);
            instance.transform.localScale = new Vector3(s, s, s);
            instance.transform.rotation = Quaternion.identity;
            instance.transform.position = Vector3.zero;
            var rebased = MeasureBounds(instance);
            instance.transform.position = new Vector3(-rebased.center.x, -rebased.min.y, -rebased.center.z);

            // 5) Colliders sobre todas las mallas.
            EnsureColliders(instance.transform);
            var finalBounds = MeasureBounds(instance);

            // 6) Reubicar luces de zona dentro del mapa.
            RelocateLights(finalBounds);

            EditorSceneManager.SaveScene(scene);

            report.AppendLine("mapa importado: " + TargetPath);
            report.AppendLine("mallas construidas: " + (builtMeshes == null ? 0 : builtMeshes.Length));
            report.AppendLine("bounds nativos: " + native);
            report.AppendLine("escala elegida: " + s);
            report.AppendLine("bounds finales: " + finalBounds);
            report.AppendLine("footprint final (x,z): " + finalBounds.size.x + ", " + finalBounds.size.z);
            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("ReplaceScenario: Mapa importado y escena guardada. " + report);
        }

        static Bounds MeasureBounds(GameObject root)
        {
            var rs = root.GetComponentsInChildren<Renderer>(true);
            bool init = false;
            var b = new Bounds();
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        static void EnsureColliders(Transform root)
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshCollider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>();
            }
        }

        static void RelocateLights(Bounds mb)
        {
            string[] names = { "WarmSpawn", "CoolCorridor", "CyanPuzzle", "RedBoss", "GreenExit" };
            float[] t = { 0.06f, 0.27f, 0.5f, 0.73f, 0.94f };
            bool alongX = mb.size.x >= mb.size.z;
            for (int i = 0; i < names.Length; i++)
            {
                var go = GameObject.Find(names[i]);
                if (go == null) continue;
                Vector3 p;
                if (alongX)
                    p = new Vector3(mb.min.x + mb.size.x * t[i], mb.min.y + mb.size.y * 0.6f, mb.center.z);
                else
                    p = new Vector3(mb.center.x, mb.min.y + mb.size.y * 0.6f, mb.min.z + mb.size.z * t[i]);
                go.transform.position = p;
                go.transform.rotation = Quaternion.identity;
            }
        }
    }
}