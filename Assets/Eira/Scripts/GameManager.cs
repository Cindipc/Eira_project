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
        public int InfoCollectedCount { get; private set; }
        public GameState State { get; private set; }
        public bool BossDefeated { get; private set; }
        public float PlayerHealth => player != null ? player.Health : 0f;
        public float PlayerHeart => player != null ? player.HeartFrac : 0f;

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

        void Awake()
        {
            Instance = this;
            GameEvents.ResetAll();
            State = GameState.Playing;
            Lives = EiraConst.MaxLives;
            Score = 0;
            InfoCollectedCount = 0;
            player = FindObjectOfType<PlayerController>();
            nova = FindObjectOfType<NovaCompanion>();
            if (player != null)
                EiraModelo.EnsureNativa(player.visual, MakeLit);
            checkpoint = player != null ? player.transform.position : Vector3.zero;
            checkpointRot = player != null ? player.transform.rotation : Quaternion.identity;

            AudioFX.Init();
            EnsureHud();

            SetObjective(Missions.Names[Missions.MWake]);
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
            hud = FindObjectOfType<HUDManager>();
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

        // -------- salud / corazón --------
        public void OnPlayerHit()
        {
            if (hud != null) hud.FlashDamage();
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
    }
}