using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Acceso al interior del Nucleo Central (Nivel 3). Se coloca junto al
    /// Nucleo del refugio. Solo se puede usar cuando el Nivel 2 ha localizado
    /// el Nucleo; al usarlo, Eira entra a la batalla final (dentro de la misma
    /// escena, en una zona interior aparte).
    /// </summary>
    public class NucleoPortal : Interactable
    {
        public Transform interiorSpawn;

        bool used;

        void Start()
        {
            promptText = "E - Entrar al Nucleo Central";
        }

        public override bool CanInteract(PlayerController p)
        {
            if (used) return false;
            var d = Level2Director.Instance;
            return d == null || d.NucleoFound;
        }

        public override void OnInteract(PlayerController p)
        {
            if (used) return;
            used = true;
            usable = false;

            AudioFX.Door();

            if (Level3Director.Instance != null && interiorSpawn != null)
            {
                Level3Director.Instance.Begin(p, interiorSpawn);
            }
            else if (interiorSpawn != null)
            {
                p.transform.position = interiorSpawn.position;
            }
        }
    }
}
