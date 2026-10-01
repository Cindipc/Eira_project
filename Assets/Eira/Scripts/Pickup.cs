using UnityEngine;

namespace EiraGame
{
    public enum PickupKind { Medkit, Energy }

    // Botiquín tecnológico / cápsula de energía.
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

        Vector3 basePos;
        Light glowLight;
        Renderer[] renderers;
        float phase;

        void Start()
        {
            promptText = kind == PickupKind.Medkit ? "E — Botiquín tecnológico" : "E — Cápsula de energía";

            // La base se toma aquí, no en Awake, porque la escena puede estar
            // reparándose y la posición final no estar fijada todavía.
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
                    : new Color(0.4f, 0.8f, 1f);
            }
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
            else
            {
                gm.RestoreHeart(amount);
                AudioFX.Pickup();
            }
            EnvOrb.Spawn(transform.position, new Color(0.5f, 1f, 0.8f));
            Destroy(gameObject);
        }
    }
}