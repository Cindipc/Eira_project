using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace EiraGame.Editor
{
    public static class EiraPaths
    {
        public const string Root = "Assets/Eira";
        public const string Env = Root + "/Environment";
        public const string Spz = Env + "/full_res.spz";
        public const string SplatFolder = Env + "/Splat";
        public const string SplatAsset = SplatFolder + "/full_res.asset";
        public const string GlbSource = Env + "/environment_collider.glb";
        public const string MeshAsset = Env + "/EnvironmentColliderMesh.asset";
        public const string Scenes = Root + "/Scenes";
        public const string Materials = Root + "/Materials";

        public static void EnsureFolders()
        {
            Ensure(System.IO.Path.Combine("Assets", "Eira"));
            Ensure("Assets/Eira/Environment");
            Ensure("Assets/Eira/Materials");
            Ensure("Assets/Eira/Scripts");
            Ensure("Assets/Eira/Scenes");
        }

        static void Ensure(string folder)
        {
            if (!System.IO.Directory.Exists(folder))
                System.IO.Directory.CreateDirectory(folder);
        }
    }

    // Integración opcional del paquete de Gaussian Splatting. Usa reflexión,
    // así la compilación no depende del paquete.
    public static class EiraGaussian
    {
        static System.Type s_rendererType;
        static System.Type s_rendererTypeSafe()
        {
            if (s_rendererType == null)
                s_rendererType = System.Type.GetType("GaussianSplatting.Runtime.GaussianSplatRenderer, GaussianSplatting");
            return s_rendererType;
        }

        static System.Type s_creatorType;

        public static bool IsPackageAvailable => s_rendererTypeSafe() != null;

        // 1) Añade GaussianSplatURPFeature a los renderer URP activos.
        public static void ConfigureURPRenderers()
        {
            if (!IsPackageAvailable) { Debug.LogWarning("EiraGaussian: paquete de Gaussian Splatting no presente."); return; }
            var featureType = System.Type.GetType("GaussianSplatting.Runtime.GaussianSplatURPFeature, GaussianSplatting");
            if (featureType == null) { Debug.LogWarning("EiraGaussian: no se encontró GaussianSplatURPFeature."); return; }

            var guids = AssetDatabase.FindAssets("t:ScriptableRendererData");
            int added = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var rd = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rd == null) continue;
                bool has = false;
                foreach (var f in rd.rendererFeatures)
                    if (f != null && f.GetType() == featureType) { has = true; break; }
                if (has) continue;
                var instance = ScriptableObject.CreateInstance(featureType);
                if (instance != null)
                {
                    instance.name = "GaussianSplatURPFeature";
                    AssetDatabase.AddObjectToAsset(instance, AssetDatabase.GetAssetPath(rd));
                    var so = new SerializedObject(rd);
                    var arr = so.FindProperty("m_RendererFeatures");
                    arr.arraySize++;
                    arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = instance;
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(rd);
                    added++;
                }
            }
            AssetDatabase.SaveAssets();
            if (added > 0) AssetDatabase.Refresh();
            Debug.Log($"EiraGaussian: feature URP añadido a {added} renderer(s).");
        }

        // 2) Convierte full_res.spz en un GaussianSplatAsset mediante la ventana del paquete (reflexión).
        public static void ConvertSplat()
        {
            System.IO.Directory.CreateDirectory(EiraPaths.SplatFolder);
            if (!System.IO.File.Exists(EiraPaths.Spz))
            {
                Debug.LogError("EiraGaussian: no existe " + EiraPaths.Spz);
                return;
            }
            if (!IsPackageAvailable)
            {
                Debug.LogError("EiraGaussian: instala el paquete org.nesnausk.gaussian-splatting primero.");
                return;
            }

            if (s_creatorType == null)
                s_creatorType = System.Type.GetType("GaussianSplatting.Editor.GaussianSplatAssetCreator, GaussianSplattingEditor");

            if (s_creatorType == null)
            {
                Debug.LogError("EiraGaussian: no se encontró GaussianSplatAssetCreator.");
                return;
            }

            var win = ScriptableObject.CreateInstance(s_creatorType);
            try
            {
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                s_creatorType.GetField("m_InputFile", flags)?.SetValue(win, System.IO.Path.GetFullPath(EiraPaths.Spz).Replace('\\', '/'));
                s_creatorType.GetField("m_OutputFolder", flags)?.SetValue(win, EiraPaths.SplatFolder);
                s_creatorType.GetField("m_ImportCameras", flags)?.SetValue(win, false);
                var qualityEnum = System.Enum.Parse(s_creatorType.GetNestedType("DataQuality", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic), "Medium");
                s_creatorType.GetField("m_Quality", flags)?.SetValue(win, qualityEnum);
                s_creatorType.GetMethod("ApplyQualityLevel", flags)?.Invoke(win, null);
                var bytesExist = System.IO.File.Exists(EiraPaths.SplatAsset);
                s_creatorType.GetMethod("CreateAsset", flags)?.Invoke(win, null);
                if (System.IO.File.Exists(EiraPaths.SplatAsset))
                {
                    Debug.Log("EiraGaussian: splat asset creado en " + EiraPaths.SplatAsset + (bytesExist ? " (reemplazado)" : ""));
                }
                else
                {
                    var err = s_creatorType.GetField("m_ErrorMessage", flags)?.GetValue(win) as string;
                    Debug.LogWarning("EiraGaussian: no se generó el asset. Error: " + (err ?? "desconocido"));
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError("EiraGaussian: fallo al crear el splat asset: " + ex.Message);
            }
            finally
            {
                Object.DestroyImmediate(win);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        // 3) Crea el objeto de entorno: renderer de splats + collider desde el GLB.
        public static GameObject CreateEnvironmentObject(Vector3 position, Vector3 scale)
        {
            var root = new GameObject("GaussianEnvironment");

            if (IsPackageAvailable && System.IO.File.Exists(EiraPaths.SplatAsset))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(EiraPaths.SplatAsset);
                if (asset != null)
                {
                    var comp = root.AddComponent(s_rendererTypeSafe());
                    if (comp != null)
                    {
                        var so = new SerializedObject(comp);
                        so.FindProperty("m_Asset").objectReferenceValue = asset;
                        var shaders = new Dictionary<string, string>
                        {
                            { "m_ShaderSplats", "RenderGaussianSplats" },
                            { "m_ShaderComposite", "GaussianComposite" },
                            { "m_ShaderDebugPoints", "GaussianDebugRenderPoints" },
                            { "m_ShaderDebugBoxes", "GaussianDebugRenderBoxes" },
                        };
                        foreach (var kv in shaders)
                        {
                            var prop = so.FindProperty(kv.Key);
                            if (prop != null)
                            {
                                var cands = AssetDatabase.FindAssets(kv.Value + " t:Shader");
                                if (cands.Length > 0)
                                {
                                    var sh = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(cands[0]));
                                    if (sh != null) prop.objectReferenceValue = sh;
                                }
                            }
                        }
                        var csProp = so.FindProperty("m_CSSplatUtilities");
                        if (csProp != null)
                        {
                            var cands = AssetDatabase.FindAssets("SplatUtilities t:ComputeShader");
                            if (cands.Length > 0)
                                csProp.objectReferenceValue = AssetDatabase.LoadAssetAtPath<ComputeShader>(AssetDatabase.GUIDToAssetPath(cands[0]));
                        }
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(comp);
                    }
                }
            }

            var mesh = LoadOrCreateColliderMesh();
            if (mesh != null)
            {
                var mc = root.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }

            root.transform.position = position;
            root.transform.localScale = scale;
            return root;
        }

        static Mesh LoadOrCreateColliderMesh()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(EiraPaths.MeshAsset);
            if (existing != null) return existing;

            if (!System.IO.File.Exists(EiraPaths.GlbSource)) return null;
            try
            {
                var res = GlbImporter.Load(EiraPaths.GlbSource);
                var mesh = res.mesh;
                if (mesh == null || mesh.vertexCount == 0)
                {
                    Debug.LogWarning("EiraGaussian: GLB sin vértices.");
                    return null;
                }
                AssetDatabase.CreateAsset(mesh, EiraPaths.MeshAsset);
                AssetDatabase.SaveAssets();
                return mesh;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("EiraGaussian: no se pudo leer el GLB del entorno: " + ex.Message);
                return null;
            }
        }
    }
}