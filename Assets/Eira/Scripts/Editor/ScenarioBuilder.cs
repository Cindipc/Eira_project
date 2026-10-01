#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EiraGame.EditorTools
{
    public class ScenarioBuilder : EditorWindow
    {
        private GameObject scenarioRoot;

        private bool clearPreviousBuild = true;
        private bool createFloorColliders = true;
        private bool createWallColliders = true;
        private bool createObstacleColliders = true;
        private bool createCameraColliders = true;

        private float floorThickness = 0.25f;
        private float floorPadding = 0.05f;

        private Vector2 scroll;

        [MenuItem("EiraGame/Escenario/Constructor Profesional")]
        public static void Open()
        {
            GetWindow<ScenarioBuilder>(
                "Constructor de Escenario"
            );
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "CONSTRUCTOR DE ESCENARIO PROFESIONAL",
                EditorStyles.boldLabel
            );

            EditorGUILayout.Space(5);

            EditorGUILayout.HelpBox(
                "Esta herramienta trabaja sobre el escenario actual. " +
                "No sustituye tus modelos ni modifica visualmente la geometría.",
                MessageType.Info
            );

            EditorGUILayout.Space(10);

            scenarioRoot = (GameObject)EditorGUILayout.ObjectField(
                "Escenario",
                scenarioRoot,
                typeof(GameObject),
                true
            );

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField(
                "Construcción física",
                EditorStyles.boldLabel
            );

            clearPreviousBuild = EditorGUILayout.Toggle(
                "Limpiar construcción anterior",
                clearPreviousBuild
            );

            createFloorColliders = EditorGUILayout.Toggle(
                "Colisión de suelo",
                createFloorColliders
            );

            createWallColliders = EditorGUILayout.Toggle(
                "Colisión de muros",
                createWallColliders
            );

            createObstacleColliders = EditorGUILayout.Toggle(
                "Colisión de obstáculos",
                createObstacleColliders
            );

            createCameraColliders = EditorGUILayout.Toggle(
                "Colisión para cámara",
                createCameraColliders
            );

            EditorGUILayout.Space(10);

            floorThickness = EditorGUILayout.FloatField(
                "Grosor del suelo",
                floorThickness
            );

            floorPadding = EditorGUILayout.FloatField(
                "Margen del suelo",
                floorPadding
            );

            EditorGUILayout.Space(20);

            GUI.backgroundColor = new Color(
                0.25f,
                0.75f,
                0.45f
            );

            if (GUILayout.Button(
                "CONSTRUIR ESCENARIO PROFESIONAL",
                GUILayout.Height(55)
            ))
            {
                BuildScenario();
            }

            GUI.backgroundColor = Color.white;

            EditorGUILayout.Space(10);

            if (GUILayout.Button(
                "ELIMINAR CONSTRUCCIÓN GENERADA",
                GUILayout.Height(35)
            ))
            {
                DeleteGenerated();
            }

            EditorGUILayout.Space(15);

            EditorGUILayout.HelpBox(
                "Después de construir, prueba Eira en Play Mode. " +
                "Los muros deben detener al CharacterController y " +
                "el suelo debe permanecer estable.",
                MessageType.Warning
            );

            EditorGUILayout.EndScrollView();
        }

        private void BuildScenario()
        {
            if (scenarioRoot == null)
            {
                EditorUtility.DisplayDialog(
                    "Escenario no seleccionado",
                    "Arrastra el objeto padre de tu escenario al campo Escenario.",
                    "Aceptar"
                );

                return;
            }

            Undo.IncrementCurrentGroup();

            DeleteGenerated();

            GameObject collisionRoot =
                new GameObject("__SCENARIO_PHYSICS");

            Undo.RegisterCreatedObjectUndo(
                collisionRoot,
                "Crear física del escenario"
            );

            collisionRoot.transform.SetParent(
                scenarioRoot.transform,
                false
            );

            GameObject floorRoot =
                CreateFolder(
                    collisionRoot,
                    "SUELO"
                );

            GameObject wallsRoot =
                CreateFolder(
                    collisionRoot,
                    "MUROS"
                );

            GameObject obstaclesRoot =
                CreateFolder(
                    collisionRoot,
                    "OBSTACULOS"
                );

            GameObject cameraRoot =
                CreateFolder(
                    collisionRoot,
                    "CAMERA_OBSTACLES"
                );

            List<Transform> objects =
                CollectGeometry(scenarioRoot.transform);

            int floorCount = 0;
            int wallCount = 0;
            int obstacleCount = 0;

            foreach (Transform t in objects)
            {
                if (t == scenarioRoot.transform)
                    continue;

                if (t.name.StartsWith("__"))
                    continue;

                MeshFilter mesh =
                    t.GetComponent<MeshFilter>();

                Renderer renderer =
                    t.GetComponent<Renderer>();

                if (mesh == null ||
                    mesh.sharedMesh == null ||
                    renderer == null)
                {
                    continue;
                }

                string name =
                    t.name.ToLowerInvariant();

                Bounds bounds =
                    renderer.bounds;

                bool isFloor =
                    IsFloor(name, bounds);

                bool isWall =
                    IsWall(name, bounds);

                bool isObstacle =
                    IsObstacle(name, bounds);

                if (isFloor && createFloorColliders)
                {
                    CreateMeshCollider(
                        t,
                        floorRoot,
                        "Floor_" + floorCount++
                    );
                }
                else if (isWall && createWallColliders)
                {
                    GameObject wall =
                        CreateMeshCollider(
                            t,
                            wallsRoot,
                            "Wall_" + wallCount++
                        );

                    if (createCameraColliders)
                    {
                        CreateCameraCollider(
                            wall,
                            cameraRoot
                        );
                    }
                }
                else if (isObstacle &&
                         createObstacleColliders)
                {
                    GameObject obstacle =
                        CreateMeshCollider(
                            t,
                            obstaclesRoot,
                            "Obstacle_" + obstacleCount++
                        );

                    if (createCameraColliders)
                    {
                        CreateCameraCollider(
                            obstacle,
                            cameraRoot
                        );
                    }
                }
            }

            CreateSafetyFloor(
                scenarioRoot,
                floorRoot
            );

            ConfigureLayers(
                collisionRoot
            );

            Selection.activeGameObject =
                collisionRoot;

            EditorUtility.SetDirty(
                scenarioRoot
            );

            Undo.CollapseUndoOperations(
                Undo.GetCurrentGroup()
            );

            EditorUtility.DisplayDialog(
                "ESCENARIO CONSTRUIDO",
                "Construcción terminada.\n\n" +
                "Suelo: " + floorCount + "\n" +
                "Muros: " + wallCount + "\n" +
                "Obstáculos: " + obstacleCount + "\n\n" +
                "La física se creó dentro de:\n" +
                "__SCENARIO_PHYSICS",
                "Perfecto"
            );
        }

        private List<Transform> CollectGeometry(
            Transform root
        )
        {
            List<Transform> result =
                new List<Transform>();

            MeshFilter[] meshes =
                root.GetComponentsInChildren<MeshFilter>(
                    true
                );

            foreach (MeshFilter mesh in meshes)
            {
                if (mesh.sharedMesh != null)
                    result.Add(mesh.transform);
            }

            return result;
        }

        private bool IsFloor(
            string name,
            Bounds bounds
        )
        {
            if (name.Contains("floor") ||
                name.Contains("suelo") ||
                name.Contains("ground") ||
                name.Contains("platform") ||
                name.Contains("plataforma") ||
                name.Contains("road") ||
                name.Contains("street") ||
                name.Contains("piso"))
            {
                return true;
            }

            float width = bounds.size.x;
            float depth = bounds.size.z;
            float height = bounds.size.y;

            return height < 0.5f &&
                   width > 1.0f &&
                   depth > 1.0f;
        }

        private bool IsWall(
            string name,
            Bounds bounds
        )
        {
            if (name.Contains("wall") ||
                name.Contains("muro") ||
                name.Contains("pared") ||
                name.Contains("building") ||
                name.Contains("edificio") ||
                name.Contains("barrier") ||
                name.Contains("barrera"))
            {
                return true;
            }

            float width = bounds.size.x;
            float depth = bounds.size.z;
            float height = bounds.size.y;

            return height > 1.5f &&
                   (width > 0.5f || depth > 0.5f);
        }

        private bool IsObstacle(
            string name,
            Bounds bounds
        )
        {
            if (name.Contains("obstacle") ||
                name.Contains("obstaculo") ||
                name.Contains("obstáculo") ||
                name.Contains("barrel") ||
                name.Contains("crate") ||
                name.Contains("box") ||
                name.Contains("machine") ||
                name.Contains("maquina"))
            {
                return true;
            }

            return bounds.size.y > 0.5f &&
                   bounds.size.y < 1.8f;
        }

        private GameObject CreateMeshCollider(
            Transform source,
            GameObject parent,
            string objectName
        )
        {
            GameObject obj =
                new GameObject(objectName);

            Undo.RegisterCreatedObjectUndo(
                obj,
                "Crear collider"
            );

            obj.transform.SetParent(
                parent.transform,
                true
            );

            MeshFilter sourceMesh =
                source.GetComponent<MeshFilter>();

            MeshCollider collider =
                obj.AddComponent<MeshCollider>();

            collider.sharedMesh =
                sourceMesh.sharedMesh;

            collider.convex = false;

            return obj;
        }

        private void CreateCameraCollider(
            GameObject source,
            GameObject cameraRoot
        )
        {
            Bounds bounds =
                CalculateBounds(source);

            GameObject cameraObject =
                new GameObject(
                    source.name + "_Camera"
                );

            Undo.RegisterCreatedObjectUndo(
                cameraObject,
                "Crear obstáculo de cámara"
            );

            cameraObject.transform.SetParent(
                cameraRoot.transform,
                true
            );

            cameraObject.transform.position =
                bounds.center;

            BoxCollider box =
                cameraObject.AddComponent<BoxCollider>();

            box.size =
                bounds.size +
                Vector3.one * 0.05f;
        }

        private Bounds CalculateBounds(
            GameObject obj
        )
        {
            Renderer[] renderers =
                obj.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
                return new Bounds(
                    obj.transform.position,
                    Vector3.one
                );

            Bounds bounds =
                renderers[0].bounds;

            for (int i = 1;
                 i < renderers.Length;
                 i++)
            {
                bounds.Encapsulate(
                    renderers[i].bounds
                );
            }

            return bounds;
        }

        private void CreateSafetyFloor(
            GameObject scenario,
            GameObject floorRoot
        )
        {
            Bounds bounds =
                CalculateScenarioBounds(
                    scenario
                );

            if (bounds.size.x <= 0 ||
                bounds.size.z <= 0)
            {
                return;
            }

            GameObject safety =
                new GameObject(
                    "SafetyFloor"
                );

            Undo.RegisterCreatedObjectUndo(
                safety,
                "Crear suelo de seguridad"
            );

            safety.transform.SetParent(
                floorRoot.transform,
                true
            );

            float y =
                bounds.min.y -
                floorThickness * 0.5f;

            safety.transform.position =
                new Vector3(
                    bounds.center.x,
                    y,
                    bounds.center.z
                );

            BoxCollider collider =
                safety.AddComponent<BoxCollider>();

            collider.size =
                new Vector3(
                    bounds.size.x +
                    floorPadding * 2f,

                    floorThickness,

                    bounds.size.z +
                    floorPadding * 2f
                );

            int layer =
                LayerMask.NameToLayer(
                    "ScenarioFloor"
                );

            if (layer >= 0)
                safety.layer = layer;
        }

        private Bounds CalculateScenarioBounds(
            GameObject root
        )
        {
            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>();

            if (renderers.Length == 0)
            {
                return new Bounds(
                    root.transform.position,
                    Vector3.zero
                );
            }

            Bounds bounds =
                renderers[0].bounds;

            foreach (Renderer renderer
                     in renderers)
            {
                bounds.Encapsulate(
                    renderer.bounds
                );
            }

            return bounds;
        }

        private GameObject CreateFolder(
            GameObject parent,
            string name
        )
        {
            GameObject folder =
                new GameObject(name);

            Undo.RegisterCreatedObjectUndo(
                folder,
                "Crear carpeta de escenario"
            );

            folder.transform.SetParent(
                parent.transform,
                false
            );

            return folder;
        }

        private void ConfigureLayers(
            GameObject root
        )
        {
            SetLayerRecursively(
                root,
                "ScenarioObstacle"
            );

            Transform floor =
                root.transform.Find(
                    "SUELO"
                );

            if (floor != null)
            {
                SetLayerRecursively(
                    floor.gameObject,
                    "ScenarioFloor"
                );
            }

            Transform walls =
                root.transform.Find(
                    "MUROS"
                );

            if (walls != null)
            {
                SetLayerRecursively(
                    walls.gameObject,
                    "ScenarioWall"
                );
            }

            Transform obstacles =
                root.transform.Find(
                    "OBSTACULOS"
                );

            if (obstacles != null)
            {
                SetLayerRecursively(
                    obstacles.gameObject,
                    "ScenarioObstacle"
                );
            }

            Transform camera =
                root.transform.Find(
                    "CAMERA_OBSTACLES"
                );

            if (camera != null)
            {
                SetLayerRecursively(
                    camera.gameObject,
                    "CameraObstacle"
                );
            }
        }

        private void SetLayerRecursively(
            GameObject obj,
            string layerName
        )
        {
            int layer =
                LayerMask.NameToLayer(
                    layerName
                );

            if (layer < 0)
                return;

            obj.layer = layer;

            foreach (Transform child
                     in obj.transform)
            {
                SetLayerRecursively(
                    child.gameObject,
                    layerName
                );
            }
        }

        private void DeleteGenerated()
        {
            if (scenarioRoot == null)
                return;

            Transform old =
                scenarioRoot.transform.Find(
                    "__SCENARIO_PHYSICS"
                );

            if (old == null)
                return;

            Undo.DestroyObjectImmediate(
                old.gameObject
            );
        }
    }
}

#endif