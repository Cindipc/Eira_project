
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
        // ================================================================
        // ENTORNO ESCANEADO
        // ================================================================

        public static Vector3 EnvPosition = new Vector3(60f, -35f, 0f);
        public static Vector3 EnvScale = new Vector3(20f, 20f, 20f);
        public static Vector3 EnvRotation = new Vector3(0f, 90f, 0f);

        // ================================================================
        // GEOMETRÍA PRINCIPAL
        // ================================================================

        const float FloorFrom = -3f;
        const float FloorTo = 121f;
        const float WallZ = 12f;

        // Alturas estructurales
        const float WallHeight = 4.8f;
        const float RoofHeight = 6.2f;

        // ================================================================
        // MATERIALES
        // ================================================================

        class Mats
        {
            public Material floor;
            public Material floorDark;

            public Material wall;
            public Material wallDark;

            public Material metal;
            public Material metalDark;

            public Material jEira;
            public Material sEira;
            public Material dEira;

            public Material jNova;
            public Material sNova;
            public Material dNova;

            public Material accCyan;
            public Material accGreen;
            public Material accRed;
            public Material accWarm;
        }

        // ================================================================
        // MENÚ UNITY
        // ================================================================

        [MenuItem("Eira/Construir Todo (Intro + Nivel 1 + Splats)")]
        public static void BuildAll()
        {
            EiraPaths.EnsureFolders();

            EiraGaussian.ConfigureURPRenderers();
            EiraGaussian.ConvertSplat();

            BuildIntroScene();
            BuildLevel1Scene();
            SetBuildScenes();

            Debug.Log(
                "Eira: ¡escenas de Intro y Nivel 1 construidas y añadidas al Build!"
            );
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

        // ================================================================
        // INTRO
        // ================================================================

        static void BuildIntroScene()
        {
            var scene = NewScene("IntroScene");

            CreateCamera("MainCamera");

            new GameObject(
                "Intro",
                typeof(IntroFlow)
            );

            ConfigureWeather();

            SaveScene("IntroScene", scene);
        }

        // ================================================================
        // NIVEL 1
        // ================================================================

        static void BuildLevel1Scene()
        {
            var scene = NewScene("Level1Scene");

            var mats = new Mats();

            BuildMaterials(mats);

            new GameObject(
                "GameManager",
                typeof(GameManager)
            );

            var player =
                BuildPlayer(
                    new Vector3(2f, 0.2f, 0f),
                    mats
                );

            BuildNova(
                player.transform.position +
                new Vector3(0f, 0f, -2.2f),
                mats
            );

            // IMPORTANTE:
            // Aquí NO agregamos ProfessionalThirdPersonCamera.
            // La cámara queda independiente para poder probar
            // solamente la nueva escenografía.

            CreateCamera("MainCamera");

            ConfigureWeather();

            BuildLights();

            BuildEnvironment();

            BuildArchitecture(mats);

            BuildGameplay(mats);

            SaveScene(
                "Level1Scene",
                scene
            );
        }

        // ================================================================
        // ESCENAS
        // ================================================================

        static Scene NewScene(string name)
        {
            var scene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single
                );

            scene.name = name;

            return scene;
        }

        static void SaveScene(
            string name,
            Scene scene
        )
        {
            SaveSceneAs(
                scene,
                EiraPaths.Scenes +
                "/" +
                name +
                ".unity"
            );
        }

        static void SaveSceneAs(
            Scene scene,
            string path
        )
        {
            string directory =
                Path.GetDirectoryName(path);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            EditorSceneManager.SaveScene(
                scene,
                path
            );
        }

        // ================================================================
        // CÁMARA BÁSICA
        // ================================================================

        static void CreateCamera(string name)
        {
            // Eliminar cámaras anteriores para evitar duplicados.
            Camera[] cameras =
                Object.FindObjectsOfType<Camera>();

            foreach (Camera existing in cameras)
            {
                if (existing != null)
                    Object.DestroyImmediate(
                        existing.gameObject
                    );
            }

            var go =
                new GameObject(name);

            go.tag = "MainCamera";

            var cam =
                go.AddComponent<Camera>();

            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 500f;

            cam.clearFlags =
                CameraClearFlags.SolidColor;

            cam.backgroundColor =
                new Color(
                    0.01f,
                    0.015f,
                    0.03f
                );

            if (
                Object.FindObjectOfType<AudioListener>() ==
                null
            )
            {
                go.AddComponent<AudioListener>();
            }

            // Posición temporal de prueba.
            // No se añade ningún script de cámara.
            go.transform.position =
                new Vector3(
                    2f,
                    2.2f,
                    -7f
                );

            go.transform.rotation =
                Quaternion.Euler(
                    12f,
                    0f,
                    0f
                );
        }

        // ================================================================
        // CLIMA
        // ================================================================

        static void ConfigureWeather()
        {
            RenderSettings.fog = true;

            RenderSettings.fogColor =
                new Color(
                    0.018f,
                    0.045f,
                    0.032f
                );

            RenderSettings.fogMode =
                FogMode.Exponential;

            RenderSettings.fogDensity =
                0.018f;
        }

        // ================================================================
        // ILUMINACIÓN
        // ================================================================

        static void BuildLights()
        {
            var sun =
                new GameObject(
                    "Sun",
                    typeof(Light)
                );

            var dl =
                sun.GetComponent<Light>();

            dl.type =
                LightType.Directional;

            dl.color =
                new Color(
                    0.42f,
                    0.56f,
                    0.48f,
                    1f
                );

            dl.intensity = 0.32f;

            sun.transform.rotation =
                Quaternion.Euler(
                    50f,
                    -160f,
                    0f
                );

            // ------------------------------------------------------------
            // ZONA A
            // ------------------------------------------------------------

            PointLight(
                "WarmSpawn",
                new Color(
                    1f,
                    0.72f,
                    0.45f
                ),
                2.3f,
                12f,
                new Vector3(
                    4f,
                    2.4f,
                    0f
                )
            );

            // ------------------------------------------------------------
            // ZONA B
            // ------------------------------------------------------------

            PointLight(
                "CoolCorridor",
                new Color(
                    0.35f,
                    0.55f,
                    1f
                ),
                1.8f,
                15f,
                new Vector3(
                    34f,
                    2.8f,
                    0f
                )
            );

            // ------------------------------------------------------------
            // ZONA D
            // ------------------------------------------------------------

            PointLight(
                "CyanPuzzle",
                new Color(
                    0.2f,
                    0.8f,
                    1f
                ),
                2.2f,
                14f,
                new Vector3(
                    78f,
                    2.4f,
                    0f
                )
            );

            // ------------------------------------------------------------
            // JEFE
            // ------------------------------------------------------------

            PointLight(
                "RedBoss",
                new Color(
                    1f,
                    0.2f,
                    0.08f
                ),
                3.5f,
                18f,
                new Vector3(
                    100f,
                    2.8f,
                    0f
                )
            );

            // ------------------------------------------------------------
            // SALIDA
            // ------------------------------------------------------------

            PointLight(
                "GreenExit",
                new Color(
                    0.25f,
                    1f,
                    0.45f
                ),
                2.4f,
                14f,
                new Vector3(
                    118f,
                    2.4f,
                    0f
                )
            );

            // Luces estructurales adicionales.
            PointLight(
                "ZoneA_Ceiling",
                new Color(
                    0.85f,
                    0.95f,
                    0.8f
                ),
                0.9f,
                10f,
                new Vector3(
                    18f,
                    4.6f,
                    0f
                )
            );

            PointLight(
                "ZoneB_Ceiling",
                new Color(
                    0.45f,
                    0.65f,
                    1f
                ),
                0.8f,
                10f,
                new Vector3(
                    45f,
                    4.6f,
                    0f
                )
            );

            PointLight(
                "ZoneD_Ceiling",
                new Color(
                    0.25f,
                    0.85f,
                    1f
                ),
                0.9f,
                10f,
                new Vector3(
                    72f,
                    4.6f,
                    0f
                )
            );

            PointLight(
                "ZoneE_Ceiling",
                new Color(
                    1f,
                    0.3f,
                    0.15f
                ),
                1.0f,
                12f,
                new Vector3(
                    108f,
                    4.8f,
                    0f
                )
            );
        }

        static void PointLight(
            string name,
            Color c,
            float intensity,
            float range,
            Vector3 pos
        )
        {
            var go =
                new GameObject(
                    name,
                    typeof(Light)
                );

            var l =
                go.GetComponent<Light>();

            l.type =
                LightType.Point;

            l.color = c;
            l.intensity = intensity;
            l.range = range;

            go.transform.position = pos;
        }

        // ================================================================
        // ENTORNO SPLAT
        // ================================================================

        static void BuildEnvironment()
        {
            if (!EiraGaussian.IsPackageAvailable)
            {
                Debug.LogWarning(
                    "EiraSceneBuilder: sin paquete de Gaussian Splatting; " +
                    "el nivel se genera sin entorno real."
                );

                return;
            }

            var env =
                EiraGaussian.CreateEnvironmentObject(
                    EnvPosition,
                    EnvScale
                );

            if (env == null)
                return;

            env.transform.rotation =
                Quaternion.Euler(
                    EnvRotation
                );
        }

        // ================================================================
        // MATERIALES
        // ================================================================

        static void BuildMaterials(Mats m)
        {
            m.floor =
                ModelFactory.Lit(
                    new Color(
                        0.075f,
                        0.13f,
                        0.095f
                    ),
                    0.42f,
                    0.48f
                );

            m.floorDark =
                ModelFactory.Lit(
                    new Color(
                        0.035f,
                        0.065f,
                        0.05f
                    ),
                    0.4f,
                    0.42f
                );

            m.wall =
                ModelFactory.Lit(
                    new Color(
                        0.12f,
                        0.19f,
                        0.145f
                    ),
                    0.35f,
                    0.42f
                );

            m.wallDark =
                ModelFactory.Lit(
                    new Color(
                        0.055f,
                        0.085f,
                        0.07f
                    ),
                    0.38f,
                    0.38f
                );

            m.metal =
                ModelFactory.Lit(
                    new Color(
                        0.42f,
                        0.45f,
                        0.48f
                    ),
                    0.72f,
                    0.56f
                );

            m.metalDark =
                ModelFactory.Lit(
                    new Color(
                        0.105f,
                        0.115f,
                        0.13f
                    ),
                    0.65f,
                    0.5f
                );

            m.jEira =
                ModelFactory.Lit(
                    new Color(
                        0.38f,
                        0.2f,
                        0.11f
                    ),
                    0f,
                    0.3f
                );

            m.sEira =
                ModelFactory.Lit(
                    new Color(
                        0.88f,
                        0.66f,
                        0.48f
                    ),
                    0f,
                    0.4f
                );

            m.dEira =
                ModelFactory.Lit(
                    new Color(
                        0.12f,
                        0.12f,
                        0.14f
                    ),
                    0.2f,
                    0.3f
                );

            m.jNova =
                ModelFactory.Lit(
                    new Color(
                        0.85f,
                        0.87f,
                        0.92f
                    ),
                    0.2f,
                    0.6f
                );

            m.sNova =
                ModelFactory.Lit(
                    new Color(
                        0.9f,
                        0.92f,
                        0.95f
                    ),
                    0.1f,
                    0.4f
                );

            m.dNova =
                ModelFactory.Lit(
                    new Color(
                        0.15f,
                        0.16f,
                        0.18f
                    ),
                    0.3f,
                    0.5f
                );

            m.accCyan =
                ModelFactory.Glow(
                    new Color(
                        0.15f,
                        0.75f,
                        0.9f
                    )
                );

            m.accGreen =
                ModelFactory.Glow(
                    new Color(
                        0.3f,
                        0.9f,
                        0.5f
                    )
                );

            m.accRed =
                ModelFactory.Glow(
                    new Color(
                        0.95f,
                        0.25f,
                        0.15f
                    )
                );

            m.accWarm =
                ModelFactory.Glow(
                    new Color(
                        1f,
                        0.7f,
                        0.3f
                    )
                );
        }

        // ================================================================
        // EIRA
        // ================================================================

        static PlayerController BuildPlayer(
            Vector3 pos,
            Mats m
        )
        {
            var go =
                new GameObject("Eira");

            go.transform.position = pos;

            var cc =
                go.AddComponent<CharacterController>();

            cc.radius = 0.4f;
            cc.height = 1.75f;

            cc.center =
                new Vector3(
                    0f,
                    0.875f,
                    0f
                );

            cc.slopeLimit = 45f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.08f;

            var pc =
                go.AddComponent<PlayerController>();

            const string modelPath =
                "Assets/Eira/Characters/Eira_Base.fbx";

            var eiraModel =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    modelPath
                );

            if (eiraModel == null)
            {
                Debug.LogError(
                    "EiraSceneBuilder: no se encontró " +
                    "Eira_Base.fbx en " +
                    modelPath
                );

                return pc;
            }

            var visual =
                (GameObject)
                PrefabUtility.InstantiatePrefab(
                    eiraModel
                );

            if (visual == null)
            {
                Debug.LogError(
                    "EiraSceneBuilder: no se pudo " +
                    "instanciar Eira_Base.fbx."
                );

                return pc;
            }

            visual.name = "EiraVisual";

            visual.transform.SetParent(
                go.transform,
                false
            );

            visual.transform.localPosition =
                Vector3.zero;

            visual.transform.localRotation =
                Quaternion.identity;

            visual.transform.localScale =
                Vector3.one;

            pc.visual =
                visual.transform;

            return pc;
        }

        // ================================================================
        // NOVA
        // ================================================================

        static NovaCompanion BuildNova(
            Vector3 pos,
            Mats m
        )
        {
            var go =
                new GameObject("NOVA");

            go.transform.position = pos;

            var comp =
                go.AddComponent<NovaCompanion>();

            var vis =
                ModelFactory.BuildHumanoid(
                    "NOVAVisual",
                    HumanStyle.Nova,
                    new[]
                    {
                        m.jNova,
                        m.sNova,
                        m.dNova
                    }
                );

            vis.transform.SetParent(
                go.transform,
                false
            );

            return comp;
        }

        // ================================================================
        // ARQUITECTURA PRINCIPAL
        // ================================================================

        static void BuildArchitecture(Mats m)
        {
            var root =
                new GameObject(
                    "Escenografia"
                );

            // ============================================================
            // 1. PISO PRINCIPAL
            // ============================================================

            FloorRun(
                root.transform,
                FloorFrom,
                57f,
                26f,
                m.floor
            );

            FloorRun(
                root.transform,
                61f,
                FloorTo,
                26f,
                m.floor
            );

            // ============================================================
            // 2. BORDES LATERALES ESTRUCTURALES
            // ============================================================
            //
            // Ya no son pequeños parapetos de 1.4 m.
            // Son muros reales de laboratorio.
            //
            // Tienen collider porque Block() utiliza CubeCollider.
            // ============================================================

            BuildSideWall(
                root.transform,
                WallZ,
                m.wall
            );

            BuildSideWall(
                root.transform,
                -WallZ,
                m.wall
            );

            // ============================================================
            // 3. MUROS FRONTAL Y TRASERO
            // ============================================================

            Block(
                root.transform,
                new Vector3(
                    FloorFrom - 0.5f,
                    WallHeight / 2f,
                    0f
                ),
                new Vector3(
                    1f,
                    WallHeight,
                    27f
                ),
                m.metalDark
            );

            Block(
                root.transform,
                new Vector3(
                    FloorTo + 0.5f,
                    WallHeight / 2f,
                    0f
                ),
                new Vector3(
                    1f,
                    WallHeight,
                    27f
                ),
                m.metalDark
            );

            // ============================================================
            // 4. CIMENTACIÓN / ZÓCALOS
            // ============================================================

            BuildWallBase(
                root.transform,
                WallZ,
                m.metalDark
            );

            BuildWallBase(
                root.transform,
                -WallZ,
                m.metalDark
            );

            // ============================================================
            // 5. COLUMNAS ESTRUCTURALES
            // ============================================================

            for (float x = 4f; x < 118f; x += 12f)
            {
                BuildStructuralColumn(
                    root.transform,
                    new Vector3(
                        x,
                        2.4f,
                        10.7f
                    ),
                    m.metalDark
                );

                BuildStructuralColumn(
                    root.transform,
                    new Vector3(
                        x,
                        2.4f,
                        -10.7f
                    ),
                    m.metalDark
                );
            }

            // ============================================================
            // 6. VIGAS SUPERIORES
            // ============================================================

            BuildRoofStructure(
                root.transform,
                m
            );

            // ============================================================
            // 7. TUBERÍAS LATERALES
            // ============================================================

            BuildPipeNetwork(
                root.transform,
                m
            );

            // ============================================================
            // 8. ZONA A — ÁREA DE DESPERTAR
            // ============================================================

            BuildZoneA(
                root.transform,
                m
            );

            // ============================================================
            // 9. ZONA B — CORREDOR INDUSTRIAL
            // ============================================================

            BuildZoneB(
                root.transform,
                m
            );

            // ============================================================
            // 10. FOSO CENTRAL
            // ============================================================

            BuildCentralPit(
                root.transform,
                m
            );

            // ============================================================
            // 11. ZONA D — PUZZLE
            // ============================================================

            BuildPuzzleArchitecture(
                root.transform,
                m
            );

            // ============================================================
            // 12. ZONA E — ARENA DEL JEFE
            // ============================================================

            BuildBossArchitecture(
                root.transform,
                m
            );

            // ============================================================
            // 13. ZONA F — SALIDA
            // ============================================================

            BuildExitArchitecture(
                root.transform,
                m
            );
        }

        // ================================================================
        // MUROS LATERALES
        // ================================================================

        static void BuildSideWall(
            Transform parent,
            float z,
            Material mat
        )
        {
            Block(
                parent,
                new Vector3(
                    59f,
                    2.4f,
                    z
                ),
                new Vector3(
                    124f,
                    4.8f,
                    0.6f
                ),
                mat
            );

            // Banda metálica superior.
            Block(
                parent,
                new Vector3(
                    59f,
                    4.55f,
                    z - Mathf.Sign(z) * 0.15f
                ),
                new Vector3(
                    124f,
                    0.25f,
                    0.8f
                ),
                mat
            );
        }

        // ================================================================
        // BASES DE MURO
        // ================================================================

        static void BuildWallBase(
            Transform parent,
            float z,
            Material mat
        )
        {
            Block(
                parent,
                new Vector3(
                    59f,
                    0.18f,
                    z - Mathf.Sign(z) * 0.05f
                ),
                new Vector3(
                    124f,
                    0.36f,
                    0.9f
                ),
                mat
            );
        }

        // ================================================================
        // COLUMNA ESTRUCTURAL
        // ================================================================

        static void BuildStructuralColumn(
            Transform parent,
            Vector3 pos,
            Material mat
        )
        {
            // Columna vertical.
            Block(
                parent,
                pos,
                new Vector3(
                    1.2f,
                    4.8f,
                    1.2f
                ),
                mat
            );

            // Base.
            Block(
                parent,
                new Vector3(
                    pos.x,
                    0.22f,
                    pos.z
                ),
                new Vector3(
                    1.8f,
                    0.44f,
                    1.8f
                ),
                mat
            );

            // Cabezal.
            Block(
                parent,
                new Vector3(
                    pos.x,
                    4.55f,
                    pos.z
                ),
                new Vector3(
                    1.8f,
                    0.35f,
                    1.8f
                ),
                mat
            );
        }

        // ================================================================
        // ESTRUCTURA SUPERIOR
        // ================================================================

        static void BuildRoofStructure(
            Transform parent,
            Mats m
        )
        {
            // Vigas longitudinales.
            Block(
                parent,
                new Vector3(
                    59f,
                    RoofHeight,
                    9.5f
                ),
                new Vector3(
                    124f,
                    0.45f,
                    0.8f
                ),
                m.metalDark
            );

            Block(
                parent,
                new Vector3(
                    59f,
                    RoofHeight,
                    -9.5f
                ),
                new Vector3(
                    124f,
                    0.45f,
                    0.8f
                ),
                m.metalDark
            );

            // Vigas transversales.
            for (float x = 6f; x < 118f; x += 12f)
            {
                Block(
                    parent,
                    new Vector3(
                        x,
                        RoofHeight,
                        0f
                    ),
                    new Vector3(
                        0.55f,
                        0.45f,
                        20f
                    ),
                    m.metalDark
                );
            }

            // Luminarias lineales.
            for (float x = 10f; x < 116f; x += 20f)
            {
                Block(
                    parent,
                    new Vector3(
                        x,
                        5.55f,
                        0f
                    ),
                    new Vector3(
                        5f,
                        0.12f,
                        0.35f
                    ),
                    m.accCyan
                );
            }
        }

        // ================================================================
        // RED DE TUBERÍAS
        // ================================================================

        static void BuildPipeNetwork(
            Transform parent,
            Mats m
        )
        {
            // Tubos del lado norte.
            BuildPipe(
                parent,
                new Vector3(
                    28f,
                    3.4f,
                    10.25f
                ),
                new Vector3(
                    42f,
                    0.25f,
                    0.25f
                ),
                m.metal
            );

            BuildPipe(
                parent,
                new Vector3(
                    70f,
                    3.65f,
                    10.25f
                ),
                new Vector3(
                    28f,
                    0.22f,
                    0.22f
                ),
                m.metal
            );

            // Tubos del lado sur.
            BuildPipe(
                parent,
                new Vector3(
                    22f,
                    3.2f,
                    -10.25f
                ),
                new Vector3(
                    35f,
                    0.3f,
                    0.3f
                ),
                m.metal
            );

            BuildPipe(
                parent,
                new Vector3(
                    108f,
                    3.3f,
                    -10.25f
                ),
                new Vector3(
                    24f,
                    0.24f,
                    0.24f
                ),
                m.metal
            );

            // Conductos verticales en zonas técnicas.
            BuildPipe(
                parent,
                new Vector3(
                    18f,
                    2.4f,
                    10.1f
                ),
                new Vector3(
                    0.45f,
                    4.8f,
                    0.45f
                ),
                m.metalDark
            );

            BuildPipe(
                parent,
                new Vector3(
                    72f,
                    2.4f,
                    -10.1f
                ),
                new Vector3(
                    0.45f,
                    4.8f,
                    0.45f
                ),
                m.metalDark
            );
        }

        static void BuildPipe(
            Transform parent,
            Vector3 pos,
            Vector3 size,
            Material mat
        )
        {
            Block(
                parent,
                pos,
                size,
                mat
            );
        }

        // ================================================================
        // ZONA A
        // ================================================================

        static void BuildZoneA(
            Transform parent,
            Mats m
        )
        {
            // Consola técnica detrás de la zona inicial.
            Block(
                parent,
                new Vector3(
                    9f,
                    1.1f,
                    9.3f
                ),
                new Vector3(
                    4.5f,
                    2.2f,
                    1.0f
                ),
                m.metalDark
            );

            // Panel luminoso.
            Block(
                parent,
                new Vector3(
                    9f,
                    2.1f,
                    8.72f
                ),
                new Vector3(
                    2.6f,
                    0.35f,
                    0.08f
                ),
                m.accWarm
            );

            // Cableado bajo.
            Block(
                parent,
                new Vector3(
                    16f,
                    0.75f,
                    9.7f
                ),
                new Vector3(
                    7f,
                    0.25f,
                    0.25f
                ),
                m.metal
            );
        }

        // ================================================================
        // ZONA B
        // ================================================================

        static void BuildZoneB(
            Transform parent,
            Mats m
        )
        {
            // Dos estaciones técnicas laterales.
            BuildMachine(
                parent,
                new Vector3(
                    28f,
                    1f,
                    8.5f
                ),
                new Vector3(
                    3.4f,
                    2f,
                    2.2f
                ),
                m
            );

            BuildMachine(
                parent,
                new Vector3(
                    42f,
                    1f,
                    -8.5f
                ),
                new Vector3(
                    3.4f,
                    2f,
                    2.2f
                ),
                m
            );

            // Cobertura baja funcional.
            Block(
                parent,
                new Vector3(
                    37f,
                    0.65f,
                    6.4f
                ),
                new Vector3(
                    4.5f,
                    1.3f,
                    1.1f
                ),
                m.metalDark
            );

            Block(
                parent,
                new Vector3(
                    37f,
                    0.65f,
                    -6.4f
                ),
                new Vector3(
                    4.5f,
                    1.3f,
                    1.1f
                ),
                m.metalDark
            );
        }

        // ================================================================
        // MÁQUINA
        // ================================================================

        static void BuildMachine(
            Transform parent,
            Vector3 pos,
            Vector3 size,
            Mats m
        )
        {
            Block(
                parent,
                pos,
                size,
                m.metalDark
            );

            Block(
                parent,
                new Vector3(
                    pos.x,
                    pos.y + size.y * 0.25f,
                    pos.z -
                    size.z * 0.52f
                ),
                new Vector3(
                    size.x * 0.55f,
                    size.y * 0.25f,
                    0.08f
                ),
                m.accCyan
            );
        }

        // ================================================================
        // FOSO CENTRAL
        // ================================================================

        static void BuildCentralPit(
            Transform parent,
            Mats m
        )
        {
            // Fondo del foso.
            Block(
                parent,
                new Vector3(
                    59f,
                    -8f,
                    0f
                ),
                new Vector3(
                    3.8f,
                    1f,
                    22f
                ),
                m.floorDark
            );

            // Paredes internas del foso.
            Block(
                parent,
                new Vector3(
                    57.2f,
                    -3.8f,
                    0f
                ),
                new Vector3(
                    0.5f,
                    8f,
                    22f
                ),
                m.wallDark
            );

            Block(
                parent,
                new Vector3(
                    60.8f,
                    -3.8f,
                    0f
                ),
                new Vector3(
                    0.5f,
                    8f,
                    22f
                ),
                m.wallDark
            );

            // Borde luminoso.
            Block(
                parent,
                new Vector3(
                    57f,
                    0.05f,
                    0f
                ),
                new Vector3(
                    0.12f,
                    0.1f,
                    22f
                ),
                m.accRed
            );

            Block(
                parent,
                new Vector3(
                    61f,
                    0.05f,
                    0f
                ),
                new Vector3(
                    0.12f,
                    0.1f,
                    22f
                ),
                m.accRed
            );

            // Estructura técnica bajo el foso.
            for (float z = -8f; z <= 8f; z += 4f)
            {
                Block(
                    parent,
                    new Vector3(
                        59f,
                        -6.7f,
                        z
                    ),
                    new Vector3(
                        2.8f,
                        0.35f,
                        0.35f
                    ),
                    m.metal
                );
            }

            // Puente norte.
            Block(
                parent,
                new Vector3(
                    58.2f,
                    0.7f,
                    4f
                ),
                new Vector3(
                    2f,
                    0.2f,
                    1.6f
                ),
                m.wall
            );

            // Puente sur.
            Block(
                parent,
                new Vector3(
                    59.8f,
                    0.7f,
                    -4f
                ),
                new Vector3(
                    2f,
                    0.2f,
                    1.6f
                ),
                m.wall
            );

            // Soportes de los puentes.
            Block(
                parent,
                new Vector3(
                    58.2f,
                    -2.8f,
                    4f
                ),
                new Vector3(
                    0.35f,
                    5.6f,
                    0.35f
                ),
                m.metalDark
            );

            Block(
                parent,
                new Vector3(
                    59.8f,
                    -2.8f,
                    -4f
                ),
                new Vector3(
                    0.35f,
                    5.6f,
                    0.35f
                ),
                m.metalDark
            );
        }

        // ================================================================
        // ZONA PUZZLE
        // ================================================================

        static void BuildPuzzleArchitecture(
            Transform parent,
            Mats m
        )
        {
            // Sala técnica lateral.
            Block(
                parent,
                new Vector3(
                    75f,
                    1.6f,
                    9.4f
                ),
                new Vector3(
                    9f,
                    3.2f,
                    1.2f
                ),
                m.wallDark
            );

            // Marco de puerta.
            BuildDoorFrame(
                parent,
                84.5f,
                m
            );

            // Tiras de iluminación de emergencia.
            Block(
                parent,
                new Vector3(
                    76f,
                    4.25f,
                    9.0f
                ),
                new Vector3(
                    6f,
                    0.12f,
                    0.12f
                ),
                m.accCyan
            );

            Block(
                parent,
                new Vector3(
                    82f,
                    4.25f,
                    -9.0f
                ),
                new Vector3(
                    5f,
                    0.12f,
                    0.12f
                ),
                m.accCyan
            );
        }

        // ================================================================
        // MARCO DE PUERTA
        // ================================================================

        static void BuildDoorFrame(
            Transform parent,
            float x,
            Mats m
        )
        {
            // Marco norte.
            Block(
                parent,
                new Vector3(
                    x,
                    2.1f,
                    6.9f
                ),
                new Vector3(
                    0.65f,
                    4.2f,
                    0.65f
                ),
                m.metal
            );

            // Marco sur.
            Block(
                parent,
                new Vector3(
                    x,
                    2.1f,
                    -6.9f
                ),
                new Vector3(
                    0.65f,
                    4.2f,
                    0.65f
                ),
                m.metal
            );

            // Viga superior.
            Block(
                parent,
                new Vector3(
                    x,
                    4.25f,
                    0f
                ),
                new Vector3(
                    0.65f,
                    0.65f,
                    14.2f
                ),
                m.metal
            );

            // Señal.
            Block(
                parent,
                new Vector3(
                    x,
                    4.55f,
                    0f
                ),
                new Vector3(
                    0.9f,
                    0.15f,
                    3f
                ),
                m.accCyan
            );
        }

        // ================================================================
        // ZONA DEL JEFE
        // ================================================================

        static void BuildBossArchitecture(
            Transform parent,
            Mats m
        )
        {
            // Entrada de la arena.
            BuildDoorFrame(
                parent,
                89f,
                m
            );

            // Columnas principales de la arena.
            BuildArenaColumn(
                parent,
                new Vector3(
                    94f,
                    2.4f,
                    8.7f
                ),
                m
            );

            BuildArenaColumn(
                parent,
                new Vector3(
                    94f,
                    2.4f,
                    -8.7f
                ),
                m
            );

            BuildArenaColumn(
                parent,
                new Vector3(
                    108f,
                    2.4f,
                    8.7f
                ),
                m
            );

            BuildArenaColumn(
                parent,
                new Vector3(
                    108f,
                    2.4f,
                    -8.7f
                ),
                m
            );

            // Paneles técnicos de la arena.
            BuildMachine(
                parent,
                new Vector3(
                    96f,
                    1f,
                    9.2f
                ),
                new Vector3(
                    3f,
                    2f,
                    1.2f
                ),
                m
            );

            BuildMachine(
                parent,
                new Vector3(
                    104f,
                    1f,
                    -9.2f
                ),
                new Vector3(
                    3f,
                    2f,
                    1.2f
                ),
                m
            );

            // Marco de salida.
            BuildDoorFrame(
                parent,
                116f,
                m
            );

            // Iluminación roja de la arena.
            Block(
                parent,
                new Vector3(
                    100f,
                    4.7f,
                    9.5f
                ),
                new Vector3(
                    7f,
                    0.12f,
                    0.12f
                ),
                m.accRed
            );

            Block(
                parent,
                new Vector3(
                    100f,
                    4.7f,
                    -9.5f
                ),
                new Vector3(
                    7f,
                    0.12f,
                    0.12f
                ),
                m.accRed
            );
        }

        // ================================================================
        // COLUMNAS DE ARENA
        // ================================================================

        static void BuildArenaColumn(
            Transform parent,
            Vector3 pos,
            Mats m
        )
        {
            BuildStructuralColumn(
                parent,
                pos,
                m.metal
            );

            Block(
                parent,
                new Vector3(
                    pos.x,
                    3.8f,
                    pos.z -
                    Mathf.Sign(pos.z) * 0.65f
                ),
                new Vector3(
                    0.18f,
                    2.3f,
                    0.18f
                ),
                m.accRed
            );
        }

        // ================================================================
        // SALIDA
        // ================================================================

        static void BuildExitArchitecture(
            Transform parent,
            Mats m
        )
        {
            // Pequeño vestíbulo técnico.
            Block(
                parent,
                new Vector3(
                    118f,
                    1.2f,
                    9.3f
                ),
                new Vector3(
                    5f,
                    2.4f,
                    1.1f
                ),
                m.wallDark
            );

            Block(
                parent,
                new Vector3(
                    118f,
                    1.2f,
                    -9.3f
                ),
                new Vector3(
                    5f,
                    2.4f,
                    1.1f
                ),
                m.wallDark
            );

            // Señal de salida.
            Block(
                parent,
                new Vector3(
                    118f,
                    4.3f,
                    0f
                ),
                new Vector3(
                    3.8f,
                    0.18f,
                    0.18f
                ),
                m.accGreen
            );
        }

        // ================================================================
        // SUELO
        // ================================================================

        static void FloorRun(
            Transform parent,
            float from,
            float to,
            float width,
            Material mat
        )
        {
            for (
                float x0 = from;
                x0 < to;
                x0 += 20f
            )
            {
                float w =
                    Mathf.Min(
                        20f,
                        to - x0
                    );

                if (w <= 0.01f)
                    break;

                Block(
                    parent,
                    new Vector3(
                        x0 + w / 2f,
                        -0.5f,
                        0f
                    ),
                    new Vector3(
                        w + 0.02f,
                        1f,
                        width
                    ),
                    mat
                );
            }
        }

        // ================================================================
        // BLOQUE DECORATIVO
        // ================================================================

        static void DecorativeBlock(
            Transform parent,
            Vector3 pos,
            Vector3 size,
            Material mat
        )
        {
            Block(
                parent,
                pos,
                size,
                mat
            );
        }

        // ================================================================
        // PILAR DECORATIVO
        // ================================================================

        static void DecorativePillar(
            Transform parent,
            Vector3 pos,
            Material mat
        )
        {
            BuildStructuralColumn(
                parent,
                pos,
                mat
            );
        }

        // ================================================================
        // CUBO CON COLLIDER
        // ================================================================

        static GameObject Block(
            Transform parent,
            Vector3 pos,
            Vector3 size,
            Material mat
        )
        {
            var go =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            go.transform.localScale =
                size;

            var renderer =
                go.GetComponent<Renderer>();

            if (renderer != null)
                renderer.material = mat;

            // CreatePrimitive ya crea BoxCollider.
            // NO se elimina.
            // Por eso estas estructuras son sólidas.

            return go;
        }

        // ================================================================
        // GAMEPLAY
        // ================================================================

        static void BuildGameplay(Mats m)
        {
            var root =
                new GameObject(
                    "Gameplay"
                );

            // ============================================================
            // ZONA A
            // ============================================================

            CheckpointZoneAt(
                root.transform,
                new Vector3(
                    4f,
                    0f,
                    0f
                ),
                new Vector3(
                    6f,
                    4f,
                    8f
                )
            );

            DataLogAt(
                root.transform,
                new Vector3(
                    8f,
                    0.9f,
                    2f
                ),
                0,
                "Registro 01 — El Gran Vacío\n" +
                "\"Tras la Gran Migración del 2440, " +
                "la humanidad abandonó la Tierra. " +
                "Solo quedamos las máquinas... " +
                "y los que despiertan.\""
            );

            PickupAt(
                root.transform,
                new Vector3(
                    12f,
                    0.9f,
                    -3f
                ),
                PickupKind.Medkit,
                45f
            );

            // ============================================================
            // ZONA B
            // ============================================================

            CheckpointZoneAt(
                root.transform,
                new Vector3(
                    30f,
                    0f,
                    0f
                ),
                new Vector3(
                    5f,
                    4f,
                    8f
                )
            );

            DroneAt(
                root.transform,
                new Vector3(
                    33f,
                    2.5f,
                    5f
                ),
                new[]
                {
                    new Vector3(
                        32f,
                        2.5f,
                        5f
                    ),
                    new Vector3(
                        40f,
                        2.5f,
                        5f
                    )
                },
                m
            );

            DroneAt(
                root.transform,
                new Vector3(
                    33f,
                    2.5f,
                    -5f
                ),
                new[]
                {
                    new Vector3(
                        32f,
                        2.5f,
                        -5f
                    ),
                    new Vector3(
                        40f,
                        2.5f,
                        -5f
                    )
                },
                m
            );

            DataLogAt(
                root.transform,
                new Vector3(
                    32.5f,
                    0.9f,
                    -3f
                ),
                1,
                "Registro 02 — Año 3000\n" +
                "\"Los últimos humanos conocidos " +
                "desaparecieron en el 2561. " +
                "Este laboratorio vive en silencio " +
                "desde entonces.\""
            );

            PickupAt(
                root.transform,
                new Vector3(
                    35f,
                    0.9f,
                    3f
                ),
                PickupKind.Medkit,
                50f
            );

            PickupAt(
                root.transform,
                new Vector3(
                    39f,
                    0.9f,
                    -3f
                ),
                PickupKind.Energy,
                40f
            );

            var hide =
                new GameObject(
                    "HideSpot",
                    typeof(BoxCollider),
                    typeof(HideSpotZone)
                );

            hide.transform.SetParent(
                root.transform,
                false
            );

            hide.transform.position =
                new Vector3(
                    36f,
                    0f,
                    0f
                );

            hide.GetComponent<BoxCollider>()
                .isTrigger = true;

            hide.GetComponent<BoxCollider>()
                .size =
                new Vector3(
                    8f,
                    4f,
                    8f
                );

            // ============================================================
            // ZONA C
            // ============================================================

            CheckpointZoneAt(
                root.transform,
                new Vector3(
                    46f,
                    0f,
                    0f
                ),
                new Vector3(
                    5f,
                    4f,
                    8f
                )
            );

            DataLogAt(
                root.transform,
                new Vector3(
                    52f,
                    0.9f,
                    3f
                ),
                2,
                "Registro 03 — La instalación\n" +
                "\"El Laboratorio Núcleo lleva activo " +
                "desde el 2600. La Máquina Guardiana " +
                "protege la salida. Nadie la ha desafiado.\""
            );

            DataLogAt(
                root.transform,
                new Vector3(
                    54.5f,
                    0.9f,
                    -3f
                ),
                3,
                "Registro 04 — La salida\n" +
                "\"El plano indica: SALIDA al Lado Este. " +
                "Desactiva la Guardiana cargando " +
                "3 terminales con energía genética.\""
            );

            DroneAt(
                root.transform,
                new Vector3(
                    59f,
                    2.2f,
                    -8f
                ),
                new[]
                {
                    new Vector3(
                        55f,
                        2.2f,
                        -8f
                    ),
                    new Vector3(
                        63f,
                        2.2f,
                        -8f
                    )
                },
                m
            );

            PickupAt(
                root.transform,
                new Vector3(
                    50f,
                    0.9f,
                    5f
                ),
                PickupKind.Energy,
                45f
            );

            // ============================================================
            // ZONA D
            // ============================================================

            CheckpointZoneAt(
                root.transform,
                new Vector3(
                    74f,
                    0f,
                    0f
                ),
                new Vector3(
                    5f,
                    4f,
                    8f
                )
            );

            var door =
                SlidingDoorAt(
                    root.transform,
                    new Vector3(
                        84.5f,
                        0f,
                        0f
                    ),
                    m,
                    2
                );

            PillarFlank(
                root.transform,
                84.5f,
                m.metalDark
            );

            PanelAt(
                root.transform,
                new Vector3(
                    76f,
                    0f,
                    -3.5f
                ),
                PanelKind.PuzzlePanel,
                door,
                null,
                1,
                m
            );

            PanelAt(
                root.transform,
                new Vector3(
                    80f,
                    0f,
                    3.5f
                ),
                PanelKind.PuzzlePanel,
                door,
                null,
                1,
                m
            );

            PickupAt(
                root.transform,
                new Vector3(
                    78f,
                    0.9f,
                    3f
                ),
                PickupKind.Medkit,
                50f
            );

            // ============================================================
            // ZONA E
            // ============================================================

            CheckpointZoneAt(
                root.transform,
                new Vector3(
                    89f,
                    0f,
                    0f
                ),
                new Vector3(
                    6f,
                    4f,
                    10f
                )
            );

            var exitDoor =
                SlidingDoorAt(
                    root.transform,
                    new Vector3(
                        116f,
                        0f,
                        0f
                    ),
                    m,
                    2
                );

            PillarFlank(
                root.transform,
                116f,
                m.metalDark
            );

            var guardian =
                BuildGuardian(
                    root.transform,
                    new Vector3(
                        100f,
                        0f,
                        0f
                    ),
                    m
                );

            guardian.exitDoor =
                exitDoor;

            BossTerminalAt(
                root.transform,
                new Vector3(
                    92f,
                    0f,
                    6.5f
                ),
                guardian,
                m
            );

            BossTerminalAt(
                root.transform,
                new Vector3(
                    92f,
                    0f,
                    -6.5f
                ),
                guardian,
                m
            );

            BossTerminalAt(
                root.transform,
                new Vector3(
                    107f,
                    0f,
                    6.5f
                ),
                guardian,
                m
            );

            PickupAt(
                root.transform,
                new Vector3(
                    94f,
                    0.9f,
                    3f
                ),
                PickupKind.Energy,
                30f
            );

            // ============================================================
            // ZONA F
            // ============================================================

            var exit =
                new GameObject(
                    "LevelExit",
                    typeof(BoxCollider),
                    typeof(LevelExitZone)
                );

            exit.transform.SetParent(
                root.transform,
                false
            );

            exit.transform.position =
                new Vector3(
                    118.5f,
                    0f,
                    0f
                );

            exit.GetComponent<BoxCollider>()
                .isTrigger = true;

            exit.GetComponent<BoxCollider>()
                .size =
                new Vector3(
                    3f,
                    4f,
                    6f
                );
        }

        // ================================================================
        // CHECKPOINT
        // ================================================================

        static void CheckpointZoneAt(
            Transform parent,
            Vector3 pos,
            Vector3 size
        )
        {
            var go =
                new GameObject(
                    "Checkpoint",
                    typeof(BoxCollider),
                    typeof(CheckpointZone)
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var bc =
                go.GetComponent<BoxCollider>();

            bc.isTrigger = true;
            bc.size = size;
        }

        // ================================================================
        // DATA LOG
        // ================================================================

        static void DataLogAt(
            Transform parent,
            Vector3 pos,
            int id,
            string content
        )
        {
            var go =
                ModelFactory.BuildDataLog(
                    "DataLog" + id,
                    new Material[]
                    {
                        ModelFactory.Lit(
                            new Color(
                                0.55f,
                                0.6f,
                                0.7f
                            ),
                            0.5f,
                            0.5f
                        )
                    }
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var dl =
                go.AddComponent<DataLog>();

            dl.logId = id;
            dl.content = content;
        }

        // ================================================================
        // PICKUP
        // ================================================================

        static void PickupAt(
            Transform parent,
            Vector3 pos,
            PickupKind kind,
            float amount
        )
        {
            var mats =
                new Material[]
                {
                    ModelFactory.Lit(
                        new Color(
                            0.9f,
                            0.35f,
                            0.2f
                        ),
                        0.2f,
                        0.5f
                    ),

                    ModelFactory.Lit(
                        Color.white,
                        0f,
                        0.4f
                    ),

                    ModelFactory.Lit(
                        Color.white,
                        0f,
                        0.5f
                    )
                };

            var go =
                ModelFactory.BuildPickup(
                    kind ==
                    PickupKind.Medkit
                        ? "Botiquin"
                        : "CapsulaEnergia",
                    kind,
                    mats
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var pk =
                go.AddComponent<Pickup>();

            pk.kind = kind;
            pk.amount = amount;
        }

        // ================================================================
        // PUERTA DESLIZANTE
        // ================================================================

        static DoorControl SlidingDoorAt(
            Transform parent,
            Vector3 pos,
            Mats m,
            int needed
        )
        {
            var go =
                ModelFactory.BuildSlidingDoor(
                    "DoorLab",
                    new Material[]
                    {
                        m.metalDark,
                        m.metal
                    }
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var dc =
                go.GetComponent<DoorControl>();

            dc.neededActivations =
                needed;

            return dc;
        }

        // ================================================================
        // FLANCOS DE PUERTA
        // ================================================================

        static void PillarFlank(
            Transform parent,
            float x,
            Material mat
        )
        {
            Block(
                parent,
                new Vector3(
                    x,
                    1.6f,
                    7.1f
                ),
                new Vector3(
                    0.5f,
                    3.2f,
                    9.6f
                ),
                mat
            );

            Block(
                parent,
                new Vector3(
                    x,
                    1.6f,
                    -7.1f
                ),
                new Vector3(
                    0.5f,
                    3.2f,
                    9.6f
                ),
                mat
            );
        }

        // ================================================================
        // PANEL
        // ================================================================

        static void PanelAt(
            Transform parent,
            Vector3 pos,
            PanelKind kind,
            DoorControl door,
            GuardianBoss boss,
            int charges,
            Mats m
        )
        {
            var go =
                ModelFactory.BuildPanel(
                    kind ==
                    PanelKind.BossTerminal
                        ? "TerminalBoss"
                        : "ConsolaPuzzle",
                    new Material[]
                    {
                        m.metal,
                        m.metalDark
                    }
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var pa =
                go.AddComponent<PanelActivator>();

            pa.kind = kind;
            pa.linkedDoor = door;
            pa.boss = boss;
            pa.neededCharges = charges;

            pa.glowRenderer =
                go.transform.Find(
                    "Glow"
                )?.GetComponent<Renderer>();
        }

        // ================================================================
        // TERMINAL DEL JEFE
        // ================================================================

        static void BossTerminalAt(
            Transform parent,
            Vector3 pos,
            GuardianBoss boss,
            Mats m
        )
        {
            PanelAt(
                parent,
                pos,
                PanelKind.BossTerminal,
                null,
                boss,
                1,
                m
            );
        }

        // ================================================================
        // DRON
        // ================================================================

        static void DroneAt(
            Transform parent,
            Vector3 pos,
            Vector3[] patrol,
            Mats m
        )
        {
            var go =
                ModelFactory.BuildDrone(
                    "Dron",
                    new Material[]
                    {
                        m.metal,
                        m.metalDark
                    }
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var dc =
                go.AddComponent<DroneController>();

            dc.patrolNodes =
                patrol;

            dc.eyeLight =
                go.transform.Find(
                    "eyeLight"
                );

            dc.eyeLight2 =
                go.transform.Find(
                    "eyeLight2"
                );
        }

        // ================================================================
        // JEFE
        // ================================================================

        static GuardianBoss BuildGuardian(
            Transform parent,
            Vector3 pos,
            Mats m
        )
        {
            var go =
                ModelFactory.BuildGuardian(
                    "MáquinaGuardiana",
                    new Material[]
                    {
                        m.metal,
                        m.metalDark
                    }
                );

            go.transform.SetParent(
                parent,
                false
            );

            go.transform.position =
                pos;

            var solid =
                go.AddComponent<BoxCollider>();

            solid.center =
                new Vector3(
                    0f,
                    1.35f,
                    0f
                );

            solid.size =
                new Vector3(
                    2.3f,
                    2.9f,
                    1.2f
                );

            var boss =
                go.AddComponent<GuardianBoss>();

            var trigGo =
                new GameObject(
                    "BossActivateTrigger",
                    typeof(BoxCollider),
                    typeof(BossTriggerForward)
                );

            trigGo.transform.SetParent(
                go.transform,
                false
            );

            trigGo.transform.localPosition =
                new Vector3(
                    0f,
                    1f,
                    0f
                );

            var tb =
                trigGo.GetComponent<BoxCollider>();

            tb.isTrigger = true;

            tb.size =
                new Vector3(
                    5f,
                    3f,
                    6f
                );

            trigGo
                .GetComponent<BossTriggerForward>()
                .boss = boss;

            boss.core =
                go.transform.Find(
                    "core"
                );

            boss.eye =
                go.transform.Find(
                    "eye"
                );

            boss.redLight =
                go.transform.Find(
                    "redLight"
                )?.GetComponent<Light>();

            return boss;
        }

        // ================================================================
        // BUILD SETTINGS
        // ================================================================

        static void SetBuildScenes()
        {
            var list =
                new[]
                {
                    EiraPaths.Scenes +
                    "/IntroScene.unity",

                    EiraPaths.Scenes +
                    "/Level1Scene.unity"
                };

            var scenes =
                new EditorBuildSettingsScene[
                    list.Length
                ];

            for (
                int i = 0;
                i < list.Length;
                i++
            )
            {
                scenes[i] =
                    new EditorBuildSettingsScene(
                        list[i],
                        true
                    );
            }

            EditorBuildSettings.scenes =
                scenes;
        }

        // ================================================================
        // AGREGAR EIRA A ESCENA ACTUAL
        // ================================================================

        [MenuItem("Eira/Agregar Eira a escena actual")]
        public static void AddEiraToCurrentScene()
        {
            var existing =
                GameObject.Find("Eira");

            if (existing != null)
            {
                Debug.LogWarning(
                    "Eira: ya existe una Eira " +
                    "en la escena actual."
                );

                Selection.activeGameObject =
                    existing;

                return;
            }

            var player =
                BuildPlayer(
                    new Vector3(
                        2f,
                        0.2f,
                        0f
                    ),
                    new Mats()
                );

            if (player == null)
            {
                Debug.LogError(
                    "Eira: no se pudo crear " +
                    "el personaje."
                );

                return;
            }

            Selection.activeGameObject =
                player.gameObject;

            EditorSceneManager.MarkSceneDirty(
                SceneManager.GetActiveScene()
            );

            Debug.Log(
                "Eira: personaje agregado " +
                "a la escena actual sin " +
                "modificar el escenario."
            );
        }
    }
}
