using UnityEngine;

namespace EiraGame
{
    // Registros de información sobre el año 3000 que Eira encuentra en el laboratorio.
    public class DataLog : Interactable
    {
        public int logId;
        [TextArea(3, 6)] public string content = "Información del año 3000.";
        bool collected;

        void Start()
        {
            promptText = "E — Leer información";
            if (transform.childCount > 0)
            {
                var glow = transform.GetChild(0);
                if (glow != null) glow.localScale = Vector3.one * 0.9f;
            }
        }

        void Update()
        {
            if (collected) return;
            if (transform.childCount > 0)
                transform.GetChild(0).Rotate(0f, 40f * Time.deltaTime, 0f);
        }

        public override void OnInteract(PlayerController p)
        {
            if (collected) return;
            collected = true;
            usable = false;
            AudioFX.Info();
            GameEvents.PickedInfo(content);
            var gm = GameManager.Instance;
            if (gm != null) gm.OnInfoCollected(logId);
            if (transform.childCount > 0)
                transform.GetChild(0).gameObject.SetActive(false);
            EnvOrb.Spawn(transform.position, new Color(0.4f, 0.9f, 1f));
        }
    }

    // Pequeño efecto de partículas simple (estilo ascenso de polvo/energía).
    public static class EnvOrb
    {
        public static void Spawn(Vector3 pos, Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.18f;
            var r = go.GetComponent<Renderer>();
            if (r != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                r.material = new Material(sh != null ? sh : Shader.Find("Standard")) { color = c };
            }
            var mv = go.AddComponent<EnvOrbMove>();
            mv.color = c;
        }
    }

    public class EnvOrbMove : MonoBehaviour
    {
        public Color color;
        float life = 0.9f;
        Vector3 vel;

        void Start()
        {
            vel = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(2.5f, 4f), Random.Range(-1.5f, 1.5f));
        }

        void Update()
        {
            life -= Time.deltaTime;
            transform.position += vel * Time.deltaTime;
            var s = 0.18f * Mathf.Max(0f, life);
            transform.localScale = Vector3.one * s;
            if (life <= 0f) Destroy(gameObject);
        }
    }
}