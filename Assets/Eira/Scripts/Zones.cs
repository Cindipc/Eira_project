using UnityEngine;

namespace EiraGame
{
    // Zona de cobertura: si Eira está agachada aquí, los drones casi no la detectan.
    public class HideSpotZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null) { pc.hideZoneLevel = 1; GameEvents.Subtitle("Zona segura: los drones no te verán agachada aquí.", 3f); }
        }

        void OnTriggerExit(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null) { pc.hideZoneLevel = 0; }
        }
    }

    // Punto de reaparición.
    public class CheckpointZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && GameManager.Instance != null)
                GameManager.Instance.SetCheckpoint(transform.position, transform.rotation);
        }
    }

    // Zona de victoria (salida del laboratorio).
    public class LevelExitZone : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null && GameManager.Instance != null)
                GameManager.Instance.OnExitReached();
        }
    }
}