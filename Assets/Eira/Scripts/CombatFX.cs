using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Fábrica de materiales y efectos compartidos por el combate.
    ///
    /// Se centraliza aquí porque buscar el shader y crear el material se
    /// hace en varios sitios y antes cada uno repetía el código con posibles
    /// diferencias (por eso unos brillaban y otros no).
    ///
    /// Todos los materiales se cachean: crear uno por disparo generaba GC y
    /// llenaba la lista de objetos del editor.
    /// </summary>
    public static class CombatFX
    {
        static Shader litShader;
        static Shader glowShader;

        static readonly System.Collections.Generic.Dictionary<int, Material> cache =
            new System.Collections.Generic.Dictionary<int, Material>();

        /// <summary>Shader con emisión, para todo lo que debe brillar.</summary>
        public static Shader Glow
        {
            get
            {
                if (glowShader != null)
                    return glowShader;

                // URP primero, luego los shaders clásicos como reserva.
                glowShader = Shader.Find("Universal Render Pipeline/Lit");
                if (glowShader == null)
                    glowShader = Shader.Find("Standard");
                if (glowShader == null)
                    glowShader = Shader.Find("Sprites/Default");

                return glowShader;
            }
        }

        /// <summary>Shader sin emisión, para superficies mates.</summary>
        public static Shader Lit
        {
            get
            {
                if (litShader != null)
                    return litShader;

                litShader = Shader.Find("Universal Render Pipeline/Lit");
                if (litShader == null)
                    litShader = Shader.Find("Standard");

                return litShader;
            }
        }

        /// <summary>
        /// Material emisivo de un color. El mismo color devuelve siempre la
        /// misma instancia, así que no se acumulan materiales en runtime.
        /// </summary>
        public static Material GlowMaterial(Color color, float intensity = 2.5f)
        {
            int key = Quantize(color) ^ (Mathf.RoundToInt(intensity * 10f) << 24);

            if (cache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            var m = new Material(Glow)
            {
                name = "FX_" + ColorUtility.ToHtmlStringRGB(color),
                color = color
            };

            EnableEmission(m, color * intensity);

            cache[key] = m;
            return m;
        }

        public static Material LitMaterial(Color color)
        {
            int key = Quantize(color) ^ 0x5A5A5A5A;

            if (cache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            var m = new Material(Lit)
            {
                name = "Lit_" + ColorUtility.ToHtmlStringRGB(color),
                color = color
            };

            cache[key] = m;
            return m;
        }

        static void EnableEmission(Material m, Color emission)
        {
            // "_EmissionColor" + "_EMISSION" es el par de URP.
            // "._EmissionColor" + "_Color" es el de los shaders clásicos.
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return;
            }

            if (m.HasProperty("_Color"))
                m.SetColor("_Color", emission);
        }

        /// <summary>
        /// Reduce el color a 5 bits por canal. Sin esto, dos colores "casi
        /// iguales" generaban materiales distintos y la cache no servía.
        /// </summary>
        static int Quantize(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 31f), 0, 31);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 31f), 0, 31);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 31f), 0, 31);
            return (r << 10) | (g << 5) | b;
        }

        /// <summary>Esfera emisiva sin collider, para FX.</summary>
        public static GameObject Orb(Vector3 position, float size, Color color, float intensity = 3f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var col = go.GetComponent<Collider>();
            if (col != null)
                Object.Destroy(col);

            go.name = "FX_Orb";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;

            var r = go.GetComponent<Renderer>();
            if (r != null)
                r.material = GlowMaterial(color, intensity);

            return go;
        }

        /// <summary>
        /// Destello de disparo: una esfera que crece y se apaga. No bloquea
        /// nada ni necesita Destroy inmediato, así que es seguro crearla en
        /// medio del Update del jugador.
        /// </summary>
        public static void MuzzleFlash(Vector3 position, Color color)
        {
            var orb = Orb(position, 0.35f, color, 6f);
            orb.AddComponent<FlashFade>();
        }

        /// <summary>Explosión de dron: orbes que salen despedidos.</summary>
        public static void Explosion(Vector3 position, Color color, int pieces = 10)
        {
            for (int i = 0; i < pieces; i++)
            {
                var orb = Orb(position, Random.Range(0.12f, 0.3f), color, 5f);
                orb.AddComponent<BurstPiece>();
            }
        }

        /// <summary>Chispa corta de impacto.</summary>
        public static void Impact(Vector3 position, Color color)
        {
            var orb = Orb(position, 0.22f, color, 4f);
            orb.AddComponent<FlashFade>();
        }
    }

    /// <summary>Esfera que se agranda y se desvanece, y se destruye sola.</summary>
    public class FlashFade : MonoBehaviour
    {
        float life = 0.12f;
        float maxScale;
        Renderer[] renderers;

        void Start()
        {
            maxScale = transform.localScale.x;
            renderers = GetComponentsInChildren<Renderer>();
        }

        void Update()
        {
            life -= Time.deltaTime;

            float t = life > 0f ? life / 0.12f : 0f;

            transform.localScale = Vector3.one * (maxScale * (1.6f - 0.6f * t));

            if (renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null)
                        continue;

                    // Se desvanece por el color: funciona en cualquier
                    // pipeline sin depender de _BaseColor ni de HDRP.
                    MaterialPropertyBlock block = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(block);
                    Color c = renderers[i].material.color;
                    c.a = t;
                    block.SetColor("_BaseColor", c);
                    block.SetColor("_Color", c);
                    renderers[i].SetPropertyBlock(block);
                }
            }

            if (life <= 0f)
                Destroy(gameObject);
        }
    }

    /// <summary>Trozo de explosión con velocidad y gravedad.</summary>
    public class BurstPiece : MonoBehaviour
    {
        Vector3 velocity;
        float life = 0.55f;
        float maxScale;

        void Start()
        {
            maxScale = transform.localScale.x;

            Vector3 dir = Random.onUnitSphere;
            dir.y = Mathf.Abs(dir.y) * 0.8f + 0.2f;
            velocity = dir.normalized * Random.Range(2.5f, 6.5f);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            life -= dt;
            velocity += Vector3.down * 9f * dt;
            transform.position += velocity * dt;
            transform.Rotate(220f * dt, 130f * dt, 0f, Space.World);

            float t = Mathf.Clamp01(life / 0.55f);
            transform.localScale = Vector3.one * (maxScale * t);

            if (life <= 0f)
                Destroy(gameObject);
        }
    }
}
