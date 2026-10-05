using System.Collections.Generic;
using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Guia a Eira a traves de las misiones del nivel.
    ///
    /// El problema que resuelve: el nivel tiene los objetos de mision
    /// repartidos (registros, paneles, terminales, salida), pero nada le
    /// decia a la jugadora cual era el siguiente ni donde estaba. Ahora:
    ///
    ///  1. Busca los objetivos reales en la escena por tipo de script, sin
    ///     posiciones escritas a mano: si el designer mueve algo, sigue bien.
    ///  2. Elige el siguiente segun la mision activa (el estado real, no un
    ///     contador propio que se pueda desincronizar).
    ///  3. Publica un waypoint que el HUD dibuja como flecha sobre el suelo.
    ///  4. Nova lo anuncia y recuerda si la jugadora se aleja.
    ///  5. Avanza solo cuando la mision se completa, asi que la guia termina
    ///     con el nivel.
    /// </summary>
    public class MissionDirector : MonoBehaviour
    {
        [Header("Waypoint")]
        [Tooltip("Altura del marcador sobre el suelo para que se vea por encima de Eira.")]
        [SerializeField] private float markerHeight = 0.06f;

        [Tooltip("Radio del anillo del marcador.")]
        [SerializeField] private float markerRadius = 0.85f;

        [Tooltip("Flecha flotante sobre el anillo.")]
        [SerializeField] private float arrowHeight = 2.3f;

        [Header("Guia de Nova")]
        [Tooltip("Distancia al objetivo a la que Nova deja de repetir donde esta.")]
        [SerializeField] private float nearDistance = 4f;

        [Tooltip("Segundos entre recordatorios si la jugadora se aleja del objetivo.")]
        [SerializeField] private float remindInterval = 24f;

        [Tooltip("Margen para el recordatorio: no molesta si la jugadora esta cerca.")]
        [SerializeField] private float remindDistance = 14f;

        [Header("Textos de Nova")]
        [TextArea(2, 4)] public string lineOnStart = "Vamos a por los datos, Eira. Te marco donde esta cada uno.";

        [TextArea(2, 4)] public string lineOnAllInfo = "Eso es todo. 3000... Los humanos llevan decades desaparecidos. La salida esta por ahi.";

        [TextArea(2, 4)] public string lineOnPuzzleSolved = "Puerta abierta. Hay paneles por delante, activalos con F.";

        [TextArea(2, 4)] public string lineOnPanelsDone = "Ya no queda ninguna consola. El guardian esta mas adelante: carga los tres terminales con F.";

        [TextArea(2, 4)] public string lineOnBossEnd = "Salida despejada. Corre, Eira, corre.";

        [TextArea(2, 4)] public string remindLine = "Por aqui, Eira. Ves la baliza verde?";

        [Header("Objetivo mostrado en el HUD")]
        [TextArea(1, 3)] public string hintOnStart = "Recoge los 4 registros de datos";

        [TextArea(1, 3)] public string hintOnAllInfo = "Cruza hasta la salida";

        [TextArea(1, 3)] public string hintOnPanelsDone = "Activa las consolas con F";

        [TextArea(1, 3)] public string hintOnBoss = "Carga los 3 terminales del guardian (F)";

        [TextArea(1, 3)] public string hintOnWin = "Vuelve a la salida";

        public Vector3 TargetPosition { get; private set; }
        public bool HasTarget { get; private set; }
        public float TargetDistance { get; private set; }

        PlayerController player;
        GameManager gm;

        readonly List<Transform> infoTargets = new List<Transform>();
        readonly List<Transform> puzzleTargets = new List<Transform>();
        readonly List<Transform> terminalTargets = new List<Transform>();
        readonly List<Transform> exitTargets = new List<Transform>();

        ObjectiveMarker marker;
        float nextRemindTime;
        bool saidStart;
        bool saidAllInfo;
        bool saidPuzzle;
        bool saidPanelsDone;
        bool saidWin;

        void Awake()
        {
            player = FindAnyObjectByType<PlayerController>();
            gm = GameManager.Instance;
        }

        void OnEnable()
        {
            GameEvents.OnPuzzleSolved += OnPuzzleSolved;
        }

        void OnDisable()
        {
            GameEvents.OnPuzzleSolved -= OnPuzzleSolved;
        }

        void Start()
        {
            CacheTargets();
            BuildMarker();

            if (gm != null)
                gm.missionDirector = this;

            if (!saidStart)
            {
                saidStart = true;
                GameEvents.NovaSpeak(lineOnStart, 5f);
                GameEvents.Objective(hintOnStart);
            }
        }

        void OnDestroy()
        {
            if (marker != null)
                Destroy(marker.gameObject);
        }

        // =========================================================
        // DESCUBRIR OBJETIVOS
        // =========================================================

        /// <summary>
        /// Se recorren los scripts reales del nivel. Si el nivel cambia, esta
        /// lista se adapta sola; no hay posiciones escritas a mano.
        /// </summary>
        void CacheTargets()
        {
            infoTargets.Clear();
            puzzleTargets.Clear();
            terminalTargets.Clear();
            exitTargets.Clear();

            foreach (var t in FindObjectsByType<DataLog>(FindObjectsInactive.Exclude))
                infoTargets.Add(t.transform);

            foreach (var p in FindObjectsByType<PanelActivator>(FindObjectsInactive.Exclude))
            {
                if (p.kind == PanelKind.BossTerminal)
                    terminalTargets.Add(p.transform);
                else
                    puzzleTargets.Add(p.transform);
            }

            foreach (var z in FindObjectsByType<LevelExitZone>(FindObjectsInactive.Exclude))
                exitTargets.Add(z.transform);
        }

        // =========================================================
        // ELECCION DEL OBJETIVO ACTUAL
        // =========================================================

        void Update()
        {
            if (gm == null)
                gm = GameManager.Instance;

            if (gm == null)
                return;

            // La cache se rehace si el nivel ha cambiado (por ejemplo tras
            // un respawn que destruye y recrea pickups).
            if (infoTargets.Count == 0 && gm.InfoCollectedCount < EiraConst.InfoLogsNeeded)
                CacheTargets();

            HasTarget = ChooseTarget(out Vector3 at);

            if (!HasTarget)
            {
                if (marker != null)
                    marker.gameObject.SetActive(false);

                TargetPosition = Vector3.zero;
                TargetDistance = 0f;
                GameEvents.ObjectiveMarkerMoved(Vector3.zero);
                return;
            }

            TargetPosition = at;

            if (player != null)
                TargetDistance = Vector3.Distance(player.transform.position, at);

            if (marker != null)
            {
                marker.gameObject.SetActive(true);
                marker.Place(at, markerRadius, markerHeight, arrowHeight);
            }

            // Se publica el punto en el suelo (Y = 0 para el HUD), no la
            // altura de la flecha, para que la flecha del HUD sea coherente.
            GameEvents.ObjectiveMarkerMoved(at);

            UpdateReminder();
        }

        bool ChooseTarget(out Vector3 at)
        {
            GameManager g = GameManager.Instance;

            // 1. Registros de datos: mientras falten.
            if (g == null ||
                g.InfoCollectedCount < EiraConst.InfoLogsNeeded)
            {
                Transform info = NearestActive(infoTargets, out at);
                if (info != null)
                    return true;
            }

            // 2. Paneles de puzle, si el nivel los tiene activos.
            Transform panel = NearestActive(puzzleTargets, out at);
            if (panel != null)
            {
                if (!saidPuzzle)
                {
                    saidPuzzle = true;
                    GameEvents.NovaSpeak(lineOnPuzzleSolved, 5f);
                    GameEvents.Objective(hintOnPanelsDone);
                }

                return true;
            }

            // 3. Terminales del guardian.
            Transform terminal = NearestActive(terminalTargets, out at);
            if (terminal != null)
            {
                if (!saidPanelsDone)
                {
                    saidPanelsDone = true;
                    GameEvents.NovaSpeak(lineOnPanelsDone, 5f);
                    GameEvents.Objective(hintOnBoss);
                }

                return true;
            }

            // 4. Ultimate: la salida.
            Transform exit = NearestActive(exitTargets, out at);
            if (exit != null)
            {
                if (g != null && g.BossDefeated)
                {
                    if (!saidWin)
                    {
                        saidWin = true;
                        GameEvents.NovaSpeak(lineOnBossEnd, 5f);
                    }

                    GameEvents.Objective(hintOnWin);
                }
                else
                {
                    if (!saidAllInfo)
                    {
                        saidAllInfo = true;
                        GameEvents.NovaSpeak(lineOnAllInfo, 5.5f);
                    }

                    GameEvents.Objective(hintOnAllInfo);
                }

                return true;
            }

            at = Vector3.zero;
            return false;
        }

        Transform NearestActive(List<Transform> list, out Vector3 at)
        {
            at = Vector3.zero;

            if (player == null || list == null || list.Count == 0)
                return null;

            Transform best = null;
            float bestSqr = float.MaxValue;
            Vector3 origin = player.transform.position;

            for (int i = 0; i < list.Count; i++)
            {
                Transform t = list[i];

                if (t == null || !t.gameObject.activeInHierarchy)
                    continue;

                float sqr = (t.position - origin).sqrMagnitude;

                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = t;
                }
            }

            if (best == null)
                return null;

            // Se marca el suelo, no el centro del objeto: la baliza tiene
            // que quedar bajo los pies, no a media altura.
            at = best.position;
            return best;
        }

        void UpdateReminder()
        {
            if (player == null || !HasTarget)
                return;

            if (TargetDistance <= nearDistance)
                return;

            if (TargetDistance <= remindDistance)
                return;

            if (Time.time < nextRemindTime)
                return;

            nextRemindTime = Time.time + remindInterval;

            GameEvents.NovaSpeak(remindLine, 3.5f);
        }

        // =========================================================
        // EVENTOS
        // =========================================================

        // No hace falta reaccionar a cada registro: ChooseTarget ya comprueba
        // InfoCollectedCount en cada frame y en cuanto se completa el
        // objetivo cambia solo de "registros" a "salida" y anuncia el texto.

        void OnPuzzleSolved()
        {
            // Al abrirse la puerta, el siguiente objetivo vuelve a evaluarse
            // desde cero: pueden quedar paneles sin activar.
            saidPuzzle = false;
            saidAllInfo = false;
        }

        // =========================================================
        // MARCADOR
        // =========================================================

        void BuildMarker()
        {
            var go = new GameObject("ObjectiveMarker");
            go.transform.SetParent(transform, false);
            marker = go.AddComponent<ObjectiveMarker>();
            go.SetActive(false);
        }
    }

    /// <summary>
    /// Baliza de objetivo: un anillo en el suelo y una flecha flotando.
    /// Todo generado por codigo, sin assets, con cache de material.
    /// </summary>
    public class ObjectiveMarker : MonoBehaviour
    {
        static readonly Color MarkerColor = new Color(0.35f, 1f, 0.7f);

        Transform ring;
        Transform arrow;
        Vector3 ground;
        float baseY;
        float arrowBaseY;
        float radius;
        float phase;
        Light light;

        public void Place(Vector3 position, float ringRadius, float groundOffset, float arrowHeight)
        {
            ground = position;
            radius = ringRadius;
            baseY = position.y + groundOffset;
            arrowBaseY = position.y + arrowHeight;
        }

        void EnsureBuilt()
        {
            if (ring != null)
                return;

            phase = Random.value * 6.28f;

            // --- anillo en el suelo ---
            var ringGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ringGo.name = "Ring";

            var ringCol = ringGo.GetComponent<Collider>();
            if (ringCol != null)
                Destroy(ringCol);

            ringGo.transform.SetParent(transform, false);
            ringGo.GetComponent<Renderer>().material = CombatFX.GlowMaterial(MarkerColor, 4f);
            ring = ringGo.transform;

            // --- flecha flotante: cono apuntando hacia abajo ---
            var arrowGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            arrowGo.name = "Arrow";

            var arrowCol = arrowGo.GetComponent<Collider>();
            if (arrowCol != null)
                Destroy(arrowCol);

            arrowGo.transform.SetParent(transform, false);
            arrowGo.GetComponent<Renderer>().material = CombatFX.GlowMaterial(MarkerColor, 5f);
            arrow = arrowGo.transform;

            light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = MarkerColor;
            light.range = 7f;
            light.intensity = 1.5f;
            light.shadows = LightShadows.None;
        }

        void Update()
        {
            if (ring == null)
                EnsureBuilt();

            float t = Time.time;

            // Anillo: gira y late, con el radio base recuperado del suelo
            // en vez de multiplicar el scale actual (que se acumularía).
            float pulse = 1f + Mathf.Sin(t * 2.2f + phase) * 0.07f;
            float diameter = radius * 2f * pulse;

            ring.localScale = new Vector3(diameter, 1f, diameter);
            ring.localRotation = Quaternion.identity;
            ring.localPosition = new Vector3(0f, baseY - ground.y + 0.02f, 0f);
            ring.Rotate(0f, 55f * Time.deltaTime, 0f, Space.World);

            // Flecha: flota apuntando hacia abajo.
            arrow.localScale = new Vector3(0.34f, 0.22f, 0.34f);
            arrow.localPosition = new Vector3(
                0f,
                arrowBaseY - ground.y + Mathf.Sin(t * 2.6f + phase) * 0.16f,
                0f);
            arrow.Rotate(180f, 0f, 0f);
        }
    }
}
