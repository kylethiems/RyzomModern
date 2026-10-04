using System.Numerics;
using Ryzom.Engine.Render;
using Xunit;

namespace Ryzom.Tests;

public class SpatialMuscleMemoryTests
{
    private readonly SpatialMuscleMemoryCacheEngine _engine = new(maxPatchCapacity: 4096);

    [Fact]
    public void QuantizePosition_MapsContinuouslyToDiscretizedVoxels()
    {
        var pos1 = new Vector3(10.2f, 20.8f, 5.1f);
        var pos2 = new Vector3(11.9f, 21.4f, 5.9f); // Same 2.0-unit voxel (5, 10, 2)
        var pos3 = new Vector3(12.5f, 20.8f, 5.1f); // Different voxel (6, 10, 2)

        var key1 = SpatialMuscleMemoryCacheEngine.QuantizePosition(regionId: 1, pos1, voxelSize: 2.0f);
        var key2 = SpatialMuscleMemoryCacheEngine.QuantizePosition(regionId: 1, pos2, voxelSize: 2.0f);
        var key3 = SpatialMuscleMemoryCacheEngine.QuantizePosition(regionId: 1, pos3, voxelSize: 2.0f);

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
        Assert.Equal(5, key1.VoxelX);
        Assert.Equal(10, key1.VoxelY);
        Assert.Equal(2, key1.VoxelZ);
    }

    [Fact]
    public void CacheMiss_TriggersStore_AndSubsequentLookupHits()
    {
        var key = new LocationVoxelKey(RegionId: 10, VoxelX: 42, VoxelY: 88, VoxelZ: 3);

        // First attempt should miss
        bool hit1 = _engine.TryQueryPatch(key, out var patch1);
        Assert.False(hit1);
        Assert.Null(patch1);

        // Store patch
        var newPatch = new CachedSurfacePatch(
            PatchId: 104288,
            Position: new Vector3(84f, 176f, 6f),
            Albedo: (0.8f, 0.7f, 0.5f),
            StoredRadiance: (0.4f, 0.35f, 0.25f),
            Roughness: 0.3f,
            TexturePageId: 4,
            LastAccessedTimestampMs: 1
        );
        _engine.StorePatch(key, newPatch);

        // Second attempt should hit
        bool hit2 = _engine.TryQueryPatch(key, out var patch2);
        Assert.True(hit2);
        Assert.NotNull(patch2);
        Assert.Equal(104288ul, patch2.PatchId);
        Assert.Equal(0.3f, patch2.Roughness);
    }

    [Fact]
    public void PrewarmedLocation_AchievesHighCacheHitRate_AndLowLatency()
    {
        // Pre-warm 1000 patches for Pyr Outpost (Region 1)
        _engine.PrewarmLocation(regionId: 1, patchCount: 1000, ambientRadiance: (0.9f, 0.5f, 0.2f));
        Assert.Equal(1000, _engine.CachedPatchesCount);

        // Simulate traversal with 90% familiarity in pre-warmed outpost
        var telemetry = _engine.SimulateLocationTraversal(
            locationName: "Pyr Outpost",
            regionId: 1,
            totalQueries: 500,
            spatialFamiliarityRatio: 0.90f
        );

        Assert.True(telemetry.CacheHitRatePercent >= 80.0f, $"Expected >=80% hit rate, got {telemetry.CacheHitRatePercent}%");
        Assert.True(telemetry.LatencyReductionPercent >= 70.0f, $"Expected >=70% latency reduction, got {telemetry.LatencyReductionPercent}%");
        Assert.True(telemetry.WarmFrameTimeMs < 5.0f, $"Expected warm frame time <5ms, got {telemetry.WarmFrameTimeMs}ms");
    }

    [Fact]
    public void EvictOldestPatches_RespectsCapacityLimit_LRUPolicy()
    {
        var smallEngine = new SpatialMuscleMemoryCacheEngine(maxPatchCapacity: 100);

        for (int i = 0; i < 150; i++)
        {
            var key = new LocationVoxelKey(RegionId: 1, VoxelX: i, VoxelY: 0, VoxelZ: 0);
            var patch = new CachedSurfacePatch(
                PatchId: (ulong)i,
                Position: new Vector3(i, 0, 0),
                Albedo: (1f, 1f, 1f),
                StoredRadiance: (0.5f, 0.5f, 0.5f),
                Roughness: 0.5f,
                TexturePageId: 0,
                LastAccessedTimestampMs: 0
            );
            smallEngine.StorePatch(key, patch);
        }

        // Cache must have enforced capacity bounds
        Assert.True(smallEngine.CachedPatchesCount <= 100);

        // The earliest entries (e.g. i=0) should have been evicted
        var evictedKey = new LocationVoxelKey(RegionId: 1, VoxelX: 0, VoxelY: 0, VoxelZ: 0);
        Assert.False(smallEngine.TryQueryPatch(evictedKey, out _));

        // The newest entries (e.g. i=149) must be retained
        var recentKey = new LocationVoxelKey(RegionId: 1, VoxelX: 149, VoxelY: 0, VoxelZ: 0);
        Assert.True(smallEngine.TryQueryPatch(recentKey, out _));
    }
}
