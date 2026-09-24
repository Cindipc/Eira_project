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
    }
}