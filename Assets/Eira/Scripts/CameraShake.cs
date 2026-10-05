using UnityEngine;

namespace EiraGame
{
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] private float defaultDuration = 0.3f;
        [SerializeField] private float defaultIntensity = 0.3f;

        private Vector3 originalPos;
        private float shakeTime;
        private float shakeIntensity;
        private float shakeDuration;
        private bool isShaking;

        private void Awake()
        {
            Instance = this;
        }

        private void LateUpdate()
        {
            if (!isShaking) return;

            if (shakeTime > 0)
            {
                Vector3 offset = Random.insideUnitSphere * shakeIntensity;
                offset.z = 0f;
                transform.localPosition = originalPos + offset;

                shakeTime -= Time.unscaledDeltaTime;
            }
            else
            {
                transform.localPosition = originalPos;
                isShaking = false;
            }
        }

        public void Shake(float intensity, float duration)
        {
            originalPos = transform.localPosition;
            shakeIntensity = intensity;
            shakeDuration = duration;
            shakeTime = duration;
            isShaking = true;
        }

        public void ShakeDefault()
        {
            Shake(defaultIntensity, defaultDuration);
        }

        public void StopShake()
        {
            isShaking = false;
            transform.localPosition = originalPos;
        }
    }
}