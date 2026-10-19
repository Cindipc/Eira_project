using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Sistema central del Nucleo. Solo se puede acceder cuando Kael ha caido:
    /// Eira usa su sangre para conectarse y desbloquear la decision final.
    /// </summary>
    public class NucleoCore : Interactable
    {
        [TextArea(2, 4)] public string accessLine = "Sangre de Eira... el Nucleo te reconoce. Elige el destino.";

        bool revealed;

        void Start()
        {
            promptText = "E - Acceder al sistema central";
            usable = false;
        }

        void Update()
        {
            if (!revealed) return;
            transform.Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
        }

        public void Reveal()
        {
            revealed = true;
            usable = true;

            var light = GetComponentInChildren<Light>();
            if (light != null)
                light.color = new Color(0.6f, 0.4f, 1f);
        }

        public override bool CanInteract(PlayerController p) => revealed;

        public override void OnInteract(PlayerController p)
        {
            if (!revealed) return;
            usable = false;

            AudioFX.Ability();
            GameEvents.PickedInfo(accessLine);
            EnvOrb.Spawn(transform.position, new Color(0.7f, 0.4f, 1f));

            if (Level3Director.Instance != null)
                Level3Director.Instance.OnCoreAccessed();
        }
    }
}
