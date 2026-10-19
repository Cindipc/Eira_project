using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Multi-layer sci-fi laboratory dressing for EIRA Level 1.
    ///
    /// Works exclusively on the existing generated level root
    /// ("EIRA_Level1_Generated") and rebuilds only its own container
    /// ("Lab_Detailed_Decor"). It never rebuilds the level, never moves
    /// gameplay objects and never adds colliders to decorative pieces.
    ///
    /// Menu:
    ///   EIRA/Lab/Decorate Level 1 (Preview)      -> applies, does not save
    ///   EIRA/Lab/Decorate Level 1 and Save       -> applies and saves
    ///   EIRA/Lab/Validate Lab Decoration         -> structural report in Console
    /// </summary>
    public static class Level1LabDecorator
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string TargetRootName = "EIRA_Level1_Generated";
        const string DecorRootName = "Lab_Detailed_Decor";
        const string MaterialFolder = "Assets/Eira/Materials/Laboratorio/Generated";

        const float CorridorHeight = 7f;
        const float CeilingUnder = 6.75f;
        const float WallHalfThickness = 0.25f;
        const float ModuleWidth = 4.0f;
        const float ModuleHeight = 5.8f;
        const float ModuleBaseY = 0.55f;

        static readonly Color ColIndustrialBlack = new Color(0.028f, 0.033f, 0.040f);
        static readonly Color ColGraphite = new Color(0.085f, 0.098f, 0.115f);
        static readonly Color ColSteel = new Color(0.150f, 0.170f, 0.195f);
        static readonly Color ColTitanium = new Color(0.255f, 0.280f, 0.305f);
        static readonly Color ColBlackFrame = new Color(0.014f, 0.016f, 0.020f);
        static readonly Color ColCeramic = new Color(0.360f, 0.375f, 0.390f);
        static readonly Color ColPlastic = new Color(0.050f, 0.058f, 0.068f);
        static readonly Color ColRubber = new Color(0.025f, 0.025f, 0.028f);
        static readonly Color ColGrime = new Color(0.075f, 0.062f, 0.045f);
        static readonly Color ColRust = new Color(0.185f, 0.080f, 0.038f);
        static readonly Color ColSignalYellow = new Color(0.720f, 0.560f, 0.035f);

        static readonly Color ColCyan = new Color(0.00f, 0.80f, 1.00f);
        static readonly Color ColBlue = new Color(0.08f, 0.32f, 1.00f);
        static readonly Color ColGreen = new Color(0.18f, 1.00f, 0.35f);
        static readonly Color ColAmber = new Color(1.00f, 0.52f, 0.02f);
        static readonly Color ColRed = new Color(1.00f, 0.06f, 0.08f);
        static readonly Color ColColdWhite = new Color(0.88f, 0.94f, 1.00f);

        struct ZoneInfo
        {
            public string Name;
            public float Start;
            public float End;
            public float HalfWidth;
            public int Index;

            public ZoneInfo(string name, float start, float end, float halfWidth, int index)
            {
                Name = name;
                Start = start;
                End = end;
                HalfWidth = halfWidth;
                Index = index;
            }

            public float Length { get { return End - Start; } }
            public float Mid { get { return (Start + End) * 0.5f; } }
            public float WallZ { get { return HalfWidth - WallHalfThickness; } }
        }

        static readonly ZoneInfo[] Zones =
        {
            new ZoneInfo("Z1_Entry",       0f,  20f, 10f, 0),
            new ZoneInfo("Z2_Laboratory",  20f, 60f, 10f, 1),
            new ZoneInfo("Z3_Combat",      60f, 100f, 15f, 2),
            new ZoneInfo("Z4_Containment", 100f, 140f, 20f, 3),
            new ZoneInfo("Z5_Exit",        140f, 170f, 10f, 4)
        };

        enum ModuleType
        {
            BlastArmor,
            Electrical,
            Maintenance,
            TechPanel,
            Conduits,
            ContainmentWindow,
            Vent
        }

        enum SignType
        {
            Biohazard,
            HighVoltage,
            Restricted,
            Research,
            Containment,
            Exit,
            Compartment
        }

        [MenuItem("EIRA/Lab/Decorate Level 1 (Preview)", priority = 10)]
        public static void DecoratePreview()
        {
            Execute(false);
        }

        [MenuItem("EIRA/Lab/Decorate Level 1 and Save", priority = 11)]
        public static void DecorateAndSave()
        {
            Execute(true);
        }

        [MenuItem("EIRA/Lab/Decorate as Abandoned Laboratory", priority = 12)]
        static void DecorateLegacyEntry()
        {
            Execute(true);
        }

        [MenuItem("EIRA/Lab/Validate Lab Decoration", priority = 30)]
        public static void ValidateDecoration()
        {
            var decor = FindSceneRoot(DecorRootName);
            if (decor == null)
            {
                Debug.LogError("[LabDecorator] Validation: " + DecorRootName + " was not found in the active scene.");
                return;
            }

            var renderers = decor.GetComponentsInChildren<Renderer>(true);
            var colliders = decor.GetComponentsInChildren<Collider>(true);
            var lights = decor.GetComponentsInChildren<Light>(true);
            var transforms = decor.GetComponentsInChildren<Transform>(true);

            int modules = 0;
            Bounds bounds = new Bounds(decor.transform.position, Vector3.zero);
            bool hasBounds = false;

            foreach (var t in transforms)
            {
                if (t.name.StartsWith("Module_")) modules++;
                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    if (!hasBounds)
                    {
                        bounds = r.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }

            Debug.Log("[LabDecorator] Validation report\n" +
                      "  Root: " + DecorRootName + "\n" +
                      "  Transforms: " + transforms.Length + "\n" +
                      "  Renderers: " + renderers.Length + "\n" +
                      "  Colliders (must be 0): " + colliders.Length + "\n" +
                      "  Lights: " + lights.Length + "\n" +
                      "  Module groups: " + modules + "\n" +
                      "  Bounds center: " + bounds.center.ToString("F2") + "  size: " + bounds.size.ToString("F2"));

            if (colliders.Length > 0)
            {
                Debug.LogError("[LabDecorator] Decorative colliders detected. Gameplay must remain untouched.");
            }
        }

        static void Execute(bool save)
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[LabDecorator] Cannot decorate while Unity is in Play Mode. Stop the game first.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != Level1Scene)
            {
                if (!Application.isBatchMode && scene.isDirty)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        Debug.LogWarning("[LabDecorator] Operation cancelled. The active scene was not saved.");
                        return;
                    }
                }

                scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
            }

            GameObject root = FindSceneRoot(TargetRootName);
            if (root == null)
            {
                Debug.LogError("[LabDecorator] Error: " + TargetRootName + " was not found. Rebuild the level first.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Decorate Level 1 Laboratory");

            var oldDecor = FindSceneRoot(DecorRootName);
            if (oldDecor != null)
            {
                Undo.DestroyObjectImmediate(oldDecor);
            }

            var decorRoot = new GameObject(DecorRootName);
            Undo.RegisterCreatedObjectUndo(decorRoot, "Create detailed laboratory decoration");
            decorRoot.transform.SetParent(root.transform, false);
            decorRoot.transform.localPosition = Vector3.zero;
            decorRoot.transform.localRotation = Quaternion.identity;
            decorRoot.transform.localScale = Vector3.one;

            var mats = new MaterialLibrary();
            int moduleCounter = 0;

            for (int i = 0; i < Zones.Length; i++)
            {
                moduleCounter += BuildZone(decorRoot.transform, Zones[i], mats);
            }

            BuildConnectingDressing(decorRoot.transform, mats);

            Undo.CollapseUndoOperations(undoGroup);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);

            if (save)
            {
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[LabDecorator] Level 1 laboratory dressing generated.\n" +
                      "  Zones: " + Zones.Length + "\n" +
                      "  Wall modules: " + moduleCounter + "\n" +
                      "  Container: EIRA_Level1_Generated/" + DecorRootName + "\n" +
                      (save ? "  Scene saved." : "  Preview only: scene is dirty and has not been saved."));
        }

        static GameObject FindSceneRoot(string name)
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid()) return null;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name) return roots[i];
            }

            return GameObject.Find(name);
        }

        class MaterialLibrary
        {
            public Material SteelDark;
            public Material Graphite;
            public Material Steel;
            public Material Titanium;
            public Material BlackFrame;
            public Material Ceramic;
            public Material Plastic;
            public Material Rubber;
            public Material Grime;
            public Material Rust;
            public Material SignalYellow;

            public Material GlassTint;
            public Material GlassGreen;
            public Material LiquidGreen;
            public Material LiquidCyan;

            public Material CyanGlow;
            public Material BlueGlow;
            public Material GreenGlow;
            public Material AmberGlow;
            public Material RedGlow;
            public Material WhiteGlow;

            public Material ScreenCyan;
            public Material ScreenAmber;
            public Material ScreenDark;
            public Material IndicatorDark;

            public MaterialLibrary()
            {
                EnsureFolder(MaterialFolder);

                SteelDark = Mat("Mat_LabGen_SteelDark", ColIndustrialBlack, 0.84f, 0.34f, Color.black, 0f, false, 1f);
                Graphite = Mat("Mat_LabGen_Graphite", ColGraphite, 0.82f, 0.30f, Color.black, 0f, false, 1f);
                Steel = Mat("Mat_LabGen_Steel", ColSteel, 0.86f, 0.42f, Color.black, 0f, false, 1f);
                Titanium = Mat("Mat_LabGen_Titanium", ColTitanium, 0.92f, 0.48f, Color.black, 0f, false, 1f);
                BlackFrame = Mat("Mat_LabGen_BlackFrame", ColBlackFrame, 0.62f, 0.22f, Color.black, 0f, false, 1f);
                Ceramic = Mat("Mat_LabGen_Ceramic", ColCeramic, 0.06f, 0.58f, Color.black, 0f, false, 1f);
                Plastic = Mat("Mat_LabGen_Plastic", ColPlastic, 0.02f, 0.72f, Color.black, 0f, false, 1f);
                Rubber = Mat("Mat_LabGen_Rubber", ColRubber, 0.00f, 0.08f, Color.black, 0f, false, 1f);
                Grime = Mat("Mat_LabGen_Grime", ColGrime, 0.14f, 0.20f, Color.black, 0f, false, 1f);
                Rust = Mat("Mat_LabGen_Rust", ColRust, 0.10f, 0.22f, Color.black, 0f, false, 1f);
                SignalYellow = Mat("Mat_LabGen_SignalYellow", ColSignalYellow, 0.10f, 0.48f, Color.black, 0f, false, 1f);

                GlassTint = Mat("Mat_LabGen_GlassTint", new Color(0.10f, 0.19f, 0.22f, 0.30f), 0.95f, 0.12f, Color.black, 0f, true, 0.30f);
                GlassGreen = Mat("Mat_LabGen_GlassGreen", new Color(0.06f, 0.32f, 0.24f, 0.26f), 0.95f, 0.10f, Color.black, 0f, true, 0.26f);
                LiquidGreen = Mat("Mat_LabGen_LiquidGreen", new Color(0.08f, 0.75f, 0.24f, 0.58f), 0.06f, 0.30f, ColGreen, 1.1f, true, 0.58f);
                LiquidCyan = Mat("Mat_LabGen_LiquidCyan", new Color(0.05f, 0.62f, 0.92f, 0.58f), 0.06f, 0.30f, ColCyan, 1.0f, true, 0.58f);

                CyanGlow = Mat("Mat_LabGen_CyanGlow", new Color(0.01f, 0.05f, 0.07f), 0.20f, 0.35f, ColCyan, 3.6f, false, 1f);
                BlueGlow = Mat("Mat_LabGen_BlueGlow", new Color(0.01f, 0.03f, 0.09f), 0.20f, 0.35f, ColBlue, 3.0f, false, 1f);
                GreenGlow = Mat("Mat_LabGen_GreenGlow", new Color(0.01f, 0.06f, 0.02f), 0.20f, 0.35f, ColGreen, 3.4f, false, 1f);
                AmberGlow = Mat("Mat_LabGen_AmberGlow", new Color(0.08f, 0.04f, 0.01f), 0.20f, 0.35f, ColAmber, 3.2f, false, 1f);
                RedGlow = Mat("Mat_LabGen_RedGlow", new Color(0.09f, 0.01f, 0.01f), 0.20f, 0.35f, ColRed, 3.8f, false, 1f);
                WhiteGlow = Mat("Mat_LabGen_WhiteGlow", new Color(0.20f, 0.22f, 0.25f), 0.20f, 0.35f, ColColdWhite, 2.6f, false, 1f);

                ScreenCyan = Mat("Mat_LabGen_ScreenCyan", new Color(0.008f, 0.045f, 0.060f), 0.55f, 0.18f, ColCyan, 1.45f, false, 1f);
                ScreenAmber = Mat("Mat_LabGen_ScreenAmber", new Color(0.060f, 0.035f, 0.006f), 0.55f, 0.18f, ColAmber, 1.35f, false, 1f);
                ScreenDark = Mat("Mat_LabGen_ScreenDark", new Color(0.004f, 0.008f, 0.010f), 0.60f, 0.12f, ColCyan, 0.16f, false, 1f);
                IndicatorDark = Mat("Mat_LabGen_IndicatorDark", new Color(0.006f, 0.008f, 0.010f), 0.60f, 0.25f, Color.black, 0f, false, 1f);
            }
        }

        static Material Mat(string name, Color baseColor, float metallic, float smoothness,
                            Color emission, float emissionIntensity, bool transparent, float alpha)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (m == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                m = new Material(shader);
                m.name = name;
                AssetDatabase.CreateAsset(m, path);
            }

            Color baseWithAlpha = baseColor;
            baseWithAlpha.a = transparent ? alpha : 1f;

            m.SetColor("_BaseColor", baseWithAlpha);
            m.SetColor("_Color", baseWithAlpha);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Glossiness", smoothness);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.enableInstancing = true;

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Mode", 3f);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetShaderPassEnabled("ShadowCaster", false);
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.One);
                m.SetFloat("_DstBlend", (float)BlendMode.Zero);
                m.SetFloat("_ZWrite", 1f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Mode", 0f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.DisableKeyword("_ALPHATEST_ON");
                m.SetShaderPassEnabled("ShadowCaster", true);
                m.renderQueue = -1;
            }

            if (emission != Color.black && emissionIntensity > 0f)
            {
                Color e = emission;
                e.a = 1f;
                m.SetColor("_EmissionColor", e * emissionIntensity);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string normalized = path.Replace('\\', '/');
            int lastSlash = normalized.LastIndexOf('/');
            if (lastSlash <= 0) return;

            string parent = normalized.Substring(0, lastSlash);
            string folder = normalized.Substring(lastSlash + 1);
            EnsureFolder(parent);

            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }


        // ------------------------------------------------------------------
        // Zone and wall generation
        // ------------------------------------------------------------------

        static int BuildZone(Transform decorRoot, ZoneInfo zone, MaterialLibrary mats)
        {
            var zoneRoot = new GameObject("Zone_" + zone.Name);
            zoneRoot.transform.SetParent(decorRoot, false);
            zoneRoot.transform.localPosition = Vector3.zero;

            int generated = 0;

            generated += BuildWallRun(zoneRoot.transform, zone, -1, mats);
            generated += BuildWallRun(zoneRoot.transform, zone, 1, mats);

            BuildCeiling(zoneRoot.transform, zone, mats);
            BuildFloorDressing(zoneRoot.transform, zone, mats);
            BuildZoneEquipment(zoneRoot.transform, zone, mats);
            BuildZoneSignage(zoneRoot.transform, zone, mats);
            BuildZoneLighting(zoneRoot.transform, zone, mats);

            return generated;
        }

        static int BuildWallRun(Transform parent, ZoneInfo zone, int side, MaterialLibrary mats)
        {
            string sideName = side < 0 ? "WallL" : "WallR";
            var wallRoot = new GameObject(sideName);
            wallRoot.transform.SetParent(parent, false);
            wallRoot.transform.localPosition = Vector3.zero;

            float wallZ = zone.WallZ * side;
            Quaternion rotation = side > 0 ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;

            BuildWallBase(wallRoot.transform, zone, side, wallZ, rotation, mats);

            List<ModuleSlot> slots = BuildLayout(zone, side);
            for (int i = 0; i < slots.Count; i++)
            {
                BuildWallModule(wallRoot.transform, zone, slots[i], wallZ, rotation, mats);
            }

            BuildWallPipes(wallRoot.transform, zone, side, wallZ, rotation, mats);

            return slots.Count;
        }

        struct ModuleSlot
        {
            public ModuleType Type;
            public int Variant;
            public float CenterX;
            public float Width;
            public float Seed;
        }

        static List<ModuleSlot> BuildLayout(ZoneInfo zone, int side)
        {
            var result = new List<ModuleSlot>();

            bool hasEntry = zone.Index <= 1;
            bool hasExit = zone.Index >= 3;

            float usableStart = zone.Start + (hasEntry ? 1.6f : 0.9f);
            float usableEnd = zone.End - (hasExit ? 1.6f : 0.9f);
            float usable = usableEnd - usableStart;
            if (usable < 2f) return result;

            float spacing = 0.32f;
            float maxWidth = ModuleWidth;
            int count = Mathf.Max(1, Mathf.CeilToInt((usable + spacing) / (maxWidth + spacing)));
            float actualWidth = (usable - spacing * (count - 1)) / count;
            actualWidth = Mathf.Clamp(actualWidth, 2.2f, maxWidth);
            float firstX = usableStart + actualWidth * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = firstX + i * (actualWidth + spacing);
                float seed = Hash(zone.Index, side, i);

                result.Add(new ModuleSlot
                {
                    Type = ChooseModuleType(zone, side, i, seed),
                    Variant = (int)(Hash(side, i, zone.Index + 17) % 3),
                    CenterX = x,
                    Width = actualWidth,
                    Seed = seed
                });
            }

            return result;
        }

        static ModuleType ChooseModuleType(ZoneInfo zone, int side, int index, float seed)
        {
            float r = seed % 1f;

            if (zone.Index == 0)
            {
                if (index == 0) return ModuleType.BlastArmor;
                if (r < 0.34f) return ModuleType.Electrical;
                if (r < 0.68f) return ModuleType.Conduits;
                return ModuleType.BlastArmor;
            }

            if (zone.Index == 1)
            {
                if (r < 0.26f) return ModuleType.ContainmentWindow;
                if (r < 0.50f) return ModuleType.TechPanel;
                if (r < 0.74f) return ModuleType.Electrical;
                return ModuleType.Conduits;
            }

            if (zone.Index == 2)
            {
                if (r < 0.30f) return ModuleType.BlastArmor;
                if (r < 0.58f) return ModuleType.Maintenance;
                if (r < 0.82f) return ModuleType.Conduits;
                return ModuleType.Vent;
            }

            if (zone.Index == 3)
            {
                if (r < 0.30f) return ModuleType.ContainmentWindow;
                if (r < 0.52f) return ModuleType.BlastArmor;
                if (r < 0.76f) return ModuleType.TechPanel;
                return ModuleType.Maintenance;
            }

            if (r < 0.46f) return ModuleType.BlastArmor;
            if (r < 0.74f) return ModuleType.Vent;
            return ModuleType.Conduits;
        }

        static void BuildWallBase(Transform parent, ZoneInfo zone, int side, float wallZ, Quaternion rotation, MaterialLibrary mats)
        {
            float length = zone.Length;
            float centerX = zone.Mid;

            Vector3 at = new Vector3(centerX, 0f, wallZ);
            Vector3 inward = rotation * Vector3.forward;

            var baseRoot = new GameObject("Wall_Base");
            baseRoot.transform.SetParent(parent, false);
            baseRoot.transform.localPosition = Vector3.zero;

            AddBox(baseRoot.transform, "Plinth", at + Vector3.up * 0.34f + inward * 0.035f,
                    new Vector3(length, 0.68f, 0.34f), rotation, mats.SteelDark);
            AddBox(baseRoot.transform, "PlinthInset", at + Vector3.up * 0.34f + inward * 0.215f,
                    new Vector3(length - 0.20f, 0.46f, 0.04f), rotation, mats.Graphite);
            AddBox(baseRoot.transform, "WallPanelUpper", at + Vector3.up * 3.35f + inward * 0.02f,
                    new Vector3(length, 5.14f, 0.06f), rotation, mats.Graphite);
            AddBox(baseRoot.transform, "Cornice", at + Vector3.up * 5.74f + inward * 0.09f,
                    new Vector3(length, 0.28f, 0.24f), rotation, mats.SteelDark);
            AddBox(baseRoot.transform, "CorniceLip", at + Vector3.up * 5.60f + inward * 0.22f,
                    new Vector3(length, 0.07f, 0.06f), rotation, mats.Steel);

            float ribSpacing = 4.2f;
            int ribs = Mathf.Max(2, Mathf.CeilToInt(length / ribSpacing) + 1);
            for (int i = 0; i < ribs; i++)
            {
                float t = ribs == 1 ? 0.5f : i / (float)(ribs - 1);
                float x = Mathf.Lerp(zone.Start + 0.22f, zone.End - 0.22f, t);
                float seed = Hash(side, i, zone.Index);

                AddBox(baseRoot.transform, "WallRib", new Vector3(x, 2.95f, wallZ + inward.z * 0.075f),
                        new Vector3(0.16f, 5.35f, 0.18f), rotation, mats.SteelDark);
                AddBox(baseRoot.transform, "RibAccent", new Vector3(x, 0.86f, wallZ + inward.z * 0.175f),
                        new Vector3(0.075f, 1.10f, 0.035f), rotation, (seed % 1f) < 0.55f ? mats.CyanGlow : mats.AmberGlow);

                AddBox(baseRoot.transform, "RibGussetLow", new Vector3(x, 0.74f, wallZ + inward.z * 0.20f),
                        new Vector3(0.30f, 0.16f, 0.40f), rotation, mats.SteelDark);
                AddBox(baseRoot.transform, "RibGussetHigh", new Vector3(x, 5.56f, wallZ + inward.z * 0.18f),
                        new Vector3(0.32f, 0.16f, 0.38f), rotation, mats.SteelDark);

                AddBolt(baseRoot.transform, new Vector3(x, 1.10f, wallZ + inward.z * 0.215f), rotation, mats.Titanium, 0.045f);
                AddBolt(baseRoot.transform, new Vector3(x, 5.18f, wallZ + inward.z * 0.205f), rotation, mats.Titanium, 0.045f);

                if ((seed * 7f) % 1f < 0.55f)
                {
                    AddBox(baseRoot.transform, "WallGrime", new Vector3(x + 0.55f, 2.4f, wallZ + inward.z * 0.055f),
                            new Vector3(0.95f, 1.40f, 0.012f), rotation, mats.Grime);
                }
            }

            AddBox(baseRoot.transform, "FloorThreshold", at + Vector3.up * 0.012f + inward * 0.54f,
                    new Vector3(length, 0.024f, 0.84f), rotation, mats.SteelDark);
            AddBox(baseRoot.transform, "FloorWarningLine", at + Vector3.up * 0.028f + inward * 0.94f,
                    new Vector3(length, 0.018f, 0.12f), rotation, mats.SignalYellow);

            for (float x = zone.Start + 1.2f; x < zone.End - 0.6f; x += 6.0f)
            {
                AddHazardStrip(baseRoot.transform, new Vector3(x, 0.032f, wallZ + inward.z * 1.12f), rotation, 1.6f, mats);
            }
        }

        static void BuildWallModule(Transform parent, ZoneInfo zone, ModuleSlot slot, float wallZ, Quaternion rotation, MaterialLibrary mats)
        {
            var moduleRoot = new GameObject("Module_" + slot.Type + "_" + slot.CenterX.ToString("F1"));
            moduleRoot.transform.SetParent(parent, false);

            Vector3 at = new Vector3(slot.CenterX, ModuleBaseY, wallZ) + rotation * Vector3.forward * 0.20f;

            moduleRoot.transform.localPosition = at;
            moduleRoot.transform.localRotation = rotation;

            float w = Mathf.Min(slot.Width, ModuleWidth);
            float h = ModuleHeight;
            float seed = slot.Seed;

            BuildModuleFrame(moduleRoot.transform, w, h, mats);

            switch (slot.Type)
            {
                case ModuleType.BlastArmor:
                    BuildBlastArmor(moduleRoot.transform, w, h, seed, mats);
                    break;
                case ModuleType.Electrical:
                    BuildElectrical(moduleRoot.transform, w, h, seed, slot.Variant, mats);
                    break;
                case ModuleType.Maintenance:
                    BuildMaintenance(moduleRoot.transform, w, h, seed, mats);
                    break;
                case ModuleType.TechPanel:
                    BuildTechPanel(moduleRoot.transform, w, h, seed, slot.Variant, mats);
                    break;
                case ModuleType.Conduits:
                    BuildConduits(moduleRoot.transform, w, h, seed, mats);
                    break;
                case ModuleType.ContainmentWindow:
                    BuildContainmentWindow(moduleRoot.transform, w, h, seed, mats);
                    break;
                case ModuleType.Vent:
                    BuildVent(moduleRoot.transform, w, h, seed, mats);
                    break;
            }

            AddGrime(moduleRoot.transform, w, h, seed, mats);
            AddScratch(moduleRoot.transform, w, h, seed, mats);
        }

        static void BuildModuleFrame(Transform parent, float w, float h, MaterialLibrary mats)
        {
            float depth = 0.16f;

            AddBox(parent, "FrameBack", new Vector3(0f, h * 0.5f, depth * 0.5f),
                    new Vector3(w, h, depth), Quaternion.identity, mats.Graphite);

            AddBox(parent, "FrameTop", new Vector3(0f, h - 0.09f, 0.055f),
                    new Vector3(w, 0.18f, 0.20f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "FrameBottom", new Vector3(0f, 0.09f, 0.055f),
                    new Vector3(w, 0.18f, 0.20f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "FrameLeft", new Vector3(-w * 0.5f + 0.075f, h * 0.5f, 0.055f),
                    new Vector3(0.15f, h, 0.20f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "FrameRight", new Vector3(w * 0.5f - 0.075f, h * 0.5f, 0.055f),
                    new Vector3(0.15f, h, 0.20f), Quaternion.identity, mats.SteelDark);

            AddBox(parent, "FrameTopLip", new Vector3(0f, h - 0.185f, 0.14f),
                    new Vector3(w - 0.24f, 0.035f, 0.06f), Quaternion.identity, mats.Steel);

            for (int i = 0; i < 4; i++)
            {
                float x = Mathf.Lerp(-w * 0.5f + 0.19f, w * 0.5f - 0.19f, i / 3f);
                AddBolt(parent, new Vector3(x, h - 0.09f, 0.158f), Quaternion.identity, mats.Titanium, 0.035f);
                AddBolt(parent, new Vector3(x, 0.09f, 0.158f), Quaternion.identity, mats.Titanium, 0.035f);
            }
        }


        // ------------------------------------------------------------------
        // Individual wall modules. Local +Z always points into the corridor.
        // ------------------------------------------------------------------

        static void BuildBlastArmor(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            float z = 0.19f;

            AddBox(parent, "ArmorPlateA", new Vector3(-w * 0.23f, h * 0.52f, z),
                    new Vector3(w * 0.46f, h * 0.80f, 0.14f), Quaternion.identity, mats.Steel);
            AddBox(parent, "ArmorPlateB", new Vector3(w * 0.23f, h * 0.52f, z),
                    new Vector3(w * 0.46f, h * 0.80f, 0.14f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "ArmorSeam", new Vector3(0f, h * 0.52f, z + 0.075f),
                    new Vector3(0.09f, h * 0.80f, 0.04f), Quaternion.identity, mats.BlackFrame);

            AddBox(parent, "ArmorTopRail", new Vector3(0f, h * 0.90f, z + 0.06f),
                    new Vector3(w * 0.88f, 0.16f, 0.10f), Quaternion.identity, mats.Titanium);
            AddBox(parent, "ArmorBottomRail", new Vector3(0f, h * 0.14f, z + 0.06f),
                    new Vector3(w * 0.88f, 0.16f, 0.10f), Quaternion.identity, mats.Titanium);

            for (int i = 0; i < 6; i++)
            {
                float x = Mathf.Lerp(-w * 0.38f, w * 0.38f, i / 5f);
                AddBolt(parent, new Vector3(x, h * 0.86f, z + 0.075f), Quaternion.identity, mats.Titanium, 0.032f);
                AddBolt(parent, new Vector3(x, h * 0.18f, z + 0.075f), Quaternion.identity, mats.Titanium, 0.032f);
            }

            AddBox(parent, "ArmorHazard", new Vector3(0f, h * 0.40f, z + 0.085f),
                    new Vector3(w * 0.72f, 0.14f, 0.012f), Quaternion.identity, mats.SignalYellow);
            AddHazardStrip(parent, new Vector3(0f, h * 0.40f, z + 0.093f), Quaternion.identity, w * 0.70f, mats);

            AddBox(parent, "ArmorLabel", new Vector3(0f, h * 0.66f, z + 0.075f),
                    new Vector3(w * 0.26f, 0.22f, 0.012f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "ArmorLabelGlow", new Vector3(0f, h * 0.66f, z + 0.082f),
                    new Vector3(w * 0.18f, 0.035f, 0.008f), Quaternion.identity, mats.CyanGlow);

            AddBox(parent, "ArmorStencil", new Vector3(-w * 0.28f, h * 0.72f, z + 0.076f),
                    new Vector3(0.32f, 0.055f, 0.010f), Quaternion.identity, mats.Grime);
            AddBox(parent, "ArmorStencil2", new Vector3(-w * 0.20f, h * 0.72f, z + 0.077f),
                    new Vector3(0.10f, 0.055f, 0.010f), Quaternion.identity, mats.Grime);
        }

        static void BuildElectrical(Transform parent, float w, float h, float seed, int variant, MaterialLibrary mats)
        {
            float z = 0.21f;
            float cabinetW = Mathf.Min(w * 0.86f, 3.15f);
            float cabinetH = h * 0.78f;
            float cabinetY = h * 0.52f;

            AddBox(parent, "Cabinet", new Vector3(0f, cabinetY, z),
                    new Vector3(cabinetW, cabinetH, 0.34f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "CabinetInset", new Vector3(0f, cabinetY, z + 0.18f),
                    new Vector3(cabinetW - 0.16f, cabinetH - 0.16f, 0.025f), Quaternion.identity, mats.Graphite);

            float doorW = (cabinetW - 0.32f) * 0.5f;
            AddBox(parent, "DoorA", new Vector3(-doorW * 0.5f - 0.04f, cabinetY, z + 0.215f),
                    new Vector3(doorW, cabinetH - 0.30f, 0.035f), Quaternion.identity, mats.Steel);
            AddBox(parent, "DoorB", new Vector3(doorW * 0.5f + 0.04f, cabinetY, z + 0.215f),
                    new Vector3(doorW, cabinetH - 0.30f, 0.035f), Quaternion.identity, mats.Steel);

            AddBox(parent, "DoorSeam", new Vector3(0f, cabinetY, z + 0.235f),
                    new Vector3(0.035f, cabinetH - 0.36f, 0.012f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "HandleA", new Vector3(-0.09f, cabinetY, z + 0.25f),
                    new Vector3(0.035f, 0.42f, 0.035f), Quaternion.identity, mats.Titanium);
            AddBox(parent, "HandleB", new Vector3(0.09f, cabinetY, z + 0.25f),
                    new Vector3(0.035f, 0.42f, 0.035f), Quaternion.identity, mats.Titanium);

            for (int i = 0; i < 5; i++)
            {
                float y = cabinetY - cabinetH * 0.28f + i * 0.085f;
                AddBox(parent, "VentSlotA", new Vector3(-cabinetW * 0.32f, y, z + 0.24f),
                        new Vector3(cabinetW * 0.16f, 0.022f, 0.012f), Quaternion.identity, mats.BlackFrame);
                AddBox(parent, "VentSlotB", new Vector3(cabinetW * 0.32f, y, z + 0.24f),
                        new Vector3(cabinetW * 0.16f, 0.022f, 0.012f), Quaternion.identity, mats.BlackFrame);
            }

            Material screen = variant == 0 ? mats.ScreenCyan : (variant == 1 ? mats.ScreenAmber : mats.ScreenDark);
            Material indicator = variant == 0 ? mats.CyanGlow : (variant == 1 ? mats.AmberGlow : mats.RedGlow);

            AddBox(parent, "StatusScreen", new Vector3(0f, cabinetY + cabinetH * 0.30f, z + 0.245f),
                    new Vector3(cabinetW * 0.34f, 0.30f, 0.018f), Quaternion.identity, screen);
            AddBox(parent, "StatusScreenFrame", new Vector3(0f, cabinetY + cabinetH * 0.30f, z + 0.24f),
                    new Vector3(cabinetW * 0.36f, 0.33f, 0.012f), Quaternion.identity, mats.BlackFrame);

            for (int i = 0; i < 4; i++)
            {
                float x = -cabinetW * 0.24f + i * cabinetW * 0.16f;
                AddBox(parent, "Indicator", new Vector3(x, cabinetY - cabinetH * 0.34f, z + 0.245f),
                        new Vector3(0.07f, 0.045f, 0.018f), Quaternion.identity,
                        (i == 0 || (variant == 1 && i == 2)) ? indicator : mats.IndicatorDark);
            }

            AddBox(parent, "CabinetFootA", new Vector3(-cabinetW * 0.38f, 0.07f, z + 0.06f),
                    new Vector3(0.20f, 0.14f, 0.42f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "CabinetFootB", new Vector3(cabinetW * 0.38f, 0.07f, z + 0.06f),
                    new Vector3(0.20f, 0.14f, 0.42f), Quaternion.identity, mats.BlackFrame);

            MatCableRun(parent, new Vector3(-cabinetW * 0.5f - 0.12f, h * 0.90f, z - 0.02f), 0.08f, 3, 0.5f, mats);
        }

        static void BuildMaintenance(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            float z = 0.18f;
            float alcoveW = Mathf.Min(w * 0.82f, 2.9f);
            float alcoveH = h * 0.62f;

            AddBox(parent, "Alcove", new Vector3(0f, h * 0.46f, z),
                    new Vector3(alcoveW, alcoveH, 0.16f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "AlcoveFloor", new Vector3(0f, h * 0.46f - alcoveH * 0.5f + 0.04f, z + 0.16f),
                    new Vector3(alcoveW - 0.10f, 0.08f, 0.34f), Quaternion.identity, mats.SteelDark);

            AddBox(parent, "BackPlate", new Vector3(0f, h * 0.50f, z + 0.055f),
                    new Vector3(alcoveW - 0.20f, alcoveH - 0.20f, 0.03f), Quaternion.identity, mats.Graphite);

            for (int i = 0; i < 4; i++)
            {
                float y = h * 0.30f + i * 0.34f;
                float pipeR = 0.055f + (i % 2) * 0.025f;
                AddCylinder(parent, "ServicePipe", new Vector3(0f, y, z + 0.13f),
                        pipeR, alcoveW - 0.30f, Quaternion.Euler(0f, 0f, 90f),
                        i % 2 == 0 ? mats.Steel : mats.Titanium, 14);

                AddBox(parent, "PipeBracket", new Vector3(-alcoveW * 0.28f, y, z + 0.10f),
                        new Vector3(0.10f, 0.18f, 0.14f), Quaternion.identity, mats.SteelDark);
                AddBox(parent, "PipeBracket2", new Vector3(alcoveW * 0.28f, y, z + 0.10f),
                        new Vector3(0.10f, 0.18f, 0.14f), Quaternion.identity, mats.SteelDark);
            }

            AddCylinder(parent, "ValveWheel", new Vector3(-alcoveW * 0.30f, h * 0.42f, z + 0.26f),
                    0.18f, 0.04f, Quaternion.Euler(90f, 0f, 0f), mats.Rust, 20);
            AddCylinder(parent, "ValveHub", new Vector3(-alcoveW * 0.30f, h * 0.42f, z + 0.29f),
                    0.055f, 0.06f, Quaternion.Euler(90f, 0f, 0f), mats.Titanium, 16);
            AddBox(parent, "ValveSpokeA", new Vector3(-alcoveW * 0.30f, h * 0.42f, z + 0.27f),
                    new Vector3(0.34f, 0.035f, 0.03f), Quaternion.identity, mats.Rust);
            AddBox(parent, "ValveSpokeB", new Vector3(-alcoveW * 0.30f, h * 0.42f, z + 0.27f),
                    new Vector3(0.035f, 0.34f, 0.03f), Quaternion.identity, mats.Rust);

            AddBox(parent, "ToolRack", new Vector3(alcoveW * 0.28f, h * 0.48f, z + 0.11f),
                    new Vector3(0.44f, 0.62f, 0.05f), Quaternion.identity, mats.SteelDark);
            for (int i = 0; i < 3; i++)
            {
                AddBox(parent, "Tool", new Vector3(alcoveW * 0.28f - 0.12f + i * 0.12f, h * 0.48f, z + 0.15f),
                        new Vector3(0.035f, 0.42f, 0.035f), Quaternion.identity, mats.Titanium);
            }

            AddBox(parent, "AlcoveLight", new Vector3(0f, h * 0.72f, z + 0.10f),
                    new Vector3(alcoveW * 0.50f, 0.035f, 0.025f), Quaternion.identity, mats.AmberGlow);
            AddBox(parent, "HazardBase", new Vector3(0f, h * 0.13f, z + 0.12f),
                    new Vector3(alcoveW, 0.18f, 0.025f), Quaternion.identity, mats.SignalYellow);
            AddHazardStrip(parent, new Vector3(0f, h * 0.13f, z + 0.135f), Quaternion.identity, alcoveW, mats);

            AddBox(parent, "GratingA", new Vector3(0f, 0.10f, z + 0.22f),
                    new Vector3(alcoveW, 0.05f, 0.34f), Quaternion.identity, mats.BlackFrame);
            for (int i = 0; i < 6; i++)
            {
                AddBox(parent, "GratingBar", new Vector3(-alcoveW * 0.42f + i * alcoveW * 0.168f, 0.13f, z + 0.40f),
                        new Vector3(0.035f, 0.02f, 0.06f), Quaternion.identity, mats.Steel);
            }
        }

        static void BuildTechPanel(Transform parent, float w, float h, float seed, int variant, MaterialLibrary mats)
        {
            float z = 0.22f;
            float panelW = Mathf.Min(w * 0.86f, 3.1f);

            AddBox(parent, "PanelBody", new Vector3(0f, h * 0.52f, z),
                    new Vector3(panelW, h * 0.74f, 0.26f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "PanelFace", new Vector3(0f, h * 0.52f, z + 0.14f),
                    new Vector3(panelW - 0.14f, h * 0.70f, 0.03f), Quaternion.identity, mats.Graphite);

            Material screen = variant == 0 ? mats.ScreenCyan : (variant == 1 ? mats.ScreenAmber : mats.ScreenCyan);
            Material glow = variant == 0 ? mats.CyanGlow : (variant == 1 ? mats.AmberGlow : mats.BlueGlow);

            AddBox(parent, "MainScreenFrame", new Vector3(-panelW * 0.19f, h * 0.62f, z + 0.165f),
                    new Vector3(panelW * 0.36f, h * 0.22f, 0.025f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "MainScreen", new Vector3(-panelW * 0.19f, h * 0.62f, z + 0.185f),
                    new Vector3(panelW * 0.32f, h * 0.18f, 0.012f), Quaternion.identity, screen);

            AddBox(parent, "SideScreenFrame", new Vector3(panelW * 0.27f, h * 0.66f, z + 0.165f),
                    new Vector3(panelW * 0.22f, h * 0.14f, 0.025f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "SideScreen", new Vector3(panelW * 0.27f, h * 0.66f, z + 0.185f),
                    new Vector3(panelW * 0.18f, h * 0.10f, 0.012f), Quaternion.identity, mats.ScreenDark);

            for (int i = 0; i < 8; i++)
            {
                float x = -panelW * 0.30f + i * panelW * 0.085f;
                AddBox(parent, "KeypadKey", new Vector3(x, h * 0.40f, z + 0.18f),
                        new Vector3(panelW * 0.045f, 0.055f, 0.018f), Quaternion.identity,
                        i % 3 == 0 ? glow : mats.IndicatorDark);
            }

            AddBox(parent, "GaugeRing", new Vector3(panelW * 0.27f, h * 0.48f, z + 0.18f),
                    new Vector3(0.24f, 0.24f, 0.02f), Quaternion.identity, mats.Titanium);
            AddCylinder(parent, "GaugeFace", new Vector3(panelW * 0.27f, h * 0.48f, z + 0.195f),
                    0.085f, 0.015f, Quaternion.Euler(90f, 0f, 0f), mats.Ceramic, 20);
            AddBox(parent, "GaugeNeedle", new Vector3(panelW * 0.27f, h * 0.48f, z + 0.208f),
                    new Vector3(0.012f, 0.075f, 0.008f), Quaternion.Euler(0f, 0f, -35f), mats.RedGlow);

            AddBox(parent, "LowerVent", new Vector3(0f, h * 0.25f, z + 0.17f),
                    new Vector3(panelW * 0.70f, 0.20f, 0.025f), Quaternion.identity, mats.BlackFrame);
            for (int i = 0; i < 5; i++)
            {
                AddBox(parent, "LowerVentSlot", new Vector3(0f, h * 0.25f - 0.06f + i * 0.03f, z + 0.19f),
                        new Vector3(panelW * 0.64f, 0.014f, 0.009f), Quaternion.identity, mats.Steel);
            }

            AddBox(parent, "PanelTopStrip", new Vector3(0f, h * 0.91f, z + 0.04f),
                    new Vector3(panelW, 0.05f, 0.05f), Quaternion.identity, glow);
            AddBox(parent, "CableTray", new Vector3(0f, h * 0.88f, z - 0.15f),
                    new Vector3(panelW * 0.92f, 0.10f, 0.22f), Quaternion.identity, mats.SteelDark);
            MatCableRun(parent, new Vector3(0f, h * 0.86f, z - 0.17f), 0.045f, 5, panelW * 0.40f, mats);
        }


        static void BuildConduits(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            float z = 0.16f;
            float innerW = Mathf.Min(w * 0.88f, 3.3f);

            AddBox(parent, "ConduitBack", new Vector3(0f, h * 0.52f, z),
                    new Vector3(innerW, h * 0.82f, 0.10f), Quaternion.identity, mats.BlackFrame);

            for (int i = 0; i < 4; i++)
            {
                float y = h * 0.24f + i * h * 0.17f;
                float radius = 0.055f + (i % 2) * 0.020f;
                AddCylinder(parent, "MainPipe", new Vector3(0f, y, z + 0.17f + i * 0.015f),
                        radius, innerW, Quaternion.Euler(0f, 0f, 90f),
                        i == 1 ? mats.Rust : (i == 3 ? mats.Titanium : mats.Steel), 16);

                for (int c = 0; c < 3; c++)
                {
                    float x = Mathf.Lerp(-innerW * 0.42f, innerW * 0.42f, c / 2f);
                    AddBox(parent, "PipeClamp", new Vector3(x, y, z + 0.155f),
                            new Vector3(0.10f, radius * 2.6f, 0.16f), Quaternion.identity, mats.SteelDark);
                    AddBolt(parent, new Vector3(x, y + radius * 0.9f, z + 0.24f), Quaternion.identity, mats.Titanium, 0.025f);
                }
            }

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -innerW * 0.32f : innerW * 0.34f;
                AddCylinder(parent, "VerticalPipe", new Vector3(x, h * 0.50f, z + 0.30f),
                        0.045f, h * 0.72f, Quaternion.identity, mats.Steel, 14);

                AddBox(parent, "JunctionBox", new Vector3(x, h * 0.33f, z + 0.31f),
                        new Vector3(0.28f, 0.34f, 0.18f), Quaternion.identity, mats.SteelDark);
                AddBox(parent, "JunctionLight", new Vector3(x, h * 0.36f, z + 0.41f),
                        new Vector3(0.12f, 0.025f, 0.012f), Quaternion.identity, mats.AmberGlow);
            }

            AddCylinder(parent, "ConduitValve", new Vector3(-innerW * 0.20f, h * 0.58f, z + 0.24f),
                    0.16f, 0.035f, Quaternion.Euler(90f, 0f, 0f), mats.Rust, 20);
            AddBox(parent, "ValveHandle", new Vector3(-innerW * 0.20f, h * 0.58f, z + 0.26f),
                    new Vector3(0.30f, 0.03f, 0.03f), Quaternion.identity, mats.Rust);

            AddBox(parent, "ConduitPlate", new Vector3(innerW * 0.30f, h * 0.78f, z + 0.16f),
                    new Vector3(0.42f, 0.20f, 0.02f), Quaternion.identity, mats.SignalYellow);
            AddWarningTriangle(parent, new Vector3(innerW * 0.30f, h * 0.78f, z + 0.175f), mats);
            AddCableDrop(parent, new Vector3(-innerW * 0.42f, h * 0.20f, z + 0.24f), 1.8f, mats);
        }

        static void BuildContainmentWindow(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            if (Rand01(seed + 4.2f) < 0.45f)
            {
                BuildObservationWindow(parent, w, h, seed, mats);
                return;
            }

            float z = 0.26f;
            float tankW = Mathf.Min(w * 0.44f, 1.45f);
            float tankH = h * 0.58f;
            float tankY = h * 0.53f;
            float tankRadius = tankW * 0.5f;

            AddBox(parent, "WindowBack", new Vector3(0f, tankY, z - 0.02f),
                    new Vector3(w * 0.88f, h * 0.78f, 0.08f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "WindowOpening", new Vector3(0f, tankY, z + 0.02f),
                    new Vector3(tankW + 0.34f, tankH + 0.34f, 0.05f), Quaternion.identity, mats.BlackFrame);

            AddCylinder(parent, "TankGlass", new Vector3(0f, tankY, z + 0.18f),
                    tankRadius, tankH, Quaternion.identity, mats.GlassGreen, 24);
            AddCylinder(parent, "TankLiquid", new Vector3(0f, tankY - 0.10f, z + 0.18f),
                    tankRadius * 0.86f, tankH * 0.78f, Quaternion.identity, mats.LiquidGreen, 24);
            AddCylinder(parent, "TankCore", new Vector3(0f, tankY - 0.10f, z + 0.18f),
                    tankRadius * 0.22f, tankH * 0.62f, Quaternion.identity, mats.GreenGlow, 16);

            AddCylinder(parent, "TankCapTop", new Vector3(0f, tankY + tankH * 0.5f, z + 0.18f),
                    tankRadius * 1.10f, 0.16f, Quaternion.identity, mats.Titanium, 24);
            AddCylinder(parent, "TankCapBottom", new Vector3(0f, tankY - tankH * 0.5f, z + 0.18f),
                    tankRadius * 1.10f, 0.16f, Quaternion.identity, mats.Titanium, 24);

            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                float x = Mathf.Cos(angle * Mathf.Deg2Rad) * tankRadius * 1.02f;
                float y = tankY + Mathf.Sin(angle * Mathf.Deg2Rad) * tankRadius * 1.02f;
                AddBox(parent, "TankBrace", new Vector3(x, y, z + 0.18f),
                        new Vector3(0.09f, tankH * 0.86f, 0.06f), Quaternion.Euler(0f, 0f, angle), mats.SteelDark);
            }

            AddCylinder(parent, "TankPipeTop", new Vector3(tankRadius * 0.8f, tankY + tankH * 0.5f + 0.22f, z + 0.12f),
                    0.045f, 0.52f, Quaternion.identity, mats.Steel, 12);
            AddCylinder(parent, "TankPipeBottom", new Vector3(-tankRadius * 0.8f, tankY - tankH * 0.5f - 0.22f, z + 0.12f),
                    0.045f, 0.52f, Quaternion.identity, mats.Steel, 12);

            for (int i = 0; i < 7; i++)
            {
                float bx = Mathf.Sin(seed + i * 1.7f) * tankRadius * 0.45f;
                float by = tankY - tankH * 0.28f + ((i * 0.37f + seed) % 1f) * tankH * 0.48f;
                AddSphere(parent, "Bubble", new Vector3(bx, by, z + 0.18f + tankRadius * 0.65f),
                        0.018f + (i % 3) * 0.008f, mats.WhiteGlow);
            }

            AddBox(parent, "WindowHazard", new Vector3(-tankW * 0.5f - 0.42f, tankY, z + 0.04f),
                    new Vector3(0.16f, tankH, 0.03f), Quaternion.identity, mats.SignalYellow);
            AddHazardStrip(parent, new Vector3(-tankW * 0.5f - 0.42f, tankY, z + 0.058f), Quaternion.Euler(0f, 0f, 90f), tankH, mats);

            AddBox(parent, "WindowLabel", new Vector3(tankW * 0.5f + 0.36f, tankY + 0.10f, z + 0.05f),
                    new Vector3(0.34f, 0.24f, 0.03f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "WindowLabelGlow", new Vector3(tankW * 0.5f + 0.36f, tankY + 0.10f, z + 0.068f),
                    new Vector3(0.22f, 0.045f, 0.012f), Quaternion.identity, mats.GreenGlow);

            AddBox(parent, "WindowSill", new Vector3(0f, tankY - tankH * 0.5f - 0.26f, z + 0.10f),
                    new Vector3(w * 0.84f, 0.10f, 0.34f), Quaternion.identity, mats.Titanium);
            AddBox(parent, "WindowCyanStrip", new Vector3(0f, tankY + tankH * 0.5f + 0.24f, z + 0.12f),
                    new Vector3(w * 0.72f, 0.035f, 0.03f), Quaternion.identity, mats.CyanGlow);
        }

        static void BuildObservationWindow(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            float z = 0.22f;
            float winW = Mathf.Min(w * 0.76f, 2.95f);
            float winH = h * 0.50f;
            float winY = h * 0.56f;

            AddBox(parent, "WindowBackwall", new Vector3(0f, winY, z - 0.05f),
                    new Vector3(w * 0.90f, h * 0.80f, 0.10f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "WindowVoid", new Vector3(0f, winY, z + 0.035f),
                    new Vector3(winW, winH, 0.035f), Quaternion.identity, mats.ScreenDark);

            AddBox(parent, "WindowHorizon", new Vector3(0f, winY - winH * 0.20f, z + 0.055f),
                    new Vector3(winW * 0.96f, 0.018f, 0.012f), Quaternion.identity, mats.BlueGlow);

            for (int i = 0; i < 16; i++)
            {
                float sx = (Rand01(seed + i * 3.1f) - 0.5f) * winW * 0.86f;
                float sy = winY + (Rand01(seed + i * 7.7f) - 0.5f) * winH * 0.84f;
                float r = 0.008f + Rand01(seed + i * 2.3f) * 0.014f;
                AddSphere(parent, "WindowStar", new Vector3(sx, sy, z + 0.062f), r,
                        i % 4 == 0 ? mats.CyanGlow : mats.WhiteGlow);
            }

            AddBox(parent, "WindowGlass", new Vector3(0f, winY, z + 0.16f),
                    new Vector3(winW, winH, 0.025f), Quaternion.identity, mats.GlassTint);

            AddBox(parent, "WindowFrameTop", new Vector3(0f, winY + winH * 0.5f + 0.09f, z + 0.12f),
                    new Vector3(winW + 0.36f, 0.18f, 0.30f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "WindowFrameBottom", new Vector3(0f, winY - winH * 0.5f - 0.09f, z + 0.12f),
                    new Vector3(winW + 0.36f, 0.18f, 0.30f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "WindowFrameLeft", new Vector3(-winW * 0.5f - 0.09f, winY, z + 0.12f),
                    new Vector3(0.18f, winH + 0.36f, 0.30f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "WindowFrameRight", new Vector3(winW * 0.5f + 0.09f, winY, z + 0.12f),
                    new Vector3(0.18f, winH + 0.36f, 0.30f), Quaternion.identity, mats.SteelDark);

            AddBox(parent, "WindowMullionV", new Vector3(0f, winY, z + 0.19f),
                    new Vector3(0.10f, winH, 0.08f), Quaternion.identity, mats.Titanium);
            AddBox(parent, "WindowMullionH", new Vector3(0f, winY, z + 0.19f),
                    new Vector3(winW, 0.09f, 0.08f), Quaternion.identity, mats.Titanium);

            AddBox(parent, "WindowSill", new Vector3(0f, winY - winH * 0.5f - 0.24f, z + 0.20f),
                    new Vector3(winW + 0.52f, 0.12f, 0.44f), Quaternion.identity, mats.Titanium);
            AddBox(parent, "WindowCyanBar", new Vector3(0f, winY + winH * 0.5f + 0.22f, z + 0.20f),
                    new Vector3(winW * 0.82f, 0.045f, 0.05f), Quaternion.identity, mats.CyanGlow);

            AddBox(parent, "WindowPlate", new Vector3(-winW * 0.5f - 0.34f, winY + winH * 0.10f, z + 0.05f),
                    new Vector3(0.26f, 0.42f, 0.04f), Quaternion.identity, mats.SignalYellow);
            AddWarningTriangle(parent, new Vector3(-winW * 0.5f - 0.34f, winY + winH * 0.10f, z + 0.075f), mats);
            AddBox(parent, "WindowLabel", new Vector3(winW * 0.5f + 0.36f, winY - winH * 0.08f, z + 0.05f),
                    new Vector3(0.32f, 0.24f, 0.04f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "WindowLabelGlow", new Vector3(winW * 0.5f + 0.36f, winY - winH * 0.08f, z + 0.075f),
                    new Vector3(0.20f, 0.035f, 0.012f), Quaternion.identity, mats.CyanGlow);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (winW * 0.5f + 0.09f);
                float y = winY + (i < 2 ? -1f : 1f) * (winH * 0.5f + 0.09f);
                AddBolt(parent, new Vector3(x, y, z + 0.27f), Quaternion.identity, mats.Titanium, 0.035f);
            }

            AddBox(parent, "WindowLowerHazard", new Vector3(0f, winY - winH * 0.5f - 0.40f, z + 0.20f),
                    new Vector3(winW * 0.90f, 0.14f, 0.03f), Quaternion.identity, mats.SignalYellow);
            AddHazardStrip(parent, new Vector3(0f, winY - winH * 0.5f - 0.40f, z + 0.225f),
                    Quaternion.identity, winW * 0.88f, mats);
        }

        static void BuildVent(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            float z = 0.18f;
            float ventW = Mathf.Min(w * 0.72f, 2.5f);
            float ventH = h * 0.52f;
            float ventY = h * 0.52f;

            AddBox(parent, "VentHousing", new Vector3(0f, ventY, z),
                    new Vector3(ventW + 0.24f, ventH + 0.24f, 0.28f), Quaternion.identity, mats.SteelDark);
            AddBox(parent, "VentRecess", new Vector3(0f, ventY, z + 0.15f),
                    new Vector3(ventW, ventH, 0.04f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "VentBack", new Vector3(0f, ventY, z + 0.06f),
                    new Vector3(ventW - 0.10f, ventH - 0.10f, 0.03f), Quaternion.identity, mats.Graphite);

            int slats = 9;
            for (int i = 0; i < slats; i++)
            {
                float y = ventY - ventH * 0.42f + i * (ventH * 0.84f / (slats - 1));
                AddBox(parent, "VentSlat", new Vector3(0f, y, z + 0.19f),
                        new Vector3(ventW - 0.08f, 0.045f, 0.10f), Quaternion.Euler(28f, 0f, 0f), mats.Steel);
            }

            AddCylinder(parent, "VentFanHub", new Vector3(0f, ventY, z + 0.15f),
                    0.09f, 0.06f, Quaternion.Euler(90f, 0f, 0f), mats.Titanium, 16);
            for (int i = 0; i < 4; i++)
            {
                AddBox(parent, "FanBlade", new Vector3(0f, ventY, z + 0.15f),
                        new Vector3(ventW * 0.32f, 0.10f, 0.025f),
                        Quaternion.Euler(0f, 0f, i * 45f + 20f), mats.BlackFrame);
            }

            AddBox(parent, "VentTopGrille", new Vector3(0f, ventY + ventH * 0.5f + 0.20f, z + 0.22f),
                    new Vector3(ventW, 0.10f, 0.20f), Quaternion.Euler(-35f, 0f, 0f), mats.Titanium);
            AddBox(parent, "VentBottomGrille", new Vector3(0f, ventY - ventH * 0.5f - 0.20f, z + 0.22f),
                    new Vector3(ventW, 0.10f, 0.20f), Quaternion.Euler(35f, 0f, 0f), mats.Titanium);

            AddBox(parent, "VentWarning", new Vector3(-ventW * 0.5f - 0.30f, ventY, z + 0.02f),
                    new Vector3(0.22f, 0.30f, 0.03f), Quaternion.identity, mats.SignalYellow);
            AddWarningTriangle(parent, new Vector3(-ventW * 0.5f - 0.30f, ventY, z + 0.045f), mats);
            AddBox(parent, "VentIndicator", new Vector3(ventW * 0.5f + 0.26f, ventY + 0.20f, z + 0.03f),
                    new Vector3(0.08f, 0.05f, 0.02f), Quaternion.identity, mats.CyanGlow);

            AddCylinder(parent, "VentDuct", new Vector3(0f, ventY - ventH * 0.5f - 0.50f, z + 0.18f),
                    0.22f, 0.90f, Quaternion.identity, mats.Steel, 16);
            AddBox(parent, "VentDuctBracket", new Vector3(0f, ventY - ventH * 0.5f - 0.86f, z + 0.18f),
                    new Vector3(0.62f, 0.10f, 0.62f), Quaternion.identity, mats.SteelDark);
        }

        static void BuildWallPipes(Transform parent, ZoneInfo zone, int side, float wallZ, Quaternion rotation, MaterialLibrary mats)
        {
            float length = zone.Length;
            float centerX = zone.Mid;
            Vector3 inward = rotation * Vector3.forward;

            float[] heights = { 5.45f, 5.25f, 5.05f };
            float[] radii = { 0.075f, 0.055f, 0.045f };

            for (int i = 0; i < heights.Length; i++)
            {
                Vector3 p = new Vector3(centerX, heights[i], wallZ) + inward * (0.20f + i * 0.03f);
                AddCylinder(parent, "CeilingPipe", p, radii[i], length, rotation * Quaternion.Euler(0f, 0f, 90f),
                        i == 0 ? mats.Rust : mats.Steel, 14);
            }

            for (float x = zone.Start + 1.0f; x < zone.End - 0.6f; x += 4.6f)
            {
                float offset = Hash(side, Mathf.RoundToInt(x), zone.Index) * 0.15f;

                AddCylinder(parent, "DropPipe", new Vector3(x + offset, 3.05f, wallZ + inward.z * 0.22f),
                        0.045f, 4.7f, Quaternion.identity, mats.Steel, 12);
                AddBox(parent, "DropBracket", new Vector3(x + offset, 5.50f, wallZ + inward.z * 0.16f),
                        new Vector3(0.12f, 0.10f, 0.30f), rotation, mats.SteelDark);
                AddBox(parent, "DropBracket2", new Vector3(x + offset, 0.85f, wallZ + inward.z * 0.16f),
                        new Vector3(0.12f, 0.10f, 0.30f), rotation, mats.SteelDark);

                AddCylinder(parent, "Elbow", new Vector3(x + offset, 0.72f, wallZ + inward.z * 0.30f),
                        0.055f, 0.18f, rotation * Quaternion.Euler(90f, 0f, 0f), mats.Rust, 12);
            }

            AddBox(parent, "PipeRouteSign", new Vector3(zone.Start + 2.2f, 5.34f, wallZ + inward.z * 0.40f),
                    new Vector3(0.52f, 0.24f, 0.03f), rotation, mats.BlackFrame);
            AddBox(parent, "PipeRouteSignGlow", new Vector3(zone.Start + 2.2f, 5.34f, wallZ + inward.z * 0.42f),
                    new Vector3(0.34f, 0.035f, 0.012f), rotation, mats.AmberGlow);

            AddCableRun(parent, new Vector3(zone.Start + 0.8f, 5.62f, wallZ + inward.z * 0.16f),
                    rotation, length - 1.6f, mats.BlackFrame, mats.Rubber);
        }


        static void BuildCeiling(Transform parent, ZoneInfo zone, MaterialLibrary mats)
        {
            var ceilingRoot = new GameObject("Ceiling");
            ceilingRoot.transform.SetParent(parent, false);

            float length = zone.Length;
            float half = zone.HalfWidth;

            float beamSpacing = 4.4f;
            int beams = Mathf.Max(2, Mathf.CeilToInt(length / beamSpacing) + 1);
            for (int i = 0; i < beams; i++)
            {
                float t = beams == 1 ? 0.5f : i / (float)(beams - 1);
                float x = Mathf.Lerp(zone.Start + 0.25f, zone.End - 0.25f, t);

                AddBox(ceilingRoot.transform, "CeilingBeam", new Vector3(x, CeilingUnder - 0.16f, 0f),
                        new Vector3(0.22f, 0.32f, half * 2f - 0.30f), Quaternion.identity, mats.SteelDark);
                AddBox(ceilingRoot.transform, "BeamLight", new Vector3(x + 0.18f, CeilingUnder - 0.32f, 0f),
                        new Vector3(0.035f, 0.035f, half * 1.2f), Quaternion.identity,
                        (i % 3 == 0) ? mats.CyanGlow : mats.WhiteGlow);

                AddBox(ceilingRoot.transform, "BeamGussetA", new Vector3(x, CeilingUnder - 0.40f, -half + 1.0f),
                        new Vector3(0.28f, 0.24f, 0.60f), Quaternion.identity, mats.SteelDark);
                AddBox(ceilingRoot.transform, "BeamGussetB", new Vector3(x, CeilingUnder - 0.40f, half - 1.0f),
                        new Vector3(0.28f, 0.24f, 0.60f), Quaternion.identity, mats.SteelDark);
            }

            AddBox(ceilingRoot.transform, "MainDuct", new Vector3(zone.Mid, CeilingUnder - 0.62f, 0f),
                    new Vector3(length - 0.60f, 0.58f, 1.10f), Quaternion.identity, mats.Steel);
            AddBox(ceilingRoot.transform, "MainDuctTop", new Vector3(zone.Mid, CeilingUnder - 0.30f, 0f),
                    new Vector3(length - 0.60f, 0.06f, 1.24f), Quaternion.identity, mats.SteelDark);

            for (float x = zone.Start + 1.4f; x < zone.End - 1.0f; x += 5.2f)
            {
                AddBox(ceilingRoot.transform, "DuctStrap", new Vector3(x, CeilingUnder - 0.64f, 0f),
                        new Vector3(0.14f, 0.74f, 1.26f), Quaternion.identity, mats.SteelDark);
                AddBox(ceilingRoot.transform, "DuctVent", new Vector3(x + 2.0f, CeilingUnder - 0.92f, 0f),
                        new Vector3(0.90f, 0.035f, 0.62f), Quaternion.identity, mats.BlackFrame);
            }

            AddBox(ceilingRoot.transform, "CableTrayL", new Vector3(zone.Mid, CeilingUnder - 0.46f, -half + 1.55f),
                    new Vector3(length - 1.2f, 0.12f, 0.46f), Quaternion.identity, mats.BlackFrame);
            AddBox(ceilingRoot.transform, "CableTrayR", new Vector3(zone.Mid, CeilingUnder - 0.46f, half - 1.55f),
                    new Vector3(length - 1.2f, 0.12f, 0.46f), Quaternion.identity, mats.BlackFrame);

            AddCableRun(ceilingRoot.transform, new Vector3(zone.Start + 0.7f, CeilingUnder - 0.52f, -half + 1.55f),
                    Quaternion.identity, length - 1.4f, mats.Rubber, mats.Steel);
            AddCableRun(ceilingRoot.transform, new Vector3(zone.Start + 0.7f, CeilingUnder - 0.52f, half - 1.55f),
                    Quaternion.identity, length - 1.4f, mats.Rubber, mats.Steel);

            int fixtureCount = Mathf.Max(2, Mathf.RoundToInt(length / 7.5f));
            for (int i = 0; i < fixtureCount; i++)
            {
                float t = fixtureCount == 1 ? 0.5f : (i + 0.5f) / fixtureCount;
                float x = Mathf.Lerp(zone.Start + 2.2f, zone.End - 2.2f, t);

                AddBox(ceilingRoot.transform, "LightHousing", new Vector3(x, CeilingUnder - 0.42f, 0f),
                        new Vector3(1.35f, 0.22f, 0.52f), Quaternion.identity, mats.BlackFrame);
                AddBox(ceilingRoot.transform, "LightTube", new Vector3(x, CeilingUnder - 0.55f, 0f),
                        new Vector3(1.10f, 0.05f, 0.30f), Quaternion.identity,
                        zone.Index == 3 ? mats.GreenGlow : (zone.Index == 2 ? mats.AmberGlow : mats.WhiteGlow));
            }

            AddBox(ceilingRoot.transform, "SkipLightA", new Vector3(zone.Start + 3.2f, CeilingUnder - 0.55f, -half + 1.55f),
                    new Vector3(0.85f, 0.045f, 0.14f), Quaternion.identity, mats.CyanGlow);
            AddBox(ceilingRoot.transform, "SkipLightB", new Vector3(zone.End - 4.0f, CeilingUnder - 0.55f, half - 1.55f),
                    new Vector3(0.85f, 0.045f, 0.14f), Quaternion.identity, mats.CyanGlow);

            for (int i = 0; i < 3; i++)
            {
                float x = Mathf.Lerp(zone.Start + 2.0f, zone.End - 2.0f, (i + 0.5f) / 3f);
                AddBox(ceilingRoot.transform, "CeilingVent", new Vector3(x, CeilingUnder - 0.06f, (i % 2 == 0 ? -1f : 1f) * half * 0.52f),
                        new Vector3(1.1f, 0.05f, 0.7f), Quaternion.identity, mats.BlackFrame);
                for (int s = 0; s < 4; s++)
                {
                    AddBox(ceilingRoot.transform, "CeilingVentSlat",
                            new Vector3(x, CeilingUnder - 0.085f, (i % 2 == 0 ? -1f : 1f) * half * 0.52f - 0.24f + s * 0.16f),
                            new Vector3(0.94f, 0.018f, 0.055f), Quaternion.identity, mats.Steel);
                }
            }
        }

        static void BuildFloorDressing(Transform parent, ZoneInfo zone, MaterialLibrary mats)
        {
            var floorRoot = new GameObject("Floor");
            floorRoot.transform.SetParent(parent, false);

            float length = zone.Length;
            float half = zone.HalfWidth;

            AddBox(floorRoot.transform, "FloorWalkway", new Vector3(zone.Mid, 0.010f, 0f),
                    new Vector3(length - 0.8f, 0.020f, 3.2f), Quaternion.identity, mats.Graphite);
            AddBox(floorRoot.transform, "WalkwayEdgeL", new Vector3(zone.Mid, 0.022f, -1.66f),
                    new Vector3(length - 0.8f, 0.020f, 0.08f), Quaternion.identity, mats.SignalYellow);
            AddBox(floorRoot.transform, "WalkwayEdgeR", new Vector3(zone.Mid, 0.022f, 1.66f),
                    new Vector3(length - 0.8f, 0.020f, 0.08f), Quaternion.identity, mats.SignalYellow);

            for (float x = zone.Start + 2.0f; x < zone.End - 1.4f; x += 7.0f)
            {
                AddBox(floorRoot.transform, "FloorPlate", new Vector3(x, 0.030f, -half * 0.62f),
                        new Vector3(2.4f, 0.025f, 1.5f), Quaternion.identity, mats.SteelDark);
                AddBox(floorRoot.transform, "FloorPlateEdge", new Vector3(x, 0.045f, -half * 0.62f),
                        new Vector3(2.3f, 0.010f, 1.4f), Quaternion.identity, mats.Graphite);

                AddBox(floorRoot.transform, "DrainFrame", new Vector3(x + 3.2f, 0.032f, half * 0.52f),
                        new Vector3(0.86f, 0.020f, 0.62f), Quaternion.identity, mats.BlackFrame);
                for (int i = 0; i < 5; i++)
                {
                    AddBox(floorRoot.transform, "DrainBar", new Vector3(x + 3.2f, 0.045f, half * 0.52f - 0.24f + i * 0.12f),
                            new Vector3(0.70f, 0.012f, 0.035f), Quaternion.identity, mats.Steel);
                }
            }

            AddBox(floorRoot.transform, "FloorCenterLine", new Vector3(zone.Mid, 0.024f, 0f),
                    new Vector3(length - 1.2f, 0.008f, 0.055f), Quaternion.identity, mats.Steel);

            for (int i = 0; i < 14; i++)
            {
                float rx = zone.Start + 1.0f + Rand01(zone.Index * 31f + i * 7.3f) * (length - 2.0f);
                float rz = (Rand01(zone.Index * 17f + i * 3.7f) - 0.5f) * (half - 1.2f) * 1.6f;
                float s = 0.05f + Rand01(i * 5.1f + zone.Index) * 0.10f;

                AddBox(floorRoot.transform, "FloorDebris", new Vector3(rx, 0.045f + s * 0.3f, rz),
                        new Vector3(s, s * 0.35f, s * 0.65f),
                        Quaternion.Euler(0f, Rand01(i * 9.7f) * 180f, Rand01(i * 4.3f) * 28f),
                        i % 4 == 0 ? mats.Titanium : mats.SteelDark);
            }

            if (zone.Index == 1 || zone.Index == 3)
            {
                AddBox(floorRoot.transform, "Spill", new Vector3(zone.Mid + 3.4f, 0.036f, half * 0.36f),
                        new Vector3(2.2f, 0.012f, 1.1f), Quaternion.Euler(0f, 18f, 0f), mats.LiquidCyan);
                AddBox(floorRoot.transform, "SpillEdge", new Vector3(zone.Mid + 3.4f, 0.044f, half * 0.36f),
                        new Vector3(1.5f, 0.008f, 0.5f), Quaternion.Euler(0f, 18f, 0f), mats.LiquidGreen);
            }

            AddBox(floorRoot.transform, "ThresholdMain", new Vector3(zone.End - 0.35f, 0.040f, 0f),
                    new Vector3(0.30f, 0.035f, half * 1.7f), Quaternion.identity, mats.SignalYellow);
            AddBox(floorRoot.transform, "ThresholdInset", new Vector3(zone.End - 0.35f, 0.060f, 0f),
                    new Vector3(0.14f, 0.012f, half * 1.6f), Quaternion.identity, mats.BlackFrame);
        }

        static void BuildZoneEquipment(Transform parent, ZoneInfo zone, MaterialLibrary mats)
        {
            var equipmentRoot = new GameObject("Equipment");
            equipmentRoot.transform.SetParent(parent, false);

            float hw = zone.HalfWidth;

            switch (zone.Index)
            {
                case 0:
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 3.4f, 0f, -hw + 1.5f), 1, mats);
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 6.6f, 0f, hw - 1.6f), 2, mats);
                    AddTerminalPillar(equipmentRoot.transform, new Vector3(zone.Start + 10.2f, 0f, -hw + 1.35f), mats, mats.CyanGlow);
                    AddBarrelCluster(equipmentRoot.transform, new Vector3(zone.Start + 14.2f, 0f, hw - 1.5f), 4, mats);
                    break;

                case 1:
                    AddLabBench(equipmentRoot.transform, new Vector3(zone.Start + 5.0f, 0f, -hw + 2.2f), 3.6f, mats);
                    AddLabBench(equipmentRoot.transform, new Vector3(zone.Start + 13.0f, 0f, hw - 2.2f), 3.0f, mats);
                    AddSpecimenPod(equipmentRoot.transform, new Vector3(zone.Start + 22.0f, 0f, -hw + 2.0f), mats);
                    AddSpecimenPod(equipmentRoot.transform, new Vector3(zone.Start + 26.5f, 0f, -hw + 2.0f), mats);
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 33.0f, 0f, hw - 1.7f), 2, mats);
                    AddTerminalPillar(equipmentRoot.transform, new Vector3(zone.Start + 36.0f, 0f, -hw + 1.35f), mats, mats.GreenGlow);
                    break;

                case 2:
                    AddDeployableBarrier(equipmentRoot.transform, new Vector3(zone.Start + 8.0f, 0f, -hw + 2.0f), 0f, mats);
                    AddDeployableBarrier(equipmentRoot.transform, new Vector3(zone.Start + 16.0f, 0f, hw - 2.0f), 180f, mats);
                    AddDeployableBarrier(equipmentRoot.transform, new Vector3(zone.Start + 28.0f, 0f, -hw + 2.0f), 0f, mats);
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 22.0f, 0f, hw - 1.8f), 3, mats);
                    AddBarrelCluster(equipmentRoot.transform, new Vector3(zone.Start + 33.0f, 0f, hw - 1.5f), 5, mats);
                    AddTerminalPillar(equipmentRoot.transform, new Vector3(zone.Start + 12.0f, 0f, hw - 1.35f), mats, mats.AmberGlow);
                    AddTerminalPillar(equipmentRoot.transform, new Vector3(zone.Start + 30.0f, 0f, -hw + 1.35f), mats, mats.RedGlow);
                    break;

                case 3:
                    AddContainmentFrame(equipmentRoot.transform, new Vector3(zone.Start + 10.0f, 0f, -hw + 3.0f), mats);
                    AddContainmentFrame(equipmentRoot.transform, new Vector3(zone.Start + 10.0f, 0f, hw - 3.0f), mats);
                    AddGenerator(equipmentRoot.transform, new Vector3(zone.Start + 18.0f, 0f, -hw + 3.4f), mats);
                    AddGenerator(equipmentRoot.transform, new Vector3(zone.Start + 32.0f, 0f, hw - 3.4f), mats);
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 14.0f, 0f, hw - 2.0f), 2, mats);
                    AddBarrelCluster(equipmentRoot.transform, new Vector3(zone.Start + 31.0f, 0f, -hw + 2.0f), 6, mats);
                    break;

                case 4:
                    AddDecontaminationGate(equipmentRoot.transform, new Vector3(zone.Start + 5.5f, 0f, 0f), mats);
                    AddTerminalPillar(equipmentRoot.transform, new Vector3(zone.Start + 12.0f, 0f, -hw + 1.35f), mats, mats.GreenGlow);
                    AddCrateStack(equipmentRoot.transform, new Vector3(zone.Start + 18.0f, 0f, hw - 1.8f), 1, mats);
                    AddBarrelCluster(equipmentRoot.transform, new Vector3(zone.Start + 22.0f, 0f, -hw + 1.6f), 3, mats);
                    break;
            }
        }

        static void BuildZoneSignage(Transform parent, ZoneInfo zone, MaterialLibrary mats)
        {
            var signRoot = new GameObject("Signage");
            signRoot.transform.SetParent(parent, false);

            SignType primary = zone.Index == 0 ? SignType.Restricted
                : zone.Index == 1 ? SignType.Research
                : zone.Index == 2 ? SignType.Biohazard
                : zone.Index == 3 ? SignType.Containment
                : SignType.Exit;

            AddHangingSign(signRoot.transform, new Vector3(zone.Start + 1.6f, 5.45f, -2.0f), primary, mats);
            AddHangingSign(signRoot.transform, new Vector3(zone.Start + 1.6f, 5.45f, 2.0f), SignType.Compartment, mats);

            AddWallSign(signRoot.transform, new Vector3(zone.Mid, 4.55f, -zone.WallZ + 0.18f), primary, mats);
            AddWallSign(signRoot.transform, new Vector3(zone.Mid, 4.55f, zone.WallZ - 0.18f), SignType.HighVoltage, mats);

            AddFloorStencil(signRoot.transform, new Vector3(zone.Start + zone.Length * 0.55f, 0.030f, 0f), primary, mats);
        }

        static void BuildZoneLighting(Transform parent, ZoneInfo zone, MaterialLibrary mats)
        {
            var lightRoot = new GameObject("Lighting");
            lightRoot.transform.SetParent(parent, false);

            Color primary = zone.Index == 0 ? ColCyan
                : zone.Index == 1 ? new Color(0.35f, 0.85f, 1f)
                : zone.Index == 2 ? ColAmber
                : zone.Index == 3 ? ColGreen
                : ColColdWhite;

            Color secondary = zone.Index == 3 ? ColGreen : (zone.Index == 2 ? ColRed : ColBlue);

            int count = Mathf.Max(2, Mathf.RoundToInt(zone.Length / 9f));
            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float x = Mathf.Lerp(zone.Start + 2.0f, zone.End - 2.0f, t);

                AddPointLight(lightRoot.transform, new Vector3(x, 5.9f, 0f), primary, 1.35f, 11f);
                AddPointLight(lightRoot.transform, new Vector3(x + 2.2f, 4.9f, (i % 2 == 0 ? -1f : 1f) * zone.HalfWidth * 0.62f),
                        secondary, 0.85f, 8f);
            }

            AddSpotLight(lightRoot.transform, new Vector3(zone.Start + 0.9f, 4.2f, 0f), primary, 2.2f, 14f);
            AddSpotLight(lightRoot.transform, new Vector3(zone.End - 1.2f, 4.2f, 0f), secondary, 1.7f, 12f);
        }

        static void BuildConnectingDressing(Transform decorRoot, MaterialLibrary mats)
        {
            var transitions = new GameObject("Zone_Transitions");
            transitions.transform.SetParent(decorRoot, false);

            float[] xs = { 20f, 60f, 100f, 140f };
            float[] halfWidths = { 10f, 10f, 15f, 10f };
            for (int i = 0; i < xs.Length; i++)
            {
                float x = xs[i];
                float zEdge = halfWidths[i] - 1.8f;
                float beamLength = zEdge * 2f + 0.55f;
                float lightLength = Mathf.Max(4f, zEdge * 2f - 4f);

                AddBox(transitions.transform, "BulkheadColumnL", new Vector3(x, 3.0f, -zEdge),
                        new Vector3(0.55f, 6.0f, 0.55f), Quaternion.identity, mats.SteelDark);
                AddBox(transitions.transform, "BulkheadColumnR", new Vector3(x, 3.0f, zEdge),
                        new Vector3(0.55f, 6.0f, 0.55f), Quaternion.identity, mats.SteelDark);
                AddBox(transitions.transform, "BulkheadBeam", new Vector3(x, 5.95f, 0f),
                        new Vector3(0.55f, 0.55f, beamLength), Quaternion.identity, mats.SteelDark);
                AddBox(transitions.transform, "BulkheadBeamLight", new Vector3(x - 0.30f, 5.72f, 0f),
                        new Vector3(0.04f, 0.04f, lightLength), Quaternion.identity, mats.CyanGlow);
                AddBox(transitions.transform, "BulkheadHazardL", new Vector3(x - 0.30f, 2.0f, -zEdge),
                        new Vector3(0.04f, 1.4f, 0.30f), Quaternion.identity, mats.SignalYellow);
                AddBox(transitions.transform, "BulkheadHazardR", new Vector3(x - 0.30f, 2.0f, zEdge),
                        new Vector3(0.04f, 1.4f, 0.30f), Quaternion.identity, mats.SignalYellow);
                AddHazardStrip(transitions.transform, new Vector3(x - 0.33f, 2.0f, -zEdge),
                        Quaternion.Euler(0f, 90f, 90f), 1.3f, mats);
                AddHazardStrip(transitions.transform, new Vector3(x - 0.33f, 2.0f, zEdge),
                        Quaternion.Euler(0f, 90f, 90f), 1.3f, mats);

                AddBox(transitions.transform, "ThresholdLight", new Vector3(x, 0.035f, 0f),
                        new Vector3(0.22f, 0.02f, zEdge * 2f - 3.0f), Quaternion.identity, mats.CyanGlow);

                AddHangingSign(transitions.transform, new Vector3(x + 0.35f, 5.0f, 0f),
                        i == 0 ? SignType.Research : (i == 2 ? SignType.Containment : SignType.Compartment), mats);
            }

            AddBox(transitions.transform, "EntryGateTop", new Vector3(0.45f, 5.85f, 0f),
                    new Vector3(0.45f, 0.45f, 17.0f), Quaternion.identity, mats.SteelDark);
            AddBox(transitions.transform, "EntryGateLight", new Vector3(0.68f, 5.60f, 0f),
                    new Vector3(0.04f, 0.04f, 12.0f), Quaternion.identity, mats.GreenGlow);
            AddBox(transitions.transform, "ExitGateTop", new Vector3(169.3f, 5.85f, 0f),
                    new Vector3(0.45f, 0.45f, 17.0f), Quaternion.identity, mats.SteelDark);
            AddBox(transitions.transform, "ExitGateLight", new Vector3(169.05f, 5.60f, 0f),
                    new Vector3(0.04f, 0.04f, 12.0f), Quaternion.identity, mats.GreenGlow);
        }


        // ------------------------------------------------------------------
        // Free-standing equipment. All pieces stay close to the walls so the
        // central route remains clear.
        // ------------------------------------------------------------------

        static void AddCrateStack(Transform parent, Vector3 basePos, int variant, MaterialLibrary mats)
        {
            var root = new GameObject("CrateStack");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;
            root.transform.localRotation = Quaternion.Euler(0f, variant * 23f, 0f);

            int count = 2 + variant;
            for (int i = 0; i < count; i++)
            {
                float w = 1.0f - (i % 2) * 0.16f;
                float h = 0.72f;
                float d = 0.78f - (i % 3) * 0.06f;
                float x = (i % 2 == 0 ? -0.12f : 0.34f);
                float z = (i % 2 == 0 ? 0.05f : -0.28f);
                float y = 0.08f + i * 0.76f;

                AddBox(root.transform, "Crate", new Vector3(x, y + h * 0.5f, z),
                        new Vector3(w, h, d), Quaternion.Euler(0f, (i * 7f) % 5f, 0f),
                        i % 2 == 0 ? mats.SteelDark : mats.Graphite);
                AddBox(root.transform, "CrateBand", new Vector3(x, y + h * 0.5f, z),
                        new Vector3(w + 0.03f, 0.10f, d + 0.03f), Quaternion.identity, mats.SignalYellow);
                AddBox(root.transform, "CrateLatch", new Vector3(x, y + h * 0.55f, z + d * 0.52f),
                        new Vector3(0.16f, 0.12f, 0.035f), Quaternion.identity, mats.Titanium);
                AddBox(root.transform, "CrateLabel", new Vector3(x - w * 0.22f, y + h * 0.72f, z + d * 0.52f),
                        new Vector3(0.28f, 0.10f, 0.012f), Quaternion.identity, mats.Grime);
            }

            AddBox(root.transform, "CratePallet", new Vector3(0.1f, 0.04f, -0.10f),
                    new Vector3(1.35f, 0.08f, 1.0f), Quaternion.identity, mats.BlackFrame);
        }

        static void AddTerminalPillar(Transform parent, Vector3 basePos, MaterialLibrary mats, Material glow)
        {
            var root = new GameObject("TerminalPillar");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            AddBox(root.transform, "PillarBase", new Vector3(0f, 0.10f, 0f),
                    new Vector3(0.82f, 0.20f, 0.72f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "PillarBody", new Vector3(0f, 0.95f, 0f),
                    new Vector3(0.55f, 1.70f, 0.48f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "PillarFace", new Vector3(0f, 1.20f, 0.26f),
                    new Vector3(0.42f, 0.62f, 0.035f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "PillarScreen", new Vector3(0f, 1.20f, 0.285f),
                    new Vector3(0.36f, 0.52f, 0.012f), Quaternion.identity, glow);
            AddBox(root.transform, "PillarKeypad", new Vector3(0f, 0.80f, 0.265f),
                    new Vector3(0.34f, 0.14f, 0.018f), Quaternion.identity, mats.IndicatorDark);

            for (int i = 0; i < 3; i++)
            {
                AddBox(root.transform, "PillarKey", new Vector3(-0.10f + i * 0.10f, 0.80f, 0.278f),
                        new Vector3(0.06f, 0.045f, 0.010f), Quaternion.identity, i == 0 ? glow : mats.Titanium);
            }

            AddBox(root.transform, "PillarTop", new Vector3(0f, 1.88f, 0f),
                    new Vector3(0.62f, 0.08f, 0.52f), Quaternion.identity, mats.Titanium);
            AddBox(root.transform, "PillarLight", new Vector3(0f, 1.94f, 0f),
                    new Vector3(0.24f, 0.035f, 0.18f), Quaternion.identity, glow);
            AddCylinder(root.transform, "PillarCable", new Vector3(0.34f, 0.78f, -0.28f),
                    0.035f, 1.45f, Quaternion.identity, mats.Rubber, 10);
        }

        static void AddBarrelCluster(Transform parent, Vector3 basePos, int count, MaterialLibrary mats)
        {
            var root = new GameObject("BarrelCluster");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            for (int i = 0; i < count; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float radius = i == 0 ? 0f : 0.48f + (i % 2) * 0.16f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;

                AddCylinder(root.transform, "Barrel", new Vector3(x, 0.48f, z),
                        0.34f, 0.94f, Quaternion.identity,
                        i % 3 == 0 ? mats.Rust : (i % 3 == 1 ? mats.SteelDark : mats.SignalYellow), 18);
                AddCylinder(root.transform, "BarrelBandTop", new Vector3(x, 0.78f, z),
                        0.355f, 0.07f, Quaternion.identity, mats.BlackFrame, 18);
                AddCylinder(root.transform, "BarrelBandBottom", new Vector3(x, 0.20f, z),
                        0.355f, 0.07f, Quaternion.identity, mats.BlackFrame, 18);
                AddCylinder(root.transform, "BarrelCap", new Vector3(x, 0.965f, z),
                        0.30f, 0.035f, Quaternion.identity, mats.Titanium, 18);
            }
        }

        static void AddLabBench(Transform parent, Vector3 basePos, float length, MaterialLibrary mats)
        {
            var root = new GameObject("LabBench");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            AddBox(root.transform, "BenchTop", new Vector3(0f, 0.95f, 0f),
                    new Vector3(length, 0.10f, 0.90f), Quaternion.identity, mats.Ceramic);
            AddBox(root.transform, "BenchFrame", new Vector3(0f, 0.58f, 0f),
                    new Vector3(length - 0.12f, 0.62f, 0.76f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "BenchKick", new Vector3(0f, 0.16f, 0.30f),
                    new Vector3(length - 0.30f, 0.14f, 0.10f), Quaternion.identity, mats.BlackFrame);

            int drawers = Mathf.Max(2, Mathf.RoundToInt(length / 1.1f));
            float drawerW = (length - 0.35f) / drawers;
            for (int i = 0; i < drawers; i++)
            {
                float x = -length * 0.5f + 0.18f + drawerW * (i + 0.5f);
                AddBox(root.transform, "Drawer", new Vector3(x, 0.58f, 0.40f),
                        new Vector3(drawerW - 0.04f, 0.24f, 0.035f), Quaternion.identity, mats.Steel);
                AddBox(root.transform, "DrawerHandle", new Vector3(x, 0.58f, 0.43f),
                        new Vector3(drawerW * 0.44f, 0.025f, 0.025f), Quaternion.identity, mats.Titanium);
            }

            AddBox(root.transform, "BenchBack", new Vector3(0f, 1.55f, -0.38f),
                    new Vector3(length - 0.20f, 1.10f, 0.05f), Quaternion.identity, mats.Graphite);
            AddBox(root.transform, "BenchShelf", new Vector3(0f, 1.62f, -0.20f),
                    new Vector3(length - 0.40f, 0.045f, 0.34f), Quaternion.identity, mats.Steel);

            for (int i = 0; i < 4; i++)
            {
                float x = -length * 0.5f + 0.48f + i * (length - 0.96f) / 3f;
                AddBox(root.transform, "BenchBottle", new Vector3(x, 1.24f, -0.08f),
                        new Vector3(0.12f, 0.36f, 0.12f), Quaternion.identity,
                        i % 2 == 0 ? mats.GlassGreen : mats.GlassTint);
                AddCylinder(root.transform, "BenchBottleCap", new Vector3(x, 1.44f, -0.08f),
                        0.055f, 0.06f, Quaternion.identity, mats.Titanium, 12);
            }

            AddBox(root.transform, "BenchScreen", new Vector3(length * 0.22f, 1.35f, -0.320f),
                    new Vector3(0.62f, 0.36f, 0.025f), Quaternion.Euler(-8f, 0f, 0f), mats.ScreenCyan);
            AddBox(root.transform, "BenchScreenFrame", new Vector3(length * 0.22f, 1.35f, -0.300f),
                    new Vector3(0.68f, 0.42f, 0.020f), Quaternion.Euler(-8f, 0f, 0f), mats.BlackFrame);
            AddCableDrop(root.transform, new Vector3(-length * 0.34f, 0.90f, -0.30f), 0.78f, mats);
        }

        static void AddSpecimenPod(Transform parent, Vector3 basePos, MaterialLibrary mats)
        {
            var root = new GameObject("SpecimenPod");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            AddCylinder(root.transform, "PodBase", new Vector3(0f, 0.16f, 0f),
                    0.50f, 0.32f, Quaternion.identity, mats.SteelDark, 20);
            AddCylinder(root.transform, "PodGlass", new Vector3(0f, 1.30f, 0f),
                    0.42f, 1.85f, Quaternion.identity, mats.GlassGreen, 22);
            AddCylinder(root.transform, "PodLiquid", new Vector3(0f, 1.15f, 0f),
                    0.36f, 1.35f, Quaternion.identity, mats.LiquidGreen, 22);
            AddCylinder(root.transform, "PodCore", new Vector3(0f, 1.20f, 0f),
                    0.11f, 1.10f, Quaternion.identity, mats.GreenGlow, 14);
            AddCylinder(root.transform, "PodCapTop", new Vector3(0f, 2.30f, 0f),
                    0.50f, 0.20f, Quaternion.identity, mats.Titanium, 20);
            AddCylinder(root.transform, "PodCapBand", new Vector3(0f, 2.18f, 0f),
                    0.43f, 0.055f, Quaternion.identity, mats.CyanGlow, 20);
            AddCylinder(root.transform, "PodTopPipe", new Vector3(0f, 2.58f, 0f),
                    0.055f, 0.42f, Quaternion.identity, mats.Steel, 12);
            AddBox(root.transform, "PodLabel", new Vector3(0f, 0.52f, 0.46f),
                    new Vector3(0.40f, 0.16f, 0.025f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "PodLabelGlow", new Vector3(0f, 0.52f, 0.475f),
                    new Vector3(0.28f, 0.030f, 0.010f), Quaternion.identity, mats.GreenGlow);
        }

        static void AddDeployableBarrier(Transform parent, Vector3 basePos, float yaw, MaterialLibrary mats)
        {
            var root = new GameObject("DeployableBarrier");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            AddBox(root.transform, "BarrierBody", new Vector3(0f, 0.72f, 0f),
                    new Vector3(2.0f, 1.20f, 0.24f), Quaternion.Euler(-12f, 0f, 0f), mats.SteelDark);
            AddBox(root.transform, "BarrierTop", new Vector3(0f, 1.34f, 0.08f),
                    new Vector3(2.1f, 0.16f, 0.32f), Quaternion.identity, mats.Titanium);
            AddBox(root.transform, "BarrierStripe", new Vector3(0f, 0.72f, 0.14f),
                    new Vector3(1.84f, 0.22f, 0.020f), Quaternion.Euler(-12f, 0f, 0f), mats.SignalYellow);
            AddHazardStrip(root.transform, new Vector3(0f, 0.72f, 0.155f), Quaternion.Euler(-12f, 0f, 0f), 1.8f, mats);

            for (int i = 0; i < 3; i++)
            {
                float x = -0.72f + i * 0.72f;
                AddBox(root.transform, "BarrierLeg", new Vector3(x, 0.25f, 0.30f),
                        new Vector3(0.14f, 0.50f, 0.14f), Quaternion.Euler(18f, 0f, 0f), mats.BlackFrame);
                AddCylinder(root.transform, "BarrierFoot", new Vector3(x, 0.06f, 0.46f),
                        0.10f, 0.12f, Quaternion.Euler(0f, 0f, 90f), mats.Rubber, 12);
            }

            AddBox(root.transform, "BarrierLight", new Vector3(0.86f, 1.30f, 0.18f),
                    new Vector3(0.10f, 0.10f, 0.05f), Quaternion.identity, mats.AmberGlow);
        }

        static void AddContainmentFrame(Transform parent, Vector3 basePos, MaterialLibrary mats)
        {
            var root = new GameObject("ContainmentUnit");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;
            root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            AddBox(root.transform, "ContainmentBase", new Vector3(0f, 0.18f, 0f),
                    new Vector3(2.4f, 0.36f, 1.4f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "ContainmentTank", new Vector3(0f, 1.60f, 0f),
                    new Vector3(2.1f, 2.30f, 1.10f), Quaternion.identity, mats.GlassGreen);
            AddBox(root.transform, "ContainmentLiquid", new Vector3(0f, 1.34f, 0f),
                    new Vector3(1.96f, 1.70f, 0.98f), Quaternion.identity, mats.LiquidGreen);
            AddBox(root.transform, "ContainmentCore", new Vector3(0f, 1.42f, 0f),
                    new Vector3(0.34f, 1.30f, 0.34f), Quaternion.identity, mats.GreenGlow);

            for (int i = 0; i < 4; i++)
            {
                float x = -1.06f + i * 0.707f;
                AddBox(root.transform, "ContainmentFrameBar", new Vector3(x, 1.60f, 0.58f),
                        new Vector3(0.10f, 2.42f, 0.10f), Quaternion.identity, mats.Titanium);
            }

            AddBox(root.transform, "ContainmentCap", new Vector3(0f, 2.82f, 0f),
                    new Vector3(2.30f, 0.22f, 1.26f), Quaternion.identity, mats.Titanium);
            AddBox(root.transform, "ContainmentWarning", new Vector3(0f, 0.42f, 0.73f),
                    new Vector3(0.72f, 0.22f, 0.025f), Quaternion.identity, mats.SignalYellow);
            AddBox(root.transform, "ContainmentWarningGlow", new Vector3(0f, 0.42f, 0.745f),
                    new Vector3(0.48f, 0.035f, 0.012f), Quaternion.identity, mats.GreenGlow);
            AddCylinder(root.transform, "ContainmentPipe", new Vector3(0.72f, 2.98f, 0f),
                    0.08f, 0.42f, Quaternion.identity, mats.Steel, 12);
        }

        static void AddGenerator(Transform parent, Vector3 basePos, MaterialLibrary mats)
        {
            var root = new GameObject("Generator");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;
            root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            AddBox(root.transform, "GeneratorBase", new Vector3(0f, 0.16f, 0f),
                    new Vector3(2.8f, 0.32f, 1.6f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "GeneratorBody", new Vector3(0f, 1.15f, 0f),
                    new Vector3(2.5f, 1.80f, 1.36f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "GeneratorPanel", new Vector3(0f, 1.22f, 0.70f),
                    new Vector3(2.10f, 1.30f, 0.035f), Quaternion.identity, mats.Graphite);

            for (int i = 0; i < 7; i++)
            {
                AddBox(root.transform, "GeneratorVent", new Vector3(0f, 0.60f + i * 0.10f, 0.725f),
                        new Vector3(1.7f, 0.035f, 0.020f), Quaternion.identity, mats.BlackFrame);
            }

            AddCylinder(root.transform, "GeneratorTurbine", new Vector3(0f, 1.24f, 0.76f),
                    0.36f, 0.10f, Quaternion.Euler(90f, 0f, 0f), mats.Titanium, 20);
            AddCylinder(root.transform, "GeneratorTurbineHub", new Vector3(0f, 1.24f, 0.82f),
                    0.10f, 0.08f, Quaternion.Euler(90f, 0f, 0f), mats.CyanGlow, 16);

            AddBox(root.transform, "GeneratorTop", new Vector3(0f, 2.10f, 0f),
                    new Vector3(2.62f, 0.16f, 1.46f), Quaternion.identity, mats.Titanium);
            AddCylinder(root.transform, "GeneratorExhaust", new Vector3(-0.8f, 2.60f, 0f),
                    0.18f, 0.90f, Quaternion.identity, mats.Rust, 16);
            AddCylinder(root.transform, "GeneratorExhaustCap", new Vector3(-0.8f, 3.06f, 0f),
                    0.24f, 0.10f, Quaternion.identity, mats.BlackFrame, 16);
            AddBox(root.transform, "GeneratorLight", new Vector3(0.9f, 1.82f, 0.74f),
                    new Vector3(0.30f, 0.06f, 0.03f), Quaternion.identity, mats.AmberGlow);
        }

        static void AddDecontaminationGate(Transform parent, Vector3 basePos, MaterialLibrary mats)
        {
            var root = new GameObject("DecontaminationGate");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePos;

            AddBox(root.transform, "GateColumnL", new Vector3(0f, 1.8f, -3.2f),
                    new Vector3(0.55f, 3.6f, 0.55f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "GateColumnR", new Vector3(0f, 1.8f, 3.2f),
                    new Vector3(0.55f, 3.6f, 0.55f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "GateBeam", new Vector3(0f, 3.75f, 0f),
                    new Vector3(0.55f, 0.50f, 6.95f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "GateHeading", new Vector3(0f, 3.45f, 0f),
                    new Vector3(0.30f, 0.16f, 5.60f), Quaternion.identity, mats.CyanGlow);

            for (int i = 0; i < 5; i++)
            {
                AddCylinder(root.transform, "GateNozzle", new Vector3(0f, 3.98f, -2.2f + i * 1.1f),
                        0.045f, 0.55f, Quaternion.Euler(90f, 0f, 0f), mats.Titanium, 10);
                AddCylinder(root.transform, "GateMist", new Vector3(0f, 3.60f, -2.2f + i * 1.1f),
                        0.016f, 1.30f, Quaternion.identity, mats.LiquidCyan, 8);
            }

            AddBox(root.transform, "GateHazardL", new Vector3(0f, 1.0f, -3.2f),
                    new Vector3(0.58f, 1.2f, 0.58f), Quaternion.identity, mats.SignalYellow);
            AddBox(root.transform, "GateHazardR", new Vector3(0f, 1.0f, 3.2f),
                    new Vector3(0.58f, 1.2f, 0.58f), Quaternion.identity, mats.SignalYellow);
            AddBox(root.transform, "GateFloor", new Vector3(0f, 0.030f, 0f),
                    new Vector3(1.2f, 0.02f, 6.4f), Quaternion.identity, mats.CyanGlow);
        }


        // ------------------------------------------------------------------
        // Signage and lighting
        // ------------------------------------------------------------------

        static void AddHangingSign(Transform parent, Vector3 pos, SignType type, MaterialLibrary mats)
        {
            var root = new GameObject("HangingSign_" + type);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            float width = 1.45f;
            float height = 0.72f;

            AddSignBoard(root.transform, width, height, type, mats);

            float deltaY = CeilingUnder - pos.y;
            float rodBottom = height * 0.5f;
            float rodHeight = Mathf.Max(0.10f, deltaY - rodBottom);

            AddBox(root.transform, "SignRodL", new Vector3(-width * 0.36f, rodBottom + rodHeight * 0.5f, 0f),
                    new Vector3(0.045f, rodHeight, 0.045f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "SignRodR", new Vector3(width * 0.36f, rodBottom + rodHeight * 0.5f, 0f),
                    new Vector3(0.045f, rodHeight, 0.045f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "SignMount", new Vector3(0f, deltaY - 0.05f, 0f),
                    new Vector3(width * 0.86f, 0.08f, 0.16f), Quaternion.identity, mats.BlackFrame);
        }

        static void AddWallSign(Transform parent, Vector3 pos, SignType type, MaterialLibrary mats)
        {
            var root = new GameObject("WallSign_" + type);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = pos.z < 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);

            AddSignBoard(root.transform, 2.10f, 0.78f, type, mats);

            AddBox(root.transform, "SignBracketTop", new Vector3(0f, 0.48f, -0.02f),
                    new Vector3(1.92f, 0.06f, 0.18f), Quaternion.identity, mats.SteelDark);
            AddBox(root.transform, "SignBracketBottom", new Vector3(0f, -0.48f, -0.02f),
                    new Vector3(1.92f, 0.06f, 0.18f), Quaternion.identity, mats.SteelDark);
        }

        static void AddFloorStencil(Transform parent, Vector3 pos, SignType type, MaterialLibrary mats)
        {
            var root = new GameObject("FloorStencil_" + type);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = pos;
            root.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            AddBox(root.transform, "StencilPlate", Vector3.zero,
                    new Vector3(2.20f, 1.35f, 0.020f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "StencilEdgeX", new Vector3(0f, 0.62f, 0.020f),
                    new Vector3(2.10f, 0.045f, 0.012f), Quaternion.identity, mats.SignalYellow);
            AddBox(root.transform, "StencilEdgeY", new Vector3(0f, -0.62f, 0.020f),
                    new Vector3(2.10f, 0.045f, 0.012f), Quaternion.identity, mats.SignalYellow);

            Material glow = GlowFor(type, mats);
            AddSignIcon(root.transform, type, 2.0f, 1.15f, 0.035f, glow, mats);
        }

        static void AddSignBoard(Transform parent, float width, float height, SignType type, MaterialLibrary mats)
        {
            AddBox(parent, "SignFrame", Vector3.zero,
                    new Vector3(width + 0.10f, height + 0.10f, 0.10f), Quaternion.identity, mats.BlackFrame);
            AddBox(parent, "SignPanel", new Vector3(0f, 0f, 0.055f),
                    new Vector3(width, height, 0.035f), Quaternion.identity, mats.Graphite);
            AddBox(parent, "SignTopBar", new Vector3(0f, height * 0.5f - 0.055f, 0.076f),
                    new Vector3(width * 0.92f, 0.035f, 0.012f), Quaternion.identity, GlowFor(type, mats));
            AddBox(parent, "SignBottomBar", new Vector3(0f, -height * 0.5f + 0.055f, 0.076f),
                    new Vector3(width * 0.92f, 0.035f, 0.012f), Quaternion.identity, GlowFor(type, mats));

            AddSignIcon(parent, type, width, height, 0.080f, GlowFor(type, mats), mats);

            AddBolt(parent, new Vector3(-width * 0.42f, height * 0.34f, 0.085f), Quaternion.identity, mats.Titanium, 0.026f);
            AddBolt(parent, new Vector3(width * 0.42f, height * 0.34f, 0.085f), Quaternion.identity, mats.Titanium, 0.026f);
            AddBolt(parent, new Vector3(-width * 0.42f, -height * 0.34f, 0.085f), Quaternion.identity, mats.Titanium, 0.026f);
            AddBolt(parent, new Vector3(width * 0.42f, -height * 0.34f, 0.085f), Quaternion.identity, mats.Titanium, 0.026f);
        }

        static Material GlowFor(SignType type, MaterialLibrary mats)
        {
            switch (type)
            {
                case SignType.Biohazard: return mats.GreenGlow;
                case SignType.HighVoltage: return mats.AmberGlow;
                case SignType.Restricted: return mats.RedGlow;
                case SignType.Research: return mats.CyanGlow;
                case SignType.Containment: return mats.GreenGlow;
                case SignType.Exit: return mats.GreenGlow;
                default: return mats.AmberGlow;
            }
        }

        static void AddSignIcon(Transform parent, SignType type, float w, float h, float z, Material glow, MaterialLibrary mats)
        {
            switch (type)
            {
                case SignType.Biohazard:
                    AddCylinder(parent, "IconCircleA", new Vector3(-w * 0.10f, h * 0.10f, z),
                            h * 0.09f, 0.012f, Quaternion.Euler(90f, 0f, 0f), glow, 16);
                    AddCylinder(parent, "IconCircleB", new Vector3(w * 0.10f, h * 0.10f, z),
                            h * 0.09f, 0.012f, Quaternion.Euler(90f, 0f, 0f), glow, 16);
                    AddCylinder(parent, "IconCircleC", new Vector3(0f, -h * 0.10f, z),
                            h * 0.09f, 0.012f, Quaternion.Euler(90f, 0f, 0f), glow, 16);
                    break;

                case SignType.HighVoltage:
                    AddBox(parent, "IconBoltA", new Vector3(-w * 0.035f, h * 0.10f, z),
                            new Vector3(w * 0.035f, h * 0.20f, 0.012f), Quaternion.Euler(0f, 0f, 24f), glow);
                    AddBox(parent, "IconBoltB", new Vector3(w * 0.010f, -h * 0.02f, z),
                            new Vector3(w * 0.035f, h * 0.20f, 0.012f), Quaternion.Euler(0f, 0f, -26f), glow);
                    AddBox(parent, "IconBoltC", new Vector3(-w * 0.010f, -h * 0.14f, z),
                            new Vector3(w * 0.035f, h * 0.16f, 0.012f), Quaternion.Euler(0f, 0f, 22f), glow);
                    break;

                case SignType.Restricted:
                    AddCylinder(parent, "IconRing", Vector3.zero, h * 0.22f, 0.010f,
                            Quaternion.Euler(90f, 0f, 0f), glow, 24);
                    AddBox(parent, "IconSlash", new Vector3(0f, 0f, z + 0.012f),
                            new Vector3(w * 0.42f, h * 0.035f, 0.012f), Quaternion.Euler(0f, 0f, -40f), glow);
                    break;

                case SignType.Research:
                    AddBox(parent, "IconResearchA", new Vector3(0f, h * 0.12f, z),
                            new Vector3(w * 0.40f, h * 0.045f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconResearchB", new Vector3(-w * 0.07f, 0f, z),
                            new Vector3(w * 0.26f, h * 0.045f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconResearchC", new Vector3(-w * 0.12f, -h * 0.12f, z),
                            new Vector3(w * 0.16f, h * 0.045f, 0.012f), Quaternion.identity, glow);
                    break;

                case SignType.Containment:
                    AddBox(parent, "IconContainT", new Vector3(0f, h * 0.16f, z),
                            new Vector3(w * 0.36f, h * 0.035f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconContainB", new Vector3(0f, -h * 0.16f, z),
                            new Vector3(w * 0.36f, h * 0.035f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconContainL", new Vector3(-w * 0.18f, 0f, z),
                            new Vector3(h * 0.035f, h * 0.32f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconContainR", new Vector3(w * 0.18f, 0f, z),
                            new Vector3(h * 0.035f, h * 0.32f, 0.012f), Quaternion.identity, glow);
                    AddSphere(parent, "IconContainCore", new Vector3(0f, 0f, z), h * 0.045f, glow);
                    break;

                case SignType.Exit:
                    AddBox(parent, "IconExitShaft", new Vector3(-w * 0.04f, 0f, z),
                            new Vector3(w * 0.30f, h * 0.055f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconExitHeadA", new Vector3(w * 0.09f, h * 0.045f, z),
                            new Vector3(h * 0.055f, h * 0.16f, 0.012f), Quaternion.Euler(0f, 0f, -45f), glow);
                    AddBox(parent, "IconExitHeadB", new Vector3(w * 0.09f, -h * 0.045f, z),
                            new Vector3(h * 0.055f, h * 0.16f, 0.012f), Quaternion.Euler(0f, 0f, 45f), glow);
                    break;

                default:
                    AddBox(parent, "IconCompA", new Vector3(-w * 0.08f, 0f, z),
                            new Vector3(w * 0.055f, h * 0.26f, 0.012f), Quaternion.identity, glow);
                    AddBox(parent, "IconCompB", new Vector3(w * 0.08f, 0f, z),
                            new Vector3(w * 0.055f, h * 0.26f, 0.012f), Quaternion.identity, glow);
                    break;
            }
        }

        static void AddPointLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("PointLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.bounceIntensity = 0.7f;
        }

        static void AddSpotLight(Transform parent, Vector3 pos, Color color, float intensity, float range)
        {
            var go = new GameObject("SpotLight");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.spotAngle = 72f;
            light.innerSpotAngle = 36f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.bounceIntensity = 0.7f;
        }

        // ------------------------------------------------------------------
        // Low level construction helpers (no colliders are kept)
        // ------------------------------------------------------------------

        static GameObject AddBox(Transform parent, string name, Vector3 localPos, Vector3 size,
                                 Quaternion localRot, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = size;

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            ApplyMaterial(go, material);
            return go;
        }

        static GameObject AddCylinder(Transform parent, string name, Vector3 localPos, float radius,
                                      float height, Quaternion localRot, Material material, int segments)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            ApplyMaterial(go, material);
            return go;
        }

        static GameObject AddSphere(Transform parent, string name, Vector3 localPos, float radius, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * (radius * 2f);

            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);

            ApplyMaterial(go, material);
            return go;
        }

        static void ApplyMaterial(GameObject go, Material material)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = material != null && material.renderQueue >= 3000
                    ? ShadowCastingMode.Off
                    : ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        static void AddBolt(Transform parent, Vector3 localPos, Quaternion localRot, Material material, float radius)
        {
            AddCylinder(parent, "Bolt", localPos, radius, 0.030f,
                    localRot * Quaternion.Euler(90f, 0f, 0f), material, 8);
        }

        static void AddHazardStrip(Transform parent, Vector3 localPos, Quaternion localRot,
                                   float length, MaterialLibrary mats)
        {
            var root = new GameObject("HazardStrip");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;
            root.transform.localRotation = localRot;

            AddBox(root.transform, "HazardBase", Vector3.zero,
                    new Vector3(length, 0.11f, 0.016f), Quaternion.identity, mats.SignalYellow);

            int stripes = Mathf.Max(2, Mathf.FloorToInt(length / 0.18f));
            for (int i = 0; i < stripes; i++)
            {
                float x = -length * 0.5f + (i + 0.5f) * (length / stripes);
                AddBox(root.transform, "HazardStripe", new Vector3(x, 0f, 0.010f),
                        new Vector3(length / stripes * 0.42f, 0.11f, 0.006f),
                        Quaternion.Euler(0f, 0f, 45f), mats.BlackFrame);
            }
        }

        static void AddWarningTriangle(Transform parent, Vector3 localPos, MaterialLibrary mats)
        {
            var root = new GameObject("WarningTriangle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPos;

            AddBox(root.transform, "TriBase", new Vector3(0f, -0.055f, 0f),
                    new Vector3(0.16f, 0.022f, 0.010f), Quaternion.identity, mats.BlackFrame);
            AddBox(root.transform, "TriLeft", new Vector3(-0.040f, 0f, 0f),
                    new Vector3(0.020f, 0.150f, 0.010f), Quaternion.Euler(0f, 0f, -32f), mats.BlackFrame);
            AddBox(root.transform, "TriRight", new Vector3(0.040f, 0f, 0f),
                    new Vector3(0.020f, 0.150f, 0.010f), Quaternion.Euler(0f, 0f, 32f), mats.BlackFrame);
            AddBox(root.transform, "TriMark", new Vector3(0f, 0.010f, 0.006f),
                    new Vector3(0.018f, 0.060f, 0.008f), Quaternion.identity, mats.SignalYellow);
        }

        static void AddGrime(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            int patches = 3 + Mathf.FloorToInt(seed % 1f * 3f);
            for (int i = 0; i < patches; i++)
            {
                float x = (Rand01(seed + i * 4.7f) - 0.5f) * w * 0.80f;
                float y = 0.35f + Rand01(seed + i * 8.1f) * h * 0.55f;
                float pw = 0.12f + Rand01(seed + i * 2.3f) * 0.45f;
                float ph = 0.20f + Rand01(seed + i * 6.4f) * 0.70f;

                AddBox(parent, "GrimePatch", new Vector3(x, y, 0.228f),
                        new Vector3(pw, ph, 0.010f),
                        Quaternion.Euler(0f, 0f, Rand01(seed + i) * 18f - 9f), mats.Grime);
            }

            AddBox(parent, "GrimeEdge", new Vector3(0f, 0.20f, 0.230f),
                    new Vector3(w * 0.92f, 0.10f, 0.010f), Quaternion.identity, mats.Grime);
        }

        static void AddScratch(Transform parent, float w, float h, float seed, MaterialLibrary mats)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = (Rand01(seed + i * 9.3f) - 0.5f) * w * 0.75f;
                float y = 0.60f + Rand01(seed + i * 3.9f) * h * 0.50f;

                AddBox(parent, "Scratch", new Vector3(x, y, 0.232f),
                        new Vector3(0.30f + Rand01(seed + i) * 0.35f, 0.012f, 0.008f),
                        Quaternion.Euler(0f, 0f, Rand01(seed + i * 2.1f) * 50f - 25f), mats.Titanium);
            }
        }

        static void AddCableDrop(Transform parent, Vector3 topPos, float length, MaterialLibrary mats)
        {
            AddCylinder(parent, "CableDropA", topPos + new Vector3(0.03f, -length * 0.5f, 0f),
                    0.025f, length, Quaternion.identity, mats.Rubber, 8);
            AddCylinder(parent, "CableDropB", topPos + new Vector3(-0.03f, -length * 0.5f, 0.03f),
                    0.020f, length * 0.92f, Quaternion.identity, mats.BlackFrame, 8);
            AddBox(parent, "CableClip", topPos + new Vector3(0f, -0.12f, 0f),
                    new Vector3(0.12f, 0.05f, 0.10f), Quaternion.identity, mats.Steel);
        }

        static void MatCableRun(Transform parent, Vector3 center, float radius, int count,
                                float length, MaterialLibrary mats)
        {
            var root = new GameObject("CableBundle");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = center;

            for (int i = 0; i < count; i++)
            {
                float offsetY = (i - (count - 1) * 0.5f) * radius * 1.15f;
                Material m = i % 3 == 0 ? mats.Rubber : (i % 3 == 1 ? mats.SteelDark : mats.Steel);
                AddCylinder(root.transform, "Cable", new Vector3(0f, offsetY, 0f),
                        radius * 0.42f, length, Quaternion.Euler(0f, 0f, 90f), m, 10);
            }

            AddBox(root.transform, "CableTie", new Vector3(0f, 0f, radius * 1.2f),
                    new Vector3(0.04f, count * radius * 1.2f, 0.03f), Quaternion.identity, mats.BlackFrame);
        }

        static void AddCableRun(Transform parent, Vector3 start, Quaternion rotation, float length,
                                Material matA, Material matB)
        {
            var root = new GameObject("CableRun");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = start;
            root.transform.localRotation = rotation;

            float[] offY = { -0.05f, 0f, 0.05f };
            float[] offZ = { -0.05f, 0.04f, -0.02f };
            float direction = Vector3.Dot(rotation * Vector3.right, Vector3.right) >= 0f ? 1f : -1f;
            for (int i = 0; i < 3; i++)
            {
                AddCylinder(root.transform, "CableRunSegment", new Vector3(direction * length * 0.5f, offY[i], offZ[i]),
                        0.035f, length, Quaternion.Euler(0f, 0f, 90f),
                        i % 2 == 0 ? matA : matB, 8);
            }

            for (float x = 0.6f; x < length; x += 2.4f)
            {
                AddBox(root.transform, "CableRunClamp", new Vector3(direction * x, 0f, 0f),
                        new Vector3(0.09f, 0.20f, 0.20f), Quaternion.identity, matA);
            }
        }

        static float Hash(int a, int b, int c)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h = (h ^ (uint)c) * 16777619u;
                h ^= h >> 13;
                h *= 0x5bd1e995u;
                h ^= h >> 15;
                return (h & 0x7fffffffu) / 2147483647f;
            }
        }

        static float Rand01(float seed)
        {
            float value = Mathf.Sin(seed * 12.9898f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
