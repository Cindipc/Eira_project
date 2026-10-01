using UnityEngine;

namespace EiraGame
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        // =========================================================
        // MOVIMIENTO
        // =========================================================

        [Header("Movement")]
        public float walkSpeed = 3.6f;
        public float runSpeed = 6.2f;
        public float crouchSpeed = 1.7f;

        [Tooltip("Aceleración horizontal en el suelo (m/s²).")]
        public float groundAcceleration = 32f;

        [Tooltip("Aceleración horizontal en el aire (m/s²).")]
        public float airAcceleration = 12f;

        [Tooltip("Velocidad vertical inicial del salto.")]
        public float jumpVelocity = 8.2f;

        [Tooltip("Segundos de margen para saltar tras abandonar el suelo.")]
        public float coyoteTime = 0.12f;

        [Tooltip("Segundos en los que un salto pulsado antes de tocar suelo se guarda.")]
        public float jumpBuffer = 0.14f;

        [Tooltip("Proporción del impulso de salto que se conserva al soltar el botón.")]
        [Range(0.1f, 1f)]
        public float jumpCutMultiplier = 0.45f;

        [Tooltip("Segundos tras despegar en los que se ignora el contacto con el suelo. Sin esto, cc.isGrounded y el sondeo siguen tocando el suelo dos o tres frames y la gravedad no actúa: el salto parece un rebote.")]
        public float takeoffGrace = 0.12f;

        [Tooltip("Segundos tras el aterrizaje en los que no se acepta un salto nuevo. Evita encadenar dos saltos por una pulsación larga.")]
        public float landingLock = 0.08f;

        [Tooltip("Distancia del rayo bajo los pies para detectar suelo (slope-safe).")]
        public float groundProbeDistance = 0.22f;

        [Tooltip("Distancia a la que Eira se pega al suelo al bajar peldaños o al cruzar los bordes de los slabs, para no quedarse flotando un frame.")]
        public float snapToGroundDistance = 0.4f;

        [Tooltip("Antiatravesar muros: si hay pared delante, se anula la velocidad que empuja contra ella. 0 = desactivado.")]
        public float wallBlockDistance = 0.25f;

        [Tooltip("Fuerza de gravedad.")]
        public float gravity = 22f;

        [Tooltip("Velocidad de giro de Eira.")]
        public float rotationSpeed = 12f;

        [Tooltip("Giro máximo que Eira da para encarar la dirección de marcha. Si tendría que girar más que esto, se desplaza de lado hacia donde mira en vez de dar media vuelta de golpe. Es lo que quita el latigazo de 90° al pulsar una flecha.")]
        [Range(10f, 180f)]
        public float turnAssistLimit = 55f;

        [Header("Steps")]
        [Tooltip("Distancia mínima entre pasos al caminar/correr.")]
        public float footstepIntervalWalk = 0.46f;
        public float footstepIntervalRun = 0.29f;

        // =========================================================
        // HABILIDAD
        // =========================================================

        [Header("Ability")]
        public float abilityRadius = 4.6f;
        public float abilityCost = 12f;
        public float heartRegen = 1.6f;
        public float heartDanger = 15f;
        public float pulseInterval = 0.35f;

        // =========================================================
        // VISUAL
        // =========================================================

        [Header("Visual")]
        public Transform visual;

        [SerializeField]
        private Animator animator;

        [Tooltip("Script que traduce el estado de Eira al Animator.")]
        [SerializeField]
        private EiraAnimationController animationDriver;

        [Tooltip("Desactiva Root Motion para que la física controle a Eira.")]
        public bool disableRootMotion = true;

        [Tooltip("Alinea el modelo con la cápsula: pies en el suelo y cuerpo centrado en XZ.")]
        public bool autoAlignVisual = true;

        [Tooltip("Ajusta la cápsula al modelo real (EiraBase) en vez de usar los valores a mano. Es lo que hace que la cápsula verde de Unity coincida con el personaje.")]
        public bool fitCapsuleToModel = true;

        [Tooltip("Radio de la cápsula como fracción de la anchura real del modelo. 1 = el modelo cabe justo, 0.6 = deja margen para atravesar puertas.")]
        [Range(0.4f, 1.2f)]
        public float capsuleRadiusFactor = 0.62f;

        [Tooltip("Margen entre los pies del modelo y el suelo de la cápsula.")]
        public float capsuleFootMargin = 0.02f;

        [Tooltip("Altura máxima de la cápsula: evita que un modelo muy alto atraviese el techo de una puerta.")]
        public float capsuleMaxHeight = 2.1f;

        // =========================================================
        // STEALTH
        // =========================================================

        [Header("Stealth")]
        public float hideZoneLevel;

        [Header("Crouch")]
        [SerializeField] private float crouchHeight = 1.05f;
        [SerializeField] private float crouchHeightSpeed = 7f;

        [Header("Anti atascos")]
        [Tooltip("Segundos que Eira puede pasar queriendo andar sin avanzar antes de que se intente sacarla de la geometría. Antes esto no existía: si la cápsula se quedaba dentro de un collider ya no respondía a ninguna tecla para siempre.")]
        public float unstickDelay = 0.35f;

        [Tooltip("Distancia máxima a la que se empuja a Eira para sacarla de un collider.")]
        public float depenetrationRange = 0.6f;

        [Tooltip("Si no encuentra suelo por debajo en este margen, da a Eira por perdida y la devuelve al ultimo punto firme. Cubre los huecos del suelo.")]
        public float groundWatchdogDistance = 12f;

        [Tooltip("Margen por debajo del suelo por debajo del cual se considera que Eira esta cayendo al vacio.")]
        public float fallLimit = -8f;

        [Header("Camera")]
        [SerializeField] private ProfessionalThirdPersonCamera camera;

        [Tooltip("Arma de Eira. Se busca sola si no se asigna.")]
        [SerializeField] private EiraWeapon weapon;

        public EiraWeapon Weapon => weapon;

        public bool IsCrouching { get; private set; }

        public float Health { get; private set; }

        // =========================================================
        // ESTADO DE MOVIMIENTO
        // =========================================================

        [Header("Debug Movement")]
        [SerializeField]
        private bool isGrounded;

        [SerializeField]
        private bool isJumping;

        [SerializeField]
        private bool isFalling;

        [SerializeField]
        private float verticalVelocity;

        public bool IsGrounded => isGrounded;
        public bool IsJumping => isJumping;
        public bool IsFalling => isFalling;

        private bool isSprinting;
        private float lastGroundedTime = -99f;
        private float jumpPressedTime = -99f;
        private float footstepDistance;
        private float crouchHeightCurrent = 1.75f;
        private float standHeight = 1.75f;
        private Vector2 localMoveDirection;

        // Un salto por vuelo. Sin este contador, el buffer de entrada + el
        // coyote time podían disparar un segundo salto en el mismo aire: la
        // pulsación se guardaba, Eira aterrizaba poco después y salía otro
        // salto de la misma pulsación.
        private int airborneJumps;
        private float takeoffTime = -99f;
        private float landingTime = -99f;
        private bool jumpCutApplied;
        private bool hasMoveInput;

        // Anti atascos: cuánto tiempo lleva Eira queriendo andar sin avanzar,
        // y el ultimo sitio con suelo firme. Sin esto una penetracion en un
        // collider la dejaba clavada para siempre.
        private float stuckTimer;
        private float escapeCooldown;
        private Vector3 lastProgressPosition;
        private Vector3 lastSafePosition;
        private float groundWatchdog;

        // Buffers reutilizados: las consultas de fisica de este frame no
        // pueden generar basura cada vez.
        private readonly RaycastHit[] wallHits = new RaycastHit[8];
        private readonly Collider[] overlapHits = new Collider[16];

        public bool IsSprinting => isSprinting;
        public Vector2 LocalMoveDirection => localMoveDirection;

        // =========================================================
        // COMPONENTES
        // =========================================================

        private CharacterController cc;

        private Vector3 velocity;

        private float invuln;
        private float heart = EiraConst.MaxHeart;
        private float pulseCooldown;

        private readonly System.Collections.Generic.List<Interactable> near =
            new System.Collections.Generic.List<Interactable>();

        // =========================================================
        // ANIMATOR HASHES
        // =========================================================

        // =========================================================
        // PROPIEDADES
        // =========================================================

        public float DetectionMultiplier
        {
            get
            {
                if (!IsCrouching)
                    return 1f;

                if (hideZoneLevel > 0.5f)
                    return 0.07f;

                return 0.28f;
            }
        }

        public float HeartFrac =>
            heart / EiraConst.MaxHeart;

        /// <summary>Corazón en valor absoluto (0..MaxHeart).</summary>
        public float Heart => heart;

        public GameState PlayerState =>
            GameManager.Instance != null
                ? GameManager.Instance.State
                : GameState.Playing;

        // =========================================================
        // AWAKE
        // =========================================================

        private void Awake()
        {
            cc = GetComponent<CharacterController>();

            if (cc == null)
            {
                Debug.LogError(
                    "Eira: PlayerController necesita un CharacterController."
                );

                enabled = false;
                return;
            }

            Health = EiraConst.MaxHealth;

            if (visual == null)
                visual = FindVisual();

            if (visual == null)
                Debug.LogError(
                    "[Eira] No se encuentra el modelo de Eira: el campo 'visual' " +
                    "está vacío y la jerarquía no tiene un hijo con malla de " +
                    "piel. Sin él no se puede alinear el modelo con la cápsula.",
                    this);

            if (camera == null)
                camera = FindObjectOfType<ProfessionalThirdPersonCamera>();

            if (weapon == null)
                weapon = GetComponentInChildren<EiraWeapon>(true);

            if (weapon == null)
                weapon = gameObject.AddComponent<EiraWeapon>();

            if (animationDriver == null)
                animationDriver = GetComponentInChildren<EiraAnimationController>(true);

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (animator == null)
            {
                Debug.LogWarning(
                    "Eira: No se encontró un Animator dentro del modelo."
                );
            }
            else
            {
                // Evita que el Animator intente mover físicamente
                // el objeto Eira.
                if (disableRootMotion)
                    animator.applyRootMotion = false;

                if (animationDriver == null)
                    animationDriver = gameObject.AddComponent<EiraAnimationController>();

                animationDriver.Refresh();
            }

            NormalizeCapsule();
            FitBodyToModel();

            standHeight = cc.height;
            crouchHeightCurrent = cc.height;

            // Estado inicial.
            velocity = Vector3.zero;
            isGrounded = cc.isGrounded;
            lastProgressPosition = transform.position;
            lastSafePosition = transform.position;
        }

        /// <summary>
        /// Localiza el modelo de Eira cuando el campo visual no viene
        /// asignado. Ver <see cref="EiraBodyFit.FindVisual"/>: se busca por la
        /// malla de piel, no por el nombre, porque el hijo se llama "EiraBase"
        /// y Transform.Find distingue mayúsculas.
        /// </summary>
        private Transform FindVisual()
        {
            return EiraBodyFit.FindVisual(transform);
        }

        /// <summary>
        /// Deja la cápsula centrada en el GameObject. El centro de la cápsula
        /// debe estar en (0, altura/2, 0): cualquier desvío lateral hace que
        /// el pivote de la cámara, los rayos de suelo y la rejilla de
        /// colisiones no coincidan con el transform de Eira.
        ///
        /// NO decide la altura ni el radio: eso lo hace
        /// <see cref="FitBodyToModel"/> midiendo el modelo.
        /// </summary>
        private void NormalizeCapsule()
        {
            float height = Mathf.Max(0.2f, cc.height);
            float radius = Mathf.Clamp(cc.radius, 0.05f, height * 0.5f);

            cc.height = height;
            cc.radius = radius;
            cc.center = new Vector3(0f, height * 0.5f, 0f);

            // Evita que el paso se atasque contra los bordes del suelo.
            cc.stepOffset = Mathf.Min(cc.stepOffset, height * 0.45f);

            if (cc.slopeLimit < 30f)
                cc.slopeLimit = 45f;
        }

        /// <summary>
        /// Encaja cápsula y modelo midiendo la malla real de Eira (EiraBase,
        /// con las placas y las botas que cuelgan de 'visual').
        ///
        /// Antes la altura y el radio venían a mano (1.75 / 0.3) y el modelo se
        /// alineaba después a esa caja, así que la cápsula verde de Unity no
        /// coincidía con el personaje. Y la medida se hacía con
        /// Renderer.bounds, que es el AABB de la pose animada: al saltar, con
        /// las piernas recogidas, el AABB crecía, la cápsula salía alta y
        /// Eira se quedaba flotando sobre el suelo.
        ///
        /// Ahora la medida la hace <see cref="EiraBodyFit"/> con
        /// SkinnedMeshRenderer.localBounds (pose de referencia, estable) y
        /// solo con las mallas de piel, sin auras ni efectos.
        ///
        /// Es idempotente: se puede volver a ejecutar sin que el modelo derive.
        /// </summary>
        private void FitBodyToModel()
        {
            if (visual == null)
                return;

            bool ok = EiraBodyFit.Apply(
                visual,
                cc,
                capsuleRadiusFactor,
                capsuleFootMargin,
                capsuleMaxHeight,
                autoAlignVisual,
                fitCapsuleToModel
            );

            if (ok)
                return;

            // Medición no creíble (sin mallas, escala rota, bounds disparado):
            // no se toca nada y se avisa, porque encajar con una medida falsa
            // deja a Eira del tamaño de un edificio.
            Debug.LogWarning(
                "[Eira] No se ha podido medir el cuerpo de Eira: se conservan " +
                "los valores de la cápsula. Revisa la escala del modelo y que " +
                "tenga malla de piel.", this);

            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
        }


        // =========================================================
        // START
        // =========================================================

        private void Start()
        {
            if (cc == null)
                cc = GetComponent<CharacterController>();

            if (cc == null)
                return;

            velocity = Vector3.zero;

            // El transform queda a los PIES de Eira (centro de la cápsula = +Y/2),
            // así que buscamos el suelo desde un poco por encima de la base.
            Vector3 capsuleBottom = transform.position + cc.center - Vector3.up * (cc.height * 0.5f);

            if (Physics.Raycast(
                    capsuleBottom + Vector3.up * 0.2f,
                    Vector3.down,
                    out RaycastHit hit,
                    4f,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                hit.transform != transform &&
                !hit.transform.IsChildOf(transform))
            {
                transform.position = new Vector3(
                    transform.position.x,
                    hit.point.y,
                    transform.position.z
                );
            }

            velocity = Vector3.zero;
            isGrounded = cc.isGrounded;
            lastGroundedTime = Time.time;
            lastProgressPosition = transform.position;
            lastSafePosition = transform.position;
            groundWatchdog = 0f;

            if (isGrounded)
                velocity.y = -2f;

            // Puede que el punto de aparicion de la escena este dentro de un
            // collider (un slab solapado, un mueble, un muro). Antes de
            // nada se saca a Eira de ahí: aparecer encajonada era una de las
            // formas de que el nivel fuese injugable desde el primer segundo.
            if (IsStuckInGeometry())
            {
                if (!Depenetrate())
                    LandOnFloor();
            }

            UpdateAnimator();

            if (camera != null)
                camera.SnapBehind();

            // Eira ya está apoyada en el suelo, así que este es el momento
            // fiable para fijar el checkpoint inicial. Se hace aquí y no en
            // GameManager.Start porque el orden de ejecución de Start entre
            // distintos GameObjects no está garantizado.
            if (GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position, transform.rotation);

            Debug.Log("EIRA COLOCADA SOBRE EL SUELO: " + transform.position);
        }


        // =========================================================
        // CONFIGURAR ANIMATOR
        // =========================================================

        // =========================================================
        // COLOCAR EIRA SOBRE EL SUELO
        // =========================================================

        public void LandOnFloor()
        {
            if (cc == null)
                cc = GetComponent<CharacterController>();

            if (cc == null)
                return;

            Vector3 start = transform.position;

            cc.enabled = false;

            for (int i = 0; i < EscapeOffsets.Length; i++)
            {
                Vector3 p = start +
                    new Vector3(
                        EscapeOffsets[i].x,
                        0f,
                        EscapeOffsets[i].y
                    );

                if (!DropToTopSurface(ref p))
                    continue;

                if (!OverlapsSolid(p))
                {
                    transform.position = p;

                    velocity = Vector3.zero;

                    cc.enabled = true;

                    ResetVerticalState();

                    return;
                }
            }

            Vector3 pos = transform.position;

            if (DropToTopSurface(ref pos))
            {
                transform.position = pos;
            }

            velocity = Vector3.zero;

            cc.enabled = true;

            ResetVerticalState();
        }

        private void ResetVerticalState()
        {
            isGrounded = cc != null ? cc.isGrounded : false;
            isJumping = false;
            isFalling = false;
            verticalVelocity = 0f;
            velocity = Vector3.zero;
            lastGroundedTime = isGrounded ? Time.time : -99f;
            jumpPressedTime = -99f;
            footstepDistance = 0f;
            airborneJumps = 0;
            takeoffTime = -99f;
            landingTime = -99f;
            jumpCutApplied = false;
            stuckTimer = 0f;
            escapeCooldown = 0f;
            groundWatchdog = 0f;
            lastProgressPosition = transform.position;
        }

        private bool DropToTopSurface(ref Vector3 p)
        {
            // El transform va a los pies, así que basta con cubrir
            // desde un poco por encima de la altura de la cabeza.
            Vector3 rayOrigin = p + Vector3.up * (standHeight + 1.5f);

            if (Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                standHeight + 8f,
                ~0,
                QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
            {
                p = new Vector3(p.x, hit.point.y, p.z);
                return true;
            }

            return false;
        }

        private bool OverlapsSolid(Vector3 p)
        {
            if (cc == null)
                return false;

            float radius = Mathf.Max(
                0.05f,
                cc.radius * 0.9f
            );

            float halfHeight = Mathf.Max(
                0.05f,
                cc.height * 0.5f - cc.radius
            );

            Vector3 center = p + cc.center;

            Collider[] colliders =
                Physics.OverlapCapsule(
                    center - Vector3.up * halfHeight,
                    center + Vector3.up * halfHeight,
                    radius,
                    ~0,
                    QueryTriggerInteraction.Ignore
                );

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                if (col.isTrigger)
                    continue;

                if (col.transform == transform)
                    continue;

                if (col.transform.IsChildOf(transform))
                    continue;

                return true;
            }

            return false;
        }

        // =========================================================
        // RESPAWN
        // =========================================================

        public void RespawnAt(Vector3 pos, Quaternion rot)
        {
            if (cc == null)
                cc = GetComponent<CharacterController>();

            cc.enabled = false;

            transform.position = pos;
            transform.rotation = rot;

            velocity = Vector3.zero;

            cc.enabled = true;

            Health = EiraConst.MaxHealth;

            invuln = 2.2f;

            heart = Mathf.Max(
                heart,
                EiraConst.MaxHeart * 0.5f
            );

            LandOnFloor();

            camera?.SnapBehind();
        }

        // =========================================================
        // UPDATE
        // =========================================================

        private void Update()
        {
            if (cc == null || !cc.enabled)
                return;

            invuln = Mathf.Max(
                0f,
                invuln - Time.deltaTime
            );

            if (PlayerState == GameState.Playing)
            {
                TickMovement();
                TickAbilities();
                TickWeapon();
            }
            else if (PlayerState == GameState.Paused)
            {
                // Nada se mueve en pausa: ni gravedad ni animación.
                // Time.deltaTime ya vale 0, pero se evita tocar la física
                // para que al reanudar no haya un salto de posición.
            }
            else
            {
                ApplyGravityWhilePaused();
            }
        }

        // =========================================================
        // GRAVEDAD FUERA DEL GAMEPLAY
        // =========================================================

        private void ApplyGravityWhilePaused()
        {
            if (cc.isGrounded)
            {
                velocity.y = -2f;
            }
            else
            {
                velocity.y -= gravity * Time.deltaTime;

                velocity.y = Mathf.Max(
                    velocity.y,
                    -40f
                );
            }

            cc.Move(
                Vector3.up *
                velocity.y *
                Time.deltaTime
            );

            UpdateGroundState();
            UpdateAnimator();
        }

        // =========================================================
        // MOVIMIENTO
        // =========================================================

        private void TickMovement()
        {
            float dt = Time.deltaTime;

            if (dt <= 0f)
                return;

            escapeCooldown = Mathf.Max(0f, escapeCooldown - dt);

            // =====================================================
            // INPUT
            // =====================================================

            if (EiraInput.JumpDown())
                jumpPressedTime = Time.time;

            bool jumpHeld = EiraInput.JumpHeld();

            Vector2 input = EiraInput.MoveAxis();

            if (input.sqrMagnitude > 1f)
                input.Normalize();

            bool wantsCrouch = EiraInput.CrouchHeld();

            // =====================================================
            // CÁMARA (fuente de verdad: la cámara publica su yaw)
            // =====================================================

            float cameraYaw = GetCameraYaw();

            Quaternion cameraRotation = Quaternion.Euler(0f, cameraYaw, 0f);

            Vector3 cameraForward = cameraRotation * Vector3.forward;
            Vector3 cameraRight = cameraRotation * Vector3.right;

            cameraForward.y = 0f;
            cameraRight.y = 0f;

            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 moveDir = cameraForward * input.y + cameraRight * input.x;

            bool hasInput = moveDir.sqrMagnitude > 0.0004f;

            if (hasInput)
                moveDir.Normalize();

            // Se guarda aparte: la animación de caminar tiene que seguir a la
            // tecla, no al tiempo que tarda la física en frenar. Sin esto,
            // Eira seguía dando pasos un momento después de soltar el mando.
            hasMoveInput = hasInput;

            // =====================================================
            // SPRINT: solo hacia delante y sin agacharse
            // =====================================================

            isSprinting =
                EiraInput.Sprint() &&
                !IsCrouching &&
                hasInput &&
                input.y > 0.15f;

            // =====================================================
            // AGACHARSE
            // =====================================================

            if (wantsCrouch != IsCrouching)
            {
                if (!wantsCrouch && !CanStandUp())
                {
                    // Techo bajo: se sigue agachada.
                    wantsCrouch = true;
                }
                else
                {
                    IsCrouching = wantsCrouch;
                }
            }

            float targetHeight = IsCrouching
                ? crouchHeight
                : standHeight;

            // Interpolación suave: subir/bajar la cápsula de golpe
            // hace que el modelo atraviese el suelo.
            crouchHeightCurrent = Mathf.MoveTowards(
                crouchHeightCurrent,
                targetHeight,
                crouchHeightSpeed * dt
            );

            ApplyCrouchHeight(crouchHeightCurrent);

            // =====================================================
            // VELOCIDAD OBJETIVO
            // =====================================================

            float speed = IsCrouching
                ? crouchSpeed
                : (isSprinting ? runSpeed : walkSpeed);

            Vector3 targetVelocity = moveDir * speed;

            // =====================================================
            // GRAVEDAD Y SALTO
            // =====================================================

            bool wasGrounded = isGrounded;

            // Tras despegar, el CharacterController sigue tocando el suelo un
            // par de frames (skin width) y el sondeo corto también lo ve. Si
            // se aceptara ese "isGrounded", la gravedad no actuaría, no se
            // refrescaría el coyote time y isJumping parpadearía: el salto se
            // veía como un rebote en vez de un salto.
            bool inTakeoffGrace = Time.time - takeoffTime < takeoffGrace;

            if (isGrounded && !inTakeoffGrace)
            {
                if (velocity.y < 0f)
                    velocity.y = -2f;

                lastGroundedTime = Time.time;
            }
            else
            {
                velocity.y = Mathf.Max(velocity.y - gravity * dt, -40f);
            }

            // Un salto por vuelo. El buffer sigue guardando la pulsación para
            // que el salto salga al aterrizar si se pulsó un poco antes, pero
            // nunca dentro del mismo aire: eso era el "salta dos veces".
            bool canJump =
                airborneJumps == 0 &&
                Time.time - landingTime >= landingLock &&
                (isGrounded || Time.time - lastGroundedTime <= coyoteTime) &&
                !IsCrouching;

            if (canJump && Time.time - jumpPressedTime <= jumpBuffer)
            {
                velocity.y = jumpVelocity;

                airborneJumps = 1;
                takeoffTime = Time.time;
                jumpCutApplied = false;

                lastGroundedTime = -99f;
                jumpPressedTime = -99f;

                animationDriver?.TriggerJump();
            }

            // Altura variable: al SOLTAR el botón el impulso se recorta UNA vez.
            // Antes se multiplicaba en cada frame mientras se subiera, así que
            // un toque corto dejaba el salto en 1 cm de altura y Eira rebotaba
            // contra el suelo en vez de subir.
            if (!jumpCutApplied &&
                !jumpHeld &&
                velocity.y > 0f &&
                airborneJumps > 0)
            {
                velocity.y *= jumpCutMultiplier;
                jumpCutApplied = true;
            }

            // =====================================================
            // ACELERACIÓN
            // =====================================================

            Vector3 current = cc.velocity;

            Vector3 horizontal = new Vector3(current.x, 0f, current.z);

            float acceleration = wasGrounded
                ? groundAcceleration
                : airAcceleration;

            horizontal = Vector3.MoveTowards(
                horizontal,
                targetVelocity,
                acceleration * dt
            );

            // Antiatravesar muros: antes de mover, se quitan de la velocidad
            // las componentes que entran en una pared. No se toca la posición
            // a mano para no pelearse con la resolución de colisión de la
            // cápsula.
            horizontal = SlideAlongWalls(horizontal, dt, hasInput ? moveDir : Vector3.zero);

            // =====================================================
            // MOVER
            // =====================================================

            Vector3 finalVelocity = new Vector3(
                horizontal.x,
                velocity.y,
                horizontal.z
            );

            CollisionFlags flags = cc.Move(finalVelocity * dt);

            // =====================================================
            // ESTADO DE SUELO
            // =====================================================

            // El grace de despegue también tiene que filtrar ESTA medición, no
            // solo la gravedad de arriba. Si no, se rompen dos cosas:
            //
            //  - cc.isGrounded sigue dando true uno o dos frames después de
            //    soltar el suelo (es el skin width) y GroundProbe() también ve
            //    el suelo, porque Eira todavía no ha subido nada. Sin filtrarlo,
            //    el mismo frame del despegue cuenta como aterrizaje: aquí abajo
            //    airborneJumps vuelve a 0 y landingTime se pone a "ahora". Eso
            //    consume el landingLock subiendo en vez de al aterrizar, que es
            //    justo lo que deja salir un segundo salto en pleno aire.
            //
            //  - isJumping e isGrounded parpadean durante los primeros frames
            //    del salto, y eso es lo que lee el Animator. Por eso el salto se
            //    veía mal justo al despegar.
            bool touchingGround =
                (flags & CollisionFlags.Below) != 0 ||
                cc.isGrounded ||
                GroundProbe();

            isGrounded = touchingGround && !inTakeoffGrace;

            // Agarre al suelo: si el frame anterior estaba apoyada y este no,
            // pero no está subiendo (velocity.y <= 0, o sea no está saltando),
            // se pega al suelo en vez de quedarse flotando un frame. Es lo que
            // arregla los bordes de los slabs y las cuestas.
            if (!isGrounded && wasGrounded && velocity.y <= 0f)
                if (StickToGround())
                    isGrounded = true;

            if (isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            // Aterrizaje: se rearma el vuelo. Un salto nuevo solo puede salir
            // pasado landingLock, para que un pisotón en el frame de contacto
            // no encadene dos saltos.
            if (isGrounded)
            {
                lastGroundedTime = Time.time;

                if (airborneJumps > 0)
                {
                    airborneJumps = 0;
                    landingTime = Time.time;
                    jumpCutApplied = false;
                }
            }

            isJumping = !isGrounded && velocity.y > 0.05f;
            isFalling = !isGrounded && velocity.y < -0.05f;

            verticalVelocity = velocity.y;

            // =====================================================
            // ORIENTACIÓN
            // =====================================================

            if (hasInput)
            {
                // Dirección de marcha en el eje de la CÁMARA, no en el de
                // Eira. Antes se medía contra transform.right/forward, y
                // como Eira giraba para encarar la marcha el resultado era
                // siempre (0, 1): el Animator recibía Strafe = 0 y no
                // podía diferenciar andar recto de ir de lado.
                localMoveDirection = new Vector2(
                    Vector3.Dot(moveDir, cameraRight),
                    Vector3.Dot(moveDir, cameraForward)
                );

                Quaternion targetRotation = Quaternion.LookRotation(
                    ResolveFacing(moveDir),
                    Vector3.up
                );

                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSpeed * dt)
                );
            }
            else
            {
                localMoveDirection = Vector2.Lerp(
                    localMoveDirection,
                    Vector2.zero,
                    1f - Mathf.Exp(-rotationSpeed * dt)
                );
            }

            // =====================================================
            // PASOS
            // =====================================================

            float planarSpeed = new Vector3(current.x, 0f, current.z).magnitude;

            if (isGrounded && planarSpeed > 0.4f)
            {
                float interval = isSprinting
                    ? footstepIntervalRun
                    : footstepIntervalWalk;

                footstepDistance += planarSpeed * dt;

                if (footstepDistance >= interval)
                {
                    footstepDistance = 0f;
                    AudioFX.Footstep();
                }
            }
            else
            {
                footstepDistance = 0f;
            }

            // =====================================================
            // VIGILANCIA DE PROGRESO (antiatasco)
            // =====================================================

            float progressDistance = (transform.position - lastProgressPosition).magnitude;
            if (hasInput && isGrounded && planarSpeed < 0.25f && progressDistance < 0.01f)
            {
                stuckTimer += dt;
                if (escapeCooldown <= 0f && stuckTimer >= unstickDelay)
                {
                    // Intenta sacar a Eira de una penetración ligera
                    if (Depenetrate() || TryPushAway())
                    {
                        stuckTimer = 0f;
                        escapeCooldown = 0.5f;
                    }
                    else
                    {
                        stuckTimer = 0f;
                    }
                }
            }
            else if (progressDistance > 0.05f)
            {
                lastProgressPosition = transform.position;
                stuckTimer = 0f;
            }
            else if (!hasInput)
            {
                stuckTimer = Mathf.Max(0f, stuckTimer - dt * 0.5f);
            }

            // Guarda un punto seguro si está apoyada
            if (isGrounded && (transform.position - lastSafePosition).magnitude > 0.2f)
            {
                lastSafePosition = transform.position;
                groundWatchdog = 0f;
            }

            groundWatchdog += dt;
            if (groundWatchdog > 3f && !IsOnSolidGround())
            {
                // Busca suelo cerca
                if (FindSafeGround(ref lastSafePosition))
                {
                    cc.enabled = false;
                    transform.position = lastSafePosition + Vector3.up * 0.01f;
                    cc.enabled = true;
                    velocity = Vector3.zero;
                    ResetVerticalState();
                    groundWatchdog = 0f;
                }
            }

            // =====================================================
            // CAÍDA DEL MAPA
            // =====================================================

            if (transform.position.y < fallLimit)
            {
                // Antes de matar, intenta recuperar el último sitio seguro
                if (lastSafePosition.y > transform.position.y + 2f)
                {
                    cc.enabled = false;
                    transform.position = lastSafePosition + Vector3.up * 0.05f;
                    cc.enabled = true;
                    velocity = Vector3.zero;
                    ResetVerticalState();
                    groundWatchdog = 0f;
                }
                else
                {
                    TakeDamage(20f, null);
                    GameManager gm = GameManager.Instance;
                    if (gm != null)
                        gm.RespawnPlayer();
                    return;
                }
            }

            UpdateAnimator();
        }

        /// <summary>
        /// Antiatravesar muros por deslizamiento.
        ///
        /// Barre una esfera del tamaño de la cápsula en la dirección del
        /// movimiento y proyecta la velocidad sobre el plano de cada muro que
        /// toca, en vez de anularla entera. La diferencia con la versión
        /// anterior es que antes se hacía un ProjectOnPlane sobre un único
        /// impacto: si ese muro era una esquina o un mueble con dos caras,
        /// la proyección contra el primer plano metía a Eira contra el segundo
        /// y el resultado era una velocidad cero permanente, es decir, clavada.
        /// Aquí se acumulan todas las caras del barrido, así que Eira siempre
        /// conserva la componente que la deja deslizar por el muro.
        ///
        /// Los escalones (normal casi vertical) se ignoran, así que subir un
        /// peldaño sigue funcionando.
        /// </summary>
        private Vector3 SlideAlongWalls(
            Vector3 horizontal,
            float dt,
            Vector3 desiredDir)
        {
            if (wallBlockDistance <= 0f)
                return horizontal;

            Vector3 flat = new Vector3(horizontal.x, 0f, horizontal.z);

            float speed = flat.magnitude;

            if (speed < 0.05f)
                return horizontal;

            Vector3 dir = flat / speed;

            if (desiredDir.sqrMagnitude > 0.0001f)
            {
                Vector3 wanted = new Vector3(
                    desiredDir.x, 0f, desiredDir.z
                ).normalized;

                // Se barre hacia donde el jugador quiere ir, no hacia donde
                // va la velocidad actual: si Eira ya está frenando contra el
                // muro, un barrido hacia atrás no encontraría nada y se
                // quedaría enganchada.
                dir = wanted;
            }

            // Desde el centro de la cápsula, un poco por encima de los pies,
            // para no detectar el propio suelo.
            Vector3 origin =
                transform.position +
                cc.center +
                Vector3.up * (cc.height * 0.2f);

            float reach = speed * dt + wallBlockDistance;

            int hits = Physics.SphereCastNonAlloc(
                origin,
                cc.radius * 0.97f,
                dir,
                wallHits,
                reach,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            Vector3 result = horizontal;

            for (int i = 0; i < hits; i++)
            {
                RaycastHit hit = wallHits[i];

                if (hit.transform.IsChildOf(transform))
                    continue;

                if (hit.collider == null || hit.collider.isTrigger)
                    continue;

                // Normal casi vertical: es un escalón o el suelo, no un muro.
                if (Mathf.Abs(hit.normal.y) > 0.6f)
                    continue;

                // Proyectar sobre el plano del muro deja la componente que
                // corre paralela a él: eso es el deslizamiento.
                result = Vector3.ProjectOnPlane(result, hit.normal);
            }

            return result;
        }

        /// <summary>
        /// Decide hacia dónde mira Eira.
        ///
        /// El problema que arregla: con la cámara mirando a un lado, pulsar
        /// una sola flecha pedía un giro instantáneo de hasta 180°, y
        /// el slerp de rotationSpeed lo recorría en un par de frames. Se veía
        /// como un latigazo de 90° que además dejaba a Eira encarada a un muro
        /// del pasillo, obligando a girar la cámara para volver a encarrilarse.
        ///
        /// Ahora el giro está acotado a turnAssistLimit grados por frame de
        /// objetivo: si Eira tendría que girarse más, sigue su dirección de entrada pero
        /// mostrando la cara hacia la dirección que ya tiene, y el Animator
        /// recibe un Strafe distinto de cero para que ande de lado. El giro
        /// grande se reparte en varios frames y nunca hay salto.
        /// </summary>
        private Vector3 ResolveFacing(Vector3 moveDir)
        {
            if (moveDir.sqrMagnitude < 0.0001f)
                return transform.forward;

            float targetAngle =
                Mathf.Atan2(moveDir.x, moveDir.z) * Mathf.Rad2Deg;

            float currentAngle = transform.eulerAngles.y;

            float delta = Mathf.DeltaAngle(currentAngle, targetAngle);

            // El objetivo de rotación nunca se aleja más de turnAssistLimit de
            // la orientación actual. Como el slerp de abajo es suave, el giro
            // real acaba siendo progresivo en vez de un salto.
            if (Mathf.Abs(delta) > turnAssistLimit)
                targetAngle = currentAngle + Mathf.Sign(delta) * turnAssistLimit;

            float limited = targetAngle * Mathf.Deg2Rad;

            return new Vector3(
                Mathf.Sin(limited),
                0f,
                Mathf.Cos(limited)
            );
        }

        /// <summary>
        /// ¿La cápsula está dentro de alguna geometría?
        ///
        /// El CharacterController resuelve colisiones pero no penetraciones:
        /// si Eira nace dentro de un collider o alguien la empuja dentro, se
        /// queda ahí para siempre y deja de responder a las teclas. Esto
        /// detecta el caso para poder sacarla.
        /// </summary>
        private bool IsStuckInGeometry()
        {
            Vector3 center = transform.position + cc.center;
            float radius = cc.radius * 0.95f;
            int hits = Physics.OverlapSphereNonAlloc(center, radius, overlapHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapHits[i];
                if (col == null || col.isTrigger || col.transform.IsChildOf(transform))
                    continue;
                return true;
            }
            return false;
        }

        private bool Depenetrate()
        {
            Vector3 center = transform.position + cc.center;
            float radius = cc.radius * 0.9f;
            int hits = Physics.OverlapSphereNonAlloc(center, radius + 0.05f, overlapHits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapHits[i];
                if (col == null || col.isTrigger || col.transform.IsChildOf(transform))
                    continue;

                Vector3 dir;
                if (Physics.ComputePenetration(
                    cc, transform.position, transform.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out dir, out float dist))
                {
                    if (dist <= 0f) continue;
                    cc.enabled = false;
                    transform.position += dir.normalized * (dist + 0.01f);
                    cc.enabled = true;
                    velocity = Vector3.zero;
                    return true;
                }
            }
            return false;
        }

        private bool TryPushAway()
        {
            Vector3 center = transform.position + cc.center;
            float radius = cc.radius + 0.1f;
            Vector3 push = Vector3.zero;
            int hits = Physics.OverlapSphereNonAlloc(center, radius, overlapHits, ~0, QueryTriggerInteraction.Ignore);
            int count = 0;
            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapHits[i];
                if (col == null || col.isTrigger || col.transform.IsChildOf(transform))
                    continue;
                Vector3 offset = center - col.ClosestPoint(center);
                if (offset.sqrMagnitude < 0.0001f) offset = transform.right * 0.1f;
                push += offset.normalized;
                count++;
            }
            if (count == 0) return false;
            cc.enabled = false;
            transform.position += push.normalized * 0.08f;
            cc.enabled = true;
            return true;
        }

        private bool IsOnSolidGround()
        {
            Vector3 origin = transform.position + cc.center - Vector3.up * (cc.height * 0.5f - 0.05f);
            if (Physics.Raycast(origin, Vector3.down, groundProbeDistance + 0.5f, ~0, QueryTriggerInteraction.Ignore))
                return true;
            return false;
        }

        private bool FindSafeGround(ref Vector3 result)
        {
            Vector3 origin = transform.position + Vector3.up * 1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, groundWatchdogDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    result = new Vector3(hit.point.x, hit.point.y, hit.point.z);
                    return true;
                }
            }
            return false;
        }

        private float GetCameraYaw()
        {
            if (camera != null)
                return camera.Yaw;

            if (GameManager.Instance != null)
                return GameManager.Instance.CameraYaw;

            return transform.eulerAngles.y;
        }

        /// <summary>
        /// Rayo corto bajo los pies. Evita que Eira "flote" al bajar escaleras
        /// o al cruzar los huecos entre slabs, donde isGrounded se pierde.
        /// </summary>
        private bool GroundProbe()
        {
            Vector3 origin =
                transform.position +
                cc.center -
                Vector3.up * (cc.height * 0.5f - 0.05f);

            if (!Physics.Raycast(
                    origin,
                    Vector3.down,
                    out RaycastHit hit,
                    groundProbeDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return false;

            return !hit.transform.IsChildOf(transform);
        }

        /// <summary>
        /// Pega a Eira al suelo cuando ha perdido el contacto por un frame
        /// (borde de un slab, bajada de peldaño, cuesta). Devuelve true si ha
        /// quedado apoyada.
        ///
        /// Se usa cc.Move y no un teleport de posición: el CharacterController
        /// vuelve a resolver colisiones, así que pegar al suelo nunca mete a
        /// Eira dentro de un muro.
        /// </summary>
        private bool StickToGround()
        {
            if (snapToGroundDistance <= 0f)
                return false;

            Vector3 feet =
                transform.position +
                cc.center -
                Vector3.up * (cc.height * 0.5f);

            if (!Physics.Raycast(
                    feet,
                    Vector3.down,
                    out RaycastHit hit,
                    snapToGroundDistance + 0.05f,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return false;

            if (hit.transform.IsChildOf(transform))
                return false;

            // Cuesta demasiado empinada: mejor dejar caer que arrastrar.
            if (hit.normal.y < Mathf.Cos(cc.slopeLimit * Mathf.Deg2Rad))
                return false;

            float drop = hit.distance + 0.02f;

            CollisionFlags flags = cc.Move(Vector3.down * drop);

            return
                (flags & CollisionFlags.Below) != 0 ||
                cc.isGrounded ||
                GroundProbe();
        }

        // =========================================================
        // ACTUALIZAR ESTADO DE SUELO
        // =========================================================

        private void UpdateGroundState()
        {
            if (cc == null)
                return;

            // Mismo grace de despegue que en el movimiento: si el juego se pausa
            // en mitad de un salto, cc.isGrounded y el sondeo siguen viendo el
            // suelo y el Animator volvería a Locomotion con Eira en el aire.
            bool inTakeoffGrace = Time.time - takeoffTime < takeoffGrace;

            isGrounded = (cc.isGrounded || GroundProbe()) && !inTakeoffGrace;

            if (isGrounded)
            {
                if (velocity.y < 0f)
                    velocity.y = -2f;

                lastGroundedTime = Time.time;
                isJumping = false;
                isFalling = false;
            }
            else
            {
                isJumping = velocity.y > 0.05f;
                isFalling = velocity.y < -0.05f;
            }

            verticalVelocity = velocity.y;
        }

        // =========================================================
        // ANIMATOR
        // =========================================================

        private void UpdateAnimator()
        {
            if (animationDriver == null)
                return;

            Vector3 v = cc != null ? cc.velocity : Vector3.zero;

            float horizontalSpeed = new Vector3(v.x, 0f, v.z).magnitude;

            animationDriver.Drive(
                horizontalSpeed,
                localMoveDirection,
                isGrounded,
                verticalVelocity,
                IsCrouching,
                isSprinting,
                isJumping,
                hasMoveInput
            );
        }

        // =========================================================
        // AGACHARSE
        // =========================================================

        private void ApplyCrouchHeight(float height)
        {
            cc.height = height;
            cc.center = new Vector3(0f, height * 0.5f, 0f);
        }

        /// <summary>
        /// No permite levantarse si hay algo encima: si no, la cápsula
        /// se metería dentro del techo.
        /// </summary>
        private bool CanStandUp()
        {
            Vector3 bottom = transform.position + cc.center - Vector3.up * (cc.height * 0.5f);

            Vector3 top = bottom + Vector3.up * standHeight;

            float radius = Mathf.Max(0.05f, cc.radius * 0.95f);

            return Physics.CheckCapsule(
                bottom + Vector3.up * radius,
                top - Vector3.up * radius,
                radius,
                ~0,
                QueryTriggerInteraction.Ignore
            ) == false;
        }

        // =========================================================
        // HABILIDADES
        // =========================================================

        private void TickAbilities()
        {
            heart = Mathf.Min(
                EiraConst.MaxHeart,
                heart +
                heartRegen *
                Time.deltaTime
            );

            if (heart < heartDanger)
            {
                GameEvents.HeartDanger();
                AudioFX.Heart();
            }

            pulseCooldown =
                Mathf.Max(
                    0f,
                    pulseCooldown -
                    Time.deltaTime
                );

            if (EiraInput.AbilityDown())
                UseAbility();

            if (EiraInput.InteractDown())
                TryInteract();

            string prompt = null;

            Interactable best = null;

            float bestD =
                float.MaxValue;

            for (int i = near.Count - 1; i >= 0; i--)
            {
                if (near[i] == null)
                {
                    near.RemoveAt(i);
                    continue;
                }

                if (!near[i].CanInteract(this))
                    continue;

                float d =
                    (
                        near[i].transform.position -
                        transform.position
                    ).sqrMagnitude;

                if (d < bestD)
                {
                    bestD = d;
                    best = near[i];
                }
            }

            if (best != null)
                prompt = best.Prompt;

            GameEvents.InteractPrompt(prompt);
        }

        // =========================================================
        // ARMA
        // =========================================================

        /// <summary>
        /// El arma se actualiza desde aquí, no desde su propio Update, para
        /// que un disparo nunca salga en un frame en el que el juego está en
        /// pausa o en medio de un respawn.
        /// </summary>
        private void TickWeapon()
        {
            if (weapon == null)
                return;

            weapon.Tick();
        }

        // =========================================================
        // PULSO
        // =========================================================

        private void UseAbility()
        {
            if (pulseCooldown > 0f)
                return;

            if (heart < 1f)
                return;

            if (heart < abilityCost ||
                heart < heartDanger * 0.8f)
            {
                GameEvents.HeartDanger();
                AudioFX.Heart();
                return;
            }

            pulseCooldown =
                pulseInterval;

            heart -= abilityCost;

            AudioFX.Ability();

            GameEvents.AbilityUsed();

            Collider[] colliders =
                Physics.OverlapSphere(
                    transform.position +
                    Vector3.up,
                    abilityRadius
                );

            foreach (Collider c in colliders)
            {
                if (c == null)
                    continue;

                Interactable it =
                    c.GetComponent<Interactable>();

                if (it != null)
                    it.OnAbilityPulse(this);
            }

            bool repelled = false;

            Vector3 pulseCenter =
                transform.position +
                Vector3.up;

            DroneController[] drones =
                UnityEngine.Object.FindObjectsOfType<DroneController>();

            foreach (DroneController drone in drones)
            {
                if (drone == null)
                    continue;

                if (Vector3.Distance(
                    pulseCenter,
                    drone.transform.position
                ) <= abilityRadius)
                {
                    drone.OnDefensePulse(
                        transform.position
                    );

                    repelled = true;
                }
            }

            if (repelled)
            {
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.AddScore(
                        Points.AvoidEnemy
                    );
                }

                GameEvents.Subtitle(
                    "Pulso defensivo: dron repelido. +" +
                    Points.AvoidEnemy,
                    1.8f
                );
            }
        }

        // =========================================================
        // INTERACTUAR
        // =========================================================

        private void TryInteract()
        {
            Interactable best = null;

            float bestD =
                float.MaxValue;

            for (int i = 0; i < near.Count; i++)
            {
                if (near[i] == null)
                    continue;

                if (!near[i].CanInteract(this))
                    continue;

                float d =
                    (
                        near[i].transform.position -
                        transform.position
                    ).sqrMagnitude;

                if (d < bestD)
                {
                    bestD = d;
                    best = near[i];
                }
            }

            if (best != null)
                best.OnInteract(this);
        }

        // =========================================================
        // DAÑO
        // =========================================================

        public void TakeDamage(
            float amount,
            GameObject attacker)
        {
            if (PlayerState != GameState.Playing)
                return;

            if (invuln > 0f)
                amount = 0f;

            Health -= amount;

            invuln =
                Mathf.Max(
                    invuln,
                    0.9f
                );

            if (amount > 0f)
            {
                AudioFX.Hurt();

                if (GameManager.Instance != null)
                    GameManager.Instance.OnPlayerHit();
            }

            if (Health <= 0f)
            {
                if (GameManager.Instance != null)
                    GameManager.Instance.OnPlayerDied();
            }
        }

        // =========================================================
        // CURACIÓN
        // =========================================================

        public void Heal(float amount)
        {
            Health =
                Mathf.Clamp(
                    Health + amount,
                    0f,
                    EiraConst.MaxHealth
                );

            if (GameManager.Instance != null)
                GameManager.Instance.RefreshPlayerHud();
        }

        public void RestoreHeart(float amount)
        {
            heart =
                Mathf.Min(
                    EiraConst.MaxHeart,
                    heart + amount
                );

            if (GameManager.Instance != null)
                GameManager.Instance.RefreshPlayerHud();
        }

        // =========================================================
        // INTERACTABLES
        // =========================================================

        public void DeregisterInteractable(
            Interactable it)
        {
            near.Remove(it);
        }

        private void OnTriggerEnter(Collider other)
        {
            // GetComponentInParent, no GetComponent: en varios objetos del
            // nivel el collider está en un hijo y el script en el padre, y
            // con GetComponent el botiquín nunca se registraba.
            Interactable it =
                other.GetComponentInParent<Interactable>();

            if (it != null &&
                !near.Contains(it))
            {
                near.Add(it);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Interactable it =
                other.GetComponentInParent<Interactable>();

            if (it != null)
                near.Remove(it);
        }

        // =========================================================
        // POSICIONES DE ESCAPE
        // =========================================================

        private static readonly Vector2[] EscapeOffsets =
        {
            Vector2.zero,

            new Vector2(1f, 0f),
            new Vector2(-1f, 0f),

            new Vector2(0f, 1f),
            new Vector2(0f, -1f),

            new Vector2(1.5f, 1.5f),
            new Vector2(-1.5f, 1.5f),

            new Vector2(1.5f, -1.5f),
            new Vector2(-1.5f, -1.5f),

            new Vector2(3f, 0f),
            new Vector2(-3f, 0f),

            new Vector2(0f, 3f),
            new Vector2(0f, -3f),

            new Vector2(3f, 3f),
            new Vector2(-3f, 3f),

            new Vector2(3f, -3f),
            new Vector2(-3f, -3f)
        };
    }
}
