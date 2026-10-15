using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Cambia el escenario del Nivel 1 por el "Secret Laboratory"
    /// (scene.blend de Blender exportado a FBX en
    /// Assets/Eira/Models/SecretLaboratory/SecretLaboratory.fbx) y mueve el
    /// escenario que tenía el Nivel 1 (LabAbandonado + FuturisticCity_Environment)
    /// al Nivel 2.
    ///
    /// Nivel 1: laboratorio secreto ajustado al corredor + suelo de juego
    /// (se conserva TODO el gameplay: misiones, drones, Máquina Guardiana,
    /// terminales, salida).
    ///
    /// Nivel 2: recibe el escenario del antiguo Nivel 1; se elimina el
    /// "Escenario_Nivel2_DesdeCero".
    /// </summary>
    public static class SecretLabSwap
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string Level2Scene = "Assets/Eira/Scenes/Level2Scene.unity";
        const string LabPrefabPath = "Assets/Eira/Environment/LabAbandonado.prefab";
        const string CityFbxPath = "Assets/Eira/Models/FuturisticCity.fbx";
        const string LabFbxPath = "Assets/Eira/Models/SecretLaboratory/SecretLaboratory.fbx";
        const string MatFolder = "Assets/Eira/Materials/SecretLab";

        // Zona de juego del nivel: corredor en X desde 0 hasta ~120, a ras de suelo y=0.
        const float CorridorStartX = 0f;
        const float CorridorEndX = 120f;
        const float CorridorHalfWidth = 13f;
        const float CorridorWidth = CorridorHalfWidth * 2f;

        class LabData
        {
            public Vector3 labPosition;
            public Quaternion labRotation;
            public Vector3 cityPosition;
            public Quaternion cityRotation;
            public Vector3 cityScale;
        }

        [MenuItem("Eira/SecretLab/Swap")]
        public static void Swap()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                var data = RebuildLevel1();
                RebuildLevel2(data);

                Debug.Log("[SecretLab] SWAP TERMINADO con éxito.");
                Verify();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[SecretLab] SWAP ERROR: " + ex);
                throw;
            }
        }

        // =========================================================
        // NIVEL 1 -> LABORATORIO SECRETO
        // =========================================================

        static LabData RebuildLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

            var lab = GameObject.Find("LabAbandonado");
            var city = GameObject.Find("FuturisticCity_Environment");

            if (lab == null) throw new System.Exception("No se encontró 'LabAbandonado' en Level1Scene.");
            if (city == null) throw new System.Exception("No se encontró 'FuturisticCity_Environment' in Level1Scene.");

            var data = new LabData();

            data.labPosition = lab.transform.position;
            data.labRotation = lab.transform.rotation;
            Debug.Log("[SecretLab] Guardando LabAbandonado como prefab en: " + LabPrefabPath);
            var saved = PrefabUtility.SaveAsPrefabAsset(lab, LabPrefabPath);
            if (saved == null) throw new System.Exception("Falló SaveAsPrefabAsset(LabAbandonado).");

            data.cityPosition = city.transform.position;
            data.cityRotation = city.transform.rotation;
            data.cityScale = city.transform.localScale;
            Debug.Log("[SecretLab] Ciudad FuturisticCity -> pos=" + data.cityPosition + " scale=" + data.cityScale);

            Object.DestroyImmediate(city);
            Object.DestroyImmediate(lab);

            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabFbxPath);
            if (labPrefab == null) throw new System.Exception("No se pudo cargar el FBX del laboratorio: " + LabFbxPath);

            var labRoot = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            labRoot.name = "SecretLaboratory_Environment";

            var animator = labRoot.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            var legacyAnim = labRoot.GetComponent<Animation>();
            if (legacyAnim != null) Object.DestroyImmediate(legacyAnim);

            FitLabToCorridor(labRoot);
            AssignLabMaterials(labRoot);
            AddLabLights(labRoot);

            // Las luces de zona del viejo escenario flotan en el aire del nuevo.
            foreach (var name in new[] { "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            CreateCorridorFloor();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SecretLab] Nivel 1 guardado: escenario = Secret Laboratory.");
            return data;
        }

        static void FitLabToCorridor(GameObject labRoot)
        {
            var t = labRoot.transform;
            Bounds b = CombinedBounds(labRoot);

            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[SecretLab] El laboratorio no tiene renderers; no se ajusta.");
                return;
            }

            // Escala UNIFORME para que la anchura (Z) de la sala llene el corredor.
            float s = CorridorWidth / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.001f);
            t.localScale = new Vector3(t.localScale.x * s, t.localScale.y * s, t.localScale.z * s);

            b = CombinedBounds(labRoot);

            // Alinear el lado largo con el eje X del corredor.
            if (b.size.z > b.size.x)
            {
                t.rotation = Quaternion.Euler(0f, 90f, 0f) * t.rotation;
                b = CombinedBounds(labRoot);
            }

            // El suelo de la sala queda a ras de suelo (y=0) y la entrada
            // (lado +X) queda abierta hacia el resto del corredor.
            float doorX = CorridorStartX + 6f;
            Vector3 shift = new Vector3(doorX - b.min.x, -b.min.y, 0f);
            t.position += shift;

            b = CombinedBounds(labRoot);
            Debug.Log("[SecretLab] Laboratorio ajustado. Centro=" + b.center + " Tamaño=" + b.size + " min=" + b.min);
        }

        // =========================================================
        // MATERIALES DEL LABORATORIO (URP/Lit)
        // =========================================================

        static void AssignLabMaterials(GameObject labRoot)
        {
            var floorMat = GetOrCreateMaterial("SL_Floor", new Color(0.08f, 0.09f, 0.11f, 1f), 0.35f, 0f);
            var panelMat = GetOrCreateMaterial("SL_Panel", new Color(0.16f, 0.18f, 0.22f, 1f), 0.45f, 0.2f);
            var pipeMat = GetOrCreateMaterial("SL_Pipe", new Color(0.35f, 0.45f, 0.55f, 1f), 0.75f, 0.8f);
            var reactorMat = GetOrCreateMaterial("SL_Reactor", new Color(0.02f, 0.10f, 0.12f, 1f), 0.6f, 0.1f,
                new Color(0.25f, 1f, 0.9f, 1f));

            var renderers = labRoot.GetComponentsInChildren<Renderer>(true);
            int floor = 0, panel = 0, pipe = 0, reactor = 0;
            foreach (var r in renderers)
            {
                string n = r.name;
                Material mat;
                if (n == "Cube.001" || n == "Cube.001.001") { mat = floorMat; floor++; }
                else if (n.StartsWith("Cube")) { mat = panelMat; panel++; }
                else if (IsReactor(n)) { mat = reactorMat; reactor++; }
                else { mat = pipeMat; pipe++; }
                r.sharedMaterial = mat;
            }
            Debug.Log($"[SecretLab] Materiales asignados: floor={floor} panel={panel} pipe={pipe} reactor={reactor} renderers={renderers.Length}");
        }

        static bool IsReactor(string name)
        {
            // La torre central del laboratorio (reactor): cilindro grande + envolventes + base.
            return name == "Cylinder.003" || name == "Cylinder.005" || name == "Cylinder.006" || name == "Cylinder.004";
        }

        static Material GetOrCreateMaterial(string name, Color baseColor, float smoothness, float metallic, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var path = MatFolder + "/" + name + ".mat";

            EnsureFolder(MatFolder);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m;
            if (existing != null)
            {
                m = existing;
            }
            else
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
            }

            m.name = name;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        // =========================================================
        // LUCES DEL LABORATORIO (replicadas del .blend)
        // =========================================================

        static void AddLabLights(GameObject labRoot)
        {
            // Posiciones locales del .blend (se escalan con el padre).
            var points = new[]
            {
                new Vector3(5.994f, 3.683f, 2.553f),
                new Vector3(-5.571f, 3.683f, 2.553f),
                new Vector3(2.155f, 3.683f, 2.553f),
                new Vector3(-1.992f, 3.683f, 2.553f),
            };
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject("SL_LightPoint_" + i);
                go.transform.SetParent(labRoot.transform, false);
                go.transform.localPosition = points[i];
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.7f, 0.85f, 1f);
                lt.intensity = 1.6f;
                lt.range = 14f;
            }

            // "Lamp" del .blend: direccional de relleno.
            var lamp = new GameObject("SL_Lamp");
            lamp.transform.SetParent(labRoot.transform, false);
            lamp.transform.localPosition = new Vector3(5.72f, -0.32f, 6.269f);
            var dl = lamp.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(0.8f, 0.9f, 1f);
            dl.intensity = 0.6f;
            dl.transform.localRotation = Quaternion.Euler(60f, 210f, 0f);
        }

        // =========================================================
        // SUELO DEL CORREDOR (sin escenario ya no hay donde caminar)
        // =========================================================

        static void CreateCorridorFloor()
        {
            float centerX = (CorridorStartX + CorridorEndX) * 0.5f;

            Material floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMat.name = "Mat_Escenario_SecretLab_Suelo";
            floorMat.SetColor("_BaseColor", new Color(0.06f, 0.07f, 0.09f, 1f));
            floorMat.SetFloat("_Smoothness", 0.3f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "SecretLab_Floor";
            floor.transform.SetPositionAndRotation(new Vector3(centerX, -0.2f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(
                CorridorEndX - CorridorStartX + 6f,
                0.4f,
                CorridorWidth + 2f);
            var fr = floor.GetComponent<Renderer>();
            fr.sharedMaterial = floorMat;
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fr.receiveShadows = true;

            // Paredes invisibles laterales (solo colisión) para no salirse del jugable.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -(CorridorHalfWidth + 1f) : (CorridorHalfWidth + 1f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "SecretLab_Wall_" + side;
                wall.transform.SetPositionAndRotation(new Vector3(centerX, 4f, z), Quaternion.identity);
                wall.transform.localScale = new Vector3(CorridorEndX - CorridorStartX + 6f, 9f, 1f);
                var wr = wall.GetComponent<Renderer>();
                if (wr != null) Object.DestroyImmediate(wr);
            }
        }

        // =========================================================
        // NIVEL 2 -> RECIBE EL ESCENARIO DEL ANTIGUO NIVEL 1
        // =========================================================

        static void RebuildLevel2(LabData data)
        {
            var scene = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

            foreach (var n in new[] { "Escenario_Nivel2_DesdeCero", "GaussianEnvironment", "Escenografia", "Laboratorio", "HuecosFillFloor" })
            {
                var go = GameObject.Find(n);
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                    Debug.Log("[SecretLab] Nivel 2: escenario anterior eliminado -> " + n);
                }
            }

            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            if (labPrefab == null) throw new System.Exception("No se encontró el prefab del laboratorio: " + LabPrefabPath);
            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.name = "LabAbandonado";
            lab.transform.position = data.labPosition;
            lab.transform.rotation = data.labRotation;

            var cityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityFbxPath);
            if (cityPrefab == null) throw new System.Exception("No se encontró FuturisticCity.fbx: " + CityFbxPath);
            var city = (GameObject)PrefabUtility.InstantiatePrefab(cityPrefab);
            city.name = "FuturisticCity_Environment";
            city.transform.position = data.cityPosition;
            city.transform.rotation = data.cityRotation;
            city.transform.localScale = data.cityScale;

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SecretLab] Nivel 2 guardado: escenario = antiguo Nivel 1 (Lab + FuturisticCity).");
        }

        // =========================================================
        // VERIFICACIÓN
        // =========================================================

        [MenuItem("Eira/SecretLab/Verify")]
        public static void Verify()
        {
            foreach (var p in new[] { Level1Scene, Level2Scene })
            {
                var sc = EditorSceneManager.OpenScene(p, OpenSceneMode.Single);
                Debug.Log("[Verify] ==================== " + sc.name + " : " + p + " ====================");
                Debug.Log("[Verify] rootCount=" + sc.rootCount);
                foreach (var go in sc.GetRootGameObjects())
                {
                    Debug.Log("[Verify] ROOT: '" + go.name + "' active=" + go.activeSelf +
                              " pos=" + go.transform.position + " scale=" + go.transform.localScale);
                }
                foreach (var n in new[] { "LabAbandonado", "FuturisticCity_Environment", "SecretLaboratory_Environment", "Eira" })
                {
                    foreach (var go in sc.GetRootGameObjects())
                    {
                        if (go.name != n) continue;
                        Bounds b = CombinedBounds(go);
                        Debug.Log("[Verify]   " + n + " bounds center=" + b.center + " size=" + b.size + " min=" + b.min);
                        break;
                    }
                }
            }
        }

        static Bounds CombinedBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
                return;

            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}