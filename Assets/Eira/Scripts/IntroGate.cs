using UnityEngine;
using UnityEngine.SceneManagement;

namespace EiraGame
{
    /// <summary>
    /// Encamina el arranque a la intro con video.
    ///
    /// El botón Play del Editor reproduce la escena que esté abierta en el
    /// Hierarchy, no la primera del Build Settings. Como Level1Scene es la
    /// que se abre siempre para trabajar, al darle Play se entraba
    /// directamente al nivel y la intro con video no se veía nunca.
    ///
    /// Solo actúa cuando esta es la PRIMERA escena de la sesión. Eso se
    /// comprueba contando las escenas ya cargadas, y no solo con un flag:
    ///
    ///   - En el Editor, Play sobre Level1Scene -> 0 escenas previas, salta
    ///     a la intro. Es justo lo que se quiere.
    ///   - En un build, el Build Settings ya empieza por Intro; cuando el
    ///     video acaba y carga el nivel ya hay 1 escena previa, asi que el
    ///     guard no hace nada y no se vuelve a la intro. Sin esta cuenta
    ///     seria un bucle infinito Intro -> Nivel -> Intro.
    ///
    /// "Reiniciar nivel" y "volver a jugar" tampoco se ven afectados: la
    /// intro ya se dio, y IntroVideo deja constancia con IntroPlayed.
    /// </summary>
    public class IntroGate : MonoBehaviour
    {
        /// <summary>
        /// La intro con video ya se ha visto en esta sesion. Lo marca
        /// IntroVideo al empezar, para que el nivel sepa que no debe
        /// devolver al jugador a la intro.
        /// </summary>
        public static bool IntroPlayed;

        static int scenesLoaded;

        /// <summary>
        /// SubsystemRegistration corre antes que cualquier escena y tambien
        /// cuando el Editor entra en Play sin recargar dominio, que es
        /// justo cuando un static "sucio" haria que el guard se saltase la
        /// intro en la segunda pulsacion.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            IntroPlayed = false;
            scenesLoaded = 0;

            SceneManager.sceneLoaded -= CountScene;
            SceneManager.sceneLoaded += CountScene;
        }

        static void CountScene(Scene scene, LoadSceneMode mode)
        {
            scenesLoaded++;
        }

        void Awake()
        {
            if (IntroPlayed)
                return;

            var scene = SceneManager.GetActiveScene();

            if (scene.name != EiraConst.Level1Scene)
                return;

            // Ya se habia cargado otra escena antes: el video ya sevio.
            if (scenesLoaded > 0)
                return;

            IntroPlayed = true;

            Debug.Log(
                "[Eira] IntroGate: Play abierto en " + scene.name +
                " -> " + EiraConst.IntroVideoScene
            );

            SceneManager.LoadScene(EiraConst.IntroVideoScene);
        }
    }
}