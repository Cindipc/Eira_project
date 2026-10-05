using UnityEngine;
using System.Collections.Generic;

namespace EiraGame
{
    public class DebrisZone : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private string zoneName = "Zona Destruida";
        [SerializeField] private bool isRadiationZone;
        [SerializeField] private float radiationDamagePerSecond = 5f;

        [Header("Debris Spawning")]
        [SerializeField] private GameObject[] debrisPrefabs;
        [SerializeField] private int debrisCount = 20;
        [SerializeField] private float spawnRadius = 15f;
        [SerializeField] private LayerMask groundLayer = -1;
        [SerializeField] private bool spawnOnStart = true;

        [Header("Visual")]
        [SerializeField] private Color zoneColor = new Color(0.8f, 0.2f, 0.1f, 0.3f);
        [SerializeField] private bool showGizmos = true;

        private List<GameObject> spawnedDebris = new List<GameObject>();
        private Collider zoneCollider;

        private void Awake()
        {
            zoneCollider = GetComponent<Collider>();
            if (zoneCollider == null)
            {
                zoneCollider = gameObject.AddComponent<BoxCollider>();
                zoneCollider.isTrigger = true;
            }
        }

        private void Start()
        {
            if (spawnOnStart)
            {
                SpawnDebris();
            }
        }

        public void SpawnDebris()
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0)
            {
                Debug.LogWarning($"[DebrisZone] {zoneName}: No hay prefabs de escombros asignados");
                return;
            }

            for (int i = 0; i < debrisCount; i++)
            {
                Vector3 randomPoint = transform.position + Random.insideUnitSphere * spawnRadius;
                randomPoint.y = transform.position.y + 10f;

                if (Physics.Raycast(randomPoint, Vector3.down, out RaycastHit hit, 20f, groundLayer))
                {
                    GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                    GameObject debris = Instantiate(prefab, hit.point, Random.rotation);
                    debris.transform.SetParent(transform);
                    spawnedDebris.Add(debris);

                    var rb = debris.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.mass = Random.Range(0.5f, 5f);
                    }
                }
            }

            Debug.Log($"[DebrisZone] {zoneName}: Generados {spawnedDebris.Count} escombros");
        }

        public void ClearDebris()
        {
            foreach (var d in spawnedDebris)
            {
                if (d != null) Destroy(d);
            }
            spawnedDebris.Clear();
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isRadiationZone) return;

            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.TakeDamage(radiationDamagePerSecond * Time.deltaTime, gameObject);
            }
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

            Gizmos.color = zoneColor;
            if (zoneCollider != null)
            {
                if (zoneCollider is BoxCollider box)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawCube(box.center, box.size);
                }
                else if (zoneCollider is SphereCollider sphere)
                {
                    Gizmos.matrix = transform.localToWorldMatrix;
                    Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                }
            }
            else
            {
                Gizmos.DrawWireSphere(transform.position, spawnRadius);
            }
        }

        public int GetDebrisCount() => spawnedDebris.Count;
        public string ZoneName => zoneName;
    }
}