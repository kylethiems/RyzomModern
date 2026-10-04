using System;
using Ryzom.Core.Math3D;
using Ryzom.Engine.Spatial;
using Xunit;

namespace Ryzom.Tests;

public class AcetoneWashTests
{
    [Fact]
    public void ApplyAcetoneWash_SmoothsStairStepNoiseWithoutCollapsingVolume()
    {
        // Construct a 3x3 grid with an elevated central spike (stair-step layer ridge)
        var vertices = new Vector3f[]
        {
            new(0f, 0f, 0f), new(1f, 0f, 0f), new(2f, 0f, 0f),
            new(0f, 1f, 0f), new(1f, 1f, 1.0f), new(2f, 1f, 0f), // vertex 4 is spiked (+1.0)
            new(0f, 2f, 0f), new(1f, 2f, 0f), new(2f, 2f, 0f)
        };

        var indices = new int[]
        {
            0, 1, 3,  1, 4, 3,
            1, 2, 4,  2, 5, 4,
            3, 4, 6,  4, 7, 6,
            4, 5, 7,  5, 8, 7
        };

        float initialSpike = vertices[4].Z;

        // Apply 2-cycle Acetone Vapor Bath
        var result = AcetoneWashMeshFilter.ApplyAcetoneWash(
            vertices, indices, cycles: 2, lambda: 0.25f, mu: -0.26f);

        Assert.Equal(vertices.Length, result.Vertices.Length);
        Assert.Equal(indices.Length, result.Indices.Length);
        Assert.Equal(vertices.Length, result.Normals.Length);
        Assert.Equal(vertices.Length, result.Roughness.Length);

        // Verify the sharp spike has been smoothed down by surface tension
        float smoothedSpike = result.Vertices[4].Z;
        Assert.True(smoothedSpike < initialSpike,
            $"Spike not smoothed: initial={initialSpike}, smoothed={smoothedSpike}");
        Assert.True(smoothedSpike > 0.0f,
            $"Spike over-smoothed/collapsed below 0: {smoothedSpike}");

        // Verify bounds remain intact (no collapse of outer dimensions)
        Assert.True(MathF.Abs(result.Vertices[0].X - 0f) < 0.1f);
        Assert.True(MathF.Abs(result.Vertices[8].X - 2f) < 0.1f);
    }

    [Fact]
    public void ApplyAcetoneWash_ComputesValidNormalsAndHighGlossRoughness()
    {
        var vertices = new Vector3f[]
        {
            new(0f, 0f, 0f),
            new(1f, 0f, 0f),
            new(0.5f, 1f, 0f)
        };
        var indices = new int[] { 0, 1, 2 };

        var result = AcetoneWashMeshFilter.ApplyAcetoneWash(vertices, indices, cycles: 2);

        foreach (var normal in result.Normals)
        {
            Assert.True(MathF.Abs(normal.Length() - 1.0f) < 1e-4f);
        }

        foreach (var roughness in result.Roughness)
        {
            // High-gloss curing yields low roughness on flat, smoothed manifolds
            Assert.True(roughness >= 0.05f && roughness <= 0.85f);
        }
    }
}
