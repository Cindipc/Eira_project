using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Forma de parpadeo de una luz de laboratorio abandonado.
    /// </summary>
    public enum FlickerMode
    {
        /// <summary>Titileo rápido e irregular: tubo fluorescente moribundo.</summary>
        Flicker,

        /// <summary>Respiración lenta con ruido: baliza de emergencia estable.</summary>
        Pulse,

        /// <summary>Encendido y apagado a ritmo fijo: alarma.</summary>
        Blink,

        /// <summary>Caídas de tensión largas: la luz se apaga y vuelve poco a poco.</summary>
        BrownOut
    }

    /// <summary>
    /// Parpadeo de intensidad para las luces y pantallas del laboratorio.
    ///
    /// Se usa ruido de Perlin en lugar de Random puro porque el ruido puro
    /// produce saltos por frame que parpadean de forma desagradable; el ruido
    /// da la irregularidad electricianamente creíble de un tubo dying.
    ///
    /// No necesita corrutina ni Update si la luz es estática: si el componente
    /// está desactivado o la luz es horneada, no hace nada.
    /// </summary>
    [AddComponentMenu("Eira/Lab Flicker")]
    [DisallowMultipleComponent]
    public class LabFlicker : MonoBehaviour
    {
        [Header("Objetivos")]
        [Tooltip("Luz que parpadea. Si se deja vacía se usa la de este GameObject.")]
        public Light targetLight;

        [Tooltip("Renderer cuyo material emisivo parpadea (pantallas, letreros). Se instancia el material, no se toca el asset.")]
        public Renderer emissiveRenderer;

        [Header("Comportamiento")]
        public FlickerMode mode = FlickerMode.Flicker;

        [Min(0f)] public float baseIntensity = 2f;
        [Range(0f, 1f)] public float jitter = 0.55f;
        [Range(0f, 1f)] public float minFactor = 0.04f;
        [Min(0.1f)] public float speed = 11f;

        [Header("Emisión (opcional)")]
        public Color emissionColor = Color.white;
        [Min(0f)] public float emissionIntensity = 3f;

        [Header("Variación")]
        [Tooltip("Desfase por instancia para que dos luces iguales no parpadeen al unísono.")]
        [SerializeField] private float phase;

        [Tooltip("Se rellena solo con GetInstanceID; cámbialo a mano para un patrón distinto.")]
        [SerializeField] private bool randomPhase = true;

        private Material _emissiveMaterial;
        private bool _initialized;
        private float _dipTimer;
        private float _dipDepth;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;

            if (targetLight == null)
                targetLight = GetComponent<Light>();

            if (randomPhase)
                phase = (GetHashCode() % 997) * 0.0137f;

            // material instanciado: la pantalla parpadea sin arrastrar al
            // resto de objetos que comparten el material del proyecto.
            if (emissiveRenderer != null)
            {
                _emissiveMaterial = emissiveRenderer.material;

                if (!_emissiveMaterial.HasProperty("_EMISSION"))
                    _emissiveMaterial.EnableKeyword("_EMISSION");
            }
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void OnValidate()
        {
            baseIntensity = Mathf.Max(0f, baseIntensity);
            minFactor = Mathf.Clamp01(minFactor);
            jitter = Mathf.Clamp01(jitter);
            speed = Mathf.Max(0.1f, speed);
            emissionIntensity = Mathf.Max(0f, emissionIntensity);
        }

        private void Update()
        {
            if (!_initialized)
                Initialize();

            float factor = Evaluate();

            if (targetLight != null)
                targetLight.intensity = baseIntensity * factor;

            if (_emissiveMaterial != null && _emissiveMaterial.HasProperty("_EmissionColor"))
                _emissiveMaterial.SetColor("_EmissionColor", emissionColor * (emissionIntensity * factor));
        }

        /// <summary>
        /// Multiplicador de intensidad en este frame, entre minFactor y 1.
        /// </summary>
        private float Evaluate()
        {
            float t = (Time.time + phase) * speed;

            switch (mode)
            {
                case FlickerMode.Pulse:
                    return Mathf.Lerp(1f - jitter, 1f, Mathf.PerlinNoise(t * 0.35f, 0.7f));

                case FlickerMode.Blink:
                    return Mathf.PerlinNoise(t * 0.9f, 3.1f) > 0.52f ? 1f : minFactor;

                case FlickerMode.BrownOut:
                    return BrownOut(t);

                default:
                    return Flicker(t);
            }
        }

        private float Flicker(float t)
        {
            // Dos octavas de ruido con ritmos distintos: la rápida da el
            // titileo del tubo, la lenta decide si ahora toca un hueco largo.
            float fast = Mathf.PerlinNoise(t, 0.13f);
            float slow = Mathf.PerlinNoise(t * 0.11f, 5.7f);

            float on = Mathf.Lerp(1f - jitter, 1f, fast);
            float gate = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.25f, 0.45f, slow));

            return Mathf.Max(minFactor, on * gate);
        }

        private float BrownOut(float t)
        {
            // Una caída de tensión dura varios segundos y se recupera sola.
            _dipTimer -= Time.deltaTime;

            if (_dipTimer <= 0f)
            {
                _dipTimer = Random.Range(4f, 14f);

                _dipDepth = Random.value < 0.45f
                    ? Random.Range(minFactor, 0.22f)
                    : Random.Range(0.5f, 0.9f);
            }

            float breath = Mathf.PerlinNoise(t * 0.25f, 11.3f);

            return Mathf.Lerp(minFactor, 1f, breath) * _dipDepth + (1f - _dipDepth);
        }
    }
}