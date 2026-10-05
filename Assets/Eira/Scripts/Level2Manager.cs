using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame
{
    public class Level2Manager : MonoBehaviour
    {
        [Header("Level Info")]
        [SerializeField] private string levelName = "Nivel 2: Calles Destruidas";

        [Header("Debris Zones")]
        [SerializeField] private DebrisZone[] debrisZones;

        [Header("Explosions")]
        [SerializeField] private Explosion[] scriptedExplosions;
        [SerializeField] private float[] explosionTimings;

        [Header("Pickups")]
        [SerializeField] private GameObject healthPickupPrefab;
        [SerializeField] private GameObject heartPickupPrefab;
        [SerializeField] private GameObject ammoPickupPrefab;
        [SerializeField] private Transform[] pickupSpawnPoints;
        [SerializeField] private float pickupRespawnTime = 30f;

        [Header("Enemy Spawners")]
        [SerializeField] private GameObject dronePrefab;
        [SerializeField] private Transform[] droneSpawnPoints;
        [SerializeField] private int maxDrones = 8;
        [SerializeField] private float droneSpawnInterval = 15f;

        [Header("Environment")]
        [SerializeField] private AudioClip ambientMusic;

        private int currentExplosionIndex;
        private float nextDroneSpawnTime;
        private int activeDrones;

        private void Awake()
        {
            GameEvents.OnDroneDestroyed += OnDroneDestroyed;
        }

        private void OnDestroy()
        {
            GameEvents.OnDroneDestroyed -= OnDroneDestroyed;
        }

        private void Start()
        {
            SetupLevel();
            StartScriptedEvents();
            SetupPickups();
        }

        private void SetupLevel()
        {
            GameEvents.Subtitle($"[{levelName}]", 3f);
            GameEvents.NovaSpeak($"Eira, bienvenida a {levelName}. Las calles están destruidas. Ten cuidado con la radiación y los escombros.", 5f);

            foreach (var zone in debrisZones)
            {
                if (zone != null)
                {
                    zone.SpawnDebris();
                }
            }

            if (ambientMusic != null)
            {
                // AudioFX is a static class, no Instance property
                Debug.Log($"[Level2Manager] Would play ambient music: {ambientMusic.name}");
            }
        }

        private void StartScriptedEvents()
        {
            if (scriptedExplosions != null && explosionTimings != null)
            {
                for (int i = 0; i < Mathf.Min(scriptedExplosions.Length, explosionTimings.Length); i++)
                {
                    if (scriptedExplosions[i] != null)
                    {
                        Invoke(nameof(TriggerNextExplosion), explosionTimings[i]);
                    }
                }
            }
        }

        private void TriggerNextExplosion()
        {
            if (currentExplosionIndex < scriptedExplosions.Length)
            {
                var exp = scriptedExplosions[currentExplosionIndex];
                if (exp != null)
                {
                    exp.Detonate();
                }
                currentExplosionIndex++;
            }
        }

        private void SetupPickups()
        {
            if (pickupSpawnPoints == null || pickupSpawnPoints.Length == 0) return;

            foreach (var point in pickupSpawnPoints)
            {
                if (point == null) continue;

                GameObject prefab = GetRandomPickup();
                if (prefab != null)
                {
                    var pickup = Instantiate(prefab, point.position, point.rotation);
                    var p = pickup.GetComponent<Pickup>();
                    if (p != null)
                    {
                        p.SetRespawn(point, pickupRespawnTime);
                    }
                }
            }
        }

        private GameObject GetRandomPickup()
        {
            float r = Random.value;
            if (r < 0.4f && healthPickupPrefab != null) return healthPickupPrefab;
            if (r < 0.7f && heartPickupPrefab != null) return heartPickupPrefab;
            if (ammoPickupPrefab != null) return ammoPickupPrefab;
            return healthPickupPrefab;
        }

        private void Update()
        {
            HandleDroneSpawning();
        }

        private void HandleDroneSpawning()
        {
            if (dronePrefab == null || droneSpawnPoints == null || droneSpawnPoints.Length == 0) return;
            if (activeDrones >= maxDrones) return;

            if (Time.time >= nextDroneSpawnTime)
            {
                SpawnDrone();
                nextDroneSpawnTime = Time.time + droneSpawnInterval;
            }
        }

        private void SpawnDrone()
        {
            var validPoints = new System.Collections.Generic.List<Transform>();
            var player = GameManager.Instance?.Player;

            foreach (var point in droneSpawnPoints)
            {
                if (point == null) continue;
                if (player != null && Vector3.Distance(point.position, player.transform.position) < 10f) continue;
                validPoints.Add(point);
            }

            if (validPoints.Count == 0) return;

            var spawnPoint = validPoints[Random.Range(0, validPoints.Count)];
            var drone = Instantiate(dronePrefab, spawnPoint.position, spawnPoint.rotation);
            activeDrones++;

            var dc = drone.GetComponent<DroneController>();
            if (dc != null)
            {
                dc.OnDeath += () => activeDrones--;
            }
        }

        private void OnDroneDestroyed(int points)
        {
            activeDrones = Mathf.Max(0, activeDrones - 1);
        }

        public void TriggerExplosionAt(Vector3 position, float radius = 5f, float damage = 50f, float force = 800f)
        {
            Explosion.Create(position, radius, damage, force);
        }

        public void RegisterDebrisZone(DebrisZone zone)
        {
            if (zone != null && System.Array.IndexOf(debrisZones, zone) < 0)
            {
                System.Array.Resize(ref debrisZones, debrisZones.Length + 1);
                debrisZones[debrisZones.Length - 1] = zone;
            }
        }
    }
}