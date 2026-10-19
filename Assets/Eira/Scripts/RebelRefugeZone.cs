using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Refugio rebelde al este de la ciudad. Cuando Eira llega y ademas ha
    /// encontrado el Nucleo Central, se gana el nivel.
    /// </summary>
    public class RebelRefugeZone : MonoBehaviour
    {
        [TextArea(2, 4)] public string enterLine = "El refugio rebelde. Estamos a salvo... por ahora.";

        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc == null) return;

            GameEvents.NovaSpeak(enterLine, 4f);

            if (Level2Director.Instance != null)
                Level2Director.Instance.OnEnteredRefuge();
        }
    }
}
