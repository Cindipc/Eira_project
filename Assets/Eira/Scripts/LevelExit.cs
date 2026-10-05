using UnityEngine;

namespace EiraGame
{
    public class LevelExit : MonoBehaviour
    {
        [Header("Exit Settings")]
        [SerializeField] private string nextLevelScene = "";
        [SerializeField] private string exitMessage = "Has encontrado la salida.";
        [SerializeField] private float messageDuration = 3f;

        [Header("Requirements")]
        [SerializeField] private bool requireBossDefeated = true;
        [SerializeField] private bool requireAllMissions = false;
        [SerializeField] private int requiredInfoLogs = 0;

        [Header("Visual")]
        [SerializeField] private ParticleSystem exitEffect;
        [SerializeField] private Light exitLight;
        [SerializeField] private AudioClip exitSound;
        [SerializeField] private float activationDistance = 3f;

        [Header("Exit Sequence")]
        [SerializeField] private float exitDelay = 2f;
        [SerializeField] private bool autoLoadNextLevel = true;

        private bool isActivated;
        private bool playerInRange;
        private PlayerController player;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                col = gameObject.AddComponent<SphereCollider>();
                ((SphereCollider)col).radius = activationDistance;
            }
            col.isTrigger = true;

            if (exitLight != null)
            {
                exitLight.enabled = false;
            }
        }

        private void Update()
        {
            if (playerInRange && !isActivated && player != null)
            {
                float dist = Vector3.Distance(player.transform.position, transform.position);
                if (dist <= activationDistance)
                {
                    CheckRequirements();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                player = pc;
                playerInRange = true;
                UpdatePrompt();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc == player)
            {
                player = null;
                playerInRange = false;
                GameEvents.InteractPrompt("");
            }
        }

        private void CheckRequirements()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (requireBossDefeated && !gm.BossDefeated)
            {
                GameEvents.Subtitle("La salida está sellada. Derrota al Guardián primero.", 3f);
                GameEvents.NovaSpeak("Eira, la Máquina Guardiana bloquea la salida. ¡Derrótala!", 3f);
                return;
            }

            if (requireAllMissions)
            {
                for (int i = 0; i < gm.MissionsDone.Length; i++)
                {
                    if (!gm.MissionsDone[i] && i != 5)
                    {
                        GameEvents.Subtitle("Completa todas las misiones antes de salir.", 3f);
                        return;
                    }
                }
            }

            if (requiredInfoLogs > 0 && gm.InfoCollectedCount < requiredInfoLogs)
            {
                GameEvents.Subtitle($"Faltan {requiredInfoLogs - gm.InfoCollectedCount} registros de información.", 3f);
                return;
            }

            ActivateExit();
        }

        private void ActivateExit()
        {
            if (isActivated) return;
            isActivated = true;

            GameEvents.InteractPrompt("");

            if (exitEffect != null)
            {
                exitEffect.Play();
            }

            if (exitLight != null)
            {
                exitLight.enabled = true;
            }

            if (exitSound != null)
            {
                AudioSource.PlayClipAtPoint(exitSound, transform.position);
            }

            GameEvents.Subtitle(exitMessage, messageDuration);
            GameEvents.NovaSpeak("¡Lo logramos, Eira! La salida está libre.", messageDuration + 1f);

            if (autoLoadNextLevel && !string.IsNullOrEmpty(nextLevelScene))
            {
                Invoke(nameof(LoadNextLevel), exitDelay);
            }
            else
            {
                GameManager.Instance?.OnExitReached();
            }
        }

        private void LoadNextLevel()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextLevelScene);
        }

        private void UpdatePrompt()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            if (requireBossDefeated && !gm.BossDefeated)
            {
                GameEvents.InteractPrompt("Salida bloqueada: Derrota al Guardián");
            }
            else if (requiredInfoLogs > 0 && gm.InfoCollectedCount < requiredInfoLogs)
            {
                GameEvents.InteractPrompt($"Salida bloqueada: Faltan {requiredInfoLogs - gm.InfoCollectedCount} registros");
            }
            else
            {
                GameEvents.InteractPrompt("Presiona E para salir del nivel");
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = isActivated ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, activationDistance);

            if (exitLight != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 1f);
            }
        }
    }
}