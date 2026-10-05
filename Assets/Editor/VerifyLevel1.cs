using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Menu: Eira > Verificar Nivel 1. No modifica nada.
public static class VerifyLevel1
{
    [MenuItem("Eira/Verificar Nivel 1")]
    public static void Run()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Eira/Scenes/Level1Scene.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();

        var sb = new StringBuilder();
        sb.AppendLine("=== VERIFICACION DEL NIVEL 1 ===");

        // --- Raices ---
        GameObject[] roots = scene.GetRootGameObjects();
        sb.AppendLine("Raices: " + roots.Length);
        for (int i = 0; i < roots.Length; i++)
            sb.AppendLine("   - " + roots[i].name);

        // --- Escenario viejo: debe estar ausente ---
        string[] viejos = { "Escenografia", "Laboratorio", "FuturisticCity_Environment" };
        for (int i = 0; i < viejos.Length; i++)
        {
            GameObject g = GameObject.Find(viejos[i]);
            sb.AppendLine("'" + viejos[i] + "': " + (g == null ? "AUSENTE" : "presente"));
        }

        // --- Ciudad ---
        GameObject city = GameObject.Find("FuturisticCity_Environment");
        if (city != null)
        {
            Bounds b = Bounds(city);
            sb.AppendLine("Ciudad: tam=" + b.size.ToString("F1") +
                          " colliders=" + city.GetComponentsInChildren<Collider>(true).Length +
                          " renderers=" + city.GetComponentsInChildren<Renderer>(true).Length);
            sb.AppendLine("Ciudad XZ: " + b.min.x.ToString("F0") + ".." + b.max.x.ToString("F0") +
                          " / " + b.min.z.ToString("F0") + ".." + b.max.z.ToString("F0"));
        }

        // --- Eira ---
        GameObject eira = GameObject.Find("Eira");
        if (eira != null)
        {
            CharacterController cc = eira.GetComponent<CharacterController>();
            float baseY = eira.transform.position.y +
                (cc != null ? cc.center.y - cc.height * 0.5f : 0f);
            sb.AppendLine();
            sb.AppendLine("Eira pos=" + eira.transform.position.ToString("F2") +
                          " | altura Eira=" + eira.transform.lossyScale.y.ToString("F2") +
                          " | rotY=" + eira.transform.eulerAngles.y.ToString("F0"));
            if (cc != null)
                sb.AppendLine("  CC height=" + cc.height.ToString("F2") +
                              " radius=" + cc.radius.ToString("F2") +
                              " center=" + cc.center.ToString("F2") +
                              " -> pies=" + baseY.ToString("F2"));

            Renderer[] er = eira.GetComponentsInChildren<Renderer>(true);
            Bounds vb = new Bounds();
            bool first = true;
            for (int i = 0; i < er.Length; i++)
            {
                if (first) { vb = er[i].bounds; first = false; }
                else vb.Encapsulate(er[i].bounds);
            }
            sb.AppendLine("  visual tam=" + vb.size.ToString("F2") +
                          " minY=" + vb.min.y.ToString("F2") +
                          " centradoXZ=" +
                          (Mathf.Abs(vb.center.x - eira.transform.position.x) < 0.2f &&
                           Mathf.Abs(vb.center.z - eira.transform.position.z) < 0.2f));

            // Suelo real debajo de Eira (ignorando a Eira misma)
            RaycastHit hit;
            Vector3 from = eira.transform.position + Vector3.up * 30f +
                           Vector3.right * 1.5f;
            if (Physics.Raycast(from, Vector3.down, out hit, 200f))
                sb.AppendLine("  RAYCAST suelo: '" + hit.collider.name + "' y=" +
                              hit.point.y.ToString("F2") + "  (pies " +
                              (baseY - hit.point.y).ToString("F2") + " m sobre el suelo)");
            else
                sb.AppendLine("  RAYCAST suelo: NADA (Eira caeria)");
        }

        // --- NOVA ---
        GameObject nova = GameObject.Find("NOVA");
        if (nova != null)
        {
            Renderer[] nr = nova.GetComponentsInChildren<Renderer>(true);
            Bounds nb = new Bounds();
            bool first = true;
            for (int i = 0; i < nr.Length; i++)
            {
                if (first) { nb = nr[i].bounds; first = false; }
                else nb.Encapsulate(nr[i].bounds);
            }

            Transform prefabRoot = nova.transform.Find("NOVAVisual_Robot");
            string prefab = "no-prefab";
            if (prefabRoot != null)
            {
                string ap = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    prefabRoot.gameObject);
                if (!string.IsNullOrEmpty(ap))
                    prefab = System.IO.Path.GetFileNameWithoutExtension(ap);
            }

            sb.AppendLine();
            sb.AppendLine("NOVA pos=" + nova.transform.position.ToString("F2") +
                          " esc=" + nova.transform.lossyScale.ToString("F3"));
            sb.AppendLine("  prefab=" + prefab +
                          " renderers=" + nr.Length +
                          " tam=" + nb.size.ToString("F2") +
                          " pies=" + nb.min.y.ToString("F2") +
                          " maxY=" + nb.max.y.ToString("F2"));

            // NOVA volteado? Se mira el nodo raiz del visual, no cada pieza
            // (los brazos y piernas del robot van girados por diseño).
            Transform vroot = prefabRoot != null ? prefabRoot : nova.transform;
            Vector3 vup = vroot.up;
            sb.AppendLine("  recto=" + (Vector3.Dot(vup, Vector3.up) > 0.99f) +
                          " upVisual=" + vup.ToString("F2") +
                          " | enVertical=" +
                          (nb.max.y - nb.min.y > nb.size.x && nb.max.y - nb.min.y > nb.size.z));

            GameObject cam = GameObject.Find("EiraCamera");
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                Vector3 d = nova.transform.position - cam.transform.position;
                d.y = 0f;
                sb.AppendLine("  cam->NOVA=" + d.magnitude.ToString("F2") + " m,dot=" +
                              Vector3.Dot(f, d.normalized).ToString("F2") +
                              " (1 = justo delante)");
            }
        }

        // --- Camara ---
        GameObject camera = GameObject.Find("EiraCamera");
        if (camera != null)
        {
            MonoBehaviour[] mb = camera.GetComponents<MonoBehaviour>();
            for (int i = 0; i < mb.Length; i++)
            {
                if (mb[i] == null) continue;
                System.Reflection.FieldInfo fi = mb[i].GetType().GetField("Target");
                if (fi != null)
                {
                    object v = fi.GetValue(mb[i]);
                    sb.AppendLine();
                    sb.AppendLine("Camara pos=" + camera.transform.position.ToString("F2") +
                                  " | " + mb[i].GetType().Name + ".Target = " +
                                  (v == null ? "NULL" : ((Object)v).name));
                }
            }
        }

        // --- Objetos de gameplay: sobre suelo? ---
        GameObject gameplay = GameObject.Find("Gameplay");
        if (gameplay != null && city != null)
        {
            Bounds cb = Bounds(city);
            Collider[] cols = city.GetComponentsInChildren<Collider>(true);
            sb.AppendLine();
            sb.AppendLine("GAMEPLAY (¿hay suelo debajo?):");
            Transform[] gt = gameplay.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < gt.Length; i++)
            {
                if (gt[i].parent != gameplay.transform) continue;
                Vector3 p = gt[i].position;
                bool dentro = p.x >= cb.min.x && p.x <= cb.max.x &&
                              p.z >= cb.min.z && p.z <= cb.max.z;

                bool suelo = false; float y = 0f;
                for (int c = 0; c < cols.Length; c++)
                {
                    Bounds b = cols[c].bounds;
                    if (p.x < b.min.x || p.x > b.max.x) continue;
                    if (p.z < b.min.z || p.z > b.max.z) continue;
                    if (b.max.y > y) { y = b.max.y; suelo = true; }
                }
                sb.AppendLine("  " + gt[i].name.PadRight(16) + " pos=" +
                              p.ToString("F1").PadRight(20) +
                              " enCiudad=" + (dentro ? "si" : "NO") +
                              " suelo=" + (suelo ? "si (y=" + y.ToString("F2") + ")" : "NO"));
            }
        }

        sb.AppendLine("=== FIN VERIFICACION ===");
        Debug.Log(sb.ToString());
    }

    static Bounds Bounds(GameObject go)
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
