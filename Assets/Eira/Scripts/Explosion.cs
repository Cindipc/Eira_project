using UnityEngine;

namespace EiraGame
{
    public class Explosion : MonoBehaviour
    {
        [Header("Explosion Settings")]
        [SerializeField] private float radius = 5f;
        [SerializeField] private float damage = 50f;
        [SerializeField] private float force = 800f;
        [SerializeField] private float upwardForce = 1.5f;
        [SerializeField] private LayerMask affectLayers = -1;

        [Header("Visuals")]
        [SerializeField] private ParticleSystem explosionParticles;
        [SerializeField] private Light explosionLight;
        [SerializeField] private float lightIntensity = 8f;
        [SerializeField] private float lightDuration = 0.3f;
        [SerializeField] private AudioClip explosionSound;

        [Header("Screen Shake")]
        [SerializeField] private float shakeIntensity = 0.5f;
        [SerializeField] private float shakeDuration = 0.4f;

        private bool hasExploded;

        public static Explosion Create(Vector3 position, float radius = 5f, float damage = 50f, float force = 800f)
        {
            var go = new GameObject("Explosion");
            go.transform.position = position;
            var exp = go.AddComponent<Explosion>();
            exp.radius = radius;
            exp.damage = damage;
            exp.force = force;
            return exp;
        }

        private void Start()
        {
            Detonate();
        }

        public void Detonate()
        {
            if (hasExploded) return;
            hasExploded = true;

            Collider[] hits = Physics.OverlapSphere(transform.position, radius, affectLayers, QueryTriggerInteraction.Collide);
            foreach (var hit in hits)
            {
                if (hit.transform == transform) continue;

                var rb = hit.attachedRigidbody;
                if (rb != null)
                {
                    Vector3 dir = (rb.transform.position - transform.position).normalized;
                    dir.y += upwardForce;
                    rb.AddForce(dir * force, ForceMode.Impulse);
                }

                var pc = hit.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(damage, gameObject);
                }

                var drone = hit.GetComponent<DroneController>();
                if (drone != null)
                {
                    GameManager.Instance?.OnDroneDestroyed(drone, Points.DroneKill, transform);
                    drone.DestroyDrone();
                }

                var destructible = hit.GetComponent<DestructibleObject>();
                if (destructible != null)
                {
                    destructible.TakeDamage(damage);
                }
            }

            if (explosionParticles != null)
            {
                explosionParticles.transform.parent = null;
                explosionParticles.Play();
                Destroy(explosionParticles.gameObject, explosionParticles.main.duration + 1f);
            }

            if (explosionLight != null)
            {
                explosionLight.intensity = lightIntensity;
                StartCoroutine(LightFlash());
            }

            if (explosionSound != null)
            {
                AudioSource.PlayClipAtPoint(explosionSound, transform.position, 1f);
            }

            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(shakeIntensity, shakeDuration);
            }

            GameEvents.Subtitle($"¡Explosión! Radio: {radius}m", 1.5f);

            Destroy(gameObject, 0.5f);
        }

        private System.Collections.IEnumerator LightFlash()
        {
            float t = 0f;
            float startIntensity = explosionLight.intensity;
            while (t < lightDuration)
            {
                t += Time.deltaTime;
                explosionLight.intensity = Mathf.Lerp(startIntensity, 0f, t / lightDuration);
                yield return null;
            }
            explosionLight.intensity = 0f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}