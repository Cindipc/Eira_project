using System.IO;
using EiraGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.Editor
{
    public static class EiraSceneBuilder
    {
        // Posición/escala de mapeo del entorno real escaneado dentro del laboratorio.
        // Ajustables visualmente: centro del pasillo en X, Y respecto al suelo, y escala.
        public static Vector3 EnvPosition = new Vector3(60f, -35f, 0f);
        public static Vector3 EnvScale = new Vector3(20f, 20f, 20f);
        public static Vector3 EnvRotation = new Vector3(0f, 90f, 0f);

        // Geometría del pasillo jugable.
        const float FloorFrom = -3f;
        const float FloorTo = 121f;
        const float WallZ = 12f;

        // Materiales persistentes del nivel.
        class Mats
        {
            public Material floor, floorDark, wall, wallDark, metal, metalDark;
            public Material jEira, sEira, dEira, jNova, sNova, dNova;
            public Material accCyan, accGreen, accRed, accWarm;
        }

        [MenuItem("Eira/Construir Todo (Intro + Nivel 1 + Splats)")]
        public static void BuildAll()
        {
            EiraPaths.EnsureFolders();
            EiraGaussian.ConfigureURPRenderers();
            EiraGaussian.ConvertSplat();
            BuildIntroScene();
            BuildLevel1Scene();
            SetBuildScenes();
            Debug.Log("Eira: ¡escenas de Intro y Nivel 1 construidas y añadidas al Build!");
        }

        [MenuItem("Eira/Construir Solo Intro")]
        public static void BuildIntroOnly()
        {
            EiraPaths.EnsureFolders();
            BuildIntroScene();
            Debug.Log("Eira: Intro reconstruida.");
        }

        [MenuItem("Eira/Construir Solo Nivel 1")]
        public static void BuildLevel1Only()
        {
            EiraPaths.EnsureFolders();
            BuildLevel1Scene();
            Debug.Log("Eira: Nivel 1 reconstruido.");
        }

        [MenuItem("Eira/Configurar URP + Convertir Splats")]
        public static void SetupUrpAndSplats()
        {
            EiraPaths.EnsureFolders();
            EiraGaussian.ConfigureURPRenderers();
            EiraGaussian.ConvertSplat();
            Debug.Log("Eira: URP y splats listos.");
        }

        // ------------------------------------------------------------------- Intro
        static void BuildIntroScene()
        {
            var scene = NewScene("IntroScene");
            CreateCamera("MainCamera");
            new GameObject("Intro", typeof(IntroFlow));
            ConfigureWeather();
            SaveScene("IntroScene", scene);
        }

        // ------------------------------------------------------------------- Nivel 1
        static void BuildLevel1Scene()
        {
            var scene = NewScene("Level1Scene");
            var mats = new Mats();
            BuildMaterials(mats);

            new GameObject("GameManager", typeof(GameManager));
            var player = BuildPlayer(new Vector3(2f, 0.2f, 0f), mats);
            BuildNova(player.transform.position + new Vector3(0f, 0f, -2.2f), mats);
            CreateCamera("MainCamera");
            ConfigureWeather();
            BuildLights();
            BuildEnvironment();
            BuildArchitecture(mats);
            BuildGameplay(mats);
            SaveScene("Level1Scene", scene);
        }

        static UnityEngine.SceneManagement.Scene NewScene(string name)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = name;
            return scene;
        }

        static void SaveScene(string name, UnityEngine.SceneManagement.Scene scene)
        {
            SaveSceneAs(scene, EiraPaths.Scenes + "/" + name + ".unity");
        }

        static void SaveSceneAs(UnityEngine.SceneManagement.Scene scene, string path)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.SaveScene(scene, path);
        }

        static void CreateCamera(string name)
        {
            var go = new GameObject(name);
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.015f, 0.03f);
            if (name == "MainCamera" && UnityEngine.Object.FindObjectOfType<FollowCamera>() == null)
            {
                go.AddComponent<AudioListener>();
                var fc = go.AddComponent<FollowCamera>();
                fc.cam = cam;
            }
            go.transform.position = new Vector3(0f, 1.3f, -3.2f);
        }

        static void ConfigureWeather()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.02f, 0.05f, 0.03f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.02f;
        }

        // ------------------------------------------------------------------- iluminación
        static void BuildLights()
        {
            var sun = new GameObject("Sun", typeof(Light));
            var dl = sun.GetComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.42f, 0.56f, 0.48f, 1f);
            dl.intensity = 0.32f;
            sun.transform.rotation = Quaternion.Euler(50f, -160f, 0f);

            PointLight("WarmSpawn", new Color(1f, 0.75f, 0.5f), 2.3f, 12f, new Vector3(4f, 2.4f, 0f));
            PointLight("CoolCorridor", new Color(0.4f, 0.6f, 1f), 1.8f, 14f, new Vector3(34f, 2.6f, 0f));
            PointLight("CyanPuzzle", new Color(0.3f, 0.8f, 1f), 2f, 12f, new Vector3(78f, 2f, 0f));
            PointLight("RedBoss", new Color(1f, 0.25f, 0.12f), 3.5f, 18f, new Vector3(100f, 2.6f, 0f));
            PointLight("GreenExit", new Color(0.35f, 1f, 0.5f), 2.4f, 12f, new Vector3(118f, 2f, 0f));
        }

        static void PointLight(string name, Color c, float intensity, float range, Vector3 pos)
        {
            var go = new GameObject(name, typeof(Light));
            var l = go.GetComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = range;
            go.transform.position = pos;
        }

        // ------------------------------------------------------------------- entorno splat
        static void BuildEnvironment()
        {
            if (!EiraGaussian.IsPackageAvailable)
            {
                Debug.LogWarning("EiraSceneBuilder: sin paquete de Gaussian Splatting; el nivel se genera sin entorno real.");
                return;
            }
            var env = EiraGaussian.CreateEnvironmentObject(EnvPosition, EnvScale);
            if (env == null) return;
            env.transform.rotation = Quaternion.Euler(EnvRotation);
        }

        // ------------------------------------------------------------------- materiales
        static void BuildMaterials(Mats m)
        {
            m.floor = ModelFactory.Lit(new Color(0.09f, 0.17f, 0.11f), 0.4f, 0.5f);
            m.floorDark = ModelFactory.Lit(new Color(0.05f, 0.10f, 0.07f), 0.4f, 0.4f);
            m.wall = ModelFactory.Lit(new Color(0.13f, 0.21f, 0.15f), 0.35f, 0.4f);
            m.wallDark = ModelFactory.Lit(new Color(0.07f, 0.12f, 0.09f), 0.35f, 0.35f);
            m.metal = ModelFactory.Lit(new Color(0.5f, 0.52f, 0.56f), 0.7f, 0.55f);
            m.metalDark = ModelFactory.Lit(new Color(0.16f, 0.17f, 0.19f), 0.6f, 0.5f);
            m.jEira = ModelFactory.Lit(new Color(0.38f, 0.2f, 0.11f), 0f, 0.3f);
            m.sEira = ModelFactory.Lit(new Color(0.88f, 0.66f, 0.48f), 0f, 0.4f);
            m.dEira = ModelFactory.Lit(new Color(0.12f, 0.12f, 0.14f), 0.2f, 0.3f);
            m.jNova = ModelFactory.Lit(new Color(0.85f, 0.87f, 0.92f), 0.2f, 0.6f);
            m.sNova = ModelFactory.Lit(new Color(0.9f, 0.92f, 0.95f), 0.1f, 0.4f);
            m.dNova = ModelFactory.Lit(new Color(0.15f, 0.16f, 0.18f), 0.3f, 0.5f);
            m.accCyan = ModelFactory.Glow(new Color(0.15f, 0.75f, 0.9f));
            m.accGreen = ModelFactory.Glow(new Color(0.3f, 0.9f, 0.5f));
            m.accRed = ModelFactory.Glow(new Color(0.95f, 0.25f, 0.15f));
            m.accWarm = ModelFactory.Glow(new Color(1f, 0.7f, 0.3f));
        }

        // ------------------------------------------------------------------- personajes
        static PlayerController BuildPlayer(Vector3 pos, Mats m)
        {
            var go = new GameObject("Eira");
            go.transform.position = pos;
            var cc = go.AddComponent<CharacterController>();
            cc.radius = 0.4f;
            cc.height = 1.75f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.08f;
            var pc = go.AddComponent<PlayerController>();
            var vis = ModelFactory.BuildHumanoid("EiraVisual", HumanStyle.Eira, new[] { m.jEira, m.sEira, m.dEira });
            vis.transform.SetParent(go.transform, false);
            pc.visual = vis.transform;
            return pc;
        }

        static NovaCompanion BuildNova(Vector3 pos, Mats m)
        {
            var go = new GameObject("NOVA");
            go.transform.position = pos;
            var comp = go.AddComponent<NovaCompanion>();
            var vis = ModelFactory.BuildHumanoid("NOVAVisual", HumanStyle.Nova, new[] { m.jNova, m.sNova, m.dNova });
            vis.transform.SetParent(go.transform, false);
            return comp;
        }

        // ------------------------------------------------------------------- escenografía
        static void BuildArchitecture(Mats m)
        {
            var root = new GameObject("Escenografia");

            // suelo: dos tramos (con hueco para el foso en x57..61)
            FloorRun(root.transform, FloorFrom, 57f, 26f, m.floor);
            FloorRun(root.transform, 61f, FloorTo, 26f, m.floor);

            // parapetos laterales (contienen al jugador; mide ~1.4 de alto)
            Block(root.transform, new Vector3((FloorFrom + FloorTo) / 2f, 0.7f, WallZ + 0.25f), new Vector3(FloorTo - FloorFrom, 1.4f, 0.5f), m.wall);
            Block(root.transform, new Vector3((FloorFrom + FloorTo) / 2f, 0.7f, -WallZ - 0.25f), new Vector3(FloorTo - FloorFrom, 1.4f, 0.5f), m.wall);

            // muro posterior y muro de salida
            Block(root.transform, new Vector3(FloorFrom - 0.5f, 2.5f, 0f), new Vector3(1f, 5f, 27f), m.metalDark);
            Block(root.transform, new Vector3(FloorTo + 0.5f, 2.5f, 0f), new Vector3(1f, 5f, 27f), m.metalDark);

            // columnas decorativas / soportes cada ~12 m (visual)
            for (float xp = 4f; xp < 118f; xp += 12f)
            {
                DecorativePillar(root.transform, new Vector3(xp, 2.4f, WallZ - 1.2f), m.wallDark);
                DecorativePillar(root.transform, new Vector3(xp, 2.4f, -WallZ + 1.2f), m.wallDark);
            }

            // foso central (x57..61): abajo solo un tope lejano para no caer infinito
            Block(root.transform, new Vector3(59f, -8f, 0f), new Vector3(3.8f, 1f, 22f), m.floorDark);
            Block(root.transform, new Vector3(57f, 0.05f, 0f), new Vector3(0.12f, 0.1f, 22f), m.accRed);
            Block(root.transform, new Vector3(61f, 0.05f, 0f), new Vector3(0.12f, 0.1f, 22f), m.accRed);
            // puentes sobre el foso
            Block(root.transform, new Vector3(58.2f, 0.7f, 4f), new Vector3(2f, 0.2f, 1.6f), m.wall);
            Block(root.transform, new Vector3(59.8f, 0.7f, -4f), new Vector3(2f, 0.2f, 1.6f), m.wall);

            // decoración: maquinaria/chatarra como cobertura
            DecorativeBlock(root.transform, new Vector3(11f, 0f, 5f), new Vector3(2.2f, 1.6f, 1.8f), m.metalDark);
            DecorativeBlock(root.transform, new Vector3(14f, 0f, -5f), new Vector3(1.6f, 0.9f, 1.6f), m.metalDark);
            DecorativeBlock(root.transform, new Vector3(44f, 0f, 5f), new Vector3(2f, 1.2f, 2f), m.metalDark);
            DecorativeBlock(root.transform, new Vector3(64f, 0f, -5f), new Vector3(1.8f, 1.4f, 1.8f), m.metalDark);
            DecorativeBlock(root.transform, new Vector3(90f, 0f, 5f), new Vector3(2.4f, 1.1f, 2f), m.metalDark);
            DecorativeBlock(root.transform, new Vector3(112f, 0f, -5f), new Vector3(2f, 1f, 2f), m.metalDark);
        }

        static void FloorRun(Transform parent, float from, float to, float width, Material mat)
        {
            for (float x0 = from; x0 < to; x0 += 20f)
            {
                float w = Mathf.Min(20f, to - x0);
                if (w <= 0.01f) break;
                Block(parent, new Vector3(x0 + w / 2f, -0.5f, 0f), new Vector3(w + 0.02f, 1f, width), mat);
            }
        }

        static void DecorativeBlock(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            Block(parent, pos, size, mat);
        }

        static void DecorativePillar(Transform parent, Vector3 pos, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(2.2f, 4.8f, 2.2f);
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
        }

        // Cubo con collider (muro/suelo).
        static GameObject Block(Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = mat;
            return go;
        }

        // ------------------------------------------------------------------- gameplay
        static void BuildGameplay(Mats m)
        {
            var root = new GameObject("Gameplay");

            // ---- Zona A · Despertar ----
            CheckpointZoneAt(root.transform, new Vector3(4f, 0f, 0f), new Vector3(6f, 4f, 8f));
            DataLogAt(root.transform, new Vector3(8f, 0.9f, 2f), 0, "Registro 01 — El Gran Vacío\n\"Tras la Gran Migración del 2440, la humanidad abandonó la Tierra. Solo quedamos las máquinas... y los que despiertan.\"");
            PickupAt(root.transform, new Vector3(12f, 0.9f, -3f), PickupKind.Medkit, 45f);

            // ---- Zona B · Corredor (drones + cobertura) ----
            CheckpointZoneAt(root.transform, new Vector3(30f, 0f, 0f), new Vector3(5f, 4f, 8f));
            DroneAt(root.transform, new Vector3(33f, 2.5f, 5f), new Vector3[] { new Vector3(32f, 2.5f, 5f), new Vector3(40f, 2.5f, 5f) }, m);
            DroneAt(root.transform, new Vector3(33f, 2.5f, -5f), new Vector3[] { new Vector3(32f, 2.5f, -5f), new Vector3(40f, 2.5f, -5f) }, m);
            DataLogAt(root.transform, new Vector3(32.5f, 0.9f, -3f), 1, "Registro 02 — Año 3000\n\"Los últimos humanos conocidos desaparecieron en el 2561. Este laboratorio vive en silencio desde entonces.\"");
            PickupAt(root.transform, new Vector3(35f, 0.9f, 3f), PickupKind.Medkit, 50f);
            PickupAt(root.transform, new Vector3(39f, 0.9f, -3f), PickupKind.Energy, 40f);
            var hide = new GameObject("HideSpot", typeof(BoxCollider), typeof(HideSpotZone));
            hide.transform.SetParent(root.transform, false);
            hide.transform.position = new Vector3(36f, 0f, 0f);
            hide.GetComponent<BoxCollider>().isTrigger = true;
            hide.GetComponent<BoxCollider>().size = new Vector3(8f, 4f, 8f);

            // ---- Zona C · Información (foso) ----
            CheckpointZoneAt(root.transform, new Vector3(46f, 0f, 0f), new Vector3(5f, 4f, 8f));
            DataLogAt(root.transform, new Vector3(52f, 0.9f, 3f), 2, "Registro 03 — La instalación\n\"El Laboratorio Núcleo lleva activo desde el 2600. La Máquina Guardiana protege la salida. Nadie la ha desafiado.\"");
            DataLogAt(root.transform, new Vector3(54.5f, 0.9f, -3f), 3, "Registro 04 — La salida\n\"El plano indica: SALIDA al Lado Este. Desactiva la Guardiana cargando 3 terminales con energía genética.\"");
            DroneAt(root.transform, new Vector3(59f, 2.2f, -8f), new Vector3[] { new Vector3(55f, 2.2f, -8f), new Vector3(63f, 2.2f, -8f) }, m);
            PickupAt(root.transform, new Vector3(50f, 0.9f, 5f), PickupKind.Energy, 45f);

            // ---- Zona D · Acertijo (consolas -> puerta) ----
            CheckpointZoneAt(root.transform, new Vector3(74f, 0f, 0f), new Vector3(5f, 4f, 8f));
            var door = SlidingDoorAt(root.transform, new Vector3(84.5f, 0f, 0f), m, 2);
            PillarFlank(root.transform, 84.5f, m.metalDark);
            PanelAt(root.transform, new Vector3(76f, 0f, -3.5f), PanelKind.PuzzlePanel, door, null, 1, m);
            PanelAt(root.transform, new Vector3(80f, 0f, 3.5f), PanelKind.PuzzlePanel, door, null, 1, m);
            PickupAt(root.transform, new Vector3(78f, 0.9f, 3f), PickupKind.Medkit, 50f);

            // ---- Zona E · Jefe ----
            CheckpointZoneAt(root.transform, new Vector3(89f, 0f, 0f), new Vector3(6f, 4f, 10f));
            var exitDoor = SlidingDoorAt(root.transform, new Vector3(116f, 0f, 0f), m, 2);
            PillarFlank(root.transform, 116f, m.metalDark);
            var guardian = BuildGuardian(root.transform, new Vector3(100f, 0f, 0f), m);
            guardian.exitDoor = exitDoor;
            BossTerminalAt(root.transform, new Vector3(92f, 0f, 6.5f), guardian, m);
            BossTerminalAt(root.transform, new Vector3(92f, 0f, -6.5f), guardian, m);
            BossTerminalAt(root.transform, new Vector3(107f, 0f, 6.5f), guardian, m);
            PickupAt(root.transform, new Vector3(94f, 0.9f, 3f), PickupKind.Energy, 30f);

            // ---- Zona F · Salida ----
            var exit = new GameObject("LevelExit", typeof(BoxCollider), typeof(LevelExitZone));
            exit.transform.SetParent(root.transform, false);
            exit.transform.position = new Vector3(118.5f, 0f, 0f);
            exit.GetComponent<BoxCollider>().isTrigger = true;
            exit.GetComponent<BoxCollider>().size = new Vector3(3f, 4f, 6f);
        }

        static void CheckpointZoneAt(Transform parent, Vector3 pos, Vector3 size)
        {
            var go = new GameObject("Checkpoint", typeof(BoxCollider), typeof(CheckpointZone));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var bc = go.GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.size = size;
        }

        static void DataLogAt(Transform parent, Vector3 pos, int id, string content)
        {
            var go = ModelFactory.BuildDataLog("DataLog" + id, new Material[] { ModelFactory.Lit(new Color(0.55f, 0.6f, 0.7f), 0.5f, 0.5f) });
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var dl = go.AddComponent<DataLog>();
            dl.logId = id;
            dl.content = content;
        }

        static void PickupAt(Transform parent, Vector3 pos, PickupKind kind, float amount)
        {
            var mats = new Material[] { ModelFactory.Lit(new Color(0.9f, 0.35f, 0.2f), 0.2f, 0.5f), ModelFactory.Lit(Color.white, 0, 0.4f), ModelFactory.Lit(Color.white, 0, 0.5f) };
            var go = ModelFactory.BuildPickup(kind == PickupKind.Medkit ? "Botiquin" : "CapsulaEnergia", kind, mats);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var pk = go.AddComponent<Pickup>();
            pk.kind = kind;
            pk.amount = amount;
        }

        static DoorControl SlidingDoorAt(Transform parent, Vector3 pos, Mats m, int needed)
        {
            var go = ModelFactory.BuildSlidingDoor("DoorLab", new Material[] { m.metalDark, m.metal });
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var dc = go.GetComponent<DoorControl>();
            dc.neededActivations = needed;
            return dc;
        }

        static void PillarFlank(Transform parent, float x, Material mat)
        {
            Block(parent, new Vector3(x, 1.6f, 7.1f), new Vector3(0.5f, 3.2f, 9.6f), mat);
            Block(parent, new Vector3(x, 1.6f, -7.1f), new Vector3(0.5f, 3.2f, 9.6f), mat);
        }

        static void PanelAt(Transform parent, Vector3 pos, PanelKind kind, DoorControl door, GuardianBoss boss, int charges, Mats m)
        {
            var go = ModelFactory.BuildPanel(kind == PanelKind.BossTerminal ? "TerminalBoss" : "ConsolaPuzzle", new Material[] { m.metal, m.metalDark });
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var pa = go.AddComponent<PanelActivator>();
            pa.kind = kind;
            pa.linkedDoor = door;
            pa.boss = boss;
            pa.neededCharges = charges;
            pa.glowRenderer = go.transform.Find("Glow")?.GetComponent<Renderer>();
        }

        static void BossTerminalAt(Transform parent, Vector3 pos, GuardianBoss boss, Mats m)
        {
            PanelAt(parent, pos, PanelKind.BossTerminal, null, boss, 1, m);
        }

        static void DroneAt(Transform parent, Vector3 pos, Vector3[] patrol, Mats m)
        {
            var go = ModelFactory.BuildDrone("Dron", new Material[] { m.metal, m.metalDark });
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var dc = go.AddComponent<DroneController>();
            dc.patrolNodes = patrol;
            dc.eyeLight = go.transform.Find("eyeLight");
            dc.eyeLight2 = go.transform.Find("eyeLight2");
        }

        static GuardianBoss BuildGuardian(Transform parent, Vector3 pos, Mats m)
        {
            var go = ModelFactory.BuildGuardian("MáquinaGuardiana", new Material[] { m.metal, m.metalDark });
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var solid = go.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 1.35f, 0f);
            solid.size = new Vector3(2.3f, 2.9f, 1.2f);

            var boss = go.AddComponent<GuardianBoss>();
            var trigGo = new GameObject("BossActivateTrigger", typeof(BoxCollider), typeof(BossTriggerForward));
            trigGo.transform.SetParent(go.transform, false);
            trigGo.transform.localPosition = new Vector3(0f, 1f, 0f);
            var tb = trigGo.GetComponent<BoxCollider>();
            tb.isTrigger = true;
            tb.size = new Vector3(5f, 3f, 6f);
            trigGo.GetComponent<BossTriggerForward>().boss = boss;

            boss.core = go.transform.Find("core");
            boss.eye = go.transform.Find("eye");
            boss.redLight = go.transform.Find("redLight")?.GetComponent<Light>();
            return boss;
        }

        static void SetBuildScenes()
        {
            var list = new[]
            {
                EiraPaths.Scenes + "/IntroScene.unity",
                EiraPaths.Scenes + "/Level1Scene.unity",
            };
            var scenes = new EditorBuildSettingsScene[list.Length];
            for (int i = 0; i < list.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(list[i], true);
            EditorBuildSettings.scenes = scenes;
        }
    }
}