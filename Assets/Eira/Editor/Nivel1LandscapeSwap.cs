using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Rediseña el escenario del Nivel 1 con dos partes:
    ///  - Nivel1_Laboratorio: el pasillo del laboratorio (corridor.blend) que
    ///    recorre todo el nivel (x 0..120, suelo a y=0). Eira Spawnea DENTRO
    ///    del edificio y el flujo actual (misiones -> Guardián -> salida en el
    ///    extremo del pasillo) resuelve el nivel al salir del edificio.
    ///  - Nivel1_Paisaje: la maqueta toscana escalada y colocada a un lado
    ///    (lado +Z) como exterior visible.
    /// La planta de sulfonación se DESCARTA (se elimina). El Nivel 2 no se toca.
    /// El gameplay (Gameplay/Misiones/Jefe/Salida) permanece intacto.
    /// </summary>
    public static class Nivel1LandscapeSwap
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string LabFbxPath = "Assets/Eira/Models/Nivel1Laboratorio/LabCorridor.fbx";
        const string ToscanaFbxPath = "Assets/Eira/Models/Nivel1Paisaje/Toscana.fbx";
        const string MatFolder = "Assets/Eira/Materials/Nivel1Lab";

        const float CorridorStartX = 0f;
        const float CorridorEndX = 120f;
        const float CorridorHalfWidth = 13f;

        // Paisaje: escala y posición del centro en el lado +Z, junto al edificio.
        const float LandscapeScale = 12f;
        const float LandscapeXCenter = 60f;
        const float LandscapeZCenter = 130f;

        [MenuItem("Eira/Nivel1Paisaje/Swap")]
        public static void Swap()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                RebuildLevel1();
                Debug.Log("[N1P] SWAP TERMINADO con éxito.");
                Verify();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[N1P] SWAP ERROR: " + ex);
                throw;
            }
        }

        static void RebuildLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

            // ---- Descartar la planta de sulfonación ----
            var plant = GameObject.Find("SuphonationPlant_Environment");
            if (plant != null)
            {
                Object.DestroyImmediate(plant);
                Debug.Log("[N1P] Planta de sulfonación descartada (eliminada).");
            }

            // ---- Edificio laboratorio (corridor.blend) ----
            if (GameObject.Find("Nivel1_Laboratorio") == null && GameObject.Find("Nivel1_Laboratorio_Environment") == null)
            {
                var labPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LabFbxPath);
                if (labPrefab == null) throw new System.Exception("No se pudo cargar el FBX del laboratorio: " + LabFbxPath);

                var lab = (GameObject)PrefabUtility.InstantiatePrefab(labPrefab);
                lab.name = "Nivel1_Laboratorio_Environment";

                var animator = lab.GetComponent<Animator>();
                if (animator != null) Object.DestroyImmediate(animator);
                var legacy = lab.GetComponent<Animation>();
                if (legacy != null) Object.DestroyImmediate(legacy);

                FitLabToLevel(lab);
                AssignLabMaterials(lab);
                HideBuildingFloor(lab);
                AddLabLights(lab);
            }
            else
            {
                Debug.Log("[N1P] Laboratorio ya presente; se conserva.");
            }

            // ---- Paisaje toscana a un lado ----
            if (GameObject.Find("Nivel1_Paisaje") == null && GameObject.Find("Nivel1_Paisaje_Environment") == null)
            {
                var toscanaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToscanaFbxPath);
                if (toscanaPrefab == null) throw new System.Exception("No se pudo cargar el FBX del paisaje: " + ToscanaFbxPath);

                var toscana = (GameObject)PrefabUtility.InstantiatePrefab(toscanaPrefab);
                toscana.name = "Nivel1_Paisaje_Environment";
                PlaceLandscape(toscana);
                AssignLandscapeMaterials(toscana);
            }
            else
            {
                Debug.Log("[N1P] Paisaje ya presente; se conserva.");
            }

            // ---- Sol del nivel y luces de zona ----
            if (GameObject.Find("N1_Sun") == null)
            {
                var sun = new GameObject("N1_Sun");
                var dl = sun.AddComponent<Light>();
                dl.type = LightType.Directional;
                dl.color = new Color(1f, 0.86f, 0.62f);
                dl.intensity = 1.1f;
                sun.transform.rotation = Quaternion.Euler(50f, 210f, 0f);
            }
            foreach (var name in new[] { "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle", "SunDia", "SunTarde" })
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }

            // ---- Suelo de juego cubriendo el interior del edificio ----
            ExtendFloorToBuilding();

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[N1P] Nivel 1 guardado: laboratorio + paisaje toscano.");
        }

        // =========================================================
        // EDIFICIO LABORATORIO
        // =========================================================

        static void FitLabToLevel(GameObject root)
        {
            var t = root.transform;
            Bounds b = CombinedBounds(root);
            if (b.size.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[N1P] El laboratorio no tiene renderers; no se ajusta.");
                return;
            }

            // El pasillo del .blend es largo en Y; rotarlo para que vaya en X.
            if (b.size.z > b.size.x)
            {
                t.rotation = Quaternion.Euler(0f, 90f, 0f) * t.rotation;
                b = CombinedBounds(root);
            }

            // El edificio ocupa TODO el nivel (0..120): suelo y=0, empieza en x=0.
            float s = (CorridorEndX - CorridorStartX) / Mathf.Max(b.size.x, 0.001f);
            t.localScale = new Vector3(t.localScale.x * s, t.localScale.y * s, t.localScale.z * s);
            b = CombinedBounds(root);

            float zShift = -b.center.z;
            t.position += new Vector3(0f, 0f, zShift);
            b = CombinedBounds(root);

            t.position += new Vector3(-b.min.x, -b.min.y, 0f);
            b = CombinedBounds(root);
            Debug.Log("[N1P] Laboratorio ajustado. Centro=" + b.center + " Tamaño=" + b.size + " min=" + b.min);
        }

        static void HideBuildingFloor(GameObject root)
        {
            int hidden = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                if (mn.ToLower().Contains("floor") || r.name.ToLower().Contains("floor"))
                {
                    r.enabled = false;
                    hidden++;
                }
            }
            Debug.Log("[N1P] Suelos del edificio ocultos: " + hidden);
        }

        static void AssignLabMaterials(GameObject root)
        {
            var struts = GetOrCreateMaterial("N1L_Struts", new Color(0.5f, 0.53f, 0.56f, 1f), 0.35f, 0.7f);
            var green = GetOrCreateMaterial("N1L_Green", new Color(0.22f, 0.62f, 0.34f, 1f), 0.6f, 0.1f);
            var blue = GetOrCreateMaterial("N1L_BlueLight", new Color(0.02f, 0.05f, 0.1f, 1f), 0.5f, 0.2f,
                new Color(0f, 0.75f, 1f, 1f));
            var white = GetOrCreateMaterial("N1L_WhiteLight", new Color(0.9f, 0.92f, 0.95f, 1f), 0.7f, 0f,
                new Color(1f, 0.98f, 0.9f, 1f));
            var floor = GetOrCreateMaterial("N1L_Floor", new Color(0.06f, 0.07f, 0.09f, 1f), 0.3f, 0.1f);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int g = 0, bL = 0, w = 0, f = 0, s = 0;
            foreach (var r in renderers)
            {
                if (!r.enabled) { f++; continue; }
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                Material mat;
                if (mn.Contains("cubeGreen")) { mat = green; g++; }
                else if (mn.Contains("BlueLights")) { mat = blue; bL++; }
                else if (mn.Contains("lightWhite")) { mat = white; w++; }
                else if (mn.Contains("floor")) { mat = floor; f++; }
                else { mat = struts; s++; }
                r.sharedMaterial = mat;
            }
            Debug.Log($"[N1P] Materiales laboratorio: struts={s} green={g} blueLights={bL} whiteLights={w} floor={f} renderers={renderers.Length}");
        }

        static void AddLabLights(GameObject root)
        {
            var pts = new[] { 10f, 38f, 66f, 94f };
            for (int i = 0; i < pts.Length; i++)
            {
                var go = new GameObject("N1Lab_Light_" + i);
                go.transform.SetParent(root.transform, true);
                go.transform.position = new Vector3(pts[i], 4.5f, 0f);
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.72f, 0.88f, 1f);
                lt.intensity = 1.6f;
                lt.range = 18f;
            }
        }

        static void ExtendFloorToBuilding()
        {
            var floor = GameObject.Find("SecretLab_Floor");
            if (floor == null)
            {
                Debug.LogWarning("[N1P] No hay SecretLab_Floor; no se extiende.");
                return;
            }
            var s = floor.transform.localScale;
            floor.transform.localScale = new Vector3(s.x, s.y, 58f);
            Debug.Log("[N1P] Suelo de juego extendido a z±29 (interior del edificio).");
        }

        // =========================================================
        // PAISAJE TOSCANA
        // =========================================================

        static void PlaceLandscape(GameObject root)
        {
            var t = root.transform;
            t.localScale = Vector3.one * LandscapeScale;
            Bounds b = CombinedBounds(root);

            // El suelo (Ground) queda a ras de y=0.
            var ground = FindChildRenderer(root, "Ground");
            float yShift = ground ? -ground.bounds.min.y : -b.min.y;
            t.position += new Vector3(0f, yShift, 0f);
            b = CombinedBounds(root);

            t.position += new Vector3(LandscapeXCenter - b.center.x, 0f, LandscapeZCenter - b.center.z);
            b = CombinedBounds(root);
            Debug.Log("[N1P] Paisaje colocado. Centro=" + b.center + " Tamaño=" + b.size + " min=" + b.min);
        }

        static void AssignLandscapeMaterials(GameObject root)
        {
            var brown = GetOrCreateMaterial("N1L_Brown", new Color(0.42f, 0.3f, 0.18f, 1f), 0.8f, 0f);
            var darkGreen = GetOrCreateMaterial("N1L_DarkGreen", new Color(0.12f, 0.3f, 0.13f, 1f), 0.6f, 0f);
            var green = GetOrCreateMaterial("N1L_Green", new Color(0.2f, 0.44f, 0.2f, 1f), 0.5f, 0f);
            var sand = GetOrCreateMaterial("N1L_Sand", new Color(0.72f, 0.65f, 0.53f, 1f), 0.9f, 0f);
            var white = GetOrCreateMaterial("N1L_White", new Color(0.9f, 0.9f, 0.88f, 1f), 0.6f, 0f);
            var grass = GetOrCreateMaterial("N1L_Grass", new Color(0.32f, 0.47f, 0.23f, 1f), 0.7f, 0f);

            int hidden = 0, keptTex = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                // La maqueta tiene un cubo de volumen atmosférico: no se dibuja.
                if (mn.Contains("Volume.001") || r.name.Contains("VolumeCube"))
                {
                    r.enabled = false;
                    hidden++;
                    continue;
                }

                Material mat;
                if (mn.Contains("Groung"))
                {
                    // El suelo trae textura de hierba embebida: se conserva si existe.
                    var imp = r.sharedMaterial;
                    if (imp != null && imp.HasProperty("_BaseMap") && imp.GetTexture("_BaseMap") != null)
                    {
                        imp.SetFloat("_Smoothness", 0.5f);
                        keptTex++;
                        continue;
                    }
                    mat = grass;
                }
                else if (mn.Contains("kiparis_wood")) mat = brown;
                else if (mn.Contains("kiparis_leaves")) mat = darkGreen;
                else if (mn.Contains("leaves_simple")) mat = green;
                else if (mn.Contains("zamok_base")) mat = sand;
                else if (mn.Contains("Material.001")) mat = white;
                else mat = grass;
                r.sharedMaterial = mat;
            }
            Debug.Log($"[N1P] Materiales paisaje: textura conservada={keptTex} ocultos={hidden}");
        }

        // =========================================================
        // UTILIDADES
        // =========================================================

        static Renderer FindChildRenderer(GameObject root, string name)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                if (r.name == name) return r;
            return null;
        }

        static Material GetOrCreateMaterial(string name, Color baseColor, float smoothness, float metallic, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var path = MatFolder + "/" + name + ".mat";

            EnsureFolder(MatFolder);

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            Material m;
            if (existing != null)
                m = existing;
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

        [MenuItem("Eira/Nivel1Paisaje/Verify")]
        public static void Verify()
        {
            var sc = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
            Debug.Log("[Verify] ============ Level1Scene : " + Level1Scene + " ============");
            Debug.Log("[Verify] rootCount=" + sc.rootCount);
            foreach (var go in sc.GetRootGameObjects())
                Debug.Log("[Verify] ROOT: '" + go.name + "' active=" + go.activeSelf + " pos=" + go.transform.position + " scale=" + go.transform.localScale);

            foreach (var n in new[] { "Nivel1_Laboratorio_Environment", "Nivel1_Paisaje_Environment", "SuphonationPlant_Environment", "Nivel1_Paisaje", "Nivel1_Laboratorio", "Eira", "NOVA" })
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

        static Bounds CombinedBounds(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b;
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = System.IO.Path.GetFileName(folder);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}