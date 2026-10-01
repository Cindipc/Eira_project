using UnityEngine;

namespace EiraGame
{
    // Dron enemigo del Nivel 1: patrulla, detecta a Eira, la persigue y ataca por contacto.
    public class DroneController : MonoBehaviour
    {
        [Header("Combate")]
        [Tooltip("Vida del dron. A 0 se destruye.")]
        public float maxHealth = 40f;

        [Tooltip("Puntos al destruirlo.")]
        public int scoreOnDestroy = 150;

        public Vector3[] patrolNodes;
        public float detectRadius = 8f;
        public float attackRange = 2.8f;
        public float patrolSpeed = 2.2f;
        public float chaseSpeed = 5.2f;
        public float attackDamage = 12f;
        public float attackPeriod = 1.1f;
        public Transform eyeLight;
        public Transform eyeLight2;

        float health;
        int nodeIndex;
        Vector3 basePos;
        float phase;
        float lastSeenTimer;
        float attackTimer;
        bool chasing;
        bool awarded;
        bool gaveDamage;
        Vector3 moveDir;
        Vector3 knock;
        float stunTimer;
        bool dead;
        bool lastAlert;

        public bool IsChasing => chasing;
        public float Health => health;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        public bool IsDead => dead;

        /// <summary>Se enciende cuando el dron recibe un disparo, para el HUD.</summary>
        public bool HitFlash { get; private set; }

        static readonly Color AlertColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color CalmColor = new Color(0.3f, 0.95f, 1f);

        Renderer[] bodyRenderers;
        MaterialPropertyBlock hurtBlock;
        float flashTimer;
        Color[] baseColors;

        void Start()
        {
            basePos = transform.position;
            phase = Random.value * 10f;
            health = maxHealth;

            if (patrolNodes == null || patrolNodes.Length == 0)
                patrolNodes = new[] { basePos };

            CacheBodyRenderers();
            ApplyEyeColor(CalmColor);
        }

        void CacheBodyRenderers()
        {
            bodyRenderers = GetComponentsInChildren<Renderer>(true);

            if (bodyRenderers == null)
                return;

            baseColors = new Color[bodyRenderers.Length];

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null)
                {
                    baseColors[i] = Color.magenta;
                    continue;
                }

                baseColors[i] = bodyRenderers[i].material.color;
            }

            hurtBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            if (dead)
                return;

            UpdateHurtFlash();

            if (GameManager.Instance == null) return;
            var player = GameManager.Instance.Player;
            if (player == null) return;

            phase += Time.deltaTime;
            var desired = GetDesiredPosition(player);

            transform.position = Vector3.MoveTowards(transform.position, desired, GetSpeed(player) * Time.deltaTime);

            // retroceso del pulso defensivo de Eira
            if (knock.sqrMagnitude > 0.0004f)
            {
                transform.position += knock * Time.deltaTime;
                knock = Vector3.MoveTowards(knock, Vector3.zero, 6f * Time.deltaTime);
            }
            stunTimer = Mathf.Max(0f, stunTimer - Time.deltaTime);

            // orientación visual
            var v = desired - transform.position;
            if (v.magnitude > 0.15f)
            {
                moveDir = v.normalized;
                var look = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir, Vector3.up), 6f * Time.deltaTime);
                transform.rotation = look;
            }

            if (player.PlayerState != EiraGame.GameState.Playing) return;

            // detección
            float hDist = Vector3.Distance(new Vector3(player.transform.position.x, 0, player.transform.position.z), new Vector3(transform.position.x, 0, transform.position.z));
            bool los = HasLoS(player);
            float radius = detectRadius * player.DetectionMultiplier;
            bool sensed = hDist < radius && los;

            if (sensed && hDist > 1.2f)
            {
                chasing = true;
                lastSeenTimer = 0f;
            }
            else if (chasing)
            {
                lastSeenTimer += Time.deltaTime;
                if (lastSeenTimer > 3.5f)
                {
                    chasing = false;
                    if (!awarded && !gaveDamage)
                    {
                        awarded = true;
                        AudioFX.Beep();
                        var gm = GameManager.Instance;
                        if (gm != null) gm.OnDroneEvaded();
                    }
                }
            }

            // El color de los ojos cambia solo al cambiar el estado, no cada
            // frame: antes se recalmaba y se encendía el mesmo frame.
            if (chasing != lastAlert)
            {
                lastAlert = chasing;
                ApplyEyeColor(chasing ? AlertColor : CalmColor);
            }

            // ataque (suspendido mientras dura el aturdimiento del pulso defensivo)
            if (stunTimer <= 0f && hDist < attackRange && player.PlayerState == EiraGame.GameState.Playing)
            {
                attackTimer -= Time.deltaTime;
                if (attackTimer <= 0f)
                {
                    attackTimer = attackPeriod;
                    gaveDamage = true;
                    player.TakeDamage(attackDamage, gameObject);
                }
            }
        }

        // =========================================================
        // DAÑO
        // =========================================================

        /// <summary>
        /// Recibe un disparo. Devuelve true si ha muerto con este impacto.
        /// </summary>
        public bool OnShot(float amount, Vector3 incoming, Transform shooter)
        {
            if (dead)
                return false;

            health -= amount;
            HitFlash = true;
            flashTimer = 0.12f;

            // Retroceso en la dirección contraria al proyectil, y el dron
            // deja de perseguir durante un instante: se lee como impacto.
            knock += -incoming.normalized * 3.2f;

            if (health <= 0f)
            {
                DestroyDrone(shooter);
                return true;
            }

            stunTimer = Mathf.Max(stunTimer, 0.25f);
            AudioFX.DroneHit();
            return false;
        }

        void DestroyDrone(Transform shooter)
        {
            if (dead)
                return;

            dead = true;

            var gm = GameManager.Instance;

            if (gm != null)
                gm.OnDroneDestroyed(this, scoreOnDestroy, shooter);

            CombatFX.Explosion(
                transform.position,
                new Color(0.4f, 0.95f, 1f),
                14);

            // Se desactiva en vez de destruirlo: el GameManager ya ha sido
            // notificado y no queda nada colgando si algo lo busca después.
            gameObject.SetActive(false);
        }

        void UpdateHurtFlash()
        {
            HitFlash = false;

            if (flashTimer <= 0f)
                return;

            flashTimer -= Time.deltaTime;

            if (flashTimer <= 0f || bodyRenderers == null || hurtBlock == null)
                return;

            float t = flashTimer / 0.12f;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null)
                    continue;

                Color c = Color.Lerp(baseColors[i], Color.white, t);
                hurtBlock.Clear();
                hurtBlock.SetColor("_BaseColor", c);
                hurtBlock.SetColor("_Color", c);
                bodyRenderers[i].SetPropertyBlock(hurtBlock);
            }
        }

        /// <summary>Los "ojos" cambian de color según el estado: avisa sin HUD.</summary>
        void ApplyEyeColor(Color color)
        {
            SetLight(eyeLight, color);
            SetLight(eyeLight2, color);
        }

        void SetLight(Transform t, Color color)
        {
            if (t == null)
                return;

            var l = t.GetComponent<Light>();
            if (l != null)
                l.color = color;
        }

        Vector3 GetDesiredPosition(PlayerController player)
        {
            if (chasing && player != null)
            {
                var px = player.transform.position;
                return new Vector3(px.x, basePos.y + Mathf.Sin(phase * 3f) * 0.35f, px.z);
            }
            var node = patrolNodes[nodeIndex % patrolNodes.Length];
            var dir = node - transform.position;
            dir.y = 0f;
            if (dir.magnitude < 0.6f)
            {
                nodeIndex++;
                return transform.position;
            }
            var hover = new Vector3(node.x, basePos.y + Mathf.Sin(phase * 2.2f) * 0.35f, node.z);
            return hover;
        }

        float GetSpeed(PlayerController player) => chasing ? chaseSpeed : patrolSpeed;

        // Empuja el drón lejos de Eira y lo aturde un instante para que no ataque.
        public void OnDefensePulse(Vector3 origin)
        {
            var away = transform.position - origin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            knock = away.normalized * 9f;
            stunTimer = 1.4f;
            chasing = false;
        }

        bool HasLoS(PlayerController player)
        {
            if (player == null) return true;
            var origin = transform.position + Vector3.up * 0.3f;
            var target = player.transform.position + Vector3.up * 1.2f;
            var dist = Vector3.Distance(origin, target);
            if (dist > detectRadius * 1.4f) return false;
            if (Physics.Raycast(origin, (target - origin).normalized, out var hit, dist))
            {
                if (hit.collider.isTrigger) return true;
                return hit.collider.GetComponent<PlayerController>() != null || hit.collider.GetComponentInParent<PlayerController>() != null;
            }
            return true;
        }
    }
}