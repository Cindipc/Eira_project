using UnityEngine;

namespace EiraGame
{
    public class DestructibleObject : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth;

        [Header("Destruction")]
        [SerializeField] private GameObject destroyedVersion;
        [SerializeField] private ParticleSystem destructionParticles;
        [SerializeField] private AudioClip destructionSound;
        [SerializeField] private float explosionForce = 200f;
        [SerializeField] private float explosionRadius = 3f;
        [SerializeField] private int scoreValue = 50;

        [Header("Drops")]
        [SerializeField] private GameObject[] possibleDrops;
        [SerializeField] [Range(0f, 1f)] private float dropChance = 0.3f;

        private bool isDestroyed;
        private Collider col;
        private Renderer rend;

        private void Awake()
        {
            currentHealth = maxHealth;
            col = GetComponent<Collider>();
            rend = GetComponent<Renderer>();
        }

        public void TakeDamage(float damage)
        {
            if (isDestroyed) return;

            currentHealth -= damage;

            if (currentHealth <= 0f)
            {
                DestroyObject();
            }
            else
            {
                StartCoroutine(FlashDamage());
            }
        }

        private System.Collections.IEnumerator FlashDamage()
        {
            if (rend == null) yield break;

            Color original = rend.material.color;
            rend.material.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            rend.material.color = original;
        }

        private void DestroyObject()
        {
            isDestroyed = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreValue);
            }

            if (destroyedVersion != null)
            {
                var debris = Instantiate(destroyedVersion, transform.position, transform.rotation);
                var rbs = debris.GetComponentsInChildren<Rigidbody>();
                foreach (var rb in rbs)
                {
                    Vector3 forceDir = (rb.transform.position - transform.position).normalized;
                    forceDir.y += 0.5f;
                    rb.AddForce(forceDir * explosionForce, ForceMode.Impulse);
                    rb.AddTorque(Random.insideUnitSphere * explosionForce * 0.5f, ForceMode.Impulse);
                }
                Destroy(debris, 10f);
            }
            else if (destructionParticles != null)
            {
                var ps = Instantiate(destructionParticles, transform.position, transform.rotation);
                ps.Play();
                Destroy(ps.gameObject, ps.main.duration + 1f);
            }

            if (destructionSound != null)
            {
                AudioSource.PlayClipAtPoint(destructionSound, transform.position);
            }

            TryDropItem();

            if (col != null) col.enabled = false;
            if (rend != null) rend.enabled = false;

            Destroy(gameObject, 0.1f);
        }

        private void TryDropItem()
        {
            if (possibleDrops.Length == 0) return;
            if (Random.value > dropChance) return;

            var drop = possibleDrops[Random.Range(0, possibleDrops.Length)];
            if (drop != null)
            {
                Instantiate(drop, transform.position + Vector3.up * 0.5f, Quaternion.identity);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, explosionRadius);
        }
    }
}