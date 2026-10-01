using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Arma de Eira. Dispara con el botón izquierdo del ratón o el gatillo
    /// derecho, y -esto es lo importante- dispara MIENTRAS se mantiene
    /// pulsado: no hay ni temporizadores ni animación bloqueante. El primer
    /// disparo sale en el mismo frame de la pulsación y, si se sigue
    /// teniendo pulsado, se repite al ritmo de <see cref="shotsPerSecond"/>.
    ///
    /// Apunta desde la cámara hacia el centro de la pantalla, que es donde
    /// mira el jugador, y no desde el pecho de Eira: así el proyectil va
    /// exactamente al punto que señala la mira.
    /// </summary>
    public class EiraWeapon : MonoBehaviour
    {
        [Header("Disparo")]
        [Tooltip("Projectiles por segundo mientras se mantiene pulsado.")]
        [SerializeField] private float shotsPerSecond = 6f;

        [Tooltip("Daño por impacto.")]
        [SerializeField] private float damage = 12f;

        [Tooltip("Coración que cuesta cada disparo. 0 = disparo gratis.")]
        [SerializeField] private float heartCost = 0f;

        [Header("Ámbito de apuntado")]
        [Tooltip("Distancia máxima a la que se busca un objetivo al disparar.")]
        [SerializeField] private float maxAimDistance = 90f;

        [Tooltip("Cámara desde la que se calcula la dirección del disparo.")]
        [SerializeField] private Camera aimCamera;

        [Header("Presentación")]
        [SerializeField] private Color tracerColor = new Color(0.35f, 0.95f, 1f);

        [Tooltip("Segundos que el arma está bloqueada tras disparar (evita spam).")]
        [SerializeField] private float minInterval = 0.08f;

        float cooldown;
        PlayerController player;

        public bool IsFiring { get; private set; }

        /// <summary>True solo en el frame en que sale un disparo, para que el HUD pueda reactsionar.</summary>
        public bool FiredThisFrame { get; private set; }

        void Awake()
        {
            player = GetComponent<PlayerController>();

            if (aimCamera == null)
                aimCamera = Camera.main;

            if (aimCamera == null)
                aimCamera = FindObjectOfType<Camera>();
        }

        void Start()
        {
            if (aimCamera == null)
                aimCamera = Camera.main;
        }

        /// <summary>
        /// Se llama desde el Update del jugador, no desde un Update propio,
        /// para que el disparo use exactamente el mismo frame que la
        /// animación y el estado del juego.
        /// </summary>
        public void Tick()
        {
            FiredThisFrame = false;

            if (player == null || player.PlayerState != GameState.Playing)
            {
                IsFiring = false;
                return;
            }

            float dt = Time.deltaTime;

            if (cooldown > 0f)
                cooldown = Mathf.Max(0f, cooldown - dt);

            bool held = EiraInput.AttackHeld();

            if (!held)
            {
                IsFiring = false;

                // Al soltar, el arma queda lista al instante: la siguiente
                // pulsación dispara sin esperar.
                cooldown = 0f;
                return;
            }

            IsFiring = true;

            float interval = Mathf.Max(
                minInterval,
                1f / Mathf.Max(0.1f, shotsPerSecond));

            if (cooldown > 0f)
                return;

            if (heartCost > 0f && player.Heart < heartCost)
                return;

            Shoot();
            cooldown = interval;
        }

        void Shoot()
        {
            Vector3 origin;
            Vector3 direction;

            ComputeAim(out origin, out direction);

            if (heartCost > 0f && player != null)
                player.RestoreHeart(-heartCost);

            Projectile.Fire(origin, direction, damage, transform, tracerColor);

            CombatFX.MuzzleFlash(origin, tracerColor);
            AudioFX.Shoot();

            FiredThisFrame = true;
        }

        /// <summary>
        /// Dirección del disparo: rayo desde la cámara hacia el centro de la
        /// pantalla. Si no hay cámara (o no hay nada delante), se usa la
        /// mirada de Eira como reserva para que el arma nunca se quede muda.
        /// </summary>
        void ComputeAim(out Vector3 origin, out Vector3 direction)
        {
            origin = transform.position + Vector3.up * 1.25f;
            direction = transform.forward;

            if (aimCamera == null)
                return;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    maxAimDistance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                direction = (hit.point - origin).normalized;
                return;
            }

            // Sin impacto: se apunta a lo lejos por el centro de pantalla.
            direction = (ray.GetPoint(maxAimDistance) - origin).normalized;
        }
    }
}
