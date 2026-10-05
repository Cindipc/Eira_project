

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
        [Tooltip("Distancia por detrás de Eira (metros).")]
        [SerializeField] private float behindDistance = 2f;

        [Tooltip("Desplazamiento lateral a la derecha de Eira (metros).")]
        [SerializeField] private float sideOffset = 0.8f;

        [Tooltip("Altura del objetivo sobre el suelo.")]
        [SerializeField] private float followHeight = 0f;

        [Tooltip("Suavizado de la posición (más alto = más pegada).")]
        [SerializeField] private float followSmooth = 8f;

        [Tooltip("Suavizado del giro.")]
        [SerializeField] private float rotationSmooth = 10f;

        [Tooltip("Si Nova se aleja más que esto, se teletransporta junto a Eira.")]
        [SerializeField] private float teleportDistance = 6f;

        [Tooltip("Velocidad máxima de Nova al recuperar distancia.")]
        [SerializeField] private float maxSpeed = 8f;

        [Tooltip("Radio del barrido para no atravesar paredes.")]
        [SerializeField] private float avoidRadius = 0.35f;

        [Tooltip("Distancia a la que mira en la dirección de movimiento en vez de a Eira.")]
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

            director = FindAnyObjectByType<MissionDirector>();

            GameEvents.OnPickedInfo += HandleInfo;
            GameEvents.OnPuzzleSolved += HandlePuzzle;
            GameEvents.OnBossDefeated += HandleBossEnd;
            GameEvents.OnSubtitle += OnSubtitleHook;

            // WarmSpawn: coloca a Nova en el suelo detrás de Eira al empezar la escena.
            if (player != null)
            {
                Vector3 spawnPos = ComputeSlotPosition();
                transform.position = ClampToGround(spawnPos);
                transform.rotation = ComputeLookRotation();
            }
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
                transform.position = ClampToGround(target);
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
        /// Punto de formación detrás y a la derecha de Eira.
        /// </summary>
        private Vector3 ComputeSlotPosition()
        {
            Vector3 position = player.transform.position;

            // Se usa el suelo bajo Eira como base, no su transform: así Nova
            // no sube con Eira cuando salta.
            Vector3 basePosition = ClampToGround(position);

            return basePosition
                - player.transform.forward * behindDistance
                + player.transform.right * sideOffset
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
            // NOVA es compinera de Eira y comparte su nivel, asi que su
            // referencia de suelo son los pies de Eira (base del
            // CharacterController).
            //
            // Antes se usaba un raycast vertical contra la geometria. En
            // esta ciudad no sirve: el FBX de Sketchfab esta rotado 270 en
            // X y el suelo no responde a los rayos frontales, asi que el
            // unico impacto era el plano Object_2 a y=1.892 y NOVA quedaba
            // flotando por encima del suelo real.
            if (player != null)
            {
                var pc = player.GetComponent<CharacterController>();

                float feetY = player.transform.position.y +
                              (pc != null ? pc.center.y - pc.height * 0.5f : 0f);

                position.y = feetY + followHeight;
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

            // Mira en la dirección en la que se mueve (su velocity horizontal),
            // con fallback a mirar a Eira si está quieta.
            CharacterController cc = playerController;
            if (cc != null)
            {
                Vector3 travel = cc.velocity;
                travel.y = 0f;

                if (travel.sqrMagnitude > 0.04f)
                {
                    // lookAheadDistance: mira un poco por delante en la dirección de movimiento
                    Vector3 lookAheadPos = player.transform.position + travel.normalized * lookAheadDistance;
                    Vector3 lookDir = lookAheadPos - transform.position;
                    lookDir.y = 0f;
                    return Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                }
            }

            // Fallback: mira a Eira (un poco por encima de la cabeza)
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