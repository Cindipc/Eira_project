using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.Editor
{
    /// <summary>
    /// Genera el Nivel 2 "La caza" sobre la ciudad futurista ya colocada
    /// (Nivel2City_Environment): conserva el entorno, Eira, NOVA, la camara y
    /// el GameManager, borra el contenido viejo y vuelve a construir la capa de
    /// gameplay (misiones, enemigos, recolectables, jefe y refugio).
    /// </summary>
    public static class Level2Builder
    {
        const string ScenePath = "Assets/Eira/Scenes/Level2Scene.unity";
        const string EnvName = "Nivel2City_Environment";
        const string GameplayRoot = "Nivel2_Gameplay";
        const string MinionFolder = "Assets/Eira/Environment/Level2";
        const string MinionPrefabPath = MinionFolder + "/AndroideMinion.prefab";
        const string ReportFile =
            @"C:\Users\Usuario\AppData\Local\Temp\opencode\level2_build.log";

        // Nombres de raiz que NO se tocan al limpiar la escena.
        static readonly string[] Keep =
        {
            "Nivel2City_Environment", "Eira", "NOVA", "EiraCamera", "MainCamera",
            "GameManager", "Sun", "SunDia", "SunTarde", "Directional Light", "HUD",
        };

        static readonly StringBuilder report = new StringBuilder();

        [MenuItem("Eira/Nivel 2/Construir Nivel 2 (La caza)", false, 2)]
        public static void Build()
        {
            report.Clear();
            Log("=== Construir Nivel 2 (La caza) ===");

            Scene scene = OpenLevel2();

            GameObject env = FindRoot(EnvName);
            if (env == null)
                Log("AVISO: no se encontro " + EnvName + " (se construye alrededor del origen).");

            CleanScene();

            // ---- materiales ----
            var androidJacket = ModelFactory.Lit(new Color(0.52f, 0.57f, 0.64f), 0.55f, 0.5f);
            var androidSkin = ModelFactory.Lit(new Color(0.72f, 0.75f, 0.8f), 0.4f, 0.45f);
            var dark = ModelFactory.Lit(new Color(0.1f, 0.12f, 0.16f), 0.6f, 0.5f);
            var eyeBlue = ModelFactory.Glow(new Color(0.35f, 0.7f, 1f));

            var rebelJacket = ModelFactory.Lit(new Color(0.45f, 0.3f, 0.18f), 0.1f, 0.3f);
            var rebelSkin = ModelFactory.Lit(new Color(0.86f, 0.68f, 0.52f), 0f, 0.4f);

            var kaelJacket = ModelFactory.Lit(new Color(0.2f, 0.06f, 0.06f), 0.2f, 0.4f);
            var kaelSkin = ModelFactory.Lit(new Color(0.68f, 0.58f, 0.52f), 0.05f, 0.35f);
            var kaelDark = ModelFactory.Lit(new Color(0.05f, 0.05f, 0.06f), 0.4f, 0.4f);
            var kaelExtra = ModelFactory.Lit(new Color(0.35f, 0.35f, 0.4f), 0.85f, 0.6f);

            var metal = ModelFactory.Lit(new Color(0.42f, 0.45f, 0.48f), 0.72f, 0.56f);
            var metalDark = ModelFactory.Lit(new Color(0.11f, 0.12f, 0.14f), 0.65f, 0.5f);
            var medkit = ModelFactory.Lit(new Color(0.85f, 0.9f, 0.95f), 0.2f, 0.5f);
            var medkitCross = ModelFactory.Glow(new Color(0.3f, 1f, 0.6f));

            var androidMats = new[] { androidJacket, androidSkin, dark, eyeBlue };
            var rebelMats = new[] { rebelJacket, rebelSkin, dark, rebelJacket };
            var kaelMats = new[] { kaelJacket, kaelSkin, kaelDark, kaelExtra };

            // ---- centro de la ciudad ----
            Vector3 center = CityCenter(env);
            Log("Centro de ciudad estimado: " + V(center));

            // ---- raiz de gameplay ----
            var rootGo = new GameObject(GameplayRoot);
            var root = rootGo.transform;

            float cz = center.z;
            float cx = center.x;

            // ---- personajes ----
            GameObject gmGo = FindRoot("GameManager");
            if (gmGo == null)
            {
                gmGo = new GameObject("GameManager", typeof(GameManager));
                Log("Creado GameManager (no existia).");
            }
            if (gmGo.GetComponent<Level2Director>() == null)
                gmGo.AddComponent<Level2Director>();

            GameObject eira = FindRoot("Eira");
            if (eira == null)
            {
                eira = BuildPlayerFallback();
                Log("AVISO: Eira no existia; creada.");
            }

            GameObject nova = FindRoot("NOVA");
            if (nova == null)
            {
                nova = BuildNovaFallback();
                Log("AVISO: NOVA no existia; creado.");
            }

            if (FindRoot("EiraCamera") == null && FindRoot("MainCamera") == null)
            {
                BuildCameraFallback();
                Log("AVISO: no habia camara; creada EiraCamera.");
            }

            // ---- posiciones a lo largo del eje X ----
            float startX = cx - 36f;
            Place(eira, startX, cz, 0.2f);
            Place(nova, startX + 1.8f, cz - 1.4f, 0.1f);

            // Puntos de interes
            float[] androidX = { cx - 26f, cx - 16f, cx - 6f, cx + 10f, cx + 20f, cx + 30f };
            float[] rebelX = { cx - 10f, cx - 2f, cx + 6f, cx + 34f };
            float[] techX = { cx - 20f, cx + 4f, cx + 24f };
            float[] fileX = { cx - 14f, cx + 12f, cx + 26f };
            float[] droneX = { cx - 12f, cx + 8f, cx + 28f };
            float[] checkpointX = { startX, cx - 2f, cx + 18f };
            float[] hideX = { cx - 24f, cx + 2f, cx + 22f };
            float[] pickupX = { cx - 30f, cx - 18f, cx - 8f, cx + 6f, cx + 18f, cx + 32f };
            float refugeX = cx + 40f;

            // ---- Jefe Kael (aparece a media travesia) ----
            float kaelX = cx + 15f;
            var kael = BuildHumanoid("Kael", root, kaelMats, HumanStyle.Kael);
            Place(kael, kaelX, cz, 0.05f);
            var kaelBoss = kael.AddComponent<KaelBoss>();

            // ---- refuerzos de Kael (prefab de androide) ----
            GameObject minionPrefab = BuildMinionPrefab(androidMats);
            if (minionPrefab != null)
                kaelBoss.minionPrefab = minionPrefab;

            // ---- androides militares ----
            for (int i = 0; i < androidX.Length; i++)
            {
                float x = androidX[i];
                var a = BuildHumanoid("AndroideMilitar" + i, root, androidMats, HumanStyle.Eira);
                Place(a, x, cz, 0.05f);

                var box = a.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.95f, 0f);
                box.size = new Vector3(0.7f, 1.9f, 0.55f);

                var soldier = a.AddComponent<AndroidSoldier>();
                float g = Ground(x, cz);
                soldier.patrolNodes = new[]
                {
                    new Vector3(x, g, cz - 2.6f),
                    new Vector3(x, g, cz + 2.6f),
                };
            }

            // ---- rebeldes ----
            for (int i = 0; i < rebelX.Length; i++)
            {
                float x = rebelX[i];
                var r = BuildHumanoid("Rebelde" + i, root, rebelMats, HumanStyle.Eira);
                Place(r, x, cz, 0.05f);

                var trig = r.AddComponent<SphereCollider>();
                trig.isTrigger = true;
                trig.radius = 2.2f;
                trig.center = new Vector3(0f, 1f, 0f);

                r.AddComponent<RebelAndroid>();
            }

            // ---- piezas tecnologicas ----
            for (int i = 0; i < techX.Length; i++)
            {
                var t = BuildTechPiece("PiezaTecnologica" + i, root, techX[i], cz);
            }

            // ---- archivos del Proyecto Eira ----
            for (int i = 0; i < fileX.Length; i++)
            {
                var f = BuildProjectFile("ArchivoEira" + i, root, fileX[i], cz, false);
            }

            // ---- drones ----
            for (int i = 0; i < droneX.Length; i++)
            {
                var d = BuildDrone("Dron" + i, root, droneX[i], cz);
            }

            // ---- pickups ----
            for (int i = 0; i < pickupX.Length; i++)
            {
                PickupKind kind = (i % 3 == 0) ? PickupKind.Medkit
                    : (i % 3 == 1) ? PickupKind.Energy : PickupKind.Ammo;
                var p = BuildPickupObj("Pickup" + i, root, pickupX[i], cz, kind,
                    new[] { medkit, medkitCross, medkitCross });
            }

            // ---- checkpoints ----
            for (int i = 0; i < checkpointX.Length; i++)
            {
                var c = Trigger("Checkpoint" + i, root, checkpointX[i], cz, new Vector3(7f, 3f, 7f));
                c.AddComponent<CheckpointZone>();
            }

            // ---- zonas seguras ----
            for (int i = 0; i < hideX.Length; i++)
            {
                var h = Trigger("HideSpot" + i, root, hideX[i], cz, new Vector3(5f, 3f, 5f));
                h.AddComponent<HideSpotZone>();
            }

            // ---- refugio rebelde + Nucleo Central ----
            var refuge = Trigger("RefugioRebelde", root, refugeX, cz, new Vector3(16f, 6f, 16f));
            refuge.AddComponent<RebelRefugeZone>();

            var nucleo = BuildProjectFile("NucleoCentral", root, refugeX, cz, true);
            // El Nucleo se mantiene dentro del refugio.

            // ---- reporte y guardado ----
            RootReparent(rootGo);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Log("Gameplay construido bajo '" + GameplayRoot + "'.");
            Log("Posiciones: androides=" + androidX.Length + ", rebeldes=" + rebelX.Length +
                ", piezas=" + techX.Length + ", archivos=" + fileX.Length +
                ", drones=" + droneX.Length + ", pickups=" + pickupX.Length);
            Log("Kael en x=" + kaelX.ToString("0.0") + ", refugio en x=" + refugeX.ToString("0.0"));
            Log("=== Nivel 2 guardado ===");

            WriteReport();
            AssetDatabase.SaveAssets();
            Debug.Log("[Level2Builder] Nivel 2 construido. Informe: " + ReportFile);
        }

        // ============================================================
        // ESCENA
        // ============================================================

        static Scene OpenLevel2()
        {
            Scene active = SceneManager.GetActiveScene();
            if (active.path == ScenePath)
                return active;

            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        static GameObject FindRoot(string name)
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i] != null && roots[i].name == name)
                    return roots[i];
            return null;
        }

        static void CleanScene()
        {
            GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
            var toDelete = new List<GameObject>();

            for (int i = 0; i < roots.Length; i++)
            {
                GameObject go = roots[i];
                if (go == null) continue;

                bool keep = false;
                for (int k = 0; k < Keep.Length; k++)
                {
                    if (string.Equals(go.name, Keep[k], System.StringComparison.OrdinalIgnoreCase))
                    {
                        keep = true;
                        break;
                    }
                }

                if (!keep)
                    toDelete.Add(go);
            }

            foreach (GameObject go in toDelete)
            {
                if (go == null) continue;
                Log("Eliminado de la escena: " + go.name);
                Object.DestroyImmediate(go);
            }
        }

        static void RootReparent(GameObject rootGo)
        {
            // Nada: el GameObject raiz ya es de primer nivel.
            if (rootGo.transform.parent != null)
                rootGo.transform.SetParent(null, true);
        }

        // ============================================================
        // PERSONAJES FALTANTES
        // ============================================================

        static GameObject BuildPlayerFallback()
        {
            var go = new GameObject("Eira");
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f; cc.height = 1.75f; cc.center = new Vector3(0f, 0.875f, 0f);
            cc.slopeLimit = 45f; cc.stepOffset = 0.35f; cc.skinWidth = 0.08f;

            var pc = go.AddComponent<PlayerController>();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Eira/Eira/Characters/EiraBase.fbx");
            if (model == null) model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Eira/Models/EiraBase.fbx");
            if (model != null)
            {
                var vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
                vis.name = "EiraVisual";
                vis.transform.SetParent(go.transform, false);
                vis.transform.localPosition = Vector3.zero;
                vis.transform.localRotation = Quaternion.identity;
                vis.transform.localScale = Vector3.one;
                pc.visual = vis.transform;
            }
            return go;
        }

        static GameObject BuildNovaFallback()
        {
            var go = new GameObject("NOVA");
            go.AddComponent<NovaCompanion>();
            var vis = BuildHumanoid("NovaVisual", go.transform,
                new[]
                {
                    ModelFactory.Lit(new Color(0.85f, 0.87f, 0.92f), 0.2f, 0.6f),
                    ModelFactory.Lit(new Color(0.9f, 0.92f, 0.95f), 0.1f, 0.4f),
                    ModelFactory.Lit(new Color(0.1f, 0.12f, 0.16f), 0.5f, 0.5f),
                    ModelFactory.Glow(new Color(0.2f, 0.6f, 1f)),
                }, HumanStyle.Nova);
            vis.transform.localPosition = Vector3.zero;
            return go;
        }

        static void BuildCameraFallback()
        {
            var go = new GameObject("EiraCamera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 800f;
            if (Object.FindAnyObjectByType<AudioListener>() == null)
                go.AddComponent<AudioListener>();
        }

        // ============================================================
        // CONSTRUCCION DE OBJETOS
        // ============================================================

        static GameObject BuildHumanoid(string name, Transform parent, Material[] mats, HumanStyle style)
        {
            var go = ModelFactory.BuildHumanoid(name, style, mats);
            go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject BuildTechPiece(string name, Transform parent, float x, float z)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);

            var glow = ModelFactory.Glow(new Color(0.4f, 0.85f, 1f));
            var core = ModelFactory.Prim(root.transform, PrimitiveType.Cube,
                new Vector3(0f, 0.6f, 0f), Vector3.one * 0.35f, glow);

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 0.85f, 1f);
            light.range = 4.5f;
            light.intensity = 1.4f;
            light.shadows = LightShadows.None;

            var trig = root.AddComponent<SphereCollider>();
            trig.isTrigger = true;
            trig.radius = 1.2f;
            trig.center = new Vector3(0f, 0.6f, 0f);

            root.AddComponent<TechPiece>();

            Place(root, x, z, 0.05f);
            return root;
        }

        static GameObject BuildProjectFile(string name, Transform parent, float x, float z, bool nucleo)
        {
            var go = ModelFactory.BuildDataLog(name,
                new[] { ModelFactory.Lit(new Color(0.18f, 0.2f, 0.26f), 0.7f, 0.5f) });
            go.transform.SetParent(parent, false);

            var pf = go.AddComponent<ProjectFile>();
            pf.isNucleoCentral = nucleo;
            pf.content = nucleo
                ? "Nucleo Central del Proyecto Eira: la memoria completa de lo que fue la humanidad."
                : "Archivo del Proyecto Eira: fragmentos sobre el origen de Eira y la caida del ano 3000.";

            Place(go, x, z, 0.05f);
            return go;
        }

        static GameObject BuildDrone(string name, Transform parent, float x, float z)
        {
            var go = ModelFactory.BuildDrone(name,
                new[]
                {
                    ModelFactory.Lit(new Color(0.42f, 0.45f, 0.48f), 0.72f, 0.56f),
                    ModelFactory.Lit(new Color(0.11f, 0.12f, 0.14f), 0.65f, 0.5f),
                });
            go.transform.SetParent(parent, false);

            float g = Ground(x, z);
            go.transform.position = new Vector3(x, g + 2.0f, z);

            var dc = go.AddComponent<DroneController>();
            dc.patrolNodes = new[]
            {
                new Vector3(x, g + 2.0f, z - 3f),
                new Vector3(x, g + 2.0f, z + 3f),
            };
            dc.eyeLight = go.transform.Find("eyeLight");
            dc.eyeLight2 = go.transform.Find("eyeLight2");

            return go;
        }

        static GameObject BuildPickupObj(string name, Transform parent, float x, float z,
            PickupKind kind, Material[] mats)
        {
            var go = ModelFactory.BuildPickup(name, kind, mats);
            go.transform.SetParent(parent, false);

            var p = go.AddComponent<Pickup>();
            p.kind = kind;

            Place(go, x, z, 0.05f);
            return go;
        }

        static GameObject Trigger(string name, Transform parent, float x, float z, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            float g = Ground(x, z);
            go.transform.position = new Vector3(x, g + size.y * 0.5f, z);

            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;

            return go;
        }

        static GameObject BuildMinionPrefab(Material[] androidMats)
        {
            var temp = BuildHumanoid("AndroideMinion", null, androidMats, HumanStyle.Eira);

            var box = temp.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.95f, 0f);
            box.size = new Vector3(0.7f, 1.9f, 0.55f);
            temp.AddComponent<AndroidSoldier>();

            Directory.CreateDirectory(MinionFolder);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, MinionPrefabPath);
            Object.DestroyImmediate(temp);

            Log("Prefab de refuerzo: " + MinionPrefabPath);
            return prefab;
        }

        // ============================================================
        // UTILIDADES
        // ============================================================

        static void Place(GameObject go, float x, float z, float yOffset)
        {
            if (go == null) return;
            float g = Ground(x, z);
            go.transform.position = new Vector3(x, g + yOffset, z);
        }

        static float Ground(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 200f, z), Vector3.down, out RaycastHit hit,
                400f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }
            return 0f;
        }

        static Vector3 CityCenter(GameObject env)
        {
            if (env == null)
                return Vector3.zero;

            Renderer[] rs = env.GetComponentsInChildren<Renderer>(true);
            Vector3 sum = Vector3.zero;
            int n = 0;

            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                Bounds b = rs[i].bounds;
                if (b.size.y < 8f) continue; // solo edificios
                sum += b.center;
                n++;
            }

            if (n > 0)
                return sum / n;

            // Fallback: centro de todos los renderers.
            sum = Vector3.zero; n = 0;
            for (int i = 0; i < rs.Length; i++)
            {
                if (rs[i] == null) continue;
                sum += rs[i].bounds.center;
                n++;
            }
            return n > 0 ? sum / n : env.transform.position;
        }

        static string V(Vector3 v)
        {
            return "(" + v.x.ToString("0.0") + ", " + v.y.ToString("0.0") + ", " + v.z.ToString("0.0") + ")";
        }

        static void Log(string s)
        {
            report.AppendLine(s);
        }

        static void WriteReport()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportFile));
                File.WriteAllText(ReportFile, report.ToString());
            }
            catch { }
        }
    }
}
