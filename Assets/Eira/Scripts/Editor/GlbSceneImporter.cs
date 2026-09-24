using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace EiraGame.Editor
{
    // Importador GLB (glTF v2) mínimo para escenografía estática: nodos -> meshes,
    // materiales de color plano (URP Lit), MeshCollider por malla. Solo JSON/BIN,
    // sin Draco. Espeja X (glTF es right-handed) e invierte el winding.
    public static class GlbSceneImporter
    {
        // --- esquema JSON glTF (JsonUtility) ---
        [Serializable] class GlRoot
        {
            public GlNode[] nodes;
            public GlMesh[] meshes;
            public GlMat[] materials;
            public GlAccessor[] accessors;
            public GlBufferView[] bufferViews;
        }
        [Serializable] class GlNode
        {
            public string name;
            public int mesh = -1;
            public int[] children;
            public float[] matrix;
            public float[] translation;
            public float[] rotation;
            public float[] scale;
        }
        [Serializable] class GlMesh { public GlPrimitive[] primitives; }
        [Serializable] class GlPrimitive
        {
            public GlAttrs attributes;
            public int indices = -1;
            public int material = -1;
        }
        [Serializable] class GlAttrs
        {
            public int POSITION = -1; public int NORMAL = -1; public int TANGENT = -1;
            public int TEXCOORD_0 = -1; public int COLOR_0 = -1;
            public int JOINTS_0 = -1; public int WEIGHTS_0 = -1;
        }
        [Serializable] class GlAccessor
        {
            public int bufferView = -1; public int byteOffset = 0;
            public int componentType = 0; public int count = 0;
            public string type;
        }
        [Serializable] class GlBufferView
        {
            public int buffer = -1; public int byteOffset = 0;
            public int byteLength = 0; public int byteStride = 0;
        }
        [Serializable] class GlMat
        {
            public GlMatExt extensions;
            public GlPbrM pbrMetallicRoughness;
        }
        [Serializable] class GlMatExt { public GlPbr pbrSpecularGlossiness; }
        [Serializable] class GlPbr { public float[] diffuseFactor; public float glossinessFactor; }
        [Serializable] class GlPbrM { public float[] baseColorFactor; public float metallicFactor; public float roughnessFactor; }

        public static GameObject BuildFromFile(string glbPath, string rootName, out Mesh[] savedMeshes)
        {
            savedMeshes = new Mesh[0];
            var root = new GameObject(rootName);
            byte[] json;
            try
            {
                if (!ReadGlb(glbPath, out json)) return root;
            }
            catch (Exception e)
            {
                Debug.LogError("GlbSceneImporter: " + e.Message);
                return root;
            }

            var txt = Encoding.UTF8.GetString(json).TrimStart('\uFEFF');
            GlRoot o;
            try { o = JsonUtility.FromJson<GlRoot>(txt); }
            catch (Exception e)
            {
                Debug.LogError("GlbSceneImporter JSON: " + e.Message);
                return root;
            }

            var b = new byte[0];
            using (var fs = File.OpenRead(glbPath))
            {
                fs.Seek(12, SeekOrigin.Begin);
                while (fs.Position < fs.Length)
                {
                    var h = new byte[8];
                    if (fs.Read(h, 0, 8) != 8) break;
                    int clen = BitConverter.ToInt32(h, 0);
                    string ctype = Encoding.ASCII.GetString(h, 4, 4).TrimEnd('\0');
                    if (ctype == "BIN")
                    {
                        b = new byte[clen];
                        if (fs.Read(b, 0, clen) != clen) b = new byte[0];
                    }
                    else
                    {
                        fs.Seek(clen, SeekOrigin.Current);
                    }
                }
            }

            var nodes = o.nodes == null ? new GlNode[0] : o.nodes;
            var meshes = o.meshes == null ? new GlMesh[0] : o.meshes;
            var mats = o.materials;
            var accs = o.accessors;
            var bvs = o.bufferViews;

            var meshCache = new Dictionary<int, Mesh>();
            var meshAssets = new List<Mesh>();
            var gos = new GameObject[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                gos[i] = new GameObject(string.IsNullOrEmpty(nodes[i].name) ? "Node_" + i : nodes[i].name);

            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                var go = gos[i];
                if (n.mesh >= 0 && n.mesh < meshes.Length && meshes[n.mesh].primitives != null && meshes[n.mesh].primitives.Length > 0)
                {
                    var p = meshes[n.mesh].primitives[0];
                    Mesh m;
                    if (!meshCache.TryGetValue(n.mesh, out m))
                    {
                        m = BuildMesh(p, accs, bvs, b);
                        if (m != null)
                        {
                            m.name = "mapshalo_mesh_" + n.mesh;
                            meshCache[n.mesh] = m;
                            meshAssets.Add(m);
                        }
                    }
                    if (m != null)
                    {
                        go.AddComponent<MeshFilter>().sharedMesh = m;
                        go.AddComponent<MeshRenderer>().sharedMaterial = MakeMaterial(mats, p.material);
                        go.AddComponent<MeshCollider>().sharedMesh = m;
                    }
                }
                ApplyTransform(go.transform, n);
            }

            for (int i = 0; i < nodes.Length; i++)
                if (nodes[i].children != null)
                    foreach (var c in nodes[i].children)
                        if (c >= 0 && c < nodes.Length)
                            gos[c].transform.SetParent(gos[i].transform, false);

            if (nodes.Length > 0)
                gos[0].transform.SetParent(root.transform, false);

            savedMeshes = meshAssets.ToArray();
            return root;
        }

        public static void SaveMeshesAsAssets(IEnumerable<Mesh> meshes, string folder)
        {
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            int idx = 0;
            foreach (var m in meshes)
            {
                if (m == null) continue;
                var path = folder + "/Mesh_" + idx + ".asset";
                UnityEditor.AssetDatabase.CreateAsset(m, path);
                idx++;
            }
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
        }

        static bool ReadGlb(string path, out byte[] json)
        {
            json = new byte[0];
            using (var fs = File.OpenRead(path))
            {
                var hdr = new byte[12];
                if (fs.Read(hdr, 0, 12) != 12) return false;
                string magic = Encoding.ASCII.GetString(hdr, 0, 4);
                if (magic != "glTF") return false;
                while (fs.Position < fs.Length)
                {
                    var h = new byte[8];
                    if (fs.Read(h, 0, 8) != 8) return false;
                    int clen = BitConverter.ToInt32(h, 0);
                    string ctype = Encoding.ASCII.GetString(h, 4, 4);
                    if (ctype == "JSON")
                    {
                        json = new byte[clen];
                        return fs.Read(json, 0, clen) == clen;
                    }
                    fs.Seek(clen, SeekOrigin.Current);
                }
            }
            return false;
        }

        static Vector3[] ReadVec3(int accessor, GlAccessor[] accs, GlBufferView[] bvs, byte[] bin, ref int stride)
        {
            if (accessor < 0 || accessor >= accs.Length) return null;
            var a = accs[accessor];
            if (a.componentType != 5126 || a.type != "VEC3") return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : 12;
            var outV = new Vector3[a.count];
            stride = st;
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                outV[i] = new Vector3(-BitConverter.ToSingle(bin, off), BitConverter.ToSingle(bin, off + 4), BitConverter.ToSingle(bin, off + 8));
            }
            return outV;
        }

        static Vector3[] ReadNormals(int accessor, GlAccessor[] accs, GlBufferView[] bvs, byte[] bin)
        {
            if (accessor < 0) return null;
            var a = accs[accessor];
            if (a == null || a.type != "VEC3" || a.componentType != 5126) return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : 12;
            var outV = new Vector3[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                outV[i] = new Vector3(-BitConverter.ToSingle(bin, off), BitConverter.ToSingle(bin, off + 4), BitConverter.ToSingle(bin, off + 8));
            }
            return outV;
        }

        static int[] ReadIndices(int accessor, GlAccessor[] accs, GlBufferView[] bvs, byte[] bin)
        {
            if (accessor < 0) return null;
            var a = accs[accessor];
            if (a == null) return null;
            var bv = a.bufferView >= 0 ? bvs[a.bufferView] : null;
            int baseOff = (bv == null ? 0 : bv.byteOffset) + a.byteOffset;
            int st = bv != null && bv.byteStride != 0 ? bv.byteStride : (a.componentType == 5125 ? 4 : 2);
            var tris = new int[a.count];
            for (int i = 0; i < a.count; i++)
            {
                int off = baseOff + i * st;
                if (a.componentType == 5125) tris[i] = BitConverter.ToInt32(bin, off);
                else tris[i] = BitConverter.ToUInt16(bin, off);
            }
            return tris;
        }

        static Mesh BuildMesh(GlPrimitive p, GlAccessor[] accs, GlBufferView[] bvs, byte[] bin)
        {
            int stride = 0;
            var pos = ReadVec3(p.attributes.POSITION, accs, bvs, bin, ref stride);
            if (pos == null || pos.Length == 0) return null;

            var nrm = ReadNormals(p.attributes.NORMAL, accs, bvs, bin);
            var ind = ReadIndices(p.indices, accs, bvs, bin);

            var m = new Mesh();
            m.name = "mapshalo_mesh";
            m.vertices = pos;
            if (nrm != null && nrm.Length == pos.Length) m.normals = nrm;

            if (ind != null && ind.Length >= 3)
            {
                var tris = new int[ind.Length];
                for (int t = 0; t + 2 < ind.Length; t += 3)
                {
                    tris[t] = ind[t + 1];
                    tris[t + 1] = ind[t + 2];
                    tris[t + 2] = ind[t];
                }
                m.triangles = tris;
            }
            else
            {
                var tri = new int[pos.Length];
                for (int t = 0; t + 2 < pos.Length; t += 3)
                { tri[t] = t + 1; tri[t + 1] = t + 2; tri[t + 2] = t; }
                m.triangles = tri;
            }
            if (nrm == null) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Material MakeMaterial(GlMat[] mats, int materialIndex)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh == null ? Shader.Find("Standard") : sh);
            Color c = new Color(0.55f, 0.55f, 0.6f, 1f);
            if (mats != null && materialIndex >= 0 && materialIndex < mats.Length && mats[materialIndex] != null)
            {
                var m = mats[materialIndex];
                if (m.extensions != null && m.extensions.pbrSpecularGlossiness != null && m.extensions.pbrSpecularGlossiness.diffuseFactor != null && m.extensions.pbrSpecularGlossiness.diffuseFactor.Length >= 3)
                {
                    var d = m.extensions.pbrSpecularGlossiness.diffuseFactor;
                    c = new Color(d[0], d[1], d[2], d.Length > 3 ? d[3] : 1f);
                }
                else if (m.pbrMetallicRoughness != null && m.pbrMetallicRoughness.baseColorFactor != null && m.pbrMetallicRoughness.baseColorFactor.Length >= 3)
                {
                    var b = m.pbrMetallicRoughness.baseColorFactor;
                    c = new Color(b[0], b[1], b[2], b.Length > 3 ? b[3] : 1f);
                }
            }
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", 0.15f);
            mat.SetFloat("_Smoothness", 0.5f);
            return mat;
        }

        static void ApplyTransform(Transform t, GlNode n)
        {
            if (n.matrix != null && n.matrix.Length == 16)
            {
                var m = new Matrix4x4();
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 4; c++)
                        m[r, c] = n.matrix[c * 4 + r];
                t.localPosition = m.GetColumn(3);
                t.localRotation = m.rotation;
                t.localScale = m.lossyScale;
                return;
            }
            t.localPosition = n.translation == null ? Vector3.zero : new Vector3(n.translation[0], n.translation[1], n.translation[2]);
            t.localRotation = n.rotation == null ? Quaternion.identity : new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3]);
            t.localScale = n.scale == null ? Vector3.one : new Vector3(n.scale[0], n.scale[1], n.scale[2]);
        }
    }
}