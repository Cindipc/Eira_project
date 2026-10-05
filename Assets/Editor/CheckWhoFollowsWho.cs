using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EiraGame
{
    public class CheckWhoFollowsWho : EditorWindow
    {
        [MenuItem("Eira/Debug Who Follows Who")]
        public static void Check()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");

            // Find all PlayerControllers
            var allPlayers = GameObject.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"=== PLAYERCONTROLLERS FOUND: {allPlayers.Length} ===");
            foreach (var p in allPlayers)
            {
                Debug.Log($"  - {p.name} at {p.transform.position} | tag: {p.tag} | layer: {p.gameObject.layer}");
            }

            // Find all NovaCompanions
            var allNovas = GameObject.FindObjectsByType<NovaCompanion>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"=== NOVACOMPANIONS FOUND: {allNovas.Length} ===");
            foreach (var n in allNovas)
            {
                Debug.Log($"  - On: {n.gameObject.name} at {n.transform.position}");
            }

            // Find all cameras
            var allCams = GameObject.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"=== CAMERAS FOUND: {allCams.Length} ===");
            foreach (var c in allCams)
            {
                var camScript = c.GetComponent<FirstPersonCamera>();
                string targetInfo = "NO SCRIPT";
                if (camScript != null)
                {
                    var field = typeof(FirstPersonCamera).GetField("target", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field != null)
                    {
                        var target = field.GetValue(camScript) as Transform;
                        targetInfo = target != null ? target.name : "NULL";
                    }
                }
                Debug.Log($"  - {c.name} at {c.transform.position} | parent: {(c.transform.parent != null ? c.transform.parent.name : "ROOT")} | MainCamera: {c.tag == "MainCamera"} | Target: {targetInfo}");
            }

            GameObject eira = GameObject.Find("Eira");
            GameObject nova = GameObject.Find("NOVA");
            
            if (eira != null)
                Debug.Log($"Eira hierarchy: {GetHierarchyPath(eira)}");
            if (nova != null)
                Debug.Log($"Nova hierarchy: {GetHierarchyPath(nova)}");

            Debug.Log("=== CHECK COMPLETE ===");
        }

        static string GetHierarchyPath(GameObject obj)
        {
            string path = obj.name;
            Transform current = obj.transform.parent;
            while (current != null)
            {
                path = current.name + " -> " + path;
                current = current.parent;
            }
            return path;
        }
    }
}