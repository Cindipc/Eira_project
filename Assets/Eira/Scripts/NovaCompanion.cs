

using UnityEngine;

namespace EiraGame
{
    public class NovaCompanion : MonoBehaviour
    {
        [Header("Consejos")]
        public string[] idleTips =
        {
            "Los drones te detectan más lejos si corres. Agáchate (Ctrl) y usarás menos el radar.",
            "Si una máquina brilla en verde, actívala con F.",
            "Tu corazón es tu energía: el pulso con F lo consume, y si estás por debajo del 50% te hará daño.",
            "Los botiquines y cápsulas de energía te restauran. Busca sus luces.",
            "Mantén Shift para correr y Espacio para saltar. Presiona el ratón para girar la cámara."
        };

        [Header("Diálogos")]
        [TextArea(3, 6)]
        public string linesOnInfo;

        [TextArea(2, 5)]
        public string lineOnPuzzle;

        [TextArea(2, 5)]
        public string lineOnBoss;

        [TextArea(2, 5)]
        public string lineOnBossEnd;

        [TextArea(2, 5)]
        public string lineOnWin;

        [Header("Configuración")]
        public float sayDuration = 4f;

        [Header("Seguimiento")]
        [Tooltip("Distancia lateral respecto a Eira.")]
        [SerializeField] private float followDistance = 1.1f;

        [Tooltip("Distancia por detrás de Eira.")]
        [SerializeField] private float behindDistance = 1.5f;

        [Tooltip("Altura del objetivo sobre el suelo.")]
        [SerializeField] private float followHeight = 0f;

        [Tooltip("Suavizado de la posición (más alto = más pegada).")]
        [SerializeField] private float followSmooth = 6f;

        [Tooltip("Suavizado del giro.")]
        [SerializeField] private float rotationSmooth = 8f;

        [Tooltip("Si Nova se aleja más que esto, se teletransporta junto a Eira.")]
        [SerializeField] private float teleportDistance = 6f;

        [Tooltip("Velocidad máxima de Nova al recuperar distancia.")]
        [SerializeField] private float maxSpeed = 7.5f;

        [Tooltip("Radio del barrido para no atravesar paredes.")]
        [SerializeField] private float avoidRadius = 0.35f;

        [Tooltip("Distancia a la que mira al frente en vez de a Eira.")]
        [SerializeField] private float lookAheadDistance = 3.5f;

        private PlayerController player;
        private CharacterController playerController;
        private MissionDirector director;
        private int tipIndex;

        public bool tipsDone { get; private set; }

        private void Start()
        {
            GameManager gm = GameManager.Instance;

            if (gm != null)
                player = gm.Player;

            if (player != null)
                playerController = player.GetComponent<CharacterController>();

            director = FindObjectOfType<MissionDirector>();

            GameEvents.OnPickedInfo += HandleInfo;
            GameEvents.OnPuzzleSolved += HandlePuzzle;
            GameEvents.OnBossDefeated += HandleBossEnd;
            GameEvents.OnSubtitle += OnSubtitleHook;

            // Empieza pegada a Eira para que no atraviese el nivel al cargar.
            if (player != null)
                transform.position = ComputeSlotPosition() + Vector3.up * 0.05f;
        }

        private void OnDestroy()
        {
            GameEvents.OnPickedInfo -= HandleInfo;
            GameEvents.OnPuzzleSolved -= HandlePuzzle;
            GameEvents.OnBossDefeated -= HandleBossEnd;
            GameEvents.OnSubtitle -= OnSubtitleHook;
        }

        private void OnSubtitleHook(
            string message,
            float duration)
        {
            // Reservado para sincronizar diálogos de NOVA.
        }

        private void HandleInfo(string info)
        {
            GameManager gm =
                GameManager.Instance;

            if (gm == null)
                return;

            if (gm.InfoCollectedCount <
                EiraConst.InfoLogsNeeded)
                return;

            if (string.IsNullOrWhiteSpace(linesOnInfo))
            {
                linesOnInfo =
                    "Eso es todo. 3000... Los humanos llevan décadas desaparecidos. Vamos, la salida está por ahí.";
            }

            GameEvents.NovaSpeak(
                linesOnInfo,
                sayDuration + 1f
            );
        }

        private void HandlePuzzle()
        {
            string message =
                string.IsNullOrWhiteSpace(lineOnPuzzle)
                    ? "¡Perfecto, Eira! La puerta está abierta."
                    : lineOnPuzzle;

            GameEvents.NovaSpeak(
                message,
                sayDuration
            );
        }

        private void HandleBossEnd()
        {
            string message =
                string.IsNullOrWhiteSpace(lineOnBossEnd)
                    ? "¡Lo lograste! La salida está despejada. ¡Corre!"
                    : lineOnBossEnd;

            GameEvents.NovaSpeak(
                message,
                sayDuration
            );
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;

            if (gm == null)
                return;

            if (player == null)
            {
                player = gm.Player;

                if (player == null)
                    return;

                playerController = player.GetComponent<CharacterController>();
            }

            // ---------------------------------------------------------
            // POSICIÓN
            // ---------------------------------------------------------

            Vector3 target = ComputeSlotPosition();

            // Si Nova se ha quedado atrás (respawn, caída, atasco), salta
            // directamente: nada de arrastrarse por el mapa a 1 m/s.
            Vector3 flatDelta = target - transform.position;
            flatDelta.y = 0f;

            if (flatDelta.magnitude > teleportDistance)
            {
                transform.position = target + Vector3.up * 0.05f;
            }
            else
            {
                // Barrido contra paredes: si hay obstáculo entre Nova y su
                // sitio, se acerca lo máximo posible sin atravesarlo.
                target = SweepTowards(target);

                float smooth = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);

                Vector3 next = Vector3.Lerp(transform.position, target, smooth);

                // Tope de velocidad: al recuperar distancia en una carrera
                // no debe teletransportarse, pero tampoco quedarse atrás.
                Vector3 step = next - transform.position;

                if (step.magnitude > maxSpeed * Time.deltaTime)
                    next = transform.position + step.normalized * maxSpeed * Time.deltaTime;

                transform.position = next;
            }

            // ---------------------------------------------------------
            // SUELO: se pega al suelo para no quedar flotando
            // ---------------------------------------------------------

            transform.position = ClampToGround(transform.position);

            // ---------------------------------------------------------
            // ORIENTACIÓN
            // ---------------------------------------------------------

            Quaternion lookRotation = ComputeLookRotation();

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                lookRotation,
                1f - Mathf.Exp(-rotationSmooth * Time.deltaTime)
            );
        }

        /// <summary>
        /// Punto de formación detrás del hombro derecho de Eira.
        /// </summary>
        private Vector3 ComputeSlotPosition()
        {
            Vector3 position = player.transform.position;

            // Se usa el suelo bajo Eira como base, no su transform: así Nova
            // no sube con Eira cuando salta.
            Vector3 basePosition = ClampToGround(position);

            return basePosition
                - player.transform.forward * behindDistance
                + player.transform.right * followDistance
                + Vector3.up * followHeight;
        }

        private Vector3 SweepTowards(Vector3 target)
        {
            Vector3 delta = target - transform.position;

            if (delta.sqrMagnitude < 0.0001f)
                return target;

            Vector3 direction = delta.normalized;
            float distance = delta.magnitude;

            if (!Physics.SphereCast(
                    transform.position + Vector3.up * avoidRadius,
                    avoidRadius,
                    direction,
                    out RaycastHit hit,
                    distance,
                    ~0,
                    QueryTriggerInteraction.Ignore))
                return target;

            // Se detiene justo antes del obstáculo, con un pequeño margen.
            float safe = Mathf.Max(0f, hit.distance - 0.1f);

            Vector3 result = transform.position + direction * safe;

            return ClampToGround(result);
        }

        private Vector3 ClampToGround(Vector3 position)
        {
            if (Physics.Raycast(
                    position + Vector3.up * 1.5f,
                    Vector3.down,
                    out RaycastHit hit,
                    8f,
                    ~0,
                    QueryTriggerInteraction.Ignore) &&
                !hit.transform.IsChildOf(transform))
            {
                return new Vector3(position.x, hit.point.y, position.z);
            }

            return position;
        }

        private Quaternion ComputeLookRotation()
        {
            // ---- MODO GUÍA ----
            // Si hay un objetivo activo, Nova mira hacia el objetivo en vez
            // de mirar a Eira: así guía de verdad hacia dónde ir, y no se
            // limita a decir una frase.
            if (director != null && director.HasTarget)
            {
                Vector3 toTarget = director.TargetPosition - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude > 0.25f)
                    return Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            }

            // Cuando está lejos mira hacia donde va Eira; cuando está cerca,
            // mira a Eira. Evita el "tornillo" continuo de la cabeza.
            Vector3 flat = player.transform.position - transform.position;
            flat.y = 0f;

            if (flat.sqrMagnitude > lookAheadDistance * lookAheadDistance && playerController != null)
            {
                Vector3 travel = playerController.velocity;
                travel.y = 0f;

                if (travel.sqrMagnitude > 0.04f)
                    return Quaternion.LookRotation(travel.normalized, Vector3.up);
            }

            Vector3 lookDirection =
                player.transform.position +
                Vector3.up * 1.2f -
                transform.position;

            if (lookDirection.sqrMagnitude < 0.0001f)
                return transform.rotation;

            return Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        public void SayNextTip()
        {
            if (tipsDone)
                return;

            if (idleTips == null ||
                idleTips.Length == 0)
                return;

            AudioFX.Beep();

            string tip =
                idleTips[
                    tipIndex %
                    idleTips.Length
                ];

            tipIndex++;

            GameEvents.NovaSpeak(
                tip,
                sayDuration
            );

            if (tipIndex >= idleTips.Length)
                tipsDone = true;
        }

        public void SayBoss()
        {
            string message =
                string.IsNullOrWhiteSpace(lineOnBoss)
                    ? "Eira, cuidado. El guardián está activo."
                    : lineOnBoss;

            GameEvents.NovaSpeak(
                message,
                sayDuration
            );
        }

        public void SayWin()
        {
            string message =
                string.IsNullOrWhiteSpace(lineOnWin)
                    ? "Lo conseguimos. Salgamos de aquí."
                    : lineOnWin;

            GameEvents.NovaSpeak(
                message,
                sayDuration
            );
        }
    }
}