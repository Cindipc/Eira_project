using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    /// <summary>
    /// Reparación de Eira: deja el personaje, la cámara y el Animator en un
    /// estado jugable y coherente.
    ///
    /// Problemas que corrige:
    ///  1. EiraAnimator.controller solo tenía Idle y Walk, con transiciones
    ///     por exit time (la animación se quedaba pegada al andar).
    ///  2. El Animator del modelo no tenía controller asignado.
    ///  3. El CharacterController tenía el centro desplazado 3.87 m en X y
    ///     1 m por debajo, así que la cápsula no coincide con el transform:
    ///     la cámara orbita fuera del personaje y los rayos de suelo fallan.
    ///  4. El modelo estaba compensado a mano para tapar ese error.
    ///  5. Nova arrancaba lejos de Eira y aparecía en medio del suelo.
    ///  6. Los slabs del suelo tenían alturas distintas (-0.5 y 0).
    ///
    /// El script es idempotente: se puede volver a ejecutar sin romper nada.
    /// </summary>
    public static class EiraRepair
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const string ControllerPath = "Assets/Eira/Eira/EiraAnimator.controller";
        const string ClipsFolder = "Assets/Eira/Eira/Animations";

        const float WalkSpeed = 3.6f;
        const float RunSpeed = 6.2f;

        // Altura a la que apoya todo el nivel (botiquines, checkpoints, etc.).
        const float FloorTop = 0f;

        static readonly StringBuilder Log = new StringBuilder();

        // =============================================================
        // PUNTO DE ENTRADA
        // =============================================================

        const string SessionKey = "EiraRepair.Attempted";

        /// <summary>
        /// Se engancha a la recarga de dominio para que la reparación se
        /// aplique sola al abrir el proyecto. Es inocuo: si la escena ya
        /// está correcta no hace nada, y el menú manual sigue disponible.
        /// </summary>
        [InitializeOnLoadMethod]
        static void ScheduleAutoRepair()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            SessionState.SetBool(SessionKey, true);

            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling)
                    return;

                // Nunca se toca una escena con cambios sin guardar: la
                // reparación abre Level1Scene en Single y eso las perdería.
                if (EditorSceneManager.GetActiveScene().isDirty)
                {
                    Debug.LogWarning(
                        "[Eira] Hay cambios sin guardar en la escena activa. " +
                        "Guarda el nivel y pulsa Ctrl+Shift+R para repararlo.");
                    return;
                }

                if (NeedsRepair())
                {
                    Debug.LogWarning(
                        "[Eira] Se detectó el nivel sin reparar. " +
                        "Aplicando 'Eira > Reparar nivel (jugable)' automáticamente. " +
                        "También puedes lanzarlo a mano con Ctrl+Shift+R.");

                    try
                    {
                        RepairAll();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("[Eira] La reparación automática falló: " + e);
                    }
                }
            };
        }

        /// <summary>
        /// Comprueba si el nivel tiene los defectos conocidos. Solo mira
        /// datos de la escena en memoria, no escribe nada.
        /// </summary>
        static bool NeedsRepair()
        {
            GameObject eira = GameObject.Find("Eira");

            if (eira == null)
                return false;

            CharacterController cc = eira.GetComponent<CharacterController>();

            if (cc != null && Mathf.Abs(cc.center.x) > 0.01f)
                return true;

            // La cápsula tiene que coincidir con EiraBase. Si la diferencia
            // es de más de 2 cm es que la caja se escribió a mano en algún
            // momento y quedó desfasada del modelo.
            if (cc != null)
            {
                // Se usa el campo visual del PlayerController y NO
                // transform.Find("visual"): el hijo se llama "EiraBase" y
                // Find es sensible a mayúsculas, así que la búsqueda devolvía
                // null y esta comprobación nunca se cumplía.
                var pc = eira.GetComponent<PlayerController>();
                Transform visual = pc != null ? pc.visual : null;

                if (visual != null &&
                    EiraBodyFit.Measure(visual, out Bounds model) &&
                    Mathf.Abs(EiraBodyFit.PlausibleHeight(visual, model) - cc.height) > 0.02f)
                {
                    return true;
                }
            }

            Animator animator = eira.GetComponentInChildren<Animator>(true);

            if (animator != null && animator.runtimeAnimatorController == null)
                return true;

            // Si el suelo no aguanta, el nivel no es jugable: se repara
            // aunque la escena no tenga ningún otro defecto.
            //
            // Solo si la escena activa ES el nivel. Al abrir el proyecto
            // puede estar IntroScene, y auditar el intro daría un falso
            // "falta el suelo" que dispararía la reparación sin motivo.
            var active = EditorSceneManager.GetActiveScene();

            if (!active.IsValid() || active.name != "Level1Scene")
                return false;

            Physics.Simulate(0.02f);

            FloorAudit audit = AuditFloor();

            return audit.samples > 0 && audit.hits < audit.samples * 0.5f;
        }

        /// <summary>
        /// Deja los clips de animación en condiciones de usefulness.
        ///
        /// Eiradle y EiraWalking ya tienen un clip explícito con Loop Time
        /// activado. EiraRunning y EiraJump no: tienen clipAnimations vacío,
        /// así que Unity genera el clip por defecto, y ese clip viene con Loop
        /// Time DESACTIVADO. Un ciclo de carrera que no se repite se congela
        /// en el último fotograma mientras la cápsula sigue avanzando: es
        /// exactamente la sensación de "se mueve como una piedra".
        ///
        /// Para tocarlo hay que definir un clip explícito. Se siembla desde
        /// defaultClipAnimations, que es donde Unity guarda el take y el
        /// rango de frames correctos del FBX, y luego se ajustan los flags.
        /// No se cambia ni el nombre del take ni el rango, así que el
        /// internalID no cambia y las referencias del controller siguen
        /// apuntando al mismo clip.
        /// </summary>
        static void SanitizeClips()
        {
            Say("--- Clips de animación ---");

            // El de salto no debe repetir: se reproduce una vez y mantiene la
            // pose aérea hasta que se vuelva a Locomotion.
            var wanted = new[]
            {
                new { file = "Eiradle", loop = true },
                new { file = "EiraWalking", loop = true },
                new { file = "EiraRunning", loop = true },
                new { file = "EiraJump", loop = false }
            };

            foreach (var entry in wanted)
            {
                string path = ClipsFolder + "/" + entry.file + ".fbx";

                var importer = AssetImporter.GetAtPath(path) as ModelImporter;

                if (importer == null)
                {
                    Say("  " + entry.file + ": no encontrado.");
                    continue;
                }

                ModelImporterClipAnimation[] clips = importer.clipAnimations;
                bool wasEmpty = clips == null || clips.Length == 0;

                if (wasEmpty)
                {
                    // Se siembra con los valores por defecto del modelo, que
                    // traen el takeName y los frames reales del take.
                    clips = importer.defaultClipAnimations;
                }

                if (clips == null || clips.Length == 0)
                {
                    Say("  " + entry.file + ": el FBX no expone ningún take.");
                    continue;
                }

                bool changed = wasEmpty;

                for (int i = 0; i < clips.Length; i++)
                {
                    var c = clips[i];

                    if (c.loopTime != entry.loop) { c.loopTime = entry.loop; changed = true; }
                    if (c.loop != entry.loop) { c.loop = entry.loop; changed = true; }

                    // Sin esto la animación de caminar sube y baja el cuerpo
                    // con cada paso y acaba leyéndose como que flota.
                    if (entry.loop && !c.keepOriginalPositionY)
                    {
                        c.keepOriginalPositionY = true;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    Say("  " + entry.file + ": ya correcto.");
                    continue;
                }

                importer.clipAnimations = clips;
                importer.SaveAndReimport();

                Say("  " + entry.file + ": " + (wasEmpty ? "clip creado" : "clip corregido") +
                    ", loopTime=" + entry.loop + ", take=" + clips[0].takeName +
                    ", frames " + clips[0].firstFrame + "-" + clips[0].lastFrame + ".");
            }

            // Tras reimportar, los clips son objetos nuevos: hay que releerlos
            // antes de tocar el controller, o se guardarían referencias viejas.
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Comprueba que cada estado del controller tenga un clip válido detrás.
        ///
        /// Una referencia a clip rota no da error: el estado simplemente no
        /// anima nada y el personaje se desliza con la pose de referencia, que
        /// es el fallo más difícil de diagnosticar a simple vista.
        /// </summary>
        static void VerifyMotions(AnimatorController controller)
        {
            if (controller == null)
                return;

            // Los parámetros primero: un Bool declarado como Int hace que
            // EiraAnimationController los descarte en silencio y que las
            // transiciones no se puedan cumplir nunca.
            var expected = new[]
            {
                new { name = "Speed", type = AnimatorControllerParameterType.Float },
                new { name = "Grounded", type = AnimatorControllerParameterType.Bool },
                new { name = "VerticalSpeed", type = AnimatorControllerParameterType.Float },
                new { name = "Jump", type = AnimatorControllerParameterType.Trigger },
                new { name = "Falling", type = AnimatorControllerParameterType.Bool },
                new { name = "Crouch", type = AnimatorControllerParameterType.Bool },
                new { name = "Moving", type = AnimatorControllerParameterType.Bool },
                new { name = "Sprint", type = AnimatorControllerParameterType.Bool },
                new { name = "Strafe", type = AnimatorControllerParameterType.Float }
            };

            var actual = controller.parameters;

            foreach (var e in expected)
            {
                AnimatorControllerParameterType found = AnimatorControllerParameterType.Int;
                bool exists = false;

                for (int i = 0; i < actual.Length; i++)
                {
                    if (actual[i].name != e.name) continue;
                    found = actual[i].type;
                    exists = true;
                    break;
                }

                if (!exists)
                    Say("AVISO: falta el parámetro '" + e.name + "'.");
                else if (found != e.type)
                    Say("AVISO: '" + e.name + "' es " + found + " y debería ser " + e.type + ".");
            }

            if (controller.layers.Length == 0)
            {
                Say("AVISO: el controller no tiene capas.");
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            if (machine == null)
                return;

            foreach (ChildAnimatorState child in machine.states)
            {
                AnimatorState state = child.state;

                if (state == null)
                    continue;

                if (state.motion == null)
                {
                    Say("AVISO: el estado '" + state.name +
                        "' se queda sin clip (referencia rota): no animará nada.");
                    continue;
                }

                if (state.motion is AnimationClip clip)
                {
                    Say("  '" + state.name + "' -> " + clip.name +
                        " (" + clip.length.ToString("F2") + " s, loop=" +
                        clip.isLooping + ")");
                }
                else
                {
                    Say("  '" + state.name + "' -> " + state.motion.GetType().Name);
                }
            }
        }

        [MenuItem("Eira/Reparar nivel (jugable) %#r")]
        public static void RepairAll()
        {
            Log.Length = 0;

            Say("=== REPARACIÓN DE EIRA ===");

            // Primero los clips y sus flags, y luego el controller: al
            // reimportar, los clips cambian de objeto y el controller se
            // tiene que reconstruir con los nuevos.
            SanitizeClips();

            AnimationClip idle = FindClip("Eiradle");
            AnimationClip walk = FindClip("EiraWalking");
            AnimationClip run = FindClip("EiraRunning");
            AnimationClip jump = FindClip("EiraJump");

            if (idle == null || walk == null || run == null || jump == null)
            {
                Say("ERROR: faltan clips de animación. No se toca nada más.");
                Flush();
                return;
            }

            Say("Clips: idle=" + idle.name + " walk=" + walk.name +
                " run=" + run.name + " jump=" + jump.name);

            RebuildAnimatorController(idle, walk, run, jump);

            VerifyMotions(
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            );

            RepairScene(idle, walk, run, jump);

            Flush();
        }

        [MenuItem("Eira/Informar suelo y colisiones %#I")]
        public static void ReportOnly()
        {
            Log.Length = 0;

            Say("=== INFORME DE SUELO Y COLISIONES (no modifica nada) ===");

            if (!Application.isPlaying)
            {
                // La física de editor no está cookteada hasta que se ha
                // simulado alguna vez; sin esto los rayos darían 0 siempre.
                Physics.Simulate(0.02f);
            }

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            EnsureFloor();
            ReportPassages();

            Flush();
        }

        // =============================================================
        // ANIMATOR CONTROLLER
        // =============================================================

        static void RebuildAnimatorController(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip jump)
        {
            // Se reconstruye el asset EN EL SITIO (mismo GUID) para no romper
            // las referencias que ya apuntan a él.
            AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                Say("Animator controller creado.");
            }
            else
            {
                controller.layers = new AnimatorControllerLayer[0];
                Say("Animator controller anterior vaciado.");
            }

            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Falling", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Crouch", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Sprint", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Strafe", AnimatorControllerParameterType.Float);

            AnimatorStateMachine machine = new AnimatorStateMachine
            {
                name = "Base Layer",
                hideFlags = HideFlags.HideInHierarchy
            };

            AssetDatabase.AddObjectToAsset(machine, controller);

            AnimatorControllerLayer layer = new AnimatorControllerLayer
            {
                name = "Base Layer",
                defaultWeight = 1f,
                stateMachine = machine
            };

            controller.AddLayer(layer);

            // ---- Locomotion: blend 1D en m/s --------------------------
            BlendTree locomotion = new BlendTree
            {
                name = "Locomotion",
                hideFlags = HideFlags.HideInHierarchy,
                blendParameter = "Speed",
                blendType = BlendTreeType.Simple1D,
                useAutomaticThresholds = false
            };

            AssetDatabase.AddObjectToAsset(locomotion, controller);

            locomotion.AddChild(idle, 0f);
            locomotion.AddChild(walk, WalkSpeed);
            locomotion.AddChild(run, RunSpeed);

            AnimatorState moveState = machine.AddState("Locomotion", new Vector3(300f, 0f, 0f));
            moveState.motion = locomotion;
            moveState.writeDefaultValues = false;

            // ---- Jump -------------------------------------------------
            AnimatorState jumpState = machine.AddState("Jump", new Vector3(300f, 180f, 0f));
            jumpState.motion = jump;
            jumpState.writeDefaultValues = false;
            // Un Mixamo de salto dura ~1s: al 1.15x termina justo al aterrizar.
            jumpState.speed = 1.15f;

            // ---- Fall -------------------------------------------------
            // No hay clip de caída en el proyecto: se reaprovecha el de salto.
            // A 1x, no a más velocidad: el clip de salto ya empieza con las
            // piernas recogidas, y acelerarlo lo convertía en un bucle
            // nervioso que parecía una deformación del modelo.
            AnimatorState fallState = machine.AddState("Fall", new Vector3(540f, 180f, 0f));
            fallState.motion = jump;
            fallState.writeDefaultValues = false;
            fallState.speed = 1f;

            // ---- Transiciones ----------------------------------------
            // NINGUNA transición usa exit time. Exit time significa "espera a
            // que el clip haya advanced X% antes de salir", y eso es
            // exactamente lo que hacía que Eira siguiera caminando un rato
            // después de soltar la tecla: la animación iba por su cuenta, no
            // por el estado de la física. Con hasExitTime = false la
            // animación reacciona en el mismo frame al estado real.
            AnimatorStateTransition anyToJump = machine.AddAnyStateTransition(jumpState);
            anyToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            anyToJump.hasExitTime = false;
            anyToJump.duration = 0.05f;
            anyToJump.canTransitionToSelf = false;

            // Jump -> Locomotion en cuanto toca suelo (nada de esperar al 70%
            // del clip de salto, que es lo que dejaba a Eira "aterrizando"
            // mientras ya andaba).
            AnimatorStateTransition jumpToMove = jumpState.AddTransition(moveState);
            jumpToMove.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            jumpToMove.hasExitTime = false;
            jumpToMove.duration = 0.1f;
            jumpToMove.hasFixedDuration = true;

            // Jump -> Fall en cuanto empieza a caer, sin esperar a que se
            // agote el clip.
            AnimatorStateTransition jumpToFall = jumpState.AddTransition(fallState);
            jumpToFall.AddCondition(AnimatorConditionMode.If, 0f, "Falling");
            jumpToFall.hasExitTime = false;
            jumpToFall.duration = 0.1f;
            jumpToFall.hasFixedDuration = true;

            // Fall -> Locomotion al tocar suelo.
            AnimatorStateTransition fallToMove = fallState.AddTransition(moveState);
            fallToMove.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            fallToMove.hasExitTime = false;
            fallToMove.duration = 0.15f;
            fallToMove.hasFixedDuration = true;

            // Locomotion -> Jump por si se salta desde el propio suelo sin
            // haber pasado por un estado airborne antes.
            AnimatorStateTransition moveToJump = moveState.AddTransition(jumpState);
            moveToJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            moveToJump.hasExitTime = false;
            moveToJump.duration = 0.05f;
            moveToJump.hasFixedDuration = true;

            machine.defaultState = moveState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Say("Animator: Locomotion(Idle/Walk/Run en 1D sobre Speed m/s) " +
                "+ Jump + Fall. Default = Locomotion.");
        }

        // =============================================================
        // ESCENA
        // =============================================================

        static void RepairScene(
            AnimationClip idle,
            AnimationClip walk,
            AnimationClip run,
            AnimationClip jump)
        {
            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Los MeshCollider de los prefabs no se consultan correctamente
            // en modo editor hasta que la física se ha simulado una vez: sin
            // esto los rayos del audit darían "no hay suelo" siempre.
            if (!Application.isPlaying)
                Physics.Simulate(0.02f);

            // ------------------------------------------------------
            // 1. Suelo del corredor: audit real de física
            // ------------------------------------------------------
            EnsureFloor();

            // ------------------------------------------------------
            // 1b. Pasos y huecos: solo informa, no cambia geometría
            // ------------------------------------------------------
            ReportPassages();

            // ------------------------------------------------------
            // 2. Eira
            // ------------------------------------------------------
            GameObject eira = GameObject.Find("Eira");

            if (eira == null)
            {
                Say("ERROR: no se encontró el GameObject 'Eira'.");
                return;
            }

            PlayerController player = eira.GetComponent<PlayerController>();

            if (player == null)
            {
                Say("ERROR: Eira no tiene PlayerController.");
                return;
            }

            CharacterController cc = eira.GetComponent<CharacterController>();

            // Modelo: se alinea con la cápsula midiendo los bounds reales.
            Transform visual = player.visual != null
                ? player.visual
                : EiraBodyFit.FindVisual(eira.transform);

            if (cc != null && visual != null)
            {
                FitEiraBody(cc, visual);
            }
            else
            {
                if (cc != null)
                {
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
                    cc.slopeLimit = 50f;
                    cc.stepOffset = 0.4f;
                    cc.skinWidth = 0.04f;
                    cc.minMoveDistance = 0f;

                    Say("AVISO: sin modelo, la cápsula queda solo normalizada.");
                }

                if (visual == null)
                    Say("AVISO: no se encontró el modelo hijo de Eira.");
            }

            // Animator: se le asigna el controller y se quita el root motion.
            Animator animator = eira.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                AnimatorController controller =
                    AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                Say("Eira Animator (" + animator.gameObject.name + "): controller " +
                    (controller != null ? controller.name : "NULL") +
                    ", rootMotion off, culling AlwaysAnimate.");
            }
            else
            {
                Say("AVISO: Eira no tiene Animator en la jerarquía.");
            }

            // EiraAnimationController: el puente hacia el Animator.
            EiraAnimationController driver =
                eira.GetComponentInChildren<EiraAnimationController>(true);

            if (driver == null)
                driver = eira.AddComponent<EiraAnimationController>();

            Say("EiraAnimationController: " +
                (driver != null ? "presente en " + driver.gameObject.name : "NO se pudo crear."));

            // Cámara: valores de trabajo.
            FirstPersonCamera cam =
                UnityEngine.Object.FindAnyObjectByType<FirstPersonCamera>();

            if (cam != null)
                TuneCamera(cam, eira.transform);
            else
                Say("AVISO: no se encontró FirstPersonCamera.");

            // Puntos de aparición.
            PlaceEira(eira);
            PlaceNova();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Say("Escena guardada: " + ScenePath);
        }

        // -------------------------------------------------------------
        // SUELO: AUDIT REAL DE FISICA
        // -------------------------------------------------------------

        /// <summary>
        /// Comprueba el suelo con rayos DE VERDAD, no leyendo alturas
        /// declaradas: el nivel llega de un prefab y sus mallas pueden tener
        /// bounds que no coinciden con lo que la escena dice.
        ///
        /// Barre una rejilla por todo el corredor y cuenta cuántas muestras
        /// apoyan a la altura correcta. Si casi ninguna apoya, el suelo
        /// falta; en ese caso se activan los slabs del corredor, que en el
        /// archivo están con m_IsActive: 0 y por eso no colisionan.
        /// </summary>
        static FloorAudit AuditFloor()
        {
            var audit = new FloorAudit();

            const float fromY = 8f;
            const float toY = -14f;

            for (float x = -2f; x <= 124f; x += 2f)
            {
                for (int i = 0; i < ZSamples.Length; i++)
                {
                    float z = ZSamples[i];
                    var origin = new Vector3(x, fromY, z);

                    if (Physics.Raycast(
                            origin,
                            Vector3.down,
                            out RaycastHit hit,
                            fromY - toY,
                            ~0,
                            QueryTriggerInteraction.Ignore))
                    {
                        audit.samples++;
                        audit.hits++;

                        float y = hit.point.y;

                        if (Mathf.Abs(y - FloorTop) <= 0.35f)
                            audit.onFloor++;

                        if (hit.point.y < audit.lowestY)
                            audit.lowestY = hit.point.y;

                        if (audit.hitName == null)
                            audit.hitName = hit.collider.name;
                    }
                }
            }

            audit.slabCount = CountCorridorSlabs();

            return audit;
        }

        static readonly float[] ZSamples = { -8f, -4f, 0f, 4f, 8f };

        /// <summary>
        /// Decide si el suelo falta y, si falta, lo arregla. Es la parte que
        /// hace que Eira no flote ni se hunda: sin ella el juego depende de
        /// que el prefab traiga el suelo puesto, y aquí no lo trae.
        /// </summary>
        static void EnsureFloor()
        {
            FloorAudit audit = AuditFloor();

            Say("Suelo: " + audit.hits + "/" + audit.samples +
                " muestras con apoyo, " + audit.onFloor + " a la altura correcta" +
                (audit.hitName != null ? ", primero golpea '" + audit.hitName + "'" : "") +
                ", slabs de corredor: " + audit.slabCount + ".");

            // Criterio: si menos de la mitad de las muestras apoya, el suelo
            // no está. Se exige que falte de verdad para no activar slabs
            // encima de un suelo que ya funciona.
            bool floorMissing =
                audit.samples > 0 &&
                audit.hits < audit.samples * 0.5f;

            if (floorMissing)
            {
                Say("Suelo: NO HAY SUELO CONTINUO. Activando slabs del corredor.");

                int n = ActivateFloorSlabs();

                Say("Suelo: " + n + " slabs activados y alineados a Y=" + FloorTop + ".");

                // Second pass: comprobar que ahora sí apoya.
                FloorAudit after = AuditFloor();

                Say("Suelo tras reparar: " + after.hits + "/" + after.samples +
                    " muestras con apoyo, " + after.onFloor + " a la altura correcta.");

                if (after.hits < after.samples * 0.8f)
                {
                    Say("AVISO: siguen faltando zonas de suelo. " +
                        "Revisa que el prefab de entorno tenga malla de suelo.");
                }
            }
            else if (audit.onFloor == 0 && audit.hits > 0)
            {
                Say("AVISO: hay suelo, pero NO está a Y=" + FloorTop +
                    ". Eira spawnearía por encima o por debajo.");
            }
        }

        static void ReportPassages()
        {
            List<string> holes = AuditHoles();

            if (holes.Count == 0)
            {
                Say("Huecos: ninguno en el recorrido principal.");
            }
            else
            {
                Say("Huecos: " + holes.Count + " muestras sin suelo.");

                int max = Mathf.Min(holes.Count, 12);

                for (int i = 0; i < max; i++)
                    Say("  " + holes[i]);

                if (holes.Count > max)
                    Say("  ... y " + (holes.Count - max) + " más.");
            }

            List<string> passages = AuditPassages();

            if (passages.Count == 0)
            {
                Say("Pasos: ningún estrechamiento por debajo de 1.1 m.");
            }
            else
            {
                Say("Pasos: " + passages.Count + " puntos estrechos.");

                int max = Mathf.Min(passages.Count, 12);

                for (int i = 0; i < max; i++)
                    Say("  " + passages[i]);
            }
        }

        /// <summary>
        /// Cuenta los slabs del corredor,ACTIVOS e INACTIVOS. Son los
        /// candidatos a suelo si el audit no encuentra apoyo.
        /// </summary>
        static int CountCorridorSlabs()
        {
            int n = 0;

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (IsCorridorSlab(t))
                    n++;
            }

            return n;
        }

        /// <summary>
        /// Un slab de corredor es un cubo ancho, de poco grosor, colgado del
        /// padre del corredor. Se buscan en toda la jerarquía (incluidos
        /// inactivos) porque en el archivo están desactivados.
        /// </summary>
        static bool IsCorridorSlab(Transform t)
        {
            if (t == null || t.gameObject.GetComponent<Collider>() == null)
                return false;

            Vector3 s = t.lossyScale;

            if (s.x < 15f || s.z < 20f || s.y > 1.5f)
                return false;

            // El recorrido del nivel va por X: los slabs se solapan en X
            // para no dejar juntas.
            return s.x > s.z;
        }

        /// <summary>
        /// Activa y alinea los slabs del corredor para que su cara superior
        /// quede en Y = FloorTop. Es idempotente.
        /// </summary>
        static int ActivateFloorSlabs()
        {
            int fixedCount = 0;

            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (!IsCorridorSlab(t))
                    continue;

                GameObject go = t.gameObject;

                if (!go.activeSelf)
                {
                    go.SetActive(true);
                    Say("Suelo: slab '" + go.name + "' activado (estaba inactivo y no colisionaba).");
                }

                Vector3 s = t.lossyScale;

                // Se escribe la posición local para no romper la jerarquía:
                // el padre puede tener su propia rotación.
                float topWorld = t.position.y + s.y * 0.5f;
                float delta = FloorTop - topWorld;

                if (Mathf.Abs(delta) > 0.01f)
                {
                    Vector3 local = t.parent != null
                        ? t.parent.InverseTransformPoint(t.position + Vector3.up * delta)
                        : t.position + Vector3.up * delta;

                    t.localPosition = local;
                }

                fixedCount++;
            }

            return fixedCount;
        }

        // -------------------------------------------------------------
        // PASOS Y HUECOS
        // -------------------------------------------------------------

        /// <summary>
        /// Mide el hueco libre a la altura del pecho de Eira a lo largo del
        /// corredor, y detecta donde se estrecha demasiado para su cápsula.
        ///
        /// Se mide con una esfera del tamaño de la cápsula (radio 0.32) a
        /// 0.9 m, la altura a la que va el centro del cuerpo. Un sitio es
        /// "imposible de atravesar" si el clearance es menor que el radio.
        /// </summary>
        static List<string> AuditPassages()
        {
            var problems = new List<string>();
            const float chest = 0.9f;
            const float radius = 0.32f;
            const float comfortable = 1.1f;

            for (float x = 2f; x <= 122f; x += 4f)
            {
                for (int s = 0; s < 2; s++)
                {
                    // Se mide hacia +Z y hacia -Z desde el centro del pasillo.
                    float z0 = 0f;
                    var dir = s == 0 ? Vector3.forward : Vector3.back;

                    if (!Physics.Raycast(
                            new Vector3(x, chest, z0),
                            dir,
                            out RaycastHit hit,
                            14f,
                            ~0,
                            QueryTriggerInteraction.Ignore))
                        continue;

                    float gap = hit.distance;

                    if (gap < comfortable)
                    {
                        problems.Add(
                            "Paso estrecho en x=" + x.ToString("F0") +
                            " (" + (s == 0 ? "+Z" : "-Z") +
                            "): hueco de " + gap.ToString("F2") + " m" +
                            (gap <= radius
                                ? "  <-- IMPOSIBLE de atravesar"
                                : "  <-- muy justo"));
                    }
                }
            }

            return problems;
        }

        /// <summary>
        /// Detecta puntos del corredor SIN suelo debajo. Son los huecos por
        /// los que Eira se cae al vacío.
        /// </summary>
        static List<string> AuditHoles()
        {
            var holes = new List<string>();

            for (float x = 0f; x <= 124f; x += 2f)
            {
                for (int i = 0; i < ZSamples.Length; i++)
                {
                    var origin = new Vector3(x, 6f, ZSamples[i]);

                    bool found = Physics.Raycast(
                        origin,
                        Vector3.down,
                        out RaycastHit hit,
                        20f,
                        ~0,
                        QueryTriggerInteraction.Ignore);

                    if (!found)
                        holes.Add(
                            "SIN SUELO en x=" + x.ToString("F0") +
                            " z=" + ZSamples[i].ToString("F0"));
                }
            }

            return holes;
        }

        struct FloorAudit
        {
            public int samples;
            public int hits;
            public int onFloor;
            public int slabCount;
            public float lowestY;
            public string hitName;
        }

        // -------------------------------------------------------------
        // MODELO
        // -------------------------------------------------------------

        /// <summary>
        /// Ajusta la cápsula de Eira al modelo real y deja el modelo con los
        /// pies en el suelo.
        ///
        /// Antes la caja se escribía a mano (height 1.8, radius 0.32) y luego
        /// se metía el modelo dentro. Con eso el gizmo verde que dibuja Unity
        /// no coincidía con el personaje: le sobraba por encima de la cabeza.
        /// Ahora se mide EiraBase (incluidas las placas y las botas) y la
        /// cápsula se deriva de esa medida, así que ambos encajan.
        /// </summary>
        static void FitEiraBody(CharacterController cc, Transform visual)
        {
            // Misma medición que el juego (EiraBodyFit): así lo que se ve en
            // el editor y lo que ocurre en Awake no pueden divergir. Antes
            // estaban duplicados y el editor medía con Renderer.bounds
            // (pose animada) mientras el juego hacía lo mismo por su cuenta.
            if (!EiraBodyFit.Measure(visual, out Bounds local))
            {
                if (cc != null)
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

                Say("AVISO: no se ha podido medir el cuerpo de Eira (sin " +
                    "malla de piel ni bounds utilizable). No se ha tocado la " +
                    "capsula ni el modelo.");
                return;
            }

            if (EiraBodyFit.PlausibleHeight(visual, local) <= 0f)
            {
                if (cc != null)
                    cc.center = new Vector3(0f, cc.height * 0.5f, 0f);

                Say("AVISO: la medida del cuerpo de Eira no es creible (" +
                    EiraBodyFit.WorldSize(visual, local).y.ToString("F3") +
                    " m de alto). No se ha tocado la capsula ni el modelo.");
                return;
            }

            Vector3 beforeCenter = cc != null ? cc.center : Vector3.zero;
            float beforeHeight = cc != null ? cc.height : 0f;
            float beforeRadius = cc != null ? cc.radius : 0f;

            EiraBodyFit.Apply(
                visual,
                cc,
                0.62f,
                0.02f,
                2.1f,
                true,
                true
            );

            if (cc == null)
                return;

            Say("Eira cuerpo: capsula " + beforeHeight.ToString("F3") + "h/" +
                beforeRadius.ToString("F3") + "r -> " + cc.height.ToString("F3") + "h/" +
                cc.radius.ToString("F3") + "r, center " + beforeCenter + " -> " + cc.center);

            Say("Eira modelo: bounds " + EiraBodyFit.WorldSize(visual, local).ToString("F3") +
                " m, localPosition " + visual.localPosition.ToString("F3") +
                " (pies en el suelo, cuerpo centrado).");
        }

        // MeasureLocalBounds queda por compatibilidad interna, pero ya no se usa.
        // La medida canónica está en EiraBodyFit.Measure (solo mallas de piel).

        static void AlignVisual(Transform visual)
        {
            FitEiraBody(null, visual);
        }

        // -------------------------------------------------------------
        // CÁMARA
        // -------------------------------------------------------------

        static void TuneCamera(
            FirstPersonCamera cam,
            Transform eira)
        {
            SerializedObject so = new SerializedObject(cam);

            SetPrivate(so, "eyeOffset", new Vector3(0f, 1.6f, 0.1f));
            SetPrivate(so, "mouseSensitivity", 0.12f);
            SetPrivate(so, "minPitch", -85f);
            SetPrivate(so, "maxPitch", 85f);
            SetPrivate(so, "rotationSmooth", 20f);
            SetPrivate(so, "positionSmooth", 25f);
            SetPrivate(so, "baseFov", 65f);
            SetPrivate(so, "sprintFov", 75f);
            SetPrivate(so, "fovSmooth", 10f);
            SetPrivate(so, "enableBobbing", true);
            SetPrivate(so, "walkBobAmount", 0.05f);
            SetPrivate(so, "walkBobSpeed", 8f);
            SetPrivate(so, "runBobAmount", 0.08f);
            SetPrivate(so, "runBobSpeed", 12f);
            SetPrivate(so, "collisionRadius", 0.1f);

            so.ApplyModifiedPropertiesWithoutUndo();

            // Coloca la cámara en la posición de los ojos de Eira
            cam.transform.position = eira.position + new Vector3(0f, 1.6f, 0.1f);
            cam.transform.rotation = Quaternion.Euler(0f, eira.eulerAngles.y, 0f);
            cam.Yaw = eira.eulerAngles.y;
            cam.Pitch = 0f;
            cam.SnapBehind();

            Say("Cámara (FirstPerson): eyeOffset 1.6, sensibilidad 0.12, FOV 65->75 al correr, bobbing activado, target = Eira.");
        }

        static void SetPrivate(SerializedObject so, string name, float value)
        {
            SerializedProperty p = so.FindProperty(name);

            if (p == null)
                return;

            p.floatValue = value;
        }

        static void SetPrivate(SerializedObject so, string name, Vector3 value)
        {
            SerializedProperty p = so.FindProperty(name);

            if (p == null)
                return;

            p.vector3Value = value;
        }

        static void SetPrivate(SerializedObject so, string name, bool value)
        {
            SerializedProperty p = so.FindProperty(name);

            if (p == null)
                return;

            p.boolValue = value;
        }

        // -------------------------------------------------------------
        // SPAWNS
        // -------------------------------------------------------------

        static void PlaceEira(GameObject eira)
        {
            // Centro del corredor inicial, mirando hacia +X (hacia la salida).
            Vector3 position = new Vector3(6f, 1.2f, 0f);
            Quaternion rotation = Quaternion.Euler(0f, 90f, 0f);

            eira.transform.position = position;
            eira.transform.rotation = rotation;

            Say("Eira: spawn en " + position + " con yaw 90 (mirando por el corredor).");
        }

        static void PlaceNova()
        {
            GameObject eira = GameObject.Find("Eira");
            NovaCompanion nova = UnityEngine.Object.FindAnyObjectByType<NovaCompanion>();

            if (eira == null || nova == null)
            {
                Say("AVISO: no se pudo colocar NOVA.");
                return;
            }

            Vector3 slot =
                eira.transform.position -
                eira.transform.forward * 1.5f +
                eira.transform.right * 1.1f;

            nova.transform.position = slot;
            nova.transform.rotation = eira.transform.rotation;

            Say("NOVA: reubicada junto a Eira en " + slot.ToString("F2") + ".");
        }

        // =============================================================
        // UTILIDADES
        // =============================================================

        static AnimationClip FindClip(string fileName)
        {
            string path = ClipsFolder + "/" + fileName + ".fbx";

            if (!File.Exists(path))
                return null;

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                AnimationClip clip = o as AnimationClip;

                if (clip == null)
                    continue;

                // Se descartan los clips internos del importador.
                if (clip.name.StartsWith("__"))
                    continue;

                if (clip.length < 0.1f)
                    continue;

                return clip;
            }

            return null;
        }

        static void Say(string message)
        {
            Log.AppendLine(message);
        }

        static void Flush()
        {
            Debug.Log(Log.ToString());
        }
    }
}
