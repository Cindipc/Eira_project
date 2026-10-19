using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Connects the Laboratory exit (EIRA_Level1_Generated) with the Forest area (PisoBosque).
    /// Ensures a seamless transition in height, position, and lighting.
    /// </summary>
    public static class ForestConnector
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string LabRootName = "EIRA_Level1_Generated";
        const string ForestRootName = "PisoBosque";

        [MenuItem("EIRA/World/Connect Lab to Forest")]
        public static void Connect()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ForestConnector] Cannot connect while in Play Mode.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
                
                GameObject lab = GameObject.Find(LabRootName);
                GameObject forest = GameObject.Find(ForestRootName);

                if (lab == null || forest == null)
                {
                    Debug.LogError("[ForestConnector] Error: Lab or PisoBosque not found in scene.");
                    return;
                }

                // 1. Determine Lab Exit Position
                // Based on Level1Reconstructor: Zone5End is at 170. 
                // ExitPortal is at x = 170.
                Vector3 labExitPos = new Vector3(170f, 0, 0); 
                
                // 2. Calculate distance and alignment
                Vector3 forestPos = forest.transform.position;
                float heightDiff = forestPos.y - labExitPos.y;

                Debug.Log($"[ForestConnector] Aligning Lab exit ({labExitPos}) with Forest ({forestPos}). Height Diff: {heightDiff}");

                // 3. Create Transition Bridge/Platform
                // We create a short bridge that connects the exit portal to the forest floor
                CreateTransitionPlatform(lab, forest, labExitPos, forestPos, heightDiff);

                // 4. Adjust Forest Position if it's too far or misaligned
                // We move the forest to start exactly where the lab ends to ensure no gaps
                // but we keep its relative internal structure.
                Vector3 newForestPos = new Vector3(175f, forestPos.y, 0);
                forest.transform.position = newForestPos;

                // 5. Lighting Transition
                ApplyLightingTransition(labExitPos, newForestPos);

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ForestConnector] Lab and Forest successfully connected.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ForestConnector] CRITICAL ERROR: " + ex);
            }
        }

        static void CreateTransitionPlatform(GameObject lab, GameObject forest, Vector3 startPos, Vector3 endPos, float heightDiff)
        {
            GameObject bridge = new GameObject("Lab_Forest_Transition");
            bridge.transform.SetParent(lab.transform, false);

            // Transition length
            float bridgeLength = 10f;
            Vector3 bridgePos = new Vector3(startPos.x + bridgeLength/2, (startPos.y + endPos.y)/2, 0);
            Vector3 bridgeScale = new Vector3(bridgeLength, 0.5f, 20f); 

            // Create a ramp that actually slopes if there is a height difference
            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "TransitionPlatform";
            platform.transform.SetParent(bridge.transform, false);
            platform.transform.position = bridgePos;
            platform.transform.localScale = bridgeScale;
            
            // Rotate the platform to create a ramp slope
            float angle = Mathf.Atan2(heightDiff, bridgeLength) * Mathf.Rad2Deg;
            platform.transform.rotation = Quaternion.Euler(-angle, 0, 0);

            // Material: High-tech metallic transition
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.name = "Mat_Transition_Metal";
            mat.SetColor("_BaseColor", new Color(0.15f, 0.15f, 0.18f));
            mat.SetFloat("_Metallic", 0.9f);
            mat.SetFloat("_Smoothness", 0.6f);
            
            // Add a cyan emissive edge to the ramp
            var emissiveMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            emissiveMat.name = "Mat_Transition_Edge";
            emissiveMat.SetColor("_BaseColor", new Color(0, 0.8f, 1f));
            emissiveMat.SetColor("_EmissionColor", new Color(0, 0.8f, 1f) * 2f);
            emissiveMat.EnableKeyword("_EMISSION");

            platform.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void ApplyLightingTransition(Vector3 startPos, Vector3 endPos)
        {
            // Light at the transition point (Cyan fading to Natural)
            GameObject transitionLight = new GameObject("TransitionLight");
            transitionLight.transform.position = (startPos + endPos) / 2;
            
            var l = transitionLight.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(0.0f, 0.8f, 1.0f); // Cyan
            l.intensity = 3f;
            l.range = 15f;
        }
    }
}
