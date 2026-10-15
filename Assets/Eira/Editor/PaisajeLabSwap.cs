using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// PARTE 1 - Nivel 1: laboratorio (corredor sci-fi) DETRÁS + paisaje toscano DELANTE.
    /// Eira aparece DENTRO del laboratorio; al salir entra al paisaje donde se desarrolla el nivel.
    ///
    /// Layout (corredor de juego X 0..120, Z ±13):
    ///   - Laboratorio: de x=0 a ~45, centrado en Z, suelo a y=0 (rotado 90° para que su
    ///     longitud siga el eje X del nivel).
    ///   - Paisaje toscano: delante, centrado en x~72, base a y=0.
    ///   - Se elimina la planta de sulfonación anterior.
    /// </summary>
    public static class PaisajeLabSwap
    {
        const string Level1Scene = "Assets/Eira/Scenes/Level1Scene.unity";
        const string LabFbxPath = "Assets/Eira/Models/Laboratorio/LaboratoryCorridor.fbx";
        const string TuscanyFbxPath = "Assets/Eira/Models/PaisajeNivel1/Tuscany.fbx";
        const string MatFolderLab = "Assets/Eira/Materials/Laboratorio";
        const string MatFolderTusc = "Assets/Eira/Materials/PaisajeNivel1";

        // El laboratorio ocupa el tramo inicial del nivel (x 0..45).
        const float LabLengthUnits = 45f;
        // El paisaje toscano (17x17) se escala para que sea la pieza central del tramo libre.
        const float TuscanyScale = 1.6f;
        // Centro en X del paisaje dentro del tramo delante del laboratorio.
        const float TuscanyCenterX = 72f;

        [MenuItem("Eira/PaisajeLab/Swap")]
        public static void Swap()
        {
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                RebuildLevel1();
                Debug.Log("[PaisajeLab] TERMINADO.");
                Verify();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[PaisajeLab] ERROR: " + ex);
                throw;
            }
        }

        static void RebuildLevel1()
        {
            var scene = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);

            // Eliminar escenario anterior (planta) y restos inactivos.
            foreach (var rootObj in scene.GetRootGameObjects())
            {
                if (rootObj.name == "SuphonationPlant_Environment" ||
                    rootObj.name == "SecretLaboratory_Environment" ||
                    rootObj.name == "Escenario_Nivel2_DesdeCero")
                {
                    string n = rootObj.name;
                    Object.DestroyImmediate(rootObj);
                    Debug.Log("[PaisajeLab] Escenario anterior eliminado -> " + n);
                }
            }

            CreateLab(scene);
            CreateTuscany(scene);

            // Luz de día para el paisaje (activar el sol del nivel).
            SetRootActive(scene, "SunDia", true);
            // Las luces de zona coloreadas del viejo laboratorio no aplican.
            foreach (var n in new[] { "WarmSpawn", "RedBoss", "GreenExit", "CyanPuzzle" })
                SetRootActive(scene, n, false);

            EditorSceneManager.SaveScene(scene);
            Debug.Log("[PaisajeLab] Nivel 1 guardado: laboratorio + paisaje toscano.");
        }

        static void CreateLab(Scene checkScene)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(LabFbxPath);
            if (fbx == null) throw new System.Exception("No se pudo cargar el FBX del laboratorio: " + LabFbxPath);

            var lab = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            lab.name = "Laboratorio_Environment";

            var animator = lab.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            var legacy = lab.GetComponent<Animation>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            var t = lab.transform;

            // Rotar para que la longitud del corredor siga el eje X del nivel.
            t.rotation = Quaternion.Euler(0f, 90f, 0f);

            Bounds b = CombinedBounds(lab);
            float s = LabLengthUnits / Mathf.Max(b.size.x, 0.001f);
            t.localScale = new Vector3(s, s, s);

            b = CombinedBounds(lab);
            t.position = new Vector3(
                0f - b.min.x,
                0f - b.min.y,
                -b.center.z);

            AssignLabMaterials(lab);
            AddLabLights(lab);

            Debug.Log("[PaisajeLab] Laboratorio ajustado: " + LabFbxPath + " escala=" + s + " bounds=" + CombinedBounds(lab));
        }

        static void CreateTuscany(Scene checkScene)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(TuscanyFbxPath);
            if (fbx == null) throw new System.Exception("No se pudo cargar el FBX del paisaje: " + TuscanyFbxPath);

            var tuscany = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            tuscany.name = "Paisaje_Toscana_Environment";

            var animator = tuscany.GetComponent<Animator>();
            if (animator != null) Object.DestroyImmediate(animator);
            var legacy = tuscany.GetComponent<Animation>();
            if (legacy != null) Object.DestroyImmediate(legacy);

            var t = tuscany.transform;
            t.localScale = new Vector3(TuscanyScale, TuscanyScale, TuscanyScale);

            Bounds b = CombinedBounds(tuscany);
            t.position = new Vector3(
                TuscanyCenterX - b.center.x,
                0f - b.min.y,
                -b.center.z);

            AssignTuscanyMaterials(tuscany);

            Debug.Log("[PaisajeLab] Paisaje toscano ajustado. bounds=" + CombinedBounds(tuscany));
        }

        // =========================================================
        // MATERIALES DEL CORREDOR-LABORATORIO (neón sci-fi)
        // =========================================================

        static void AssignLabMaterials(GameObject root)
        {
            var lightWhite = GetOrCreateMaterial(MatFolderLab, "Lab_LightWhite", new Color(0.96f, 0.97f, 1f, 1f), 0.8f, 0.1f, new Color(0.6f, 0.75f, 1f, 1f));
            var blueLights = GetOrCreateMaterial(MatFolderLab, "Lab_BlueLights", new Color(0.04f, 0.18f, 1f, 1f), 0.7f, 0.2f, new Color(0f, 0.3f, 1f, 1f));
            var cubeGreen = GetOrCreateMaterial(MatFolderLab, "Lab_CubeGreen", new Color(0.05f, 0.8f, 0.35f, 1f), 0.6f, 0.15f, new Color(0f, 0.7f, 0.25f, 1f));
            var struts = GetOrCreateMaterial(MatFolderLab, "Lab_Struts", new Color(0.13f, 0.15f, 0.19f, 1f), 0.5f, 0.35f);
            var floorM = GetOrCreateMaterial(MatFolderLab, "Lab_Floor", new Color(0.11f, 0.12f, 0.14f, 1f), 0.2f, 0.05f);

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int lw = 0, bl = 0, cg = 0, st = 0, fl = 0, other = 0;
            foreach (var r in renderers)
            {
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                Material mat;
                if (mn.Contains("lightWhite")) { mat = lightWhite; lw++; }
                else if (mn.Contains("BlueLights")) { mat = blueLights; bl++; }
                else if (mn.Contains("cubeGreen")) { mat = cubeGreen; cg++; }
                else if (mn.Contains("struts")) { mat = struts; st++; }
                else if (mn.Contains("floor")) { mat = floorM; fl++; }
                else { mat = struts; other++; }
                r.sharedMaterial = mat;
            }
            Debug.Log($"[PaisajeLab] Lab materiales: lightWhite={lw} blueLights={bl} cubeGreen={cg} struts={st} floor={fl} otro={other} renderers={renderers.Length}");
        }

        // =========================================================
        // MATERIALES DEL PAISAJE TOSCANO (por nombre del .blend)
        // =========================================================

        static void AssignTuscanyMaterials(GameObject root)
        {
            var ground = GetOrCreateMaterial(MatFolderTusc, "Tus_Ground", new Color(0.32f, 0.5f, 0.2f, 1f), 0.1f, 0f);
            var castle = GetOrCreateMaterial(MatFolderTusc, "Tus_Castle", new Color(0.62f, 0.58f, 0.5f, 1f), 0.25f, 0f);
            var wood = GetOrCreateMaterial(MatFolderTusc, "Tus_Wood", new Color(0.34f, 0.24f, 0.15f, 1f), 0.4f, 0f);
            var leaves = GetOrCreateMaterial(MatFolderTusc, "Tus_Leaves", new Color(0.22f, 0.42f, 0.18f, 1f), 0.5f, 0f);
            var leavesDark = GetOrCreateMaterial(MatFolderTusc, "Tus_LeavesDark", new Color(0.16f, 0.3f, 0.13f, 1f), 0.5f, 0f);
            var sheep = GetOrCreateMaterial(MatFolderTusc, "Tus_Sheep", new Color(0.9f, 0.9f, 0.9f, 1f), 0.6f, 0f);
            var volume = GetOrCreateMaterial(MatFolderTusc, "Tus_Volume", new Color(0.7f, 0.87f, 1f, 1f), 0.9f, 0f, new Color(0.3f, 0.55f, 0.9f, 1f));

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            int gg = 0, ca = 0, wo = 0, le = 0, ld = 0, sh = 0, vo = 0, other = 0;
            foreach (var r in renderers)
            {
                string mn = r.sharedMaterial ? r.sharedMaterial.name : "";
                Material mat;
                if (mn.Contains("Ground")) { mat = ground; gg++; }
                else if (mn.Contains("zamok")) { mat = castle; ca++; }
                else if (mn.Contains("kiparis_wood")) { mat = wood; wo++; }
                else if (mn.Contains("kiparis_leaves")) { mat = leavesDark; ld++; }
                else if (mn.Contains("leaves")) { mat = leaves; le++; }
                else if (mn.Contains("Material.001")) { mat = sheep; sh++; }
                else if (mn.Contains("Volume")) { mat = volume; vo++; }
                else { mat = ground; other++; }
                r.sharedMaterial = mat;
            }
            Debug.Log($"[PaisajeLab] Toscana materiales: ground={gg} castle={ca} wood={wo} leaves={le} leavesDark={ld} sheep={sh} volume={vo} otro={other} renderers={renderers.Length}");
        }

        static Material GetOrCreateMaterial(string folder, string name, Color baseColor, float smoothness, float metallic, Color? emission = null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var path = folder + "/" + name + ".mat";

            EnsureFolder(folder);

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
        // LUCES: interior del laboratorio (el .blend no trae luces reales)
        // =========================================================

        static void AddLabLights(GameObject labRoot)
        {
            // Posiciones MUNDO dentro del corredor (worldPositionStays=true).
            var points = new[]
            {
                new Vector3(10f, 2f, 0f),
                new Vector3(20f, 2f, 0f),
                new Vector3(30f, 2f, 0f),
                new Vector3(40f, 2f, 0f),
                new Vector3(15f, 2f, 4f),
                new Vector3(25f, 2f, -4f),
            };
            for (int i = 0; i < points.Length; i++)
            {
                var go = new GameObject("Lab_LightPoint_" + i);
                go.transform.SetParent(labRoot.transform, true);
                go.transform.position = points[i];
                var lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.75f, 0.85f, 1f);
                lt.intensity = 1.2f;
                lt.range = 12f;
            }
        }

        static void SetRootActive(Scene sc, string name, bool active)
        {
            foreach (var go in sc.GetRootGameObjects())
            {
                if (go.name == name)
                {
                    go.SetActive(active);
                    return;
                }
            }
        }

        // =========================================================
        // VERIFICACIÓN
        // =========================================================

        [MenuItem("Eira/PaisajeLab/Verify")]
        public static void Verify()
        {
            var sc = EditorSceneManager.OpenScene(Level1Scene, OpenSceneMode.Single);
            Debug.Log("[Verify] ==================== Level1Scene ====================");
            Debug.Log("[Verify] rootCount=" + sc.rootCount);
            foreach (var go in sc.GetRootGameObjects())
            {
                Debug.Log("[Verify] ROOT: '" + go.name + "' active=" + go.activeSelf +
                          " pos=" + go.transform.position + " scale=" + go.transform.localScale);
            }
            foreach (var n in new[] { "Laboratorio_Environment", "Paisaje_Toscana_Environment", "Eira", "NOVA" })
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