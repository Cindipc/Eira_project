using System;
using System.Collections.Generic;
using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Sonidos sintetizados proceduralmente con estetica cyberpunk:
    /// ondas sierra/cuadradas desafinadas, bitcrush, zumbidos de neon,
    /// servos hidraulicos y glitches. No necesita archivos de audio.
    /// Cada sonido se genera una sola vez y se guarda en cache.
    /// La API publica es la misma que antes, mas StartAmbient/StopAmbient.
    /// </summary>
    public static class AudioFX
    {
        const int SR = 44100;

        static AudioSource _src;
        static AudioSource _amb;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        static uint _seed = 90210u;
        static int _stepIdx;

        // ---------------------------------------------------------------
        // Inicializacion
        // ---------------------------------------------------------------
        public static void Init()
        {
            if (_src != null) return;
            var go = new GameObject("AudioSFX");
            UnityEngine.Object.DontDestroyOnLoad(go);

            _src = go.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;
            _src.playOnAwake = false;
            _src.volume = 0.7f;

            _amb = go.AddComponent<AudioSource>();
            _amb.spatialBlend = 0f;
            _amb.playOnAwake = false;
            _amb.loop = true;
            _amb.volume = 0.16f;
        }

        // ---------------------------------------------------------------
        // Ambiente: zumbido grave de ciudad/neon en bucle (4 s, sin corte)
        // ---------------------------------------------------------------
        public static void StartAmbient()
        {
            if (_src == null) Init();
            if (_amb.isPlaying) return;
            _amb.clip = Build("ambient", 4f, () => (t, u) =>
            {
                float tau = 2f * Mathf.PI;
                // Frecuencias elegidas para que den ciclos enteros en 4 s
                // y el bucle no haga "clic".
                float low = Mathf.Sin(tau * 55f * t) * 0.50f
                          + Mathf.Sin(tau * 110f * t) * 0.30f;
                float pad = Mathf.Sin(tau * 164.75f * t)
                          * (0.6f + 0.4f * Mathf.Sin(tau * 0.5f * t)) * 0.15f;
                float neon = Mathf.Sin(tau * 120f * t)
                           * (0.5f + 0.5f * Mathf.Sin(tau * 0.25f * t)) * 0.06f;
                return (low + pad + neon) * 0.5f;
            }, fadeTail: false);
            _amb.Play();
        }

        public static void StopAmbient()
        {
            if (_amb != null) _amb.Stop();
        }

        // ---------------------------------------------------------------
        // Compatibilidad: tono simple (por si otros scripts lo usan)
        // ---------------------------------------------------------------
        public static void PlayTone(float freq, float dur, float vol = 0.5f, bool slideDown = false, float slideTo = 0f)
        {
            if (_src == null) Init();
            string key = $"tone_{freq}_{dur}_{vol}_{slideDown}_{slideTo}";
            var clip = Build(key, dur, () =>
            {
                var o = new Osc();
                return (t, u) =>
                {
                    float f = slideDown ? Mathf.Lerp(freq, slideTo, u) : freq;
                    float p = o.Step(f);
                    float env = Mathf.Sin(Mathf.PI * u);
                    return (Saw(p) * 0.4f + Sq(p, 0.5f) * 0.3f) * env * vol;
                };
            });
            _src.PlayOneShot(clip);
        }

        // ---------------------------------------------------------------
        // Efectos del juego
        // ---------------------------------------------------------------

        /// <summary>Registro de datos: arpegio digital rapido, como un chip.</summary>
        public static void Pickup() => Play(Build("pickup", 0.16f, () =>
        {
            var o1 = new Osc(); var o2 = new Osc(); var cr = new Crush(2);
            float[] notes = { 988f, 1318f, 1976f };
            return (t, u) =>
            {
                int i = Mathf.Min(2, (int)(t / 0.05f));
                float lt = t - i * 0.05f;
                float f = notes[i];
                float env = 1f - (lt / 0.05f) * 0.6f;
                float s = Sq(o1.Step(f), 0.25f) * 0.5f + Saw(o2.Step(f * 1.01f)) * 0.25f;
                return cr.Do(s) * env * 0.4f;
            };
        }), 0.9f);

        /// <summary>Informacion: chirrido de datos de dos tonos.</summary>
        public static void Info() => Play(Build("info", 0.2f, () =>
        {
            var o1 = new Osc(); var o2 = new Osc(); var lp = new LP(0.4f);
            return (t, u) =>
            {
                float f = t < 0.08f ? 740f : 1108f;
                float s = Saw(o1.Step(f)) * 0.5f + Sq(o2.Step(f * 1.007f), 0.4f) * 0.35f;
                float env = Mathf.Min(1f, t / 0.004f) * Mathf.Pow(1f - u, 1.4f);
                return lp.Do(s) * env * 0.55f;
            };
        }), 0.9f);

        /// <summary>Habilidad: carga ascendente con temblor, tipo implante/overclock.</summary>
        public static void Ability() => Play(Build("ability", 0.35f, () =>
        {
            var o1 = new Osc(); var o2 = new Osc(); var lp = new LP(0.28f);
            return (t, u) =>
            {
                float f = Mathf.Lerp(140f, 880f, u * u);
                float s = (Saw(o1.Step(f)) + Saw(o2.Step(f * 1.012f))) * 0.5f;
                float trem = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                float env = Mathf.Sin(Mathf.PI * u);
                return lp.Do(s) * trem * env * 0.7f;
            };
        }));

        /// <summary>Bloqueo / acceso denegado: doble zumbido seco.</summary>
        public static void Block() => Play(Build("block", 0.18f, () =>
        {
            var o1 = new Osc(); var o2 = new Osc(); var cr = new Crush(3);
            return (t, u) =>
            {
                float s = Sq(o1.Step(150f)) * 0.5f + Sq(o2.Step(158f)) * 0.5f;
                float gate = Mathf.Repeat(t, 0.09f) < 0.06f ? 1f : 0f;
                float env = 1f - u * 0.4f;
                return cr.Do(s) * gate * env * 0.45f;
            };
        }));

        /// <summary>Dano: sierra distorsionada que cae, con rafaga de ruido.</summary>
        public static void Hurt() => Play(Build("hurt", 0.3f, () =>
        {
            var o = new Osc(); var cr = new Crush(2);
            return (t, u) =>
            {
                float f = Mathf.Lerp(320f, 45f, u);
                float s = Mathf.Clamp(Saw(o.Step(f)) * 3f, -1f, 1f) * 0.5f;
                s += Nz() * Mathf.Pow(1f - u, 3f) * 0.4f;
                return cr.Do(s) * Mathf.Pow(1f - u, 1.5f) * 0.6f;
            };
        }));

        /// <summary>Puerta: servo hidraulico que sube, siseo y golpe final.</summary>
        public static void Door() => Play(Build("door", 0.55f, () =>
        {
            var o = new Osc(); var lpS = new LP(0.15f); var lpN = new LP(0.08f);
            return (t, u) =>
            {
                float f = u < 0.8f ? Mathf.Lerp(80f, 240f, u / 0.8f) : 240f;
                float servo = lpS.Do(Saw(o.Step(f))) * 0.4f * (u < 0.8f ? 1f : 0f);
                float hiss = lpN.Do(Nz()) * 0.5f * Mathf.Sin(Mathf.PI * u);
                float clunk = 0f;
                if (u > 0.85f)
                    clunk = Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-(u - 0.85f) * 40f) * 0.8f;
                return (servo + hiss + clunk) * 0.8f;
            };
        }));

        /// <summary>Beep de interfaz: pitido digital corto.</summary>
        public static void Beep() => Play(Build("beep", 0.05f, () =>
        {
            var o = new Osc(); var cr = new Crush(2);
            return (t, u) => cr.Do(Sq(o.Step(1568f), 0.5f)) * (1f - u) * 0.25f;
        }));

        /// <summary>Curacion: dos notas ascendentes suaves con brillo.</summary>
        public static void Heart() => Play(Build("heart", 0.22f, () =>
        {
            var o1 = new Osc(); var o2 = new Osc();
            return (t, u) =>
            {
                float f = t < 0.1f ? 523f : 784f;
                float lt = t < 0.1f ? t : t - 0.1f;
                float env = Mathf.Pow(1f - lt / (t < 0.1f ? 0.1f : 0.12f), 1.5f);
                float s = Tri(o1.Step(f)) * 0.6f + Mathf.Sin(2f * Mathf.PI * f * 2f * t) * 0.2f;
                return s * env * 0.5f;
            };
        }));

        /// <summary>Victoria: acorde arpegiado de sierras desafinadas (La menor).</summary>
        public static void Win() => Play(Build("win", 1.0f, () =>
        {
            float[] notes = { 440f, 523.25f, 659.25f, 880f };
            float[] starts = { 0f, 0.1f, 0.2f, 0.3f };
            return (t, u) =>
            {
                float s = 0f;
                for (int i = 0; i < 4; i++)
                {
                    float lt = t - starts[i];
                    if (lt < 0f) continue;
                    float f = notes[i];
                    float env = Mathf.Min(1f, lt / 0.005f) * Mathf.Exp(-lt * (i == 3 ? 3f : 12f));
                    float a = Saw(Mathf.Repeat(f * t, 1f)) * 0.5f;
                    float b = Saw(Mathf.Repeat(f * 1.008f * t, 1f)) * 0.5f;
                    float c = Sq(Mathf.Repeat(f * 0.5f * t, 1f)) * 0.3f;
                    s += (a + b + c) * env;
                }
                return s * 0.22f;
            };
        }));

        /// <summary>Derrota: apagado del sistema, cae y se pixela cada vez mas.</summary>
        public static void Lose() => Play(Build("lose", 0.9f, () =>
        {
            var o = new Osc(); var lp = new LP(0.2f);
            Crush cr = new Crush(1);
            int lastK = 1;
            return (t, u) =>
            {
                int k = 1 + (int)(u * 8f);
                if (k != lastK) { cr = new Crush(k); lastK = k; }
                float f = Mathf.Lerp(300f, 35f, u);
                float s = lp.Do(cr.Do(Saw(o.Step(f))));
                float env = Mathf.Pow(1f - u, 1.2f);
                return s * env * 0.6f;
            };
        }));

        /// <summary>Disparo: laser/plasma, barrido agudo que cae y clic de ruido.</summary>
        public static void Shoot() => Play(Build("shoot", 0.12f, () =>
        {
            var o = new Osc(); var cr = new Crush(2);
            return (t, u) =>
            {
                float f = Mathf.Lerp(2400f, 180f, Mathf.Sqrt(u));
                float s = Sq(o.Step(f), 0.3f) * 0.6f + Nz() * 0.25f * Mathf.Pow(1f - u, 4f);
                float env = Mathf.Pow(1f - u, 2f);
                return cr.Do(s) * env * 0.4f;
            };
        }));

        /// <summary>Impacto en dron: zap metalico con modulacion en anillo.</summary>
        public static void DroneHit() => Play(Build("dronehit", 0.14f, () =>
        {
            var o = new Osc();
            return (t, u) =>
            {
                float f = Mathf.Lerp(520f, 180f, u);
                float ring = Sq(Mathf.Repeat(t * 95f, 1f));
                float s = Saw(o.Step(f)) * ring * 0.6f;
                s += Nz() * Mathf.Pow(1f - u, 6f) * 0.3f;
                return s * Mathf.Pow(1f - u, 2f) * 0.55f;
            };
        }));

        /// <summary>
        /// Paso: golpe sordo de bota sobre metal con un toque de ruido.
        /// Hay 3 variantes que se alternan para que no suene repetitivo.
        /// Se dispara por distancia recorrida, no por tiempo.
        /// </summary>
        public static void Footstep()
        {
            int v = _stepIdx++ % 3;
            float baseF = v == 0 ? 100f : (v == 1 ? 90f : 115f);
            Play(Build("step" + v, 0.09f, () =>
            {
                var o = new Osc(); var lp = new LP(0.25f);
                return (t, u) =>
                {
                    float f = Mathf.Lerp(baseF, 55f, u);
                    float thump = Mathf.Sin(2f * Mathf.PI * o.Step(f)) * 0.7f;
                    float click = lp.Do(Nz()) * Mathf.Pow(1f - u, 4f) * 0.5f;
                    return (thump + click) * Mathf.Pow(1f - u, 3f) * 0.4f;
                };
            }), 0.8f);
        }

        // ---------------------------------------------------------------
        // Motor de sintesis (helpers)
        // ---------------------------------------------------------------

        static void Play(AudioClip c, float vol = 1f)
        {
            if (_src == null) Init();
            _src.PlayOneShot(c, vol);
        }

        /// <summary>
        /// Genera el clip una sola vez. "make" crea el estado (osciladores,
        /// filtros) y devuelve la funcion (t, u) -> muestra, donde t son
        /// segundos y u va de 0 a 1.
        /// </summary>
        static AudioClip Build(string key, float dur, Func<Func<float, float, float>> make, bool fadeTail = true)
        {
            if (_cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var gen = make();
            int n = Mathf.Max(1, Mathf.CeilToInt(SR * dur));
            var data = new float[n];
            int tail = Mathf.Min(n, 130); // ~3 ms para evitar clics al final
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SR;
                float s = gen(t, (float)i / n);
                if (fadeTail && i > n - tail) s *= (float)(n - i) / tail;
                data[i] = Mathf.Clamp(s, -1f, 1f);
            }
            var clip = AudioClip.Create(key, n, 1, SR, false);
            clip.SetData(data, 0);
            _cache[key] = clip;
            return clip;
        }

        // Ruido determinista: no toca UnityEngine.Random.
        static float Nz()
        {
            _seed = _seed * 1664525u + 1013904223u;
            return ((_seed >> 8) / 8388608f) - 1f;
        }

        // Formas de onda a partir de una fase 0..1
        static float Saw(float p) => p * 2f - 1f;
        static float Sq(float p, float duty = 0.5f) => p < duty ? 1f : -1f;
        static float Tri(float p) => 4f * Mathf.Abs(p - 0.5f) - 1f;

        /// <summary>Oscilador con fase acumulada (permite barridos de frecuencia).</summary>
        sealed class Osc
        {
            double ph;
            public float Step(float f)
            {
                ph += f / SR;
                ph -= Math.Floor(ph);
                return (float)ph;
            }
        }

        /// <summary>Filtro paso bajo de un polo (a mas pequeno, mas sordo).</summary>
        sealed class LP
        {
            float y, a;
            public LP(float a) { this.a = a; }
            public float Do(float x) { y += a * (x - y); return y; }
        }

        /// <summary>Reduccion de muestreo (bitcrush): repite cada k muestras.</summary>
        sealed class Crush
        {
            int k, c; float held;
            public Crush(int k) { this.k = Mathf.Max(1, k); }
            public float Do(float x)
            {
                if (c++ % k == 0) held = x;
                return held;
            }
        }
    }
}