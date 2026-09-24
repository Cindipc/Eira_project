using System;

namespace EiraGame
{
    public static class EiraConst
    {
        public const string IntroScene = "IntroScene";
        public const string Level1Scene = "Level1Scene";
        public const int MaxLives = 3;
        public const float MaxHealth = 100f;
        public const float MaxHeart = 100f;
        public const int InfoLogsNeeded = 4;
    }

    // Eventos simples del juego (evitan acoplar todos los sistemas).
    public static class GameEvents
    {
        public static event Action<int> OnScoreAdded;
        public static event Action<string, float> OnNovaSpeak;
        public static event Action<string> OnObjective;
        public static event Action<string> OnPickedInfo;
        public static event Action<int, int> OnCollectedChanged;   // actuales, necesarias
        public static event Action OnHeartDanger;
        public static event Action OnAbilityUsed;
        public static event Action<string> OnInteractPrompt;
        public static event Action OnPuzzleSolved;
        public static event Action<float> OnSetBossBar;           // 0 = ocultar
        public static event Action OnBossDefeated;
        public static event Action<string, float> OnSubtitle;

        public static void ScoreAdded(int v) => OnScoreAdded?.Invoke(v);
        public static void NovaSpeak(string s, float d = 3.5f) => OnNovaSpeak?.Invoke(s, d);
        public static void Objective(string s) => OnObjective?.Invoke(s);
        public static void PickedInfo(string s) => OnPickedInfo?.Invoke(s);
        public static void CollectedChanged(int cur, int need) => OnCollectedChanged?.Invoke(cur, need);
        public static void HeartDanger() => OnHeartDanger?.Invoke();
        public static void AbilityUsed() => OnAbilityUsed?.Invoke();
        public static void InteractPrompt(string s) => OnInteractPrompt?.Invoke(s);
        public static void PuzzleSolved() => OnPuzzleSolved?.Invoke();
        public static void SetBossBar(float f) => OnSetBossBar?.Invoke(f);
        public static void BossDefeated() => OnBossDefeated?.Invoke();
        public static void Subtitle(string s, float d) => OnSubtitle?.Invoke(s, d);

        // Limpia todos los suscriptores entre cargas de escena.
        public static void ResetAll()
        {
            OnScoreAdded = null;
            OnNovaSpeak = null;
            OnObjective = null;
            OnPickedInfo = null;
            OnCollectedChanged = null;
            OnHeartDanger = null;
            OnAbilityUsed = null;
            OnInteractPrompt = null;
            OnPuzzleSolved = null;
            OnSetBossBar = null;
            OnBossDefeated = null;
            OnSubtitle = null;
        }
    }

    public static class Missions
    {
        public const int MWake = 0;
        public const int MExplore = 1;
        public const int MInfo = 2;
        public const int MDoors = 3;
        public const int MBoss = 4;
        public const int MEscape = 5;

        public static readonly string[] Names =
        {
            "Despierta en el laboratorio",
            "Explora las instalaciones",
            "Encuentra información sobre el año 3000 [" + EiraConst.InfoLogsNeeded + "]",
            "Activa las puertas para avanzar",
            "Enfréntate a la Máquina Guardiana",
            "Escapa del laboratorio con NOVA",
        };
    }

    public enum GameState { Playing, Dead, GameOver, Won }

    public static class Points
    {
        public const int Info = 50;
        public const int Puzzle = 100;
        public const int Deactivate = 100;
        public const int AvoidEnemy = 150;
        public const int Mission = 500;
    }
}