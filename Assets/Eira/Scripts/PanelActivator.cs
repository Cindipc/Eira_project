using UnityEngine;

namespace EiraGame
{
    public enum PanelKind { PuzzlePanel, BossTerminal }

    // Consolas que Eira activa con su habilidad genética (F).
    public class PanelActivator : Interactable
    {
        public PanelKind kind = PanelKind.PuzzlePanel;
        public DoorControl linkedDoor;     // paneles de puzzle
        public GuardianBoss boss;          // terminales de jefe
        public int neededCharges = 1;
        public Renderer glowRenderer;
        public Color chargeColor = new Color(0.3f, 1f, 0.6f);

        int charges;
        bool activated;

        void Start()
        {
            promptText = kind == PanelKind.BossTerminal ? "F — Cargar terminal" : "F — Activar consola";
            if (glowRenderer == null && transform.childCount > 0)
                glowRenderer = transform.GetChild(0).GetComponent<Renderer>();
            Tint(0f, 1f, 0.6f);
        }

        void Tint(float r, float g, float b)
        {
            if (glowRenderer == null) return;
            var m = glowRenderer.material;
            m.color = new Color(r, g, b);
        }

        public override void OnAbilityPulse(PlayerController p)
        {
            if (activated || !usable) return;
            charges++;
            AudioFX.Ability();
            GameEvents.AbilityUsed();
            Tint(0.2f + charges * 0.2f, 1f, 0.6f);
            if (charges >= neededCharges)
            {
                activated = true;
                usable = false;
                if (kind == PanelKind.PuzzlePanel && linkedDoor != null)
                {
                    linkedDoor.RegisterActivation();
                    var gm = GameManager.Instance;
                    if (gm != null) gm.OnPuzzlePanelCharged();
                }
                else if (kind == PanelKind.BossTerminal && boss != null)
                {
                    var gm = GameManager.Instance;
                    if (gm != null) gm.OnBossTerminalCharged();
                    boss.OnTerminalCharged();
                }
            }
        }

        public void ForceActivate()
        {
            OnAbilityPulse(null);
        }
    }
}