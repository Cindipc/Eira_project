using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace EiraGame.Editor
{
    // Parser mínimo de glTF 2.0 binario (.glb) para extraer posición + índices.
    // Se usa para convertir environment_collider.glb en un Mesh (mesh de colisión del entorno).
    public static class GlbImporter
    {
        public struct GlbResult
        {
            public Mesh mesh;
            public string nodeName;
        }

        public static GlbResult Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "glTF")
                throw new Exception("No es un archivo glTF binario: " + path);

            byte[] jsonChunk = null;
            byte[] binChunk = null;
            int offset = 12;
            while (offset + 8 <= bytes.Length)
            {
                uint len = BitConverter.ToUInt32(bytes, offset);
                string type = Encoding.ASCII.GetString(bytes, offset + 4, 4);
                int start = offset + 8;
                if (start + len > bytes.Length) break;
                if (type == "JSON") jsonChunk = new byte[len];
                else if (type == "BIN\0") binChunk = new byte[len];
                if (jsonChunk != null && type == "JSON") Array.Copy(bytes, start, jsonChunk, 0, (int)len);
                else if (binChunk != null && type == "BIN\0") Array.Copy(bytes, start, binChunk, 0, (int)len);
                offset += 8 + (int)len;
            }
            if (jsonChunk == null) throw new Exception("Sin chunk JSON");

            var root = JObject.Parse(Encoding.UTF8.GetString(jsonChunk));
            var accessors = root["accessors"] as JArray;
            var bufferViews = root["bufferViews"] as JArray;
            var meshes = root["meshes"] as JArray;
            var nodes = root["nodes"] as JArray;
            var scene = root["scene"]?.Value<int>() ?? 0;
            var scenes = root["scenes"] as JArray;

            if (binChunk == null) throw new Exception("Sin chunk BIN");

            // Localizar el nodo con mesh (e ignorar transform de nodo: se aplica a la mano).
            string meshNodeName = "geometry";
            if (meshes != null && meshes.Count > 0 && meshes[0] is JObject m0)
                meshNodeName = m0["name"]?.Value<string>() ?? "geometry";

            // primero primitiva del primer mesh
            JObject primitive = null;
            if (meshes != null && meshes.Count > 0)
                primitive = (meshes[0] as JObject)?["primitives"]?[0] as JObject;
            if (primitive == null) throw new Exception("Sin primitivas");

            var attributes = primitive["attributes"] as JObject;
            if (attributes == null) throw new Exception("Sin atributos");
            int posAccessorIdx = attributes["POSITION"]?.Value<int>() ?? -1;
            int idxAccessorIdx = primitive["indices"]?.Value<int>() ?? -1;
            if (posAccessorIdx < 0) throw new Exception("Sin POSITION");

            var posAcc = accessors[posAccessorIdx] as JObject;
            int posBv = posAcc["bufferView"]?.Value<int>() ?? -1;
            int posComponent = posAcc["componentType"]?.Value<int>() ?? 0;
            int posCount = posAcc["count"]?.Value<int>() ?? 0;

            var bvPos = bufferViews[posBv] as JObject;
            int posByteOffset = bvPos["byteOffset"]?.Value<int>() ?? 0;
            int posByteLength = bvPos["byteLength"]?.Value<int>() ?? 0;
            if (posByteOffset + posByteLength > binChunk.Length) throw new Exception("Posiciones fuera del rango BIN");

            var vertices = new List<Vector3>(posCount);
            if (posComponent == 5126) // FLOAT
            {
                for (int i = 0; i < posCount; i++)
                {
                    int o = posByteOffset + i * 12;
                    float x = BitConverter.ToSingle(binChunk, o);
                    float y = BitConverter.ToSingle(binChunk, o + 4);
                    float z = BitConverter.ToSingle(binChunk, o + 8);
                    vertices.Add(new Vector3(x, y, z));
                }
            }
            else
            {
                throw new Exception("componentType POSITION no soportado: " + posComponent);
            }

            // Aplicar la transformación del nodo que referencia el mesh, para que el
            // mesh quede en el espacio de la escena glTF (misma convención que los splats).
            var nodeXf = GetNodeTransform(nodes, meshes);
            if (nodeXf.HasValue && nodeXf.Value != Matrix4x4.identity)
            {
                for (int i = 0; i < vertices.Count; i++)
                    vertices[i] = nodeXf.Value.MultiplyPoint3x4(vertices[i]);
            }

            var triangles = new List<int>();
            if (idxAccessorIdx >= 0)
            {
                var idxAcc = accessors[idxAccessorIdx] as JObject;
                int idxBv = idxAcc["bufferView"]?.Value<int>() ?? -1;
                int idxComponent = idxAcc["componentType"]?.Value<int>() ?? 0;
                int idxCount = idxAcc["count"]?.Value<int>() ?? 0;
                var bvIdx = bufferViews[idxBv] as JObject;
                int idxByteOffset = bvIdx["byteOffset"]?.Value<int>() ?? 0;
                int idxByteLength = bvIdx["byteLength"]?.Value<int>() ?? 0;
                if (idxByteOffset + idxByteLength <= binChunk.Length)
                {
                    for (int i = 0; i < idxCount; i++)
                    {
                        if (idxComponent == 5125) // UINT
                            triangles.Add(BitConverter.ToInt32(binChunk, idxByteOffset + i * 4));
                        else if (idxComponent == 5123) // USHORT
                            triangles.Add(BitConverter.ToUInt16(binChunk, idxByteOffset + i * 2));
                    }
                }
            }

            var mesh = new Mesh { name = meshNodeName };
            mesh.SetVertices(vertices);
            if (triangles.Count > 0) mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return new GlbResult { mesh = mesh, nodeName = meshNodeName };
        }

        static Matrix4x4? GetNodeTransform(JArray nodes, JArray meshes)
        {
            if (nodes == null || meshes == null) return null;
            int meshIndex = meshes.Count > 0 ? 0 : -1;
            foreach (var n in nodes)
            {
                var node = n as JObject;
                if (node == null) continue;
                var mIdx = node["mesh"]?.Value<int>();
                if (mIdx == meshIndex || (mIdx == null && node["children"] == null)) { }
                if (mIdx == meshIndex)
                {
                    var matrix = node["matrix"];
                    if (matrix != null && matrix is JArray ma && ma.Count >= 16)
                    {
                        var f = new float[16];
                        for (int i = 0; i < 16; i++) f[i] = (float)ma[i];
                        return new Matrix4x4(
                            new Vector4(f[0], f[1], f[2], f[3]),
                            new Vector4(f[4], f[5], f[6], f[7]),
                            new Vector4(f[8], f[9], f[10], f[11]),
                            new Vector4(f[12], f[13], f[14], f[15]));
                    }
                    var t = node["translation"]; var r = node["rotation"]; var s = node["scale"];
                    Vector3 pos = t is JArray ta && ta.Count >= 3 ? new Vector3((float)ta[0], (float)ta[1], (float)ta[2]) : Vector3.zero;
                    Quaternion rot = r is JArray ra && ra.Count >= 4 ? new Quaternion((float)ra[0], (float)ra[1], (float)ra[2], (float)ra[3]) : Quaternion.identity;
                    Vector3 sc = s is JArray sa && sa.Count >= 3 ? new Vector3((float)sa[0], (float)sa[1], (float)sa[2]) : Vector3.one;
                    return Matrix4x4.TRS(pos, rot, sc);
                }
            }
            return null;
        }
    }
}