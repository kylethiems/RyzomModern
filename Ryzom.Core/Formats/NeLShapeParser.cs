using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Formats;

/// <summary>
/// Parser for legacy NeL 3D engine mesh and shape files (.shape / ASCII OBJ / NeL serial form).
/// </summary>
public static class NeLShapeParser
{
    /// <summary>
    /// Parses an ASCII NeL / OBJ formatted stream into a strongly-typed NeLMesh.
    /// </summary>
    public static NeLMesh Parse(TextReader reader, string meshName = "NeLModel")
    {
        var mesh = new NeLMesh { Name = meshName };
        var positions = new List<Vector3f>();
        var normals = new List<Vector3f>();
        var uvs = new List<(float U, float V)>();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) continue;

            switch (tokens[0])
            {
                case "v": // Vertex position: v x y z
                    if (tokens.Length >= 4 &&
                        float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) &&
                        float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) &&
                        float.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z))
                    {
                        positions.Add(new Vector3f(x, y, z));
                    }
                    break;

                case "vn": // Vertex normal: vn nx ny nz
                    if (tokens.Length >= 4 &&
                        float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float nx) &&
                        float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float ny) &&
                        float.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float nz))
                    {
                        normals.Add(new Vector3f(nx, ny, nz));
                    }
                    break;

                case "vt": // Texture coordinates: vt u v
                    if (tokens.Length >= 3 &&
                        float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float u) &&
                        float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    {
                        uvs.Add((u, v));
                    }
                    break;

                case "f": // Face: f v1/vt1/vn1 v2/vt2/vn2 v3/vt3/vn3
                    if (tokens.Length >= 4)
                    {
                        // Parse first 3 vertices for triangle
                        for (int i = 1; i <= 3; i++)
                        {
                            string[] parts = tokens[i].Split('/');
                            int vIdx = int.Parse(parts[0]) - 1; // 1-based to 0-based
                            int vtIdx = (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) ? int.Parse(parts[1]) - 1 : -1;
                            int vnIdx = (parts.Length > 2 && !string.IsNullOrEmpty(parts[2])) ? int.Parse(parts[2]) - 1 : -1;

                            Vector3f pos = (vIdx >= 0 && vIdx < positions.Count) ? positions[vIdx] : Vector3f.Zero;
                            Vector3f norm = (vnIdx >= 0 && vnIdx < normals.Count) ? normals[vnIdx] : Vector3f.UnitZ;
                            float fu = (vtIdx >= 0 && vtIdx < uvs.Count) ? uvs[vtIdx].U : 0f;
                            float fv = (vtIdx >= 0 && vtIdx < uvs.Count) ? uvs[vtIdx].V : 0f;

                            mesh.Vertices.Add(new Vertex3D(pos, norm, fu, fv));
                            mesh.Indices.Add((ushort)(mesh.Vertices.Count - 1));
                        }
                    }
                    break;
            }
        }

        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Parses a legacy NeL binary chunk buffer into a NeLMesh.
    /// </summary>
    public static NeLMesh ParseBinary(BinaryReader reader, string meshName = "NeLBinaryMesh")
    {
        var mesh = new NeLMesh { Name = meshName };

        // NeL binary header magic: 'NELM' (0x4D4C454E) or raw count-prefixed
        uint vertexCount = reader.ReadUInt32();
        for (uint i = 0; i < vertexCount; i++)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            float z = reader.ReadSingle();
            float nx = reader.ReadSingle();
            float ny = reader.ReadSingle();
            float nz = reader.ReadSingle();
            float u = reader.ReadSingle();
            float v = reader.ReadSingle();

            mesh.Vertices.Add(new Vertex3D(new Vector3f(x, y, z), new Vector3f(nx, ny, nz), u, v));
        }

        uint indexCount = reader.ReadUInt32();
        for (uint i = 0; i < indexCount; i++)
        {
            mesh.Indices.Add(reader.ReadUInt16());
        }

        mesh.RecalculateBounds();
        return mesh;
    }
}
