using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MoveToForest
{
    const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
    
    [MenuItem("Tools/MoveToForest")]
    static void Do()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Physics.SyncTransforms();
        
        var eira = GameObject.Find("Eira");
        var nova = GameObject.Find("NOVA");
        var gameplay = GameObject.Find("Gameplay");
        var piso = GameObject.Find("PisoBosque");
        var rampa = GameObject.Find("RampaBosque");
        var paisaje = GameObject.Find("PaisajeNivel1_Environment");
        var lab = GameObject.Find("LabAbandonado");
        
        if (piso == null) { Debug.LogError("Falta PisoBosque"); return; }
        if (gameplay == null) { Debug.LogError("Falta Gameplay"); return; }
        
        // Lights to move with the group
        var lights = new List<GameObject>();
        foreach (var name in new[] { "CyanPuzzle", "RedBoss", "GreenExit", "WarmSpawn" })
        {
            var go = GameObject.Find(name);
            if (go != null && go.transform.parent == null) lights.Add(go);
        }
        
        Vector3 delta = piso.transform.position - gameplay.transform.position;
        delta.y = piso.transform.position.y - gameplay.transform.position.y; // preserve vertical logic? but move as group
        // Better: move so that relative positions stay same; use delta from gameplay to piso position? but piso at -7.95, gameplay at -0.5 -> delta.y = -7.45
        delta = new Vector3(piso.transform.position.x - gameplay.transform.position.x, piso.transform.position.y - gameplay.transform.position.y, piso.transform.position.z - gameplay.transform.position.z);
        
        Debug.Log("DELTA=" + delta);
        
        // Move group
        if (eira != null) eira.transform.position += delta;
        if (nova != null) nova.transform.position += delta;
        gameplay.transform.position += delta;
        foreach (var l in lights) l.transform.position += delta;
        
        // Raycast down to sit on PisoBosque/RampaBosque
        SitOnFloor(eira, piso, rampa);
        SitOnFloor(nova, piso, rampa);
        SitOnFloor(gameplay, piso, rampa);
        foreach (var l in lights) SitOnFloor(l, piso, rampa);
        
        // Also sit each direct child of Gameplay? but moving whole transform is enough; raycasting children keeps them on geometry
        foreach (Transform c in gameplay.transform)
        {
            SitOnFloor(c.gameObject, piso, rampa);
        }
        
        // Camera behind Eira
        var cam = Object.FindAnyObjectByType<EiraGame.FirstPersonCamera>();
        var pc = Object.FindAnyObjectByType<EiraGame.PlayerController>();
        if (cam != null && eira != null)
        {
            cam.Yaw = eira.transform.eulerAngles.y;
            cam.SnapBehind();
        }
        if (pc != null && eira != null)
        {
            pc.transform.rotation = eira.transform.rotation;
        }
        
        // Disable LabAbandonado if it obscures view
        if (lab != null)
        {
            lab.SetActive(false);
        }
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        
        Debug.Log("FINAL Eira=" + (eira != null ? eira.transform.position.ToString() : "null") + " delta=" + delta);
    }
    
    static void SitOnFloor(GameObject go, GameObject piso, GameObject rampa)
    {
        if (go == null) return;
        Vector3 p = go.transform.position;
        float startY = p.y + 50f;
        float dist = 200f;
        RaycastHit[] hits = Physics.RaycastAll(new Vector3(p.x, startY, p.z), Vector3.down, dist);
        float bestY = float.NaN;
        foreach (var h in hits)
        {
            if (h.collider == null) continue;
            var root = h.collider.transform.root;
            if (root == piso.transform.root || root == rampa.transform.root || h.collider.gameObject == piso || h.collider.gameObject == rampa || h.collider.transform.IsChildOf(piso.transform) || h.collider.transform.IsChildOf(rampa.transform))
            {
                if (float.IsNaN(bestY) || h.point.y > bestY) bestY = h.point.y;
            }
        }
        if (!float.IsNaN(bestY))
        {
            go.transform.position = new Vector3(p.x, bestY + 0.01f, p.z);
        }
    }
}
