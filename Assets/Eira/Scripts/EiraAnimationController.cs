using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Puente entre la lógica de <see cref="PlayerController"/> y el Animator.
    ///
    /// Antes este script leía el input por su cuenta con la API antigua
    /// (Input.GetAxisRaw), lo que provocaba una excepción en runtime porque
    /// el proyecto usa solo el Input System nuevo. Además escribía "Speed"
    /// con la magnitud del mando, no con la velocidad real de Eira.
    ///
    /// Ahora no lee nada: PlayerController es el único dueño del estado y
    /// llama a <see cref="Drive"/> una vez por frame. Así el Animator y la
    /// física nunca se contradicen.
    /// </summary>
    [DisallowMultipleComponent]
    public class EiraAnimationController : MonoBehaviour
    {
        [Header("Referencias")]
        [SerializeField] private Animator animator;

        // =========================================================
        // PARÁMETROS DEL ANIMATOR
        // =========================================================

        private int speedHash;
        private int groundedHash;
        private int verticalSpeedHash;
        private int jumpHash;
        private int fallingHash;
        private int crouchHash;
        private int movingHash;
        private int strafeHash;
        private int sprintHash;

        private bool hasSpeed;
        private bool hasGrounded;
        private bool hasVerticalSpeed;
        private bool hasJump;
        private bool hasFalling;
        private bool hasCrouch;
        private bool hasMoving;
        private bool hasStrafe;
        private bool hasSprint;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheParameters();
        }

        public Animator Animator => animator;

        private void CacheParameters()
        {
            speedHash = Animator.StringToHash("Speed");
            groundedHash = Animator.StringToHash("Grounded");
            verticalSpeedHash = Animator.StringToHash("VerticalSpeed");
            jumpHash = Animator.StringToHash("Jump");
            fallingHash = Animator.StringToHash("Falling");
            crouchHash = Animator.StringToHash("Crouch");
            movingHash = Animator.StringToHash("Moving");
            strafeHash = Animator.StringToHash("Strafe");
            sprintHash = Animator.StringToHash("Sprint");

            hasSpeed = Has(AnimatorControllerParameterType.Float, "Speed");
            hasGrounded = Has(AnimatorControllerParameterType.Bool, "Grounded");
            hasVerticalSpeed = Has(AnimatorControllerParameterType.Float, "VerticalSpeed");
            hasJump = Has(AnimatorControllerParameterType.Trigger, "Jump");
            hasFalling = Has(AnimatorControllerParameterType.Bool, "Falling");
            hasCrouch = Has(AnimatorControllerParameterType.Bool, "Crouch");
            hasMoving = Has(AnimatorControllerParameterType.Bool, "Moving");
            hasStrafe = Has(AnimatorControllerParameterType.Float, "Strafe");
            hasSprint = Has(AnimatorControllerParameterType.Bool, "Sprint");
        }

        private bool Has(AnimatorControllerParameterType type, string name)
        {
            if (animator == null)
                return false;

            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name == name && p.type == type)
                    return true;
            }

            // Fallo silencioso: si el parámetro existe pero con otro tipo,
            // Has() devuelve false y el estado simplemente deja de enviarse.
            // Así es como Eira acabó "caminando como una piedra": en el
            // controller, Grounded/Moving/Falling estaban declarados como Int
            // en vez de Bool, el driver los descartaba en silencio, las
            // transiciones pedían "Grounded < 0" (imposible para un bool) y
            // el Animator se quedaba clavado en el estado de salto.
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.name != name) continue;
                Debug.LogError(
                    $"[Eira] El parámetro '{name}' del Animator es {p.type}, " +
                    $"pero EiraAnimationController escribe {type}. " +
                    $"Corrígelo en el Animator Controller o ejecuta " +
                    $"'Eira/Reparar nivel (jugable)' para regenerarlo.", animator);
                break;
            }

            return false;
        }

        /// <summary>
        /// Vuelca el estado real de Eira en el Animator.
        /// </summary>
        /// <param name="horizontalSpeed">Velocidad horizontal en m/s.</param>
        /// <param name="localDirection">
        /// Dirección de movimiento en espacio local del personaje (x = lateral,
        /// y = adelante). Permite que el Animator haga blend de strafe.
        /// </param>
        /// <param name="movingInput">
        /// true mientras el jugador mantiene una tecla de movimiento. Es lo que
        /// hace que Eira esté caminando en el instante en que se pulsa, y que
        /// se pare en el instante en que se suelta, en vez de arrastrar el paso
        /// un tiempo fijo después de soltar.
        /// </param>
        public void Drive(
            float horizontalSpeed,
            Vector2 localDirection,
            bool grounded,
            float verticalSpeed,
            bool crouching,
            bool sprinting,
            bool jumping,
            bool movingInput)
        {
            if (animator == null)
                return;

            if (hasSpeed)
            {
                // Velocidad ABSOLUTA en m/s. El blend tree del Animator
                // usa umbrales en m/s (0 · caminar · correr), así el mismo
                // número sirve para el rig y para la depuración.
                //
                // Cuando el jugador suelta la tecla la velocidad objetivo es 0,
                // y al no haber nada que frene el paso se baja a 0 en un solo
                // frame en vez de deslizarse un tiempo fijo.
                //
                // El suavizado va por exponencial y NO usa el damping del
                // Animator: el damping de Unity está calibrado a 60 Hz, así
                // que a 30 o 144 fps la animación iría a distinta velocidad.
                float target = movingInput
                    ? Mathf.Max(0f, horizontalSpeed)
                    : 0f;

                float k = target <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-18f * Time.deltaTime);

                animator.SetFloat(
                    speedHash,
                    Mathf.Lerp(animator.GetFloat(speedHash), target, k)
                );
            }

            if (hasGrounded)
                animator.SetBool(groundedHash, grounded);

            if (hasVerticalSpeed)
                animator.SetFloat(verticalSpeedHash, verticalSpeed);

            if (hasFalling)
                animator.SetBool(fallingHash, !grounded && verticalSpeed < -0.1f);

            if (hasCrouch)
                animator.SetBool(crouchHash, crouching);

            if (hasMoving)
                animator.SetBool(movingHash, movingInput || horizontalSpeed > 0.15f);

            if (hasSprint)
                animator.SetBool(sprintHash, sprinting && grounded && movingInput);

            if (hasJump)
            {
                // Si el Animator vuelve a Locomotion mientras Eira sigue
                // subiendo, el salto se corta a la mitad visualmente. Se
                // vuelve a lanzar el trigger para que el estado Jump se
                // mantenga hasta que aterrice.
                if (jumping && grounded)
                    animator.ResetTrigger(jumpHash);
            }

            if (hasStrafe)
            {
                float target = Mathf.Clamp(localDirection.x, -1f, 1f);
                float k = 1f - Mathf.Exp(-18f * Time.deltaTime);

                animator.SetFloat(
                    strafeHash,
                    Mathf.Lerp(animator.GetFloat(strafeHash), target, k)
                );
            }
        }

        /// <summary>Dispara el trigger de salto una sola vez.</summary>
        public void TriggerJump()
        {
            if (animator == null || !hasJump)
                return;

            animator.ResetTrigger(jumpHash);
            animator.SetTrigger(jumpHash);
        }

        /// <summary>
        /// Fuerza la reevaluación de los parámetros, por si el Animator se
        /// asigna después del Awake (p. ej. al instanciar desde pool).
        /// </summary>
        public void Refresh()
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            CacheParameters();
        }
    }
}
