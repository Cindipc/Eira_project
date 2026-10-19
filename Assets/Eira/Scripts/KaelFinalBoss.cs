using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Jefe final del Nivel 3: Kael, conectado al Nucleo Central. Tiene cuatro
    /// fases:
    ///   1) ataques fisicos y tecnologicos,
    ///   2) controla androides (invoca refuerzos),
    ///   3) usa el poder del Nucleo (onda de choque),
    ///   4) cae y Eira debe acceder al sistema central con su sangre.
    /// Es derrotable (tiene vida), a diferencia de su primera aparicion.
    /// </summary>
    public class KaelFinalBoss : MonoBehaviour, IDamageable
    {
        [Header("Vida y fases")]
        public float maxHealth = 420f;
        public float phase2At = 0.66f;
        public float phase3At = 0.33f;

        [Header("Movimiento y ataque")]
        public float moveSpeed = 2.2f;
        public float meleeRange = 3.2f;
        public float meleeDamage = 16f;
        public float meleePeriod = 1.6f;
        public float rangedDamage = 12f;
        public float rangedPeriod = 3.2f;
        public float shockwaveRadius = 7f;
        public float shockwaveDamage = 20f;

        [Header("Refuerzos (fase 2)")]
        public GameObject minionPrefab;
        public int minionsPerWave = 2;
        public float minionWavePeriod = 8f;
        public Transform[] minionSpawns;

        [Header("Refs")]
        public Transform core;
        public Light auraLight;

        float health;
        bool armed;
        bool arenaOpen;
        int phase = 1;
        float meleeTimer;
        float rangedTimer;
        float minionTimer;

        public bool IsAlive => phase < 4;
        public int Phase => phase;
        Level3Director director;

        void Start()
        {
            health = maxHealth;
        }

        public void Arm(Level3Director dir)
        {
            director = dir;
            armed = true;
        }

        /// <summary>Se llama cuando Eira desactiva los sistemas de seguridad: empieza el combate.</summary>
        public void OpenArena()
        {
            if (arenaOpen) return;
            arenaOpen = true;
            GameEvents.SetBossBar(1f);
            GameEvents.SetBossBar(HealthFrac());
            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossActive();
            GameEvents.NovaSpeak("Kael ha activado el Nucleo. Cuidado!", 3.5f);
        }

        float HealthFrac() => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;

        void Update()
        {
            if (core != null) core.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

            if (!armed || !arenaOpen || phase >= 4) return;

            var gm = GameManager.Instance;
            var player = gm != null ? gm.Player : null;
            if (player == null) return;

            if (auraLight != null)
                auraLight.intensity = 2f + Mathf.Sin(Time.time * 4f) * 0.6f;

            // --- Fase segun vida ---
            float frac = HealthFrac();
            int newPhase = frac > phase2At ? 1 : (frac > phase3At ? 2 : 3);
            if (newPhase != phase)
            {
                phase = newPhase;
                if (director != null) director.OnBossPhase(phase);
                if (phase >= 2 && auraLight != null) auraLight.color = new Color(1f, 0.4f, 0.2f);
                if (phase >= 3 && auraLight != null) auraLight.color = new Color(1f, 0.1f, 0.4f);
            }

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            float dist = toPlayer.magnitude;
            Vector3 dir = toPlayer.sqrMagnitude > 0.001f ? toPlayer.normalized : Vector3.forward;

            // Se mueve hacia Eira (fase 3 es mas agil).
            float speed = moveSpeed * (phase >= 3 ? 1.4f : 1f);
            if (dist > meleeRange * 0.85f)
                transform.position = Vector3.MoveTowards(transform.position,
                    new Vector3(player.transform.position.x, transform.position.y, player.transform.position.z),
                    speed * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 4f * Time.deltaTime);

            meleeTimer -= Time.deltaTime;
            rangedTimer -= Time.deltaTime;

            // Ataque cuerpo a cuerpo.
            if (dist < meleeRange && meleeTimer <= 0f)
            {
                meleeTimer = meleePeriod;
                player.TakeDamage(meleeDamage, gameObject);
                CombatFX.Impact(player.transform.position + Vector3.up, new Color(1f, 0.3f, 0.2f));
            }

            // Ataque a distancia (tecnologico).
            if (rangedTimer <= 0f && dist > meleeRange)
            {
                rangedTimer = rangedPeriod * (phase >= 3 ? 0.7f : 1f);
                Projectile.Fire(transform.position + Vector3.up * 2.2f, dir, rangedDamage, transform, new Color(1f, 0.2f, 0.5f), true);
                AudioFX.Shoot();
            }

            // Fase 2: invoca androides controlados.
            if (phase == 2)
            {
                minionTimer -= Time.deltaTime;
                if (minionTimer <= 0f)
                {
                    minionTimer = minionWavePeriod;
                    SpawnWave();
                }
            }

            // Fase 3: onda de choque usando el poder del Nucleo.
            if (phase >= 3)
            {
                if (Time.frameCount % 210 == 0)
                    Shockwave(player);
            }
        }

        void SpawnWave()
        {
            if (minionPrefab == null) return;
            int count = Mathf.Max(1, minionsPerWave);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 8f;
                if (minionSpawns != null && minionSpawns.Length > 0 && minionSpawns[i % minionSpawns.Length] != null)
                    pos = minionSpawns[i % minionSpawns.Length].position;

                var go = Instantiate(minionPrefab, pos, Quaternion.identity);
                go.SetActive(true);
            }
            GameEvents.NovaSpeak("Kael esta reclutando androides. Elimina a los controlados.", 3f);
        }

        void Shockwave(PlayerController player)
        {
            float d = Vector3.Distance(
                new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                new Vector3(transform.position.x, 0f, transform.position.z));

            GameEvents.SetBossBar(HealthFrac());
            AudioFX.Beep();

            if (d <= shockwaveRadius)
                player.TakeDamage(shockwaveDamage, gameObject);

            CombatFX.Impact(transform.position + Vector3.up, new Color(1f, 0.1f, 0.4f));
        }

        public bool TakeHit(float amount, Vector3 point, Vector3 direction, GameObject source)
        {
            if (phase >= 4 || !arenaOpen) return false;

            health -= Mathf.Max(0f, amount);
            GameEvents.SetBossBar(HealthFrac());
            CombatFX.Impact(point, new Color(1f, 0.7f, 0.2f));

            if (health <= 0f)
                BeginFinalPhase();

            return true;
        }

        void BeginFinalPhase()
        {
            health = 0f;
            phase = 4;
            GameEvents.SetBossBar(1f);
            AudioFX.Win();

            var fall = transform.rotation * Quaternion.Euler(0f, 0f, 90f);
            transform.rotation = fall;
            if (auraLight != null) auraLight.intensity = 0.6f;

            if (director != null) director.OnKaelDefeated();
            else
            {
                var gm = GameManager.Instance;
                if (gm != null) gm.AddScore(Level3Points.DefeatKael);
            }
        }
    }
}
