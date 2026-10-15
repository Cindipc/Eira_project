using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Cambia el escenario del Nivel 1 por la planta de sulfonación
    /// (suphonation_plant -> Assets/Eira/Models/SuphonationPlant/SuphonationPlant.fbx)
    /// y mueve el escenario actual del Nivel 1 (SecretLaboratory_Environment)
    /// al Nivel 2, con la COLOCACIÓN CORREGIDA: el suelo transitable queda a ras
    /// de y=0 y el recinto empieza en x=1 para que el spawn de Eira (x=6) quede
    /// dentro del escenario (antes la sala flotaba a y~5,2 y Eira/NOVA quedaban
    /// por debajo del escenario).
    /// </summary>
    public static class SuphonationPlantSwap
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string Level2Scene = "Assets/Eira/Scenes/Level2Scene.unity";
        const string LabPrefabPath = "Assets/Eira/Environment/SecretLaboratory.prefab";
        const string PlantFbxPath = "Assets/Eira/Models/SuphonationPlant/SuphonationPlant.fbx";
        const string MatFolder = "Assets/Eira/Materials/SuphonationPlant";

        const float CorridorStartX = 0f;
        const float CorridorEndX = 120f;
        const float CorridorHalfWidth = 13f;
        const float CorridorWidth = CorridorHalfWidth * 2f;

        [MenuItem("Eira/SuphonationPlant/Swap")]
        public static void Swap()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                RebuildLevel1();
                RebuildLevel2();

                Debug.Log("[Supho] SWAP TERMINADO con éxito.");
                Verify();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Supho] SWAP ERROR: " + ex);
                throw;
            }
        }

        // =========================================================
        // NIVEL 1 -> PLANTA DE SULFONACIÓN
        // =========================================================

        static void RebuildLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

            var lab = GameObject.Find("SecretLaboratory_Environment");
            if (lab == null)
            {
                // Swap ya aplicado (Nivel 1 = planta). Solo se aseguran los extras.
                Debug.Log("[Supho] Nivel 1 ya actualizado: no hay laboratorio que capturar.");
            }
            else
            {
                // 1) Corregir el laboratorio ANTES de guardarlo: suelo a y=0 y recinto desde x=1.
                PlaceEnvRoot(lab, "Cube.001", false);

                Debug.Log("[Supho] Guardando laboratorio corregido como prefab: " + LabPrefabPath);
                var saved = PrefabUtility.SaveAsPrefabAsset(lab, LabPrefabPath);
                if (saved == null) throw new System.Exception("Falló SaveAsPrefabAsset(SecretLaboratory).");

                Object.DestroyImmediate(lab);

                // 2) Colocar la planta de sulfonación.
                var plantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlantFbxPath);
                if (plantPrefab == null) throw new System.Exception("No se pudo cargar el FBX de la planta: " + PlantFbxPath);

                var plant = (GameObject)PrefabUtility.InstantiatePrefab(plantPrefab);
                plant.name = "SuphonationPlant_Environment";

                var animator = plant.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
                var legacy = plant.GetComponent<Animation>();
                if (legacy != null) Object.DestroyImmediate(legacy);

                FitPlantToCorridor(plant);
                AssignPlantMaterials(plant);
                AddPlantLights(plant);
            }

            // Las luces de zona del viejo escenario no aplican aquí.
            foreach (var name in new[] { "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle", "SunDia", "SunTarde" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            CreateCorridorFloor();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Supho] Nivel 1 guardado: escenario = Suphonation Plant.");
        }

        static void FitPlantToCorridor(GameObject root)
        {
            var t = root.transform;
            Bounds b = CombinedBounds(root);
            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[Supho] La planta no tiene renderers; no se ajusta.");
                return;
            }

            // Escala UNIFORME para que la anchura (Z) llene el corredor.
            float s = CorridorWidth / Mathf.Max(Mathf.Max(b.size.x, b.size.z), 0.001f);
            t.localScale = new Vector3(t.localScale.x * s, t.localScale.y * s, t.localScale.z * s);

            b = CombinedBounds(root);
            // Alinear el lado largo con el eje X del corredor.
            if (b.size.z > b.size.x)
            {
                t.rotation = Quaternion.Euler(0f, 90f, 0f) * t.rotation;
                b = CombinedBounds(root);
            }

            // Suelo transitable: la rejilla del catwalk (honeycomb.001), cuyas CARA
            // SUPERIOR debe quedar a y=0. Después se centra en Z y el recinto arranca
            // en x=1 para que el spawn de Eira (x=6) quede dentro.
            PlaceEnvRoot(root, "honeycomb.001", true);
        }

        // Colocación corregida del escenario sobre el corredor:
        //  - centra en Z (-z -> 0),
        //  - baja el suelo transitable hasta y=0 (cara superior o inferior del ancla),
        //  - desplaza en X para que min.x quede en x=1 (spawn de Eira x=6, dentro).
        static void PlaceEnvRoot(GameObject root, string floorAnchor, bool alignAnchorTop)
        {
            var t = root.transform;
            Bounds b = CombinedBounds(root);

            float zShift = -b.center.z;
            t.position += new Vector3(0f, 0f, zShift);
            b = CombinedBounds(root);

            var anchor = FindFloorAnchor(root, floorAnchor);
            float floorY = anchor ? (alignAnchorTop ? anchor.bounds.max.y : anchor.bounds.min.y) : b.min.y;
            Debug.Log("[Supho] Ancla de suelo: " + (anchor ? anchor.name : "(fallback bounds.min)") + " -> floorY=" + floorY);
            t.position += new Vector3(0f, -floorY, 0f);
            b = CombinedBounds(root);

            t.position += new Vector3(1f - b.min.x, 0f, 0f);

            b = CombinedBounds(root);
            Debug.Log("[Supho] Recinto '" + root.name + "' ajustado. Centro=" + b.center + " Tamaño=" + b.size + " min=" + b.min);
        }

        static Renderer FindFloorAnchor(GameObject root, string preferred)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name == preferred) return r;

            // Fallback robusto: la losa fina horizontal más baja con área grande.
            Renderer best = null;
            float bestScore = float.MaxValue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                if (b.size.y > 0.8f) continue;
                float area = b.size.x * b.size.z;
                if (area < 0.5f) continue;
                if (b.min.y < bestScore) { bestScore = b.min.y; best = r; }
            }
            if (best != null) Debug.LogWarning("[Supho] Ancla '" + preferred + "' no encontrada; se usa " + best.name + ".");
            return best;
        }

        // =========================================================
        // MATERIALES DE LA PLANTA (URP/Lit) por material del .blend
        // =========================================================

        static void AssignPlantMaterials(GameObject root)
        {
            var kuning = GetOrCreateMaterial("SPL_Kuning", new Color(1f, 0.82f, 0.2f, 1f), 0.6f, 0.1f);
            var oren = GetOrCreateMaterial("SPL_Oren", new Color(1f, 0.52f, 0.15f, 1f), 0.7f, 0.1f);
            var ijo = GetOrCreateMaterial("SPL_Ijo", new Color(0.25f, 0.75f, 0.42f, 1f), 0.5f, 0.1f);
            var krom = GetOrCreateMaterial("SPL_Krom", new Color(0.72f, 0.74f, 0.76f, 1f), 0.2f, 0.85f);
            var esp = GetOrCreateMaterial("SPL_Esp", new Color(0.55f, 0.6f, 0.65f, 1f), 0.4f, 0.4f);
            var biru = GetOrCreateMaterial("SPL_Biru", new Color(0.2f, 0.42f, 0.95f, 1f), 0.5f, 0.1f);
            var baseMat = GetOrCreateMaterial("SPL_Base", new Color(0.6f, 0.6f, 0.6f, 1f), 0.5f, 0.1f);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int k = 0, o = 0, i = 0, km = 0, e = 0, b = 0, base_ = 0;
            foreach (var r in renderers)
            {
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                Material mat;
                if (mn.Contains("kroom") || mn.Contains("krom")) { mat = krom; km++; }
                else if (mn.Contains("kuning")) { mat = kuning; k++; }
                else if (mn.Contains("oren")) { mat = oren; o++; }
                else if (mn.Contains("ijo")) { mat = ijo; i++; }
                else if (mn.Contains("esp")) { mat = esp; e++; }
                else if (mn.Contains("biru")) { mat = biru; b++; }
                else { mat = baseMat; base_++; }
                r.sharedMaterial = mat;
            }
            Debug.Log($"[Supho] Materiales asignados: kuning={k} oren={o} ijo={i} krom/kroom={km} esp={e} biru={b} base={base_} renderers={renderers.Length}");
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
        // LUCES DE LA PLANTA (el .blend no trae luces)
        // =========================================================

        static void AddPlantLights(GameObject root)
        {
            // Puntos cálidos sobre los tanques (posiciones MUNDO; ya está ajustado).
            var points = new[]
            {
                new Vector3(9f, 3.5f, -3.5f),
                new Vector3(20f, 4.5f, 2f),
                new Vector3(12f, 4f, 4f),
                new Vector3(20f, 3f, -4f),
            };
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject("SPL_LightPoint_" + i);
                go.transform.SetParent(root.transform, true);
                go.transform.position = points[i];
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.92f, 0.8f);
                lt.intensity = 1.5f;
                lt.range = 13f;
            }

            // Sol suave para iluminar la planta industrial.
            var sun = new GameObject("SPL_Sun");
            sun.transform.SetParent(root.transform, true);
            var dl = sun.AddComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(1f, 0.96f, 0.9f);
            dl.intensity = 0.85f;
            dl.transform.rotation = Quaternion.Euler(48f, 210f, 0f);
        }

        // =========================================================
        // SUELO DEL CORREDOR (se conserva si ya existe)
        // =========================================================

        static void CreateCorridorFloor()
        {
            if (GameObject.Find("SecretLab_Floor") != null)
            {
                Debug.Log("[Supho] Suelo del corredor ya existente; se conserva.");
                return;
            }

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
        // NIVEL 2 -> RECIBE EL LABORATORIO (CORREGIDO)
        // =========================================================

        static void RebuildLevel2()
        {
            var scene = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

            // Se destruyen también las raíces INACTIVAS (GameObject.Find no las encuentra).
            foreach (var rootObj in scene.GetRootGameObjects())
            {
                switch (rootObj.name)
                {
                    case "LabAbandonado":
                    case "FuturisticCity_Environment":
                    case "GaussianEnvironment":
                    case "Escenografia":
                    case "Laboratorio":
                    case "HuecosFillFloor":
                    case "SecretLaboratory_Environment":
                        string n = rootObj.name;
                        Object.DestroyImmediate(rootObj);
                        Debug.Log("[Supho] Nivel 2: escenario anterior eliminado -> " + n);
                        break;
                }
            }

            // Interior del laboratorio: apagar el día y las luces de zona.
            foreach (var name in new[] { "SunDia", "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            if (labPrefab == null) throw new System.Exception("Prefab del laboratorio no encontrado: " + LabPrefabPath);
            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.name = "SecretLaboratory_Environment";

            PlaceEnvRoot(lab, "Cube.001", false);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Supho] Nivel 2 guardado: escenario = Secret Laboratory (colocación corregida).");
        }

        // =========================================================
        // VERIFICACIÓN
        // =========================================================

        [MenuItem("Eira/SuphonationPlant/Verify")]
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
                foreach (var n in new[] { "SuphonationPlant_Environment", "SecretLaboratory_Environment", "Eira", "NOVA" })
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