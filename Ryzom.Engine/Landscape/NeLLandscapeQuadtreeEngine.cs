using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ryzom.Engine.Landscape;

/// <summary>
/// The 5 core continents of Atys.
/// </summary>
public enum AtysContinent
{
    VerdantHeights = 0,     // Matis royal bark forests & giant tree canopy
    AedenAqueous = 1,       // Tryker floating atolls & crystal lakes
    BurningDesert = 2,      // Fyros red sandstone dunes & wind-sculpted mesa
    WitheredWastes = 3,     // Zoraï fungal swamplands & ancient mycorrhizae
    PrimeRoots = 4          // Deep subterranean chitin caverns & bioluminescent chasm
}

/// <summary>
/// 4-layer terrain splatting material weights.
/// </summary>
public record TerrainSplatWeights(
    float SandDune,
    float MossyBark,
    float FertileLoam,
    float RootStone
);

/// <summary>
/// A streaming landscape chunk node in the Continuous Dynamic Quadtree (CDLOD).
/// </summary>
public record LandscapeChunkNode(
    int ChunkX,
    int ChunkZ,
    int LodLevel,           // 0 = full resolution, 3 = coarse distant
    Vector2 MinBounds,
    Vector2 MaxBounds,
    float MinElevation,
    float MaxElevation
);

/// <summary>
/// Engine for NeL landscape quadtree chunk streaming, heightfield sampling,
/// terrain collision raycasting, and multi-texture splatting.
/// </summary>
public class NeLLandscapeQuadtreeEngine
{
    private readonly Dictionary<(int X, int Z), LandscapeChunkNode> _activeChunks = new();
    private readonly float _chunkSizeMeters;
    private readonly AtysContinent _continent;

    public NeLLandscapeQuadtreeEngine(AtysContinent continent = AtysContinent.VerdantHeights, float chunkSizeMeters = 64f)
    {
        _continent = continent;
        _chunkSizeMeters = Math.Max(16f, chunkSizeMeters);
    }

    public int ActiveChunkCount => _activeChunks.Count;
    public AtysContinent Continent => _continent;

    /// <summary>
    /// Computes terrain elevation at any 2D world coordinate using continent-specific procedural height functions.
    /// </summary>
    public float GetElevation(float worldX, float worldZ)
    {
        return _continent switch
        {
            AtysContinent.VerdantHeights =>
                12.0f + 8.0f * MathF.Sin(worldX * 0.02f) * MathF.Cos(worldZ * 0.02f) +
                3.0f * MathF.Sin(worldX * 0.08f + worldZ * 0.05f),

            AtysContinent.BurningDesert =>
                5.0f + 14.0f * MathF.Sin(worldX * 0.015f) + 4.0f * MathF.Cos(worldZ * 0.03f),

            AtysContinent.AedenAqueous =>
                1.5f + 3.0f * MathF.Sin(worldX * 0.01f) * MathF.Sin(worldZ * 0.01f),

            AtysContinent.WitheredWastes =>
                4.0f + 6.0f * MathF.Sin(worldX * 0.025f) + 2.5f * MathF.Cos(worldZ * 0.04f),

            AtysContinent.PrimeRoots =>
                -25.0f + 18.0f * MathF.Sin(worldX * 0.018f) * MathF.Cos(worldZ * 0.018f) -
                8.0f * MathF.Sin(worldX * 0.06f),

            _ => 0f
        };
    }

    /// <summary>
    /// Clamps a continuous position to ground level (PACS Heightfield Clamping).
    /// </summary>
    public Vector3 ClampToTerrain(Vector3 position, float verticalOffset = 0f)
    {
        float elevation = GetElevation(position.X, position.Z);
        return new Vector3(position.X, elevation + verticalOffset, position.Z);
    }

    /// <summary>
    /// Casts a ray downward to find intersection with the landscape heightfield.
    /// </summary>
    public bool RaycastTerrain(Ray ray, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        // Simple interval stepping along ray
        float t = 0f;
        const float maxDistance = 200f;
        const float step = 1.0f;

        while (t < maxDistance)
        {
            var p = ray.Position + ray.Direction * t;
            float elev = GetElevation(p.X, p.Z);

            if (p.Y <= elev)
            {
                hitPoint = new Vector3(p.X, elev, p.Z);

                // Compute normal via finite differences
                const float eps = 0.5f;
                float hL = GetElevation(p.X - eps, p.Z);
                float hR = GetElevation(p.X + eps, p.Z);
                float hD = GetElevation(p.X, p.Z - eps);
                float hU = GetElevation(p.X, p.Z + eps);

                var normal = new Vector3(hL - hR, 2.0f * eps, hD - hU);
                hitNormal = Vector3.Normalize(normal);
                return true;
            }

            t += step;
        }

        hitPoint = Vector3.Zero;
        hitNormal = Vector3.UnitY;
        return false;
    }

    /// <summary>
    /// Computes 4-layer terrain splatting weights based on elevation, slope, and continent biome.
    /// </summary>
    public TerrainSplatWeights CalculateSplatWeights(float worldX, float worldZ)
    {
        float elevation = GetElevation(worldX, worldZ);

        // Approximate slope
        const float eps = 0.5f;
        float hL = GetElevation(worldX - eps, worldZ);
        float hR = GetElevation(worldX + eps, worldZ);
        float slope = MathF.Abs(hR - hL) / (2.0f * eps);

        if (_continent == AtysContinent.BurningDesert)
        {
            return new TerrainSplatWeights(
                SandDune: Math.Clamp(1.0f - slope * 0.5f, 0.4f, 1.0f),
                MossyBark: 0.05f,
                FertileLoam: 0.05f,
                RootStone: Math.Clamp(slope, 0.1f, 0.9f)
            );
        }

        if (_continent == AtysContinent.PrimeRoots)
        {
            return new TerrainSplatWeights(
                SandDune: 0.05f,
                MossyBark: 0.25f,
                FertileLoam: 0.20f,
                RootStone: 0.50f
            );
        }

        // Verdant Heights default
        float barkWeight = elevation > 14f ? 0.65f : 0.25f;
        float loamWeight = elevation <= 14f ? 0.60f : 0.20f;
        float stoneWeight = Math.Clamp(slope * 0.8f, 0.10f, 0.85f);

        return new TerrainSplatWeights(
            SandDune: 0.05f,
            MossyBark: barkWeight,
            FertileLoam: loamWeight,
            RootStone: stoneWeight
        );
    }

    /// <summary>
    /// Updates streaming CDLOD chunk nodes relative to camera/listener position.
    /// </summary>
    public void UpdateStreamingChunks(Vector3 cameraPos, float viewDistanceMeters = 256f)
    {
        _activeChunks.Clear();
        int chunkRadius = (int)MathF.Ceiling(viewDistanceMeters / _chunkSizeMeters);
        int centerChunkX = (int)MathF.Floor(cameraPos.X / _chunkSizeMeters);
        int centerChunkZ = (int)MathF.Floor(cameraPos.Z / _chunkSizeMeters);

        for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
        {
            for (int dz = -chunkRadius; dz <= chunkRadius; dz++)
            {
                int cx = centerChunkX + dx;
                int cz = centerChunkZ + dz;

                float dist = MathF.Sqrt(dx * dx + dz * dz) * _chunkSizeMeters;
                if (dist > viewDistanceMeters) continue;

                int lod = dist < 64f ? 0 : (dist < 128f ? 1 : 2);

                var minB = new Vector2(cx * _chunkSizeMeters, cz * _chunkSizeMeters);
                var maxB = minB + new Vector2(_chunkSizeMeters, _chunkSizeMeters);

                float h0 = GetElevation(minB.X, minB.Y);
                float h1 = GetElevation(maxB.X, maxB.Y);

                _activeChunks[(cx, cz)] = new LandscapeChunkNode(
                    ChunkX: cx,
                    ChunkZ: cz,
                    LodLevel: lod,
                    MinBounds: minB,
                    MaxBounds: maxB,
                    MinElevation: Math.Min(h0, h1) - 2f,
                    MaxElevation: Math.Max(h0, h1) + 2f
                );
            }
        }
    }
}

public readonly record struct Ray(Vector3 Position, Vector3 Direction);
