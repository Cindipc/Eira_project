using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Pieza tecnologica suelta por la ciudad. Eira la recupera con E y suma
    /// <see cref="Level2Points.Hack"/>.
    /// </summary>
    public class TechPiece : Interactable
    {
        public int scoreOnCollect = Level2Points.Hack;

        bool taken;
        Light glow;

        void Start()
        {
            promptText = "E - Recoger pieza tecnologica";
            glow = GetComponentInChildren<Light>();
        }

        void Update()
        {
            transform.Rotate(0f, 70f * Time.deltaTime, 0f, Space.World);
            if (glow != null)
                glow.intensity = 1.2f + Mathf.Sin(Time.time * 3f) * 0.5f;
        }

        public override bool CanInteract(PlayerController p) => !taken;

        public override void OnInteract(PlayerController p)
        {
            if (taken) return;
            taken = true;
            usable = false;

            var gm = GameManager.Instance;
            if (gm != null) gm.AddScore(scoreOnCollect);

            AudioFX.Pickup();
            EnvOrb.Spawn(transform.position, new Color(0.4f, 0.8f, 1f));

            if (Level2Director.Instance != null)
                Level2Director.Instance.OnTechPieceCollected();

            Destroy(gameObject, 0.05f);
        }
    }
}
