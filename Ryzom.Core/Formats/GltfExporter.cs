using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Ryzom.Core.Graphics;
using Ryzom.Core.Math3D;

namespace Ryzom.Core.Formats;

/// <summary>
/// Serializes modernized 3D meshes into standard binary glTF 2.0 (.glb) container format.
/// Embeds positions, normals, tangents, and texture coordinates into a contiguous binary payload.
/// </summary>
public static class GltfExporter
{
    public static byte[] ExportGlb(IReadOnlyList<VertexPbr> vertices, IReadOnlyList<ushort> indices, string modelName = "RyzomModernModel")
    {
        using var binStream = new MemoryStream();
        using var binWriter = new BinaryWriter(binStream);

        // 1. Write Indices (UNSIGNED_SHORT, ComponentType 5123)
        long indexOffset = binStream.Position;
        for (int i = 0; i < indices.Count; i++)
        {
            binWriter.Write(indices[i]);
        }
        // Pad to 4-byte boundary
        while (binStream.Position % 4 != 0) binWriter.Write((byte)0);
        int indexLength = (int)(binStream.Position - indexOffset);

        // 2. Write Interleaved Vertex Buffer: [Pos(3), Normal(3), Tangent(4), UV(2)]
        // Total floats per vertex: 3 + 3 + 4 + 2 = 12 floats = 48 bytes
        long vertexOffset = binStream.Position;
        Vector3f minPos = vertices.Count > 0 ? vertices[0].Position : Vector3f.Zero;
        Vector3f maxPos = vertices.Count > 0 ? vertices[0].Position : Vector3f.Zero;

        for (int i = 0; i < vertices.Count; i++)
        {
            var v = vertices[i];
            // Position
            binWriter.Write(v.Position.X);
            binWriter.Write(v.Position.Y);
            binWriter.Write(v.Position.Z);

            minPos = new Vector3f(MathF.Min(minPos.X, v.Position.X), MathF.Min(minPos.Y, v.Position.Y), MathF.Min(minPos.Z, v.Position.Z));
            maxPos = new Vector3f(MathF.Max(maxPos.X, v.Position.X), MathF.Max(maxPos.Y, v.Position.Y), MathF.Max(maxPos.Z, v.Position.Z));

            // Normal
            binWriter.Write(v.Normal.X);
            binWriter.Write(v.Normal.Y);
            binWriter.Write(v.Normal.Z);

            // Tangent (XYZW, W = +1.0)
            binWriter.Write(v.Tangent.X);
            binWriter.Write(v.Tangent.Y);
            binWriter.Write(v.Tangent.Z);
            binWriter.Write(1.0f); // W

            // UV
            binWriter.Write(v.U);
            binWriter.Write(v.V);
        }

        while (binStream.Position % 4 != 0) binWriter.Write((byte)0);
        int vertexLength = (int)(binStream.Position - vertexOffset);
        byte[] binBytes = binStream.ToArray();

        // 3. Construct glTF JSON Structure
        var gltfJsonObj = new
        {
            asset = new { version = "2.0", generator = "RyzomModern 3-Tier Batch Upgrade Engine" },
            scene = 0,
            scenes = new[] { new { nodes = new[] { 0 } } },
            nodes = new[] { new { name = modelName, mesh = 0 } },
            meshes = new[]
            {
                new
                {
                    name = modelName,
                    primitives = new[]
                    {
                        new
                        {
                            attributes = new
                            {
                                POSITION = 1,
                                NORMAL = 2,
                                TANGENT = 3,
                                TEXCOORD_0 = 4
                            },
                            indices = 0,
                            mode = 4 // TRIANGLES
                        }
                    }
                }
            },
            buffers = new object[] { new { byteLength = binBytes.Length } },
            bufferViews = new object[]
            {
                // View 0: Indices
                new { buffer = 0, byteOffset = (int)indexOffset, byteLength = indices.Count * 2, target = 34963 }, // ELEMENT_ARRAY_BUFFER
                // View 1: Interleaved Vertices
                new { buffer = 0, byteOffset = (int)vertexOffset, byteLength = vertexLength, byteStride = 48, target = 34962 } // ARRAY_BUFFER
            },
            accessors = new object[]
            {
                // 0: INDICES
                new { bufferView = 0, byteOffset = 0, componentType = 5123, count = indices.Count, type = "SCALAR" },
                // 1: POSITION
                new
                {
                    bufferView = 1, byteOffset = 0, componentType = 5126, count = vertices.Count, type = "VEC3",
                    min = new float[] { minPos.X, minPos.Y, minPos.Z },
                    max = new float[] { maxPos.X, maxPos.Y, maxPos.Z }
                },
                // 2: NORMAL
                new { bufferView = 1, byteOffset = 12, componentType = 5126, count = vertices.Count, type = "VEC3" },
                // 3: TANGENT
                new { bufferView = 1, byteOffset = 24, componentType = 5126, count = vertices.Count, type = "VEC4" },
                // 4: TEXCOORD_0
                new { bufferView = 1, byteOffset = 40, componentType = 5126, count = vertices.Count, type = "VEC2" }
            }
        };

        string jsonText = JsonSerializer.Serialize(gltfJsonObj);
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonText);

        // Pad JSON to 4-byte boundary with spaces (0x20)
        int jsonPadding = (4 - (jsonBytes.Length % 4)) % 4;
        int jsonTotalLength = jsonBytes.Length + jsonPadding;

        // 4. Assemble Binary GLB Container
        // Total GLB size: 12 (header) + 8 (json chunk header) + jsonTotalLength + 8 (bin chunk header) + binBytes.Length
        uint totalGlbLength = (uint)(12 + 8 + jsonTotalLength + 8 + binBytes.Length);

        using var glbStream = new MemoryStream((int)totalGlbLength);
        using var glbWriter = new BinaryWriter(glbStream);

        // GLB Header
        glbWriter.Write(0x46546C67); // Magic: 'glTF'
        glbWriter.Write(2u);         // Version: 2
        glbWriter.Write(totalGlbLength);

        // JSON Chunk
        glbWriter.Write((uint)jsonTotalLength);
        glbWriter.Write(0x4E4F534A); // ChunkType: 'JSON'
        glbWriter.Write(jsonBytes);
        for (int i = 0; i < jsonPadding; i++) glbWriter.Write((byte)0x20);

        // BIN Chunk
        glbWriter.Write((uint)binBytes.Length);
        glbWriter.Write(0x004E4942); // ChunkType: 'BIN\0'
        glbWriter.Write(binBytes);

        return glbStream.ToArray();
    }
}
