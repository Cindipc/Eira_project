using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace EiraGame
{
    public class AddDefaultSkybox : EditorWindow
    {
        [MenuItem("Eira/Add Default Skybox & Lighting")]
        public static void AddSkybox()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");

            Debug.Log("=== ADDING DEFAULT SKYBOX & LIGHTING ===");

            // 1. Create a simple procedural skybox material
            Material skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
            if (skyboxMat == null)
            {
                skyboxMat = new Material(Shader.Find("Skybox/6 Sided"));
            }
            if (skyboxMat == null)
            {
                skyboxMat = new Material(Shader.Find("Skybox/Cubemap"));
            }
            if (skyboxMat == null)
            {
                skyboxMat = new Material(Shader.Find("Standard"));
                Debug.LogWarning("No skybox shader found, using Standard");
            }

            // Configure procedural skybox
            if (skyboxMat.shader.name.Contains("Procedural"))
            {
                skyboxMat.SetColor("_SkyTint", new Color(0.5f, 0.7f, 1f));
                skyboxMat.SetColor("_GroundColor", new Color(0.2f, 0.25f, 0.3f));
                skyboxMat.SetFloat("_Exposure", 1.2f);
                skyboxMat.SetFloat("_SunSize", 0.04f);
                skyboxMat.SetFloat("_SunSizeConvergence", 5f);
                skyboxMat.SetFloat("_AtmosphereThickness", 1f);
            }

            // Assign to RenderSettings
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            // 2. Ensure there's a Directional Light (Sun)
            Light sunLight = GameObject.FindObjectOfType<Light>();
            if (sunLight == null)
            {
                GameObject sunObj = new GameObject("Directional Light (Sun)");
                sunLight = sunObj.AddComponent<Light>();
                sunLight.type = LightType.Directional;
                sunLight.intensity = 1.5f;
                sunLight.color = new Color(1f, 0.95f, 0.85f);
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = 1f;
                sunObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("Created Directional Light (Sun)");
            }
            else
            {
                sunLight.type = LightType.Directional;
                sunLight.intensity = 1.5f;
                sunLight.color = new Color(1f, 0.95f, 0.85f);
                sunLight.shadows = LightShadows.Soft;
                sunLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                Debug.Log("Configured existing light as Directional Sun");
            }

            // 3. Set up fog for atmosphere
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.7f, 0.85f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.005f;

            // 4. Save skybox material to Assets for persistence
            string matPath = "Assets/Eira/Materials/DefaultSkybox.mat";
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(matPath));
            AssetDatabase.CreateAsset(skyboxMat, matPath);
            AssetDatabase.SaveAssets();

            // 5. Assign the saved material
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("=== SKYBOX & LIGHTING ADDED ===");
            Debug.Log($"Skybox: {RenderSettings.skybox.name}");
            Debug.Log($"Sun: {sunLight.name} at rotation {sunLight.transform.eulerAngles}");
            Debug.Log("Fog enabled");
        }
    }
}