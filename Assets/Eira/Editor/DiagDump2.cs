using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.EditorTools
{
    public static class DiagDump2
    {
        [MenuItem("Eira/** DiagDump2")]
        public static void Run()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity", OpenSceneMode.Single);
            foreach (var root in sc.GetRootGameObjects())
                Walk(root, 0);
        }

        static void Walk(GameObject go, int depth)
        {
            string info = "";
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                string tn = c.GetType().Name;
                if (tn == "LevelExitZone" || tn == "CheckpointZone" || tn == "GuardianBoss" ||
                    tn == "LevelExit" || tn == "MissionDirector" || tn.Contains("Mission") ||
                    tn == "PlayerSpawn" || tn.Contains("Spawn") || tn == "EnemyWave" ||
                    tn == "DroneController" || tn == "Generator") 
                    info += " [" + tn + "]";
            }
            if (info.Length > 0 || depth <= 1)
            {
                var p = go.transform.position;
                Debug.Log("[D2] " + new string('.', depth) + "'" + go.name + "' pos=(" + p.x.ToString("F1") + "," + p.y.ToString("F1") + "," + p.z.ToString("F1") + ")" + info);
            }
            foreach (Transform ch in go.transform)
                Walk(ch.gameObject, depth + 1);
        }
    }
}