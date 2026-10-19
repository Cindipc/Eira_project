using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Reemplaza POR COMPLETO el escenario del Nivel 2 por el nuevo modelo
    /// "Futuristic City" (FBX). Elimina el escenario antiguo (Secret Laboratory)
    /// de la escena y también sus assets (prefab, modelo y materiales), ya que
    /// sólo eran usados por el Nivel 2.
    ///
    /// Menu: Eira / Nivel 2 / Reemplazar escenario (Futuristic City)
    /// </summary>
    public static class Level2CitySwap
    {
        const string Level2Scene = "Assets/Eira/Scenes/Level2Scene.unity";

        // FBX de origen (nuevo escenario del Nivel 2).
        const string SourceFbx = @"C:\Users\Usuario\Downloads\1790910911137_uploads_files_2727648_Futuristic_City.fbx";

        // Destino dentro del proyecto.
        const string ModelFolder = "Assets/Eira/Models/Nivel2City";
        const string TargetFbx = ModelFolder + "/FuturisticCity.fbx";
        const string MaterialFolder = "Assets/Eira/Materials/Nivel2City";

        // Escenario antiguo que se borra.
        const string OldRootName = "SecretLaboratory_Environment";
        const string OldPrefabPath = "Assets/Eira/Environment/SecretLaboratory.prefab";
        const string OldModelFolder = "Assets/Eira/Models/SecretLaboratory";
        const string OldMaterialFolder = "Assets/Eira/Materials/SecretLab";

        const string NewRootName = "Nivel2City_Environment";

        // Escala aplicada al modelo de la ciudad. 0.25 deja una ciudad de
        // ~112 u con edificios de ~37 u. Súbelo/bájalo para agrandar/reducir.
        const float ModelScale = 0.25f;

        const string ReportFile = @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_level2_swap.log";
        const string DiagFile = @"C:\Users\Usuario\AppData\Local\Temp\opencode\eira_fbx_diag.txt";
        const string BackupFolder = @"C:\Users\Usuario\AppData\Local\Temp\opencode";

        [MenuItem("Eira/Nivel 2/Reemplazar escenario (Futuristic City)", priority = 1)]
        public static void Execute()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Eira", "Detén el modo Play antes de reemplazar el escenario.", "OK");
                Debug.LogWarning("[Level2CitySwap] Detén el modo Play antes de reemplazar el escenario.");
                return;
            }

            var report = new System.Text.StringBuilder();

            try
            {
                // 1) Copiar el FBX al proyecto e importarlo.
                if (!File.Exists(SourceFbx))
                    throw new System.Exception("No se encontró el FBX de origen: " + SourceFbx);

                Directory.CreateDirectory(ModelFolder);
                Directory.CreateDirectory(MaterialFolder);
                File.Copy(SourceFbx, TargetFbx, true);

                AssetDatabase.ImportAsset(TargetFbx, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TargetFbx);
                if (fbx == null)
                    throw new System.Exception("No se pudo cargar el FBX importado: " + TargetFbx);
                report.AppendLine("FBX importado: " + TargetFbx);

                // 2) Abrir el Nivel 2 (pidiendo guardar las escenas modificadas).
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    Debug.Log("[Level2CitySwap] Cancelado por el usuario (escenas sin guardar).");
                    return;
                }
                var scene = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

                // 3) Borrar el escenario antiguo y cualquier versión previa del nuevo.
                foreach (var old in new[] { OldRootName, NewRootName, "SecretLaboratory", "Laboratorio", "Escenografia", "GaussianEnvironment" })
                {
                    var go = GameObject.Find(old);
                    if (go != null)
                    {
                        Object.DestroyImmediate(go);
                        report.AppendLine("Eliminado de la escena: " + old);
                    }
                }

                // 4) Instanciar el nuevo escenario.
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                instance.name = NewRootName;
                instance.transform.SetParent(null, false);
                instance.transform.position = Vector3.zero;
                instance.transform.rotation = Quaternion.identity;
                instance.transform.localScale = Vector3.one;

                // Quitar animaciones heredadas del modelo que puedan mover la escena.
                var animator = instance.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
                var legacyAnim = instance.GetComponent<Animation>();
                if (legacyAnim != null) Object.DestroyImmediate(legacyAnim);

                // 5) Escalar y colocar sobre la zona jugable.
                FitEnvironment(instance, report);

                // 6) Colliders sobre todas las mallas.
                int colliders = EnsureColliders(instance.transform);
                report.AppendLine("MeshColliders añadidos: " + colliders);

                // 7) Convertir materiales a URP/Lit.
                int mats = UpgradeMaterials(instance);
                report.AppendLine("Materiales convertidos a URP/Lit: " + mats);

                // 8) Guardar la escena.
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                // 9) Borrar por completo los assets del escenario antiguo.
                DeleteOldAssets(report);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                report.AppendLine("Nivel 2 guardado. TERMINADO.");

                File.WriteAllText(ReportFile, report.ToString());
                Debug.Log("[Level2CitySwap] " + report);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Eira",
                        "Escenario del Nivel 2 reemplazado por Futuristic City.\nEl escenario antiguo (Secret Laboratory) fue eliminado.",
                        "OK");
            }
            catch (System.Exception ex)
            {
                report.AppendLine("ERROR: " + ex);
                File.WriteAllText(ReportFile, report.ToString());
                Debug.LogError("[Level2CitySwap] ERROR: " + ex);
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("Eira", "Error al reemplazar el escenario:\n" + ex.Message, "OK");
                throw;
            }
        }

        [MenuItem("Eira/Nivel 2/Verificar escenario", priority = 20)]
        public static void Verify()
        {
            var sc = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);
            Debug.Log("[Level2Verify] ============ " + Level2Scene + " ============");
            foreach (var go in sc.GetRootGameObjects())
                Debug.Log("[Level2Verify] ROOT: '" + go.name + "' active=" + go.activeSelf + " pos=" + go.transform.position);
        }

        [MenuItem("Eira/Nivel 2/Diagnostico escena (objetos grandes)", priority = 22)]
        public static void DumpScene()
        {
            var sb = new System.Text.StringBuilder();
            System.Action<string> L = s => { Debug.Log(s); sb.AppendLine(s); };

            var sc = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);
            L("[SceneDiag] ===== " + Level2Scene + " =====");

            var list = new List<Renderer>();
            foreach (var root in sc.GetRootGameObjects())
            {
                L("[SceneDiag] ROOT '" + root.name + "' active=" + root.activeInHierarchy +
                  " pos=" + root.transform.position + " lossyScale=" + root.transform.lossyScale);
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    if (r != null && !(r is ParticleSystemRenderer)) list.Add(r);
            }

            list.Sort((a, b) => b.bounds.size.sqrMagnitude.CompareTo(a.bounds.size.sqrMagnitude));
            L("[SceneDiag] --- renderers mas grandes (mundo) ---");
            for (int k = 0; k < Mathf.Min(80, list.Count); k++)
            {
                var r = list[k];
                var b = r.bounds;
                if (b.size.magnitude < 2f) break;
                L("[SceneDiag] big '" + FullPath(r) + "' type=" + r.GetType().Name +
                  " active=" + r.gameObject.activeInHierarchy +
                  " center=" + b.center + " size=" + b.size +
                  " lossy=" + r.transform.lossyScale);
            }

            File.WriteAllText(DiagFile, sb.ToString());
            Debug.Log("[SceneDiag] escrito en " + DiagFile);
        }

        [MenuItem("Eira/Nivel 2/Diagnostico modelo FBX", priority = 21)]
        public static void DumpModel()
        {
            var sb = new System.Text.StringBuilder();
            System.Action<string> L = s => { Debug.Log(s); sb.AppendLine(s); };

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TargetFbx);
            if (fbx == null) { L("[FbxDiag] No se encontró " + TargetFbx); File.WriteAllText(DiagFile, sb.ToString()); return; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            inst.transform.position = Vector3.zero;
            inst.transform.rotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            L("[FbxDiag] ===== " + TargetFbx + " =====");
            L("[FbxDiag] bounds TOTAL: " + CombinedBounds(inst));
            L("[FbxDiag] total MeshFilters: " + inst.GetComponentsInChildren<MeshFilter>(true).Length);

            var list = new List<Renderer>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (r != null && !(r is ParticleSystemRenderer) && r.enabled) list.Add(r);

            var byY = new List<Renderer>(list);
            byY.Sort((a, b) => b.bounds.center.y.CompareTo(a.bounds.center.y));
            L("[FbxDiag] --- TOP 80 por centro Y (mas alto primero) ---");
            for (int k = 0; k < Mathf.Min(80, byY.Count); k++)
            {
                var b = byY[k].bounds;
                L("[FbxDiag] topY #" + k + " '" + FullPath(byY[k]) + "' center=" + b.center + " size=" + b.size);
            }

            var bySize = new List<Renderer>(list);
            bySize.Sort((a, b) => b.bounds.size.y.CompareTo(a.bounds.size.y));
            L("[FbxDiag] --- TOP 40 por tamano Y ---");
            for (int k = 0; k < Mathf.Min(40, bySize.Count); k++)
            {
                var b = bySize[k].bounds;
                L("[FbxDiag] tallY #" + k + " '" + FullPath(bySize[k]) + "' center=" + b.center + " size=" + b.size);
            }

            File.WriteAllText(DiagFile, sb.ToString());
            Object.DestroyImmediate(inst);
        }

        static string FullPath(Renderer r)
        {
            string p = r.name;
            var t = r.transform.parent;
            int guard = 0;
            while (t != null && guard++ < 8) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }

        [MenuItem("Eira/Nivel 2/Vaciar escenario (borrar TODO)", priority = 40)]
        public static void EmptyLevel()
        {
            if (!Application.isBatchMode)
            {
                if (!EditorUtility.DisplayDialog("Eira",
                        "¿Borrar TODOS los objetos del Nivel 2?\n\n" +
                        "Se guardará una copia de seguridad del escenario antes de vaciarlo.",
                        "Sí, vaciar", "Cancelar"))
                    return;
            }

            var report = new System.Text.StringBuilder();
            var sc = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

            string backup = BackupFolder + "/Level2Scene_backup_" +
                            System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            Directory.CreateDirectory(BackupFolder);
            File.Copy(Level2Scene, backup, true);
            report.AppendLine("Copia de seguridad: " + backup);

            int n = 0;
            foreach (var root in sc.GetRootGameObjects())
            {
                report.AppendLine("Eliminado: '" + root.name + "'");
                Object.DestroyImmediate(root);
                n++;
            }

            EditorSceneManager.MarkSceneDirty(sc);
            EditorSceneManager.SaveScene(sc);
            report.AppendLine("Objetos raíz eliminados: " + n);
            report.AppendLine("Nivel 2 vaciado. TERMINADO.");
            File.WriteAllText(ReportFile, report.ToString());
            Debug.Log("[EmptyLevel] " + report);

            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Eira", "Nivel 2 vaciado (" + n + " objetos).\nCopia en:\n" + backup, "OK");
        }

        // =========================================================
        // Colocación
        // =========================================================

        static void FitEnvironment(GameObject instance, System.Text.StringBuilder report)
        {
            var rends = new List<Renderer>();
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
                if (r != null && !(r is ParticleSystemRenderer) && r.enabled) rends.Add(r);

            if (rends.Count == 0)
            {
                report.AppendLine("AVISO: el nuevo modelo no tiene renderers; no se ajusta.");
                return;
            }

            // Escala fija (ajustable con ModelScale). La ciudad original mide
            // ~450 u de ancho con edificios de ~150 u de alto; a 0.25 queda una
            // ciudad de ~112 u con edificios de ~37 u, adecuada para el jugador.
            float s = ModelScale;
            instance.transform.localScale = Vector3.one * s;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.position = Vector3.zero;

            // El modelo trae una gran "Ocean" (3200x800) que es el suelo/plano
            // base; el "centro de la ciudad" lo tomamos de la mediana de los
            // centros de todos los renderers (robusta frente a valores atípicos).
            Renderer ground = null;
            float bestFootprint = -1f;
            var xs = new List<float>();
            var zs = new List<float>();
            foreach (var r in rends)
            {
                var b = r.bounds;
                xs.Add(b.center.x);
                zs.Add(b.center.z);
                float footprint = b.size.x * b.size.z;
                if (footprint > bestFootprint) { bestFootprint = footprint; ground = r; }
            }
            xs.Sort(); zs.Sort();
            float medX = xs[xs.Count / 2];
            float medZ = zs[zs.Count / 2];
            float groundTop = ground != null ? ground.bounds.max.y : 0f;

            instance.transform.position += new Vector3(-medX, -groundTop, -medZ);

            report.AppendLine("Escala aplicada: " + s);
            report.AppendLine("Suelo (mayor footprint): '" + (ground != null ? ground.name : "?") + "' top=" + groundTop);
            report.AppendLine("Centro (mediana x,z): " + medX + ", " + medZ);
            report.AppendLine("Bounds finales: " + CombinedBounds(instance));
        }

        static Bounds CombinedBounds(GameObject root)
        {
            var rends = root.GetComponentsInChildren<Renderer>(true);
            bool init = false;
            var b = new Bounds();
            foreach (var r in rends)
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!r.enabled) continue;
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!init) b = new Bounds(root.transform.position, Vector3.zero);
            return b;
        }

        static int EnsureColliders(Transform root)
        {
            int added = 0;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.GetComponent<MeshCollider>() == null)
                {
                    mf.gameObject.AddComponent<MeshCollider>();
                    added++;
                }
            }
            return added;
        }

        // =========================================================
        // Materiales -> URP/Lit
        // =========================================================

        static int UpgradeMaterials(GameObject root)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                Debug.LogWarning("[Level2CitySwap] No existe 'Universal Render Pipeline/Lit'.");
                return 0;
            }

            var map = new Dictionary<Material, Material>();
            int converted = 0;

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    if (m.shader == lit) continue;

                    Material target;
                    if (!map.TryGetValue(m, out target))
                    {
                        target = BuildLitMaterial(m, lit);
                        map[m] = target;
                        converted++;
                    }
                    mats[i] = target;
                    changed = true;
                }
                if (changed) r.sharedMaterials = mats;
            }

            AssetDatabase.SaveAssets();
            return converted;
        }

        static Material BuildLitMaterial(Material src, Shader lit)
        {
            var dst = new Material(lit);
            dst.name = src.name + "_URP";

            var mainTex = src.HasProperty("_MainTex") ? src.GetTexture("_MainTex") : null;
            var mainColor = src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white;
            float smoothness = src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.3f;
            float metallic = src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f;
            var bumpTex = src.HasProperty("_BumpMap") ? src.GetTexture("_BumpMap") : null;
            var emissiveColor = src.HasProperty("_EmissionColor") ? src.GetColor("_EmissionColor") : Color.black;
            var emissiveMap = src.HasProperty("_EmissionMap") ? src.GetTexture("_EmissionMap") : null;
            bool emissionOn = emissiveColor.maxColorComponent > 0.003f || emissiveMap != null;
            bool transparent = (src.HasProperty("_Mode") && src.GetFloat("_Mode") >= 3f) || src.renderQueue >= 3000;

            if (mainTex != null)
            {
                dst.SetTexture("_BaseMap", mainTex);
                if (src.HasProperty("_MainTex"))
                {
                    dst.SetTextureOffset("_BaseMap", src.GetTextureOffset("_MainTex"));
                    dst.SetTextureScale("_BaseMap", src.GetTextureScale("_MainTex"));
                }
            }
            dst.SetColor("_BaseColor", mainColor);
            dst.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            dst.SetFloat("_Metallic", Mathf.Clamp01(metallic));

            if (bumpTex != null)
            {
                dst.SetTexture("_BumpMap", bumpTex);
                dst.SetFloat("_BumpScale", src.HasProperty("_BumpScale") ? src.GetFloat("_BumpScale") : 1f);
                dst.EnableKeyword("_NORMALMAP");
            }

            if (emissionOn)
            {
                dst.SetColor("_EmissionColor", emissiveColor);
                if (emissiveMap != null) dst.SetTexture("_EmissionMap", emissiveMap);
                dst.EnableKeyword("_EMISSION");
                dst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (transparent)
            {
                dst.SetFloat("_Surface", 1f);
                dst.SetFloat("_Blend", 0f);
                dst.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                dst.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                dst.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                dst.SetOverrideTag("RenderType", "Transparent");
                dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                dst.SetFloat("_Surface", 0f);
                dst.SetOverrideTag("RenderType", "Opaque");
                dst.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath(MaterialFolder + "/" + Sanitize(src.name) + ".mat");
            AssetDatabase.CreateAsset(dst, path);
            return dst;
        }

        static string Sanitize(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return string.IsNullOrEmpty(name) ? "Material" : name;
        }

        // =========================================================
        // Borrado del escenario antiguo
        // =========================================================

        static void DeleteOldAssets(System.Text.StringBuilder report)
        {
            foreach (var path in new[] { OldPrefabPath, OldModelFolder, OldMaterialFolder })
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null ||
                    AssetDatabase.IsValidFolder(path) ||
                    File.Exists(path))
                {
                    if (AssetDatabase.DeleteAsset(path))
                        report.AppendLine("Asset antiguo eliminado: " + path);
                    else
                        report.AppendLine("AVISO: no se pudo eliminar: " + path);
                }
                else
                {
                    report.AppendLine("Asset antiguo no encontrado (se omite): " + path);
                }
            }
        }
    }
}
