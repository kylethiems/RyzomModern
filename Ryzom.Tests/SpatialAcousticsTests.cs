using System.Numerics;
using Ryzom.Engine.Audio;
using Xunit;

namespace Ryzom.Tests;

public class SpatialAcousticsTests
{
    private readonly SpatialAcousticsCacheEngine _engine = new(maxProbeCapacity: 2048);

    [Fact]
    public void QuantizeAcousticPosition_DiscretizesToProbes()
    {
        var pos1 = new Vector3(12.2f, 15.8f, 3.1f);
        var pos2 = new Vector3(14.9f, 13.4f, 2.9f); // Same 4.0-unit voxel (3, 3, 0)
        var pos3 = new Vector3(18.5f, 15.8f, 3.1f); // Different voxel (4, 3, 0)

        var key1 = SpatialAcousticsCacheEngine.QuantizeAcousticPosition(regionId: 1, pos1, voxelSize: 4.0f);
        var key2 = SpatialAcousticsCacheEngine.QuantizeAcousticPosition(regionId: 1, pos2, voxelSize: 4.0f);
        var key3 = SpatialAcousticsCacheEngine.QuantizeAcousticPosition(regionId: 1, pos3, voxelSize: 4.0f);

        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
        Assert.Equal(3, key1.VoxelX);
        Assert.Equal(3, key1.VoxelY);
        Assert.Equal(0, key1.VoxelZ);
    }

    [Fact]
    public void ClassifyVoiceTier_PrioritizesFocalBinauralAndCullsDormant()
    {
        var listenerPos = new Vector3(0, 0, 0);
        var listenerForward = new Vector3(0, 0, 1); // Looking along +Z

        // 1. Direct frontal target inside focal cone (5m ahead)
        var focalPos = new Vector3(0, 0, 5);
        var tierFocal = SpatialAcousticsCacheEngine.ClassifyVoiceTier(listenerPos, listenerForward, focalPos, listenerFovDeg: 80f);
        Assert.Equal(AudioVoiceTier.BinauralHrtf, tierFocal);

        // 2. Peripheral combatant to the right flank (45m out at 60 degrees)
        var peripheralPos = new Vector3(35, 0, 25);
        var tierPeriph = SpatialAcousticsCacheEngine.ClassifyVoiceTier(listenerPos, listenerForward, peripheralPos, listenerFovDeg: 80f);
        Assert.Equal(AudioVoiceTier.PeripheralAmbisonics, tierPeriph);

        // 3. Occluded target behind a dune/wall (25m away)
        var occludedPos = new Vector3(10, 0, 20);
        var tierOccluded = SpatialAcousticsCacheEngine.ClassifyVoiceTier(listenerPos, listenerForward, occludedPos, listenerFovDeg: 80f, isDirectlyOccluded: true);
        Assert.Equal(AudioVoiceTier.OccludedPortalDiffraction, tierOccluded);

        // 4. Distant target across massive arena (>150m)
        var distantPos = new Vector3(0, 0, 180);
        var tierDormant = SpatialAcousticsCacheEngine.ClassifyVoiceTier(listenerPos, listenerForward, distantPos, listenerFovDeg: 80f);
        Assert.Equal(AudioVoiceTier.VirtualVoiceDormant, tierDormant);
    }

    [Fact]
    public void PrewarmBiomeAcoustics_PopulatesProbeCache_AndHits()
    {
        // Pre-warm 500 acoustic probes for Prime Roots Cavern (Region 3)
        _engine.PrewarmBiomeAcoustics(regionId: 3, probeCount: 500, rt60Sec: 4.2f, directToReverbRatio: 0.35f);
        Assert.Equal(500, _engine.CachedProbeCount);

        // Query probe at (0, 0, 0)
        var key = new AcousticProbeKey(RegionId: 3, VoxelX: 0, VoxelY: 0, VoxelZ: 0);
        bool hit = _engine.TryQueryAcousticProbe(key, out var impulse);

        Assert.True(hit);
        Assert.NotNull(impulse);
        Assert.Equal(4.2f, impulse.ReverbDecayRt60Sec);
        Assert.Equal(0.35f, impulse.EarlyReflectionsRatio);
    }

    [Fact]
    public void CalculateAcousticBudget_MassivePvPArena_ReducesDspTimeBelowBufferDeadline()
    {
        // 400 combatants shouting, casting spells, and swinging blades
        const int totalEmitters = 400;
        var telemetry = _engine.CalculateAcousticBudget(
            biomeName: "Fyros Arena (Pyr Citadel)",
            totalEmitters: totalEmitters,
            focalRatio: 0.08f,       // 32 focal binaural voices
            peripheralRatio: 0.22f,  // 88 ambisonic voices
            occludedRatio: 0.30f,    // 120 occluded portal voices
            probeCacheHitRatio: 0.90f,
            bufferSizeSamples: 512f,
            sampleRate: 48000f
        );

        // Baseline (400 * 0.12ms = 48.0ms) severely overruns the 10.67ms buffer deadline
        Assert.True(telemetry.BaselineDspTimeMs > 40.0f, $"Baseline DSP was {telemetry.BaselineDspTimeMs}ms");

        // Optimized DSP execution must sit comfortably inside the 10.67ms hardware deadline
        Assert.True(telemetry.OptimizedDspTimeMs < telemetry.DspBufferBudgetMs,
            $"Expected optimized DSP {telemetry.OptimizedDspTimeMs}ms < buffer deadline {telemetry.DspBufferBudgetMs}ms");

        // Savings must exceed 80%
        Assert.True(telemetry.DspSavingsPercent >= 80.0f, $"Expected >=80% savings, got {telemetry.DspSavingsPercent}%");
    }

    [Fact]
    public void StoreAcousticProbe_RespectsMaxCapacity_LRUPolicy()
    {
        var smallEngine = new SpatialAcousticsCacheEngine(maxProbeCapacity: 50);

        for (int i = 0; i < 80; i++)
        {
            var key = new AcousticProbeKey(RegionId: 1, VoxelX: i, VoxelY: 0, VoxelZ: 0);
            var probe = new CachedAcousticImpulse(
                ProbeId: (ulong)i,
                Position: new Vector3(i, 0, 0),
                ReverbDecayRt60Sec: 1.2f,
                EarlyReflectionsRatio: 0.6f,
                OcclusionTransmissionLossDb: 2.0f,
                LowPassCutoffHz: 8000f,
                LastAccessedTimestampMs: 0
            );
            smallEngine.StoreAcousticProbe(key, probe);
        }

        Assert.True(smallEngine.CachedProbeCount <= 50);

        var evictedKey = new AcousticProbeKey(RegionId: 1, VoxelX: 0, VoxelY: 0, VoxelZ: 0);
        Assert.False(smallEngine.TryQueryAcousticProbe(evictedKey, out _));

        var recentKey = new AcousticProbeKey(RegionId: 1, VoxelX: 79, VoxelY: 0, VoxelZ: 0);
        Assert.True(smallEngine.TryQueryAcousticProbe(recentKey, out _));
    }
}
