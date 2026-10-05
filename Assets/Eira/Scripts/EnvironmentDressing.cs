using UnityEngine;
using UnityEngine.Rendering;

namespace EiraGame
{
    /// <summary>
    /// Ambientación futurista del Nivel 1: neón, niebla volumétrica falsa,
    /// polvo suspendido, luz de relleno y post-procesado.
    ///
    /// Se hace en runtime y por código a propósito. El nivel llega de un
    /// prefab con la iluminación casi plana y, editarlo por YAML exigiría
    /// conocer la escena de referencia de cada objeto (no está guardada en el
    /// archivo). Con esto el resultado es idéntico en cualquier máquina y se
    /// puede ajustar tocando números.
    ///
    /// Se ejecuta una sola vez (idempotente): si el objeto ya existe de una
    /// sesión anterior, no se duplica nada.
    /// </summary>
    public class EnvironmentDressing : MonoBehaviour
    {
        [Header("Niebla")]
        [SerializeField] private bool fog = true;

        [SerializeField] private Color fogColor = new Color(0.02f, 0.04f, 0.09f);
        [SerializeField] private float fogDensity = 0.022f;

        [Header("Luces de ambiente")]
        [SerializeField] private bool ambientLights = true;

        [Tooltip("Luces de neón a lo largo del corredor. El nivel mide unos 125 m.")]
        [SerializeField] private int neonLights = 10;

        [SerializeField] private float neonStartX = 2f;
        [SerializeField] private float neonEndX = 118f;
        [SerializeField] private float neonHeight = 3.4f;
        [SerializeField] private float neonRange = 13f;
        [SerializeField] private float neonIntensity = 2.6f;

        [Header("Partículas de polvo")]
        [SerializeField] private bool dust = true;

        [Tooltip("Puntos de polvo repartidos por el corredor para que la luz se vea en el aire.")]
        [SerializeField] private int dustCount = 420;

        [Header("Post-procesado")]
        [SerializeField] private bool postProcessing = true;

        [SerializeField] private float bloomIntensity = 1.5f;
        [SerializeField] private float bloomThreshold = 0.85f;
        [SerializeField] private Color colorGrade = new Color(0.94f, 0.98f, 1.06f);
        [SerializeField] private float vignette = 0.28f;

        static bool installed;

        void Awake()
        {
            // Guard global: si el nivel se recarga, no se duplica la niebla
            // ni se apilan las luces.
            if (installed)
            {
                enabled = false;
                return;
            }

            installed = true;

            ApplyAmbient();
            ApplyFog();
            ApplyNeon();
            ApplyDust();
            ApplyPostProcessing();
        }

        void OnDestroy()
        {
            if (installed)
                installed = false;
        }

        // =========================================================
        // AMBIENTE
        // =========================================================

        void ApplyAmbient()
        {
            // Un ambiente azulado bajo hace que las zonas oscuras se lean
            // como "azul oscuro" y no como "negro roto", que es el error
            // típico de un nivel futurista mal iluminado.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.14f, 0.22f);
            RenderSettings.ambientEquatorColor = new Color(0.05f, 0.07f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.04f);
            RenderSettings.reflectionIntensity = 0.55f;
        }

        void ApplyFog()
        {
            if (!fog)
            {
                RenderSettings.fog = false;
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        // =========================================================
        // NEÓN
        // =========================================================

        void ApplyNeon()
        {
            if (!ambientLights || neonLights <= 0)
                return;

            var parent = new GameObject("NeonRig");
            parent.transform.SetParent(transform, false);

            // Paleta fría con acentos magenta/ámbar: el contraste de color
            // es lo que hace que un corredor gris parezca un sitio futurista.
            Color[] palette =
            {
                new Color(0.15f, 0.85f, 1f),
                new Color(0.55f, 0.25f, 1f),
                new Color(0.1f, 1f, 0.75f),
                new Color(1f, 0.35f, 0.75f)
            };

            float span = Mathf.Max(1f, neonEndX - neonStartX);

            for (int i = 0; i < neonLights; i++)
            {
                float t = neonLights == 1 ? 0.5f : i / (float)(neonLights - 1);
                float x = Mathf.Lerp(neonStartX, neonEndX, t);

                Color color = palette[i % palette.Length];

                // Se alternan los lados: da sensación de recorrido, no de
                // hilera simétrica.
                float z = (i % 2 == 0) ? -9.5f : 9.5f;

                var go = new GameObject("Neon_" + i);
                go.transform.SetParent(parent.transform, false);
                go.transform.position = new Vector3(x, neonHeight, z);

                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = color;
                light.range = neonRange;
                light.intensity = neonIntensity;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.ForceVertex;

                AddNeonStrip(go.transform, color);
            }
        }

        /// <summary>
        /// Barra emisiva sobre la luz. La luz sola no se ve (solo ilumina);
        /// la barra es lo que el jugador identifica como neón.
        /// </summary>
        void AddNeonStrip(Transform parent, Color color)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Strip";

            var col = strip.GetComponent<Collider>();
            if (col != null)
                Destroy(col);

            strip.transform.SetParent(parent, false);
            strip.transform.localScale = new Vector3(2.6f, 0.1f, 0.1f);
            strip.transform.localPosition = new Vector3(0f, -0.05f, 0f);

            strip.GetComponent<Renderer>().material = CombatFX.GlowMaterial(color, 7f);
        }

        // =========================================================
        // POLVO
        // =========================================================

        void ApplyDust()
        {
            if (!dust || dustCount <= 0)
                return;

            var go = new GameObject("DustParticles");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.loop = true;
            main.startLifetime = 14f;
            main.startSpeed = 0.12f;
            main.startSize = 0.045f;
            main.startColor = new Color(0.7f, 0.9f, 1f, 0.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = dustCount;
            main.playOnAwake = true;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(124f, 4.5f, 22f);
            shape.position = new Vector3(59f, 2.2f, 0f);

            // Deriva lenta: el polvo baja y sube, como si hubiera
            // ventilación en el techo.
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.09f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.12f;
            noise.frequency = 0.4f;
            noise.scrollSpeed = 0.25f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = CombatFX.GlowMaterial(new Color(0.8f, 0.95f, 1f), 2.2f);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        // =========================================================
        // POST-PROCESADO
        // =========================================================

        void ApplyPostProcessing()
        {
            if (!postProcessing)
                return;

            var cam = Camera.main;

            if (cam == null)
                cam = FindAnyObjectByType<Camera>();

            if (cam == null)
                return;

            // El paquete de post-procesado de URP no siempre está en el
            // proyecto, y referenciarlo sin él rompe la compilación. Se
            // busca por tipo una sola vez y se ignora si no existe.
            AddVolume<UnityEngine.Rendering.Universal.Bloom>(cam, b =>
            {
                b.intensity.overrideState = true;
                b.intensity.value = bloomIntensity;
                b.threshold.overrideState = true;
                b.threshold.value = bloomThreshold;
            });

            AddVolume<UnityEngine.Rendering.Universal.ColorAdjustments>(cam, c =>
            {
                c.colorFilter.overrideState = true;
                c.colorFilter.value = colorGrade;
                c.postExposure.overrideState = true;
                c.postExposure.value = 0.15f;
            });

            AddVolume<UnityEngine.Rendering.Universal.Vignette>(cam, v =>
            {
                v.intensity.overrideState = true;
                v.intensity.value = vignette;
                v.smoothness.overrideState = true;
                v.smoothness.value = 0.45f;
            });
        }

        /// <summary>
        /// Añade un override a la pila de post-procesado de la cámara. Si la
        /// cámara no trae volumen (el caso normal si nadie lo ha puesto en el
        /// nivel), se crea uno hijo suyo para que bloom y color grade
        /// funcionen igual.
        /// </summary>
        static void AddVolume<T>(Camera cam, System.Action<T> configure)
            where T : VolumeComponent
        {
            Volume volume = cam.GetComponent<Volume>();

            if (volume == null)
            {
                var go = new GameObject("PostProcessVolume");
                go.transform.SetParent(cam.transform, false);

                volume = go.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1f;
                volume.weight = 1f;
            }

            // Volume.profile crea el perfil en memoria si no hay ninguno
            // asignado, así que no hace falta guardarlo en disco.
            T component = volume.profile.TryGet(out T found) ? found : null;

            if (component == null)
                component = volume.profile.Add<T>(true);

            configure(component);
        }
    }
}
