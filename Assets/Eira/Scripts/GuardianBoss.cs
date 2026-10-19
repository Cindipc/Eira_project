using UnityEngine;

namespace EiraGame
{
    // Jefe del Nivel 1: la Máquina Guardiana. Bloquea la salida del laboratorio.
    // Para desactivarla, Eira debe cargar 3 terminales con su habilidad mientras esquiva sus ataques.
    public class GuardianBoss : MonoBehaviour, IDamageable
    {
        public int neededTerminals = 3;
        public float moveSpeed = 1.4f;
        public float attackDamage = 22f;
        public float attackRange = 3.5f;
        public float attackPeriod = 2.6f;

        [Tooltip("Segundos de aviso (la luz roja se intensifica) antes de que el golpe conecte.")]
        public float attackWindup = 0.85f;

        public DoorControl exitDoor;
        public Transform core;
        public Transform eye;
        public Light redLight;

        // Punto del paisaje (bosque) que Eira encara al derrotar a la Maquina.
        // El bosque del Nivel 1 esta al oeste/norte, sobre el suelo plano.
        public Vector3 faceTarget = new Vector3(60f, 0f, 12f);

        int terminalsCharged;
        float stunTimer;
        float attackTimer;
        float windupTimer;
        bool active;
        bool defeated;
        Vector3 startPos;

        public bool IsActive => active;
        public bool IsAlive => !defeated;

        void Start()
        {
            startPos = transform.position;
        }

        void Update()
        {
            if (defeated) return;
            if (!active) return;

            var player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            if (stunTimer > 0f)
            {
                stunTimer -= Time.deltaTime;
                windupTimer = 0f;
                Flash(true);
                return;
            }

            Flash(false);
            var toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            if (dist > 1.8f)
            {
                transform.position = Vector3.MoveTowards(transform.position, new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z), moveSpeed * Time.deltaTime);
            }
            if (transform.childCount > 0)
            {
                var dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.forward;
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 4f * Time.deltaTime);
            }

            // Ataque con aviso: primero carga la luz roja, luego golpea.
            if (attackTimer > 0f)
                attackTimer -= Time.deltaTime;

            bool inRange = dist < attackRange;

            if (!inRange || windupTimer <= 0f && attackTimer > 0f)
            {
                windupTimer = Mathf.Max(0f, windupTimer - Time.deltaTime * 2f);
                return;
            }

            if (windupTimer <= 0f)
                windupTimer = 0.0001f;

            windupTimer += Time.deltaTime;
            float t = Mathf.Clamp01(windupTimer / Mathf.Max(0.05f, attackWindup));
            if (redLight != null) redLight.intensity = Mathf.Lerp(4f, 12f, t);

            if (windupTimer >= attackWindup)
            {
                windupTimer = 0f;
                attackTimer = attackPeriod;
                if (redLight != null) redLight.intensity = 4f;

                // Boss attack: can be a physical hit or a shockwave based on distance
                if (dist > 2f)
                {
                    // Ranged attack: shockwave projectile
                    Projectile.Fire(
                        transform.position + Vector3.up * 2f,
                        (player.transform.position - transform.position).normalized,
                        attackDamage,
                        transform,
                        Color.red,
                        true // isEnemy
                    );
                }
                else
                {
                    player.TakeDamage(attackDamage, gameObject);
                }
            }

            if (core != null) core.Rotate(0f, 90f * Time.deltaTime, 0f);
        }

        /// <summary>
        /// El jefe no tiene barra de vida: se derrota cargando los terminales.
        /// Pero un golpe devuelto (bloqueo o pulso) lo aturde un instante, que
        /// es lo que da la ventana para cargar el siguiente terminal.
        /// </summary>
        public bool TakeHit(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            if (defeated || !active)
                return false;

            stunTimer = Mathf.Max(stunTimer, 0.6f);
            windupTimer = 0f;
            CombatFX.Impact(point, new Color(0.5f, 0.95f, 1f));
            return false;
        }

        public void Stun(float duration)
        {
            if (defeated || !active || duration <= 0f)
                return;

            stunTimer = Mathf.Max(stunTimer, duration);
            windupTimer = 0f;
        }

        void Flash(bool tint)
        {
            if (eye == null) return;
            var r = eye.GetComponent<Renderer>();
            if (r != null)
            {
                float t = tint ? 1f : (terminalsCharged > 0 ? 0.4f : 0f);
                r.material.color = Color.Lerp(new Color(0.8f, 0.2f, 0.1f), Color.cyan, Mathf.Clamp01(t * 0.5f));
            }
            if (redLight != null) redLight.intensity = tint ? 8f : 4f;
        }

        public void Activate()
        {
            active = true;
            GameEvents.SetBossBar(1f);
            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossActive();
        }

        public void OnTerminalCharged()
        {
            if (defeated) return;
            terminalsCharged++;
            stunTimer = 3.2f;
            GameEvents.SetBossBar(Mathf.Max(0f, 1f - terminalsCharged / (float)neededTerminals));
            AudioFX.Ability();
            var gm = GameManager.Instance;
            if (gm != null) gm.OnTerminalChargedProgress(terminalsCharged, neededTerminals);

            if (terminalsCharged >= neededTerminals)
                Defeat();
        }

        void Defeat()
        {
            defeated = true;
            active = false;
            AudioFX.Win();
            GameEvents.BossDefeated();
            GameEvents.SetBossBar(0f);
            var fall = transform.rotation * Quaternion.Euler(0f, 0f, 90f);
            transform.rotation = fall;
            if (redLight != null) redLight.intensity = 0f;
            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossDefeated();
            if (exitDoor != null) exitDoor.Open();

            // Al caer la Maquina, Eira gira para encarar el paisaje (el bosque)
            // antes de que el Nivel 1 termine.
            var player = gm != null ? gm.Player : null;
            if (player != null)
                player.FaceTowards(faceTarget);
        }

        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && !active && !defeated) Activate();
        }
    }
}