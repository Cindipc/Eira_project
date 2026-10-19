using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Rebelde atrapado por los androides. Eira lo libera con E y suma
    /// <see cref="Level2Points.Rebel"/>. Cuatro rebeldes liberados completan la
    /// mision secundaria de rescate.
    /// </summary>
    public class RebelAndroid : Interactable
    {
        public int scoreOnRescue = Level2Points.Rebel;
        [TextArea(2, 4)] public string rescueLine = "Gracias, Eira. Los rebeldes te esperan en el refugio del este.";

        public bool Rescued { get; private set; }

        Renderer glow;

        void Start()
        {
            promptText = "E - Liberar rebelde";
            glow = GetComponentInChildren<Renderer>();
        }

        void Update()
        {
            if (!Rescued && transform.childCount > 0)
                transform.GetChild(0).Rotate(0f, 50f * Time.deltaTime, 0f, Space.World);
        }

        public override bool CanInteract(PlayerController p) => !Rescued;

        public override void OnInteract(PlayerController p)
        {
            if (Rescued) return;
            Rescued = true;
            usable = false;

            var gm = GameManager.Instance;
            if (gm != null) gm.AddScore(scoreOnRescue);

            GameEvents.NovaSpeak(rescueLine, 4f);
            AudioFX.Info();
            EnvOrb.Spawn(transform.position + Vector3.up, new Color(0.4f, 1f, 0.6f));

            if (Level2Director.Instance != null)
                Level2Director.Instance.OnRebelRescued(this);

            if (glow != null)
                glow.material.color = new Color(0.3f, 0.9f, 0.5f);
        }
    }
}
