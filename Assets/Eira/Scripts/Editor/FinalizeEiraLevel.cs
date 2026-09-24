using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    // Finalización del Nivel 1:
    //  1) Importa base.obj como el modelo de Eira (reemplaza al GLB anterior).
    //  2) Iluminación de "tarde" (sol direccional cálido + ambiente cálido).
    //  3) Piso de relleno al nivel de la arena para tapar huecos por donde caería Eira.
    //  4) Guarda la escena y loguea un resumen.
    public static class FinalizeEiraLevel
    {
        const string ObjPath = @"C:\Users\Usuario\Downloads\eira_para_mixamo_limpio.obj";
        const string Folder = "Assets/Eira/Environment/CharacterMeshes";
        const string MeshPath = Folder + "/EiraMesh.asset";
        const string MatPath = Folder + "/EiraVertexColor.mat";
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";
        const float FillFloorY = 0.09f;         // nivel del piso del escenario en coords de escena (igual a BaseY)
        const float MapScale = 0.1574658f;
        const float BaseY = 0.09f;              // y del suelo del mapa en mundo (esc y=-0.5 + local 0.59)

        [MenuItem("Eira/Finalize Level")]
        public static void Do()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);

            // ---- 1) Personaje base.obj ----
            Mesh mesh = BuildObjMesh(ObjPath);
            if (mesh == null) { Debug.LogError("FinalizeEiraLevel: fallo leyendo base.obj"); return; }

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.DeleteAsset(MatPath);

            AssetDatabase.CreateAsset(mesh, MeshPath);

            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) sh = Shader.Find("Standard");
            var mat = new Material(sh) { name = "EiraMaterial" };
            mat.SetColor("_BaseColor", new Color(0.76f, 0.76f, 0.79f, 1f));
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.25f);
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var eira = GameObject.Find("Eira");
            if (eira == null) { Debug.LogError("FinalizeEiraLevel: no 'Eira'"); return; }
            var vis = eira.transform.Find("EiraVisual");
            if (vis == null) { Debug.LogError("FinalizeEiraLevel: no 'EiraVisual'"); return; }
            var mf = vis.GetComponent<MeshFilter>();
            if (mf == null) mf = vis.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = vis.GetComponent<MeshRenderer>();
            if (mr == null) mr = vis.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            vis.localPosition = Vector3.zero;
            vis.localRotation = Quaternion.identity;
            vis.localScale = Vector3.one;

            // ---- 2) Iluminación de tarde ----
            var sun = GameObject.Find("Sun");
            if (sun != null) sun.SetActive(false);

            var sunTarde = new GameObject("SunTarde", typeof(Light));
            var dl = sunTarde.GetComponent<Light>();
            dl.type = LightType.Directional;
            dl.color = new Color(1f, 0.82f, 0.62f, 1f);
            dl.intensity = 1.15f;
            dl.shadows = LightShadows.Soft;
            dl.shadowStrength = 0.85f;
            sunTarde.transform.rotation = Quaternion.Euler(18f, -25f, 0f);
            sunTarde.transform.position = Vector3.zero;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(1f, 0.8f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.55f, 0.45f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.12f, 0.08f);

            // ---- 3) Piso de relleno (tapa huecos al nivel de la arena) ----
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "HuecosFillFloor";
            floor.transform.position = new Vector3(0f, FillFloorY, 0f);
            floor.transform.rotation = Quaternion.identity;
            floor.transform.localScale = new Vector3(10.4f, 1f, 6.6f);
            var fr = floor.GetComponent<MeshRenderer>();
            fr.sharedMaterial = mat != null ? MakeFlatMaterial(sh, new Color(0.07f, 0.08f, 0.1f)) : null;
            var fc = floor.GetComponent<MeshCollider>();
            if (fc == null) floor.AddComponent<MeshCollider>();
            floor.isStatic = true;

            EditorSceneManager.SaveScene(scene);

            Debug.Log(string.Format(
                "FinalizeEiraLevel OK: personaje base.obj mesh {0} verts / {1} tris, bounds A={2} C={3}; " +
                "SunTarde creado; HuecosFillFloor en y={4} (escala 10.4x6.6); mascara mapa {5}",
                mesh.vertexCount, mesh.triangles.Length / 3, mesh.bounds.size, mesh.bounds.center,
                FillFloorY.ToString("0.00"), (BaseY).ToString("0.00")));
        }

        static Material MakeFlatMaterial(Shader sh, Color c)
        {
            var m = new Material(sh) { name = "Fill" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", 0f);
            return m;
        }

        // ---- parser OBJ (v / vn / vt / f con triangulación en abanico) ----
        static Mesh BuildObjMesh(string path)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            var outP = new List<Vector3>();
            var outN = new List<Vector3>();
            var outU = new List<Vector2>();
            var outI = new List<int>();

            bool hasN = false;
            long faceCount = 0;

            using (var sr = new StreamReader(path))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    char c = line[0];
                    if (c == 'v')
                    {
                        if (line.Length > 1 && line[1] == ' ')
                        {
                            var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            float x, y, z;
                            if (p.Length >= 4 && float.TryParse(p[1], out x) && float.TryParse(p[2], out y) && float.TryParse(p[3], out z))
                                positions.Add(new Vector3(x, y, z));
                        }
                        else if (line.Length > 1 && line[1] == 'n')
                        {
                            var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            float x, y, z;
                            if (p.Length >= 4 && float.TryParse(p[1], out x) && float.TryParse(p[2], out y) && float.TryParse(p[3], out z))
                                normals.Add(new Vector3(x, y, z));
                        }
                        else if (line.Length > 1 && line[1] == 't')
                        {
                            var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            float u, v;
                            if (p.Length >= 3 && float.TryParse(p[1], out u) && float.TryParse(p[2], out v))
                                uvs.Add(new Vector2(u, v));
                        }
                    }
                    else if (c == 'f')
                    {
                        var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (p.Length < 4) continue;
                        var vi = new int[p.Length - 1];
                        var ni = new int[p.Length - 1];
                        var ti = new int[p.Length - 1];
                        bool ok = true;
                        for (int k = 0; k < vi.Length; k++)
                        {
                            var tok = p[k + 1].Split('/');
                            if (!int.TryParse(tok[0], out vi[k])) { ok = false; break; }
                            ni[k] = tok.Length >= 3 && tok[2].Length > 0 && int.TryParse(tok[2], out int x) ? x : -1;
                            ti[k] = tok.Length >= 2 && tok[1].Length > 0 && int.TryParse(tok[1], out int y) ? y : -1;
                            if (vi[k] < 0) vi[k] += positions.Count + 1;
                            if (ni[k] < 0) ni[k] += normals.Count + 1;
                            if (ti[k] < 0) ti[k] += uvs.Count + 1;
                        }
                        if (!ok) continue;

                        int baseCount = vi.Length;
                        int vertBase = outP.Count;
                        for (int k = 0; k < baseCount; k++)
                        {
                            int v = vi[k] - 1;
                            Vector3 pos = positions[v];
                            outP.Add(pos);
                            if (ni[k] > 0 && ni[k] - 1 < normals.Count)
                            {
                                var n = normals[ni[k] - 1];
                                outN.Add(new Vector3(-n.x, n.y, n.z));
                                hasN = true;
                            }
                            else
                            {
                                outN.Add(Vector3.up);
                            }
                            if (ti[k] > 0 && ti[k] - 1 < uvs.Count) outU.Add(uvs[ti[k] - 1]);
                            else outU.Add(Vector2.zero);
                        }
                        for (int k = 1; k + 1 < baseCount; k++)
                        {
                            outI.Add(vertBase + 0);
                            outI.Add(vertBase + k);
                            outI.Add(vertBase + k + 1);
                        }
                        faceCount++;
                    }
                }
            }

            if (outP.Count == 0 || outI.Count == 0) return null;

            // espejo X de posiciones (misma convención que el mapa)
            for (int i = 0; i < outP.Count; i++) { var pp = outP[i]; outP[i] = new Vector3(-pp.x, pp.y, pp.z); }

            // pies en y = 0
            float minY = float.MaxValue;
            for (int i = 0; i < outP.Count; i++) if (outP[i].y < minY) minY = outP[i].y;
            if (minY < 0f) for (int i = 0; i < outP.Count; i++) outP[i] = outP[i] + new Vector3(0f, -minY, 0f);

            var m = new Mesh { name = "EiraObj" };
            m.SetVertices(outP);
            m.SetNormals(outN);
            m.SetUVs(0, outU);
            m.SetTriangles(outI, 0);
            if (!hasN) m.RecalculateNormals();
            m.RecalculateBounds();

            Debug.Log(string.Format("OBJ cargado: {0} caras, {1} vertices emitidos, minY shift {2}", faceCount, outP.Count, minY.ToString("0.000")));
            return m;
        }
    }
}