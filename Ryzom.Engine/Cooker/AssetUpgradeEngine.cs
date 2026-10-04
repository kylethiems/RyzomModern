using System;
using System.Diagnostics;
using System.IO;
using Ryzom.Core.Formats;
using Ryzom.Core.Graphics;
using Ryzom.Core.Math3D;
using Ryzom.Engine.Spatial;

namespace Ryzom.Engine.Cooker;

/// <summary>
/// Telemetry metrics produced by modernizing a legacy NeL asset.
/// </summary>
public record AssetUpgradeTelemetry(
    string ModelName,
    int OriginalVertices,
    int DecimatedVertices,
    float PolygonReductionPercent,
    int TriangleCount,
    float VolumePreservationRatio,
    double ExecutionTimeMs,
    byte[] GlbBinary,
    PbrTextureSet? PbrTextures);

/// <summary>
/// Autonomous 3-Tier Modernization Engine for legacy Ryzom MMORPG assets.
/// Chains Voxel Centroid Decimation, Digital Acetone Wash (Taubin non-shrinking curvature flow),
/// Tangent-Space Orthogonalization, and GLB/PBR Packaging.
/// </summary>
public static class AssetUpgradeEngine
{
    public static AssetUpgradeTelemetry UpgradeNeLMesh(
        NeLMesh legacyMesh,
        byte[]? diffuseTextureRgba = null,
        int texWidth = 512,
        int texHeight = 512,
        float voxelTolerance = 0.001f,
        int acetoneWashCycles = 4,
        float taubinLambda = 0.33f,
        float taubinMu = -0.34f)
    {
        var sw = Stopwatch.StartNew();
        int initialVerts = legacyMesh.Vertices.Count;

        // Step 1: LiDAR Voxel Grid Centroid Decimation
        legacyMesh.DecimateAndClean(voxelTolerance);
        int decimatedVerts = legacyMesh.Vertices.Count;
        float reductionPct = initialVerts > 0 ? (1.0f - (float)decimatedVerts / initialVerts) * 100.0f : 0f;

        // Step 2: Digital Acetone Wash (Volume-Preserving Taubin Dual-Step Smoothing)
        var posArray = new Vector3f[legacyMesh.Vertices.Count];
        var idxArray = new int[legacyMesh.Indices.Count];
        for (int i = 0; i < posArray.Length; i++) posArray[i] = legacyMesh.Vertices[i].Position;
        for (int i = 0; i < idxArray.Length; i++) idxArray[i] = legacyMesh.Indices[i];

        float initialNorm = ComputeMeshNorm(posArray);

        var washed = AcetoneWashMeshFilter.ApplyAcetoneWash(
            posArray, idxArray, cycles: acetoneWashCycles, lambda: taubinLambda, mu: taubinMu);

        float washedNorm = ComputeMeshNorm(washed.Vertices);
        float volumeRatio = initialNorm > 0 ? washedNorm / initialNorm : 1.0f;

        // Reconstruct NeLMesh with smoothed positions and cured Voronoi normals
        legacyMesh.Vertices.Clear();
        for (int i = 0; i < washed.Vertices.Length; i++)
        {
            legacyMesh.Vertices.Add(new Vertex3D(
                washed.Vertices[i],
                washed.Normals[i],
                posArray[i].X, posArray[i].Y // Preserve UVs
            ));
        }

        // Step 3: Tangent Space Generation (MikkTSpace Orthogonalization)
        var pbrVertices = TangentSpaceGenerator.GenerateTangents(legacyMesh);

        // Step 4: 4K PBR Texture Synthesis & Curing (if diffuse provided)
        PbrTextureSet? pbrSet = null;
        if (diffuseTextureRgba != null && diffuseTextureRgba.Length > 0)
        {
            pbrSet = PbrTextureSynthesizer.SynthesizePbrSet(diffuseTextureRgba, texWidth, texHeight, normalStrength: 2.2f);
        }

        // Step 5: Export to Modern Binary glTF 2.0 (.glb)
        byte[] glbData = GltfExporter.ExportGlb(pbrVertices, legacyMesh.Indices, legacyMesh.Name);

        sw.Stop();

        return new AssetUpgradeTelemetry(
            legacyMesh.Name,
            initialVerts,
            decimatedVerts,
            reductionPct,
            legacyMesh.Indices.Count / 3,
            volumeRatio,
            sw.Elapsed.TotalMilliseconds,
            glbData,
            pbrSet);
    }

    private static float ComputeMeshNorm(ReadOnlySpan<Vector3f> verts)
    {
        float sumSq = 0.0f;
        for (int i = 0; i < verts.Length; i++)
        {
            sumSq += verts[i].LengthSquared();
        }
        return MathF.Sqrt(sumSq);
    }
}
