using UnityEngine;

namespace EiraGame
{
    /// <summary>
    /// Archivo secreto del Nucleo Central (Nivel 3). Eira lo lee con E y suma
    /// <see cref="Level3Points.SecretFile"/>.
    /// </summary>
    public class SecretFile : Interactable
    {
        public int scoreOnRead = Level3Points.SecretFile;
        [TextArea(3, 6)] public string content = "Archivo secreto del Proyecto Eira: ORIGEN fue el primer androide consciente.";

        bool read;

        void Start()
        {
            promptText = "E - Leer archivo secreto";
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
            if (gm != null) gm.AddScore(scoreOnRead);

            AudioFX.Info();
            GameEvents.PickedInfo(content);

            if (Level3Director.Instance != null)
                Level3Director.Instance.OnSecretFile(this);

            if (transform.childCount > 0)
                transform.GetChild(0).gameObject.SetActive(false);
            EnvOrb.Spawn(transform.position + Vector3.up, new Color(0.8f, 0.6f, 1f));
        }
    }
}
