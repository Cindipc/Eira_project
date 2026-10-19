using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;

namespace EiraGame.Gameplay
{
    /// <summary>
    /// Trigger that detects when the player reaches the end of the forest.
    /// Triggers the victory sequence and loads the next level.
    /// </summary>
    public class VictoryTrigger : MonoBehaviour
    {
        [Header("Transition Settings")]
        public string nextSceneName = "Level2Scene";
        public float delayBeforeLoad = 3f;
        public Color victoryColor = Color.cyan;

        private bool _hasBeenTriggered = false;

        private void OnTriggerEnter(Collider other)
        {
            if (_hasBeenTriggered) return;

            if (other.CompareTag("Player"))
            {
                _hasBeenTriggered = true;
                OnVictory();
            }
        }

        private void OnVictory()
        {
            Debug.Log("[Victory] Player reached the end of Level 1! Transitioning to Level 2...");
            
            // Visual feedback: Change the trigger color or trigger a global event
            var renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.SetColor("_BaseColor", victoryColor);
                renderer.material.SetColor("_EmissionColor", victoryColor * 2f);
                renderer.material.EnableKeyword("_EMISSION");
            }

            // Load next scene after delay
            Invoke(nameof(LoadNextLevel), delayBeforeLoad);
        }

        private void LoadNextLevel()
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
