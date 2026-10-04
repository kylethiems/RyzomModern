using Ryzom.Core.Formats;
using Ryzom.Core.Graphics;
using Ryzom.Core.Math3D;

namespace Ryzom.Tests;

public class PbrGraphicsTests
{
    [Fact]
    public void SynthesizePbrSet_GeneratesValidNormalAndOrmMaps()
    {
        int width = 16;
        int height = 16;
        byte[] diffuse = new byte[width * height * 4];

        // Create high-contrast checkerboard pattern to trigger spatial gradients
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = (y * width + x) * 4;
                byte val = (byte)((x % 2 == y % 2) ? 240 : 30);
                diffuse[idx + 0] = val; // R
                diffuse[idx + 1] = val; // G
                diffuse[idx + 2] = val; // B
                diffuse[idx + 3] = 255; // A
            }
        }

        var pbr = PbrTextureSynthesizer.SynthesizePbrSet(diffuse, width, height, normalStrength: 2.0f);

        Assert.Equal(width, pbr.Width);
        Assert.Equal(height, pbr.Height);
        Assert.Equal(diffuse.Length, pbr.NormalRgba.Length);
        Assert.Equal(diffuse.Length, pbr.OrmRgba.Length);

        // Check normal map blue channel (Z is pointing out of surface, encoded > 128)
        Assert.True(pbr.NormalRgba[2] >= 120);

        // Check ORM map channels
        byte ao = pbr.OrmRgba[0];
        byte roughness = pbr.OrmRgba[1];
        Assert.True(ao > 0);
        Assert.True(roughness > 0);
    }

    [Fact]
    public void GenerateTangents_CalculatesOrthogonalTangents()
    {
        var mesh = new NeLMesh();
        // Triangle in XY plane facing +Z
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0f, 0f, 0f), Vector3f.UnitZ, 0f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(1f, 0f, 0f), Vector3f.UnitZ, 1f, 0f));
        mesh.Vertices.Add(new Vertex3D(new Vector3f(0f, 1f, 0f), Vector3f.UnitZ, 0f, 1f));
        mesh.Indices.AddRange(new ushort[] { 0, 1, 2 });

        var pbrVertices = TangentSpaceGenerator.GenerateTangents(mesh);

        Assert.Equal(3, pbrVertices.Count);
        foreach (var v in pbrVertices)
        {
            // Tangent must be normalized and orthogonal to normal (Dot(N, T) == 0)
            Assert.True(MathF.Abs(v.Tangent.Length() - 1.0f) < 1e-3f);
            Assert.True(MathF.Abs(Vector3f.Dot(v.Normal, v.Tangent)) < 1e-4f);
        }
    }
}
