using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FillFloorHoles : EditorWindow
{
    [MenuItem("Eira/Fill Floor Holes in Futuristic City")]
    public static void FillHoles()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Eira/Scenes/Level1Scene.unity");
        Debug.Log("=== FILLING FLOOR HOLES ===");

        var city = GameObject.Find("FuturisticCity_Environment");
        if (city == null) { Debug.LogError("City not found!"); return; }

        // Get all floor-like renderers in the city
        var renderers = city.GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log($"Found {renderers.Length} renderers in city");

        // Collect all floor triangles to understand walkable areas
        var floorMeshes = new System.Collections.Generic.List<MeshFilter>();
        foreach (var r in renderers)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                // Check if this mesh is roughly horizontal (floor-like)
                var bounds = mf.sharedMesh.bounds;
                var lossyScale = mf.transform.lossyScale;
                float thickness = bounds.size.y * lossyScale.y;
                float area = bounds.size.x * lossyScale.x * bounds.size.z * lossyScale.z;
                
                // Floor-like: flat (thin) and wide
                if (thickness < 2f && area > 4f)
                {
                    floorMeshes.Add(mf);
                }
            }
        }
        Debug.Log($"Found {floorMeshes.Count} floor-like meshes");

        // Create a comprehensive floor collider system
        // Approach: Create a large flat plane at the average floor height, or
        // Generate colliders for each floor mesh that doesn't have one

        int collidersAdded = 0;
        int holesFilled = 0;

        // Method 1: Ensure all floor meshes have MeshColliders
        foreach (var mf in floorMeshes)
        {
            if (mf.GetComponent<MeshCollider>() == null)
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.convex = false;
                mc.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation;
                collidersAdded++;
            }
        }

        // Method 2: Detect gaps by raycasting a grid and fill them
        holesFilled = DetectAndFillGaps(city, floorMeshes);

        // Method 3: Create a unified ground plane at the lowest walkable level
        // CreateGroundPlane(city, floorMeshes);

        Debug.Log($"Added {collidersAdded} MeshColliders, filled {holesFilled} gaps");
        
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        
        Debug.Log("=== FLOOR HOLES FILLED ===");
    }

    static int DetectAndFillGaps(GameObject city, System.Collections.Generic.List<MeshFilter> floorMeshes)
    {
        // Find the bounding box of all floors
        Bounds totalBounds = new Bounds();
        bool first = true;
        float avgFloorY = 0f;
        int floorCount = 0;

        foreach (var mf in floorMeshes)
        {
            var wsBounds = GetWorldBounds(mf);
            if (first) { totalBounds = wsBounds; first = false; }
            else totalBounds.Encapsulate(wsBounds);
            
            avgFloorY += mf.transform.position.y;
            floorCount++;
        }
        avgFloorY /= Mathf.Max(1, floorCount);

        Debug.Log($"Floor bounds: {totalBounds}, avg Y: {avgFloorY}");

        // Raycast a grid to find gaps
        float gridSize = 2f; // 2m grid
        int fillCount = 0;

        int xSteps = Mathf.CeilToInt(totalBounds.size.x / gridSize);
        int zSteps = Mathf.CeilToInt(totalBounds.size.z / gridSize);

        // Limit grid to reasonable size
        xSteps = Mathf.Clamp(xSteps, 5, 50);
        zSteps = Mathf.Clamp(zSteps, 5, 50);

        Vector3 start = totalBounds.min;
        
        for (int x = 0; x < xSteps; x++)
        {
            for (int z = 0; z < zSteps; z++)
            {
                Vector3 testPos = start + new Vector3(x * gridSize, totalBounds.max.y + 10f, z * gridSize);
                
                // Raycast down
                if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hit, 50f))
                {
                    // Check if hit is city geometry
                    if (hit.transform.IsChildOf(city.transform))
                    {
                        // Good - there's floor here
                    }
                    else
                    {
                        // Gap! Check if we should fill it
                        // Only fill if it's near other floors (within 5m horizontally)
                        bool nearFloor = false;
                        var nearby = Physics.OverlapSphere(hit.point + Vector3.up * 0.1f, 5f);
                        foreach (var c in nearby)
                        {
                            if (c.transform.IsChildOf(city.transform))
                            {
                                var mf = c.GetComponent<MeshFilter>();
                                if (mf != null)
                                {
                                    var bounds = mf.sharedMesh.bounds;
                                    var lossy = mf.transform.lossyScale;
                                    if (bounds.size.y * lossy.y < 2f) // flat
                                    {
                                        nearFloor = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (nearFloor)
                        {
                            // Create a small floor patch
                            CreateFloorPatch(hit.point, gridSize * 0.9f, city.transform);
                            fillCount++;
                        }
                    }
                }
            }
        }

        return fillCount;
    }

    static Bounds GetWorldBounds(MeshFilter mf)
    {
        var bounds = mf.sharedMesh.bounds;
        var t = mf.transform;
        Vector3 center = t.TransformPoint(bounds.center);
        Vector3 extents = Vector3.Scale(bounds.extents, t.lossyScale);
        return new Bounds(center, extents * 2f);
    }

    static void CreateFloorPatch(Vector3 position, float size, Transform parent)
    {
        var go = new GameObject("FloorPatch_" + position.x.ToString("F0") + "_" + position.z.ToString("F0"));
        go.transform.position = position;
        go.transform.SetParent(parent, true);
        
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        
        // Create a simple quad mesh
        Mesh mesh = new Mesh();
        float h = size * 0.5f;
        mesh.vertices = new Vector3[] {
            new Vector3(-h, 0, -h), new Vector3(h, 0, -h),
            new Vector3(-h, 0, h), new Vector3(h, 0, h)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        mesh.uv = new Vector2[] {
            new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1)
        };
        mesh.normals = new Vector3[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
        mesh.RecalculateBounds();
        mf.mesh = mesh;

        // Invisible but collidable material
        mr.material = new Material(Shader.Find("Standard")) { color = new Color(0,0,0,0) };
        mr.enabled = false; // invisible
        
        var mc = go.AddComponent<MeshCollider>();
        mc.convex = false;
    }
}