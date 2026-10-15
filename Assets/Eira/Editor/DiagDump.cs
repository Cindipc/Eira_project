using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    public static class DiagDump
    {
        [MenuItem("Eira/SecretLab/DiagDump")]
        public static void Run()
        {
            Debug.Log("[Diag] ============ Level1Scene ============");
            DumpScene("Assets/Eira/Scenes/Level1Scene.unity", "SecretLaboratory_Environment");
            Debug.Log("[Diag] ============ Level2Scene ============");
            DumpScene("Assets/Eira/Scenes/Level2Scene.unity", "SecretLaboratory_Environment");
        }

        static void DumpScene(string path, string envName)
        {
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Debug.Log("[Diag] roo_TOTAL=" + sc.rootCount);
            foreach (var go in sc.GetRootGameObjects())
                Debug.Log("[Diag]   ROOT '" + go.name + "' active=" + go.activeSelf + " pos=" + F(go.transform.position) + " rot=" + F(go.transform.eulerAngles) + " scl=" + F(go.transform.localScale));

            var env = GameObject.Find(envName);
            if (env == null) { Debug.Log("[Diag] NO env '" + envName + "'"); return; }
            Debug.Log("[Diag] ENV '" + envName + "' pos=" + F(env.transform.position) + " rot=" + F(env.transform.eulerAngles) + " scl=" + F(env.transform.localScale));

            foreach (var r in env.GetComponentsInChildren<Renderer>(true))
            {
                Bounds b = r.bounds;
                Debug.Log("[Diag]   REND '" + r.name + "' parent=" + (r.transform.parent ? r.transform.parent.name : "-") +
                          " worldPos=" + F(r.transform.position) + " locPos=" + F(r.transform.localPosition) +
                          " scl=" + F(r.transform.localScale) + " bmin=" + F(b.min) + " bmax=" + F(b.max));
            }
            foreach (var lt in env.GetComponentsInChildren<Light>(true))
                Debug.Log("[Diag]   LIGHT '" + lt.name + "' type=" + lt.type + " pos=" + F(lt.transform.position) + " loc=" + F(lt.transform.localPosition) + " inten=" + lt.intensity + " range=" + lt.range);

            var eira = GameObject.Find("Eira");
            if (eira != null)
            {
                Debug.Log("[Diag] EIRA pos=" + F(eira.transform.position));
                foreach (var r in eira.GetComponentsInChildren<Renderer>(true))
                    Debug.Log("[Diag]   EIRA_REND '" + r.name + "' bmin=" + F(r.bounds.min) + " bmax=" + F(r.bounds.max));
                var cc = eira.GetComponent<CharacterController>();
                if (cc != null) Debug.Log("[Diag]   EIRA_CC height=" + cc.height + " center=" + F(cc.center) + " radius=" + cc.radius + " skin=" + cc.skinWidth);
            }
            var nova = GameObject.Find("NOVA");
            if (nova != null)
            {
                Debug.Log("[Diag] NOVA pos=" + F(nova.transform.position));
                foreach (var r in nova.GetComponentsInChildren<Renderer>(true))
                    Debug.Log("[Diag]   NOVA_REND '" + r.name + "' bmin=" + F(r.bounds.min) + " bmax=" + F(r.bounds.max));
            }
        }

        static string F(Vector3 v) => $"({v.x:F2},{v.y:F2},{v.z:F2})";
    }
}