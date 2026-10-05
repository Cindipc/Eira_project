using UnityEngine;

namespace EiraGame
{
    public class LevelEntry : MonoBehaviour
    {
        [Header("Entry Settings")]
        [SerializeField] private string levelName = "Nivel 2: Calles Destruidas";
        [SerializeField] private string entryMessage = "Bienvenida a las ruinas de la ciudad.";
        [SerializeField] private float messageDuration = 4f;

        [Header("Spawn Point")]
        [SerializeField] private Transform playerSpawnPoint;
        [SerializeField] private Transform novaSpawnPoint;
        [SerializeField] private Transform cameraStartPoint;

        [Header("Visual")]
        [SerializeField] private ParticleSystem entryEffect;
        [SerializeField] private AudioClip entrySound;

        private bool hasTriggered;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
        }

        private void Start()
        {
            if (playerSpawnPoint == null)
            {
                playerSpawnPoint = transform;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasTriggered) return;

            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                hasTriggered = true;
                TriggerEntry(pc);
            }
        }

        public void TriggerEntry(PlayerController player)
        {
            if (entryEffect != null)
            {
                entryEffect.Play();
            }

            if (entrySound != null)
            {
                AudioSource.PlayClipAtPoint(entrySound, transform.position);
            }

            GameEvents.Subtitle($"[{levelName}]\n{entryMessage}", messageDuration);
            GameEvents.NovaSpeak($"Eira, entramos en {levelName}. {entryMessage}", messageDuration + 1f);

            if (playerSpawnPoint != null)
            {
                player.transform.position = playerSpawnPoint.position;
                player.transform.rotation = playerSpawnPoint.rotation;
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.SetCheckpoint(playerSpawnPoint.position, playerSpawnPoint.rotation);
                if (gm.Nova != null && novaSpawnPoint != null)
                {
                    gm.Nova.transform.position = novaSpawnPoint.position;
                    gm.Nova.transform.rotation = novaSpawnPoint.rotation;
                }
            }

            var cam = FindAnyObjectByType<FirstPersonCamera>();
            if (cam != null && cameraStartPoint != null)
            {
                cam.transform.position = cameraStartPoint.position;
                cam.transform.rotation = cameraStartPoint.rotation;
                cam.Yaw = cameraStartPoint.eulerAngles.y;
                cam.Pitch = cameraStartPoint.eulerAngles.x;
                cam.SnapBehind();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireCube(transform.position, GetComponent<BoxCollider>()?.size ?? Vector3.one * 2f);

            if (playerSpawnPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(playerSpawnPoint.position, 0.5f);
                Gizmos.DrawRay(playerSpawnPoint.position, playerSpawnPoint.forward * 2f);
            }
        }
    }
}