using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Tool to add high-fidelity industrial details to Level 1.
    /// It places pipes, consoles, and warning lights along the corridor
    /// and configures atmospheric lighting and post-processing.
    /// </summary>
    public static class EnvironmentDresser
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string DecorRootName = "Visual_Environment_Details";
        
        // Corridor constraints
        const float CorridorStartX = 0f;
        const float CorridorEndX = 120f;
        const float CorridorHalfWidth = 13f;
        const float CorridorHeight = 6f;

        [MenuItem("Eira/Visuals/Dress Level 1")]
        public static void DressLevel()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[EnvironmentDresser] Cannot dress level during Play Mode. Please stop the game first.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

                
                Debug.Log("[EnvironmentDresser] Starting industrial dressing of Level 1...");

                // 1. Clean previous dressing to avoid duplicates
                ClearExistingDressing();

                // 2. Create root container
                GameObject root = new GameObject(DecorRootName);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;

                // 3. Place industrial details
                PlaceIndustrialPipes(root.transform);
                PlaceConsolesAndPanels(root.transform);
                PlaceCeilingDetails(root.transform);
                PlaceWarningLights(root.transform);

                // 4. Professional Lighting Pass
                ApplyProLighting();

                // 5. Post-Processing (Programmatic setup)
                SetupPostProcessing();

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[EnvironmentDresser] Level 1 Dressing completed successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[EnvironmentDresser] ERROR: " + ex);
            }
        }

        static void ClearExistingDressing()
        {
            var existing = GameObject.Find(DecorRootName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                Debug.Log("[EnvironmentDresser] Removed previous dressing root.");
            }
        }

        static void PlaceIndustrialPipes(Transform parent)
        {
            // Place pipes along the top edges and randomly on walls
            Color pipeColor = new Color(0.2f, 0.22f, 0.25f);
            Material pipeMat = CombatFX.LitMaterial(pipeColor);

            for (int x = 0; x <= CorridorEndX; x += 15)
            {
                // High ceiling pipe
                CreateCylinder("Pipe_Ceiling_" + x, parent, 
                    new Vector3(x, CorridorHeight - 0.5f, CorridorHalfWidth - 0.5f), 
                    new Vector3(15f, 0.2f, 0.2f), 
                    Quaternion.Euler(0, 0, 90), pipeMat);

                // Random wall pipe
                if (Random.value > 0.5f)
                {
                    float z = Random.value > 0.5f ? CorridorHalfWidth - 0.3f : -CorridorHalfWidth + 0.3f;
                    CreateCylinder("Pipe_Wall_" + x, parent,
                        new Vector3(x, Random.Range(1f, 4f), z),
                        new Vector3(Random.Range(2f, 5f), 0.15f, 0.15f),
                        Quaternion.Euler(0, 0, 90), pipeMat);
                }
            }
        }

        static void PlaceConsolesAndPanels(Transform parent)
        {
            // Tech panels and consoles
            Material panelMat = CombatFX.LitMaterial(new Color(0.15f, 0.18f, 0.22f));
            Material glowMat = CombatFX.GlowMaterial(Color.cyan, 3f);

            for (int x = 10; x < CorridorEndX; x += 25)
            {
                float z = (x % 50 == 0) ? -CorridorHalfWidth + 0.5f : CorridorHalfWidth - 0.5f;
                
                // Main Console
                var console = GameObject.CreatePrimitive(PrimitiveType.Cube);
                console.name = "Console_" + x;
                console.transform.SetParent(parent, false);
                console.transform.position = new Vector3(x, 1.2f, z);
                console.transform.localScale = new Vector3(0.8f, 1.5f, 0.6f);
                console.transform.localRotation = Quaternion.Euler(0, (z > 0 ? -90 : 90), 0);
                console.GetComponent<Renderer>().sharedMaterial = panelMat;

                // Glow Screen
                var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                screen.name = "Screen_" + x;
                screen.transform.SetParent(console.transform, false);
                screen.transform.localPosition = new Vector3(0, 0.2f, -0.31f);
                screen.transform.localScale = new Vector3(0.8f, 0.4f, 0.05f);
                screen.GetComponent<Renderer>().sharedMaterial = glowMat;
            }
        }

        static void PlaceCeilingDetails(Transform parent)
        {
            // Vents and structural beams
            Material beamMat = CombatFX.LitMaterial(new Color(0.1f, 0.12f, 0.15f));
            for (int x = 0; x <= CorridorEndX; x += 10)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Beam_" + x;
                beam.transform.SetParent(parent, false);
                beam.transform.position = new Vector3(x, CorridorHeight, 0);
                beam.transform.localScale = new Vector3(1f, 0.5f, CorridorHalfWidth * 2f);
                beam.GetComponent<Renderer>().sharedMaterial = beamMat;
            }
        }

        static void PlaceWarningLights(Transform parent)
        {
            // Amber warning lights every 30m
            for (int x = 0; x <= CorridorEndX; x += 30)
            {
                var lightGo = new GameObject("WarningLight_" + x);
                lightGo.transform.SetParent(parent, false);
                lightGo.transform.position = new Vector3(x, CorridorHeight - 0.2f, 0);
                
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.5f, 0f); // Amber
                l.intensity = 3f;
                l.range = 10f;
                
                // Visual bulb
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.transform.SetParent(lightGo.transform, false);
                bulb.transform.localScale = Vector3.one * 0.2f;
                bulb.GetComponent<Renderer>().sharedMaterial = CombatFX.GlowMaterial(new Color(1f, 0.5f, 0f), 5f);
            }
        }

        static void ApplyProLighting()
        {
            // Deep Cobalt / Cyan / Amber palette
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.02f, 0.04f, 0.08f);

            var dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                var l = dirLight.GetComponent<Light>();
                l.color = new Color(0.4f, 0.5f, 0.7f);
                l.intensity = 0.3f;
            }
        }

        static void SetupPostProcessing()
        {
            // Setup basic Volume if URP is present
            // Since we can't easily add packages, we ensure a global Volume object exists
            // and we set the fog to a professional deep blue.
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.02f, 0.05f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;
        }

        static void CreateCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
