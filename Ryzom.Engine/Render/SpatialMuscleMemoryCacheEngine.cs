using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ryzom.Engine.Render;

/// <summary>
/// 3D spatial voxel coordinate identifying a localized volume in the game world.
/// </summary>
public readonly record struct LocationVoxelKey(int RegionId, int VoxelX, int VoxelY, int VoxelZ);

/// <summary>
/// Cached surface state stored in localized muscle memory (Surface Radiance Cache & SVT Page).
/// </summary>
public record CachedSurfacePatch(
    ulong PatchId,
    Vector3 Position,
    (float R, float G, float B) Albedo,
    (float R, float G, float B) StoredRadiance,
    float Roughness,
    int TexturePageId,
    long LastAccessedTimestampMs
);

/// <summary>
/// Telemetry comparing cold pipeline execution (fresh rasterization & ray tracing) vs warm localized cache retrieval.
/// </summary>
public record SpatialMuscleMemoryTelemetry(
    string LocationName,
    int TotalVoxelQueries,
    int CacheHits,
    int CacheMisses,
    float CacheHitRatePercent,
    float ColdFrameTimeMs,
    float WarmFrameTimeMs,
    float LatencyReductionPercent,
    float MemoryAllocatedMb,
    int StoredPatchesCount
);

/// <summary>
/// Engine for localized spatial muscle memory, implementing Surface Radiance Caching,
/// Streaming Virtual Texture (SVT) page lookups, and Potentially Visible Set (PVS) tables.
/// Replaces full-pipeline re-rendering with ultra-low latency O(1) cache retrieval.
/// </summary>
public class SpatialMuscleMemoryCacheEngine
{
    private readonly Dictionary<LocationVoxelKey, CachedSurfacePatch> _cache = new();
    private readonly int _maxPatchCapacity;
    private long _currentTimestampMs = 0;

    // Baseline processing costs in milliseconds
    public const float ColdRasterAndRayTracePerPatchMs = 0.052f; // Cost to compute geometry, shaders & bounce light
    public const float CacheRetrievalPerPatchMs = 0.0035f;        // Cost of O(1) hash lookup & memory fetch
    public const float PatchVramSizeBytes = 256f;               // 256 bytes per surface patch record

    public SpatialMuscleMemoryCacheEngine(int maxPatchCapacity = 65536)
    {
        _maxPatchCapacity = Math.Max(16, maxPatchCapacity);
    }

    public int CachedPatchesCount => _cache.Count;

    public float MemoryAllocatedMb => (_cache.Count * PatchVramSizeBytes) / (1024f * 1024f);

    /// <summary>
    /// Converts a continuous world position into a discretized spatial voxel key.
    /// </summary>
    public static LocationVoxelKey QuantizePosition(int regionId, Vector3 position, float voxelSize = 2.0f)
    {
        return new LocationVoxelKey(
            regionId,
            (int)MathF.Floor(position.X / voxelSize),
            (int)MathF.Floor(position.Y / voxelSize),
            (int)MathF.Floor(position.Z / voxelSize)
        );
    }

    /// <summary>
    /// Attempts to retrieve a surface patch from localized muscle memory, or populates it via cold evaluation.
    /// </summary>
    public bool TryQueryPatch(LocationVoxelKey key, out CachedSurfacePatch? patch)
    {
        _currentTimestampMs++;
        if (_cache.TryGetValue(key, out var existing))
        {
            // LRU touch
            patch = existing with { LastAccessedTimestampMs = _currentTimestampMs };
            _cache[key] = patch;
            return true;
        }

        patch = null;
        return false;
    }

    /// <summary>
    /// Stores or updates a surface patch in the localized muscle memory cache.
    /// Performs LRU eviction if capacity is reached.
    /// </summary>
    public void StorePatch(LocationVoxelKey key, CachedSurfacePatch patch)
    {
        _currentTimestampMs++;
        if (_cache.Count >= _maxPatchCapacity && !_cache.ContainsKey(key))
        {
            EvictOldestPatches(_maxPatchCapacity / 10 + 1);
        }

        _cache[key] = patch with { LastAccessedTimestampMs = _currentTimestampMs };
    }

    /// <summary>
    /// Evicts the least recently used patches from memory.
    /// </summary>
    public int EvictOldestPatches(int count)
    {
        var oldest = _cache
            .OrderBy(kv => kv.Value.LastAccessedTimestampMs)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in oldest)
        {
            _cache.Remove(key);
        }

        return oldest.Count;
    }

    /// <summary>
    /// Clears localized muscle memory for a biome or world area.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Pre-warms localized muscle memory for a known location/outpost with pre-baked radiance and material pages.
    /// </summary>
    public void PrewarmLocation(int regionId, int patchCount, (float R, float G, float B) ambientRadiance)
    {
        for (int i = 0; i < patchCount; i++)
        {
            var key = new LocationVoxelKey(regionId, i % 64, (i / 64) % 64, i / 4096);
            var patch = new CachedSurfacePatch(
                PatchId: (ulong)(regionId * 1_000_000 + i),
                Position: new Vector3(key.VoxelX * 2f, key.VoxelY * 2f, key.VoxelZ * 2f),
                Albedo: (0.75f, 0.70f, 0.60f),
                StoredRadiance: ambientRadiance,
                Roughness: 0.45f,
                TexturePageId: 1000 + (i % 16),
                LastAccessedTimestampMs: _currentTimestampMs
            );
            StorePatch(key, patch);
        }
    }

    /// <summary>
    /// Simulates player traversal through a location, measuring cache hit rates and compute latency reduction.
    /// </summary>
    public SpatialMuscleMemoryTelemetry SimulateLocationTraversal(
        string locationName,
        int regionId,
        int totalQueries,
        float spatialFamiliarityRatio)
    {
        totalQueries = Math.Max(100, totalQueries);
        spatialFamiliarityRatio = Math.Clamp(spatialFamiliarityRatio, 0.0f, 1.0f);

        int hits = 0;
        int misses = 0;

        for (int i = 0; i < totalQueries; i++)
        {
            // Familiar paths revisit cached coordinates; unfamiliar paths probe new voxels
            bool isRevisit = (i / (float)totalQueries) < spatialFamiliarityRatio;
            int voxelX = isRevisit ? (i % 32) : (1000 + i);
            int voxelY = isRevisit ? ((i / 32) % 32) : (1000 + (i / 32));
            var key = new LocationVoxelKey(regionId, voxelX, voxelY, 0);

            if (TryQueryPatch(key, out _))
            {
                hits++;
            }
            else
            {
                misses++;
                // Cold miss populates cache
                StorePatch(key, new CachedSurfacePatch(
                    PatchId: (ulong)i,
                    Position: new Vector3(voxelX * 2f, voxelY * 2f, 0),
                    Albedo: (0.8f, 0.6f, 0.4f),
                    StoredRadiance: (0.5f, 0.4f, 0.3f),
                    Roughness: 0.5f,
                    TexturePageId: i % 8,
                    LastAccessedTimestampMs: _currentTimestampMs
                ));
            }
        }

        float hitRate = (float)hits / totalQueries * 100f;

        // Compute simulated frame times for rendering 300 visible surface patches in this scene
        const int visiblePatchesPerFrame = 300;
        float coldFrameTimeMs = visiblePatchesPerFrame * ColdRasterAndRayTracePerPatchMs; // ~15.6 ms
        float warmFrameTimeMs = (visiblePatchesPerFrame * (hitRate / 100f) * CacheRetrievalPerPatchMs) +
                                (visiblePatchesPerFrame * (1f - (hitRate / 100f)) * ColdRasterAndRayTracePerPatchMs);

        float reductionPercent = coldFrameTimeMs > 0
            ? ((coldFrameTimeMs - warmFrameTimeMs) / coldFrameTimeMs) * 100f
            : 0f;

        return new SpatialMuscleMemoryTelemetry(
            LocationName: locationName,
            TotalVoxelQueries: totalQueries,
            CacheHits: hits,
            CacheMisses: misses,
            CacheHitRatePercent: hitRate,
            ColdFrameTimeMs: coldFrameTimeMs,
            WarmFrameTimeMs: warmFrameTimeMs,
            LatencyReductionPercent: reductionPercent,
            MemoryAllocatedMb: MemoryAllocatedMb,
            StoredPatchesCount: CachedPatchesCount
        );
    }
}
