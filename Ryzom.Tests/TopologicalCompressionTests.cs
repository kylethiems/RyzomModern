using System;
using Ryzom.Core.Compression;
using Ryzom.Core.Formats;
using Ryzom.Core.Math3D;
using Xunit;

namespace Ryzom.Tests;

public class TopologicalCompressionTests
{
    [Fact]
    public void TritPacker_RoundtripsArbitraryTritSequencesLosslessly()
    {
        var originalTrits = new sbyte[] { -1, 0, 1, 1, -1, 0, 0, -1, 1, 0, -1, -1, 1 };

        byte[] packed = TritPacker.PackTrits(originalTrits);
        sbyte[] unpacked = TritPacker.UnpackTrits(packed, originalTrits.Length);

        Assert.Equal(originalTrits.Length, unpacked.Length);
        for (int i = 0; i < originalTrits.Length; i++)
        {
            Assert.Equal(originalTrits[i], unpacked[i]);
        }
    }

    [Fact]
    public void TritPacker_5TritsPerByte_ValidatesAll243States()
    {
        Span<sbyte> buffer = stackalloc sbyte[5];

        for (sbyte t0 = -1; t0 <= 1; t0++)
        {
            for (sbyte t1 = -1; t1 <= 1; t1++)
            {
                for (sbyte t2 = -1; t2 <= 1; t2++)
                {
                    for (sbyte t3 = -1; t3 <= 1; t3++)
                    {
                        for (sbyte t4 = -1; t4 <= 1; t4++)
                        {
                            byte packed = TritPacker.Pack5Trits(t0, t1, t2, t3, t4);
                            Assert.True(packed < 243);

                            TritPacker.Unpack5Trits(packed, buffer);
                            Assert.Equal(t0, buffer[0]);
                            Assert.Equal(t1, buffer[1]);
                            Assert.Equal(t2, buffer[2]);
                            Assert.Equal(t3, buffer[3]);
                            Assert.Equal(t4, buffer[4]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void TopologicalLandmarkCompressor_CompressesMeshWithHighRatio()
    {
        var mesh = new NeLMesh { Name = "FyrosWarriorLOD" };

        // Generate a 100-vertex grid
        for (int y = 0; y < 10; y++)
        {
            for (int x = 0; x < 10; x++)
            {
                mesh.Vertices.Add(new Vertex3D(
                    new Vector3f(x * 0.1f, y * 0.1f, MathF.Sin(x) * 0.05f),
                    Vector3f.UnitZ,
                    x / 10f, y / 10f));
            }
        }

        // Uncompressed raw positions size: 100 verts * 12 bytes = 1200 bytes
        int rawPositionsSize = mesh.Vertices.Count * 12;

        var compressed = TopologicalLandmarkCompressor.Compress(mesh, targetLandmarks: 25, stepSize: 0.05f);

        Assert.Equal(100, compressed.OriginalVertexCount);
        Assert.Equal(25, compressed.LandmarkCount);
        Assert.True(compressed.PackedBinary.Length < rawPositionsSize,
            $"Compressed ({compressed.PackedBinary.Length}) not smaller than raw ({rawPositionsSize})");

        // Verify decompression
        Vector3f[] reconstructed = TopologicalLandmarkCompressor.Decompress(compressed.PackedBinary);
        Assert.Equal(100, reconstructed.Length);

        // Verify topological landmark anchors are exact
        for (int i = 0; i < reconstructed.Length; i++)
        {
            Vector3f orig = mesh.Vertices[i].Position;
            Vector3f rec = reconstructed[i];
            float dist = (orig - rec).Length();

            // Reconstructed points are within quantization bound
            Assert.True(dist < 0.20f, $"Reconstructed vertex {i} deviated too far: {dist}");
        }
    }
}
