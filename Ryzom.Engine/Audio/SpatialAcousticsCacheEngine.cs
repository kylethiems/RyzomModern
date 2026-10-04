using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Ryzom.Engine.Audio;

/// <summary>
/// Fidelity tier for spatial audio voice processing.
/// </summary>
public enum AudioVoiceTier
{
    BinauralHrtf = 0,               // 100% full-rate binaural HRTF convolution (pinna, azimuth, elevation, early reflections)
    PeripheralAmbisonics = 1,       // 3D Ambisonic panning into a shared biome submix reverb bus
    OccludedPortalDiffraction = 2,  // Low-pass filtered diffraction around walls/dunes
    VirtualVoiceDormant = 3         // Out of earshot or sub-threshold; voice virtualized to 0 DSP compute
}

/// <summary>
/// Spatial grid voxel key for acoustic probe wave-field caching.
/// </summary>
public readonly record struct AcousticProbeKey(int RegionId, int VoxelX, int VoxelY, int VoxelZ);

/// <summary>
/// Pre-computed or cached acoustic impulse response parameters for a localized 3D volume.
/// </summary>
public record CachedAcousticImpulse(
    ulong ProbeId,
    Vector3 Position,
    float ReverbDecayRt60Sec,       // Reverberation time RT60 (seconds)
    float EarlyReflectionsRatio,    // Ratio of direct sound to early wall/obstacle reflections
    float OcclusionTransmissionLossDb, // High-frequency attenuation through terrain/foliage
    float LowPassCutoffHz,          // Cutoff frequency for diffraction filtering
    long LastAccessedTimestampMs
);

/// <summary>
/// Comparative telemetry quantifying audio DSP buffer budget, voice allocation, and compute latency.
/// </summary>
public record SpatialAudioBudgetTelemetry(
    string BiomeName,
    float DspBufferBudgetMs,        // Hardware buffer deadline (e.g. 10.67ms for 512 samples @ 48kHz)
    int TotalActiveEmitters,
    int BinauralHrtfVoiceCount,
    int AmbisonicVoiceCount,
    int OccludedVoiceCount,
    int VirtualVoiceCount,
    float BaselineDspTimeMs,        // Unoptimized naive binaural execution across all emitters
    float OptimizedDspTimeMs,       // Foveated audio + acoustic probe cache retrieval
    float DspSavingsPercent,
    float CacheHitRatePercent,
    int CachedProbeCount
);

/// <summary>
/// Engine for localized spatial acoustics, wave-field impulse caching (Project Acoustics paradigm),
/// acoustic ray tracing, and foveated binaural HRTF audio voice management.
/// </summary>
public class SpatialAcousticsCacheEngine
{
    private readonly Dictionary<AcousticProbeKey, CachedAcousticImpulse> _probeCache = new();
    private readonly int _maxProbeCapacity;
    private long _currentTimestampMs = 0;

    // Baseline DSP computational costs per voice in milliseconds (48kHz / 512 sample buffer)
    public const float DspBinauralHrtfPerVoiceMs = 0.120f;
    public const float DspAmbisonicsPerVoiceMs = 0.025f;
    public const float DspOccludedDiffractionPerVoiceMs = 0.012f;
    public const float DspVirtualVoicePerVoiceMs = 0.000f;
    public const float DspProbeCacheLookupPerVoxelMs = 0.002f;

    public SpatialAcousticsCacheEngine(int maxProbeCapacity = 16384)
    {
        _maxProbeCapacity = Math.Max(16, maxProbeCapacity);
    }

    public int CachedProbeCount => _probeCache.Count;

    /// <summary>
    /// Quantizes a 3D coordinate into an acoustic probe voxel (default 4.0m resolution).
    /// </summary>
    public static AcousticProbeKey QuantizeAcousticPosition(int regionId, Vector3 position, float voxelSize = 4.0f)
    {
        return new AcousticProbeKey(
            regionId,
            (int)MathF.Floor(position.X / voxelSize),
            (int)MathF.Floor(position.Y / voxelSize),
            (int)MathF.Floor(position.Z / voxelSize)
        );
    }

    /// <summary>
    /// Evaluates which audio fidelity tier an emitter belongs to based on listener distance and view cone.
    /// </summary>
    public static AudioVoiceTier ClassifyVoiceTier(
        Vector3 listenerPos,
        Vector3 listenerForward,
        Vector3 emitterPos,
        float listenerFovDeg = 80f,
        bool isDirectlyOccluded = false)
    {
        float distance = Vector3.Distance(listenerPos, emitterPos);

        // Outside maximum audible threshold (~150m in open terrain)
        if (distance > 150f)
        {
            return AudioVoiceTier.VirtualVoiceDormant;
        }

        if (isDirectlyOccluded && distance > 15f)
        {
            return AudioVoiceTier.OccludedPortalDiffraction;
        }

        Vector3 toEmitter = Vector3.Normalize(emitterPos - listenerPos);
        float dot = Vector3.Dot(listenerForward, toEmitter);
        float angleDeg = MathF.Acos(Math.Clamp(dot, -1f, 1f)) * (180f / MathF.PI);

        // Focal hearing cone (coincides with central foveal cone: ~40% of FoV or under 15m proximity)
        if (distance < 12f || angleDeg <= (listenerFovDeg * 0.45f))
        {
            return AudioVoiceTier.BinauralHrtf;
        }

        // Peripheral spatial hearing
        if (distance <= 80f)
        {
            return AudioVoiceTier.PeripheralAmbisonics;
        }

        return AudioVoiceTier.OccludedPortalDiffraction;
    }

    /// <summary>
    /// Attempts to retrieve pre-computed acoustic impulse response from localized spatial muscle memory.
    /// </summary>
    public bool TryQueryAcousticProbe(AcousticProbeKey key, out CachedAcousticImpulse? impulse)
    {
        _currentTimestampMs++;
        if (_probeCache.TryGetValue(key, out var existing))
        {
            impulse = existing with { LastAccessedTimestampMs = _currentTimestampMs };
            _probeCache[key] = impulse;
            return true;
        }

        impulse = null;
        return false;
    }

    /// <summary>
    /// Stores an acoustic probe in the localized cache with LRU eviction.
    /// </summary>
    public void StoreAcousticProbe(AcousticProbeKey key, CachedAcousticImpulse impulse)
    {
        _currentTimestampMs++;
        if (_probeCache.Count >= _maxProbeCapacity && !_probeCache.ContainsKey(key))
        {
            EvictOldestProbes(_maxProbeCapacity / 10 + 1);
        }

        _probeCache[key] = impulse with { LastAccessedTimestampMs = _currentTimestampMs };
    }

    /// <summary>
    /// Evicts the least recently queried acoustic probes.
    /// </summary>
    public int EvictOldestProbes(int count)
    {
        var oldest = _probeCache
            .OrderBy(kv => kv.Value.LastAccessedTimestampMs)
            .Take(count)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var key in oldest)
        {
            _probeCache.Remove(key);
        }

        return oldest.Count;
    }

    /// <summary>
    /// Pre-warms localized acoustic impulses for a specific biome (e.g. desert dunes, dense jungle canopy, cavern).
    /// </summary>
    public void PrewarmBiomeAcoustics(int regionId, int probeCount, float rt60Sec, float directToReverbRatio)
    {
        for (int i = 0; i < probeCount; i++)
        {
            var key = new AcousticProbeKey(regionId, i % 32, (i / 32) % 32, 0);
            var impulse = new CachedAcousticImpulse(
                ProbeId: (ulong)(regionId * 100_000 + i),
                Position: new Vector3(key.VoxelX * 4f, key.VoxelY * 4f, 0f),
                ReverbDecayRt60Sec: rt60Sec,
                EarlyReflectionsRatio: directToReverbRatio,
                OcclusionTransmissionLossDb: 3.5f,
                LowPassCutoffHz: 12000f,
                LastAccessedTimestampMs: _currentTimestampMs
            );
            StoreAcousticProbe(key, impulse);
        }
    }

    /// <summary>
    /// Calculates audio DSP time and voice tier distribution across hundreds of concurrent emitters.
    /// </summary>
    public SpatialAudioBudgetTelemetry CalculateAcousticBudget(
        string biomeName,
        int totalEmitters,
        float focalRatio = 0.08f,       // ~8% in central binaural focus
        float peripheralRatio = 0.22f,  // ~22% in mid-distance ambisonic field
        float occludedRatio = 0.30f,    // ~30% behind structures/dunes
        float probeCacheHitRatio = 0.85f,
        float bufferSizeSamples = 512f,
        float sampleRate = 48000f)
    {
        float dspBufferBudgetMs = (bufferSizeSamples / sampleRate) * 1000f; // 10.67ms for 512 samples @ 48kHz

        int binauralCount = (int)MathF.Round(totalEmitters * focalRatio);
        int ambisonicCount = (int)MathF.Round(totalEmitters * peripheralRatio);
        int occludedCount = (int)MathF.Round(totalEmitters * occludedRatio);
        int virtualCount = Math.Max(0, totalEmitters - (binauralCount + ambisonicCount + occludedCount));

        // Baseline: Naive unculled audio pipeline computes full binaural HRTF convolution for all emitters
        float baselineDspTimeMs = totalEmitters * DspBinauralHrtfPerVoiceMs;

        // Optimized: Foveated voice tiers + localized acoustic probe caching
        float optimizedDspTimeMs =
            (binauralCount * DspBinauralHrtfPerVoiceMs) +
            (ambisonicCount * DspAmbisonicsPerVoiceMs) +
            (occludedCount * DspOccludedDiffractionPerVoiceMs) +
            (virtualCount * DspVirtualVoicePerVoiceMs) +
            (totalEmitters * (1.0f - probeCacheHitRatio) * 0.015f) + // Cost of cold wave-field ray trace on miss
            (totalEmitters * probeCacheHitRatio * DspProbeCacheLookupPerVoxelMs);

        float savingsPercent = baselineDspTimeMs > 0
            ? ((baselineDspTimeMs - optimizedDspTimeMs) / baselineDspTimeMs) * 100f
            : 0f;

        return new SpatialAudioBudgetTelemetry(
            BiomeName: biomeName,
            DspBufferBudgetMs: dspBufferBudgetMs,
            TotalActiveEmitters: totalEmitters,
            BinauralHrtfVoiceCount: binauralCount,
            AmbisonicVoiceCount: ambisonicCount,
            OccludedVoiceCount: occludedCount,
            VirtualVoiceCount: virtualCount,
            BaselineDspTimeMs: baselineDspTimeMs,
            OptimizedDspTimeMs: optimizedDspTimeMs,
            DspSavingsPercent: savingsPercent,
            CacheHitRatePercent: probeCacheHitRatio * 100f,
            CachedProbeCount: CachedProbeCount
        );
    }
}
