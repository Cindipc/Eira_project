using System.Collections;
using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Kael, el cazador. Primera aparicion: NO se le puede derrotar. Acecha a
    /// Eira, lanza androides y la persigue. Si Eira se queda a su alcance
    /// demasiado tiempo, la captura y pierde la partida. Pasado un tiempo,
    /// Kael se retira y deja el camino hacia el refugio rebelde.
    /// </summary>
    public class KaelBoss : MonoBehaviour
    {
        [Header("Activacion")]
        public float startRadius = 16f;
        public float captureRadius = 4.5f;
        public float captureTime = 6f;

        [Header("Combate")]
        public float attackRange = 3.5f;
        public float attackDamage = 18f;
        public float attackPeriod = 1.8f;

        [Header("Encuentro")]
        public float encounterDuration = 45f;
        public float moveSpeed = 3.4f;
        public float standoff = 3.0f;

        [Header("Refuerzos")]
        public GameObject minionPrefab;
        public int minions = 3;

        [TextArea(2, 4)] public string tauntLine = "Eira... te encontre por fin. No hay escapatoria de mi ciudad.";
        [TextArea(2, 4)] public string huntLine = "Corre. Me gusta que corran.";
        [TextArea(2, 4)] public string retreatLine = "Nos veremos de nuevo. La caceria no ha hecho mas que empezar.";

        bool started;
        bool done;
        float timer;
        float attackTimer;
        float captureTimer;
        PlayerController player;
        Renderer[] renderers;
        Color[] baseColors;

        public bool Started => started;
        public bool Done => done;

        void Start()
        {
            player = GameManager.Instance != null ? GameManager.Instance.Player : FindAnyObjectByType<PlayerController>();
            renderers = GetComponentsInChildren<Renderer>(true);
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i] != null ? renderers[i].material.color : Color.white;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (player == null || done) return;
            if (GameManager.Instance != null && GameManager.Instance.State != GameState.Playing) return;

            float dist = Vector3.Distance(player.transform.position, transform.position);

            if (!started)
            {
                if (dist <= startRadius) BeginEncounter();
                return;
            }

            timer += Time.deltaTime;

            FaceAndApproach(dist);

            if (dist <= captureRadius)
            {
                captureTimer += Time.deltaTime;
                if (captureTimer >= captureTime)
                {
                    Capture();
                    return;
                }
            }
            else
            {
                captureTimer = Mathf.Max(0f, captureTimer - Time.deltaTime);
            }

            if (attackTimer > 0f) attackTimer -= Time.deltaTime;
            if (dist <= attackRange && attackTimer <= 0f)
            {
                attackTimer = attackPeriod;
                AudioFX.DroneHit();
                player.TakeDamage(attackDamage, gameObject);
            }

            if (timer >= encounterDuration)
                Retreat();
        }

        void BeginEncounter()
        {
            started = true;
            timer = 0f;
            attackTimer = 1.5f;

            GameEvents.SetBossBar(1f);
            GameEvents.Subtitle(tauntLine, 3f);
            GameEvents.NovaSpeak("Ese es Kael. No puedes vencerle aqui, Eira. Corre hacia el refugio.", 4f);
            AudioFX.Door();

            if (minionPrefab != null)
                StartCoroutine(SpawnReinforcements());

            var gm = GameManager.Instance;
            if (gm != null) gm.OnBossActive();
        }

        IEnumerator SpawnReinforcements()
        {
            for (int i = 0; i < minions; i++)
            {
                if (done) yield break;
                Vector3 dir = Random.insideUnitSphere;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                Vector3 at = transform.position + dir.normalized * 6f;
                Instantiate(minionPrefab, at, Quaternion.identity);
                yield return new WaitForSeconds(1.2f);
            }
        }

        void FaceAndApproach(float dist)
        {
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
                transform.forward = Vector3.Lerp(transform.forward, toPlayer.normalized, 6f * Time.deltaTime);

            if (dist > standoff)
                transform.position += toPlayer.normalized * moveSpeed * Time.deltaTime;
        }

        void Capture()
        {
            if (done) return;
            done = true;
            GameEvents.SetBossBar(0f);
            GameEvents.Subtitle("Kael te ha capturado.", 3f);
            AudioFX.Lose();

            if (GameManager.Instance != null)
                GameManager.Instance.OnCapturedByKael();
        }

        void Retreat()
        {
            if (done) return;
            done = true;
            GameEvents.SetBossBar(0f);
            GameEvents.Subtitle(retreatLine, 3.5f);
            GameEvents.NovaSpeak(retreatLine, 4f);
            gameObject.SetActive(false);
        }
    }
}
