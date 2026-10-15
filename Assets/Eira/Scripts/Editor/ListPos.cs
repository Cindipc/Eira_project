using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ListPos
{
    [MenuItem("Tools/ListPos")]
    static void Do()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");
        var targets = new[] { "Eira", "NOVA", "Gameplay", "LabAbandonado", "PisoBosque", "RampaBosque", "PaisajeNivel1_Environment" };
        foreach (var t in targets)
        {
            var go = GameObject.Find(t);
            if (go == null) { Debug.Log(t + " NULL"); continue; }
            Debug.Log(t + " P=" + go.transform.position + " R=" + go.transform.rotation.eulerAngles + " S=" + go.transform.localScale);
        }
        EditorApplication.Exit(0);
    }
}
