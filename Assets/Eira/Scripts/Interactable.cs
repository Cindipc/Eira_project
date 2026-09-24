using UnityEngine;

namespace EiraGame
{
    // Base para objetos con los que Eira puede interactuar (E) o activar (F).
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected string promptText = "Interactuar";
        protected bool usable = true;

        public virtual bool CanInteract(PlayerController p) => usable;
        public virtual string Prompt => promptText;

        public virtual void OnInteract(PlayerController p)
        {
            // por defecto: un FX de confirmación
            AudioFX.Beep();
        }

        // La habilidad genética (pulso de F). Por defecto no hace nada.
        public virtual void OnAbilityPulse(PlayerController p) { }

        public void SetPromptVisible(bool visible)
        {
            if (visible) GameEvents.InteractPrompt(promptText);
        }
    }
}