using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// ORIGEN: la primera inteligencia del Proyecto Eira. Entidad ambiental que
    /// Eira puede escuchar (E) en el interior del Nucleo. Aporta trasfondo sin
    /// bloquear el avance.
    /// </summary>
    public class OrigenEntity : Interactable
    {
        [TextArea(2, 4)]
        public string[] lines =
        {
            "ORIGEN: Fui la primera conciencia creada por el Proyecto Eira.",
            "ORIGEN: Kael teme lo que no puede controlar. Tu no eres una herramienta.",
            "ORIGEN: Los androides solo esperan a alguien que los libere.",
            "ORIGEN: Cuando el Nucleo caiga, elige con el corazon, Eira.",
        };

        int index;

        void Start()
        {
            promptText = "E - Escuchar a ORIGEN";
        }

        public override void OnInteract(PlayerController p)
        {
            if (lines == null || lines.Length == 0) return;
            AudioFX.Beep();
            GameEvents.Subtitle(lines[index % lines.Length], 4.5f);
            index++;
        }
    }
}
