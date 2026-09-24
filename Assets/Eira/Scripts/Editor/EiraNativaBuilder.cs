using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    // Menú para hornear el modelo nativo de Eira (primitivas) en modo edición.
    public static class EiraNativaBuilder
    {
        [MenuItem("Eira/Construir Eira Nativa")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");

            var players = Object.FindObjectsOfType<PlayerController>();
            if (players.Length == 0)
            {
                Debug.LogError("No se encontró PlayerController en la escena.");
                return;
            }

            bool changed = false;
            foreach (var pc in players)
            {
                if (pc.visual == null) continue;
                if (EiraModelo.IsNativa(pc.visual)) continue;
                Undo.RegisterFullObjectHierarchyUndo(pc.visual.gameObject, "Construir Eira Nativa");
                EiraModelo.EnsureNativa(pc.visual, c => ModelFactory.Lit(c));
                pc.RefreshBones();
                changed = true;
            }

            if (changed)
            {
                EditorSceneManager.SaveScene(scene);
                Debug.Log("Eira nativa construida y escena guardada.");
            }
            else
            {
                Debug.Log("Eira ya es nativa. Nada que hacer.");
            }
        }
    }
}