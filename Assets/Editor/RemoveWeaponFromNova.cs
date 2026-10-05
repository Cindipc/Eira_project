using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace EiraGame
{
    public class RemoveWeaponFromNova : EditorWindow
    {
        [MenuItem("Eira/Remove Weapon from Nova")]
        public static void Fix()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");

            GameObject nova = GameObject.Find("NOVA");
            if (nova != null)
            {
                // Check Nova itself
                var weapons = nova.GetComponentsInChildren<EiraWeapon>(true);
                foreach (var w in weapons)
                {
                    Debug.Log($"REMOVING EiraWeapon from Nova: {w.name} on {w.gameObject.name}");
                    Undo.DestroyObjectImmediate(w);
                }

                // Check for any other shooting components
                var shooters = nova.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var s in shooters)
                {
                    if (s != null && (s.GetType().Name.Contains("Weapon") || s.GetType().Name.Contains("Shoot") || s.GetType().Name.Contains("Gun")))
                    {
                        Debug.Log($"REMOVING shooting component: {s.GetType().Name} on {s.gameObject.name}");
                        Undo.DestroyObjectImmediate(s);
                    }
                }
            }

            // Also check NOVAVisual specifically
            GameObject novaVisual = GameObject.Find("NOVAVisual");
            if (novaVisual != null)
            {
                var weapons = novaVisual.GetComponentsInChildren<EiraWeapon>(true);
                foreach (var w in weapons)
                {
                    Debug.Log($"REMOVING EiraWeapon from NOVAVisual: {w.name} on {w.gameObject.name}");
                    Undo.DestroyObjectImmediate(w);
                }
            }

            // Ensure Eira has the weapon
            GameObject eira = GameObject.Find("Eira");
            if (eira != null)
            {
                var eiraWeapon = eira.GetComponentInChildren<EiraWeapon>(true);
                if (eiraWeapon == null)
                {
                    // Add weapon to Eira
                    var weaponObj = new GameObject("Weapon");
                    weaponObj.transform.SetParent(eira.transform);
                    weaponObj.transform.localPosition = Vector3.zero;
                    var ew = weaponObj.AddComponent<EiraWeapon>();
                    Debug.Log($"Added EiraWeapon to Eira");
                }
                else
                {
                    Debug.Log($"Eira already has weapon: {eiraWeapon.name}");
                }
            }
            else
            {
                Debug.LogWarning("Eira not found in scene!");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            
            Debug.Log("=== WEAPON REMOVED FROM NOVA, ADDED TO EIRA ===");
        }
    }
}