using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Places the Victory Trigger at the end of the forest and configures it.
    /// </summary>
    public static class VictorySetup
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string ForestRootName = "PisoBosque";

        [MenuItem("EIRA/World/Setup Victory Trigger")]
        public static void Setup()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[VictorySetup] Cannot setup victory trigger while in Play Mode.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
                GameObject forest = GameObject.Find(ForestRootName);

                if (forest == null)
                {
                    Debug.LogError("[VictorySetup] Error: PisoBosque not found.");
                    return;
                }

                // Position the trigger at the end of the forest
                // Assuming forest is a large platform, we place it far along the X axis (approx 300)
                Vector3 triggerPos = new Vector3(300f, 1f, 0);
                
                GameObject triggerGo = new GameObject("VictoryTrigger");
                triggerGo.transform.SetParent(forest.transform, false);
                triggerGo.transform.position = triggerPos;
                triggerGo.transform.localScale = new Vector3(5f, 5f, 10f);

                // Add components
                var col = triggerGo.AddComponent<BoxCollider>();
                col.isTrigger = true;

                var mesh = triggerGo.AddComponent<MeshFilter>();
                mesh.sharedMesh = GameObject.CreatePrimitive(PrimitiveType.Cube).GetComponent<MeshFilter>().sharedMesh;
                
                var rend = triggerGo.AddComponent<MeshRenderer>();
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", new Color(0, 1f, 1f, 0.3f));
                rend.sharedMaterial = mat;

                // Add the logic script
                triggerGo.AddComponent<EiraGame.Gameplay.VictoryTrigger>();

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[VictorySetup] Victory Trigger placed at the forest end.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[VictorySetup] ERROR: " + ex);
            }
        }
    }
}
