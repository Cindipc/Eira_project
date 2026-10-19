using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Orquesta el Nivel 2 "La caza": cuenta rebeldes, piezas y archivos,
    /// empuja los objetivos a Nova y decide cuando se gana (refugio + Nucleo
    /// Central) o se pierde (Kael).
    /// </summary>
    public class Level2Director : MonoBehaviour
    {
        public static Level2Director Instance { get; private set; }

        [Header("Objetivos (HUD)")]
        [TextArea(1, 3)] public string objEscapeCity = "Escapa de la ciudad destruida";
        [TextArea(1, 3)] public string objFindRebels = "Libera a los rebeldes que encuentres [E]";
        [TextArea(1, 3)] public string objReachRefuge = "Llega al refugio rebelde del este";
        [TextArea(1, 3)] public string objFindNucleo = "Encuentra el Nucleo Central en el refugio";

        public int RebelsRescued { get; private set; }
        public int TechPiecesCollected { get; private set; }
        public int ProjectFilesRead { get; private set; }
        public bool NucleoFound { get; private set; }
        public bool InRefuge { get; private set; }
        public int AndroidsKilled { get; private set; }
        public bool Won { get; private set; }

        void Awake()
        {
            Instance = this;
            HUDManager.MissionNames = Level2Missions.Names;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Start()
        {
            GameEvents.NovaSpeak("Bienvenida a la ciudad de 3000, Eira. Los androides patrullan. Busca a los rebeldes.", 5f);
            GameManager gm = GameManager.Instance;
            if (gm != null)
                gm.SetObjective(objEscapeCity);
        }

        void Update()
        {
            // Objetivo dinamico mientras falten rebeldes.
            if (!Won && RebelsRescued < Level2Missions.RebelsNeeded && Time.frameCount % 30 == 0)
            {
                // no-op: los objetivos se empujan en los eventos
            }
        }

        public void OnAndroidKilled()
        {
            AndroidsKilled++;
        }

        public void OnRebelRescued(RebelAndroid rebel)
        {
            RebelsRescued++;

            if (RebelsRescued >= Level2Missions.RebelsNeeded)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.CompleteMission(Level2Missions.MFindRebels);
                    gm.AddScore(Level2Points.SideQuest);
                    gm.SetObjective(objReachRefuge);
                }
            }
            else
            {
                GameEvents.Objective(objFindRebels + " (" + RebelsRescued + "/" + Level2Missions.RebelsNeeded + ")");
            }
        }

        public void OnTechPieceCollected()
        {
            TechPiecesCollected++;
            if (TechPiecesCollected >= Level2Missions.TechPiecesNeeded)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.CompleteMission(Level2Missions.MTechPieces);
                    gm.AddScore(Level2Points.SideQuest);
                }
            }
        }

        public void OnProjectFileRead()
        {
            ProjectFilesRead++;
            if (ProjectFilesRead >= Level2Missions.ProjectFilesNeeded)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.CompleteMission(Level2Missions.MProjectFiles);
                    gm.AddScore(Level2Points.SideQuest);
                }
            }
        }

        public void OnNucleoFound()
        {
            NucleoFound = true;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.CompleteMission(Level2Missions.MProjectFiles);
                gm.SetObjective("Entra al Nucleo Central [E] para la batalla final");
            }

            GameEvents.NovaSpeak("Nucleo Central localizado. Eira... aqui es donde todo termina. Entra.", 5f);
        }

        public void OnEnteredRefuge()
        {
            InRefuge = true;
            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.CompleteMission(Level2Missions.MEscapeCity);
                gm.CompleteMission(Level2Missions.MAvoidPatrols);
                if (!NucleoFound)
                    gm.SetObjective(objFindNucleo);
            }
        }
    }
}
