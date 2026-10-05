using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace EiraGame
{
    public class ReplaceLevel1WithFuturisticCity : EditorWindow
    {
        [MenuItem("Eira/Replace Level 1 with Futuristic City FBX")]
        public static void ReplaceLevel1()
        {
            string scenePath = "Assets/Eira/Scenes/Level1Scene.unity";
            string fbxPath = "Assets/Eira/Models/FuturisticCity.fbx";

            // Open the scene
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Debug.Log($"Opened scene: {scene.name}");

            // 1. Save sky/lighting settings BEFORE clearing
            var renderSettings = new RenderSettingsSnapshot();
            renderSettings.Capture();

            // 2. Find and destroy old level geometry (keep Eira, Nova, Camera, GameManager, etc.)
            var allObjects = scene.GetRootGameObjects();
            foreach (var obj in allObjects)
            {
                string name = obj.name;
                // Keep essential gameplay objects
                if (name == "Eira" || name == "NOVA" || name == "Nova" || 
                    name == "MainCamera" || name == "EiraCamera" ||
                    name == "GameManager" || name == "MissionDirector" ||
                    name == "HUD" || name == "AudioSFX" ||
                    name == "LevelEntry" || name == "LevelExit" ||
                    name == "EnvironmentDressing" || name == "PostProcessing" ||
                    name == "Directional Light" || name == "Skybox" ||
                    name.StartsWith("Pickup") || name.StartsWith("Drone") ||
                    name.StartsWith("Debris") || name.StartsWith("Explosion") ||
                    name == "DataLog" || name.Contains("Panel") || name.Contains("Terminal"))
                {
                    continue;
                }

                // Destroy old level geometry: corridors, slabs, walls, floors, buildings, etc.
                if (name.Contains("Corridor") || name.Contains("Slab") || name.Contains("Wall") ||
                    name.Contains("Floor") || name.Contains("Ceiling") || name.Contains("Building") ||
                    name.Contains("Architecture") || name.Contains("Environment") || name.Contains("Level") ||
                    name == "Ground" || name == "Terrain" || name.Contains("Geometry") ||
                    name == "CorridorRoot" || name == "LevelGeometry" || name == "ArchitectureRoot")
                {
                    Debug.Log($"Destroying old level object: {name}");
                    Undo.DestroyObjectImmediate(obj);
                }
            }

            // 3. Import and instantiate the Futuristic City FBX
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxAsset == null)
            {
                Debug.LogError($"FBX not found at {fbxPath}. Make sure it's imported first.");
                return;
            }

            var cityInstance = PrefabUtility.InstantiatePrefab(fbxAsset) as GameObject;
            cityInstance.name = "FuturisticCity_Environment";
            cityInstance.transform.position = Vector3.zero;
            cityInstance.transform.rotation = Quaternion.identity;
            cityInstance.transform.localScale = Vector3.one;

            // Make sure it's at scene root
            cityInstance.transform.SetParent(null);

            // 4. Restore sky/lighting settings
            renderSettings.Apply();

            // 5. Ensure camera follows Eira (FirstPersonCamera)
            FirstPersonCamera.EnsureMainCamera();

            // 6. Re-position Eira if needed (put her at a good spawn point)
            var eira = GameObject.Find("Eira");
            if (eira != null)
            {
                // Try to find a good spawn position on the new geometry
                // For now, keep her at current position but ensure she's on ground
                var cc = eira.GetComponent<CharacterController>();
                if (cc != null)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(eira.transform.position + Vector3.up * 10f, Vector3.down, out hit, 50f))
                    {
                        eira.transform.position = new Vector3(eira.transform.position.x, hit.point.y, eira.transform.position.z);
                        Debug.Log($"Eira repositioned to ground at: {eira.transform.position}");
                    }
                }
            }

            // 7. Save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("=== LEVEL 1 REPLACED WITH FUTURISTIC CITY ===");
            Debug.Log("Sky/lighting preserved. Camera set to FirstPersonCamera.");
        }

        // Helper class to capture/restore render settings
        class RenderSettingsSnapshot
        {
            public Skybox skybox;
            public Material skyboxMaterial;
            public UnityEngine.Rendering.AmbientMode ambientMode;
            public Color ambientLight;
            public float ambientIntensity;
            public Color ambientSkyColor;
            public Color ambientEquatorColor;
            public Color ambientGroundColor;
            public float reflectionIntensity;
            public int reflectionBounces;
            public Cubemap customReflection;
            public float haloStrength;
            public float flareStrength;
            public FogMode fogMode;
            public Color fogColor;
            public float fogDensity;
            public float fogStartDistance;
            public float fogEndDistance;

            public void Capture()
            {
                skybox = FindObjectOfType<Skybox>();
                skyboxMaterial = RenderSettings.skybox;
                ambientMode = RenderSettings.ambientMode;
                ambientLight = RenderSettings.ambientLight;
                ambientIntensity = RenderSettings.ambientIntensity;
                ambientSkyColor = RenderSettings.ambientSkyColor;
                ambientEquatorColor = RenderSettings.ambientEquatorColor;
                ambientGroundColor = RenderSettings.ambientGroundColor;
                reflectionIntensity = RenderSettings.reflectionIntensity;
                reflectionBounces = RenderSettings.reflectionBounces;
                // customReflection lanza excepción si no hay cubemap - usar try/catch
                try { customReflection = RenderSettings.customReflection; } catch { customReflection = null; }
                haloStrength = RenderSettings.haloStrength;
                flareStrength = RenderSettings.flareStrength;
                fogMode = RenderSettings.fogMode;
                fogColor = RenderSettings.fogColor;
                fogDensity = RenderSettings.fogDensity;
                fogStartDistance = RenderSettings.fogStartDistance;
                fogEndDistance = RenderSettings.fogEndDistance;
            }

            public void Apply()
            {
                RenderSettings.skybox = skyboxMaterial;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.ambientIntensity = ambientIntensity;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.reflectionIntensity = reflectionIntensity;
                RenderSettings.reflectionBounces = reflectionBounces;
                if (customReflection != null)
                    RenderSettings.customReflection = customReflection;
                RenderSettings.haloStrength = haloStrength;
                RenderSettings.flareStrength = flareStrength;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogDensity = fogDensity;
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = fogEndDistance;

                // Restore skybox component if it existed
                var existingSkybox = FindObjectOfType<Skybox>();
                if (existingSkybox == null && skybox != null)
                {
                    var cam = Camera.main;
                    if (cam != null)
                    {
                        var newSkybox = cam.gameObject.AddComponent<Skybox>();
                        newSkybox.material = skyboxMaterial;
                    }
                }
                else if (existingSkybox != null)
                {
                    existingSkybox.material = skyboxMaterial;
                }
            }
        }
    }
}