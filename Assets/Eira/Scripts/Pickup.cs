using UnityEngine;

namespace EiraGame
{
    public enum PickupKind { Medkit, Energy, Ammo }

    // Botiquín tecnológico / cápsula de energía / munición.
    public class Pickup : Interactable
    {
        public PickupKind kind = PickupKind.Medkit;
        public float amount = 50f;

        [Tooltip("Flota suavemente para que se vea desde lejos.")]
        public bool bob = true;

        [Tooltip("Altura del flotado (m).")]
        public float bobHeight = 0.12f;

        [Tooltip("Velocidad del flotado.")]
        public float bobSpeed = 1.8f;

        [Tooltip("Luz propia: en un corredor oscuro es lo que hace que el botiquín se vea.")]
        public bool glow = true;

        [Header("Respawn")]
        public bool canRespawn = false;
        public float respawnTime = 30f;
        private Transform respawnPoint;

        Vector3 basePos;
        Light glowLight;
        Renderer[] renderers;
        float phase;

        void Start()
        {
            promptText = kind == PickupKind.Medkit ? "E — Botiquín tecnológico" : 
                        (kind == PickupKind.Energy ? "E — Cápsula de energía" : "E — Munición");

            basePos = transform.position;

            phase = Mathf.Repeat(basePos.x * 0.37f + basePos.z * 0.71f, Mathf.PI * 2f);

            renderers = GetComponentsInChildren<Renderer>(true);

            if (glow && renderers.Length > 0)
            {
                glowLight = gameObject.GetComponent<Light>();

                if (glowLight == null)
                    glowLight = gameObject.AddComponent<Light>();

                glowLight.type = LightType.Point;
                glowLight.range = 5.5f;
                glowLight.intensity = 1.6f;
                glowLight.shadows = LightShadows.None;
                glowLight.color = kind == PickupKind.Medkit
                    ? new Color(0.4f, 1f, 0.75f)
                    : (kind == PickupKind.Energy ? new Color(0.4f, 0.8f, 1f) : new Color(1f, 0.8f, 0.3f));
            }
        }

        public void SetRespawn(Transform point, float time)
        {
            respawnPoint = point;
            respawnTime = time;
            canRespawn = true;
        }

        void Update()
        {
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

            if (bob)
            {
                float y = Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight;
                transform.position = basePos + Vector3.up * y;
            }

            if (glowLight != null)
                glowLight.intensity = 1.3f + Mathf.Sin(Time.time * 2.4f + phase) * 0.5f;
        }

        public override void OnInteract(PlayerController p)
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (kind == PickupKind.Medkit)
            {
                gm.HealPlayer(amount);
                AudioFX.Pickup();
            }
            else if (kind == PickupKind.Energy)
            {
                gm.RestoreHeart(amount);
                AudioFX.Pickup();
            }
            else if (kind == PickupKind.Ammo)
            {
                var weapon = p.Weapon;
                if (weapon != null)
                {
                    weapon.AddAmmo(Mathf.RoundToInt(amount));
                }
                AudioFX.Pickup();
            }
            EnvOrb.Spawn(transform.position, new Color(0.5f, 1f, 0.8f));
            
            if (canRespawn && respawnPoint != null)
            {
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private System.Collections.IEnumerator RespawnRoutine()
        {
            gameObject.SetActive(false);
            yield return new WaitForSeconds(respawnTime);
            
            transform.position = respawnPoint.position;
            transform.rotation = respawnPoint.rotation;
            basePos = transform.position;
            phase = Mathf.Repeat(basePos.x * 0.37f + basePos.z * 0.71f, Mathf.PI * 2f);
            gameObject.SetActive(true);
        }
    }
}