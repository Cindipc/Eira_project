using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Consola de la decision final del Nivel 3. Hay tres (humanos, androides y
    /// libertad); solo la libertad libera a los androides y termina el juego.
    /// Permanecen apagadas hasta que Eira accede al sistema central.
    /// </summary>
    public class FinalChoiceConsole : Interactable
    {
        public FinalChoice option = FinalChoice.Freedom;
        public string label = "LIBERTAD";

        bool revealed;

        public void Reveal()
        {
            gameObject.SetActive(true);
            revealed = true;
            usable = true;
            promptText = "E - Elegir: " + label;
        }

        void Update()
        {
            if (!revealed) return;
            transform.Rotate(0f, 30f * Time.deltaTime, 0f, Space.World);
        }

        public override bool CanInteract(PlayerController p) => revealed;

        public override void OnInteract(PlayerController p)
        {
            if (!revealed) return;
            AudioFX.Beep();

            if (Level3Director.Instance != null)
                Level3Director.Instance.OnChoice(option);
        }
    }
}
