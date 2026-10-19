using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Archivo del Proyecto Eira. Eira lo lee con E: suma
    /// <see cref="Level2Points.File"/> y avanza la investigacion. Uno de ellos
    /// es el Nucleo Central, que hay que encontrar para ganar.
    /// </summary>
    public class ProjectFile : Interactable
    {
        public int scoreOnRead = Level2Points.File;
        [TextArea(3, 6)] public string content = "Archivo del Proyecto Eira.";
        public bool isNucleoCentral;

        bool read;

        void Start()
        {
            promptText = isNucleoCentral
                ? "E - Acceder al Nucleo Central"
                : "E - Leer archivo del Proyecto Eira";

            if (transform.childCount > 0)
                transform.GetChild(0).localScale = Vector3.one * 0.9f;
        }

        void Update()
        {
            if (read) return;
            if (transform.childCount > 0)
                transform.GetChild(0).Rotate(0f, 45f * Time.deltaTime, 0f);
        }

        public override bool CanInteract(PlayerController p) => !read;

        public override void OnInteract(PlayerController p)
        {
            if (read) return;
            read = true;
            usable = false;

            var gm = GameManager.Instance;
            if (gm != null && !isNucleoCentral)
                gm.AddScore(scoreOnRead);

            AudioFX.Info();
            GameEvents.PickedInfo(content);

            if (Level2Director.Instance != null)
            {
                if (isNucleoCentral)
                    Level2Director.Instance.OnNucleoFound();
                else
                    Level2Director.Instance.OnProjectFileRead();
            }

            if (transform.childCount > 0)
                transform.GetChild(0).gameObject.SetActive(false);
            EnvOrb.Spawn(transform.position + Vector3.up, new Color(0.6f, 0.9f, 1f));
        }
    }
}
