using UnityEngine;

namespace EiraGame
{
    // Cámara en tercera persona: va detrás de la espalda de Eira y orbita con el mouse.
    public class FollowCamera : MonoBehaviour
    {
        public float distance = 3.4f;
        public float height = 1.15f;
        public float sensitivity = 0.08f;
        public float yaw = 0f;
        public float pitch = 15f;
        public float minPitch = -25f;
        public float maxPitch = 60f;
        public Camera cam;

        void Start()
        {
            if (cam == null) cam = GetComponent<Camera>();
        }

        void LateUpdate()
        {
            var target = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (target == null) return;

            var look = EiraInput.LookDelta(sensitivity);
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, minPitch, maxPitch);

            if (GameManager.Instance != null)
                GameManager.Instance.CameraYaw = yaw;

            var pivot = target.transform.position + Vector3.up * height;
            // La cámara se coloca DETRÁS de Eira: el offset usa -forward, así miramos su espalda.
            var desired = pivot + Quaternion.Euler(pitch, yaw, 0f) * (Vector3.back * distance);

            // evitar atravesar paredes
            if (Physics.Raycast(pivot, (desired - pivot).normalized, out var hit, distance))
            {
                if (!hit.collider.isTrigger)
                    desired = hit.point + hit.normal * 0.25f;
            }

            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-12f * Time.deltaTime));
            transform.LookAt(pivot);

            if (cam != null)
            {
                var pc = target.GetComponent<PlayerController>();
                float targetFov = pc != null && EiraInput.Sprint() ? 74f : 62f;
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 6f * Time.deltaTime);
            }
        }
    }
}