using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    /// <summary>
    /// Normaliza todas las rotaciones guardadas en el proyecto.
    ///
    /// El aviso "QuaternionToEuler: Input quaternion was not normalized" no
    /// viene de un script en runtime: son assets ya importados que guardan un
    /// quaternion con módulo distinto de 1 (los importadores GLB de Eira
    /// escribían el valor crudo del JSON). El Inspector y la SceneView los
    /// convierten a euler al dibujarse y por eso el error aparece en
    /// UnityEditor.GUIView:ProcessEvent.
    ///
    /// Arregla escenas abiertas y todos los prefabs de Assets/, y es
    /// idempotente.
    /// </summary>
    public static class NormalizeRotationsRepair
    {
        /// <summary>
        /// Red de seguridad automática.
        ///
        /// El aviso aparece en el Inspector, que dibuja cualquier Transform
        /// seleccionado. Si un quaternion llega mal normalizado a la escena
        /// (un reimport del GLB, un objeto arrastrado, un script que multiplica
        /// rotaciones), el error salta en cada repintado de la ventana y es
        /// fácil que se despiste: la escena ya abierta en el editor sigue
        /// teniendo el valor malo aunque el archivo del disco esté limpio.
        ///
        /// Por eso, además del menú manual, se normaliza al abrir escena y
        /// tras recompilar, sin exigir que nadie se acuerde.
        /// </summary>
        [InitializeOnLoadMethod]
        static void HookAutoFix()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
            AssemblyReloadEvents.afterAssemblyReload += OnAssemblyReloaded;
        }

        static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
        {
            // En delayCall: durante sceneOpened todavía no se puede tocar la
            // jerarquía,Unity lo rechaza con "transform assignment" fuera de
            // ciclo.
            EditorApplication.delayCall += () => FixOpenScenesInMemory(true);
        }

        static void OnAssemblyReloaded()
        {
            EditorApplication.delayCall += () => FixOpenScenesInMemory(true);
        }

        /// <summary>
        /// Normaliza las rotaciones de las escenas ya cargadas en memoria.
        /// Con auto=true marca la escena como sucia para que el arreglo se
        /// pueda guardar.
        /// </summary>
        static int FixOpenScenesInMemory(bool auto)
        {
            int fixedTotal = 0;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                int fixedHere = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        var q = t.localRotation;
                        if (GlbTransformUtil.IsNormalized(q)) continue;
                        var fixedQ = GlbTransformUtil.NormalizeSafe(q);
                        if (!auto) Undo.RecordObject(t, "Normalizar rotacion");
                        t.localRotation = fixedQ;
                        fixedHere++;
                        Debug.LogWarning(
                            $"[Eira] Rotacion no normalizada corregida en '{scene.name}': " +
                            $"{GetPath(t.transform)} (modulo {Magnitude(q):0.000000}).",
                            t);
                    }
                }

                if (fixedHere > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    fixedTotal += fixedHere;
                }
            }

            if (fixedTotal > 0)
                Debug.LogWarning($"[Eira] {fixedTotal} rotacion(es) normalizada(s) automaticamente.");
            return fixedTotal;
        }

        static float Magnitude(Quaternion q)
        {
            return Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
        }

        [MenuItem("Eira/Normalizar rotaciones %#n")]
        public static void RepairAll()
        {
            int prefabs = FixPrefabs();
            int scenes = FixOpenScenesInMemory(false);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[Eira] Rotaciones normalizadas: {prefabs} prefabs, {scenes} transformaciones en escena.");
        }

        [MenuItem("Eira/Informar rotaciones no normalizadas %#j")]
        public static void ReportOnly()
        {
            var bad = FindBadPrefabs();
            foreach (var path in bad)
                Debug.LogWarning($"[Eira] Rotación no normalizada en prefab: {path}");

            var sceneBad = FindBadInOpenScenes();
            foreach (var name in sceneBad)
                Debug.LogWarning($"[Eira] Rotación no normalizada en escena: {name}");

            var clipBad = FindBadClips();
            foreach (var name in clipBad)
                Debug.LogWarning($"[Eira] Quaternion no normalizada en clip: {name}");

            Debug.Log($"[Eira] Prefabs afectados: {bad.Count}. Escenas afectadas: {sceneBad.Count}. Clips afectados: {clipBad.Count}.");
        }

        /// <summary>
        /// Busca clips de animación con keys de quaternion fuera de módulo. No
        /// se pueden reescribir (vienen de FBX importados), pero el informe
        /// dice qué clips reimportar.
        /// </summary>
        static List<string> FindBadClips()
        {
            var result = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
                if (clip == null) continue;

                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform)) continue;
                    if (binding.propertyName.IndexOf("localRotation", System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                    var curves = new AnimationCurve[4];
                    for (int i = 0; i < 4; i++)
                    {
                        var sub = EditorCurveBinding.FloatCurve(binding.path, binding.type, binding.propertyName + "." + "xyzw"[i]);
                        curves[i] = AnimationUtility.GetEditorCurve(clip, sub);
                    }
                    if (curves[0] == null || curves[3] == null) continue;

                    int count = Mathf.Min(Mathf.Min(curves[0].length, curves[1].length), Mathf.Min(curves[2].length, curves[3].length));
                    for (int k = 0; k < count; k++)
                    {
                        float x = curves[0].keys[k].value, y = curves[1].keys[k].value;
                        float z = curves[2].keys[k].value, w = curves[3].keys[k].value;
                        float mag = Mathf.Sqrt(x * x + y * y + z * z + w * w);
                        if (float.IsNaN(mag) || Mathf.Abs(mag - 1f) <= 0.0001f) continue;
                        result.Add($"{assetPath} :: {binding.path}/{binding.propertyName} key {k} modulo {mag:0.00000}");
                        break;
                    }
                }
            }
            return result;
        }

        static int FixPrefabs()
        {
            int fixedCount = 0;
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(path);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root == null) continue;

                int fixedHere = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (GlbTransformUtil.Fix(t)) fixedHere++;

                if (fixedHere > 0)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    fixedCount++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            return fixedCount;
        }

        static List<string> FindBadPrefabs()
        {
            var result = new List<string>();
            foreach (var path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(path);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var root = PrefabUtility.LoadPrefabContents(assetPath);
                if (root == null) continue;

                bool bad = false;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (IsBad(t.localRotation)) { bad = true; break; }
                }
                PrefabUtility.UnloadPrefabContents(root);
                if (bad) result.Add(assetPath);
            }
            return result;
        }

        static List<string> FindBadInOpenScenes()
        {
            var result = new List<string>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (!IsBad(t.localRotation)) continue;
                        result.Add($"{scene.name} :: {GetPath(t.transform)}");
                        break;
                    }
                }
            }
            return result;
        }

        static bool IsBad(Quaternion q)
        {
            // Delegado en GlbTransformUtil para que el umbral viva en un solo
            // sitio. Antes se duplicaba el literal 0.0001f aquí y se quedó
            // desfasado respecto al importador: por eso el quaternion de
            // EiraCamera (modulo 1.000042) no lo reparaba nadie.
            return !GlbTransformUtil.IsNormalized(q);
        }

        static string GetPath(Transform t)
        {
            var names = new List<string>();
            while (t != null) { names.Add(t.name); t = t.parent; }
            names.Reverse();
            return string.Join("/", names.ToArray());
        }
    }
}
