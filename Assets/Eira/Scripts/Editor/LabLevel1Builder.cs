using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace EiraGame.Editor
{
    // Construccion de los elementos del Laboratorio en el Nivel 1:
    //   - Maquinas y androides (con puntos de anexo "Anexo_..." para enganchar mas unidades).
    //   - Mobiliario de laboratorio (mesas, criogenicos, vitrinas, viales, tiras de luz).
    //   - Iluminacion de DIA brillante (sol direccional, cielo azul, niebla clara).
    // Re-ejecutable: elimina "Laboratorio" y "SunDia" previos antes de reconstruir.
    public static class LabLevel1Builder
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const float FloorY = 0f;

        static class Mat
        {
            public static Material metal, metalDark, metalBright, panel, accentCyan, accentBlue, accentGreen, accentWarm, glass, vials, cable, core;
        }

        // ---- Acceso desde el menu: reconstruye y deja todo de dia ----
        [MenuItem("Eira/Anexar Laboratorio (Nivel 1 - Dia)")]
        public static void BuildLab()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            SetupMaterials();
            DestroyIfExists("Laboratorio");
            DestroyIfExists("SunDia");

            SceneToDay();
            var root = new GameObject("Laboratorio");
            BuildMachines(root.transform);
            BuildAndroids(root.transform);
            BuildCryoPods(root.transform);
            BuildLabTables(root.transform);
            BuildWallLights(root.transform);
            BuildAnchors(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(string.Format(
                "Laboratorio OK: {0} maquinas, {1} androides, {2} criogenicos, {3} mesas, {4} tiras de luz, {5} anexos. Iluminacion = DIA.",
                machineCount, androidCount, cryoCount, tableCount, lightStripCount, anchorCount));
        }

        // ================================================================= materiales
        static void SetupMaterials()
        {
            Mat.metal = ModelFactory.Lit(new Color(0.78f, 0.82f, 0.87f), 0.75f, 0.6f);
            Mat.metalDark = ModelFactory.Lit(new Color(0.3f, 0.33f, 0.38f), 0.65f, 0.5f);
            Mat.metalBright = ModelFactory.Lit(new Color(0.93f, 0.95f, 0.98f), 0.3f, 0.55f);
            Mat.panel = ModelFactory.Lit(new Color(0.16f, 0.24f, 0.34f), 0.4f, 0.5f);
            Mat.accentCyan = ModelFactory.Glow(new Color(0.25f, 0.85f, 0.95f));
            Mat.accentBlue = ModelFactory.Glow(new Color(0.3f, 0.6f, 1f));
            Mat.accentGreen = ModelFactory.Glow(new Color(0.45f, 0.9f, 0.55f));
            Mat.accentWarm = ModelFactory.Glow(new Color(1f, 0.82f, 0.5f));
            Mat.glass = ModelFactory.Lit(new Color(0.62f, 0.8f, 0.95f), 0f, 0.95f);
            Mat.vials = ModelFactory.Lit(new Color(0.45f, 0.72f, 0.9f), 0f, 0.85f);
            Mat.cable = ModelFactory.Lit(new Color(0.08f, 0.1f, 0.12f), 0f, 0.25f);
            Mat.core = ModelFactory.Glow(new Color(0.35f, 0.75f, 1f));
        }

        // ================================================================= iluminacion: DIA
        static void SceneToDay()
        {
            // desactivar luces de "tarde" o escena previas para que mande el sol diurno
            var sunTarde = GameObject.Find("SunTarde");
            if (sunTarde != null) sunTarde.SetActive(false);
            var sunOld = GameObject.Find("Sun");
            if (sunOld != null) sunOld.SetActive(false);

            var sun = new GameObject("SunDia", typeof(Light));
            var dl = sun.GetComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(1f, 0.96f, 0.88f, 1f);
            dl.intensity = 1.7f;
            dl.shadows = LightShadows.Soft;
            dl.shadowStrength = 0.9f;
            sun.transform.rotation = Quaternion.Euler(48f, -28f, 0f);
            sun.transform.position = Vector3.zero;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.62f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.62f, 0.66f, 0.74f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.34f, 0.4f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.72f, 0.78f, 0.9f);
            RenderSettings.fogDensity = 0.014f;

            var sky = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            if (sky != null) RenderSettings.skybox = sky;

            var cam = GameObject.Find("MainCamera");
            if (cam != null)
            {
                var c = cam.GetComponent<Camera>();
                if (c != null)
                {
                    c.clearFlags = CameraClearFlags.SolidColor;
                    c.backgroundColor = new Color(0.42f, 0.55f, 0.78f);
                }
            }
        }

        // ================================================================= maquinas (generadores)
        static int machineCount;
        static void BuildMachines(Transform parent)
        {
            var spots = new[]
            {
                new Vector3(6f, 0f, -8.2f),
                new Vector3(26f, 0f, 8.2f),
                new Vector3(68f, 0f, -8.2f),
                new Vector3(110f, 0f, 8.2f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = BuildMachine("Generador_" + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.position = spots[i];
                go.transform.Rotate(0f, (i % 2 == 0) ? 90f : -90f, 0f);
                machineCount++;
            }
        }

        static GameObject BuildMachine(string name)
        {
            var root = new GameObject(name);
            var t = root.transform;

            // base y pedestal
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f), new Vector3(2.1f, 0.5f, 1.7f), Mat.metalDark, keepCollider: true);
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.7f, 0.5f, 0.7f), Mat.metal);
            // cuerpo reactor
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 1.45f, 0f), new Vector3(0.95f, 0.7f, 0.95f), Mat.metalBright);
            // bobina / núcleo
            var core = ModelFactory.Prim(t, PrimitiveType.Sphere, new Vector3(0f, 1.5f, 0.45f), Vector3.one * 0.45f, Mat.core);
            Object.DestroyImmediate(core.GetComponent<Collider>());
            // tuberías laterales
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0.85f, 1.15f, 0f), new Vector3(0.16f, 0.9f, 0.16f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(-0.85f, 1.15f, 0f), new Vector3(0.16f, 0.9f, 0.16f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 2.15f, 0f), new Vector3(0.3f, 0.5f, 0.3f), Mat.metal);
            // panel de estado
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0.62f), new Vector3(0.7f, 0.4f, 0.06f), Mat.panel);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.18f, 1.08f, 0.66f), new Vector3(0.1f, 0.06f, 0.02f), Mat.accentGreen);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.08f, 0.66f), new Vector3(0.12f, 0.06f, 0.02f), Mat.accentCyan);
            // anexo para enganchar mas maquinas
            var hook = new GameObject("Anexo_" + name);
            hook.transform.SetParent(t, false);
            hook.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            RingMarker(hook.transform, Vector3.zero, Mat.accentCyan, 0.35f);

            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.1f, 0f);
            bc.size = new Vector3(1.2f, 2.2f, 1.2f);
            return root;
        }

        // ================================================================= androides
        static int androidCount;
        static void BuildAndroids(Transform parent)
        {
            var spots = new[]
            {
                new Vector3(13f, 0f, 8.5f),
                new Vector3(50f, 0f, -8.5f),
                new Vector3(90f, 0f, 8.5f),
                new Vector3(114f, 0f, -8.5f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = BuildAndroid("Androide_" + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.position = spots[i];
                go.transform.Rotate(0f, (i % 2 == 0) ? 0f : 180f, 0f);
                androidCount++;
            }
        }

        static GameObject BuildAndroid(string name)
        {
            var root = new GameObject(name);
            var t = root.transform;

            // piernas
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.14f, 0.35f, 0f), new Vector3(0.22f, 0.7f, 0.22f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(-0.14f, 0.35f, 0f), new Vector3(0.22f, 0.7f, 0.22f), Mat.metalDark);
            // torso (armadura)
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(0.58f, 0.62f, 0.36f), Mat.metal);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.98f, 0.2f), new Vector3(0.52f, 0.4f, 0.03f), Mat.metalBright);
            // núcleo toracico
            var core = ModelFactory.Prim(t, PrimitiveType.Sphere, new Vector3(0f, 1.05f, 0.22f), Vector3.one * 0.16f, Mat.core);
            Object.DestroyImmediate(core.GetComponent<Collider>());
            // hombros y brazos
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.42f, 1.28f, 0f), new Vector3(0.18f, 0.24f, 0.18f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(-0.42f, 1.28f, 0f), new Vector3(0.18f, 0.24f, 0.18f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Capsule, new Vector3(0.48f, 1.02f, 0f), new Vector3(0.09f, 0.5f, 0.09f), Mat.metal);
            ModelFactory.Prim(t, PrimitiveType.Capsule, new Vector3(-0.48f, 1.02f, 0f), new Vector3(0.09f, 0.5f, 0.09f), Mat.metal);
            // cabeza con visor
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.52f, 0f), new Vector3(0.34f, 0.26f, 0.3f), Mat.metalBright);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.53f, 0.18f), new Vector3(0.26f, 0.09f, 0.02f), Mat.accentCyan);
            // antena
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 1.75f, 0f), new Vector3(0.03f, 0.16f, 0.03f), Mat.metalDark);
            var tip = ModelFactory.Prim(t, PrimitiveType.Sphere, new Vector3(0f, 1.95f, 0f), Vector3.one * 0.08f, Mat.accentGreen);
            Object.DestroyImmediate(tip.GetComponent<Collider>());
            // anexo
            var hook = new GameObject("Anexo_" + name);
            hook.transform.SetParent(t, false);
            hook.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            RingMarker(hook.transform, Vector3.zero, Mat.accentGreen, 0.3f);

            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 1.05f, 0f);
            bc.size = new Vector3(0.7f, 2.1f, 0.5f);
            return root;
        }

        // ================================================================= criogenicos
        static int cryoCount;
        static void BuildCryoPods(Transform parent)
        {
            var spots = new[]
            {
                new Vector3(29f, 0f, -8.2f),
                new Vector3(31f, 0f, 8.2f),
                new Vector3(44f, 0f, -8.2f),
                new Vector3(66f, 0f, 8.2f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = BuildCryoPod("Criogenico_" + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.position = spots[i];
                cryoCount++;
            }
        }

        static GameObject BuildCryoPod(string name)
        {
            var root = new GameObject(name);
            var t = root.transform;

            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.15f, 0f), new Vector3(0.55f, 0.15f, 0.55f), Mat.metalDark, keepCollider: true);
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(0.45f, 0.6f, 0.45f), Mat.glass);
            // liquido interior
            var liquid = ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 0.58f, 0f), new Vector3(0.34f, 0.3f, 0.34f), Mat.accentBlue);
            Object.DestroyImmediate(liquid.GetComponent<Collider>());
            // burbujas / vida
            var orb = ModelFactory.Prim(t, PrimitiveType.Sphere, new Vector3(0.12f, 0.78f, 0.1f), Vector3.one * 0.16f, Mat.accentCyan);
            Object.DestroyImmediate(orb.GetComponent<Collider>());
            // tapa y validas
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 1.42f, 0f), new Vector3(0.5f, 0.06f, 0.5f), Mat.metal);
            ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(0f, 1.7f, 0f), new Vector3(0.06f, 0.22f, 0.06f), Mat.metalDark);
            RingMarker(t, new Vector3(0f, 1.05f, 0.25f), Mat.accentCyan, 0.45f);

            var hook = new GameObject("Anexo_" + name);
            hook.transform.SetParent(t, false);
            hook.transform.localPosition = new Vector3(0f, 2.1f, 0f);

            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.9f, 0f);
            bc.size = new Vector3(0.7f, 1.9f, 0.7f);
            return root;
        }

        // ================================================================= mesas de laboratorio
        static int tableCount;
        static void BuildLabTables(Transform parent)
        {
            var spots = new[]
            {
                new Vector3(10f, 0f, 7.6f),
                new Vector3(18f, 0f, -7.6f),
                new Vector3(36f, 0f, 7.6f),
                new Vector3(46f, 0f, -7.6f),
                new Vector3(62f, 0f, 7.6f),
                new Vector3(74f, 0f, -6.8f),
                new Vector3(80f, 0f, 7.6f),
                new Vector3(96f, 0f, 7.6f),
                new Vector3(104f, 0f, -7.6f),
            };

            for (int i = 0; i < spots.Length; i++)
            {
                var go = BuildLabTable("MesaLab_" + (i + 1));
                go.transform.SetParent(parent, false);
                go.transform.position = spots[i];
                go.transform.Rotate(0f, (i % 2 == 0) ? 0f : 180f, 0f);
                tableCount++;
            }
        }

        static GameObject BuildLabTable(string name)
        {
            var root = new GameObject(name);
            var t = root.transform;

            // patas
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.68f, 0.4f, 0.36f), new Vector3(0.12f, 0.8f, 0.12f), Mat.metalDark, keepCollider: true);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(-0.68f, 0.4f, 0.36f), new Vector3(0.12f, 0.8f, 0.12f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.68f, 0.4f, -0.36f), new Vector3(0.12f, 0.8f, 0.12f), Mat.metalDark);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(-0.68f, 0.4f, -0.36f), new Vector3(0.12f, 0.8f, 0.12f), Mat.metalDark);
            // tablero
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.82f, 0f), new Vector3(1.7f, 0.08f, 1f), Mat.metalBright, keepCollider: true);
            // pantalla de monitor
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.28f, 0.4f), new Vector3(0.62f, 0.38f, 0.05f), Mat.panel);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.28f, 0.38f), new Vector3(0.54f, 0.3f, 0.02f), Mat.accentBlue);
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 1.08f, 0.34f), new Vector3(0.08f, 0.26f, 0.06f), Mat.metalDark);
            // tubos de ensayo sobre la mesa
            for (int k = 0; k < 3; k++)
            {
                float dx = (k - 1) * 0.22f;
                ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(dx, 0.98f, -0.26f), new Vector3(0.05f, 0.16f, 0.05f), Mat.vials);
                ModelFactory.Prim(t, PrimitiveType.Cylinder, new Vector3(dx, 0.93f, -0.26f), new Vector3(0.035f, 0.08f, 0.035f), Mat.accentGreen);
            }
            // portaobjetos
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0f, 0.9f, 0.05f), new Vector3(0.5f, 0.04f, 0.34f), Mat.glass);
            // cables
            ModelFactory.Prim(t, PrimitiveType.Cube, new Vector3(0.7f, 0.86f, 0.6f), new Vector3(0.1f, 0.03f, 0.5f), Mat.cable);

            var hook = new GameObject("Anexo_" + name);
            hook.transform.SetParent(t, false);
            hook.transform.localPosition = new Vector3(0f, 1.5f, 0f);

            var bc = root.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.85f, 0f);
            bc.size = new Vector3(1.7f, 1.7f, 1f);
            return root;
        }

        // ================================================================= tiras de luz de pared
        static int lightStripCount;
        static void BuildWallLights(Transform parent)
        {
            float[] xs = { 8f, 20f, 32f, 44f, 66f, 78f, 90f, 102f, 114f };
            foreach (float x in xs)
            {
                LightStripAt(parent, new Vector3(x, 2.5f, 11.35f));
                LightStripAt(parent, new Vector3(x, 2.5f, -11.35f));
                lightStripCount += 2;
            }
        }

        static void LightStripAt(Transform parent, Vector3 pos)
        {
            var go = new GameObject("TiraLuz");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var strip = ModelFactory.Prim(go.transform, PrimitiveType.Cube, Vector3.zero, new Vector3(1.4f, 0.08f, 0.05f), Mat.accentCyan);
            Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        // ================================================================= anexos (puntos de montaje en escena)
        static int anchorCount;
        static void BuildAnchors(Transform parent)
        {
            var root = new GameObject("Anexos");
            root.transform.SetParent(parent, false);

            var visuals = new[]
            {
                new object[] { "Anexo_Maquina_A", new Vector3(15f, 0f, -8.4f) },
                new object[] { "Anexo_Maquina_B", new Vector3(63f, 0f, -8.4f) },
                new object[] { "Anexo_Maquina_C", new Vector3(100f, 0f, -8.4f) },
                new object[] { "Anexo_Androide_A", new Vector3(24f, 0f, 8.6f) },
                new object[] { "Anexo_Androide_B", new Vector3(70f, 0f, 8.6f) },
                new object[] { "Anexo_Servidor", new Vector3(54f, 0f, 8.4f) },
                new object[] { "Anexo_Biocapacitor", new Vector3(98f, 0f, -8.4f) },
            };

            foreach (var item in visuals)
            {
                var name = (string)item[0];
                var pos = (Vector3)item[1];
                var anchor = new GameObject(name);
                anchor.transform.SetParent(root.transform, false);
                anchor.transform.position = pos;
                RingMarker(anchor.transform, Vector3.zero, Mat.accentCyan, 0.4f);
                ModelFactory.AddLabel(anchor, name.Replace("Anexo_", ""), 0.16f, new Color(0.6f, 0.95f, 1f));
                anchorCount++;
            }

            // anexo universal: el jugador/editor puede padrear cualquier maquina/androide aqui
            var center = new GameObject("Anexo_Fabrica");
            center.transform.SetParent(root.transform, false);
            center.transform.position = new Vector3(56.5f, 0f, 0f);
            RingMarker(center.transform, Vector3.zero, Mat.accentGreen, 0.5f);
            ModelFactory.AddLabel(center, "FABRICA (anexar aqui)", 0.14f, new Color(0.6f, 1f, 0.7f));
            anchorCount++;
        }

        // ================================================================= utilidades
        static void RingMarker(Transform parent, Vector3 localPos, Material mat, float radius)
        {
            var ring = ModelFactory.Prim(parent, PrimitiveType.Cylinder, localPos, Vector3.one * radius, mat);
            ring.transform.localScale = new Vector3(radius, 0.03f, radius);
            ring.transform.Rotate(90f, 0f, 0f);
            Object.DestroyImmediate(ring.GetComponent<Collider>());
        }

        static void DestroyIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null) Object.DestroyImmediate(go);
        }
    }
}