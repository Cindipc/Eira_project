using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Auditoría y pulido del Nivel 1.
    ///
    /// El nivel ya tiene toda la cadena jugable (4 registros, checkpoints,
    /// puzzle, paneles, 3 terminales, guardián y salida), pero le faltaba lo
    /// que hace que un sitio parezca un sitio: un guion de color por zonas,
    /// balizas que digan por dónde seguir y aire.
    ///
    /// Dos herramientas:
    ///
    ///   Eira/Auditar nivel 1  -> lista de comprobación, no toca nada.
    ///   Eira/Pulir nivel 1    -> aplica color, balizas y ambiente, y guarda.
    ///
    /// Todo se deduce de lo que ya hay en la escena (checkpoints, puertas,
    /// terminales), nunca de posiciones escritas a mano: si se mueve algo, el
    /// pulido lo sigue. Es el mismo criterio que usa MissionDirector para no
    /// desincronizarse del nivel.
    /// </summary>
    public static class Level1Polish
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const string RootName = "LevelPolish";

        static readonly StringBuilder Log = new StringBuilder();

        // =============================================================
        // GUION DE COLOR
        // =============================================================

        /// <summary>
        /// Color por zona, en el orden en que se recorren. Se parece a la
        /// paleta de la intro a propósito: el laboratorio tiene que ser el
        /// mismo mundo que la escena de la que se acaba de salir.
        /// </summary>
        static readonly Color[] ZoneColors =
        {
            new Color(1.00f, 0.72f, 0.45f),   // A · despertar, luz cálida de salida
            new Color(0.55f, 0.90f, 0.60f),   // B · el gran vacío
            new Color(0.40f, 0.95f, 0.85f),   // C · laboratorio profundo
            new Color(1.00f, 0.55f, 0.35f),   // D · antes de la guardián
            new Color(1.00f, 0.35f, 0.35f),   // E · arena del guardián
            new Color(0.45f, 0.75f, 1.00f),   // F · salida
        };

        static Color ColorFor(int zone)
        {
            if (ZoneColors.Length == 0)
                return Color.white;

            return ZoneColors[
                Mathf.Clamp(zone, 0, ZoneColors.Length - 1)
            ];
        }

        // =============================================================
        // AUDITORÍA
        // =============================================================

        [MenuItem("Eira/Auditar nivel 1", false, 20)]
        public static void Audit()
        {
            Log.Length = 0;

            Say("=== AUDITORIA DEL NIVEL 1 ===");

            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single
            );

            if (!scene.IsValid())
            {
                Say("ERROR: no se pudo abrir " + ScenePath);
                Flush();
                return;
            }

            // La física de editor no está cookteada hasta que se ha simulado
            // alguna vez; sin esto los rayos darían 0 siempre.
            Physics.Simulate(0.02f);

            AuditPlayer();
            AuditChain();
            AuditFloor();
            AuditLighting();
            AuditMaterials();
            AuditEiraActions();

            Say("");
            Say(warnings == 0
                ? "Sin avisos."
                : warnings + " aviso(s). Revisa lo de arriba.");

            Flush();
        }

        static int warnings;

        static void AuditPlayer()
        {
            Say("");
            Say("-- Jugadora --");

            var eira = GameObject.Find("Eira");

            if (eira == null)
            {
                Bad("No esta el GameObject 'Eira'.");
                return;
            }

            if (eira.GetComponent<PlayerController>() == null)
                Bad("Eira no tiene PlayerController.");

            var cc = eira.GetComponent<CharacterController>();

            if (cc == null)
            {
                Bad("Eira no tiene CharacterController.");
            }
            else
            {
                if (Mathf.Abs(cc.center.x) > 0.01f ||
                    Mathf.Abs(cc.center.z) > 0.01f)
                {
                    Bad("La capsula no esta centrada: center = " + cc.center +
                        ". Deberia ser (0, height/2, 0).");
                }
                else
                {
                    Good("Capsula centrada (" + cc.height.ToString("F2") +
                         " m de alto, radio " + cc.radius.ToString("F2") + ").");
                }

                // La caja tiene que encajar con el modelo: si no, la
                // jugadora choca con el aire o atraviesa el suelo.
                //
                // Se busca por la malla de piel y no con Find("visual"): el
                // hijo se llama "EiraBase" y Find distingue mayúsculas, así
                // que la auditoría nunca encontraba el modelo y se callaba.
                var pc = eira.GetComponent<PlayerController>();
                var visual = pc != null && pc.visual != null
                    ? pc.visual
                    : EiraBodyFit.FindVisual(eira.transform);

                if (visual != null && ModelBounds(visual, out Bounds b))
                {
                    float modelHeight = EiraBodyFit.PlausibleHeight(visual, b);
                    float diff = Mathf.Abs(modelHeight - cc.height);

                    if (modelHeight <= 0f)
                    {
                        Bad("La medida del cuerpo de Eira no es creible: no " +
                            "puede comprobarse la capsula.");
                    }
                    else if (diff > 0.05f)
                    {
                        Bad("La capsula no coincide con el modelo: modelo " +
                            modelHeight.ToString("F2") + " m, capsula " +
                            cc.height.ToString("F2") + " m.");
                    }
                    else
                    {
                        Good("La capsula encaja con EiraBase (" +
                             modelHeight.ToString("F2") + " m).");
                    }
                }
            }

            var animator = eira.GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                Bad("Eira no tiene Animator.");
            }
            else if (animator.runtimeAnimatorController == null)
            {
                Bad("El Animator no tiene controller asignado.");
            }
            else
            {
                if (animator.applyRootMotion)
                {
                    Bad("El Animator tiene root motion activo; el Animator se " +
                        "movería a Eira y pelearía con el CharacterController.");
                }
                else
                {
                    Good("Animator con " +
                         animator.runtimeAnimatorController.name +
                         ", sin root motion.");
                }
            }
        }

        static void AuditChain()
        {
            Say("");
            Say("-- Cadena jugable --");

            var logs = Object.FindObjectsByType<DataLog>(
                FindObjectsInactive.Include
            );

            if (logs.Length == EiraConst.InfoLogsNeeded)
            {
                Good("Registros de datos: " + logs.Length + " / " +
                     EiraConst.InfoLogsNeeded + ".");
            }
            else
            {
                Bad("Registros de datos: " + logs.Length + ", se necesitan " +
                    EiraConst.InfoLogsNeeded + ".");
            }

            var checkpoints = Object.FindObjectsByType<CheckpointZone>(
                FindObjectsInactive.Include
            );

            if (checkpoints.Length == 0)
            {
                Bad("No hay checkpoints: si Eira muere, reaparece en el sitio " +
                    "donde murio y puede quedarse encerrada.");
            }
            else
            {
                Good("Checkpoints: " + checkpoints.Length + ".");
            }

            // Un checkpoint cerca de cada registro: si el registro está al
            // otro extremo del nivel, reintentar es un paseo.
            var sorted = new List<CheckpointZone>(checkpoints);
            sorted.Sort((a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            for (int i = 0; i < logs.Length; i++)
            {
                float best = float.MaxValue;

                for (int c = 0; c < checkpoints.Length; c++)
                {
                    float d = Vector3.Distance(
                        logs[i].transform.position,
                        checkpoints[c].transform.position
                    );

                    if (d < best)
                        best = d;
                }

                if (best > 30f)
                {
                    Bad("El registro " + (i + 1) + " esta a " +
                        best.ToString("F0") + " m del checkpoint mas cercano.");
                }
            }

            var panels = Object.FindObjectsByType<PanelActivator>(
                FindObjectsInactive.Include
            );

            if (panels.Length >= 2)
                Good("Consolas activables: " + panels.Length + ".");
            else
                Bad("Solo " + panels.Length + " consola(s) activable(s); " +
                    "hacen falta 2 para el puzle y 3 para el guardian.");

            var doors = Object.FindObjectsByType<DoorControl>(
                FindObjectsInactive.Include
            );

            if (doors.Length > 0)
                Good("Puertas: " + doors.Length + ".");
            else
                Bad("No hay puertas controlables: el puzle no abre nada.");

            if (Object.FindAnyObjectByType<GuardianBoss>() != null)
                Good("Guardian presente.");
            else
                Bad("No esta el Guardian: la salida nunca se desbloquea.");

            if (Object.FindAnyObjectByType<LevelExitZone>() != null)
                Good("Zona de salida presente.");
            else
                Bad("No hay LevelExitZone: el nivel no se puede terminar.");

            if (Object.FindAnyObjectByType<MissionDirector>() != null)
                Good("MissionDirector presente: hay guia de objetivos.");
            else
                Bad("No hay MissionDirector: la jugadora no sabe que hacer.");
        }

        static void AuditFloor()
        {
            Say("");
            Say("-- Suelo --");

            var checkpoints = Object.FindObjectsByType<CheckpointZone>(
                FindObjectsInactive.Include
            );

            if (checkpoints.Length == 0)
            {
                Bad("Sin checkpoints no se puede trazar una ruta que probar.");
                return;
            }

            var sorted = new List<CheckpointZone>(checkpoints);
            sorted.Sort((a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            // Muestreo a lo largo de la ruta: si el suelo tiene un agujero,
            // Eira cae y el nivel es injugable sin que se note en el editor.
            int samples = 0;
            int hits = 0;

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                Vector3 a = sorted[i].transform.position;
                Vector3 b = sorted[i + 1].transform.position;

                int steps = Mathf.Max(
                    4, Mathf.CeilToInt(Vector3.Distance(a, b) / 2f)
                );

                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)s / steps);
                    p.y = 3f;

                    samples++;

                    if (Physics.Raycast(
                        p, Vector3.down, out RaycastHit hit, 8f))
                    {
                        hits++;
                    }
                }
            }

            if (hits == samples)
            {
                Good("Suelo continuo entre checkpoints (" + samples + " muestras).");
            }
            else if (hits > samples * 0.85f)
            {
                Warn("Hay " + (samples - hits) + " hueco(s) en el suelo de la ruta.");
            }
            else
            {
                Bad("Solo " + hits + " de " + samples +
                    " muestras tienen suelo: el nivel tiene agujeros.");
            }
        }

        static void AuditLighting()
        {
            Say("");
            Say("-- Iluminacion --");

            var lights = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include
            );

            int dir = 0;
            int point = 0;

            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional)
                    dir++;
                else if (lights[i].type == LightType.Point)
                    point++;
            }

            if (dir == 0)
                Bad("No hay luz direccional: las zonas cerradas son unagaratas.");
            else
                Good("Luces: " + dir + " direccional(es), " + point + " puntual(es).");

            var checkpoints = Object.FindObjectsByType<CheckpointZone>(
                FindObjectsInactive.Include
            );

            for (int i = 0; i < checkpoints.Length; i++)
            {
                bool lit = false;

                for (int l = 0; l < lights.Length; l++)
                {
                    if (Vector3.Distance(
                        lights[l].transform.position,
                        checkpoints[i].transform.position
                    ) < 14f)
                    {
                        lit = true;
                        break;
                    }
                }

                if (!lit)
                {
                    Warn("Hay una zona de checkpoint a " +
                         checkpoints[i].transform.position +
                         " sin ninguna luz cerca.");
                }
            }
        }

        static void AuditMaterials()
        {
            Say("");
            Say("-- Materiales --");

            var renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include
            );

            int missing = 0;
            var reported = new List<string>();

            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;

                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null)
                    {
                        missing++;

                        if (reported.Count < 5 &&
                            !reported.Contains(renderers[i].name))
                        {
                            reported.Add(renderers[i].name);
                        }

                        break;
                    }
                }
            }

            if (missing == 0)
                Good("Todos los renderers tienen material.");
            else
                Bad(missing + " renderer(s) sin material (sale el color " +
                    "rosa de Unity). Ejemplos: " + string.Join(", ", reported));
        }

        static void AuditEiraActions()
        {
            Say("");
            Say("-- Acciones de Eira --");

            // Las acciones que la jugadora puede hacer y el nivel debe
            // obligar a usar. Si el nivel no pide una acción, esa acción no
            // esta probada y puede fallar sin que nadie se entere.
            var hide = Object.FindObjectsByType<HideSpotZone>(
                FindObjectsInactive.Include
            );

            if (hide.Length > 0)
                Good("Zonas de cobertura: " + hide.Length +
                     " (se puede usar agacharse).");
            else
                Warn("No hay HideSpotZone: la cobertura agachada no se puede " +
                     "usar en ningun sitio del nivel.");

            var drones = Object.FindObjectsByType<DroneController>(
                FindObjectsInactive.Include
            );

            if (drones.Length > 0)
                Good("Drones: " + drones.Length + ".");
            else
                Warn("No hay drones: no hay a quien esquivar ni a quien " +
                     "disparar.");

            if (GameObject.Find("Eira") != null &&
                GameObject.Find("Eira").GetComponent<EiraWeapon>() == null)
            {
                Warn("Eira no tiene EiraWeapon en la raiz; comprueba que " +
                     "dispara (deberia estar en el modelo o en la raiz).");
            }
        }

        // =============================================================
        // PULIDO
        // =============================================================

        [MenuItem("Eira/Pulir nivel 1 (color y ambiente)", false, 21)]
        public static void Polish()
        {
            Log.Length = 0;
            warnings = 0;

            Say("=== PULIDO DEL NIVEL 1 ===");

            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single
            );

            if (!scene.IsValid())
            {
                Say("ERROR: no se pudo abrir " + ScenePath);
                Flush();
                return;
            }

            // Imprescindible antes de lanzar rayos: la física de editor no
            // está cookteada hasta que se ha simulado alguna vez. Sin esto,
            // FloorHeightAt no encontraria nada y todas las balizas y luces
            // quedarian flotando a la altura del checkpoint.
            Physics.Simulate(0.02f);

            // Es idempotente: si se vuelve a pulir, se rehace desde cero en
            // lugar de apilar una segunda copia de todo.
            var old = GameObject.Find(RootName);

            if (old != null)
                Object.DestroyImmediate(old);

            var root = new GameObject(RootName).transform;

            var checkpoints = SortedCheckpoints();
            var bounds = LevelBounds();

            Say("Zonas detectadas: " + checkpoints.Count);
            Say("Extremos del nivel: " + bounds.size);

            BuildZoneLights(root, checkpoints);
            BuildRouteBeacons(root, checkpoints);
            BuildExitBeacon(root);
            BuildAtmosphere(root, bounds);
            TuneWeather();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Say("");
            Say(warnings == 0
                ? "Pulido aplicado sin avisos."
                : warnings + " aviso(s).");

            Flush();
        }

        // ---------------------------------------------------------
        // Utilidades
        // ---------------------------------------------------------

        static List<CheckpointZone> SortedCheckpoints()
        {
            var list = new List<CheckpointZone>(
                Object.FindObjectsByType<CheckpointZone>(
                    FindObjectsInactive.Include
                )
            );

            // De la entrada a la salida. Se ordena por X porque el nivel es
            // un pasillo largo en esa dirección; si alguien lo gira, habra
            // que cambiar esta linea, y por eso esta aislada.
            list.Sort((a, b) =>
                a.transform.position.x.CompareTo(b.transform.position.x));

            return list;
        }

        /// <summary>
        /// Tamaño del nivel a partir de lo que ya hay, no de constantes: si
        /// el escenario crece, el ambiente crece con el.
        /// </summary>
        static Bounds LevelBounds()
        {
            var b = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;

            var logs = Object.FindObjectsByType<DataLog>(
                FindObjectsInactive.Include
            );

            for (int i = 0; i < logs.Length; i++)
            {
                if (any)
                    b.Encapsulate(logs[i].transform.position);
                else
                {
                    b = new Bounds(logs[i].transform.position, Vector3.zero);
                    any = true;
                }
            }

            var exits = Object.FindObjectsByType<LevelExitZone>(
                FindObjectsInactive.Include
            );

            for (int i = 0; i < exits.Length; i++)
            {
                if (any)
                    b.Encapsulate(exits[i].transform.position);
                else
                {
                    b = new Bounds(exits[i].transform.position, Vector3.zero);
                    any = true;
                }
            }

            if (!any)
                b = new Bounds(Vector3.zero, new Vector3(60f, 4f, 20f));

            b.Expand(new Vector3(20f, 10f, 20f));

            return b;
        }

        // ---------------------------------------------------------
        // Color por zona
        // ---------------------------------------------------------

        static void BuildZoneLights(
            Transform root,
            List<CheckpointZone> checkpoints
        )
        {
            Say("");
            Say("-- Luces de zona --");

            var group = new GameObject("ZoneLights").transform;
            group.SetParent(root, false);

            for (int i = 0; i < checkpoints.Count; i++)
            {
                Color color = ColorFor(i);

                // Altura por encima del suelo del nivel. Se deduce de los
                // renders, no se supone: si el suelo esta a 2 m, colgar la
                // luz a 2 m la dejaria dentro del suelo.
                float floorY = FloorHeightAt(
                    checkpoints[i].transform.position, 0f
                );

                var go = new GameObject("ZoneLight" + i, typeof(Light));
                go.transform.SetParent(group, false);
                go.transform.position = new Vector3(
                    checkpoints[i].transform.position.x,
                    floorY + 3.2f,
                    checkpoints[i].transform.position.z
                );

                var l = go.GetComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = 2.6f;
                l.range = 16f;
                l.shadows = LightShadows.None;

                Say("Zona " + (i + 1) + " en x=" +
                    go.transform.position.x.ToString("F0") +
                    " · " + Hex(color));
            }
        }

        // ---------------------------------------------------------
        // Balizas de ruta
        // ---------------------------------------------------------

        /// <summary>
        /// Balizas en el suelo entre zonas. No sustituyen a la flecha del
        /// HUD: solo dan una línea de luz que sigue el suelo, que es lo que
        /// se ve al mirar a los pies y no hacia arriba.
        /// </summary>
        static void BuildRouteBeacons(
            Transform root,
            List<CheckpointZone> checkpoints
        )
        {
            Say("");
            Say("-- Balizas de ruta --");

            var group = new GameObject("RouteBeacons").transform;
            group.SetParent(root, false);

            for (int i = 0; i < checkpoints.Count - 1; i++)
            {
                Vector3 a = checkpoints[i].transform.position;
                Vector3 b = checkpoints[i + 1].transform.position;

                float distance = Vector3.Distance(a, b);
                int count = Mathf.Clamp(
                    Mathf.RoundToInt(distance / 6f), 2, 40
                );

                var mat = NeonMaterial(
                    Color.Lerp(ColorFor(i), ColorFor(i + 1), 0.5f), 2.2f
                );

                for (int s = 0; s <= count; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)s / count);
                    float y = FloorHeightAt(p, 0.04f);

                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = "Beacon";
                    go.transform.SetParent(group, false);
                    go.transform.position = new Vector3(p.x, y, p.z);
                    go.transform.localScale = new Vector3(0.22f, 0.012f, 0.22f);

                    // Sin collider: si lo tuvieran, se harian tripas al
                    // andar por encima.
                    Object.DestroyImmediate(go.GetComponent<Collider>());

                    var r = go.GetComponent<Renderer>();
                    r.material = mat;
                    r.shadowCastingMode =
                        UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                Say("Tramo " + (i + 1) + ": " + (count + 1) +
                    " balizas entre x=" + a.x.ToString("F0") +
                    " y x=" + b.x.ToString("F0"));
            }
        }

        static void BuildExitBeacon(Transform root)
        {
            Say("");
            Say("-- Baliza de salida --");

            var exits = Object.FindObjectsByType<LevelExitZone>(
                FindObjectsInactive.Include
            );

            if (exits.Length == 0)
            {
                Warn("No hay salida: no se puede balizar.");
                return;
            }

            var group = new GameObject("ExitBeacon").transform;
            group.SetParent(root, false);

            var color = ColorFor(ZoneColors.Length - 1);
            var mat = NeonMaterial(color, 3.5f);

            for (int i = 0; i < exits.Length; i++)
            {
                Vector3 p = exits[i].transform.position;
                float y = FloorHeightAt(p, 0.05f);

                // Columna de luz: se ve desde lejos y por encima de las
                // maquinas, que es justo cuando hace falta encontrarla.
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "ExitBeam";
                pillar.transform.SetParent(group, false);
                pillar.transform.position = new Vector3(p.x, y + 3f, p.z);
                pillar.transform.localScale = new Vector3(0.6f, 3f, 0.6f);
                Object.DestroyImmediate(pillar.GetComponent<Collider>());

                var r = pillar.GetComponent<Renderer>();
                r.material = mat;
                r.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;

                Say("Salida balizada en " + p + " · " + Hex(color));
            }
        }

        // ---------------------------------------------------------
        // Ambiente
        // ---------------------------------------------------------

        static void BuildAtmosphere(Transform root, Bounds bounds)
        {
            Say("");
            Say("-- Ambiente --");

            var group = new GameObject("Atmosphere").transform;
            group.SetParent(root, false);

            // Motas de polvo por todo el nivel. El mismo truco que en la
            // intro: sin partículas, los haces de luz se ven como triángulos
            // vacíos.
            var go = new GameObject("LevelDust");
            go.transform.SetParent(group, false);
            go.transform.position = bounds.center;

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 12f;
            main.startSpeed = 0.1f;
            main.startSize = 0.04f;
            main.startColor = new Color(0.75f, 0.88f, 1f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 700;
            main.playOnAwake = true;
            main.gravityModifier = 0.01f;

            var emission = ps.emission;
            emission.rateOverTime = bounds.size.magnitude * 1.2f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = bounds.size;

            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = UnlitMaterial(new Color(0.8f, 0.9f, 1f, 0.25f));
            r.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            // rateOverTime es una curva, no un float: para imprimirla hay
            // que quedarse con la constante.
            Say("Motas de polvo: " +
                emission.rateOverTime.constant.ToString("F0") +
                " por segundo sobre " + bounds.size.x.ToString("F0") + " x " +
                bounds.size.z.ToString("F0") + " m");

            // Bruma baja: separa el suelo del fondo y da sentido de escala al
            // pasillo largo.
            var fogGo = new GameObject("GroundHaze");
            fogGo.transform.SetParent(group, false);
            fogGo.transform.position =
                new Vector3(bounds.center.x, 0.2f, bounds.center.z);
            fogGo.transform.localScale = new Vector3(
                bounds.size.x * 0.9f, 0.4f, bounds.size.z * 0.9f
            );

            var haze = fogGo.AddComponent<ParticleSystem>();

            var hmain = haze.main;
            hmain.startLifetime = 16f;
            hmain.startSpeed = 0.05f;
            hmain.startSize = 1.6f;
            hmain.startColor = new Color(0.35f, 0.5f, 0.55f, 0.06f);
            hmain.simulationSpace = ParticleSystemSimulationSpace.World;
            hmain.maxParticles = 200;
            hmain.playOnAwake = true;

            var hemission = haze.emission;
            hemission.rateOverTime = 6f;

            var hshape = haze.shape;
            hshape.shapeType = ParticleSystemShapeType.Box;
            hshape.scale = bounds.size;

            var hr = haze.GetComponent<ParticleSystemRenderer>();
            hr.renderMode = ParticleSystemRenderMode.Billboard;
            hr.material = UnlitMaterial(new Color(0.4f, 0.55f, 0.6f, 0.07f));
            hr.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            Say("Bruma baja en todo el nivel.");
        }

        static void TuneWeather()
        {
            Say("");
            Say("-- Niebla --");

            // La niebla del constructor estaba puesta para un pasillo corto.
            // Con un nivel largo, la densidad baja de 0.018 a 0.012 evita que
            // la zona lejana sea una pared gris.
            float density = 0.012f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = density;
            RenderSettings.fogColor =
                new Color(0.016f, 0.032f, 0.040f);

            Say("Niebla exponencial, densidad " + density +
                ", color " + Hex(RenderSettings.fogColor) + ".");
        }

        // ---------------------------------------------------------
        // Ayudas de escena
        // ---------------------------------------------------------

        /// <summary>
        /// Altura del suelo bajo un punto. Si no hay suelo, devuelve el valor
        /// de repuesto: es preferible una baliza flotando a que el pulido
        /// pare por un agujero que la auditoria ya ha reportado.
        /// </summary>
        static float FloorHeightAt(Vector3 p, float offset)
        {
            var from = new Vector3(p.x, p.y + 8f, p.z);

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 30f))
                return hit.point.y + offset;

            return p.y + offset;
        }

        static bool ModelBounds(Transform visual, out Bounds local)
        {
            // Tercera copia que se iba a desfasar del resto: la medición
            // canónica está en EiraBodyFit (mallas de piel, pose de
            // referencia). Aquí solo se delega.
            return EiraBodyFit.Measure(visual, out local);
        }

        // ---------------------------------------------------------
        // Materiales
        // ---------------------------------------------------------

        static Shader LitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");

            return sh != null ? sh : Shader.Find("Standard");
        }

        static Shader UnlitShader()
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");

            if (sh != null)
                return sh;

            sh = Shader.Find("Unlit/Color");
            return sh != null ? sh : Shader.Find("Sprites/Default");
        }

        static Material NeonMaterial(Color color, float intensity)
        {
            var m = new Material(LitShader())
            {
                color = color * 0.3f
            };

            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * intensity);
            m.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;

            return m;
        }

        static Material UnlitMaterial(Color color)
        {
            return new Material(UnlitShader()) { color = color };
        }

        // ---------------------------------------------------------
        // Salida
        // ---------------------------------------------------------

        static void Good(string s)
        {
            Log.AppendLine("  OK    " + s);
        }

        static void Warn(string s)
        {
            warnings++;
            Log.AppendLine("  AVISO " + s);
        }

        static void Bad(string s)
        {
            warnings++;
            Log.AppendLine("  FALLO " + s);
        }

        static void Say(string s)
        {
            Log.AppendLine(s);
        }

        static string Hex(Color c)
        {
            return "#" +
                ((int)(c.r * 255f)).ToString("X2") +
                ((int)(c.g * 255f)).ToString("X2") +
                ((int)(c.b * 255f)).ToString("X2");
        }

        static void Flush()
        {
            Debug.Log("[Eira] " + Log.ToString().TrimEnd());
        }
    }
}
