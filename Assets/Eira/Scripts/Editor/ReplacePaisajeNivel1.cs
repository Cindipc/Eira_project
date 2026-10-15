using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.Editor
{
    // Reemplaza el paisaje del Nivel 1 por el modelo descargado ("пейзаж ск.fbx"),
    // lo centra y lo escala para rodear todo el laboratorio (donde juega Eira) y
    // deja el trigger de salida cargando el Nivel 2 al salir del laboratorio.
    //
    //   Eira/Nivel 1/Reemplazar paisaje y rodear laboratorio
    public static class ReplacePaisajeNivel1
    {
        // ---------- Origen (descarga) ----------
        const string SourceFbx =
            @"C:\Users\Usuario\Downloads\paisaje\source\пейзаж ск.fbx";
        const string SourceTexDir =
            @"C:\Users\Usuario\Downloads\paisaje\textures";

        // ---------- Destino en el proyecto ----------
        const string ModelDir = "Assets/Eira/Models/PaisajeNivel1";
        const string DestFbx = ModelDir + "/Paisaje.fbx";
        const string TexDir = ModelDir + "/Texturas";
        const string MatDir = ModelDir + "/Materials";

        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";

        // Nombre del laboratorio (prefab instance) y del paisaje viejo a borrar.
        // En Level1Scene el escenario antiguo (splat gaussiano + collider) es el
        // objeto "GaussianEnvironment"; el laboratorio jugable esta dentro.
        const string LabName = "GaussianEnvironment";
        const string OldLandscapeName = "GaussianEnvironment";

        // Nombre del paisaje nuevo y del trigger de salida.
        const string NewLandscapeName = "PaisajeNivel1_Environment";
        const string ExitName = "LevelExit";
        const string NextLevelScene = "Level2Scene";

        // Margen (en unidades) que el paisaje sobresale del laboratorio por lado.
        const float Margin = 30f;

        // Altura a la que se dejan la base del paisaje y Eira (nivel antiguo).
        const float LevelHeight = 1.2f;

        const string ReportFile =
            @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_paisaje.log";

        [MenuItem("Eira/Nivel 1/Probar import Standard", false, 32)]
        public static void TryImportStandard()
        {
            var imp = AssetImporter.GetAtPath(DestFbx) as ModelImporter;
            if (imp == null) { Debug.LogError("[Eira] sin importer"); return; }
            imp.materialImportMode =
                ModelImporterMaterialImportMode.ImportStandard;
            imp.materialLocation = ModelImporterMaterialLocation.InPrefab;
            imp.materialSearch = ModelImporterMaterialSearch.Everywhere;
            imp.SaveAndReimport();
            Validate();
        }

        [MenuItem("Eira/Nivel 1/Auditar paisaje", false, 31)]
        public static void Validate()
        {
            var report = new StringBuilder();

            // Sub-assets del modelo importado (texturas/materiales/mallas).
            var subs = AssetDatabase.LoadAllAssetsAtPath(DestFbx);
            int tex = 0, mat = 0, mesh = 0;
            foreach (var a in subs)
            {
                if (a is Texture2D) tex++;
                else if (a is Material) mat++;
                else if (a is Mesh) mesh++;
            }
            report.AppendLine("sub-assets -> texturas: " + tex +
                              " · materiales: " + mat + " · mallas: " + mesh);

            int shown = 0;
            foreach (var a in subs)
            {
                var m = a as Material;
                if (m == null) continue;

                report.AppendLine("  mat " + m.name + " · " +
                                  (m.shader != null ? m.shader.name : "null"));

                var so = new SerializedObject(m);
                var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
                if (texEnvs != null && texEnvs.isArray)
                {
                    for (int i = 0; i < texEnvs.arraySize; i++)
                    {
                        var e = texEnvs.GetArrayElementAtIndex(i);
                        var key = e.FindPropertyRelative("first").stringValue;
                        var tx = e.FindPropertyRelative("second.m_Texture")
                            .objectReferenceValue as Texture;
                        if (tx != null)
                            report.AppendLine("      tex " + key + " = " +
                                              tx.name);
                    }
                }

                if (++shown >= 12) break;
            }

            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single);

            var land = GameObject.Find(NewLandscapeName);
            report.AppendLine("paisaje presente: " + (land != null));

            if (land != null)
            {
                var rends = land.GetComponentsInChildren<Renderer>(true);
                var mats = new System.Collections.Generic.HashSet<Material>();
                int withTex = 0;

                foreach (var r in rends)
                    foreach (var m in r.sharedMaterials)
                        if (m != null) mats.Add(m);

                foreach (var m in mats)
                    if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                        withTex++;
                    else if (m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null)
                        withTex++;

                report.AppendLine("renderers: " + rends.Length);
                report.AppendLine("colliders paisaje: " +
                    land.GetComponentsInChildren<Collider>(true).Length);
                report.AppendLine("materiales: " + mats.Count +
                                  " · con textura: " + withTex);
                report.AppendLine("bounds: " + RendererBounds(land));
            }

            var exit = GameObject.Find(ExitName);
            if (exit != null)
            {
                report.AppendLine("salida componentes: " +
                    exit.GetComponents<MonoBehaviour>().Length +
                    " · LevelExit: " + (exit.GetComponent<LevelExit>() != null));
            }

            var eira = GameObject.Find("Eira");
            if (eira != null)
            {
                report.AppendLine("Eira y: " + eira.transform.position.y);

                Physics.SyncTransforms();
                var hh = Physics.RaycastAll(
                    new Vector3(eira.transform.position.x, 300f,
                                eira.transform.position.z),
                    Vector3.down, 600f);
                System.Array.Sort(hh,
                    (a, b) => b.point.y.CompareTo(a.point.y));
                report.AppendLine("hits bajo Eira: " + hh.Length);
                for (int i = 0; i < hh.Length && i < 12; i++)
                    report.AppendLine("   y=" + hh[i].point.y + "  obj=" +
                                      hh[i].collider.gameObject.name);
            }

            var lab = GameObject.Find(LabName);
            if (lab != null)
            {
                var lb = RendererBounds(lab);
                report.AppendLine("lab bounds min/max y: " + lb.min.y + " / " + lb.max.y);
            }

            var mk = PlayableMarkersBounds();
            report.AppendLine("markers y min/max: " + mk.min.y + " / " + mk.max.y);

            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[Eira] Validate:\n" + report);
        }

        [MenuItem("Eira/Nivel 1/Diagnostico alturas", false, 33)]
        public static void HeightDiag()
        {
            var sb = new StringBuilder();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();

            var land = GameObject.Find(NewLandscapeName);
            if (land == null) { Debug.LogError("[Eira] sin paisaje"); return; }

            var points = new List<GameObject>();
            AddObjects<DoorControl>(points);
            AddObjects<CheckpointZone>(points);
            AddObjects<DataLog>(points);
            AddObjects<PanelActivator>(points);
            AddObjects<LevelExitZone>(points);
            AddObjects<GuardianBoss>(points);

            var eira = GameObject.Find("Eira");
            if (eira != null) points.Add(eira);
            var exit = GameObject.Find(ExitName);
            if (exit != null && !points.Contains(exit)) points.Add(exit);

            Bounds lb = RendererBounds(land);
            float startY = lb.max.y + 50f;
            float dist = lb.size.y + 200f;

            int buried = 0, ok = 0;
            foreach (var go in points)
            {
                if (go == null) continue;
                var p = go.transform.position;

                var hits = Physics.RaycastAll(
                    new Vector3(p.x, startY, p.z), Vector3.down, dist);

                float landY = float.NaN, otherY = float.NaN;
                for (int i = 0; i < hits.Length; i++)
                {
                    var c = hits[i].collider;
                    if (c == null) continue;
                    if (c.transform.IsChildOf(go.transform)) continue;

                    if (c.transform.IsChildOf(land.transform))
                    {
                        if (float.IsNaN(landY) || hits[i].point.y > landY)
                            landY = hits[i].point.y;
                    }
                    else if (float.IsNaN(otherY) || hits[i].point.y > otherY)
                        otherY = hits[i].point.y;
                }

                float delta = float.IsNaN(landY) ? 0f : p.y - landY;
                if (delta < -0.5f) buried++;
                else ok++;

                sb.AppendLine(go.name + " @(" + p.x.ToString("0.0") + "," +
                    p.y.ToString("0.00") + "," + p.z.ToString("0.0") +
                    ")  terreno=" + (float.IsNaN(landY) ? "n/a" :
                        landY.ToString("0.00")) +
                    "  delta=" + delta.ToString("0.00") +
                    (delta < -0.5f ? "  <-- ENTERRADO" : ""));
            }

            sb.AppendLine("total=" + points.Count + "  enterrados=" + buried +
                          "  ok=" + ok);
            File.WriteAllText(
                @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_heights.log",
                sb.ToString());
            Debug.Log("[Eira] HeightDiag:\n" + sb);
        }

        static void AddObjects<T>(List<GameObject> list) where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null && !list.Contains(found[i].gameObject))
                    list.Add(found[i].gameObject);
        }

        [MenuItem("Eira/Nivel 1/Reemplazar paisaje y rodear laboratorio", false, 30)]
        public static void Execute()
        {
            var report = new StringBuilder();

            // 1) Importar el modelo al proyecto.
            Directory.CreateDirectory(ModelDir);
            if (!File.Exists(SourceFbx))
            {
                Fail(report, "No existe el modelo de origen: " + SourceFbx);
                return;
            }

            File.Copy(SourceFbx, DestFbx, true);

            // Las texturas del FBX vienen embebidas como nodos "Video" que
            // Unity no extrae. Las copiamos junto al modelo con el nombre
            // exacto al que el FBX apunta, para que el importador las enlace.
            ProvisionTextures(report);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(
                DestFbx, ImportAssetOptions.ForceSynchronousImport);

            // Texturas + materiales URP propios (Unity no enlaza las texturas
            // embebidas del FBX, asi que las asignamos manualmente segun el
            // mapa extraido del propio FBX).
            ConfigureTextures();
            var matMap = CreateMaterials(report);

            var importer = AssetImporter.GetAtPath(DestFbx) as ModelImporter;
            if (importer != null)
            {
                importer.importAnimation = false;
                importer.importCameras = false;
                importer.importLights = false;
                // El Nivel 1 se juega sobre el paisaje: necesita colisiones
                // para que Eira camine por el terreno (malla legible para que
                // el MeshCollider tenga datos de colision).
                importer.addCollider = true;
                importer.isReadable = true;
                importer.materialImportMode =
                    ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.materialLocation =
                    ModelImporterMaterialLocation.InPrefab;
                importer.materialSearch =
                    ModelImporterMaterialSearch.Everywhere;
                RemapMaterials(importer, matMap, report);
                importer.SaveAndReimport();
            }

            report.AppendLine("modelo importado: " + DestFbx);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(DestFbx);
            if (model == null)
            {
                Fail(report, "Unity no pudo importar el modelo: " + DestFbx);
                return;
            }

            // 2) Abrir el Nivel 1.
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Fail(report, "No se pudo abrir " + ScenePath);
                return;
            }

            // 3) Borrar el paisaje viejo y cualquier copia previa del nuevo.
            RemoveByName(OldLandscapeName);
            RemoveByName(NewLandscapeName);

            // 4) Medir todo el nivel (marcadores jugables + malla del laboratorio)
            //    para saber donde centrar y cuanto escalar el paisaje.
            Bounds levelBounds = PlayableMarkersBounds();

            var lab = GameObject.Find(LabName);
            if (lab != null)
                levelBounds = Union(levelBounds, RendererBounds(lab));

            if (levelBounds.size == Vector3.zero)
                levelBounds = new Bounds(
                    Vector3.zero, new Vector3(120f, 4f, 20f));

            report.AppendLine("nivel: " + levelBounds.center +
                              " · tamaño " + levelBounds.size);

            // 5) Instanciar el paisaje nuevo en la raiz de la escena.
            var landscape =
                (GameObject)PrefabUtility.InstantiatePrefab(model);
            landscape.name = NewLandscapeName;
            landscape.transform.SetParent(null, false);

            StripComponents(landscape);

            // 6) Escalar para rodear el laboratorio con un margen.
            Bounds native = RendererBounds(landscape);
            float nativeFootprint = Mathf.Max(native.size.x, native.size.z);
            float targetFootprint =
                Mathf.Max(levelBounds.size.x, levelBounds.size.z) + 2f * Margin;

            float s = nativeFootprint > 0.0001f
                ? targetFootprint / nativeFootprint
                : 1f;

            landscape.transform.localScale *= s;

            // 7) Centrar el paisaje sobre el nivel y apoyar su base en el suelo.
            float groundY = LevelHeight;

            Bounds scaled = RendererBounds(landscape);
            landscape.transform.position += new Vector3(
                levelBounds.center.x - scaled.center.x,
                groundY - scaled.min.y,
                levelBounds.center.z - scaled.center.z
            );

            Bounds finalBounds = RendererBounds(landscape);

            report.AppendLine("escala elegida: " + s);
            report.AppendLine("paisaje final: " + finalBounds.center +
                              " · tamaño " + finalBounds.size);
            report.AppendLine("footprint (x,z): " + finalBounds.size.x +
                              ", " + finalBounds.size.z);

            // 7b) Quitar el escenario antiguo (splat gaussiano) del nivel.
            RemoveByName(OldLandscapeName);
            report.AppendLine("escenario antiguo '" + OldLandscapeName +
                              "' eliminado");

            // 8) Salida: al salir del laboratorio, cargar el Nivel 2.
            ConfigureExit(report);

            // 9) Apoyar cada objeto jugable sobre la superficie del paisaje,
            //    conservando su altura original sobre el suelo (y=0).
            SnapPlayablesToTerrain(landscape, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            report.AppendLine("escena guardada: " + ScenePath);

            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[Eira] ReplacePaisajeNivel1:\n" + report);
        }

        // =============================================================
        // Salida al Nivel 2
        // =============================================================

        static void ConfigureExit(StringBuilder report)
        {
            var old = GameObject.Find(ExitName);

            if (old == null)
            {
                report.AppendLine("AVISO: no se encontro '" + ExitName +
                                  "'; no se cambio la salida.");
                return;
            }

            // El objeto de salida arrastra un LevelExitZone roto que muestra
            // la pantalla de victoria. Se rehace limpio: misma posicion, misma
            // caja de disparo y solo el componente que carga el Nivel 2.
            Transform parent = old.transform.parent;
            Vector3 pos = old.transform.position;
            Quaternion rot = old.transform.rotation;

            var box = old.GetComponent<BoxCollider>();
            Vector3 boxSize = box != null ? box.size : new Vector3(3f, 4f, 6f);

            Object.DestroyImmediate(old);

            var exit = new GameObject(ExitName);
            exit.transform.SetParent(parent, false);
            exit.transform.position = pos;
            exit.transform.rotation = rot;

            var col = exit.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = boxSize;
            col.center = Vector3.zero;

            var comp = exit.AddComponent<LevelExit>();

            report.AppendLine("salida recreada en " + pos +
                              " (caja " + boxSize + ")");

            var so = new SerializedObject(comp);
            var next = so.FindProperty("nextLevelScene");
            if (next != null) next.stringValue = NextLevelScene;

            var auto = so.FindProperty("autoLoadNextLevel");
            if (auto != null) auto.boolValue = true;

            var boss = so.FindProperty("requireBossDefeated");
            if (boss != null) boss.boolValue = true;

            var delay = so.FindProperty("exitDelay");
            if (delay != null) delay.floatValue = 2f;

            so.ApplyModifiedPropertiesWithoutUndo();

            report.AppendLine("salida '" + ExitName + "' -> " + NextLevelScene +
                              " (automatico, requiere guardian)");
        }

        // =============================================================
        // Utilidades de escena
        // =============================================================

        static void RemoveByName(string name)
        {
            var all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i].name != name)
                    continue;

                // Si el objeto esta dentro de una instancia de prefab, se borra
                // la raiz de la instancia completa; si no, el objeto tal cual.
                var root = PrefabUtility.GetOutermostPrefabInstanceRoot(
                    all[i].gameObject);

                Object.DestroyImmediate(root != null ? root : all[i].gameObject);
            }
        }

        static void StripComponents(GameObject root)
        {
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                Object.DestroyImmediate(cam);

            foreach (var light in root.GetComponentsInChildren<Light>(true))
                Object.DestroyImmediate(light);

            foreach (var anim in root.GetComponentsInChildren<Animator>(true))
                Object.DestroyImmediate(anim);
        }

        // Apoya sobre la superficie del paisaje todos los objetos que forman
        // el laboratorio jugable (hijos de "Gameplay" y las raices del nivel),
        // conservando su altura original sobre el suelo del laboratorio (y=0).
        static void SnapPlayablesToTerrain(GameObject landscape, StringBuilder report)
        {
            Physics.SyncTransforms();

            var targets = new List<Transform>();

            var gameplay = GameObject.Find("Gameplay");
            if (gameplay != null)
                foreach (Transform c in gameplay.transform)
                    if (c != null) targets.Add(c);

            string[] rootNames =
            {
                "Eira", "GreenExit", "RedBoss", "CoolCorridor", "CyanPuzzle",
                "ZoneA_Ceiling", "ZoneB_Ceiling", "ZoneD_Ceiling",
                "ZoneE_Ceiling", "WarmSpawn", "NOVA"
            };

            foreach (var n in rootNames)
            {
                var go = GameObject.Find(n);
                if (go != null && go.transform.parent == null)
                    targets.Add(go.transform);
            }

            int moved = 0, skipped = 0;
            var sb = new StringBuilder();

            foreach (var t in targets)
            {
                if (t == null) continue;

                Vector3 p = t.position;
                float surface = TerrainSurfaceY(landscape, p.x, p.z);

                if (float.IsNaN(surface))
                {
                    skipped++;
                    sb.AppendLine("  sin terreno bajo " + t.name +
                                  " @(" + p.x + "," + p.z + ")");
                    continue;
                }

                // El suelo del laboratorio estaba en y=0; cada objeto conserva
                // su altura relativa y se sube/baja con el relieve local.
                Vector3 np = new Vector3(p.x, p.y + surface, p.z);
                t.position = np;
                moved++;

                sb.AppendLine("  " + t.name + " y " + p.y.ToString("0.00") +
                              " -> " + np.y.ToString("0.00"));
            }

            report.AppendLine("objetos apoyados en el terreno: " + moved +
                              " (sin terreno: " + skipped + ")");
            report.AppendLine(sb.ToString());
        }

        // Superficie (mas alta) del paisaje bajo (x,z). NaN si no hay malla.
        static float TerrainSurfaceY(GameObject landscape, float x, float z)
        {
            Bounds lb = RendererBounds(landscape);
            float startY = lb.max.y + 50f;
            float dist = lb.size.y + 200f;

            var hits = Physics.RaycastAll(
                new Vector3(x, startY, z), Vector3.down, dist);

            float surfaceY = float.NaN;
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i].collider;
                if (col == null) continue;
                if (!col.transform.IsChildOf(landscape.transform)) continue;
                if (float.IsNaN(surfaceY) || hits[i].point.y > surfaceY)
                    surfaceY = hits[i].point.y;
            }

            return surfaceY;
        }

        static Bounds RendererBounds(GameObject root)
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

        // Extensión de la ruta jugable, deducida de los marcadores que ya
        // existen en la escena (puertas, checkpoints, registros, consolas,
        // salida, jefe y la propia Eira).
        static Bounds PlayableMarkersBounds()
        {
            var points = new System.Collections.Generic.List<Vector3>();

            AddPositions<DoorControl>(points);
            AddPositions<CheckpointZone>(points);
            AddPositions<DataLog>(points);
            AddPositions<PanelActivator>(points);
            AddPositions<LevelExitZone>(points);
            AddPositions<GuardianBoss>(points);

            var eira = GameObject.Find("Eira");
            if (eira != null)
            {
                Bounds eb = RendererBounds(eira);
                points.Add(eb.min);
                points.Add(eb.max);
            }

            if (points.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var b = new Bounds(points[0], Vector3.zero);
            for (int i = 1; i < points.Count; i++)
                b.Encapsulate(points[i]);

            // Un colchon para que el paisaje no acabe justo en el borde.
            b.Expand(new Vector3(12f, 4f, 12f));
            return b;
        }

        static Bounds Union(Bounds a, Bounds b)
        {
            if (a.size == Vector3.zero) return b;
            if (b.size == Vector3.zero) return a;

            a.Encapsulate(b);
            return a;
        }

        static void AddPositions<T>(
            System.Collections.Generic.List<Vector3> points)
            where T : Component
        {
            var found = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
            for (int i = 0; i < found.Length; i++)
                if (found[i] != null) points.Add(found[i].transform.position);
        }

        // Copia las texturas de la descarga a la carpeta del modelo, con el
        // nombre exacto (extension incluida) al que el FBX hace referencia.
        static void ProvisionTextures(StringBuilder report)
        {
            if (!Directory.Exists(SourceTexDir))
            {
                report.AppendLine("aviso: no existe " + SourceTexDir);
                return;
            }

            Directory.CreateDirectory(TexDir);
            int n = 0;
            foreach (var f in Directory.GetFiles(SourceTexDir))
            {
                var name = FbxTextureName(Path.GetFileName(f));
                File.Copy(f, Path.Combine(TexDir, name), true);
                n++;
            }
            report.AppendLine("texturas provisionadas: " + n + " -> " + TexDir);
        }

        // El FBX espera ".jpg" y nombres "(carve.photos).png"; los ficheros de
        // la descarga usan ".jpeg" y "carve.photos.png" (sin cerrar ")").
        static string FbxTextureName(string fileName)
        {
            if (fileName.EndsWith(".jpeg",
                    System.StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 5) + ".jpg";

            if (fileName.EndsWith("-no-bg-preview (carve.photos.png",
                    System.StringComparison.OrdinalIgnoreCase))
                return fileName.Substring(0, fileName.Length - 4) + ").png";

            return fileName;
        }

        // Mapa material del FBX -> texturas, extraido de las conexiones del
        // propio FBX (Video -> Texture -> Material).
        class MatRec
        {
            public string Name;
            public string Base;
            public string Norm;
            public string Alpha;
            public string Emit;
        }

        static readonly MatRec[] MatMap =
        {
            new MatRec { Name = "Material.001",
                Base = "e8131ba5aad19a49706f4e45c5569e3f.jpg",
                Norm = "NormalMap (2).png" },
            new MatRec { Name = "Material.002",
                Base = "e481511a144322f1377bebc636f90e74-no-bg-preview (carve.photos).png",
                Alpha = "e481511a144322f1377bebc636f90e74-no-bg-preview (carve.photos).png" },
            new MatRec { Name = "Material.003",
                Base = "9d3980080c37eba9b9a651075fb5216c-no-bg-preview (carve.photos).png",
                Alpha = "9d3980080c37eba9b9a651075fb5216c-no-bg-preview (carve.photos).png" },
            new MatRec { Name = "Material.004",
                Base = "5abdab9bf64818adacd4984d5925e5ca-no-bg-preview (carve.photos).png",
                Alpha = "5abdab9bf64818adacd4984d5925e5ca-no-bg-preview (carve.photos).png" },
            new MatRec { Name = "Material.005",
                Base = "fd2367343ff9f30f3bc7d7a9c2a8ccec-no-bg-preview (carve.photos).png",
                Alpha = "fd2367343ff9f30f3bc7d7a9c2a8ccec-no-bg-preview (carve.photos).png" },
            new MatRec { Name = "Material.006",
                Base = "f56d72944dec91c12d398029977a8303-no-bg-preview (carve.photos).png",
                Alpha = "f56d72944dec91c12d398029977a8303-no-bg-preview (carve.photos).png" },
            new MatRec { Name = "Material.007",
                Base = "cf48572a73754e133ab5d2155f296d9b.jpg",
                Norm = "NormalMap (1).png" },
            new MatRec { Name = "Material.008",
                Base = "62b0e89a32dd55f0d2741ce8aa22014f.jpg",
                Norm = "NormalMap (3).png" },
            new MatRec { Name = "Material.009",
                Base = "e05eae72d4fb12a6c849d350f6404440.jpg",
                Norm = "NormalMap.png" },
            new MatRec { Name = "Material.011",
                Base = "file.jpg",
                Emit = "file.jpg" },
        };

        // Ajusta las texturas importadas: normal maps como NormalMap y las
        // que llevan alfa (recortes de vegetacion) con transparencia.
        static void ConfigureTextures()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var f in Directory.GetFiles(TexDir))
            {
                if (f.EndsWith(".meta")) continue;

                var ti = AssetImporter.GetAtPath(f) as TextureImporter;
                if (ti == null) continue;

                var file = Path.GetFileName(f);

                if (file.StartsWith("NormalMap",
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    ti.textureType = TextureImporterType.NormalMap;
                }
                else
                {
                    ti.textureType = TextureImporterType.Default;
                    ti.sRGBTexture = true;
                    ti.alphaIsTransparency = file.Contains("carve.photos");
                }

                ti.SaveAndReimport();
            }
        }

        static Dictionary<string, Material> CreateMaterials(StringBuilder report)
        {
            Directory.CreateDirectory(MatDir);
            var map = new Dictionary<string, Material>();
            var urp = Shader.Find("Universal Render Pipeline/Lit");

            foreach (var r in MatMap)
            {
                var m = new Material(urp != null ? urp : Shader.Find("Standard"))
                {
                    name = r.Name
                };

                SetTex(m, "_BaseMap", r.Base, true);
                SetTex(m, "_BumpMap", r.Norm, false);
                if (r.Norm != null)
                {
                    m.SetFloat("_BumpScale", 1f);
                    m.EnableKeyword("_NORMALMAP");
                }

                if (r.Emit != null)
                {
                    SetTex(m, "_EmissionMap", r.Emit, true);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.white);
                }

                if (r.Alpha != null)
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.5f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                }

                var path = MatDir + "/" + r.Name + ".mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(m, path);
                map[r.Name] = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            AssetDatabase.SaveAssets();
            report.AppendLine("materiales creados: " + map.Count);
            return map;
        }

        static void SetTex(Material m, string prop, string file, bool srgb)
        {
            if (file == null) return;
            var t = AssetDatabase.LoadAssetAtPath<Texture>(TexDir + "/" + file);
            if (t == null) return;
            m.SetTexture(prop, t);
        }

        static void RemapMaterials(ModelImporter importer,
            Dictionary<string, Material> map, StringBuilder report)
        {
            int n = 0;
            foreach (var kv in map)
            {
                importer.AddRemap(
                    new AssetImporter.SourceAssetIdentifier(
                        typeof(Material), kv.Key), kv.Value);
                n++;
            }
            report.AppendLine("remaps de material: " + n);
        }

        // =============================================================
        // Arreglo: quitar la ciudad del Nivel 2 y restaurar alturas
        // =============================================================

        [MenuItem("Eira/Nivel 1/Arreglar (quitar ciudad N2 + restaurar alturas)", false, 34)]
        public static void Arreglar()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RemoveByName("FuturisticCity_Environment");
            report.AppendLine("FuturisticCity_Environment (ciudad del Nivel 2) eliminada");

            var land = GameObject.Find(NewLandscapeName);
            if (land == null) { Fail(report, "sin paisaje en la escena"); return; }

            UndoSnapPlayables(land, report);
            LowerLandscapeBelowLab(land, report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            report.AppendLine("escena guardada: " + ScenePath);

            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[Eira] Arreglar:\n" + report);
        }

        // Coloca un piso plano al nivel del BOSQUE (el paisaje quedo por debajo
        // del lab para no atravesarlo) y una rampa desde el laboratorio hasta
        // ese piso, para que Eira pueda bajar a caminar entre los arboles.
        [MenuItem("Eira/Nivel 1/Poner piso del bosque", false, 35)]
        public static void PonerPisoBosque()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            RemoveByName("PisoBosque");
            RemoveByName("RampaBosque");
            Physics.SyncTransforms();

            // Superficie del terreno del bosque (rayo hacia abajo desde lo alto).
            float y = -8.0f; // Nivel del bosque (paisaje bajado para no atravesar el lab)
            report.AppendLine("nivel del bosque seleccionado y=" + y.ToString("0.00"));

            var mat = GroundMaterial();

            // ---- Piso plano (70 x 34) a nivel del bosque ----
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "PisoBosque";
            floor.transform.position = new Vector3(60f, y + 0.05f, 14f);
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = new Vector3(7f, 1f, 3.4f);
            var fr = floor.GetComponent<MeshRenderer>();
            if (fr != null) fr.sharedMaterial = mat;
            var mc = floor.GetComponent<MeshCollider>();
            if (mc == null) mc = floor.AddComponent<MeshCollider>();
            floor.isStatic = true;

            // ---- Rampa desde el lab (y=0) hasta el piso del bosque ----
            Vector3 top = new Vector3(60f, 0.07f, 12f);
            Vector3 bot = new Vector3(60f, y, 24f);
            Vector3 mid = (top + bot) * 0.5f;
            float dz = bot.z - top.z;
            float dy = bot.y - top.y;
            float pitch = Mathf.Atan2(-dy, dz) * Mathf.Rad2Deg;
            float len = Mathf.Sqrt(dz * dz + dy * dy);

            var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "RampaBosque";
            ramp.transform.position = mid;
            ramp.transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
            ramp.transform.localScale = new Vector3(6f, 0.5f, len);
            var rr = ramp.GetComponent<MeshRenderer>();
            if (rr != null) rr.sharedMaterial = mat;
            ramp.isStatic = true;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            report.AppendLine("PisoBosque en y=" + (y + 0.05f).ToString("0.00") + " (70x34)");
            report.AppendLine("RampaBosque de " + top.ToString("0.0") + " a " + bot.ToString("0.0") +
                              " pitch=" + pitch.ToString("0.0"));
            report.AppendLine("escena guardada: " + ScenePath);

            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[Eira] PonerPisoBosque:\n" + report);
        }

        static Material GroundMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(
                MatDir + "/Material.001.mat");
            if (mat != null) return mat;

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var m = new Material(sh) { name = "PisoBosqueMat" };
            m.SetColor("_BaseColor", new Color(0.22f, 0.32f, 0.15f, 1f));
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0f);
            return m;
        }

        // Inverso de SnapPlayablesToTerrain: cada objeto jugable vuelve a su
        // altura de suelo del laboratorio restando la altura de terreno que se
        // le sumo en el swap.
        static void UndoSnapPlayables(GameObject landscape, StringBuilder report)
        {
            Physics.SyncTransforms();
            var targets = PlayableTargets();
            int moved = 0, skipped = 0;

            foreach (var t in targets)
            {
                if (t == null) continue;

                Vector3 p = t.position;
                float s = TerrainSurfaceY(landscape, p.x, p.z);

                if (float.IsNaN(s)) { skipped++; continue; }

                t.position = new Vector3(p.x, p.y - s, p.z);
                moved++;
            }

            report.AppendLine("objetos restaurados a la altura del suelo del lab: " +
                              moved + " (sin terreno: " + skipped + ")");
        }

        static List<Transform> PlayableTargets()
        {
            var targets = new List<Transform>();

            var gameplay = GameObject.Find("Gameplay");
            if (gameplay != null)
                foreach (Transform c in gameplay.transform)
                    if (c != null) targets.Add(c);

            string[] rootNames =
            {
                "Eira", "GreenExit", "RedBoss", "CoolCorridor", "CyanPuzzle",
                "ZoneA_Ceiling", "ZoneB_Ceiling", "ZoneD_Ceiling",
                "ZoneE_Ceiling", "WarmSpawn", "NOVA"
            };

            foreach (var n in rootNames)
            {
                var go = GameObject.Find(n);
                if (go != null && go.transform.parent == null)
                    targets.Add(go.transform);
            }

            return targets;
        }

        // Baja el paisaje para que no entierre al laboratorio: deja el punto mas
        // alto del terreno bajo la huella del lab justo por debajo de su base.
        static void LowerLandscapeBelowLab(GameObject landscape, StringBuilder report)
        {
            Physics.SyncTransforms();

            Bounds lab = new Bounds();
            bool init = false;

            foreach (var n in new[] { "LabAbandonado", "Gameplay" })
            {
                var go = GameObject.Find(n);
                if (go == null) continue;
                var b = RendererBounds(go);
                if (!init) { lab = b; init = true; } else lab.Encapsulate(b);
            }

            if (!init)
                lab = new Bounds(new Vector3(60f, 1.5f, 0f), new Vector3(120f, 4f, 24f));

            float maxS = float.NegativeInfinity;
            int n2 = 0;

            for (float x = lab.min.x; x <= lab.max.x; x += 3f)
                for (float z = lab.min.z; z <= lab.max.z; z += 3f)
                {
                    float s = TerrainSurfaceY(landscape, x, z);
                    if (float.IsNaN(s)) continue;
                    if (s > maxS) maxS = s;
                    n2++;
                }

            if (n2 == 0)
            {
                report.AppendLine("AVISO: no se pudo muestrear el terreno del paisaje");
                return;
            }

            const float target = -0.5f; // justo debajo de la base del laboratorio
            float shift = target - maxS;

            landscape.transform.position += new Vector3(0f, shift, 0f);

            report.AppendLine("terreno mas alto bajo el lab: " + maxS.ToString("0.00") +
                              " -> desplazamiento paisaje " + shift.ToString("0.00") +
                              " (muestras " + n2 + ")");
        }

        static void Fail(StringBuilder report, string message)
        {
            report.AppendLine("ERROR: " + message);
            File.WriteAllText(ReportFile, report.ToString());
            Debug.LogError("[Eira] ReplacePaisajeNivel1: " + message);
        }
    }
}
