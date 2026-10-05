using System.Text;
using EiraGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Correccion del Nivel 1 en un clic. Menu: Eira > Corregir NOVA y Camara
///
/// Regla del suelo: la referencia es la base del CharacterController de
/// Eira. Se descartan las superficies que esten mas de GroundTolerance por
/// ENCIMA de esa referencia, porque en esta ciudad hay un plano (Object_2,
/// 16 vertices) a y=1.892 que tapa el raycast y hacia flotar a NOVA.
///
/// Eira no se mueve ni se escala: solo se recoloca su VISUAL (EiraBase),
/// cuyo pivote viene desplazado desde el FBX.
/// </summary>
public static class FixNovaAndCamera
{
    const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
    const float GroundTolerance = 0.35f;

    [MenuItem("Eira/Corregir NOVA y Camara")]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== CORRECCION DE NOVA, EIRA Y CAMARA ===");

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        GameObject nova = GameObject.Find("NOVA");
        GameObject eira = GameObject.Find("Eira");

        if (nova == null || eira == null)
        {
            Debug.LogError(sb + "\nFalta NOVA o Eira. No se toca nada.");
            return;
        }

        Physics.SyncTransforms();

        var cc = eira.GetComponent<CharacterController>();
        float refFeet = eira.transform.position.y +
                        (cc != null ? cc.center.y - cc.height * 0.5f : 0f);

        float targetHeight = (cc != null && cc.height > 0.1f) ? cc.height : 1.8f;

        sb.AppendLine("Eira transform=" + eira.transform.position.ToString("F2") +
                      " | base del CharacterController=" + refFeet.ToString("F3") +
                      " | altura objetivo=" + targetHeight.ToString("F2"));

        // ---------------------------------------------------------------
        // 1. REFERENCIA DE SUELO
        //
        // No se usa raycast: la ciudad (Sketchfab, escala 0.157) tiene el
        // suelo con las normales invertidas, asi que los rayos frontales
        // no lo encuentran y solo devuelven el plano Object_2 a y=1.892.
        // La referencia fiable es la base del CharacterController de Eira:
        // es exactamente donde el juego considera que estan sus pies.
        // ---------------------------------------------------------------
        float groundY = refFeet;

        sb.AppendLine("Suelo de referencia = base del CharacterController = " +
                      groundY.ToString("F3") + "  (raycast descartado: " +
                      "normales del FBX de ciudad invertidas)");

        // ---------------------------------------------------------------
        // 2. EIRA: solo su visual. El pivote del FBB viene desplazado.
        // ---------------------------------------------------------------
        Transform evis = VisualOf(eira.transform);
        if (evis != null)
        {
            Bounds eb = WorldBounds(evis.gameObject);
            sb.AppendLine();
            sb.AppendLine("EiraBase ANTES: node=" + evis.position.ToString("F2") +
                          " localPos=" + evis.localPosition.ToString("F2") +
                          " boundsMin=" + eb.min.ToString("F2") +
                          " max=" + eb.max.ToString("F2") +
                          " size=" + eb.size.ToString("F2"));

            // Se mueve el VISUAL, no Eira. El delta se calcula sobre los
            // bounds de la geometria (no sobre el nodo), porque el FBX trae
            // el pivote corrido unos 4 m respecto al collider.
            Vector3 delta = new Vector3(
                eira.transform.position.x - eb.center.x,   // centrar en X
                groundY - eb.min.y,                        // pies al nivel de Eira
                eira.transform.position.z - eb.center.z);  // centrar en Z

            evis.position += delta;

            Physics.SyncTransforms();
            Bounds eb2 = WorldBounds(evis.gameObject);

            sb.AppendLine("EiraBase DESPUES: node=" + evis.position.ToString("F2") +
                          " boundsMin=" + eb2.min.ToString("F2") +
                          " max=" + eb2.max.ToString("F2") +
                          " size=" + eb2.size.ToString("F2"));
            sb.AppendLine("  -> centrado en XZ sobre Eira, pies en " +
                          groundY.ToString("F3"));
            EditorUtility.SetDirty(evis);
        }

        // ---------------------------------------------------------------
        // 3. NOVA: mismo tamaño que Eira y sobre el suelo real
        // ---------------------------------------------------------------
        GameObject visual = VisualOf(nova.transform) != null
            ? VisualOf(nova.transform).gameObject : null;

        if (visual == null)
        {
            sb.AppendLine();
            sb.AppendLine("NOVA no tiene visual.");
            Debug.Log(sb.ToString());
            return;
        }

        Bounds before = WorldBounds(visual);
        sb.AppendLine();
        sb.AppendLine("NOVA ANTES: pos=" + nova.transform.position.ToString("F2") +
                      " esc=" + visual.transform.lossyScale.ToString("F3") +
                      " bounds=" + before.size.ToString("F2"));

        if (before.size.y > 0.0001f)
        {
            float s = targetHeight / before.size.y;
            visual.transform.localScale *= s;
            Physics.SyncTransforms();
        }

        Bounds vb = WorldBounds(visual);
        float feetOffset = vb.min.y - nova.transform.position.y;

        // NOVA debe verse en primera persona: se coloca delante y a la
        // derecha de Eira (unos 3.8 m de la camara, ~25 grados de
        // desviacion, siempre dentro del cono de vision). Antes solo se
        // apartaba si estaba a menos de 2.5 m y lo hacia en radial, lo
        // que podia dejarlo DETRAS de la camara.
        Vector3 np = nova.transform.position;

        Vector3 fwd = eira.transform.forward; fwd.y = 0f;
        Vector3 rgt = eira.transform.right;   rgt.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
        if (rgt.sqrMagnitude < 0.001f) rgt = Vector3.right;
        fwd.Normalize(); rgt.Normalize();

        Vector3 visible = eira.transform.position + fwd * 3.4f + rgt * 1.6f;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 a = cam.transform.position;
            Vector3 d = new Vector3(np.x - a.x, 0f, np.z - a.z);

            sb.AppendLine("NOVA ANTES de colocarlo: a " + d.magnitude.ToString("F2") +
                          " m de la camara, dot=" +
                          Vector3.Dot(cam.transform.forward, d.normalized).ToString("F2") +
                          " (<0 = detras de la camara)");
        }

        np = new Vector3(visible.x, np.y, visible.z);

        nova.transform.position = new Vector3(np.x, groundY - feetOffset, np.z);
        Physics.SyncTransforms();

        Bounds fin = WorldBounds(visual);
        sb.AppendLine("NOVA DESPUES: pos=" + nova.transform.position.ToString("F2") +
                      " esc=" + visual.transform.lossyScale.ToString("F3"));
        sb.AppendLine("  bounds=" + fin.size.ToString("F3") +
                      "  alto=" + fin.size.y.ToString("F3") +
                      " ancho=" + fin.size.x.ToString("F3") +
                      " fondo=" + fin.size.z.ToString("F3"));
        sb.AppendLine("  pies=" + fin.min.y.ToString("F3") +
                      " | altura de Eira=" + targetHeight.ToString("F2") +
                      " | nivel de Eira=" + groundY.ToString("F3"));
        EditorUtility.SetDirty(nova.transform);

        // ---------------------------------------------------------------
        // 4. CAMARA persiguiendo a Eira
        // ---------------------------------------------------------------
        if (cam != null)
        {
            var fpc = cam.GetComponent<FirstPersonCamera>();
            if (fpc == null) fpc = cam.gameObject.AddComponent<FirstPersonCamera>();
            fpc.Target = eira.transform;
            EditorUtility.SetDirty(fpc);
            sb.AppendLine();
            sb.AppendLine("Camara " + cam.name + " -> FirstPersonCamera.Target = " +
                          (fpc.Target != null ? fpc.Target.name : "NULO"));
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("AVISO: no hay MainCamera en la escena.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        sb.AppendLine("Escena guardada: " + saved);
        sb.AppendLine("=== FIN ===");

        Debug.Log(sb.ToString());
    }

    static Transform VisualOf(Transform t)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            Transform c = t.GetChild(i);
            if (c.GetComponentInChildren<Renderer>(true) != null) return c;
        }
        return null;
    }

    static Bounds WorldBounds(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        bool first = true;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null) continue;
            if (first) { b = rends[i].bounds; first = false; }
            else b.Encapsulate(rends[i].bounds);
        }
        return b;
    }
}
