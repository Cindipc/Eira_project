using UnityEngine;

namespace EiraGame
{
    public enum PickupKind { Medkit, Energy }

    // Botiquín tecnológico / cápsula de energía.
    public class Pickup : Interactable
    {
        public PickupKind kind = PickupKind.Medkit;
        public float amount = 50f;

        void Start()
        {
            promptText = kind == PickupKind.Medkit ? "E — Botiquín tecnológico" : "E — Cápsula de energía";
        }

        void Update()
        {
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
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