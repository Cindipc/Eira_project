using UnityEngine;

namespace EiraGame
{
    // Núcleo del juego: vidas, puntos, misiones, reinicios y flujo del nivel.
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public PlayerController Player => player;
        public NovaCompanion Nova => nova;
        public float CameraYaw { get; set; }

        public int Score { get; private set; }
        public int Lives { get; private set; }
        public int MaxLives { get; private set; } = EiraConst.MaxLives;
        public int InfoCollectedCount { get; private set; }
        public GameState State { get; private set; }
        public bool BossDefeated { get; private set; }
        public float PlayerHealth => player != null ? player.Health : 0f;
        public float PlayerHeart => player != null ? player.HeartFrac : 0f;

        /// <summary>Director de misiones (waypoints y guia de Nova). Opcional.</summary>
        public MissionDirector missionDirector;

        PlayerController player;
        NovaCompanion nova;
        public HUDManager hud;

        Vector3 checkpoint;
        Quaternion checkpointRot;
        bool[] missionsDone = new bool[6];
        public bool[] MissionsDone => missionsDone;
        public int CurrentMission { get; private set; }

        int puzzlePanelsCharged;
        int bossTerminalsCharged;
        public int PuzzlePanelsCharged => puzzlePanelsCharged;
        public int BossTerminalsCharged => bossTerminalsCharged;

        /// <summary>Drones que Eira ha derribado con el arma.</summary>
        public int DronesDestroyed { get; private set; }

        /// <summary>Drones que quedaban vivos en el nivel al empezar.</summary>
        public int DronesAlive { get; private set; }

        void Awake()
        {
            Instance = this;

            GameEvents.ResetAll();

            State = GameState.Playing;
            Lives = MaxLives;
            Score = 0;
            InfoCollectedCount = 0;

            player = FindAnyObjectByType<PlayerController>();
            nova = FindAnyObjectByType<NovaCompanion>();
            DronesAlive = FindObjectsByType<DroneController>(FindObjectsInactive.Exclude).Length;

            if (player != null)
            {
                Debug.Log(
                    "PLAYER ENCONTRADO: " +
                    player.gameObject.name +
                    " | POSICIÓN: " +
                    player.transform.position
                );
            }
            else
            {
                Debug.LogError("NO SE ENCONTRÓ NINGÚN PlayerController");
            }

            checkpoint = player != null ? player.transform.position : Vector3.zero;
            checkpointRot = player != null ? player.transform.rotation : Quaternion.identity;

            // La cámara publica su yaw cada LateUpdate, pero hay que darle
            // un valor inicial coherente: si se queda en 0, el primer frame
            // Eira se movería en una dirección que el jugador no ve.
            CameraYaw = checkpointRot.eulerAngles.y;

            AudioFX.Init();

            // Ensure main camera has FirstPersonCamera component
            FirstPersonCamera.EnsureMainCamera();

            EnsureHud();
            EnsureMissionDirector();
            EnsureLevelGround();
            EnsureEnvironmentDressing();

            SetObjective(Missions.Names[Missions.MWake]);
        }

        /// <summary>
        /// El director de misiones y la ambientación se crean en runtime si
        /// no están en la escena. Así la guía de Nova y el aspecto futurista
        /// funcionan aunque el nivel se haya guardado sin ellos, sin depender
        /// de editar la escena a mano (que además se perdería al reabrirla).
        /// </summary>
        private void EnsureMissionDirector()
        {
            missionDirector = FindAnyObjectByType<MissionDirector>();

            if (missionDirector != null)
                return;

            var go = new GameObject("MissionDirector");
            missionDirector = go.AddComponent<MissionDirector>();
        }

        private void EnsureEnvironmentDressing()
        {
            if (FindAnyObjectByType<EnvironmentDressing>() != null)
                return;

            var go = new GameObject("EnvironmentDressing");
            go.AddComponent<EnvironmentDressing>();
        }

        /// <summary>
        /// Suelo y objetos del recorrido: si el nivel quedó con mitad del
        /// camino sin piso y objetos flotando, este componente lo repara en
        /// runtime (antes de que la ambientación y los drones fijen su
        /// posición definitiva).
        /// </summary>
        private void EnsureLevelGround()
        {
            if (FindAnyObjectByType<LevelGroundFixer>() != null)
                return;

            var go = new GameObject("LevelGroundFixer");
            go.AddComponent<LevelGroundFixer>();
        }

        void Start()
        {
            // El checkpoint inicial se toma DESPUÉS de que PlayerController
            // haya asentado a Eira en el suelo, no en su posición de escena
            // (que puede estar flotando sobre una plataforma).
            if (player != null)
            {
                checkpoint = player.transform.position;
                checkpointRot = player.transform.rotation;
                CameraYaw = checkpointRot.eulerAngles.y;
            }

            // Sin cursor capturado la cámara no se puede girar.
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Materiales lit de runtime (sin AssetDatabase) para el modelo nativo de Eira.
        static Material MakeLit(Color c)
        {
            var sh = Shader.Find("Standard");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            var m = new Material(sh);
            m.name = "EiraNativa_" + c;
            m.color = c;
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            return m;
        }

        void EnsureHud()
        {
            hud = FindAnyObjectByType<HUDManager>();
            if (hud == null)
            {
                var go = new GameObject("HUD");
                hud = go.AddComponent<HUDManager>();
            }
            hud.Initialize();
        }

        public void SetObjective(string s)
        {
            GameEvents.Objective(s);
        }

        // -------- pausa --------
        public bool IsPaused => State == GameState.Paused;

        public void TogglePause()
        {
            // Solo se puede pausar durante el juego: en victoria o derrota
            // los paneles finales ya ocupan la pantalla.
            if (State != GameState.Playing && State != GameState.Paused)
                return;

            bool paused = State != GameState.Paused;

            State = paused ? GameState.Paused : GameState.Playing;

            Time.timeScale = paused ? 0f : 1f;

            if (paused)
                Cursor.lockState = CursorLockMode.None;
            else
                Cursor.lockState = CursorLockMode.Locked;

            if (hud != null)
                hud.SetPaused(paused);
        }

        public void RestartLevel()
        {
            Time.timeScale = 1f;
            State = GameState.Playing;

            if (hud != null)
                hud.SetPaused(false);

            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            UnityEngine.SceneManagement.SceneManager.LoadScene(currentScene);
        }

        public void AddScore(int amount)
        {
            Score += amount;
            GameEvents.ScoreAdded(amount);
        }

        // -------- datos de información --------
        public void OnInfoCollected(int idx)
        {
            InfoCollectedCount++;
            AddScore(Points.Info);
            AudioFX.Info();
            GameEvents.CollectedChanged(InfoCollectedCount, EiraConst.InfoLogsNeeded);
            if (InfoCollectedCount >= EiraConst.InfoLogsNeeded)
            {
                CompleteMission(Missions.MInfo);
            }
        }

        // -------- acertijo de puerta --------
        public void OnPuzzlePanelCharged()
        {
            puzzlePanelsCharged++;
            AddScore(Points.Deactivate);
            if (puzzlePanelsCharged >= 2)
            {
                AddScore(Points.Puzzle);
                GameEvents.PuzzleSolved();
                CompleteMission(Missions.MDoors);
            }
        }

        // -------- jefe --------
        public void OnBossActive()
        {
            MissionDone(Missions.MBoss);
        }

        public void OnTerminalChargedProgress(int cur, int need)
        {
            GameEvents.NovaSpeak("¡Bien! El escudo se está agotando. " + (need - cur) + " terminal más.", 3f);
        }

        public void OnBossTerminalCharged()
        {
            bossTerminalsCharged++;
            AddScore(Points.Deactivate);
        }

        public void OnBossDefeated()
        {
            BossDefeated = true;
            CompleteMission(Missions.MBoss);
        }

        // -------- evasión --------
        public void OnDroneEvaded()
        {
            AddScore(Points.AvoidEnemy);
            GameEvents.Subtitle("Enemigo evadido +" + Points.AvoidEnemy, 1.8f);
        }

        /// <summary>
        /// Un dron ha sido destruido con un disparo. Se avisa a NOVA y se
        /// suman los puntos; el dron ya se ha desactivado él mismo.
        /// </summary>
        public void OnDroneDestroyed(DroneController drone, int points, Transform shooter)
        {
            DronesDestroyed++;

            if (points > 0)
                AddScore(points);

            if (drone != null)
                DronesAlive = Mathf.Max(0, DronesAlive - 1);

            GameEvents.DroneDestroyed(points);
        }

        // -------- salud / corazón --------
        public void OnPlayerHit()
        {
            if (hud != null) hud.FlashDamage();
            RefreshPlayerHud();
        }

        /// <summary>
        /// Eira ha bloqueado un golpe (y normalmente lo ha devuelto). Refresca
        /// el HUD del corazón y avisa a la cámara para que el bloqueo se note.
        /// </summary>
        public void OnPlayerBlocked()
        {
            RefreshPlayerHud();
        }

        public void HealPlayer(float amount)
        {
            if (player != null) player.Heal(amount);
        }

        public void RestoreHeart(float amount)
        {
            if (player != null) player.RestoreHeart(amount);
        }

        public void RefreshPlayerHud()
        {
            if (hud != null) hud.RefreshBars();
        }

        // -------- muerte / reinicio --------
        public void OnPlayerDied()
        {
            if (State != GameState.Playing) return;
            Lives--;
            AudioFX.Lose();
            if (hud != null) hud.RefreshLives();
            if (Lives <= 0)
            {
                State = GameState.GameOver;
                if (hud != null) hud.ShowGameOver(Score);
                return;
            }
            RespawnPlayer();
        }

        public void RespawnPlayer()
        {
            if (player == null) return;
            player.RespawnAt(checkpoint, checkpointRot);
            SetObjective("Vidas restantes: " + Lives + ". Sigue avanzando.");
            if (hud != null) { hud.RefreshLives(); hud.RefreshBars(); }
        }

        public void SetCheckpoint(Vector3 pos, Quaternion rot)
        {
            checkpoint = pos;
            checkpointRot = rot;
        }

        // -------- salida / victoria --------
        public void OnExitReached()
        {
            if (State != GameState.Playing) return;
            if (!BossDefeated)
            {
                SetObjective("La salida está bloqueada. Derrota a la Máquina Guardiana.");
                return;
            }
            State = GameState.Won;
            CompleteMission(Missions.MEscape);
            AddScore(Points.Mission);
            AudioFX.Win();
            if (hud != null) hud.ShowWin(Score, Lives);
        }

        /// <summary>
        /// Victoria del Nivel 2: llegar al refugio rebelde con el Nucleo Central
        /// encontrado. Se separa de <see cref="OnExitReached"/> porque Kael no
        /// se puede derrotar, asi que <see cref="BossDefeated"/> nunca cambia.
        /// </summary>
        public void WinLevel2()
        {
            if (State != GameState.Playing) return;

            State = GameState.Won;
            CompleteMission(Level2Missions.MRefuge);
            AddScore(Level2Points.LevelComplete);
            AudioFX.Win();
            if (hud != null) hud.ShowWin(Score, Lives);
        }

        /// <summary>Cambia el maximo de vidas (el Nivel 3 empieza con 5).</summary>
        public void SetMaxLives(int n)
        {
            MaxLives = Mathf.Max(1, n);
            Lives = MaxLives;
            if (hud != null)
            {
                hud.SetHeartCount(MaxLives);
                hud.RefreshLives();
            }
        }

        /// <summary>
        /// Nivel 3 completado: Eira libera el Nucleo y elige el destino de
        /// humanos y androides (libertad).
        /// </summary>
        public void WinLevel3()
        {
            if (State != GameState.Playing) return;

            State = GameState.Won;
            CompleteMission(Level3Missions.MChooseDestiny);
            AudioFX.Win();
            if (hud != null) hud.ShowWin(Score, Lives);
        }

        /// <summary>Kael ha capturado a Eira: se pierde el nivel.</summary>
        public void OnCapturedByKael()
        {
            if (State != GameState.Playing) return;

            GameEvents.SetBossBar(0f);
            State = GameState.GameOver;
            AudioFX.Lose();
            if (hud != null) hud.ShowGameOver(Score);
        }

        public void LoadNextLevel()
        {
            string currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string nextScene = GetNextLevelScene(currentScene);
            if (!string.IsNullOrEmpty(nextScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
            }
        }

        private string GetNextLevelScene(string currentScene)
        {
            if (currentScene == EiraConst.Level1Scene) return EiraConst.Level2Scene;
            // El Nivel 3 se juega dentro de Level2Scene (interior del Nucleo).
            return "";
        }

        // -------- misiones --------
        public bool IsMissionDone(int id) => missionsDone[id];

        void MissionDone(int id)
        {
            missionsDone[id] = true;
        }

        public void CompleteMission(int id)
        {
            missionsDone[id] = true;
            if (hud != null) hud.RefreshMissions();
        }

        /// <summary>Reinicia la lista de misiones (al entrar al interior del Nivel 3).</summary>
        public void ResetMissions()
        {
            for (int i = 0; i < missionsDone.Length; i++)
                missionsDone[i] = false;
            CurrentMission = 0;
            if (hud != null) hud.RefreshMissions();
        }
    }
}