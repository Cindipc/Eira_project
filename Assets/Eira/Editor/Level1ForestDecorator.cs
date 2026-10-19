using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Transforms the Forest area into a dangerous, dark, bio-luminescent wasteland.
    /// Adds overgrown ruins, predatory flora, and a heavy oppressive atmosphere.
    /// </summary>
    public static class Level1ForestDecorator
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string ForestRootName = "PisoBosque";
        const string DecorRootName = "Forest_Dark_Decor";

        // Dark & Dangerous Palette
        static readonly Color ColDeepVoid = new Color(0.02f, 0.02f, 0.05f);
        static readonly Color ColToxicViolet = new Color(0.4f, 0.0f, 0.6f);
        static readonly Color ColBioluminescentBlue = new Color(0.0f, 0.4f, 1.0f);
        static readonly Color ColSicklyGreen = new Color(0.2f, 0.5f, 0.1f);
        static readonly Color ColBloodRed = new Color(0.6f, 0.0f, 0.0f);

        [MenuItem("EIRA/Forest/Decorate as Dangerous Wasteland")]
        public static void Decorate()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ForestDecorator] Cannot decorate while in Play Mode.");
                return;
            }

            try
            {
                var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
                GameObject forest = GameObject.Find(ForestRootName);
                if (forest == null)
                {
                    Debug.LogError("[ForestDecorator] Error: PisoBosque not found. Connect lab first.");
                    return;
                }

                var oldDecor = GameObject.Find(DecorRootName);
                if (oldDecor != null) Object.DestroyImmediate(oldDecor);

                GameObject decorRoot = new GameObject(DecorRootName);
                decorRoot.transform.SetParent(forest.transform, false);

                var materials = CreateForestMaterials();

                // We assume PisoBosque is a large area starting from the Lab exit (approx x=175)
                float startX = 175f;
                float endX = 300f;
                float zHalf = 15f;

                // 1. Overgrown Ruins and Debris
                for (float x = startX + 10; x < endX; x += 20)
                {
                    CreateRuinFragment(decorRoot.transform, new Vector3(x, 0, (x % 40 == 0) ? 5f : -5f), materials);
                    CreateDeadTree(decorRoot.transform, new Vector3(x + 5, 0, (x % 40 == 0) ? -8f : 8f), materials);
                }

                // 2. Predatory Bio-luminescent Flora
                for (float x = startX + 5; x < endX; x += 12)
                {
                    CreatePredatoryPlant(decorRoot.transform, new Vector3(x, 0, (x % 24 == 0) ? 7f : -7f), materials);
                }

                // 3. Oppressive Lighting
                AddAtmosphericLight(decorRoot.transform, startX + 50, ColToxicViolet);
                AddAtmosphericLight(decorRoot.transform, startX + 120, ColBioluminescentBlue);
                AddAtmosphericLight(decorRoot.transform, startX + 200, ColBloodRed);

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[ForestDecorator] Forest transformed into a Dangerous Wasteland.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[ForestDecorator] CRITICAL ERROR: " + ex);
            }
        }

        static Dictionary<string, Material> CreateForestMaterials()
        {
            var mats = new Dictionary<string, Material>();
            mats["Rock"] = CreateMat("Mat_Forest_Rock", new Color(0.1f, 0.1f, 0.12f), 0.2f, 0.1f, Color.black);
            mats["Bark"] = CreateMat("Mat_Forest_Bark", new Color(0.05f, 0.04f, 0.03f), 0.1f, 0.0f, Color.black);
            mats["BiolumViolet"] = CreateMat("Mat_Forest_Violet", ColDeepVoid, 0.5f, 0.2f, ColToxicViolet * 3f);
            mats["BiolumBlue"] = CreateMat("Mat_Forest_Blue", ColDeepVoid, 0.5f, 0.2f, ColBioluminescentBlue * 3f);
            mats["Rust"] = CreateMat("Mat_Forest_Rust", new Color(0.3f, 0.15f, 0.1f), 0.2f, 0.4f, Color.black);
            return mats;
        }

        static Material CreateMat(string name, Color baseColor, float smooth, float metal, Color emission)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", baseColor);
            m.SetColor("_Color", baseColor);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission != Color.black)
            {
                m.SetColor("_EmissionColor", emission);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        static void CreateRuinFragment(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var group = new GameObject("RuinFragment");
            group.transform.SetParent(parent, false);
            group.transform.position = pos;

            // Slanted concrete block
            CreateBox("Slab", group.transform, new Vector3(0, 1f, 0), new Vector3(4f, 2f, 4f), Quaternion.Euler(20, 0, 15), mats["Rock"]);
            // Rusty beam sticking out
            CreateBox("Beam", group.transform, new Vector3(1f, 2f, 0), new Vector3(5f, 0.4f, 0.4f), Quaternion.Euler(-30, 0, 0), mats["Rust"]);
        }

        static void CreateDeadTree(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var group = new GameObject("DeadTree");
            group.transform.SetParent(parent, false);
            group.transform.position = pos;

            // Trunk
            CreateBox("Trunk", group.transform, new Vector3(0, 4f, 0), new Vector3(1.2f, 8f, 1.2f), Quaternion.Euler(5, 0, 10), mats["Bark"]);
            // Twisted branches
            CreateBox("Branch1", group.transform, new Vector3(0, 6f, 0), new Vector3(4f, 0.5f, 0.5f), Quaternion.Euler(0, 0, 30), mats["Bark"]);
            CreateBox("Branch2", group.transform, new Vector3(0, 5f, 0), new Vector3(3f, 0.5f, 0.5f), Quaternion.Euler(0, 0, -20), mats["Bark"]);
        }

        static void CreatePredatoryPlant(Transform parent, Vector3 pos, Dictionary<string, Material> mats)
        {
            var group = new GameObject("PredatoryPlant");
            group.transform.SetParent(parent, false);
            group.transform.position = pos;

            // Bulbous base
            CreateBox("Base", group.transform, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 2f), Quaternion.identity, mats["BiolumViolet"]);
            
            // Spikes / Tendrils
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                CreateBox($"Tendril_{i}", group.transform, new Vector3(0, 1f, 0), new Vector3(0.4f, 3f, 0.4f), Quaternion.Euler(45, angle, 0), mats["BiolumBlue"]);
            }
            
            AddLight(group.transform, new Vector3(0, 2f, 0), ColToxicViolet, 8f);
        }

        static void AddAtmosphericLight(Transform parent, float x, Color color)
        {
            var lightGo = new GameObject("ForestAtmosphereLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = new Vector3(x, 10f, 0);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 4f;
            l.range = 30f;
        }

        static void CreateBox(string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.transform.rotation = rot;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static void AddLight(Transform parent, Vector3 pos, Color color, float range)
        {
            var lightGo = new GameObject("PlantLight");
            lightGo.transform.SetParent(parent, false);
            lightGo.transform.position = pos;
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = 2f;
            l.range = range;
        }
    }
}
