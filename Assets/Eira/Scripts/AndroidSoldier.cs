using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Androide militar de la ciudad de 3000. Patrulla entre nodos, detecta a
    /// Eira y la persigue para golpearla. Se le puede disparar (arma) o
    /// devolverle el golpe (defensa / pulso), ambos llegan por <see cref="IDamageable"/>.
    /// Al morir suma <see cref="Level2Points.Android"/>.
    /// </summary>
    public class AndroidSoldier : MonoBehaviour, IDamageable
    {
        [Header("Patrulla")]
        public Vector3[] patrolNodes;
        public float patrolSpeed = 2.0f;
        public float chaseSpeed = 4.2f;

        [Header("Combate")]
        public float maxHealth = 60f;
        public float detectRadius = 11f;
        public float loseRadius = 18f;
        public float attackRange = 2.2f;
        public float attackDamage = 12f;
        public float attackPeriod = 1.4f;
        public float attackWindup = 0.5f;

        [Header("Puntos")]
        public int scoreOnDestroy = Level2Points.Android;

        public event System.Action OnDeath;

        public bool IsAlive => !dead;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

        float health;
        int nodeIndex;
        bool dead;
        bool chasing;
        float attackTimer;
        float windupTimer;
        float lastSeenTimer;
        PlayerController player;
        Renderer[] renderers;
        Color[] baseColors;

        static readonly Color AlertColor = new Color(1f, 0.3f, 0.2f);
        static readonly Color CalmColor = new Color(0.8f, 0.85f, 0.9f);

        void Start()
        {
            health = maxHealth;
            player = GameManager.Instance != null ? GameManager.Instance.Player : FindAnyObjectByType<PlayerController>();
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i] != null ? renderers[i].material.color : Color.white;
        }

        void Update()
        {
            if (dead || player == null) return;

            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing)
                return;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            bool los = dist <= (chasing ? loseRadius : detectRadius);

            if (los)
            {
                lastSeenTimer = 0f;
                chasing = true;
            }
            else
            {
                lastSeenTimer += Time.deltaTime;
                if (lastSeenTimer > 2.5f) chasing = false;
            }

            if (chasing && dist <= attackRange)
                TickAttack();
            else
                Move(dist, toPlayer);
        }

        void Move(float dist, Vector3 toPlayer)
        {
            Vector3 dir;

            if (chasing)
            {
                dir = dist > 0.05f ? toPlayer / dist : Vector3.zero;
                transform.position += dir * chaseSpeed * Time.deltaTime;
                if (dir.sqrMagnitude > 0.001f)
                    transform.forward = Vector3.Lerp(transform.forward, dir, 8f * Time.deltaTime);
            }
            else
            {
                if (patrolNodes == null || patrolNodes.Length == 0) return;
                Vector3 target = patrolNodes[nodeIndex];
                Vector3 flat = target - transform.position;
                flat.y = 0f;
                if (flat.magnitude < 0.4f)
                {
                    nodeIndex = (nodeIndex + 1) % patrolNodes.Length;
                    return;
                }
                dir = flat.normalized;
                transform.position += dir * patrolSpeed * Time.deltaTime;
                transform.forward = Vector3.Lerp(transform.forward, dir, 6f * Time.deltaTime);
            }
        }

        void TickAttack()
        {
            if (attackTimer > 0f)
                attackTimer -= Time.deltaTime;

            if (windupTimer <= 0f && attackTimer <= 0f)
                windupTimer = 0.0001f;

            if (windupTimer > 0f)
            {
                windupTimer += Time.deltaTime;
                SetAlert(true);

                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.001f)
                    transform.forward = Vector3.Lerp(transform.forward, toPlayer.normalized, 8f * Time.deltaTime);

                if (windupTimer >= attackWindup)
                {
                    windupTimer = 0f;
                    attackTimer = attackPeriod;
                    SetAlert(false);
                    if (player != null)
                    {
                        AudioFX.DroneHit();
                        player.TakeDamage(attackDamage, gameObject);
                    }
                }
            }
        }

        void SetAlert(bool alert)
        {
            Color c = alert ? AlertColor : CalmColor;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                {
                    var mpb = new MaterialPropertyBlock();
                    mpb.SetColor("_BaseColor", c);
                    mpb.SetColor("_Color", c);
                    renderers[i].SetPropertyBlock(mpb);
                }
        }

        public bool TakeHit(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            if (dead) return false;

            health -= amount;
            CombatFX.Impact(point, new Color(1f, 0.6f, 0.3f));

            if (health <= 0f)
            {
                Die(source);
                return true;
            }

            SetAlert(true);
            return false;
        }

        void Die(GameObject source)
        {
            if (dead) return;
            dead = true;

            var gm = GameManager.Instance;
            if (gm != null && scoreOnDestroy > 0)
                gm.AddScore(scoreOnDestroy);

            if (Level2Director.Instance != null)
                Level2Director.Instance.OnAndroidKilled();
            if (Level3Director.Instance != null)
                Level3Director.Instance.OnControlledKilled();

            AudioFX.DroneHit();
            EnvOrb.Spawn(transform.position + Vector3.up, new Color(1f, 0.5f, 0.2f));
            OnDeath?.Invoke();
            Destroy(gameObject, 0.05f);
        }
    }
}
