using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

namespace EiraGame
{
    [InitializeOnLoad]
    public class ForceIntroOnPlay
    {
        static ForceIntroOnPlay()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                // Force load Intro scene first
                if (SceneManager.GetActiveScene().name != "Intro")
                {
                    SceneManager.LoadScene("Intro", LoadSceneMode.Single);
                }
            }
        }
    }
}