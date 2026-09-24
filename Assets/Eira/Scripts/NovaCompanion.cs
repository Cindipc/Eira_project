using UnityEngine;

namespace EiraGame
{
    // NOVA: compañera y guía. Sigue a Eira y ofrece consejos en los momentos clave.
    public class NovaCompanion : MonoBehaviour
    {
        public string[] idleTips =
        {
            "Los drones detectan mejor si corres. Agáchate (Ctrl) y usarás menos el radar.",
            "Si una máquina brilla en verde, puedes activarla con F.",
            "Tu corazón es tu energía: usa F con cuidado o te harás daño.",
            "Los botiquines y cápsulas de energía te restauran. Busca sus luces.",
        };

        [TextArea(3, 6)] public string linesOnInfo;
        [TextArea(2, 5)] public string lineOnPuzzle;
        [TextArea(2, 5)] public string lineOnBoss;
        [TextArea(2, 5)] public string lineOnBossEnd;
        [TextArea(2, 5)] public string lineOnWin;
        public float sayDuration = 4f;

        PlayerController player;
        int tipIndex;

        void Start()
        {
            var gm = GameManager.Instance;
            if (gm != null) player = gm.Player;
            GameEvents.OnPickedInfo += HandleInfo;
            GameEvents.OnPuzzleSolved += HandlePuzzle;
            GameEvents.OnBossDefeated += HandleBossEnd;
            GameEvents.OnSubtitle += OnSubtitleHook;
        }

        void OnDestroy()
        {
            GameEvents.OnPickedInfo -= HandleInfo;
            GameEvents.OnPuzzleSolved -= HandlePuzzle;
            GameEvents.OnBossDefeated -= HandleBossEnd;
            GameEvents.OnSubtitle -= OnSubtitleHook;
        }

        void OnSubtitleHook(string s, float d) { }

        void HandleInfo(string s)
        {
            if (GameManager.Instance != null && GameManager.Instance.InfoCollectedCount >= EiraConst.InfoLogsNeeded)
            {
                if (string.IsNullOrEmpty(linesOnInfo)) linesOnInfo = "Eso es todo. 3000... Los humanos llevan décadas desaparecidos. Vamos, la salida está por ahí.";
                GameEvents.NovaSpeak(linesOnInfo, sayDuration + 1f);
            }
        }

        void HandlePuzzle()
        {
            if (string.IsNullOrEmpty(lineOnPuzzle)) lineOnPuzzle = "¡Perfecto, Eira! La puerta está abierta.";
            GameEvents.NovaSpeak(lineOnPuzzle, sayDuration);
        }

        void HandleBossEnd()
        {
            if (string.IsNullOrEmpty(lineOnBossEnd)) lineOnBossEnd = "¡Lo lograste! La salida está despejada. ¡Corre!";
            GameEvents.NovaSpeak(lineOnBossEnd, sayDuration + 1f);
        }

        void Update()
        {
            player = GameManager.Instance != null ? GameManager.Instance.Player : null;
            if (player == null) return;

            var offset = -player.transform.forward * 2.0f + player.transform.right * 0.7f;
            offset.y = 0f;
            var target = player.transform.position + offset;
            var move = Vector3.Lerp(transform.position, target, 3.2f * Time.deltaTime);
            if (Mathf.Abs(move.y - target.y) > 0.01f) move.y = Mathf.Lerp(move.y, target.y, 3.2f * Time.deltaTime);
            transform.position = move;

            var look = Quaternion.LookRotation(new Vector3(player.transform.position.x - transform.position.x, 0f, player.transform.position.z - transform.position.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 5f * Time.deltaTime);
        }

        public void SayNextTip()
        {
            if (tipsDone) return;
            AudioFX.Beep();
            string tip = idleTips[tipIndex % idleTips.Length];
            tipIndex++;
            GameEvents.NovaSpeak(tip, sayDuration);
            if (tipIndex >= idleTips.Length) tipsDone = true;
        }

        public bool tipsDone { get; private set; }
    }
}