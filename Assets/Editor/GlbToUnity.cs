using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using EiraGame;

/// Conversor / importador de .glb a prefab de Unity.
/// El proyecto no tiene el paquete de importador glTF, por lo que Unity
/// trata los .glb como DefaultImporter y LoadAssetAtPath&lt;GameObject&gt;
/// devuelve null. Este script lee el contenedor GLB (JSON + BIN) a mano.
public static class GlbToUnity
{
    const string SrcGlb = "Assets/Eira/Models/NovaRobot.glb";
    const string OutFolder = "Assets/Eira/Models/NovaRobot_Generated";
    const string PrefabPath = "Assets/Eira/Models/NovaRobot.prefab";

    // ================================================================
    // MENU
    // ================================================================

    /// Un solo clic: genera el prefab del robot (si falta) y reemplaza
    /// por completo el personaje antiguo de NOVA.
    [MenuItem("Eira/Reemplazar Nova por Robot (GLB)")]
    public static void ReplaceNovaAll()
    {
        if (!File.Exists(PrefabPath))
            Generate();

        if (!File.Exists(PrefabPath))
        {
            Debug.LogError("No se pudo generar el prefab del robot. Revisa la consola.");
            return;
        }

        ReplaceNova();
        ReportNova();
    }

    /// Entrada para Unity -batchmode -executeMethod.
    /// Abre Level1Scene, reemplaza a Nova por el robot y guarda la escena.
    public static void BatchReplaceNova()
    {
        const string level1 = "Assets/Eira/Scenes/Level1Scene.unity";

        Debug.Log("=== BATCH: abriendo " + level1 + " ===");

        var scene = EditorSceneManager.OpenScene(level1, OpenSceneMode.Single);

        if (!scene.IsValid())
        {
            Debug.LogError("BATCH: no se pudo abrir " + level1);
            return;
        }

        Debug.Log("Escena abierta. Raices: " + scene.rootCount);

        // El guardado debe ocurrir aunque el reemplazo lance una excepcion.
        try
        {
            ReplaceNovaAll();
        }
        catch (Exception e)
        {
            Debug.LogError("BATCH: error durante el reemplazo -> " + e.Message);
        }

        bool ok = EditorSceneManager.SaveScene(scene);
        Debug.Log("=== BATCH: escena guardada=" + ok + " -> " + scene.path + " ===");
    }

    static void ReportNova()
    {        NovaCompanion c = UnityEngine.Object.FindAnyObjectByType<NovaCompanion>();
        if (c == null)
        {
            Debug.LogError("No hay NovaCompanion en la escena.");
            return;
        }

        GameObject nova = c.gameObject;
        var sb = new StringBuilder();
        sb.AppendLine("--- CONTENIDO ACTUAL DE " + nova.name + " ---");

        foreach (Component comp in nova.GetComponents<Component>())
        {
            // Un componente puede venir null si su script falta.
            string tipo = comp == null ? "<script ausente>" : comp.GetType().Name;
            sb.AppendLine("  componente: " + tipo);
        }

        int visuales = 0;

        foreach (Transform t in nova.transform)
        {
            visuales++;
            sb.AppendLine("  hijo: " + t.name +
                          " | escala " + t.localScale.ToString("F3") +
                          " | offset " + t.localPosition.ToString("F3"));
        }

        if (visuales != 1)
            sb.AppendLine("  AVISO: se esperaba 1 solo visual, hay " + visuales);

        Debug.Log(sb.ToString());
    }

    [MenuItem("Eira/1. Generar Prefab Robot (GLB)")]
    public static void Generate()
    {
        if (!File.Exists(SrcGlb))
        {
            Debug.LogError("No existe " + SrcGlb);
            return;
        }

        Dictionary<string, object> json;
        byte[] bin = ReadGlb(SrcGlb, out json);

        if (!AssetDatabase.IsValidFolder(OutFolder))
            AssetDatabase.CreateFolder("Assets/Eira/Models", "NovaRobot_Generated");

        var materials = BuildMaterials(json, bin);
        var root = new GameObject("NovaRobot");

        try
        {
            BuildHierarchy(json, bin, root.transform, materials);
        }
        catch (Exception e)
        {
            Debug.LogError("GLB: error construyendo jerarquia -> " + e.Message);
            UnityEngine.Object.DestroyImmediate(root);
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("=== PREFAB ROBOT GENERADO: " + PrefabPath + " ===");
    }

    [MenuItem("Eira/2. Reemplazar Nova por Robot (GLB)")]
    public static void ReplaceNova()
    {
        if (!File.Exists(PrefabPath))
        {
            Generate();
            if (!File.Exists(PrefabPath))
                return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("No se pudo cargar el prefab " + PrefabPath);
            return;
        }

        // ---- 1. Localizar el objeto NOVA ----
        NovaCompanion novaComp = UnityEngine.Object.FindAnyObjectByType<NovaCompanion>();
        GameObject nova = novaComp != null ? novaComp.gameObject : GameObject.Find("NOVA");

        if (nova == null)
        {
            Debug.LogError("No se encontro NOVA en la escena.");
            return;
        }

        // SEGURIDAD: este script jamas debe tocar a Eira.
        if (nova.GetComponent<PlayerController>() != null ||
            nova.name == "Eira" ||
            nova.name.Contains("Eira"))
        {
            Debug.LogError("ABORTADO: el objeto encontrado es Eira, no NOVA. " +
                           "No se modifico nada.");
            return;
        }

        Debug.Log("Objetivo: " + nova.name + " (NovaCompanion). Eira NO se toca.");

        // Estado de Eira antes, para comprobar al final que no cambio.
        Vector3 eiraAntes = Vector3.zero;
        Vector3 eiraScaleAntes = Vector3.one;
        int eiraHijosAntes = 0;

        GameObject eiraRef = GameObject.Find("Eira");
        if (eiraRef != null)
        {
            eiraAntes = eiraRef.transform.position;
            eiraScaleAntes = eiraRef.transform.localScale;
            eiraHijosAntes = eiraRef.transform.childCount;
        }

        // ---- 2. Instanciar el robot como hijo de NOVA ----
        var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        robot.name = "NOVAVisual_Robot";
        Undo.RegisterCreatedObjectUndo(robot, "Nova Robot");
        robot.transform.SetParent(nova.transform, false);
        robot.transform.localPosition = Vector3.zero;
        robot.transform.localRotation = Quaternion.identity;

        // ---- 3. Borrar CUALQUIER visual anterior (primitivas procedurales) ----
        var stale = new List<GameObject>();
        foreach (Transform child in nova.transform)
        {
            if (child.gameObject != robot)
                stale.Add(child.gameObject);
        }

        foreach (GameObject go in stale)
        {
            Debug.Log("Eliminado visual viejo de NOVA: " + go.name);
            Undo.DestroyObjectImmediate(go);
        }

        // Quitar NovaWalker (animacion del modelo procedural)
        var walkers = new List<Component>();

        foreach (Component comp in nova.GetComponents<Component>())
        {
            if (comp == null) continue;
            if (comp.GetType().Name == "NovaWalker")
                walkers.Add(comp);
        }

        foreach (Component w in walkers)
        {
            Debug.Log("Quitado NovaWalker (ya no hay modelo procedural)");
            Undo.DestroyObjectImmediate(w);
        }

        // Purgar componentes rotos (script ausente). Son restos de los scripts
        // de editor que se usaron para depurar a Nova y ya no aplican.
        int purgados = RemoveNullComponents(nova);

        if (purgados > 0)
            Debug.Log("Componentes rotos purgados de NOVA: " + purgados);

        // ---- 4. Escalar para ocupar el espacio de Nova (mismo alto que Eira) ----
        // Eira solo se LEE para saber la altura objetivo. No se modifica.
        float targetHeight = 1.75f;
        GameObject eira = GameObject.Find("Eira");

        if (eira != null)
        {
            var cc = eira.GetComponent<CharacterController>();
            if (cc != null && cc.height > 0.1f)
                targetHeight = cc.height;
        }

        Bounds b = CalcBounds(robot);
        if (b.size.y > 0.0001f)
        {
            float s = targetHeight / b.size.y;
            robot.transform.localScale = Vector3.one * s;
            Debug.Log("Escala del robot aplicada: " + s.ToString("F4") +
                      " (alto original " + b.size.y.ToString("F3") +
                      " -> " + targetHeight.ToString("F2") + "m)");
        }

        // Alinear los pies con el suelo de NOVA (followHeight = 0)
        // Los bounds del renderer ya estan en espacio de mundo.
        Bounds wb = CalcBounds(robot);
        float dy = wb.min.y - nova.transform.position.y;
        robot.transform.localPosition = new Vector3(0f, -dy, 0f);

        Debug.Log("Bounds robot: size=" + wb.size.ToString("F3") +
                  " minY=" + wb.min.y.ToString("F3") +
                  " maxY=" + wb.max.y.ToString("F3") +
                  " | ajuste vertical=" + (-dy).ToString("F3"));

        // ---- 5. Ajustar al suelo ----
        Vector3 ground = nova.transform.position;
        RaycastHit hit;
        if (Physics.Raycast(ground + Vector3.up * 20f, Vector3.down, out hit, 100f))
        {
            nova.transform.position = hit.point;
            Debug.Log("NOVA sobre el suelo en " + hit.point);
        }

        Selection.activeGameObject = robot;
        EditorGUIUtility.PingObject(robot);

        // ---- 6. Verificar que Eira quedo intacta ----
        if (eira != null)
        {
            bool intacto = eira.transform.position == eiraAntes &&
                           eira.transform.localScale == eiraScaleAntes &&
                           eira.transform.childCount == eiraHijosAntes;

            Debug.Log("Eira intacta: " + intacto +
                      " | pos " + eiraAntes + " | escala " + eiraScaleAntes +
                      " | hijos " + eiraHijosAntes);
        }

        Debug.Log("=== NOVA REEMPLAZADA POR ROBOT ===");
        Debug.Log("Objeto padre: " + nova.name + " | Visual: " + robot.name);
    }

    /// Elimina entradas de m_Component que apuntan a null (scripts rotos).
    /// RemoveMonoBehavioursWithMissingScript no cubre estos casos.
    static int RemoveNullComponents(GameObject go)
    {
        var so = new SerializedObject(go);
        SerializedProperty comps = so.FindProperty("m_Component");

        if (comps == null || !comps.isArray)
            return 0;

        int removed = 0;

        for (int i = comps.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty el = comps.GetArrayElementAtIndex(i);
            SerializedProperty c = el.FindPropertyRelative("component");

            if (c == null || c.objectReferenceValue != null)
                continue;

            // La primera llamada deja el hueco en null, la segunda lo quita.
            comps.DeleteArrayElementAtIndex(i);
            comps.DeleteArrayElementAtIndex(i);
            removed++;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        return removed;
    }

    static Bounds CalcBounds(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        return b;
    }

    // ================================================================
    // MATERIALES
    // ================================================================

    static List<Material> BuildMaterials(Dictionary<string, object> json, byte[] bin)
    {
        var result = new List<Material>();
        List<object> mats = Arr(json, "materials");

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) lit = Shader.Find("Standard");

        if (mats == null)
        {
            result.Add(NewMaterial("NovaRobot_Default", lit, new Color(0.7f, 0.75f, 0.85f), 0.5f, 0.3f, false));
            return result;
        }

        for (int i = 0; i < mats.Count; i++)
        {
            Dictionary<string, object> m = Dict(mats[i]);
            string name = Str(m, "name", "mat_" + i);
            Color c = Color.white;
            float metallic = 1f;
            float rough = 1f;

            Dictionary<string, object> pbr = Dict(m, "pbrMetallicRoughness");
            if (pbr != null)
            {
                List<object> bc = Arr(pbr, "baseColorFactor");
                if (bc != null && bc.Count >= 3)
                {
                    c = new Color(F(bc[0]), F(bc[1]), F(bc[2]),
                                  bc.Count >= 4 ? F(bc[3]) : 1f);
                }

                metallic = Has(pbr, "metallicFactor") ? F(pbr["metallicFactor"]) : 1f;
                rough = Has(pbr, "roughnessFactor") ? F(pbr["roughnessFactor"]) : 1f;
            }

            string alphaMode = Str(m, "alphaMode", "OPAQUE");
            bool transparent = alphaMode == "BLEND" || c.a < 0.999f;

            string path = OutFolder + "/mat_" + i + ".mat";
            DeleteIfExists(path);
            result.Add(NewMaterial(Sanitize(name), lit, c, metallic, rough, transparent, path));
        }

        return result;
    }

    static Material NewMaterial(string name, Shader sh, Color c, float metallic, float rough, bool transparent)
    {
        return NewMaterial(name, sh, c, metallic, rough, transparent, null);
    }

    static Material NewMaterial(string name, Shader sh, Color c, float metallic, float rough, bool transparent, string assetPath)
    {
        var m = new Material(sh);
        m.name = name;

        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 1f - rough);
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 1f - rough);

        if (transparent)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 2f);
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else
        {
            m.EnableKeyword("_SURFACE_TYPE_OPAQUE");
        }

        if (assetPath != null)
            AssetDatabase.CreateAsset(m, assetPath);

        return m;
    }

    // ================================================================
    // JERARQUIA
    // ================================================================

    static void BuildHierarchy(Dictionary<string, object> json, byte[] bin,
                               Transform parent, List<Material> materials)
    {
        List<object> nodes = Arr(json, "nodes");
        if (nodes == null || nodes.Count == 0) return;

        // No confiar en "scenes": se construye desde el grafo de nodos.
        // Un nodo es raiz si ningun otro lo lista como hijo. Asi cada nodo
        // se instancia exactamente una vez (evita duplicar la jerarquia).
        var isChild = new bool[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            List<object> children = Arr(Dict(nodes[i]), "children");
            if (children == null) continue;

            foreach (object c in children)
            {
                int ci = (int)F(c);
                if (ci >= 0 && ci < nodes.Count) isChild[ci] = true;
            }
        }

        int created = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (isChild[i]) continue;
            BuildNode(json, bin, nodes, i, parent, materials);
            created++;
        }

        // Si el grafo esta mal formado y no hay raices, se usan todos.
        if (created == 0)
        {
            Debug.LogWarning("GLB: ninguna raiz detectada en el grafo de nodos; " +
                             "se usan todos los nodos.");
            for (int i = 0; i < nodes.Count; i++)
                BuildNode(json, bin, nodes, i, parent, materials);
        }

        Debug.Log("GLB: nodos=" + nodes.Count + " raices=" + created);
    }

    static void BuildNode(Dictionary<string, object> json, byte[] bin,
                          List<object> nodes, int nodeIndex, Transform parent,
                          List<Material> materials)
    {
        if (nodeIndex < 0 || nodeIndex >= nodes.Count) return;

        Dictionary<string, object> node = Dict(nodes[nodeIndex]);
        if (node == null) return;

        var go = new GameObject(Str(node, "name", "node_" + nodeIndex));
        go.transform.SetParent(parent, false);

        // Transform: matrix (column-major) tiene prioridad sobre TRS
        List<object> mat = Arr(node, "matrix");
        if (mat != null && mat.Count == 16)
        {
            var m = new Matrix4x4();
            for (int i = 0; i < 16; i++) m[i] = F(mat[i]);
            go.transform.localRotation = m.rotation;

            // Unity no permite Vector3 / Vector3: se divide componente a componente.
            Vector3 sc = m.lossyScale;
            Vector4 t4 = m.GetColumn(3);

            go.transform.localPosition = new Vector3(
                sc.x != 0f ? t4.x / sc.x : t4.x,
                sc.y != 0f ? t4.y / sc.y : t4.y,
                sc.z != 0f ? t4.z / sc.z : t4.z);
        }
        else
        {
            List<object> t = Arr(node, "translation");
            List<object> r = Arr(node, "rotation");
            List<object> s = Arr(node, "scale");

            if (t != null && t.Count >= 3)
                go.transform.localPosition = new Vector3(F(t[0]), F(t[1]), F(t[2]));

            if (r != null && r.Count >= 4)
                go.transform.localRotation = new Quaternion(F(r[0]), F(r[1]), F(r[2]), F(r[3]));

            if (s != null && s.Count >= 3)
                go.transform.localScale = new Vector3(F(s[0]), F(s[1]), F(s[2]));
        }

        // Malla
        if (node.ContainsKey("mesh"))
            BuildMeshGo(json, bin, (int)F(node["mesh"]), go, materials);

        // Hijos
        List<object> children = Arr(node, "children");
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
                BuildNode(json, bin, nodes, (int)F(children[i]), go.transform, materials);
        }
    }

    static void BuildMeshGo(Dictionary<string, object> json, byte[] bin, int meshIndex,
                            GameObject go, List<Material> materials)
    {
        List<object> meshes = Arr(json, "meshes");
        if (meshes == null || meshIndex < 0 || meshIndex >= meshes.Count) return;

        Dictionary<string, object> meshDef = Dict(meshes[meshIndex]);
        List<object> prims = Arr(meshDef, "primitives");
        if (prims == null) return;

        int created = 0;

        for (int i = 0; i < prims.Count; i++)
        {
            Dictionary<string, object> prim = Dict(prims[i]);

            int mode = Has(prim, "mode") ? (int)F(prim["mode"]) : 4;
            if (mode != 4) continue; // solo triangulos

            Dictionary<string, object> attrs = Dict(prim, "attributes");
            if (attrs == null || !attrs.ContainsKey("POSITION")) continue;

            Vector3[] verts = ReadVec3(json, bin, (int)F(attrs["POSITION"]));
            if (verts == null || verts.Length == 0) continue;

            Vector3[] norms = null;
            if (attrs.ContainsKey("NORMAL"))
                norms = ReadVec3(json, bin, (int)F(attrs["NORMAL"]));

            Vector2[] uvs = null;
            if (attrs.ContainsKey("TEXCOORD_0"))
                uvs = ReadVec2(json, bin, (int)F(attrs["TEXCOORD_0"]));

            int[] tris;
            if (prim.ContainsKey("indices"))
                tris = ReadIndices(json, bin, (int)F(prim["indices"]));
            else
                tris = SequentialTris(verts.Length);

            if (tris == null || tris.Length < 3) continue;

            Material mat = null;
            if (prim.ContainsKey("material"))
            {
                int mi = (int)F(prim["material"]);
                if (mi >= 0 && mi < materials.Count) mat = materials[mi];
            }

            var um = new Mesh();
            um.name = "NovaRobot_" + meshIndex + "_" + i;
            um.indexFormat = verts.Length > 65000
                ? IndexFormat.UInt32
                : IndexFormat.UInt16;

            um.SetVertices(new List<Vector3>(verts));

            if (norms != null && norms.Length == verts.Length)
                um.SetNormals(new List<Vector3>(norms));

            if (uvs != null && uvs.Length == verts.Length)
                um.SetUVs(0, new List<Vector2>(uvs));

            um.subMeshCount = 1;
            um.SetTriangles(new List<int>(tris), 0);

            if (norms == null || norms.Length != verts.Length)
                um.RecalculateNormals();

            um.RecalculateBounds();
            if (uvs != null && uvs.Length == verts.Length)
                um.RecalculateTangents();

            string meshPath = OutFolder + "/mesh_" + meshIndex + "_" + i + ".asset";
            DeleteIfExists(meshPath);
            AssetDatabase.CreateAsset(um, meshPath);

            var child = new GameObject(um.name);
            child.transform.SetParent(go.transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = um;

            var mr = child.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            created++;
        }

        if (created == 0)
            UnityEngine.Object.DestroyImmediate(go);
    }

    static void DeleteIfExists(string path)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            AssetDatabase.DeleteAsset(path);
    }

    static string Sanitize(string s)
    {
        var sb = new StringBuilder();
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.' ? c : '_');
        return sb.ToString();
    }

    // ================================================================
    // LECTURA DE ACCESSORS
    // ================================================================

    static int[] SequentialTris(int vertCount)
    {
        var t = new int[(vertCount / 3) * 3];
        for (int i = 0; i < t.Length; i++) t[i] = i;
        return t;
    }

    static int CompCount(string type)
    {
        switch (type)
        {
            case "SCALAR": return 1;
            case "VEC2": return 2;
            case "VEC3": return 3;
            case "VEC4": return 4;
            case "MAT2": return 4;
            case "MAT3": return 9;
            case "MAT4": return 16;
            default: return 1;
        }
    }

    static int CompSize(int componentType)
    {
        switch (componentType)
        {
            case 5120: // byte
            case 5121: // ubyte
                return 1;
            case 5122: // short
            case 5123: // ushort
                return 2;
            default: // 5125 uint, 5126 float
                return 4;
        }
    }

    static byte[] SliceAccessor(Dictionary<string, object> json, byte[] bin, int accIndex, int comps, out int stride)
    {
        stride = 0;
        List<object> accessors = Arr(json, "accessors");
        if (accessors == null || accIndex < 0 || accIndex >= accessors.Count) return null;

        Dictionary<string, object> acc = Dict(accessors[accIndex]);
        if (acc == null || !acc.ContainsKey("bufferView")) return null;

        List<object> bvs = Arr(json, "bufferViews");
        Dictionary<string, object> bv = Dict(bvs[(int)F(acc["bufferView"])]);

        int bvOffset = Has(bv, "byteOffset") ? (int)F(bv["byteOffset"]) : 0;

        int accOffset = Has(acc, "byteOffset") ? (int)F(acc["byteOffset"]) : 0;
        int count = (int)F(acc["count"]);
        int cSize = CompSize((int)F(acc["componentType"]));

        int elementSize = cSize * comps;
        stride = Has(bv, "byteStride") ? (int)F(bv["byteStride"]) : elementSize;
        if (stride <= 0) stride = elementSize;

        int start = bvOffset + accOffset;
        int need = count * stride;

        if (start < 0 || start >= bin.Length) return null;
        if (start + need > bin.Length) need = bin.Length - start;
        if (need <= 0) return null;

        var outBytes = new byte[need];
        Buffer.BlockCopy(bin, start, outBytes, 0, need);
        return outBytes;
    }

    static Vector3[] ReadVec3(Dictionary<string, object> json, byte[] bin, int accIndex)
    {
        List<object> accessors = Arr(json, "accessors");
        if (accessors == null || accIndex >= accessors.Count) return null;

        Dictionary<string, object> acc = Dict(accessors[accIndex]);
        int comps = CompCount(Str(acc, "type", "VEC3"));
        int count = (int)F(acc["count"]);
        int cType = (int)F(acc["componentType"]);

        int stride;
        byte[] data = SliceAccessor(json, bin, accIndex, comps, out stride);
        if (data == null) return null;

        var result = new Vector3[count];
        bool normalized = Has(acc, "normalized") && Convert.ToBoolean(F(acc["normalized"]));

        for (int i = 0; i < count; i++)
        {
            int o = i * stride;
            if (o + comps * 4 > data.Length && cType == 5126) break;

            float x = ReadFloat(data, o, cType, normalized);
            float y = ReadFloat(data, o + CompSize(cType), cType, normalized);
            float z = comps >= 3 ? ReadFloat(data, o + 2 * CompSize(cType), cType, normalized) : 0f;
            result[i] = new Vector3(x, y, z);
        }

        return result;
    }

    static Vector2[] ReadVec2(Dictionary<string, object> json, byte[] bin, int accIndex)
    {
        List<object> accessors = Arr(json, "accessors");
        if (accessors == null || accIndex >= accessors.Count) return null;

        Dictionary<string, object> acc = Dict(accessors[accIndex]);
        int comps = CompCount(Str(acc, "type", "VEC2"));
        int count = (int)F(acc["count"]);
        int cType = (int)F(acc["componentType"]);

        int stride;
        byte[] data = SliceAccessor(json, bin, accIndex, comps, out stride);
        if (data == null) return null;

        var result = new Vector2[count];
        bool normalized = Has(acc, "normalized") && Convert.ToBoolean(F(acc["normalized"]));

        for (int i = 0; i < count; i++)
        {
            int o = i * stride;
            float x = ReadFloat(data, o, cType, normalized);
            float y = ReadFloat(data, o + CompSize(cType), cType, normalized);
            result[i] = new Vector2(x, y);
        }

        return result;
    }

    static int[] ReadIndices(Dictionary<string, object> json, byte[] bin, int accIndex)
    {
        List<object> accessors = Arr(json, "accessors");
        if (accessors == null || accIndex >= accessors.Count) return null;

        Dictionary<string, object> acc = Dict(accessors[accIndex]);
        int count = (int)F(acc["count"]);
        int cType = (int)F(acc["componentType"]);

        int stride;
        byte[] data = SliceAccessor(json, bin, accIndex, 1, out stride);
        if (data == null) return null;

        var result = new int[count];
        int elemBytes = CompSize(cType);

        for (int i = 0; i < count; i++)
        {
            int o = i * stride;
            if (o + elemBytes > data.Length) break;

            if (cType == 5125)
                result[i] = unchecked((int)BitConverter.ToUInt32(data, o));
            else if (cType == 5123)
                result[i] = BitConverter.ToUInt16(data, o);
            else if (cType == 5121)
                result[i] = data[o];
            else
                result[i] = (int)BitConverter.ToSingle(data, o);
        }

        return result;
    }

    static float ReadFloat(byte[] d, int o, int componentType, bool normalized)
    {
        switch (componentType)
        {
            case 5126: return BitConverter.ToSingle(d, o);
            case 5125: return unchecked((int)BitConverter.ToUInt32(d, o));
            case 5123: return BitConverter.ToUInt16(d, o);
            case 5122: return BitConverter.ToInt16(d, o);
            case 5121: return d[o];
            case 5120: return (sbyte)d[o];
            default: return 0f;
        }
    }

    // ================================================================
    // GLB + JSON
    // ================================================================

    static byte[] ReadGlb(string path, out Dictionary<string, object> json)
    {
        json = null;
        byte[] all = File.ReadAllBytes(path);

        if (all.Length < 12 ||
            Encoding.ASCII.GetString(all, 0, 4) != "glTF")
        {
            Debug.LogError("GLB invalido: " + path);
            return null;
        }

        uint version = BitConverter.ToUInt32(all, 4);
        if (version != 2)
            Debug.LogWarning("GLB version " + version + " (se espera 2)");

        byte[] jsonBytes = null;
        byte[] bin = null;

        int offset = 12;
        while (offset + 8 <= all.Length)
        {
            uint chunkLen = BitConverter.ToUInt32(all, offset);
            string chunkType = Encoding.ASCII.GetString(all, offset + 4, 4);
            int dataStart = offset + 8;

            if (dataStart + chunkLen > all.Length)
                break;

            if (chunkType == "JSON")
            {
                jsonBytes = new byte[chunkLen];
                Buffer.BlockCopy(all, dataStart, jsonBytes, 0, (int)chunkLen);
            }
            else if (chunkType == "BIN\0")
            {
                bin = new byte[chunkLen];
                Buffer.BlockCopy(all, dataStart, bin, 0, (int)chunkLen);
            }

            offset = dataStart + (int)chunkLen;
        }

        if (jsonBytes == null)
        {
            Debug.LogError("El GLB no tiene chunk JSON.");
            return null;
        }

        string jsonText = Encoding.UTF8.GetString(jsonBytes).TrimEnd('\0', ' ', '\n', '\r', '\t');
        json = (Dictionary<string, object>)ParseJson(jsonText);

        if (bin == null)
        {
            // .gltf embebido o buffer data URI no soportado
            bin = new byte[0];
            Debug.LogWarning("El GLB no tiene chunk BIN.");
        }

        return bin;
    }

    // --- parser JSON minimo ---

    static object ParseJson(string s)
    {
        int i = 0;
        return ParseValue(s, ref i);
    }

    static object ParseValue(string s, ref int i)
    {
        SkipWs(s, ref i);
        if (i >= s.Length) return null;

        switch (s[i])
        {
            case '{': return ParseObject(s, ref i);
            case '[': return ParseArray(s, ref i);
            case '"': return ParseString(s, ref i);
            case 't': i += 4; return true;
            case 'f': i += 5; return false;
            case 'n': i += 4; return null;
            default: return ParseNumber(s, ref i);
        }
    }

    static Dictionary<string, object> ParseObject(string s, ref int i)
    {
        var result = new Dictionary<string, object>();
        i++; // '{'
        SkipWs(s, ref i);

        while (i < s.Length && s[i] != '}')
        {
            if (s[i] == ',' || s[i] == ':')
            {
                i++;
                SkipWs(s, ref i);
                continue;
            }

            if (s[i] != '"')
            {
                i++;
                continue;
            }

            string key = ParseString(s, ref i);
            SkipWs(s, ref i);

            if (i < s.Length && s[i] == ':')
                i++;

            SkipWs(s, ref i);
            result[key] = ParseValue(s, ref i);
            SkipWs(s, ref i);
        }

        if (i < s.Length) i++; // '}'
        return result;
    }

    static List<object> ParseArray(string s, ref int i)
    {
        var result = new List<object>();
        i++; // '['
        SkipWs(s, ref i);

        while (i < s.Length && s[i] != ']')
        {
            if (s[i] == ',')
            {
                i++;
                SkipWs(s, ref i);
                continue;
            }

            result.Add(ParseValue(s, ref i));
            SkipWs(s, ref i);
        }

        if (i < s.Length) i++; // ']'
        return result;
    }

    static string ParseString(string s, ref int i)
    {
        var sb = new StringBuilder();
        i++; // '"'

        while (i < s.Length && s[i] != '"')
        {
            char c = s[i];

            if (c == '\\' && i + 1 < s.Length)
            {
                i++;
                char e = s[i];

                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 < s.Length)
                        {
                            sb.Append((char)Convert.ToInt32(
                                s.Substring(i + 1, 4), 16));
                            i += 4;
                        }
                        break;
                    default: sb.Append(e); break;
                }

                i++;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        if (i < s.Length) i++; // '"'
        return sb.ToString();
    }

    static object ParseNumber(string s, ref int i)
    {
        int start = i;

        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0)
            i++;

        string t = s.Substring(start, i - start);
        double d;

        if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
            return d;

        return 0d;
    }

    static void SkipWs(string s, ref int i)
    {
        while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
    }

    // --- helpers de acceso ---

    static bool Has(Dictionary<string, object> d, string k)
    {
        return d != null && d.ContainsKey(k) && d[k] != null;
    }

    static Dictionary<string, object> Dict(Dictionary<string, object> d, string k)
    {
        if (d == null || !d.ContainsKey(k)) return null;
        return d[k] as Dictionary<string, object>;
    }

    static Dictionary<string, object> Dict(object o)
    {
        return o as Dictionary<string, object>;
    }

    static List<object> Arr(Dictionary<string, object> d, string k)
    {
        if (d == null || !d.ContainsKey(k)) return null;
        return d[k] as List<object>;
    }

    static float F(object o)
    {
        if (o is double) return (float)(double)o;
        if (o is float) return (float)o;
        if (o is int) return (int)o;
        if (o is long) return (long)o;

        if (o != null)
        {
            double parsed;
            if (double.TryParse(o.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed))
                return (float)parsed;
        }

        return 0f;
    }

    static string Str(Dictionary<string, object> d, string k, string fallback)
    {
        if (d == null || !d.ContainsKey(k) || d[k] == null) return fallback;
        return d[k].ToString();
    }
}
