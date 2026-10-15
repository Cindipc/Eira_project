using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Herramienta única para reorganizar los escenarios según la historia de Eira:
    ///
    ///  * Nivel 1 (Capítulo 1 – El Despertar): cambia su escenario por la
    ///    Estación Espacial ("Assets/Space Station Scene/Space Station Scene.fbx").
    ///    Se conserva TODO el gameplay actual (misiones, drones, Máquina Guardiana,
    ///    terminales, salida).
    ///
    ///  * Nivel 2: recibe el escenario que tenía el Nivel 1 (Laboratorio +
    ///    FuturisticCity_Environment). El escenario antiguo del Nivel 2 se
    ///    desactiva (no se borran assets) y se conserva la red de seguridad
    ///    del suelo.
    /// </summary>
    public static class LevelEnvironmentSwap
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string Level2Scene = "Assets/Eira/Scenes/Level2Scene.unity";
        const string LabPrefabPath = "Assets/Eira/Environment/LabAbandonado.prefab";
        const string StationFbxPath = "Assets/Space Station Scene/Space Station Scene.fbx";
        const string CityFbxPath = "Assets/Eira/Models/FuturisticCity.fbx";

        // Zona de juego del nivel: corredor en X desde 0 hasta ~120, a ras de suelo y=0.
        const float CorridorStartX = 0f;
        const float CorridorEndX = 120f;
        const float CorridorHalfWidth = 13f;
        const float StationTargetLength = 112f;

        [MenuItem("Eira/EnvironmentSwap/Verify")]
        public static void Verify()
        {
            DumpScene("Assets/Eira/Scenes/Level1Scene.unity", "LEVEL1");
            DumpScene("Assets/Eira/Scenes/Level2Scene.unity", "LEVEL2");
        }

        static void DumpScene(string path, string label)
        {
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[Verify] ==================== " + label + " : " + path + " ====================");
            Debug.Log("[Verify] rootCount=" + sc.rootCount);
            foreach (var go in sc.GetRootGameObjects())
            {
                Debug.Log("[Verify] ROOT: '" + go.name + "' active=" + go.activeSelf + " pos=" + go.transform.position);
                if (go.name == "SpaceStation_Environment" || go.name == "LabAbandonado" || go.name == "FuturisticCity_Environment")
                {
                    var rends = go.GetComponentsInChildren<Renderer>(true);
                    Debug.Log("[Verify]   " + go.name + " -> renderers(fisicos)=" + rends.Length +
                              " scale=" + go.transform.localScale + " rot=" + go.transform.eulerAngles);
                    var all = go.GetComponentsInChildren<Transform>(true);
                    Debug.Log("[Verify]   " + go.name + " -> hijos=" + all.Length);
                    int matCount = 0;
                    foreach (var r in rends)
                    {
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m == null) { Debug.Log("[Verify]   material NULO en " + r.name); continue; }
                            matCount++;
                            if (matCount <= 12)
                                Debug.Log("[Verify]   mat: " + m.name + " (shader: " + m.shader.name + ") en " + r.name);
                        }
                    }
                    Debug.Log("[Verify]   " + go.name + " -> materiales asignados=" + matCount);
                }
            }
            Debug.Log("[Verify] ==================== FIN " + label + " ====================");
        }

        [MenuItem("Eira/EnvironmentSwap/UpgradeStationMaterials")]
        public static void UpgradeStationMaterials()
        {
            var guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Space Station Scene" });
            int standardCount = 0;
            int converted = 0;
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m == null) continue;
                if (m.shader != null && m.shader.name == "Standard") standardCount++;
                if (m.shader != null && m.shader.name == "Standard") { ConvertToUrpLit(m); converted++; }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Upgrade] Materiales de la estación -> Standard encontrados: " + standardCount + ", convertidos a URP/Lit: " + converted + ".");
        }

        static void ConvertToUrpLit(Material m)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[Upgrade] No existe el shader 'Universal Render Pipeline/Lit' en este proyecto."); return; }
            if (m.shader == lit) return;

            var mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
            var mainColor = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            var mainOffset = (mainTex != null && m.HasProperty("_MainTex")) ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            var mainScale = (mainTex != null && m.HasProperty("_MainTex")) ? m.GetTextureScale("_MainTex") : Vector2.one;
            var bumpTex = m.HasProperty("_BumpMap") ? m.GetTexture("_BumpMap") : null;
            var bumpScale = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1f;
            var smoothness = m.HasProperty("_Glossiness") ? m.GetFloat("_Glossiness") : 0.5f;
            var metallic = m.HasProperty("_Metallic") ? m.GetFloat("_Metallic") : 0f;
            var emissiveColor = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            var emissiveMap = m.HasProperty("_EmissionMap") ? m.GetTexture("_EmissionMap") : null;
            bool transparent = (m.HasProperty("_Mode") && m.GetFloat("_Mode") >= 3f) || m.renderQueue >= 3000;
            bool emissionOn = emissiveColor.maxColorComponent > 0.003f || emissiveMap != null;
            string baseName = m.name;

            m.shader = lit;
            if (mainTex != null)
            {
                m.SetTexture("_BaseMap", mainTex);
                m.SetTextureOffset("_BaseMap", mainOffset);
                m.SetTextureScale("_BaseMap", mainScale);
            }
            m.SetColor("_BaseColor", mainColor);
            m.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            m.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (bumpTex != null)
            {
                m.SetTexture("_BumpMap", bumpTex);
                m.SetFloat("_BumpScale", bumpScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emissionOn)
            {
                m.SetColor("_EmissionColor", emissiveColor);
                if (emissiveMap != null) m.SetTexture("_EmissionMap", emissiveMap);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (transparent)
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_AlphaClip", 0f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                m.SetFloat("_Surface", 0f);
                m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.SetOverrideTag("RenderType", "Opaque");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            }

            m.name = baseName;
            EditorUtility.SetDirty(m);
            Debug.Log("[Upgrade] Convertido: " + baseName + (transparent ? " (transparente)" : "") + (emissionOn ? " (emisión)" : ""));
        }

        // =========================================================
        // RESTAURAR NIVEL 1 (escenario original) + NIVEL 2 DESDE CERO
        // =========================================================

        const string OriginalLevel1Backup = "Assets/_Recovery/0 (2).unity";

        [MenuItem("Eira/EnvironmentSwap/RestaurarL1ConstruirL2")]
        public static void RestoreLevel1AndRebuildLevel2()
        {
            try
            {
                // 1) Nivel 1 vuelve EXACTO a como estaba: se restaura la escena original.
                if (!System.IO.File.Exists(OriginalLevel1Backup))
                    throw new System.Exception("No existe el respaldo del Nivel 1 original: " + OriginalLevel1Backup);

                var raw = System.IO.File.ReadAllText(OriginalLevel1Backup);
                if (raw.Contains("SpaceStation_Environment") || raw.Contains("Station_Floor"))
                    throw new System.Exception("El respaldo contiene la estación; ABORTADO para no restaurar basura.");

                System.IO.File.Copy(OriginalLevel1Backup, Level1Scene, true);
                AssetDatabase.ImportAsset(Level1Scene, ImportAssetOptions.ForceUpdate);
                Debug.Log("[Restore] Nivel 1 restaurado a su escenario original (LabAbandonado + FuturisticCity_Environment).");

                // 2) Nivel 2: se limpia y se construye un escenario NUEVO desde cero.
                var scene = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

                string[] remove = { "LabAbandonado", "FuturisticCity_Environment", "Escenografia", "Laboratorio", "GaussianEnvironment", "HuecosFillFloor", "Floor_Gaps", "CoolCorridor" };
                foreach (var n in remove)
                {
                    var go = GameObject.Find(n);
                    if (go != null)
                    {
                        Object.DestroyImmediate(go);
                        Debug.Log("[BuildNV2] Escenario del Nivel 2 anterior eliminado: " + n);
                    }
                }

                BuildLevel2EnvironmentFromScratch();

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[BuildNV2] Nivel 2 guardado con escenario NUEVO construido desde cero.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Restore] ERROR: " + ex);
                throw;
            }
        }

        static void BuildLevel2EnvironmentFromScratch()
        {
            var root = new GameObject("Escenario_Nivel2_DesdeCero");
            root.transform.position = Vector3.zero;
            root.transform.rotation = Quaternion.identity;

            var floorMat = CreateLitMaterial("Mat_Suelo_NV2", new Color(0.10f, 0.12f, 0.16f, 1f));
            var wallMat = CreateLitMaterial("Mat_Pared_NV2", new Color(0.24f, 0.27f, 0.34f, 1f));
            var accentMat = CreateLitMaterial("Mat_Acento_NV2", new Color(0.85f, 0.47f, 0.12f, 1f));
            var glowCyan = CreateLitMaterial("Mat_GlowCyan_NV2", new Color(0.02f, 0.06f, 0.08f, 1f), new Color(0.30f, 1f, 1f, 1f));
            var glowAmber = CreateLitMaterial("Mat_GlowAmbar_NV2", new Color(0.10f, 0.05f, 0.01f, 1f), new Color(1f, 0.55f, 0.10f, 1f));

            // Suelo (y=0 arriba) y paredes laterales que acotan el corredor jugable.
            Primitive(PrimitiveType.Cube, "Suelo", root.transform, new Vector3(60f, -0.2f, 0f), new Vector3(132f, 0.4f, 16f), Quaternion.identity, floorMat);
            Primitive(PrimitiveType.Cube, "Muro_Zmenos", root.transform, new Vector3(60f, 4.5f, -8.1f), new Vector3(132f, 11f, 0.5f), Quaternion.identity, wallMat);
            Primitive(PrimitiveType.Cube, "Muro_Zmas", root.transform, new Vector3(60f, 4.5f, 8.1f), new Vector3(132f, 11f, 0.5f), Quaternion.identity, wallMat);

            // Tiras emisivas a lo largo de las paredes.
            Primitive(PrimitiveType.Cube, "TiraGlow_Neg", root.transform, new Vector3(60f, 3.6f, -7.9f), new Vector3(132f, 0.12f, 0.06f), Quaternion.identity, glowCyan);
            Primitive(PrimitiveType.Cube, "TiraGlow_Pos", root.transform, new Vector3(60f, 3.6f, 7.9f), new Vector3(132f, 0.12f, 0.06f), Quaternion.identity, glowCyan);

            // Pilares cada 10 m a ambos lados, con anillo emisivo.
            for (int x = 0; x <= 120; x += 10)
            {
                Primitive(PrimitiveType.Cylinder, "Pilar_neg" + x, root.transform, new Vector3(x, 2.2f, -7.4f), Vector3.one, Quaternion.identity, wallMat);
                Primitive(PrimitiveType.Cylinder, "Pilar_pos" + x, root.transform, new Vector3(x, 2.2f, 7.4f), Vector3.one, Quaternion.identity, wallMat);
                Primitive(PrimitiveType.Cube, "Anillo_neg" + x, root.transform, new Vector3(x, 1.6f, -7.4f), new Vector3(0.14f, 0.07f, 0.14f), Quaternion.identity, glowAmber);
                Primitive(PrimitiveType.Cube, "Anillo_pos" + x, root.transform, new Vector3(x, 1.6f, 7.4f), new Vector3(0.14f, 0.07f, 0.14f), Quaternion.identity, glowAmber);
            }

            // Cajas bajas pegadas a los muros (no bloquean el pasillo central).
            int[] crateX = { 16, 24, 32, 40, 52, 60, 68, 76, 84 };
            for (int i = 0; i < crateX.Length; i++)
            {
                float cz = (i % 2 == 0) ? -5.3f : 5.3f;
                Primitive(PrimitiveType.Cube, "Caja_" + crateX[i], root.transform, new Vector3(crateX[i], 0.75f, cz), new Vector3(2.4f, 1.5f, 0.7f), Quaternion.identity, accentMat);
            }

            // Arco de entrada junto al spawn.
            Primitive(PrimitiveType.Cube, "Arco_Izq", root.transform, new Vector3(5.5f, 2.4f, -7.4f), new Vector3(0.7f, 4.8f, 0.7f), Quaternion.identity, wallMat);
            Primitive(PrimitiveType.Cube, "Arco_Der", root.transform, new Vector3(6.5f, 2.4f, 7.4f), new Vector3(0.7f, 4.8f, 0.7f), Quaternion.identity, wallMat);

            // 4 pilares grandes para la arena del jefe (final del corredor, sin tapar la salida).
            for (int p = 0; p < 4; p++)
            {
                float px = p < 2 ? 98f : 112f;
                float pz = (p % 2 == 0) ? -5.8f : 5.8f;
                Primitive(PrimitiveType.Cube, "PilonJefe_" + p, root.transform, new Vector3(px, 2.0f, pz), new Vector3(2f, 4f, 2f), Quaternion.identity, wallMat);
                Primitive(PrimitiveType.Cube, "PilonJefeAro_" + p, root.transform, new Vector3(px, 2.0f, pz), new Vector3(0.3f, 2.2f, 0.3f), Quaternion.identity, glowAmber);
            }

            // Iluminación puntual ambience (el neón/niebla lo pone EnvironmentDressing al jugar).
            foreach (var lp in new[] { 25f, 65f, 105f })
            {
                var lightGo = new GameObject("LuzPuntual_" + lp);
                lightGo.transform.SetParent(root.transform, false);
                lightGo.transform.localPosition = new Vector3(lp, 3.4f, 0f);
                var lt = lightGo.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.75f, 0.85f, 1f);
                lt.intensity = 2.2f;
                lt.range = 18f;
            }

            Debug.Log("[BuildNV2] Escenario NUEVO creado desde cero: " + root.name);
        }

        static Material CreateLitMaterial(string name, Color baseColor, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader);
            m.name = name;
            m.SetColor("_BaseColor", baseColor);
            m.SetFloat("_Smoothness", 0.4f);
            m.SetFloat("_Metallic", 0f);
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            if (emission.HasValue)
            {
                m.SetColor("_EmissionColor", emission.Value);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            go.transform.localRotation = localRot;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        [MenuItem("Eira/EnvironmentSwap/Run")]
        public static void Run()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

                var labData = RebuildLevel1();
                RebuildLevel2(labData);
                AddNextLevelToBuildSettings();

                Debug.Log("[EnvironmentSwap] TERMINADO con éxito.");
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(Level1Scene);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[EnvironmentSwap] ERROR: " + ex);
                throw;
            }
        }

        // =========================================================
        // NIVEL 1 -> ESTACIÓN ESPACIAL
        // =========================================================

        class LabData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 cityPosition;
            public Quaternion cityRotation;
            public Vector3 cityScale;
        }

        static LabData RebuildLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

            var lab = GameObject.Find("LabAbandonado");
            var city = GameObject.Find("FuturisticCity_Environment");

            if (lab == null) throw new System.Exception("No se encontró 'LabAbandonado' en Level1Scene.");
            if (city == null) throw new System.Exception("No se encontró 'FuturisticCity_Environment' en Level1Scene.");

            var data = new LabData();

            // 1) Guardar el escenario actual (no se borra: pasa al Nivel 2).
            data.position = lab.transform.position;
            data.rotation = lab.transform.rotation;
            Debug.Log("[EnvironmentSwap] Guardando LabAbandonado como prefab en: " + LabPrefabPath);
            var saved = PrefabUtility.SaveAsPrefabAsset(lab, LabPrefabPath);
            if (saved == null) throw new System.Exception("Falló SaveAsPrefabAsset(LabAbandonado).");

            data.cityPosition = city.transform.position;
            data.cityRotation = city.transform.rotation;
            data.cityScale = city.transform.localScale;
            Debug.Log("[EnvironmentSwap] Ciudad FuturisticCity -> pos=" + data.cityPosition + " scale=" + data.cityScale);

            // 2) Quitar el escenario antiguo del Nivel 1.
            Object.DestroyImmediate(city);
            Object.DestroyImmediate(lab);

            // 3) Añadir la Estación Espacial como nuevo escenario.
            var stationPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StationFbxPath);
            if (stationPrefab == null) throw new System.Exception("No se pudo cargar la Estación Espacial: " + StationFbxPath);

            var station = (GameObject)PrefabUtility.InstantiatePrefab(stationPrefab);
            station.name = "SpaceStation_Environment";

            // Quitar animaciones heredadas del modelo que puedan mover la escena.
            var animator = station.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            var legacyAnim = station.GetComponent<Animation>();
            if (legacyAnim != null) Object.DestroyImmediate(legacyAnim);

            FitStationToCorridor(station);

            CreateFloorAndWalls();

            // Luces decorativas de la antigua ciudad (flotaban en el aire de la estación).
            foreach (var name in new[] { "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnvironmentSwap] Nivel 1 guardado: escenario = Estación Espacial.");
            return data;
        }

        static void FitStationToCorridor(GameObject station)
        {
            var stationT = station.transform;
            Bounds b = CombinedBounds(station);

            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[EnvironmentSwap] La estación no tiene renderers; no se ajusta.");
                return;
            }

            // Escala uniforme para que el lado largo de la estación abarque el corredor.
            float longAxis = Mathf.Max(b.size.x, b.size.z);
            float s = StationTargetLength / Mathf.Max(longAxis, 0.001f);
            stationT.localScale = new Vector3(
                stationT.localScale.x * s,
                stationT.localScale.y * s,
                stationT.localScale.z * s);

            b = CombinedBounds(station);

            // Alinear el lado largo con el eje X del corredor (si hace falta).
            if (b.size.z > b.size.x)
            {
                stationT.rotation = Quaternion.Euler(0f, 90f, 0f) * stationT.rotation;
                b = CombinedBounds(station);
            }

            // Centrar en el corredor y apoyar el suelo de la estación en y=0.
            float targetX = (CorridorStartX + CorridorEndX) * 0.5f;
            Vector3 shift = new Vector3(targetX - b.center.x, -b.min.y, -b.center.z);
            stationT.position += shift;

            b = CombinedBounds(station);
            Debug.Log("[EnvironmentSwap] Estación ajustada. Centro=" + b.center + " Tamaño=" + b.size);
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

        /// <summary>
        /// Suelo visible + colisión para que Eira no caiga, más dos paredes
        /// invisibles para no salirse del corredor.
        /// </summary>
        static void CreateFloorAndWalls()
        {
            float centerX = (CorridorStartX + CorridorEndX) * 0.5f;
            float length = CorridorEndX - CorridorStartX + 12f;

            Material floorMat = new Material(Shader.Find("Standard") != null ? Shader.Find("Standard") : Shader.Find("Unlit/Color"));
            floorMat.name = "Mat_Escenario_Estacion_Suelo";
            floorMat.color = new Color(0.10f, 0.13f, 0.20f, 1f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Station_Floor";
            floor.transform.SetPositionAndRotation(new Vector3(centerX, -0.2f, 0f), Quaternion.identity);
            floor.transform.localScale = new Vector3(length, 0.4f, CorridorHalfWidth * 2f + 2f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            floor.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            floor.GetComponent<Renderer>().receiveShadows = true;

            // Paredes invisibles laterales (solo colisión) para acotar el jugable.
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? -(CorridorHalfWidth + 1f) : (CorridorHalfWidth + 1f);
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Station_Wall_" + side;
                wall.transform.SetPositionAndRotation(new Vector3(centerX, 4f, z), Quaternion.identity);
                wall.transform.localScale = new Vector3(length, 9f, 1f);
                var wr = wall.GetComponent<Renderer>();
                if (wr != null) Object.DestroyImmediate(wr);
            }
        }

        // =========================================================
        // NIVEL 2 -> RECIBE EL ESCENARIO DEL NIVEL 1
        // =========================================================

        static void RebuildLevel2(LabData data)
        {
            var scene = EditorSceneManager.OpenScene(Level2Scene, OpenSceneMode.Single);

            // Desactivar el escenario antiguo del Nivel 2 (se conservan los assets).
            string[] oldEnv = { "Escenografia", "Laboratorio", "GaussianEnvironment", "HuecosFillFloor" };
            foreach (var n in oldEnv)
            {
                var go = GameObject.Find(n);
                if (go != null)
                {
                    go.SetActive(false);
                    Debug.Log("[EnvironmentSwap] Nivel 2: escenario antiguo desactivado -> " + n);
                }
            }

            // El escenario que tenía el Nivel 1 ahora es el del Nivel 2.
            var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabPrefabPath);
            if (labPrefab == null) throw new System.Exception("No se encontró el prefab del laboratorio: " + LabPrefabPath);

            var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
            lab.name = "LabAbandonado";
            lab.transform.position = data.position;
            lab.transform.rotation = data.rotation;

            var cityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CityFbxPath);
            if (cityPrefab == null) throw new System.Exception("No se encontró FuturisticCity.fbx: " + CityFbxPath);

            var city = (GameObject)PrefabUtility.InstantiatePrefab(cityPrefab);
            city.name = "FuturisticCity_Environment";
            city.transform.position = data.cityPosition;
            city.transform.rotation = data.cityRotation;
            city.transform.localScale = data.cityScale;

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[EnvironmentSwap] Nivel 2 guardado: escenario = antiguo Nivel 1 (Lab + FuturisticCity).");
        }

        // =========================================================
        // BUILD SETTINGS
        // =========================================================

        static void AddNextLevelToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            string level2Guid = AssetDatabase.AssetPathToGUID(Level2Scene);

            bool hasLevel2 = scenes.Exists(s => string.Equals(s.path, Level2Scene, System.StringComparison.OrdinalIgnoreCase));
            if (!hasLevel2)
            {
                scenes.Add(new EditorBuildSettingsScene(Level2Scene, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[EnvironmentSwap] Level2Scene agregada a Build Settings.");
            }
            else
            {
                Debug.Log("[EnvironmentSwap] Level2Scene ya estaba en Build Settings.");
            }

            if (string.IsNullOrEmpty(level2Guid)) Debug.LogWarning("[EnvironmentSwap] No se pudo resolver el GUID de Level2Scene.");
        }
    }
}