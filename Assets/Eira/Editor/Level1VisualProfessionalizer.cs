using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Professionalizes the visual identity of EIRA Level 1.
    /// Implements a cinematic industrial palette: Cobalt, Cyan, Amber, and Red.
    /// Works exclusively on 'EIRA_Level1_Generated'.
    /// </summary>
    public static class Level1VisualProfessionalizer
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string TargetRootName = "EIRA_Level1_Generated";
        const string DecorRootName = "Visual_Decor_Lights";

        // Color Palette
        static readonly Color ColIndustrialBlack = new Color(0.06f, 0.08f, 0.10f); // #101419
        static readonly Color ColMetallicGrey = new Color(0.20f, 0.24f, 0.27f);   // #343C46
        static readonly Color ColTechCyan = new Color(0.00f, 0.85f, 1.00f);       // #00D9FF
        static readonly Color ColDeepBlue = new Color(0.07f, 0.23f, 0.38f);       // #123B60
        static readonly Color ColWarningAmber = new Color(1.00f, 0.69f, 0.00f);   // #FFB000
        static readonly Color ColDangerRed = new Color(1.00f, 0.20f, 0.31f);       // #FF334F
        static readonly Color ColColdWhite = new Color(0.86f, 0.92f, 0.95f);      // #DCEAF2

        [MenuItem("EIRA/Visuals/Professionalize Level 1")]
        public static void Professionalize()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Visuals] Cannot professionalize while in Play Mode.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
                
                GameObject root = GameObject.Find(TargetRootName);
                if (root == null)
                {
                    Debug.LogError($"[Visuals] Error: {TargetRootName} not found. Please reconstruct the scenario first.");
                    return;
                }

                Debug.Log("[Visuals] Applying professional industrial design to EIRA...");

                ApplyGlobalAtmosphere();
                ApplyZonalMaterials(root.transform);
                ApplyZonalLighting(root.transform);
                
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Visuals] Level 1 professionalization completed successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Visuals] ERROR during professionalization: " + ex);
            }
        }

        static void ApplyGlobalAtmosphere()
        {
            // Ambient Light: Neutral dark grey to avoid excessive blue tinting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.04f, 0.06f);

            // Fog: Reduced density for better visibility of distant zones
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.02f, 0.04f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;

            // Directional Light: Subtle fill light to prevent pitch-black areas
            var dirLight = GameObject.Find("Directional Light");
            if (dirLight == null)
            {
                dirLight = new GameObject("Directional Light").AddComponent<Light>().gameObject;
            }
            var l = dirLight.GetComponent<Light>();
            l.type = LightType.Directional;
            l.color = ColColdWhite;
            l.intensity = 0.2f;
            l.shadows = LightShadows.Soft;
        }

        static void ApplyZonalMaterials(Transform root)
        {
            // Use a consistent naming convention to avoid material duplication in the project
            var materials = new Dictionary<string, Material>();
            materials["Metal"] = CreateIndustrialMaterial("Mat_Industrial_Metal", ColMetallicGrey, 0.4f, 0.7f, Color.black);
            materials["Tech"] = CreateIndustrialMaterial("Mat_Industrial_Tech", ColDeepBlue, 0.6f, 0.4f, ColTechCyan * 1.5f);
            materials["Danger"] = CreateIndustrialMaterial("Mat_Industrial_Danger", ColIndustrialBlack, 0.3f, 0.6f, ColDangerRed * 1.5f);

            foreach (Transform zone in root)
            {
                string zoneName = zone.name;
                Material targetMat = materials["Metal"];
                if (zoneName.Contains("Zone4")) targetMat = materials["Danger"];
                else if (zoneName.Contains("Zone1") || zoneName.Contains("Zone5")) targetMat = materials["Tech"];

                var renderers = zone.GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r.name.Contains("Light") || r.name.Contains("Warning")) continue;
                    
                    if (r.sharedMaterials.Length > 0)
                    {
                        var mats = r.sharedMaterials;
                        mats[0] = targetMat;
                        r.sharedMaterials = mats;
                    }
                }
            }
        }

        static void ApplyZonalLighting(Transform root)
        {
            // Remove existing decor lights and bulbs to avoid duplication
            var oldDecor = GameObject.Find(DecorRootName);
            if (oldDecor != null) Object.DestroyImmediate(oldDecor);

            GameObject decorRoot = new GameObject(DecorRootName);
            decorRoot.transform.SetParent(root, false);

            //- Zone 1 & 5 (Tech/Cyan)
            AddLightStrip(decorRoot.transform, 0, 20, ColTechCyan, 15f);
            AddLightStrip(decorRoot.transform, 140, 160, ColTechCyan, 15f);

            //- Zone 2 (Industrial/Amber)
            AddLightStrip(decorRoot.transform, 20, 60, ColWarningAmber, 20f);

            //- Zone 3 (Combat/Cyan)
            AddLightStrip(decorRoot.transform, 60, 100, ColTechCyan, 15f);
            
            //- Zone 4 (Boss/Red)
            AddLightStrip(decorRoot.transform, 100, 140, ColDangerRed, 20f);
        }

        static void AddLightStrip(Transform parent, float start, float end, Color color, float spacing)
        {
            for (float x = start; x <= end; x += spacing)
            {
                var lightGo = new GameObject($"Neon_{color.ToString()}");
                lightGo.transform.SetParent(parent, false);
                lightGo.transform.position = new Vector3(x, 5.5f, 0);
                
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = color;
                l.intensity = 1.5f; // Reduced intensity to prevent "burning" the image
                l.range = 8f;        // Slightly tighter range for better contrast

                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.transform.SetParent(lightGo.transform, false);
                bulb.transform.localScale = Vector3.one * 0.15f;
                bulb.GetComponent<Renderer>().sharedMaterial = CreateEmissiveMaterial(color);
            }
        }

        static Material CreateIndustrialMaterial(string name, Color baseCol, float smooth, float metal, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", baseCol);
            m.SetColor("_Color", baseCol);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            
            if (emission != Color.black)
            {
                m.SetColor("_EmissionColor", emission);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        static Material CreateEmissiveMaterial(Color col)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (m.shader == null) m = new Material(Shader.Find("Standard"));
            m.SetColor("_EmissionColor", col * 5f);
            m.EnableKeyword("_EMISSION");
            return m;
        }
    }
}
