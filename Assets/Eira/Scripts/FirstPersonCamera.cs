using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// UNICA fuente de la posicion y rotacion de la camara.
    ///
    /// Todo se calcula en LateUpdate a partir del pivote de Eira:
    /// pivote = pies de Eira + targetHeight (hombro) y la camara se coloca
    /// "distancia" metros DETRAS de ese pivote, con "pitch" grados de
    /// inclinacion hacia abajo y mirando de vuelta al pivote.
    ///
    /// No hay ningun offset de ojos: la camara nunca se mete dentro de Eira.
    /// Con estos valores por defecto la camara queda a ~2.0 m del suelo,
    /// casi horizontal, y se ven Eira y Nova enteros.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FirstPersonCamera : MonoBehaviour
    {
        [Header("Objetivo (Eira, nunca Nova)")]
        [Tooltip("Transform de Eira (PlayerController). Se busca solo si se deja vacio.")]
        [SerializeField] private Transform target;

        [Header("Encuadre (lo unico que manda)")]
        [Tooltip("Altura del pivote sobre los pies de Eira. Torso/cabeza = 1.4 - 1.8 m.")]
        [SerializeField] private float targetHeight = 1.5f;

        [Tooltip("Distancia de la camara hacia atras desde el pivote.")]
        [SerializeField] private float distance = 3.8f;

        [Tooltip("Grados de inclinacion HACIA ABAJO. Recomendado 5 - 15. En el Transform se vera como rotation.x en negativo (8 aqui = -8 en el Transform).")]
        [SerializeField] private float pitch = 8f;

        [Tooltip("Desplazamiento lateral al hombro derecho del pivote.")]
        [SerializeField] private float shoulderOffset = 0.5f;

        [Tooltip("Suavizado de la posicion (mas alto = mas rigido).")]
        [SerializeField] private float positionSmooth = 16f;

        [Tooltip("Suavizado de la rotacion (mas alto = mas rigido).")]
        [SerializeField] private float rotationSmooth = 20f;

        [Header("Raton")]
        [SerializeField] private float mouseSensitivity = 0.12f;
        [SerializeField] private float minPitch = -15f;
        [SerializeField] private float maxPitch = 55f;

        [Header("Campo de vision")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float sprintFov = 68f;
        [SerializeField] private float fovSmooth = 8f;

        [Header("Balanceo al andar")]
        [SerializeField] private bool enableBobbing = false;
        [SerializeField] private float walkBobAmount = 0.03f;
        [SerializeField] private float walkBobSpeed = 8f;
        [SerializeField] private float runBobAmount = 0.05f;
        [SerializeField] private float runBobSpeed = 12f;

        [Header("Colision (que la camara no atraviese muros)")]
        [SerializeField] private float collisionRadius = 0.18f;
        [SerializeField] private float collisionPadding = 0.08f;

        [Tooltip("Distancia minima al pivote. Evita que la camara se pegue al cuerpo de Eira.")]
        [SerializeField] private float minDistance = 1.2f;
        [SerializeField] private LayerMask collisionMask;

        [Header("Primera persona (tecla T o V)")]
        [Tooltip("false = primera persona (camara en el pivote). true = detras de Eira.")]
        [SerializeField] private bool thirdPersonMode = true;

        private Camera cam;
        private PlayerController player;
        private float yaw;
        private float currentPitch;
        private float currentFov;
        private Vector3 currentPosition;
        private float bobTimer;
        private float bobOffset;
        private bool initialized;

        public float Yaw
        {
            get => yaw;
            set => yaw = value;
        }

        public float Pitch
        {
            get => currentPitch;
            set => currentPitch = value;
        }

        /// <summary>
        /// Transform al que apunta la camara. Debe ser Eira (la de la espada),
        /// nunca NOVA.
        /// </summary>
        public Transform Target
        {
            get => target;
            set
            {
                target = value;
                player = value != null ? value.GetComponent<PlayerController>() : null;
                initialized = false;
            }
        }

        /// <summary>
        /// Asegura que la camara principal tiene este componente y que esta
        /// desacoplada de cualquier padre.
        /// </summary>
        public static FirstPersonCamera EnsureMainCamera()
        {
            Camera mainCam = Camera.main;

            if (mainCam == null)
            {
                Camera[] cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude);

                foreach (Camera c in cams)
                {
                    if (!c.orthographic && c.tag == "MainCamera")
                    {
                        mainCam = c;
                        break;
                    }
                }
            }

            if (mainCam == null)
            {
                GameObject go = new GameObject("MainCamera");
                go.tag = "MainCamera";
                mainCam = go.AddComponent<Camera>();
                mainCam.fieldOfView = 60f;
                mainCam.nearClipPlane = 0.01f;
                mainCam.farClipPlane = 1000f;
                mainCam.clearFlags = CameraClearFlags.Skybox;

                if (FindAnyObjectByType<AudioListener>() == null)
                    mainCam.gameObject.AddComponent<AudioListener>();
            }

            FirstPersonCamera fpCam = mainCam.GetComponent<FirstPersonCamera>();

            if (fpCam == null)
                fpCam = mainCam.gameObject.AddComponent<FirstPersonCamera>();

            if (mainCam.transform.parent != null)
                mainCam.transform.SetParent(null);

            return fpCam;
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            currentFov = baseFov;

            if (cam != null)
                cam.fieldOfView = baseFov;

            if (collisionMask.value == 0)
                collisionMask = Physics.DefaultRaycastLayers;

            if (transform.parent != null)
                transform.SetParent(null);

            currentPitch = pitch;
            FindPlayer();

            if (target != null)
            {
                yaw = target.eulerAngles.y;
                SnapToTarget();
                initialized = true;
            }
            else
            {
                Debug.LogError("[FirstPersonCamera] No se encontro PlayerController en Awake. Reintenta en Start/LateUpdate.");
            }
        }

        private void Start()
        {
            if (transform.parent != null)
                transform.SetParent(null);

            if (target == null)
                FindPlayer();

            if (target != null)
            {
                yaw = target.eulerAngles.y;
                SnapToTarget();
                initialized = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void FindPlayer()
        {
            if (GameManager.Instance != null && GameManager.Instance.Player != null)
            {
                PlayerController gmPlayer = GameManager.Instance.Player;

                if (!IsNova(gmPlayer.name))
                {
                    target = gmPlayer.transform;
                    player = gmPlayer;
                    return;
                }
            }

            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);

            foreach (PlayerController pc in allPlayers)
            {
                if (pc != null && !IsNova(pc.name))
                {
                    target = pc.transform;
                    player = pc;
                    return;
                }
            }

            PlayerController legacy = FindAnyObjectByType<PlayerController>();

            if (legacy != null && !IsNova(legacy.name))
            {
                target = legacy.transform;
                player = legacy;
                return;
            }
        }

        private static bool IsNova(string objectName)
        {
            return objectName.IndexOf("NOVA", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LateUpdate()
        {
            if (transform.parent != null)
                transform.SetParent(null);

            if (target == null || IsNova(target.name))
            {
                target = null;
                player = null;
                FindPlayer();

                if (target == null)
                    return;

                initialized = false;
            }

            if (player == null)
                player = target.GetComponent<PlayerController>();

            HandleMouseLook();
            UpdateFOV();
            UpdatePosition();
            UpdateRotation();

            if (GameManager.Instance != null)
                GameManager.Instance.CameraYaw = yaw;
        }

        private void HandleMouseLook()
        {
            Vector2 look = EiraInput.LookDelta(mouseSensitivity);

            if (look.sqrMagnitude > 0.0001f)
            {
                yaw += look.x;
                currentPitch -= look.y;
            }

            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        private void UpdateFOV()
        {
            if (cam == null || player == null)
                return;

            float targetFov = player.IsSprinting ? sprintFov : baseFov;
            currentFov = Mathf.Lerp(currentFov, targetFov, 1f - Mathf.Exp(-fovSmooth * Time.deltaTime));
            cam.fieldOfView = currentFov;
        }

        public void ToggleCameraMode()
        {
            thirdPersonMode = !thirdPersonMode;
        }

        public void ToggleThirdPerson()
        {
            thirdPersonMode = !thirdPersonMode;
        }

        public void SetThirdPersonMode(bool enabled)
        {
            thirdPersonMode = enabled;
        }

        public void SetThirdPerson(bool enabled)
        {
            thirdPersonMode = enabled;
        }

        public bool IsThirdPersonMode()
        {
            return thirdPersonMode;
        }

        // =========================================================
        // POSICION
        // =========================================================

        private void UpdatePosition()
        {
            if (target == null)
                return;

            Vector3 pivot = GetPivot();
            Vector3 desired = ResolveCollision(pivot, GetOrbitPosition(pivot));

            UpdateBobbing();

            if (!initialized)
            {
                currentPosition = desired;
                initialized = true;
            }
            else
            {
                float smooth = 1f - Mathf.Exp(-Mathf.Max(0.01f, positionSmooth) * Time.deltaTime);
                currentPosition = Vector3.Lerp(currentPosition, desired, smooth);
            }

            transform.position = currentPosition + Vector3.up * bobOffset;
        }

        /// <summary>
        /// Punto de interes: a la altura del torso y al hombro derecho.
        /// </summary>
        private Vector3 GetPivot()
        {
            Vector3 pivot = target.position + Vector3.up * targetHeight;
            pivot += Quaternion.Euler(0f, yaw, 0f) * Vector3.right * shoulderOffset;
            return pivot;
        }

        /// <summary>
        /// Posicion sin colision: el pivote menos "distance" hacia atras,
        /// segun el yaw y el pitch actuales.
        /// </summary>
        private Vector3 GetOrbitPosition(Vector3 pivot)
        {
            if (!thirdPersonMode)
                return pivot;

            Quaternion orbit = Quaternion.Euler(currentPitch, yaw, 0f);
            return pivot - orbit * Vector3.forward * Mathf.Max(0f, distance);
        }

        private void UpdateBobbing()
        {
            bobOffset = 0f;

            if (!enableBobbing || player == null)
                return;

            CharacterController cc = player.GetComponent<CharacterController>();

            if (cc == null)
                return;

            Vector3 velocity = cc.velocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;

            if (speed <= 0.1f || !player.IsGrounded)
            {
                bobTimer = 0f;
                return;
            }

            bool sprinting = player.IsSprinting && !player.IsCrouching;
            float amount = sprinting ? runBobAmount : walkBobAmount;
            float bobSpeed = sprinting ? runBobSpeed : walkBobSpeed;

            bobTimer += Time.deltaTime * bobSpeed * (speed / (sprinting ? player.runSpeed : player.walkSpeed));
            bobOffset = Mathf.Sin(bobTimer) * amount;
        }

        private void UpdateRotation()
        {
            if (target == null)
                return;

            Quaternion desired;

            if (thirdPersonMode)
            {
                Vector3 lookDirection = GetPivot() - transform.position;

                desired = lookDirection.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                    : Quaternion.Euler(-currentPitch, yaw, 0f);
            }
            else
            {
                desired = Quaternion.Euler(-currentPitch, yaw, 0f);
            }

            float smooth = 1f - Mathf.Exp(-Mathf.Max(0.01f, rotationSmooth) * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired, smooth);
        }

        /// <summary>
        /// Recorta la distancia si hay un muro entre el pivote y la camara.
        ///
        /// Importante: los impactos a distancia 0 o menor que el padding se
        /// ignoran. El pivote esta a la altura del torso, pero el barrido
        /// empieza dentro de los colliders que ya tocan a Eira (suelo, capsules
        /// de Nova). Antes esos impactos contaban y el clamp de abajo dejaba
        /// la camara pegada al suelo: por eso cambiar el offset no hacia nada.
        /// </summary>
        private Vector3 ResolveCollision(Vector3 pivot, Vector3 desiredPosition)
        {
            Vector3 delta = desiredPosition - pivot;
            float length = delta.magnitude;

            if (length <= 0.01f)
                return desiredPosition;

            Vector3 direction = delta / length;
            float safeDistance = length;

            RaycastHit[] hits = Physics.SphereCastAll(
                pivot,
                collisionRadius,
                direction,
                length,
                collisionMask,
                QueryTriggerInteraction.Ignore
            );

            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;

                if (hitTransform == null)
                    continue;

                if (target != null && hitTransform.IsChildOf(target))
                    continue;

                if (IsNova(hitTransform.name))
                    continue;

                // Ya estamos dentro de este collider: no cuenta como muro.
                if (hits[i].distance <= collisionPadding)
                    continue;

                float candidate = hits[i].distance - collisionPadding;

                if (candidate < safeDistance)
                    safeDistance = candidate;
            }

            safeDistance = Mathf.Clamp(safeDistance, Mathf.Min(minDistance, length), length);
            return pivot + direction * safeDistance;
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            currentPitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Vector3 pivot = GetPivot();
            currentPosition = ResolveCollision(pivot, GetOrbitPosition(pivot));
            transform.position = currentPosition;

            Vector3 lookDirection = pivot - transform.position;

            if (lookDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        /// <summary>
        /// Recoloca la camara detras de Eira con el encuadre guardado.
        /// Se usa al reaparecer.
        /// </summary>
        public void SnapBehind()
        {
            if (target == null)
                return;

            yaw = target.eulerAngles.y;
            SnapToTarget();
            initialized = true;
        }

        // =========================================================
        // SACUDIDA
        // =========================================================

        public void AddShake(Vector3 shakeOffset, float duration = 0.2f)
        {
            StartCoroutine(ShakeRoutine(shakeOffset, duration));
        }

        private System.Collections.IEnumerator ShakeRoutine(Vector3 offset, float duration)
        {
            float elapsed = 0f;
            Vector3 originalPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float intensity = 1f - elapsed / duration;

                transform.position = originalPos + offset * intensity * Random.insideUnitSphere.magnitude;
                yield return null;
            }

            transform.position = originalPos;
        }

        private void OnDrawGizmosSelected()
        {
            if (target == null)
                return;

            Vector3 pivot = target.position + Vector3.up * targetHeight;
            Vector3 camera = pivot - Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward * distance;

            Gizmos.color = new Color(0f, 1f, 1f, 0.6f);
            Gizmos.DrawWireSphere(pivot, 0.12f);
            Gizmos.DrawWireCube(camera, new Vector3(0.3f, 0.2f, 0.45f));
            Gizmos.DrawLine(pivot, camera);
        }
    }
}