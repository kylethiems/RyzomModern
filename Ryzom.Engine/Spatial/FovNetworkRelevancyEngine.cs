using System;
using System.Collections.Generic;
using System.Numerics;

namespace Ryzom.Engine.Spatial;

/// <summary>
/// Fidelity tier for network replication and client-side simulation.
/// Controls update tick frequency, packet payload size, and CPU bone simulation.
/// </summary>
public enum RelevancyFidelityTier
{
    /// <summary>Direct Field of View: 60Hz full transform, inverse kinematics, bone ticks, uncompressed deltas.</summary>
    FocalFov = 0,

    /// <summary>Peripheral Vision: 30Hz quantized delta, simplified hitbox, LOD1 baked animation.</summary>
    Peripheral = 1,

    /// <summary>Flank / Near-Perimeter Proxy: 10Hz position-only heartbeat, zero bone simulation.</summary>
    FlankProxy = 2,

    /// <summary>Actor Dormancy: 0Hz, zero network bandwidth, zero draw calls (complete object impermanence).</summary>
    Dormant = 3
}

/// <summary>
/// Camera / Observer state for Field of View relevancy culling.
/// </summary>
public record ObserverState(
    Vector3 Position,
    Vector3 ForwardDirection,
    float FovAngleDegrees = 90.0f,
    float MaxCombatRadius = 120.0f
);

/// <summary>
/// Evaluated network replication and simulation instructions for an entity in the arena.
/// </summary>
public record EntityRelevancyPacket(
    uint EntityId,
    Vector3 Position,
    RelevancyFidelityTier Tier,
    float AngleDegrees,
    float DistanceMeters,
    float TickRateHz,
    float BandwidthBytesPerSec,
    bool RenderMesh,
    bool SimulateBones
);

/// <summary>
/// Performance and bandwidth telemetry for massive PvP arenas.
/// </summary>
public record ArenaRelevancyMetrics(
    int TotalArenaEntities,
    int FocalCount,
    int PeripheralCount,
    int FlankCount,
    int DormantCount,
    float TotalUnoptimizedBandwidthKbps,
    float OptimizedFoveatedBandwidthKbps,
    float BandwidthReductionPercent,
    float CpuSimulationSavingsPercent
);

/// <summary>
/// Foveated Network Relevancy & Directional Area-of-Interest (AoI) Culling Engine.
/// Dynamically scales network replication frequency and CPU simulation load based on
/// character field of vision, angular divergence, and distance attenuation for massive PvP.
/// </summary>
public class FovNetworkRelevancyEngine
{
    public const float FullPacketSizeBytes = 180.0f;       // Full bone transforms, status buffs, weapon trajectories
    public const float PeripheralPacketSizeBytes = 64.0f;  // Quantized 16-bit positions, packed state bitfield
    public const float FlankPacketSizeBytes = 24.0f;       // Coarse position ping for spatial audio/radar
    public const float BaseServerTickRateHz = 60.0f;

    /// <summary>
    /// Evaluates the network relevancy and simulation fidelity for an individual entity.
    /// </summary>
    public EntityRelevancyPacket CalculateEntityRelevancy(
        ObserverState observer,
        uint entityId,
        Vector3 targetPosition,
        bool isFlaggedTarget = false)
    {
        Vector3 diff = targetPosition - observer.Position;
        float distance = diff.Length();

        // 1. Distance Attenuation (Actor Dormancy beyond combat horizon)
        if (distance > observer.MaxCombatRadius && !isFlaggedTarget)
        {
            return new EntityRelevancyPacket(
                entityId,
                targetPosition,
                RelevancyFidelityTier.Dormant,
                180.0f,
                distance,
                0.0f,
                0.0f,
                false,
                false
            );
        }

        // Target at exact observer position
        if (distance < 0.001f)
        {
            return new EntityRelevancyPacket(
                entityId,
                targetPosition,
                RelevancyFidelityTier.FocalFov,
                0.0f,
                0.0f,
                BaseServerTickRateHz,
                BaseServerTickRateHz * FullPacketSizeBytes,
                true,
                true
            );
        }

        Vector3 targetDir = Vector3.Normalize(diff);
        Vector3 forwardNorm = Vector3.Normalize(observer.ForwardDirection);

        // Dot product between camera forward vector and target direction
        float dot = Math.Clamp(Vector3.Dot(forwardNorm, targetDir), -1.0f, 1.0f);
        float angleDegrees = MathF.Acos(dot) * (180.0f / MathF.PI);

        float halfFov = observer.FovAngleDegrees * 0.5f;

        // 2. Focal Field of View Zone (Direct Vision)
        if (angleDegrees <= halfFov || (isFlaggedTarget && distance < 35.0f))
        {
            float tickHz = BaseServerTickRateHz;
            float bandwidth = tickHz * FullPacketSizeBytes;
            return new EntityRelevancyPacket(
                entityId,
                targetPosition,
                RelevancyFidelityTier.FocalFov,
                angleDegrees,
                distance,
                tickHz,
                bandwidth,
                RenderMesh: true,
                SimulateBones: true
            );
        }

        // 3. Peripheral Vision Zone (LOD1 Decimated Replication)
        if (angleDegrees <= halfFov + 35.0f)
        {
            float tickHz = 30.0f;
            float bandwidth = tickHz * PeripheralPacketSizeBytes;
            return new EntityRelevancyPacket(
                entityId,
                targetPosition,
                RelevancyFidelityTier.Peripheral,
                angleDegrees,
                distance,
                tickHz,
                bandwidth,
                RenderMesh: true,
                SimulateBones: false // Bone ticks culled, static/baked animation
            );
        }

        // 4. Flank / Close Proximity Hearing Perimeter (e.g. Footsteps behind player within 18m)
        if (distance <= 18.0f)
        {
            float tickHz = 10.0f;
            float bandwidth = tickHz * FlankPacketSizeBytes;
            return new EntityRelevancyPacket(
                entityId,
                targetPosition,
                RelevancyFidelityTier.FlankProxy,
                angleDegrees,
                distance,
                tickHz,
                bandwidth,
                RenderMesh: false, // Frustum culled from rendering
                SimulateBones: false
            );
        }

        // 5. Blind Spot / Behind Back (Actor Dormancy / Zero Bandwidth)
        return new EntityRelevancyPacket(
            entityId,
            targetPosition,
            RelevancyFidelityTier.Dormant,
            angleDegrees,
            distance,
            0.0f,
            0.0f,
            RenderMesh: false,
            SimulateBones: false
        );
    }

    /// <summary>
    /// Processes an entire massive PvP arena battle (e.g. 500 fighters) and aggregates
    /// network bandwidth and CPU simulation reduction telemetry.
    /// </summary>
    public ArenaRelevancyMetrics EvaluateArenaPvP(
        ObserverState observer,
        IEnumerable<(uint Id, Vector3 Position)> arenaCombatants)
    {
        int total = 0;
        int focal = 0;
        int peripheral = 0;
        int flank = 0;
        int dormant = 0;

        float optimizedBandwidthBytesSec = 0.0f;

        foreach (var (id, pos) in arenaCombatants)
        {
            total++;
            var packet = CalculateEntityRelevancy(observer, id, pos);
            optimizedBandwidthBytesSec += packet.BandwidthBytesPerSec;

            switch (packet.Tier)
            {
                case RelevancyFidelityTier.FocalFov:
                    focal++;
                    break;
                case RelevancyFidelityTier.Peripheral:
                    peripheral++;
                    break;
                case RelevancyFidelityTier.FlankProxy:
                    flank++;
                    break;
                case RelevancyFidelityTier.Dormant:
                    dormant++;
                    break;
            }
        }

        // Unoptimized baseline: Every client receives 60Hz * 180B for all combatants
        float unoptimizedBandwidthBytesSec = total * BaseServerTickRateHz * FullPacketSizeBytes;

        float unoptKbps = (unoptimizedBandwidthBytesSec * 8.0f) / 1024.0f;
        float optKbps = (optimizedBandwidthBytesSec * 8.0f) / 1024.0f;

        float bandwidthReduction = unoptimizedBandwidthBytesSec > 0.0f
            ? (1.0f - (optimizedBandwidthBytesSec / unoptimizedBandwidthBytesSec)) * 100.0f
            : 0.0f;

        // CPU Simulation Savings: Only FocalFov simulates bones (100%), others are dormant or baked (0%)
        float cpuSavings = total > 0
            ? (1.0f - ((float)focal / total)) * 100.0f
            : 0.0f;

        return new ArenaRelevancyMetrics(
            total,
            focal,
            peripheral,
            flank,
            dormant,
            unoptKbps,
            optKbps,
            bandwidthReduction,
            cpuSavings
        );
    }
}
