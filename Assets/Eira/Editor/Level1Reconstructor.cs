using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// High-Fidelity Professional Reconstruction of Level 1.
    /// Transforms EIRA_Level1_Generated into a cinematic sci-fi industrial facility.
    /// </summary>
    public static class Level1Reconstructor
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string DecorRootName = "EIRA_Level1_Generated";

        // Corridor dimensions
        const float CorridorHalfWidth = 10f;
        const float CorridorHeight = 7f;
        
        // Zone spans along X axis
        const float Zone1End = 20f;
        const float Zone2End = 60f;
        const float Zone3End = 100f;
        const float Zone4End = 140f;
        const float Zone5End = 170f;

        // Cinematic Palette
        static readonly Color ColIndustrialBlack = new Color(0.04f, 0.05f, 0.07f); 
        static readonly Color ColMetallicGrey = new Color(0.18f, 0.22f, 0.26f);   
        static readonly Color ColTechCyan = new Color(0.00f, 0.80f, 1.00f);       
        static readonly Color ColDeepBlue = new Color(0.05f, 0.15f, 0.25f);       
        static readonly Color ColWarningAmber = new Color(1.00f, 0.60f, 0.00f);   
        static readonly Color ColDangerRed = new Color(1.00f, 0.10f, 0.20f);       
        static readonly Color ColColdWhite = new Color(0.90f, 0.95f, 1.00f);      

        [MenuItem("EIRA/Level 1/Rebuild Cinematic Scenario")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Reconstructor] Cannot rebuild while in Play Mode.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
                
                GameObject root = GameObject.Find(DecorRootName);
                if (root != null)
                {
                    Object.DestroyImmediate(root);
                }

                root = new GameObject(DecorRootName);
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;

                var materials = CreateProfessionalMaterials();

                // Architectural Reconstruction
                BuildEntryZone(root.transform, materials);
                BuildExplorationHall(root.transform, materials);
                BuildCombatHub(root.transform, materials);
                BuildBossArena(root.transform, materials);
                BuildExitZone(root.transform, materials);

                ApplyCinematicAtmosphere();
                SyncGameplaySystems();

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Reconstructor] Level 1 Cinematic Transformation Complete.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Reconstructor] CRITICAL ERROR: " + ex);
            }
        }

        static Dictionary<string, Material> CreateProfessionalMaterials()
        {
            var mats = new Dictionary<string, Material>();
            mats["Metal"] = CreateMaterial("Mat_Industrial_Metal", ColMetallicGrey, 0.5f, 0.9f, Color.black);
            mats["DarkMetal"] = CreateMaterial("Mat_Industrial_DarkMetal", ColIndustrialBlack, 0.3f, 0.8f, Color.black);
            mats["Tech"] = CreateMaterial("Mat_Industrial_Tech", ColDeepBlue, 0.7f, 0.5f, ColTechCyan * 3f);
            mats["Amber"] = CreateMaterial("Mat_Industrial_Amber", ColWarningAmber, 0.4f, 0.6f, ColWarningAmber * 2f);
            mats["Danger"] = CreateMaterial("Mat_Industrial_Danger", ColIndustrialBlack, 0.3f, 0.7f, ColDangerRed * 3f);
            mats["ExitCyan"] = CreateMaterial("Mat_Industrial_ExitCyan", ColMetallicGrey, 0.6f, 0.8f, ColTechCyan * 5f);
            return mats;
        }

        static Material CreateMaterial(string name, Color baseColor, float smoothness, float metallic, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission != Color.black)
            {
                m.SetColor("_EmissionColor", emission);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        static void BuildEntryZone(Transform root, Dictionary<string, Material> mats)
        {
            var zone = new GameObject("Zone1_Entry");
            zone.transform.SetParent(root, false);
            
            // Floor with structural seams
            CreateBox("Floor", zone.transform, new Vector3(Zone1End/2, -0.2f, 0), new Vector3(Zone1End, 0.4f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            
            // Walls with depth (main wall + panels)
            CreateBox("Wall_L", zone.transform, new Vector3(Zone1End/2, CorridorHeight/2, -CorridorHalfWidth), new Vector3(Zone1End, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_R", zone.transform, new Vector3(Zone1End/2, CorridorHeight/2, CorridorHalfWidth), new Vector3(Zone1End, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Ceiling", zone.transform, new Vector3(Zone1End/2, CorridorHeight, 0), new Vector3(Zone1End, 0.5f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            
            // Architectural Detail: Portal Entrance
            CreateBox("EntryFrame_L", zone.transform, new Vector3(0, CorridorHeight/2, -CorridorHalfWidth), new Vector3(2f, CorridorHeight, 1.2f), Quaternion.identity, mats["Tech"]);
            CreateBox("EntryFrame_R", zone.transform, new Vector3(0, CorridorHeight/2, CorridorHalfWidth), new Vector3(2f, CorridorHeight, 1.2f), Quaternion.identity, mats["Tech"]);
            
            // Tech panels on walls
            for (int x = 5; x < Zone1End; x += 8)
            {
                CreateBox("WallPanel_" + x, zone.transform, new Vector3(x, 2f, -CorridorHalfWidth + 0.3f), new Vector3(3f, 2f, 0.2f), Quaternion.identity, mats["Tech"]);
            }
        }

        static void BuildExplorationHall(Transform root, Dictionary<string, Material> mats)
        {
            var zone = new GameObject("Zone2_Hall");
            zone.transform.SetParent(root, false);
            float start = Zone1End;
            float end = Zone2End;
            float mid = (start + end) / 2;

            CreateBox("Floor", zone.transform, new Vector3(mid, -0.2f, 0), new Vector3(end - start, 0.4f, CorridorHalfWidth * 2), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_L", zone.transform, new Vector3(mid, CorridorHeight/2, -CorridorHalfWidth), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_R", zone.transform, new Vector3(mid, CorridorHeight/2, CorridorHalfWidth), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Ceiling", zone.transform, new Vector3(mid, CorridorHeight, 0), new Vector3(end - start, 0.5f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            
            // Industrial Piping and Cables
            for (int x = (int)start; x < end; x += 12)
            {
                CreateCylinder("Pipe_Main_" + x, zone.transform, new Vector3(x, CorridorHeight - 0.5f, CorridorHalfWidth - 0.5f), new Vector3(12f, 0.3f, 0.3f), Quaternion.Euler(0, 0, 90), mats["Metal"]);
                CreateCylinder("Pipe_Sub_" + x, zone.transform, new Vector3(x, CorridorHeight - 0.8f, CorridorHalfWidth - 0.8f), new Vector3(12f, 0.15f, 0.15f), Quaternion.Euler(0, 0, 90), mats["DarkMetal"]);
                
                // Structural Beams
                CreateBox("Beam_" + x, zone.transform, new Vector3(x, CorridorHeight, 0), new Vector3(1.5f, 0.8f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            }
        }

        static void BuildCombatHub(Transform root, Dictionary<string, Material> mats)
        {
            var zone = new GameObject("Zone3_CombatHub");
            zone.transform.SetParent(root, false);
            float start = Zone2End;
            float end = Zone3End;
            float mid = (start + end) / 2;

            // Widened Floor for combat
            CreateBox("Floor", zone.transform, new Vector3(mid, -0.2f, 0), new Vector3(end - start, 0.4f, CorridorHalfWidth * 3), Quaternion.identity, mats["DarkMetal"]);
            CreateBox("Wall_L", zone.transform, new Vector3(mid, CorridorHeight/2, -CorridorHalfWidth * 1.5f), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_R", zone.transform, new Vector3(mid, CorridorHeight/2, CorridorHalfWidth * 1.5f), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Ceiling", zone.transform, new Vector3(mid, CorridorHeight, 0), new Vector3(end - start, 0.5f, CorridorHalfWidth * 3), Quaternion.identity, mats["DarkMetal"]);
            
            // Modular Tech Covers (Combat Cover)
            CreateBox("Cover_L", zone.transform, new Vector3(mid - 8f, 1f, 4f), new Vector3(4f, 2.5f, 2f), Quaternion.identity, mats["Metal"]);
            CreateBox("Cover_R", zone.transform, new Vector3(mid + 8f, 1f, -4f), new Vector3(4f, 2.5f, 2f), Quaternion.identity, mats["Metal"]);
            
            // Central Power Core
            CreateBox("Core_Base", zone.transform, new Vector3(mid, 0.5f, 0), new Vector3(3f, 1f, 3f), Quaternion.identity, mats["DarkMetal"]);
            CreateBox("Core_Main", zone.transform, new Vector3(mid, 2.5f, 0), new Vector3(1.5f, 4f, 1.5f), Quaternion.identity, mats["Tech"]);
            
            // Floor Warning Strips
            for (int x = (int)start; x < end; x += 15)
            {
                CreateBox("FloorStrip_" + x, zone.transform, new Vector3(x, 0.01f, 0), new Vector3(2f, 0.02f, CorridorHalfWidth * 3), Quaternion.identity, mats["Amber"]);
            }
        }

        static void BuildBossArena(Transform root, Dictionary<string, Material> mats)
        {
            var zone = new GameObject("Zone4_BossArena");
            zone.transform.SetParent(root, false);
            float start = Zone3End;
            float end = Zone4End;
            float mid = (start + end) / 2;

            // Huge Floor
            CreateBox("Floor", zone.transform, new Vector3(mid, -0.2f, 0), new Vector3(end - start, 0.4f, CorridorHalfWidth * 4), Quaternion.identity, mats["DarkMetal"]);
            CreateBox("Wall_L", zone.transform, new Vector3(mid, CorridorHeight/2, -CorridorHalfWidth * 2), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_R", zone.transform, new Vector3(mid, CorridorHeight/2, CorridorHalfWidth * 2), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Ceiling", zone.transform, new Vector3(mid, CorridorHeight, 0), new Vector3(end - start, 0.5f, CorridorHalfWidth * 4), Quaternion.identity, mats["DarkMetal"]);
            
            // Heavy Industrial Pillars
            for (int x = (int)start + 10; x < end; x += 25)
            {
                CreateBox("Pillar_L_" + x, zone.transform, new Vector3(x, CorridorHeight/2, CorridorHalfWidth * 1.7f), new Vector3(3f, CorridorHeight, 3f), Quaternion.identity, mats["DarkMetal"]);
                CreateBox("Pillar_R_" + x, zone.transform, new Vector3(x, CorridorHeight/2, -CorridorHalfWidth * 1.7f), new Vector3(3f, CorridorHeight, 3f), Quaternion.identity, mats["DarkMetal"]);
                
                // Pillar Lighting
                CreateBox("PillarLight_" + x, zone.transform, new Vector3(x, CorridorHeight - 1f, CorridorHalfWidth * 1.7f), new Vector3(3.1f, 0.2f, 3.1f), Quaternion.identity, mats["Danger"]);
            }

            // Cinematic Danger Lighting
            for (int x = (int)start; x < end; x += 20)
            {
                var lightGo = new GameObject("BossLight_" + x);
                lightGo.transform.SetParent(zone.transform, false);
                lightGo.transform.position = new Vector3(x, CorridorHeight - 0.5f, 0);
                var l = lightGo.AddComponent<Light>();
                l.color = ColDangerRed;
                l.intensity = 5f;
                l.range = 20f;
                var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.transform.SetParent(lightGo.transform, false);
                bulb.transform.localScale = Vector3.one * 0.4f;
                bulb.GetComponent<Renderer>().sharedMaterial = mats["Danger"];
            }
        }

        static void BuildExitZone(Transform root, Dictionary<string, Material> mats)
        {
            var zone = new GameObject("Zone5_Exit");
            zone.transform.SetParent(root, false);
            float start = Zone4End;
            float end = Zone5End;
            float mid = (start + end) / 2;

            // Seamless transition from Arena: Floor continues
            CreateBox("Floor", zone.transform, new Vector3(mid, -0.2f, 0), new Vector3(end - start, 0.4f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            
            // Exit Hall Walls with architectural supports
            CreateBox("Wall_L", zone.transform, new Vector3(mid, CorridorHeight/2, -CorridorHalfWidth), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Wall_R", zone.transform, new Vector3(mid, CorridorHeight/2, CorridorHalfWidth), new Vector3(end - start, CorridorHeight, 0.5f), Quaternion.identity, mats["Metal"]);
            CreateBox("Ceiling", zone.transform, new Vector3(mid, CorridorHeight, 0), new Vector3(end - start, 0.5f, CorridorHalfWidth * 2), Quaternion.identity, mats["DarkMetal"]);
            
            // Structural Support Beams (Creating Depth)
            for (int x = (int)start; x < end; x += 10)
            {
                CreateBox("Support_L_" + x, zone.transform, new Vector3(x, CorridorHeight/2, -CorridorHalfWidth), new Vector3(1f, CorridorHeight, 1f), Quaternion.identity, mats["Metal"]);
                CreateBox("Support_R_" + x, zone.transform, new Vector3(x, CorridorHeight/2, CorridorHalfWidth), new Vector3(1f, CorridorHeight, 1f), Quaternion.identity, mats["Metal"]);
            }

            // FINAL EXIT: Futuristic Portal Structure
            // Frame
            CreateBox("ExitPortal_Base", zone.transform, new Vector3(Zone5End, 0.2f, 0), new Vector3(2f, 0.4f, CorridorHalfWidth * 2.2f), Quaternion.identity, mats["DarkMetal"]);
            CreateBox("ExitPortal_Ceil", zone.transform, new Vector3(Zone5End, CorridorHeight, 0), new Vector3(2f, 0.4f, CorridorHalfWidth * 2.2f), Quaternion.identity, mats["DarkMetal"]);
            CreateBox("ExitPortal_L", zone.transform, new Vector3(Zone5End, CorridorHeight/2, -CorridorHalfWidth), new Vector3(2f, CorridorHeight, 1f), Quaternion.identity, mats["ExitCyan"]);
            CreateBox("ExitPortal_R", zone.transform, new Vector3(Zone5End, CorridorHeight/2, CorridorHalfWidth), new Vector3(2f, CorridorHeight, 1f), Quaternion.identity, mats["ExitCyan"]);
            
            // The Door/Portal itself (Emissive Cyan)
            CreateBox("ExitDoor_Panel", zone.transform, new Vector3(Zone5End + 0.1f, CorridorHeight/2, 0), new Vector3(0.2f, CorridorHeight * 0.8f, CorridorHalfWidth * 1.8f), Quaternion.identity, mats["ExitCyan"]);
            
            // Cyan floor guidance strips
            for (int x = (int)start; x < Zone5End; x += 5)
            {
                CreateBox("ExitGuide_" + x, zone.transform, new Vector3(x, 0.01f, 0), new Vector3(1f, 0.02f, 2f), Quaternion.identity, mats["ExitCyan"]);
            }
        }

        static void ApplyCinematicAtmosphere()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.01f, 0.02f, 0.05f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.01f, 0.02f, 0.05f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.03f;
        }

        static void SyncGameplaySystems()
        {
            var player = GameObject.FindAnyObjectByType<PlayerController>();
            if (player != null) player.transform.position = new Vector3(5f, 1f, 0f);

            var boss = GameObject.FindAnyObjectByType<GuardianBoss>();
            if (boss != null) boss.transform.position = new Vector3((Zone3End + Zone4End) / 2, 1f, 0f);

            if (boss != null && boss.exitDoor != null)
            {
                boss.exitDoor.transform.position = new Vector3(Zone5End, CorridorHeight/2, 0f);
            }

            DroneController[] drones = GameObject.FindObjectsByType<DroneController>(FindObjectsSortMode.None);
            if (drones != null)
            {
                for (int i = 0; i < drones.Length; i++)
                {
                    float xPos = (i < drones.Length / 2) ? 40f : 80f;
                    float zPos = (i % 2 == 0) ? 5f : -5f;
                    drones[i].transform.position = new Vector3(xPos, 2f, zPos);
                }
            }
        }

        static GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject CreateCylinder(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }
    }
}
