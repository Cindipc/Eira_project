using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Safely removes the legacy "Gameplay" environment root while preserving the professional industrial scenario.
    /// </summary>
    public static class RemoveLegacyGameplay
    {
        const string TargetSceneName = "Level1Scene";
        const string LegacyRootName = "Gameplay";
        const string ProfessionalRootName = "EIRA_Level1_Generated";

        [MenuItem("EIRA/Level 1/Remove Legacy Gameplay")]
        public static void ExecuteCleanup()
        {
            // 1. Scene check
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.name != TargetSceneName)
            {
                Debug.LogError($"[RemoveLegacyGameplay] Error: Active scene must be {TargetSceneName}. Current: {activeScene.name}");
                return;
            }

            // 2. Find roots
            GameObject legacyRoot = GameObject.Find(LegacyRootName);
            GameObject professionalRoot = GameObject.Find(ProfessionalRootName);

            // 3. Validations
            if (legacyRoot == null)
            {
                Debug.Log("[RemoveLegacyGameplay] No legacy 'Gameplay' object found. Scene is already clean.");
                return;
            }

            if (professionalRoot == null)
            {
                Debug.LogError($"[RemoveLegacyGameplay] Error: New professional scenario '{ProfessionalRootName}' not found! " +
                               "Aborting deletion to prevent leaving the player in a void.");
                return;
            }

            // Verify professional root has content
            if (professionalRoot.transform.childCount == 0)
            {
                Debug.LogError($"[RemoveLegacyGameplay] Error: '{ProfessionalRootName}' is empty. " +
                               "The professional scenario must be generated before removing the legacy one.");
                return;
            }

            // 4. Dependency Check: Ensure no critical systems are children of Gameplay
            // We search for core gameplay components that should not be deleted.
            List<GameObject> criticalObjects = new List<GameObject>();
            
            // Check all children for critical components
            foreach (Transform child in legacyRoot.GetComponentsInChildren<Transform>(true))
            {
                if (child.GetComponent<PlayerController>() != null || 
                    child.GetComponent<GameManager>() != null || 
                    child.GetComponent<HUDManager>() != null ||
                    child.GetComponent<GuardianBoss>() != null ||
                    child.GetComponent<DroneController>() != null)
                {
                    criticalObjects.Add(child.gameObject);
                }
            }

            if (criticalObjects.Count > 0)
            {
                Debug.LogWarning($"[RemoveLegacyGameplay] Warning: {criticalObjects.Count} critical systems found inside 'Gameplay'. " +
                                 "Moving them to the root of the scene before deletion.");
                
                foreach (var obj in criticalObjects)
                {
                    Undo.SetTransformParent(obj.transform, null, "Move Critical System to Root");
                }
            }

            // 5. Execution
            try
            {
                // Register for Undo
                Undo.DestroyObjectImmediate(legacyRoot);
                
                Debug.Log($"[RemoveLegacyGameplay] Successfully removed legacy '{LegacyRootName}' root and its children.");
                
                // Mark scene as dirty to force save
                EditorSceneManager.MarkSceneDirty(activeScene);
                
                // In a real editor environment, we would save the scene here.
                // Since we are in a script, we notify the user.
                Debug.Log("[RemoveLegacyGameplay] Please save the scene (Ctrl+S) to persist changes.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RemoveLegacyGameplay] Critical failure during deletion: {ex.Message}");
            }
        }
    }
}
