using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Orquesta el Nivel 3 "La ultima guerra", que se juega en el interior del
    /// Nucleo Central (una zona aparte dentro de Level2Scene). Eira entra con 5
    /// vidas, se enfrenta a los androides controlados, desactiva la seguridad,
    /// derrota a Kael, accede al sistema central y elige el destino de humanos
    /// y androides (libertad).
    /// </summary>
    public class Level3Director : MonoBehaviour
    {
        public static Level3Director Instance { get; private set; }

        [Header("Escena interior (asignado por el builder)")]
        public Transform interiorSpawn;
        public GameObject interiorRoot;
        public KaelFinalBoss kael;

        public bool Started { get; private set; }
        public bool KaelDefeated { get; private set; }
        public bool CoreAccessed { get; private set; }
        public bool Completed { get; private set; }
        public bool NoDeath { get; private set; }

        public int ControlledKilled { get; private set; }
        public int SecurityDisabled { get; private set; }
        public int RebelsRescued { get; private set; }
        public int FilesFound { get; private set; }

        public FinalChoice Choice { get; private set; } = FinalChoice.None;

        int livesAtStart = -1;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Entra al interior del Nucleo desde el portal del Nivel 2.</summary>
        public void Begin(PlayerController p, Transform spawn)
        {
            if (Started) return;
            Started = true;

            if (spawn != null) interiorSpawn = spawn;
            if (interiorRoot != null) interiorRoot.SetActive(true);

            HUDManager.MissionNames = Level3Missions.Names;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.ResetMissions();
                gm.SetMaxLives(5);
                livesAtStart = gm.Lives;

                if (gm.hud != null) gm.hud.RefreshMissions();

                gm.CompleteMission(Level3Missions.MEnterNucleo);
                gm.SetObjective(Level3Missions.Names[Level3Missions.MDefeatControlled]);

                Vector3 pos = interiorSpawn != null ? interiorSpawn.position : p.transform.position;
                Vector3 fwd = interiorSpawn != null ? interiorSpawn.forward : p.transform.forward;
                p.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
                gm.SetCheckpoint(pos, p.transform.rotation);

                if (gm.Nova != null && interiorSpawn != null)
                    gm.Nova.transform.position = pos + interiorSpawn.right * 1.4f - interiorSpawn.forward * 1.2f;
            }

            // El Nivel 2 ya no decide el final: se apaga su director.
            if (Level2Director.Instance != null)
                Level2Director.Instance.enabled = false;

            var cam = FirstPersonCamera.EnsureMainCamera();
            if (cam != null) cam.SnapBehind();

            GameEvents.NovaSpeak("Estamos dentro, Eira. Kael controla la ciudad desde aqui. Acabemos con esto.", 5f);
            GameEvents.Subtitle("NIVEL 3 - EL NUCLEO CENTRAL", 3f);

            if (kael == null) kael = FindAnyObjectByType<KaelFinalBoss>();
            if (kael != null) kael.Arm(this);
        }

        public void OnControlledKilled()
        {
            if (!Started) return;
            ControlledKilled++;

            var gm = GameManager.Instance;
            if (ControlledKilled >= Level3Missions.ControlledNeeded)
            {
                if (gm != null)
                {
                    gm.CompleteMission(Level3Missions.MDefeatControlled);
                    gm.AddScore(Level3Points.Mission);
                    gm.SetObjective(Level3Missions.Names[Level3Missions.MDisableSecurity]);
                }
                GameEvents.NovaSpeak("Limpiaste la sala. Ahora los sistemas de seguridad.", 3.5f);
            }
            else if (gm != null)
            {
                gm.SetObjective(Level3Missions.Names[Level3Missions.MDefeatControlled]
                    + " (" + ControlledKilled + "/" + Level3Missions.ControlledNeeded + ")");
            }
        }

        public void OnSecurityDisabled(SecurityNode node)
        {
            if (!Started) return;
            SecurityDisabled++;

            if (SecurityDisabled >= Level3Missions.SecurityNeeded)
            {
                var gm = GameManager.Instance;
                if (gm != null)
                {
                    gm.CompleteMission(Level3Missions.MDisableSecurity);
                    gm.AddScore(Level3Points.Mission);
                    gm.SetObjective(Level3Missions.Names[Level3Missions.MFaceKael]);
                }
                GameEvents.NovaSpeak("Seguridad desactivada. Kael te espera en la camara del Nucleo.", 4f);
                if (kael != null) kael.OpenArena();
            }
            else
            {
                GameEvents.Objective(Level3Missions.Names[Level3Missions.MDisableSecurity]
                    + " (" + SecurityDisabled + "/" + Level3Missions.SecurityNeeded + ")");
            }
        }

        public void OnRebelRescued(RebelAndroid rebel)
        {
            if (!Started) return;
            RebelsRescued++;
        }

        public void OnSecretFile(SecretFile file)
        {
            if (!Started) return;
            FilesFound++;
        }

        public void OnBossPhase(int phase)
        {
            if (!Started) return;
            var gm = GameManager.Instance;
            if (gm == null) return;

            switch (phase)
            {
                case 2:
                    gm.SetObjective("Kael invoca androides controlados: sobrevive y contrataca");
                    break;
                case 3:
                    gm.SetObjective("Kael usa el poder del Nucleo: resiste");
                    break;
            }
        }

        public void OnKaelDefeated()
        {
            if (!Started || KaelDefeated) return;
            KaelDefeated = true;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AddScore(Level3Points.DefeatKael);
                gm.CompleteMission(Level3Missions.MFaceKael);
                gm.SetObjective(Level3Missions.Names[Level3Missions.MReachCore]);
            }

            GameEvents.NovaSpeak("Kael ha caido. Ahora accede al sistema central con tu sangre.", 4.5f);

            var core = FindAnyObjectByType<NucleoCore>();
            if (core != null) core.Reveal();
        }

        public void OnCoreAccessed()
        {
            if (!Started || CoreAccessed) return;
            CoreAccessed = true;

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.CompleteMission(Level3Missions.MReachCore);
                gm.SetObjective(Level3Missions.Names[Level3Missions.MChooseDestiny]);
            }

            GameEvents.Subtitle("SISTEMA CENTRAL ACCEDIENDO...", 2.5f);
            GameEvents.NovaSpeak("Tres opciones en pantalla. Elige, Eira.", 4f);

            var consoles = FindObjectsByType<FinalChoiceConsole>(FindObjectsInactive.Include);
            foreach (var c in consoles)
                if (c != null) c.Reveal();
        }

        public void OnChoice(FinalChoice choice)
        {
            if (!Started || Completed) return;
            if (!CoreAccessed) return;

            if (choice != FinalChoice.Freedom)
            {
                GameEvents.Subtitle("Eira: No puedo esclavizar a nadie. Elijo la libertad.", 3f);
                GameEvents.NovaSpeak("Eira... ese camino condena al mundo. Prueba otra opcion.", 3f);
                return;
            }

            Choice = FinalChoice.Freedom;
            ChooseFreedom();
        }

        void ChooseFreedom()
        {
            Completed = true;

            var gm = GameManager.Instance;
            NoDeath = gm != null && livesAtStart > 0 && gm.Lives == livesAtStart;

            if (gm != null)
            {
                gm.AddScore(Level3Points.FreeNucleo);
                if (NoDeath) gm.AddScore(Level3Points.NoDeath);
            }

            GameEvents.Subtitle("Eira: Los androides no son maquinas. Son libres.", 3f);
            GameEvents.NovaSpeak("El Nucleo libera a todos los androides. Kael cae. La ciudad respira.", 5f);
            StartCoroutine(Epilogue());
        }

        System.Collections.IEnumerator Epilogue()
        {
            yield return new WaitForSeconds(3.5f);
            GameEvents.Subtitle("SIN CONTROL. SIN AMOS. EL MUNDO SE RECONSTRUYE.", 4f);
            yield return new WaitForSeconds(4.5f);
            GameEvents.NovaSpeak("Fin del viaje, Eira. Los androides son libres.", 4f);
            yield return new WaitForSeconds(2f);

            if (GameManager.Instance != null)
                GameManager.Instance.WinLevel3();
        }
    }
}
