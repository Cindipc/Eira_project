using UnityEngine;

namespace EiraGame
{
    public enum DroneType
    {
        Pursuer,
        Ranged,
        Tank
    }

    public class DroneController : MonoBehaviour, IDamageable
    {
        [Header("Archetype")]
        public DroneType type = DroneType.Pursuer;

        [Header("Combate")]
        [Tooltip("Vida del dron. A 0 se destruye.")]
        public float maxHealth = 40f;

        [Tooltip("Puntos al destruirlo.")]
        public int scoreOnDestroy = 150;

        [Tooltip("Radio de la esfera de impacto. El dron no trae collider en la jerarquia, asi que esta esfera es lo que puede golpear una bala.")]
        public float hitRadius = 0.35f;

        public Vector3[] patrolNodes;
        public float detectRadius = 9f;
        public float attackRange = 2.6f;

        [Tooltip("Distancia que intenta mantener con Eira mientras la persigue. Sin esto el dron se le sube encima y no deja jugar.")]
        public float standoffDistance = 2.0f;

        [Tooltip("Velocidad con la que orbita alrededor de Eira cuando ya esta a distancia.")]
        public float orbitSpeed = 55f;

        [Tooltip("Radio minimo entre drones para que no se apilen unos sobre otros.")]
        public float separationRadius = 1.8f;

        [Tooltip("Altura de vuelo sobre la cabeza de Eira.")]
        public float hoverHeight = 1.7f;

        public float patrolSpeed = 2.2f;
        public float chaseSpeed = 4.8f;

        [Tooltip("Daño por golpe.")]
        public float attackDamage = 10f;

        [Tooltip("Segundos entre golpes de un mismo dron.")]
        public float attackPeriod = 1.8f;

        [Tooltip("Segundos de aviso (los ojos cargan) antes de que el golpe conecte. Es la ventana para bloquear o apartarse.")]
        public float attackWindup = 0.6f;

        [Tooltip("Separacion minima entre los golpes de drones distintos, para que no ataquen todos a la vez.")]
        public float globalStrikeGap = 0.35f;

        public Transform eyeLight;
        public Transform eyeLight2;

        [Tooltip("Puntos que devuelve un pulso defensivo de Eira.")]
        public float pulseReflectDamage = 14f;

        float health;
        int nodeIndex;
        Vector3 basePos;
        float phase;
        float lastSeenTimer;
        float attackTimer;
        float windupTimer;
        float orbitAngle;
        float orbitDir = 1f;
        bool chasing;
        bool awarded;
        bool gaveDamage;
        Vector3 moveDir;
        Vector3 knock;
        float stunTimer;
        bool dead;
        bool lastAlert;
        bool bodiesCleared;

        // Token global: evita que varios drones golpeen en el mismo instante.
        static float nextStrikeTime;

        // Evento para notificar cuando el dron muere
        public event System.Action OnDeath;

        public bool IsChasing => chasing;
        public float Health => health;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        public bool IsDead => dead;
        public bool IsAlive => !dead;

        /// <summary>Se enciende cuando el dron recibe un disparo, para el HUD.</summary>
        public bool HitFlash { get; private set; }

        static readonly Color AlertColor = new Color(1f, 0.25f, 0.2f);
        static readonly Color DangerColor = new Color(1f, 0.05f, 0.05f);
        static readonly Color CalmColor = new Color(0.3f, 0.95f, 1f);

        Renderer[] bodyRenderers;
        MaterialPropertyBlock hurtBlock;
        float flashTimer;
        Color[] baseColors;
        SphereCollider hitCollider;

        void Start()
        {
            basePos = transform.position;
            phase = Random.value * 10f;
            health = maxHealth;
            orbitAngle = Random.value * Mathf.PI * 2f;
            orbitDir = Random.value < 0.5f ? -1f : 1f;

            if (type == DroneType.Tank)
            {
                maxHealth *= 2.5f;
                health = maxHealth;
                attackDamage *= 1.5f;
                attackWindup *= 1.2f;
            }

            SanitizePatrolNodes();

            EnsureHitCollider();
            CacheBodyRenderers();
            ApplyEyeColor(CalmColor);
        }

        /// <summary>
        /// Los nodos de patrulla se guardaron en coordenadas de mundo antes de
        /// recolocar el nivel, asi que algunos quedaron a decenas de metros del
        /// dron: al darles el control salian volando fuera de la zona. Aqui se
        /// detecta ese caso y se genera un recorrido local alrededor del punto
        /// de aparicion real.
        /// </summary>
        void SanitizePatrolNodes()
        {
            const float maxNodeDistance = 14f;

            bool stale = patrolNodes == null || patrolNodes.Length == 0;

            if (!stale)
            {
                for (int i = 0; i < patrolNodes.Length; i++)
                {
                    if ((patrolNodes[i] - basePos).sqrMagnitude > maxNodeDistance * maxNodeDistance)
                    {
                        stale = true;
                        break;
                    }
                }
            }

            if (!stale)
                return;

            // Recorrido cuadrado de 5 m alrededor del punto de patrulla.
            const float r = 5f;
            patrolNodes = new[]
            {
                basePos + new Vector3(-r, 0f, -r),
                basePos + new Vector3(r, 0f, -r),
                basePos + new Vector3(r, 0f, r),
                basePos + new Vector3(-r, 0f, r)
            };
        }

        /// <summary>
        /// Le da al dron una esfera solida alrededor del cuerpo. No lleva
        /// Rigidbody, asi que no empuja nada ni altera la fisica del nivel:
        /// solo existe para que las consultas (el barrido de la bala, el
        /// apuntado) lo encuentren.
        /// </summary>
        void EnsureHitCollider()
        {
            hitCollider = GetComponent<SphereCollider>();

            if (hitCollider == null)
                hitCollider = gameObject.AddComponent<SphereCollider>();

            hitCollider.isTrigger = false;
            hitCollider.center = new Vector3(0f, 0.05f, 0f);
            hitCollider.radius = Mathf.Max(0.05f, hitRadius);

            IgnoreCollisionsWithPlayer();
        }

        void IgnoreCollisionsWithPlayer()
        {
            PlayerController[] pcs =
                FindObjectsByType<PlayerController>();

            for (int i = 0; i < pcs.Length; i++)
            {
                if (pcs[i] != null)
                    IgnoreCollisionsWith(pcs[i].GetComponentsInChildren<Collider>());
            }

            NovaCompanion[] novas =
                FindObjectsByType<NovaCompanion>();

            for (int i = 0; i < novas.Length; i++)
            {
                if (novas[i] != null)
                    IgnoreCollisionsWith(novas[i].GetComponentsInChildren<Collider>());
            }
        }

        void IgnoreCollisionsWith(Collider[] others)
        {
            if (others == null || hitCollider == null)
                return;

            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] == null || others[i] == hitCollider)
                    continue;

                Physics.IgnoreCollision(hitCollider, others[i], true);
            }
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

            // Ranged drones strafe while chasing
            if (type == DroneType.Ranged && chasing)
            {
                float strafeX = Mathf.Sin(phase * 2f) * 3f;
                desired += transform.right * strafeX;
            }

            transform.position = Vector3.MoveTowards(
                transform.position,
                desired,
                GetSpeed(player) * Time.deltaTime);

            // retroceso del pulso defensivo de Eira / impactos
            if (knock.sqrMagnitude > 0.0004f)
            {
                transform.position += knock * Time.deltaTime;
                knock = Vector3.MoveTowards(knock, Vector3.zero, 6f * Time.deltaTime);
            }
            stunTimer = Mathf.Max(0f, stunTimer - Time.deltaTime);

            // orientación visual
            var v = desired - transform.position;
            v.y = 0f;
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

            if (sensed)
            {
                chasing = true;
                lastSeenTimer = 0f;

                if (!bodiesCleared)
                {
                    bodiesCleared = true;
                    IgnoreCollisionsWithPlayer();
                }
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

            if (chasing != lastAlert)
            {
                lastAlert = chasing;
                if (!chasing)
                    ApplyEyeColor(CalmColor);
            }

            TickAttack(player, hDist, los);
        }

        /// <summary>
        /// Ataque con aviso. El dron solo golpea si esta a rango, ve a Eira y
        /// no esta aturdido; primero carga (los ojos pasan a rojo vivo) durante
        /// <see cref="attackWindup"/> segundos. Si Eira se aleja o bloquea a
        /// tiempo, el golpe no sale. Ademas se impone una separacion global
        /// para que varios drones no la fulminen en el mismo frame.
        /// </summary>
        void TickAttack(PlayerController player, float hDist, bool los)
        {
            if (stunTimer > 0f)
            {
                windupTimer = 0f;
                RestoreEyeGlow();
                return;
            }

            if (attackTimer > 0f)
                attackTimer -= Time.deltaTime;

            if (type == DroneType.Ranged)
            {
                // Ranged: ataca desde lejos, dispara proyectiles
                float rangedRange = attackRange * 2.5f;
                bool canShoot = chasing && los && hDist <= rangedRange;

                if (!canShoot)
                {
                    windupTimer = Mathf.Max(0f, windupTimer - Time.deltaTime * 2f);
                    RestoreEyeGlow();
                    return;
                }

                if (windupTimer <= 0f && attackTimer <= 0f)
                    windupTimer = 0.0001f;

                if (windupTimer > 0f)
                {
                    windupTimer += Time.deltaTime;
                    ChargeEyeGlow(windupTimer / Mathf.Max(0.05f, attackWindup));

                    if (windupTimer >= attackWindup)
                    {
                        if (Time.time < nextStrikeTime)
                        {
                            windupTimer = attackWindup * 0.9f;
                            return;
                        }
                        FireProjectile(player);
                    }
                }
            }
            else
            {
                // Pursuer and Tank: attack physically
                bool canReach = chasing && los && hDist <= attackRange;

                if (!canReach)
                {
                    windupTimer = Mathf.Max(0f, windupTimer - Time.deltaTime * 2f);
                    RestoreEyeGlow();
                    return;
                }

                if (windupTimer <= 0f && attackTimer <= 0f)
                    windupTimer = 0.0001f;

                if (windupTimer > 0f)
                {
                    windupTimer += Time.deltaTime;
                    ChargeEyeGlow(windupTimer / Mathf.Max(0.05f, attackWindup));

                    if (windupTimer >= attackWindup)
                    {
                        if (Time.time < nextStrikeTime)
                        {
                            windupTimer = attackWindup * 0.9f;
                            return;
                        }
                        Strike(player);
                    }
                }
            }
        }

        void FireProjectile(PlayerController player)
        {
            windupTimer = 0f;
            attackTimer = attackPeriod;
            nextStrikeTime = Time.time + globalStrikeGap;
            gaveDamage = true;

            Vector3 dir = (player.transform.position + Vector3.up * 1.2f) - transform.position;
            Projectile.Fire(
                transform.position,
                dir,
                attackDamage,
                transform,
                Color.red,
                true // isEnemy
            );
            ApplyEyeColor(AlertColor);
        }

        void Strike(PlayerController player)
        {
            windupTimer = 0f;
            attackTimer = attackPeriod;
            nextStrikeTime = Time.time + globalStrikeGap;
            gaveDamage = true;

            AudioFX.DroneHit();
            player.TakeDamage(attackDamage, gameObject);
            ApplyEyeColor(AlertColor);
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

        /// <summary>
        /// Contrato <see cref="IDamageable"/>: lo usan el reflejo de la defensa
        /// de Eira y su pulso.
        /// </summary>
        public bool TakeHit(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            if (dead)
                return false;

            health -= amount;
            HitFlash = true;
            flashTimer = 0.12f;
            CombatFX.Impact(point, new Color(0.4f, 0.95f, 1f));

            if (direction.sqrMagnitude > 0.0001f)
                knock += -direction.normalized * 4.5f;

            if (health <= 0f)
            {
                DestroyDrone(source != null ? source.transform : null);
                return true;
            }

            // El golpe devuelto tambien aturde: es lo que convierte defenderse
            // en una jugada ofensiva.
            stunTimer = Mathf.Max(stunTimer, 0.6f);
            chasing = false;
            AudioFX.DroneHit();
            return false;
        }

        /// <summary>
        /// Destruye el dron públicamente (para explosiones, etc.)
        /// </summary>
        public void DestroyDrone()
        {
            DestroyDrone(null);
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

            OnDeath?.Invoke();

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
            SetLight(eyeLight, color, null);
            SetLight(eyeLight2, color, null);
        }

        /// <summary>El aviso de golpe: los ojos se encienden en rojo y sube su brillo.</summary>
        void ChargeEyeGlow(float t)
        {
            Color c = Color.Lerp(AlertColor, DangerColor, Mathf.Clamp01(t));
            float intensity = Mathf.Lerp(1.4f, 6f, Mathf.Clamp01(t));
            SetLight(eyeLight, c, intensity);
            SetLight(eyeLight2, c, intensity);
        }

        void RestoreEyeGlow()
        {
            SetLight(eyeLight, null, lastAlert ? 2.4f : 1.4f);
            SetLight(eyeLight2, null, lastAlert ? 2.4f : 1.4f);
        }

        void SetLight(Transform t, Color? color, float? intensity)
        {
            if (t == null)
                return;

            var l = t.GetComponent<Light>();
            if (l == null)
                return;

            if (color.HasValue)
                l.color = color.Value;

            if (intensity.HasValue)
                l.intensity = intensity.Value;
        }

        Vector3 GetDesiredPosition(PlayerController player)
        {
            if (chasing && player != null)
            {
                Vector3 target = player.transform.position;
                Vector3 flat = new Vector3(transform.position.x - target.x, 0f, transform.position.z - target.z);
                float dist = flat.magnitude;
                Vector3 radial = dist > 0.01f ? flat / dist : transform.forward;

                Vector3 planar;

                if (dist > standoffDistance + 1.2f)
                {
                    // Aún lejos: se acerca en línea recta hasta la distancia de combate.
                    planar = new Vector3(target.x, 0f, target.z) + radial * standoffDistance;
                }
                else
                {
                    // A distancia: orbita a su alrededor y no se le sube encima.
                    orbitAngle += orbitSpeed * Mathf.Deg2Rad * orbitDir * Time.deltaTime;
                    Vector3 offset = new Vector3(Mathf.Sin(orbitAngle), 0f, Mathf.Cos(orbitAngle)) * standoffDistance;
                    planar = new Vector3(target.x, 0f, target.z) + offset;
                }

                planar += SeparationOffset();

                float y = target.y + hoverHeight + Mathf.Sin(phase * 3f) * 0.15f;
                return new Vector3(planar.x, y, planar.z);
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

        /// <summary>Empuja suavemente al dron lejos de sus compañeros para que no se apilen.</summary>
        Vector3 SeparationOffset()
        {
            if (separationRadius <= 0f)
                return Vector3.zero;

            Collider[] nearby = Physics.OverlapSphere(
                transform.position,
                separationRadius,
                ~0,
                QueryTriggerInteraction.Ignore);

            Vector3 push = Vector3.zero;
            int count = 0;

            for (int i = 0; i < nearby.Length; i++)
            {
                if (nearby[i] == null)
                    continue;

                DroneController other =
                    nearby[i].GetComponentInParent<DroneController>();

                if (other == null || other == this || other.IsDead)
                    continue;

                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float d = away.magnitude;

                if (d < 0.0001f)
                {
                    away = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f);
                    d = 0.1f;
                }

                push += away / d * (1f - d / separationRadius);
                count++;
            }

            if (count == 0)
                return Vector3.zero;

            return push * 1.4f;
        }

        float GetSpeed(PlayerController player)
        {
            if (type == DroneType.Tank) return chaseSpeed * 0.6f;
            return chasing ? chaseSpeed : patrolSpeed;
        }

        /// <summary>
        /// El pulso defensivo de Eira: empuja al dron, lo aturde y le hace
        /// daño, para que defenderse sirva para algo mas que sobrevivir.
        /// </summary>
        public void OnDefensePulse(Vector3 origin)
        {
            OnDefensePulse(origin, pulseReflectDamage);
        }

        public void OnDefensePulse(Vector3 origin, float damage)
        {
            if (dead)
                return;

            var away = transform.position - origin;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.back;
            knock = away.normalized * 9f;
            stunTimer = 1.4f;
            chasing = false;
            windupTimer = 0f;

            TakeHit(damage, transform.position, away.normalized, null);
        }

        /// <summary>
        /// Deja al dron aturdido los segundos indicados (sin atacar ni
        /// perseguir). Lo usa el bloqueo perfecto de Eira.
        /// </summary>
        public void Stun(float duration)
        {
            if (dead || duration <= 0f)
                return;

            stunTimer = Mathf.Max(stunTimer, duration);
            windupTimer = 0f;
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
                if (BelongsToThisDrone(hit.collider)) return true;

                if (hit.collider.isTrigger) return true;
                return hit.collider.GetComponent<PlayerController>() != null || hit.collider.GetComponentInParent<PlayerController>() != null;
            }
            return true;
        }

        bool BelongsToThisDrone(Collider col)
        {
            if (col == null || hitCollider == null)
                return false;

            return col.transform == transform || col.transform.IsChildOf(transform);
        }
    }
}
