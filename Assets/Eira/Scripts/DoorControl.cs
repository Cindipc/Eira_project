using UnityEngine;

namespace EiraGame
{
    // Puerta corrediza. Se abre cuando se cargan todas las consolas vinculadas
    // (puerta de acertijo) o el jefe es derrotado (puerta de salida).
    public class DoorControl : MonoBehaviour
    {
        public int neededActivations = 2;
        public Transform doorPanel;   // hijo: panel que se desliza (local y = 0 cerrada)

        int activations;
        bool openRequested;
        float openProgress;

        public bool IsOpen => openProgress > 0.95f;

        public void RegisterActivation()
        {
            activations++;
            if (activations >= neededActivations) Open();
        }

        public void Open()
        {
            AudioFX.Door();
            openRequested = true;
            if (doorPanel != null)
            {
                var col = doorPanel.GetComponent<Collider>();
                if (col != null) col.enabled = false;
            }
        }

        void Update()
        {
            if (!openRequested) return;
            openProgress = Mathf.MoveTowards(openProgress, 1f, Time.deltaTime / 1.4f);
            if (doorPanel != null)
                doorPanel.transform.localPosition = new Vector3(0f, 1.6f * openProgress, 0f);
        }
    }
}