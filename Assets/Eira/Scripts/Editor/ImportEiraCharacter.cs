using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EiraGame.Editor
{
    // Importa el nuevo personaje de Eira (GLB estatico con vertices coloreados),
    // crea mesh+material como assets y lo conecta a EiraVisual en Level1Scene,
    // eliminando el cuerpo placeholder de primitivas.
    public static class ImportEiraCharacter
    {
        const string GlbPath = @"C:\Users\Usuario\Downloads\base_colored (1).glb";
        const string Folder = "Assets/Eira/Environment/CharacterMeshes";
        const string MeshPath = Folder + "/EiraMesh.asset";
        const string MatPath = Folder + "/EiraVertexColor.mat";
        const string ScenePath = "Assets/Eira/Scenes/Level1Scene.unity";

        [Serializable] class GlRoot
        {
            public GlNode[] nodes;
            public GlMesh[] meshes;
            public GlAccessor[] accessors;
            public GlBufferView[] bufferViews;
        }
        [Serializable] class GlNode { public string name; public int mesh = -1; }
        [Serializable] class GlMesh { public GlPrimitive[] primitives; }
        [Serializable] class GlPrimitive { public GlAttrs attributes; public int indices = -1; }
        [Serializable] class GlAttrs { public int POSITION = -1; public int NORMAL = -1; public int COLOR_0 = -1; }
        [Serializable] class GlAccessor
        {
            public int bufferView = -1; public int byteOffset = 0;
            public int componentType = 0; public int count = 0; public string type;
        }
        [Serializable] class GlBufferView
        {
            public int buffer = -1; public int byteOffset = 0;
            public int byteLength = 0; public int byteStride = 0;
        }

        [MenuItem("Eira/Import Eira Character")]
        public static void Do()
        {
            byte[] json;
            byte[] bin;
            if (!ReadGlb(GlbPath, out json, out bin))
            {
                Debug.LogError("ImportEiraCharacter: no se pudo leer el GLB en " + GlbPath);
                return;
            }

            GlRoot o;
            try { o = JsonUtility.FromJson<GlRoot>(Encoding.UTF8.GetString(json).TrimStart('\uFEFF')); }
            catch (Exception e)
            {
                Debug.LogError("ImportEiraCharacter JSON: " + e.Message);
                return;
            }

            if (o.meshes == null || o.meshes.Length == 0 || o.meshes[0].primitives == null || o.meshes[0].primitives.Length == 0)
            {
                Debug.LogError("ImportEiraCharacter: el GLB no tiene meshes");
                return;
            }
            var p = o.meshes[0].primitives[0];
            var idx = p.attributes.POSITION;
            if (idx >= o.accessors.Length)
            {
                Debug.LogError("ImportEiraCharacter: sin POSITION");
                return;
            }

            Vector3[] pos = ReadVec3(o.accessors[idx], o.bufferViews, bin, mirrorX: true);
            Vector3[] nrm = ReadVec3(p.attributes.NORMAL >= 0 ? o.accessors[p.attributes.NORMAL] : null, o.bufferViews, bin, mirrorX: true);
            Color32[] col = ReadColors(p.attributes.COLOR_0 >= 0 ? o.accessors[p.attributes.COLOR_0] : null, o.bufferViews, bin);
            int[] ind = ReadIndices(p.indices >= 0 ? o.accessors[p.indices] : null, o.bufferViews, bin);

            var m = new Mesh();
            m.name = "EiraMesh";
            m.vertices = pos;
            if (nrm != null && nrm.Length == pos.Length) m.normals = nrm;
            if (col != null && col.Length == pos.Length) m.colors32 = col;

            var tris = new int[ind != null ? ind.Length : pos.Length];
            if (ind != null)
            {
                for (int t = 0; t + 2 < ind.Length; t += 3)
                {
                    tris[t] = ind[t + 1];
                    tris[t + 1] = ind[t + 2];
                    tris[t + 2] = ind[t];
                }
            }
            else
            {
                for (int t = 0; t + 2 < pos.Length; t += 3) { tris[t] = t + 1; tris[t + 1] = t + 2; tris[t + 2] = t; }
            }
            m.triangles = tris;
            if (nrm == null) m.RecalculateNormals();
            m.RecalculateBounds();

            if (!Directory.Exists(Folder)) Directory.CreateDirectory(Folder);
            AssetDatabase.DeleteAsset(MeshPath);
            AssetDatabase.DeleteAsset(MatPath);
            AssetDatabase.CreateAsset(m, MeshPath);

            var sh = Shader.Find("Universal Render Pipeline/Particles/Lit");
            if (sh == null)
            {
                Debug.LogError("ImportEiraCharacter: no se encontro 'Universal Render Pipeline/Particles/Lit'");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return;
            }
            var mat = new Material(sh) { name = "EiraVertexColor" };
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0.05f);
            mat.SetFloat("_Smoothness", 0.15f);
            mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            AssetDatabase.CreateAsset(mat, MatPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var scene = EditorSceneManager.OpenScene(ScenePath);
            var eira = GameObject.Find("Eira");
            if (eira == null)
            {
                Debug.LogError("ImportEiraCharacter: no hay GameObject 'Eira' en la escena");
                return;
            }
            var vis = eira.transform.Find("EiraVisual");
            if (vis == null)
            {
                Debug.LogError("ImportEiraCharacter: no hay 'EiraVisual' en Eira");
                return;
            }

            var placeholders = new List<GameObject>();
            foreach (Transform c in vis) placeholders.Add(c.gameObject);
            foreach (var g in placeholders) UnityEngine.Object.DestroyImmediate(g);

            var mf = vis.GetComponent<MeshFilter>();
            if (mf == null) mf = vis.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = m;

            var mr = vis.GetComponent<MeshRenderer>();
            if (mr == null) mr = vis.gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            vis.localPosition = Vector3.zero;
            vis.localRotation = Quaternion.identity;
            vis.localScale = Vector3.one;

            EditorSceneManager.SaveScene(scene);
            Debug.Log(string.Format(
                "ImportEiraCharacter OK: mesh {0} verts, {1} tris, bounds A={2}, C={3}; mesh asset {4}; mat {5}",
                m.vertexCount, m.triangles.Length / 3, m.bounds.size, m.bounds.center, MeshPath, MatPath));
        }

        static bool ReadGlb(string path, out byte[] json, out byte[] bin)
        {
            json = new byte[0];
            bin = new byte[0];
            using (var fs = File.OpenRead(path))
            {
                var hdr = new byte[12];
                if (fs.Read(hdr, 0, 12) != 12) return false;
                if (Encoding.ASCII.GetString(hdr, 0, 4) != "glTF") return false;
                while (fs.Position < fs.Length)
                {
                    var h = new byte[8];
                    if (fs.Read(h, 0, 8) != 8) return false;
                    int clen = BitConverter.ToInt32(h, 0);
                    string ctype = Encoding.ASCII.GetString(h, 4, 4).TrimEnd('\0');
                    if (ctype == "JSON")
                    {
                        json = new byte[clen];
                        if (fs.Read(json, 0, clen) != clen) return false;
                    }
                    else if (ctype == "BIN")
                    {
                        bin = new byte[clen];
                        if (fs.Read(bin, 0, clen) != clen) return false;
                    }
                    else
                    {
                        fs.Seek(clen, SeekOrigin.Current);
                    }
                }
            }
            return json.Length > 0;
        }

        static Vector3[] ReadVec3(GlAccessor a, GlBufferView[] bvs, byte[] bin, bool mirrorX)
        {
            if (a == null || a.componentType != 5126 || a.type != "VEC3") return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : 12;
            var outV = new Vector3[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                outV[i] = new Vector3(
                    mirrorX ? -BitConverter.ToSingle(bin, off) : BitConverter.ToSingle(bin, off),
                    BitConverter.ToSingle(bin, off + 4),
                    BitConverter.ToSingle(bin, off + 8));
            }
            return outV;
        }

        static Color32[] ReadColors(GlAccessor a, GlBufferView[] bvs, byte[] bin)
        {
            if (a == null || a.componentType != 5121 || a.type != "VEC4") return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : 4;
            var outC = new Color32[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                outC[i] = new Color32(bin[off], bin[off + 1], bin[off + 2], bin[off + 3]);
            }
            return outC;
        }

        static int[] ReadIndices(GlAccessor a, GlBufferView[] bvs, byte[] bin)
        {
            if (a == null) return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : (a.componentType == 5125 ? 4 : 2);
            var tris = new int[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                tris[i] = a.componentType == 5125 ? BitConverter.ToInt32(bin, off) : BitConverter.ToUInt16(bin, off);
            }
            return tris;
        }
    }
}