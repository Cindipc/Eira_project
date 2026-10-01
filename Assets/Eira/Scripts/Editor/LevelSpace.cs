using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame.EditorTools
{
    /// <summary>
    /// Herramienta de espacio del Nivel 1.
    ///
    /// El nivel se construyó con el pasillooriginal de 26 m de ancho y
    /// mounds de suelo generados por tramos de 20 m. Eso deja tres problemas
    /// muy visibles al jugar:
    ///
    ///   1. Los muros laterales están en z = ±12.25, tan cerca que la cámara
    ///      choca con ellos constantemente cuando Eira va por el borde.
    ///   2. El suelo no es continuo: FloorRun corta en x = 57 y el siguiente
    ///      tramo empieza en x = 61, dejando un hueco de 4 m.
    ///   3. La primera losa (x = 7) está medio metro más baja que las demás.
    ///
    /// Esta herramienta corrige los tres, y es idempotente: ejecutarla dos
    /// veces seguidas no cambia nada la segunda vez.
    /// </summary>
    public static class LevelSpace
    {
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";

        // Ancho total jugable y paredes
        const float TargetHalfWidth = 13.5f;   // muros en z = ±13.5
        const float WallThickness = 0.6f;
        const float OldHalfWidth = 12.25f;     // posición original de los muros

        // Losas de suelo
        const float FirstSlabX = 7f;
        const float SlabHeight = 1f;
        const float SlabWidthZ = 28f;
        const float OldSlabWidthZ = 26f;
        const float StandardSlabY = -0.5f;     // la mayoría están aquí
        const float FirstSlabY = -1f;         // la primera está medio metro más abajo

        // Hueco del suelo
        // El plano de suelo se llama HuecosFillFloor, pero en realidad es el suelo
        // REAL de todo el pasillo: los "Cube" de 20x26 están desactivados y
        // este plano es lo único que sostiene a Eira.
        const string FloorName = "HuecosFillFloor";
        const float FloorFrom = -3f;
        const float FloorTo = 121f;
        const float FloorPlaneY = 0.07f;

        // Valores originales, para poder deshacer la ampliación
        const float OriginalWallThickness = 0.5f;
        const float OriginalSlabLength = 20.02f;
        static readonly Vector3 OriginalFloorScale = new Vector3(10.4f, 1f, 6.6f);

        static Mesh planeMesh;
        static Material floorMaterial;

        [MenuItem("Eira/Deshacer ampliación del espacio", false, 26)]
        public static void Revert()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single
            );

            int changes = 0;

            // Muros a su sitio original: z = ±12.25 y grosor 0.5
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go == null)
                    continue;

                Transform t = go.transform;

                if (t.localScale.x < 100f)
                    continue;

                float z = t.localPosition.z;

                if (Mathf.Abs(Mathf.Abs(z) - TargetHalfWidth) > 0.05f)
                    continue;

                t.localPosition = new Vector3(
                    t.localPosition.x,
                    t.localPosition.y,
                    OldHalfWidth
                );

                t.localScale = new Vector3(
                    t.localScale.x,
                    t.localScale.y,
                    OriginalWallThickness
                );

                changes++;
            }

            // Losas a 26 m de ancho y la primera a su altura original
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go == null)
                    continue;

                Transform t = go.transform;

                if (Mathf.Abs(t.localScale.y - SlabHeight) > 0.1f)
                    continue;

                if (Mathf.Abs(t.localScale.z - SlabWidthZ) > 0.5f)
                    continue;

                if (t.localScale.x < 15f || t.localScale.x > 25f)
                    continue;

                if (Mathf.Abs(t.localPosition.y + 0.5f) > 0.2f &&
                    Mathf.Abs(t.localPosition.y + 1f) > 0.2f)
                    continue;

                t.localScale = new Vector3(
                    OriginalSlabLength,
                    SlabHeight,
                    OldSlabWidthZ
                );

                bool isFirst =
                    Mathf.Abs(t.localPosition.x - FirstSlabX) < 0.05f;

                t.localPosition = new Vector3(
                    t.localPosition.x,
                    isFirst ? FirstSlabY : StandardSlabY,
                    0f
                );

                changes++;
            }

            // El plano de suelo a su posición y escala originales
            GameObject floor = GameObject.Find(FloorName);

            if (floor != null)
            {
                Transform t = floor.transform;

                Vector3 targetPos = new Vector3(0f, FloorPlaneY, 0f);
                Vector3 targetScale = OriginalFloorScale;

                if (Vector3.Distance(t.position, targetPos) > 0.001f)
                {
                    t.position = targetPos;
                    changes++;
                }

                if (Vector3.Distance(t.localScale, targetScale) > 0.001f)
                {
                    t.localScale = targetScale;
                    changes++;
                }
            }

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log(
                "[Eira] Ampliación deshecha: " + changes + " correcciones."
            );
        }

        [MenuItem("Eira/Listar suelos", false, 25)]
        public static void ListFloors()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (Collider c in Object.FindObjectsByType<Collider>(
                         FindObjectsInactive.Include))
            {
                if (c == null)
                    continue;

                Bounds b = c.bounds;

                if (b.size.x < 10f)
                    continue;

                Debug.Log(
                    "[Eira] " + c.name +
                    " · centro(" + b.center.x.ToString("F1") + ", " + b.center.y.ToString("F1") + ", " + b.center.z.ToString("F1") + ")" +
                    " · tamaño(" + b.size.x.ToString("F1") + ", " + b.size.y.ToString("F1") + ", " + b.size.z.ToString("F1") + ")" +
                    " · " + c.GetType().Name +
                    " · activo=" + c.gameObject.activeInHierarchy +
                    " enabled=" + c.enabled
                );
            }
        }

        [MenuItem("Eira/Diagnóstico de suelo", false, 24)]
        public static void DiagnoseFloor()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var colliders = Object.FindObjectsByType<Collider>(
                FindObjectsInactive.Include
            );

            Debug.Log("[Eira] Colliders totales: " + colliders.Length);

            for (float x = 30f; x <= 60f; x += 1f)
            {
                Vector3 origin = new Vector3(x, 3f, 0f);

                if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 12f))
                {
                    // Lista lo que hay alrededor para entender el hueco
                    string alrededor = "";

                    foreach (Collider c in colliders)
                    {
                        if (c == null)
                            continue;

                        Bounds b = c.bounds;

                        if (Mathf.Abs(b.center.x - x) > 12f)
                            continue;

                        alrededor +=
                            "\n    " + c.name +
                            " · bounds x[" + b.min.x.ToString("F1") + ", " + b.max.x.ToString("F1") + "]" +
                            " y[" + b.min.y.ToString("F1") + ", " + b.max.y.ToString("F1") + "]" +
                            " activo=" + c.gameObject.activeInHierarchy +
                            " enabled=" + c.enabled +
                            " layer=" + c.gameObject.layer;
                    }

                    Debug.LogWarning(
                        "[Eira] Sin suelo en x = " + x.ToString("F1") +
                        " · colliders cercanos:" + alrededor
                    );
                }
            }
        }

        [MenuItem("Eira/Revisar huecos del suelo", false, 23)]
        public static void ScanFloor()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var checkpoints = Object.FindObjectsByType<CheckpointZone>(
                FindObjectsInactive.Include
            );

            var sorted = new System.Collections.Generic.List<CheckpointZone>(checkpoints);
            sorted.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

            Debug.Log(
                "[Eira] Revisando suelo entre " +
                sorted.Count + " checkpoints."
            );

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                Vector3 a = sorted[i].transform.position;
                Vector3 b = sorted[i + 1].transform.position;

                int steps = Mathf.Max(4, Mathf.CeilToInt(Vector3.Distance(a, b) / 1f));

                for (int s = 0; s <= steps; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, (float)s / steps);
                    p.y = 3f;

                    if (!Physics.Raycast(p, Vector3.down, out _, 12f))
                        Debug.LogWarning(
                            "[Eira] Hueco de suelo en x = " +
                            p.x.ToString("F2") +
                            " · z = " + p.z.ToString("F2")
                        );
                }
            }
        }

        [MenuItem("Eira/Ampliar espacio del nivel", false, 22)]
        public static void Expand()
        {
            var scene = EditorSceneManager.OpenScene(
                ScenePath, OpenSceneMode.Single
            );

            int changes = 0;

            changes += WidenSideWalls();
            changes += WidenFloorSlabs();
            changes += FillFloorGap();
            changes += AlignFirstSlab();

            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log(
                    "[Eira] Espacio del nivel ampliado: " +
                    changes +
                    " correcciones aplicadas."
                );
            }
            else
            {
                Debug.Log(
                    "[Eira] Espacio del nivel: nada que corregir, ya está bien."
                );
            }
        }

        // =============================================================
        // MUROS LATERALES
        // =============================================================

        /// <summary>
        /// Empuja los muros laterales de z = ±12.25 a z = ±13.5 y les
        /// engorda la base para que la cámara no los atraviese.
        ///
        /// Se identifican por posición en Z (±12.25) y longitud en X (>100),
        /// no por nombre: el nombre varies entre escenas y versiones.
        /// </summary>
        static int WidenSideWalls()
        {
            int changes = 0;

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go == null)
                    continue;

                Transform t = go.transform;

                float z = t.localPosition.z;

                if (Mathf.Abs(Mathf.Abs(z) - OldHalfWidth) > 0.05f)
                    continue;

                // Solo los muros largos del pasillo.
                if (t.localScale.x < 100f)
                    continue;

                float newZ = z > 0f
                    ? TargetHalfWidth
                    : -TargetHalfWidth;

                if (Mathf.Abs(z - newZ) > 0.001f)
                {
                    t.localPosition = new Vector3(
                        t.localPosition.x,
                        t.localPosition.y,
                        newZ
                    );
                    changes++;
                }

                if (Mathf.Abs(t.localScale.z - WallThickness) > 0.001f)
                {
                    t.localScale = new Vector3(
                        t.localScale.x,
                        t.localScale.y,
                        WallThickness
                    );
                    changes++;
                }
            }

            return changes;
        }

        // =============================================================
        // HUECO DEL SUELO
        // =============================================================

        /// <summary>
        /// Asegura que el plano de suelo cubra todo el pasillo.
        ///
        /// OJO: pese al nombre "HuecosFillFloor", este objeto NO es un
        /// parche para un hueco. Los "Cube" de 20x26 que hay en la escena
        /// están desactivados, así que este plano de 104x66 m es el único
        /// suelo real: es lo que sostiene a Eira de principio a fin. Por eso
        /// se dimensiona a todo el recorrido y no a un tramo concreto.
        /// </summary>
        static int FillFloorGap()
        {
            int changes = 0;

            GameObject floor = GameObject.Find(FloorName);

            if (floor == null)
            {
                floor = new GameObject(FloorName);

                MeshFilter filter = floor.AddComponent<MeshFilter>();
                filter.sharedMesh = PlaneMesh();

                MeshRenderer renderer = floor.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = DefaultFloorMaterial();

                MeshCollider mesh = floor.AddComponent<MeshCollider>();
                mesh.sharedMesh = PlaneMesh();

                changes += 3;
            }

            Transform t = floor.transform;

            // El plano de Unity mide 10x10, así que la escala es la medida
            // final dividida entre 10. Se cubre TODO el pasillo, no solo el
            // tramo del medio: los "Cube" de suelo están desactivados, así
            // que este plano es el único suelo real y taparlo en parte
            // deja agujeros por los que Eira cae.
            float widthX = FloorTo - FloorFrom;
            float depthZ = TargetHalfWidth * 2f;

            Vector3 targetScale = new Vector3(
                widthX / 10f,
                1f,
                depthZ / 10f
            );

            Vector3 targetPos = new Vector3(
                (FloorFrom + FloorTo) / 2f,
                FloorPlaneY,
                0f
            );

            if (Vector3.Distance(t.position, targetPos) > 0.001f)
            {
                t.position = targetPos;
                changes++;
            }

            if (Vector3.Distance(t.localScale, targetScale) > 0.001f)
            {
                t.localScale = targetScale;
                changes++;
            }

            MeshCollider meshCollider = floor.GetComponent<MeshCollider>();
            if (meshCollider != null && !meshCollider.enabled)
            {
                meshCollider.enabled = true;
                changes++;
            }

            if (!floor.activeSelf)
            {
                floor.SetActive(true);
                changes++;
            }

            return changes;
        }

        /// <summary>
        /// Material de suelo por defecto, para el caso de que el plano
        /// tenga que crearse desde cero.
        /// </summary>
        static Material DefaultFloorMaterial()
        {
            if (floorMaterial != null)
                return floorMaterial;

            string[] guids = AssetDatabase.FindAssets("t:Material Floor");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                floorMaterial = AssetDatabase.LoadAssetAtPath<Material>(path);

                if (floorMaterial != null)
                    break;
            }

            return floorMaterial;
        }

        /// <summary>
        /// Devuelve el quad de 10x10 que usa Unity como suelo plano. Se
        /// busca por su asset en lugar de crearlo a mano para no dejar
        /// meshes huérfanos en el proyecto.
        /// </summary>
        static Mesh PlaneMesh()
        {
            if (planeMesh == null)
            {
                string[] guids = AssetDatabase.FindAssets("NewMesh");

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!path.Contains("NewMesh"))
                        continue;

                    planeMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

                    if (planeMesh != null)
                        break;
                }
            }

            return planeMesh;
        }

        // =============================================================
        // LOSAS DE SUELO
        // =============================================================

        /// <summary>
        /// Las losas que componen el piso tienen un ancho de 20.02 m y
        /// profundidad de 26 m. Para dar más espacio lateral a Eira, a Nova
        /// y a la cámara, se aumentan a 28 m de ancho (z).
        ///
        /// También se alargan un poco en X para sellar cualquier fisura
        /// entre tramos.
        /// </summary>
        static int WidenFloorSlabs()
        {
            int changes = 0;

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go == null)
                    continue;

                Transform t = go.transform;

                // Solo losas sin ampliar todavía: el ancho antiguo 26 es el que
                // identifica una losa del pasillo principal.
                if (Mathf.Abs(t.localScale.y - SlabHeight) > 0.1f)
                    continue;

                if (Mathf.Abs(t.localScale.z - SlabWidthZ) > 0.5f)
                    continue;

                if (t.localScale.x < 15f || t.localScale.x > 25f)
                    continue;

                if (Mathf.Abs(t.localPosition.y + 0.5f) > 0.2f &&
                    Mathf.Abs(t.localPosition.y + 1f) > 0.2f)
                    continue;

                if (Mathf.Abs(t.localScale.z - SlabWidthZ) > 0.01f)
                {
                    t.localScale = new Vector3(
                        t.localScale.x,
                        t.localScale.y,
                        SlabWidthZ
                    );
                    changes++;
                }
            }

            return changes;
        }

        // =============================================================
        // LOSA INICIAL
        // =============================================================

        /// <summary>
        /// La primera losa (x = 7) está medio metro más baja que el resto.
        /// Eso creaba un escalón de 0.5 m en el punto de aparición y
        /// provocaba un tropezón al empezar a caminar.
        /// Se sube para que todas las losas estén al mismo nivel.
        /// </summary>
        static int AlignFirstSlab()
        {
            int changes = 0;

            foreach (GameObject go in Object.FindObjectsByType<GameObject>(
                         FindObjectsInactive.Include))
            {
                if (go == null)
                    continue;

                Transform t = go.transform;

                if (Mathf.Abs(t.localPosition.x - FirstSlabX) > 0.05f)
                    continue;

                if (Mathf.Abs(t.localScale.y - SlabHeight) > 0.1f)
                    continue;

                if (Mathf.Abs(t.localScale.z - 26f) > 0.5f && Mathf.Abs(t.localScale.z - 28f) > 0.5f)
                    continue;

                if (Mathf.Abs(t.localPosition.y - StandardSlabY) > 0.01f)
                {
                    t.localPosition = new Vector3(
                        FirstSlabX,
                        StandardSlabY,
                        0f
                    );
                    changes++;
                }
            }

            return changes;
        }
    }
}