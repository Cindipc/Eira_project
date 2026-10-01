using UnityEngine;

namespace EiraGame
{
    [RequireComponent(typeof(Camera))]
    public sealed class ProfessionalThirdPersonCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private float targetHeight = 1.45f;
        [SerializeField] private float shoulderOffset = 0.6f;

        [Header("Distance")]
        [SerializeField] private float distance = 3.6f;
        [SerializeField] private float minimumDistance = 0.65f;

        [Header("Follow")]
        [SerializeField] private float positionSmooth = 14f;
        [SerializeField] private float rotationSmooth = 18f;

        [Header("Look")]
        [SerializeField] private float sensitivity = 0.10f;
        [SerializeField] private float pitch = 8f;
        [SerializeField] private float minimumPitch = -18f;
        [SerializeField] private float maximumPitch = 55f;

        [Header("Behind Eira")]
        [SerializeField] private bool returnBehindWhenMoving = true;
        [SerializeField] private float returnBehindSpeed = 2.2f;
        [SerializeField] private float behindTolerance = 70f;
        [SerializeField] private float lookIdleBeforeReturn = 0.6f;

        /// <summary>
        /// Grados por segundo como máximo que la cámara puede dar al volver
        /// detrás de Eira. Es lo que quita el latigazo: sin este tope, el
        /// LerpAngle salta de golpe al ángulo de la marcha y la pantalla
        /// rota entera en un frame.
        /// </summary>
        [SerializeField] private float maxBehindStepDegrees = 90f;

        [Header("Field of View")]
        [SerializeField] private float baseFov = 60f;
        [SerializeField] private float sprintFov = 68f;
        [SerializeField] private float fovSmooth = 6f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.18f;
        [SerializeField] private float collisionPadding = 0.08f;
        [SerializeField] private float collisionSmooth = 30f;
        [SerializeField] private LayerMask collisionMask;

        private Camera cam;
        private float yaw;
        private Vector3 currentPosition;
        private bool initialized;
        private float lookIdleTimer;

        /// <summary>
        /// Orientación horizontal actual de la cámara. Es la fuente de verdad
        /// del movimiento relativo a cámara, por eso se publica en GameManager.
        /// </summary>
        public float Yaw => yaw;

        private void Awake()
        {
            cam = GetComponent<Camera>();

            FindTarget();

            if (collisionMask.value == 0)
                collisionMask = Physics.DefaultRaycastLayers;

            if (target == null)
                return;

            yaw = target.eulerAngles.y;

            if (cam != null)
            {
                baseFov = cam.fieldOfView;
                cam.fieldOfView = baseFov;
            }

            SnapToTarget();

            PublishYaw();

            initialized = true;
        }

        private void Start()
        {
            if (target == null)
                FindTarget();

            if (target == null)
                return;

            if (!initialized)
            {
                yaw = target.eulerAngles.y;
                SnapToTarget();
                PublishYaw();
                initialized = true;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                FindTarget();

                if (target == null)
                    return;

                yaw = target.eulerAngles.y;
                SnapToTarget();
                PublishYaw();
                initialized = true;
            }

            ReadLookInput();

            if (returnBehindWhenMoving)
                ReturnBehindEira();

            UpdateFieldOfView();
            UpdatePosition();
            UpdateRotation();

            PublishYaw();
        }

        private void PublishYaw()
        {
            if (GameManager.Instance == null)
                return;

            GameManager.Instance.CameraYaw = yaw;
        }

        private void FindTarget()
        {
            PlayerController player =
                FindObjectOfType<PlayerController>();

            if (player != null)
                target = player.transform;
        }

        private void ReadLookInput()
        {
            Vector2 look =
                EiraInput.LookDelta(sensitivity);

            if (look.sqrMagnitude > 0.0001f)
            {
                lookIdleTimer = 0f;

                yaw += look.x;
                pitch -= look.y;
            }
            else
            {
                lookIdleTimer += Time.deltaTime;
            }

            pitch = Mathf.Clamp(
                pitch,
                minimumPitch,
                maximumPitch
            );
        }

        private void UpdateFieldOfView()
        {
            if (cam == null)
                return;

            float targetFov = baseFov;

            PlayerController player = target != null
                ? target.GetComponent<PlayerController>()
                : null;

            if (player != null && player.IsSprinting)
                targetFov = sprintFov;

            cam.fieldOfView = Mathf.Lerp(
                cam.fieldOfView,
                targetFov,
                1f - Mathf.Exp(-fovSmooth * Time.deltaTime)
            );
        }

        private void ReturnBehindEira()
        {
            // Solo se recoloca la cámara si el jugador lleva un rato
            // sin tocar el ratón y se está moviendo hacia delante.
            if (lookIdleTimer < lookIdleBeforeReturn)
                return;

            CharacterController controller =
                target.GetComponent<CharacterController>();

            if (controller == null)
                return;

            Vector3 velocity = controller.velocity;
            velocity.y = 0f;

            if (velocity.sqrMagnitude < 1.2f)
                return;

            float movementYaw = Mathf.Atan2(velocity.x, velocity.z) * Mathf.Rad2Deg;

            // Solo si la dirección de avance es parecida a la de la cámara:
            // así el jugador puede mirar a los lados sin que le robe el control.
            float forwardDifference = Mathf.Abs(
                Mathf.DeltaAngle(yaw, movementYaw)
            );

            if (forwardDifference > behindTolerance)
                return;

            // Que la cámara no dé un tirón instantáneo: se queda quieta
            // mientras gira hacia la espalda de Eira.
            float behindAmount =
                1f -
                Mathf.Exp(
                    -returnBehindSpeed *
                    Time.deltaTime
                );

            float targetYaw =
                Mathf.LerpAngle(
                    yaw,
                    movementYaw,
                    behindAmount
                );

            // Límite de velocidad: evita el salto de 90° cuando el jugador
            // empieza a andar mientras la cámara estaba girada hacia otro lado.
            float maxStep =
                maxBehindStepDegrees *
                Time.deltaTime;

            float step =
                Mathf.DeltaAngle(
                    yaw,
                    targetYaw
                );

            step = Mathf.Clamp(
                step,
                -maxStep,
                maxStep
            );

            yaw = yaw + step;
            return;

            float amount =
                1f -
                Mathf.Exp(
                    -returnBehindSpeed *
                    Time.deltaTime
                );

            yaw = Mathf.LerpAngle(yaw, movementYaw, amount);
        }

        private void UpdatePosition()
        {
            Vector3 pivot = GetPivot();

            Quaternion orbit =
                Quaternion.Euler(
                    pitch,
                    yaw,
                    0f
                );

            Vector3 direction =
                -(orbit * Vector3.forward);

            Vector3 desiredPosition =
                pivot +
                direction * distance;

            desiredPosition =
                ResolveCollision(
                    pivot,
                    desiredPosition
                );

            if (!initialized)
            {
                currentPosition =
                    desiredPosition;
            }
            else
            {
                // Suavizado asimétrico: al acercarse a un muro la cámara tiene
                // que ceder rápido o Eira se mete dentro de la geometría; al
                // alejarse, en cambio, devolverla a la misma velocidad produce
                // un latigazo hacia atrás cada vez que pasa un pilar.
                // Acercarse usa collisionSmooth, alejarse positionSmooth.
                float desiredDistance =
                    Vector3.Distance(
                        GetPivot(),
                        desiredPosition
                    );

                float currentDistance =
                    Vector3.Distance(
                        GetPivot(),
                        currentPosition
                    );

                float rate =
                    desiredDistance < currentDistance
                        ? collisionSmooth
                        : positionSmooth;

                float smooth =
                    1f -
                    Mathf.Exp(
                        -rate *
                        Time.deltaTime
                    );

                currentPosition =
                    Vector3.Lerp(
                        currentPosition,
                        desiredPosition,
                        smooth
                    );
            }

            transform.position =
                currentPosition;
        }

        private void UpdateRotation()
        {
            Vector3 pivot =
                GetPivot();

            Vector3 direction =
                pivot -
                transform.position;

            if (direction.sqrMagnitude < 0.001f)
                return;

            Quaternion desiredRotation =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up
                );

            float smooth =
                1f -
                Mathf.Exp(
                    -rotationSmooth *
                    Time.deltaTime
                );

            transform.rotation =
                Quaternion.Slerp(
                    transform.rotation,
                    desiredRotation,
                    smooth
                );
        }

        private Vector3 GetPivot()
        {
            Vector3 pivot = target.position;

            // Pivote a la altura del pecho y desplazado al hombro derecho:
            // da una vista más natural que una cámara centrada en la espalda.
            Quaternion orbit = Quaternion.Euler(0f, yaw, 0f);

            pivot += Vector3.up * targetHeight;
            pivot += orbit * Vector3.right * shoulderOffset;

            return pivot;
        }

        private Vector3 ResolveCollision(
            Vector3 pivot,
            Vector3 desiredPosition
        )
        {
            Vector3 direction =
                desiredPosition -
                pivot;

            float length =
                direction.magnitude;

            if (length <= 0.01f)
                return desiredPosition;

            direction /= length;

            float safeDistance = length;

            // Se prueban varios impactos, no solo el primero. Con un barrido
            // único el SphereCast puede devolver el propio collider de Eira
            // (el pivote está dentro de su cápsula) o un trigger de un
            // objeto cercano, y entonces la cámara se pegaba al cuerpo
            // o desaparecía dentro del muro. Con la lista completa se
            // descartan y se gana el muro real, que es el que importa.
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
                RaycastHit hit = hits[i];

                // Nada de lo que pertenece a Eira debe empujar la cámara
                if (target != null && hit.transform.IsChildOf(target))
                    continue;

                float candidate =
                    hit.distance -
                    collisionPadding;

                if (candidate < safeDistance)
                    safeDistance = candidate;
            }

            safeDistance =
                Mathf.Clamp(
                    safeDistance,
                    minimumDistance,
                    length
                );

            return pivot +
                   direction *
                   safeDistance;
        }

        private void SnapToTarget()
        {
            if (target == null)
                return;

            Vector3 pivot =
                GetPivot();

            Quaternion orbit =
                Quaternion.Euler(
                    pitch,
                    yaw,
                    0f
                );

            Vector3 direction =
                -(orbit * Vector3.forward);

            currentPosition =
                ResolveCollision(
                    pivot,
                    pivot + direction * distance
                );

            transform.position =
                currentPosition;

            Vector3 lookDirection =
                pivot -
                transform.position;

            if (lookDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.LookRotation(
                        lookDirection,
                        Vector3.up
                    );
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target == null)
                return;

            yaw = target.eulerAngles.y;

            SnapToTarget();
            PublishYaw();

            initialized = true;
        }

        /// <summary>
        /// Recoloca la cámara detrás de Eira. Se usa al reaparecer
        /// para no dejar la cámara dentro de la geometría.
        /// </summary>
        public void SnapBehind()
        {
            if (target == null)
                return;

            yaw = target.eulerAngles.y;

            pitch = Mathf.Clamp(pitch, minimumPitch, maximumPitch);

            SnapToTarget();
            PublishYaw();

            initialized = true;
        }

        public Transform GetTarget()
        {
            return target;
        }
    }
}