using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Arma de Eira. Dispara con el botón izquierdo del ratón o el gatillo
    /// derecho. Si se mantiene pulsado sigue disparando al ritmo de
    /// <see cref="shotsPerSecond"/> (las bolas blancas de antes).
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
        [SerializeField] private float minInterval = 0.15f;

        float cooldown;
        PlayerController player;

        public bool IsFiring { get; private set; }

        /// <summary>True solo en el frame en que sale un disparo, para que el HUD pueda reactsionar.</summary>
        public bool FiredThisFrame { get; private set; }

        void Awake()
        {
            ResolvePlayer();

            if (aimCamera == null)
                aimCamera = Camera.main;

if (aimCamera == null)
                aimCamera = FindAnyObjectByType<Camera>();
        }

        /// <summary>
        /// El componente vive en un GameObject hijo de Eira (el "Weapon"), no
        /// en el mismo GameObject que el PlayerController. Con GetComponent()
        /// a secas se quedaba null, el Tick salia por la linea de guarda y el
        /// arma no disparaba nunca.
        /// </summary>
        void ResolvePlayer()
        {
            if (player == null)
                player = GetComponentInParent<PlayerController>();

            if (player == null)
                player = GetComponentInChildren<PlayerController>();

            if (player == null &&
                GameManager.Instance != null &&
                GameManager.Instance.Player != null)
            {
                player = GameManager.Instance.Player;
            }
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
            IsFiring = false;

            if (player == null)
                ResolvePlayer();

            if (player == null || player.PlayerState != GameState.Playing)
            {
                return;
            }

            float dt = Time.deltaTime;

            if (cooldown > 0f)
                cooldown = Mathf.Max(0f, cooldown - dt);

            bool held = EiraInput.AttackHeld();

            if (!held)
            {
                // Al soltar, el arma queda lista al instante: la siguiente
                // pulsacion dispara sin esperar.
                cooldown = 0f;
                return;
            }

            IsFiring = true;

            if (cooldown > 0f)
                return;

            if (heartCost > 0f && player.Heart < heartCost)
                return;

            Shoot();
            cooldown = Mathf.Max(minInterval, 1f / Mathf.Max(0.1f, shotsPerSecond));
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
            IsFiring = true;
        }

        /// <summary>
        /// Dirección del disparo: rayo desde la cámara hacia el centro de la
        /// pantalla. Con la cámara detrás de Eira ese rayo pasa por encima de
        /// su propio cuerpo, así que se recorren TODOS los impactos y se toma
        /// el primero que no sea de Eira ni de Nova. Si se cogiera el
        /// impacto contra Eira, la dirección casi horizontal dispararía la
        /// bola contra su propio hombro. Si no hay cámara (o no hay nada
        /// delante), se usa la mirada de Eira como reserva para que el arma
        /// nunca se quede muda.
        /// </summary>
        void ComputeAim(out Vector3 origin, out Vector3 direction)
        {
            Vector3 muzzle = transform.position + Vector3.up * 1.25f;

            origin = muzzle;
            direction = transform.forward;

            if (aimCamera == null)
                return;

            Ray ray = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            Vector3 aimPoint = ray.GetPoint(maxAimDistance);
            float bestDistance = float.MaxValue;
            bool found = false;

            RaycastHit[] hits = Physics.RaycastAll(
                ray,
                maxAimDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                if (BelongsToCast(hits[i].transform))
                    continue;

                if (hits[i].distance >= bestDistance)
                    continue;

                bestDistance = hits[i].distance;
                aimPoint = hits[i].point;
                found = true;
            }

            if (!found)
                aimPoint = ray.GetPoint(maxAimDistance);

            direction = (aimPoint - muzzle).normalized;

            // La boca del arma se adelanta un poco hacia el objetivo para no
            // nacer dentro de la propia capsula de Eira.
            origin = muzzle + direction * 0.35f;
        }

        /// <summary>
        /// Colliders que el rayo de apuntado tiene que ignorar: los de Eira y
        /// los de Nova. La cruz apunta a la mira de verdad, no al cuerpo de
        /// quien la lleva delante.
        /// </summary>
        bool BelongsToCast(Transform hit)
        {
            if (hit == null)
                return true;

            if (player != null &&
                (hit == player.transform || hit.IsChildOf(player.transform)))
            {
                return true;
            }

            return hit.name.IndexOf("NOVA", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Añade munición al arma (para pickups de munición).
        /// </summary>
        public void AddAmmo(int amount)
        {
            // Por ahora no hay límite de munición, pero el método existe
            // para compatibilidad con pickups. Si se implementa munición
            // limitada, aquí se incrementaría el contador.
            Debug.Log($"[EiraWeapon] AddAmmo called with {amount} (no ammo limit implemented yet)");
        }
    }
}
