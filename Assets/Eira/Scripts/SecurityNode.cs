using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Sistema de seguridad del Nucleo Central. Eira lo desactiva con E (o con
    /// el pulso de F). Al desactivar los tres, se completa la mision.
    /// </summary>
    public class SecurityNode : Interactable
    {
        public bool Disabled { get; private set; }

        Light glow;
        Renderer core;

        void Start()
        {
            promptText = "E - Desactivar sistema de seguridad";
            glow = GetComponentInChildren<Light>();
            core = GetComponentInChildren<Renderer>();
        }

        void Update()
        {
            if (Disabled) return;
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            if (glow != null)
                glow.intensity = 1.2f + Mathf.Sin(Time.time * 3.2f) * 0.5f;
        }

        public override bool CanInteract(PlayerController p) => !Disabled;

        public override void OnAbilityPulse(PlayerController p)
        {
            Disable();
        }

        public override void OnInteract(PlayerController p)
        {
            Disable();
        }

        void Disable()
        {
            if (Disabled) return;
            Disabled = true;
            usable = false;

            AudioFX.Door();
            if (glow != null)
                glow.color = new Color(0.3f, 1f, 0.5f);
            if (core != null)
                core.material.color = new Color(0.3f, 0.9f, 0.5f);
            EnvOrb.Spawn(transform.position + Vector3.up, new Color(0.3f, 1f, 0.6f));

            if (Level3Director.Instance != null)
                Level3Director.Instance.OnSecurityDisabled(this);
        }
    }
}
