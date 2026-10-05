using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using EiraGame;

/// Aplica en el Nivel 1:
///  1. el robot de robot.glb como personaje de NOVA,
///  2. que la cámara en primera persona apunte a Eira (la de la espada).
public static class ApplyNovaRobotAndCamera
{
    const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";

    public static void Apply()
    {
        Debug.Log("########## APPLY INICIO ##########");

        // ---------- 1. Generar el prefab del robot desde el .glb ----------
        try
        {
            GlbToUnity.Generate();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Fallo al generar el prefab del robot: " + e);
        }

        if (!System.IO.File.Exists("Assets/Eira/Models/NovaRobot.prefab"))
        {
            Debug.LogError("No se genero NovaRobot.prefab. Abortando.");
            return;
        }

        // ---------- 2. Abrir la escena del nivel 1 ----------
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Debug.Log("Escena abierta: " + scene.name + " (" + scene.path + ")");

        // ---------- 3. Reemplazar a NOVA por el robot ----------
        try
        {
            GlbToUnity.ReplaceNova();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Fallo al reemplazar NOVA: " + e);
        }

        // ---------- 4. Camara apuntando a Eira ----------
        EnsureCameraOnEira();

        // ---------- 5. Reporte ----------
        Report(scene);

        // ---------- 6. Guardar ----------
        EditorSceneManager.MarkSceneDirty(scene);
        bool ok = EditorSceneManager.SaveScene(scene);
        Debug.Log("Escena guardada: " + ok);

        Debug.Log("########## APPLY FIN ##########");
    }

    /// <summary>
    /// Segunda pasada: baja a NOVA hasta el suelo real y deja los pies del
    /// robot en el mismo nivel. El raycast del primer pase falló porque en
    /// modo batch los transformadores no estaban sincronizados.
    /// </summary>
    public static void GroundAlign()
    {
        Debug.Log("########## GROUNDALIGN INICIO ##########");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        NovaCompanion novaComp = null;
        foreach (var nc in Object.FindObjectsByType<NovaCompanion>(FindObjectsInactive.Include))
        {
            if (nc != null) { novaComp = nc; break; }
        }

        if (novaComp == null)
        {
            Debug.LogError("No se encontro NOVA.");
            return;
        }

        Transform nova = novaComp.transform;

        Physics.SyncTransforms();

        Vector3 arriba = nova.position + Vector3.up * 50f;
        RaycastHit hit;

        if (Physics.Raycast(arriba, Vector3.down, out hit, 200f))
        {
            Debug.Log("Suelo detectado a y=" + hit.point.y.ToString("F3") +
                      " (collider: " + hit.collider.name + ")");
            nova.position = hit.point;
        }
        else
        {
            Debug.LogWarning("No se detecto suelo con raycast; se usa y=0 para NOVA.");
            Vector3 p = nova.position;
            p.y = 0f;
            nova.position = p;
        }

        // Pies del robot al nivel de NOVA.
        Transform robot = null;
        foreach (Transform t in nova)
        {
            if (t.name == "NOVAVisual_Robot") { robot = t; break; }
        }

        if (robot != null)
        {
            Physics.SyncTransforms();

            var rends = robot.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

                Vector3 lp = robot.localPosition;
                lp.y = nova.position.y - b.min.y;
                robot.localPosition = lp;

                Debug.Log("Robot: pie en y=" + b.min.y.ToString("F3") +
                          " -> localOffset.y=" + lp.y.ToString("F3") +
                          " | altura=" + b.size.y.ToString("F3") +
                          " | ancho=" + b.size.x.ToString("F3") +
                          " | fondo=" + b.size.z.ToString("F3"));
            }
        }
        else
        {
            Debug.LogError("No se encontro NOVAVisual_Robot bajo NOVA.");
        }

        Debug.Log("NOVA final: " + nova.position.ToString("F3"));

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Escena guardada: " + EditorSceneManager.SaveScene(scene));

        Debug.Log("########## GROUNDALIGN FIN ##########");
    }

    /// <summary>
    /// Lista los bounds de cada mesh del robot para detectar cual outlier
    /// esta inflando el tamano total.
    /// </summary>
    public static void DiagnoseRobot()
    {
        Debug.Log("########## DIAGNOSE INICIO ##########");

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject robot = GameObject.Find("NOVAVisual_Robot");

        if (robot == null)
        {
            Debug.LogError("No se encontro NOVAVisual_Robot.");
            return;
        }

        Debug.Log("Robot: " + robot.name +
                  " escalaLocal=" + robot.transform.localScale.ToString("F4"));

        foreach (var mf in robot.GetComponentsInChildren<MeshFilter>(true))
        {
            Mesh m = mf.sharedMesh;
            if (m == null)
            {
                Debug.Log("  SIN MESH: " + mf.name);
                continue;
            }

            Bounds b = m.bounds;
            Vector3 c = b.center;

            Debug.Log("  " + mf.gameObject.name +
                      " verts=" + m.vertexCount +
                      " size=" + b.size.ToString("F3") +
                      " center=" + c.ToString("F3"));
        }

        var rends = robot.GetComponentsInChildren<Renderer>(true);
        Bounds all = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) all.Encapsulate(rends[i].bounds);

        Debug.Log("TOTAL: " + rends.Length + " renderers, bounds=" +
                  all.size.ToString("F3") + " center=" + all.center.ToString("F3"));

        Debug.Log("########## DIAGNOSE FIN ##########");
    }

    // ================================================================

    static void EnsureCameraOnEira()
    {
        // Eira es el unico PlayerController que no es NOVA.
        PlayerController eira = null;
        foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include))
        {
            if (pc == null) continue;
            if (pc.name.Contains("NOVA") || pc.name.Contains("Nova")) continue;
            eira = pc;
            break;
        }

        if (eira == null)
        {
            Debug.LogError("No se encontro a Eira (PlayerController).");
            return;
        }

        Debug.Log("Eira localizada en " + eira.transform.position);

        // La camara principal de la escena se llama EiraCamera.
        Camera cam = null;

        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null) continue;
            if (c.CompareTag("MainCamera") || c.gameObject.name == "EiraCamera")
            {
                cam = c;
                break;
            }
        }

        if (cam == null)
        {
            cam = Camera.main;
            Debug.Log("No se encontro camara con tag MainCamera; se usa Camera.main");
        }

        if (cam == null)
        {
            Debug.LogError("No se encontro ninguna camara en la escena.");
            return;
        }

        // Asegurar tag MainCamera (Camera.main lo necesita).
        if (!cam.CompareTag("MainCamera"))
        {
            Debug.Log("La camara " + cam.gameObject.name + " no era MainCamera; se corrige.");
            cam.gameObject.tag = "MainCamera";
        }

        var fp = cam.GetComponent<FirstPersonCamera>();
        if (fp == null)
            fp = cam.gameObject.AddComponent<FirstPersonCamera>();

        // Apuntar a Eira, nunca a NOVA.
        fp.Target = eira.transform;
        fp.SnapBehind();

        Debug.Log("Camara " + cam.gameObject.name +
                  " -> FirstPersonCamera con objetivo " + fp.Target.name);
    }

    // ================================================================

    static void Report(Scene scene)
    {
        Debug.Log("===== REPORTE =====");

        foreach (var root in scene.GetRootGameObjects())
        {
            string s = "  raiz: " + root.name +
                       " pos=" + root.transform.position.ToString("F2") +
                       " escala=" + root.transform.localScale.ToString("F2");

            var novaComp = root.GetComponent<NovaCompanion>();
            if (novaComp != null)
                s += "   <<< NOVA";

            var pc = root.GetComponent<PlayerController>();
            if (pc != null)
                s += "   <<< EIRA (PlayerController)";

            Debug.Log(s);
        }

        // Detalle de NOVA
        foreach (var nc in Object.FindObjectsByType<NovaCompanion>(FindObjectsInactive.Include))
        {
            if (nc == null) continue;

            Debug.Log("--- NOVA: " + nc.gameObject.name + " ---");
            Debug.Log("    componentes: " + nc.GetComponents<Component>().Length);
            Debug.Log("    hijos: " + nc.transform.childCount);

            foreach (Transform t in nc.transform)
            {
                int mrs = t.GetComponentsInChildren<MeshRenderer>(true).Length;
                Debug.Log("      hijo: " + t.name +
                          " escala=" + t.localScale.ToString("F3") +
                          " offset=" + t.localPosition.ToString("F3") +
                          " meshRenderers=" + mrs);
            }
        }

        // Camara
        foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (c == null) continue;
            var fp = c.GetComponent<FirstPersonCamera>();
            string t = fp != null && fp.Target != null ? fp.Target.name : "<sin objetivo>";
            Debug.Log("Camara " + c.gameObject.name +
                      " tag=" + c.tag +
                      " FirstPersonCamera=" + (fp != null) +
                      " objetivo=" + t);
        }

        // Verificar que el personaje viejo ya no existe
        int viejos = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t != null && t.name == "NOVAVisual")
                viejos++;
        }
        Debug.Log("Objetos llamada 'NOVAVisual' (personaje antiguo): " + viejos);

        int robots = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t != null && t.name == "NOVAVisual_Robot")
                robots++;
        }
        Debug.Log("Objetos llamada 'NOVAVisual_Robot' (nuevo): " + robots);
    }
}
