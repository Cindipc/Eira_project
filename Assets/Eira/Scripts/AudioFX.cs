using UnityEngine;

namespace EiraGame
{
    // Sonidos sintetizados proceduralmente (sin necesidad de archivos de audio).
    public static class AudioFX
    {
        static AudioSource _src;

        public static void Init()
        {
            if (_src != null) return;
            var go = new GameObject("AudioSFX");
            Object.DontDestroyOnLoad(go);
            _src = go.AddComponent<AudioSource>();
            _src.spatialBlend = 0f;
            _src.playOnAwake = false;
            _src.volume = 0.7f;
        }

        public static void PlayTone(float freq, float dur, float vol = 0.5f, bool slideDown = false, float slideTo = 0f)
        {
            if (_src == null) Init();
            int sr = 44100;
            int n = Mathf.Max(1, Mathf.CeilToInt(sr * dur));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float f = slideDown ? Mathf.Lerp(freq, slideTo, t / dur) : freq;
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / dur)); // ataque/decaimiento
                data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * env * vol;
            }
            var clip = AudioClip.Create("tone", n, 1, sr, false);
            clip.SetData(data, 0);
            _src.PlayOneShot(clip);
        }

        public static void Pickup() => PlayTone(880f, 0.09f, 0.5f, true, 1320f);
        public static void Info() => PlayTone(660f, 0.12f, 0.45f, true, 990f);
        public static void Ability() => PlayTone(220f, 0.25f, 0.5f, true, 110f);
        public static void Hurt() => PlayTone(160f, 0.22f, 0.55f, true, 60f);
        public static void Door() => PlayTone(110f, 0.35f, 0.5f, true, 60f);
        public static void Beep() => PlayTone(1046f, 0.05f, 0.25f);
        public static void Heart() => PlayTone(420f, 0.12f, 0.5f, true, 300f);
        public static void Win() { PlayTone(523f, 0.15f, 0.5f); PlayTone(659f, 0.15f, 0.5f); PlayTone(784f, 0.3f, 0.5f); }
        public static void Lose() => PlayTone(300f, 0.6f, 0.5f, true, 80f);

        /// <summary>Disparo: bajón corto y seco, con un poco de ruido encima.</summary>
        public static void Shoot()
        {
            if (_src == null) Init();
            const int sr = 44100;
            float dur = 0.09f;
            int n = Mathf.Max(1, Mathf.CeilToInt(sr * dur));
            var data = new float[n];
            uint seed = 90210u;
            float filtered = 0f;
            for (int i = 0; i < n; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 8) / 8388608f) - 1f;
                filtered = Mathf.Lerp(filtered, white, 0.6f);
                float t = (float)i / sr;
                // Tono que cae de 1400 a 300 Hz + un pellizco de ruido.
                float f = Mathf.Lerp(1400f, 300f, Mathf.Clamp01(t / dur));
                float env = Mathf.Pow(1f - t / dur, 2.2f);
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) * 0.7f + filtered * 0.3f) * env * 0.3f;
            }
            var clip = AudioClip.Create("shoot", n, 1, sr, false);
            clip.SetData(data, 0);
            _src.PlayOneShot(clip);
        }

        /// <summary>Impacto en dron: chasquido metálico con caída de tono.</summary>
        public static void DroneHit() => PlayTone(520f, 0.1f, 0.4f, true, 180f);

        /// <summary>
        /// Paso: ruido filtrado muy corto, no un tono. Suena a zapata
        /// sobre suelo metálico y se dispara por distancia recorrida,
        /// no por tiempo, para que el ritmo siga a la velocidad real.
        /// </summary>
        public static void Footstep()
        {
            if (_src == null) Init();
            const int sr = 44100;
            float dur = 0.07f;
            int n = Mathf.Max(1, Mathf.CeilToInt(sr * dur));
            var data = new float[n];
            // Ruido determinista: sin Random.value para no tocar el estado
            // global que usan otros sistemas.
            uint seed = 22222u;
            float last = 0f;
            for (int i = 0; i < n; i++)
            {
                seed = seed * 1664525u + 1013904223u;
                float white = ((seed >> 8) / 8388608f) - 1f;
                // Filtro paso bajo de un polo: quita el siseo y deja el golpe.
                last = Mathf.Lerp(last, white, 0.35f);
                float t = (float)i / n;
                float env = Mathf.Pow(1f - t, 3f);
                data[i] = last * env * 0.22f;
            }
            var clip = AudioClip.Create("step", n, 1, sr, false);
            clip.SetData(data, 0);
            _src.PlayOneShot(clip);
        }
    }
}