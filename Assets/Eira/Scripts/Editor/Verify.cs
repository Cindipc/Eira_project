using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Verify
{
    const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
    
    [MenuItem("Tools/Verify")]
    static void Do()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var targets = new[] { "Eira", "NOVA", "Gameplay", "LabAbandonado", "PisoBosque", "RampaBosque", "PaisajeNivel1_Environment", "CyanPuzzle", "RedBoss", "GreenExit", "WarmSpawn" };
        foreach (var t in targets)
        {
            var go = GameObject.Find(t);
            if (go == null) { Debug.Log("NULL:" + t); continue; }
            Debug.Log(t + " P=" + go.transform.position + " active=" + go.activeSelf);
        }
        EditorApplication.Exit(0);
    }
}
