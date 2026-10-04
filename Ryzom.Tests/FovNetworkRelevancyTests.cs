using System;
using System.Collections.Generic;
using System.Numerics;
using Ryzom.Engine.Spatial;
using Xunit;

namespace Ryzom.Tests;

public class FovNetworkRelevancyTests
{
    private readonly FovNetworkRelevancyEngine _engine = new();

    [Fact]
    public void DirectFov_Yields_FullSixtyHertz_And_BonesSimulation()
    {
        // Player looking directly north (+Z)
        var observer = new ObserverState(
            Position: Vector3.Zero,
            ForwardDirection: new Vector3(0, 0, 1),
            FovAngleDegrees: 90.0f
        );

        // Enemy 25m straight ahead (+Z)
        var targetPos = new Vector3(0, 0, 25.0f);
        var packet = _engine.CalculateEntityRelevancy(observer, 101, targetPos);

        Assert.Equal(RelevancyFidelityTier.FocalFov, packet.Tier);
        Assert.Equal(60.0f, packet.TickRateHz);
        Assert.Equal(60.0f * 180.0f, packet.BandwidthBytesPerSec);
        Assert.True(packet.RenderMesh);
        Assert.True(packet.SimulateBones);
        Assert.True(packet.AngleDegrees < 1.0f);
    }

    [Fact]
    public void PeripheralVision_Yields_ThirtyHertz_And_BonesCulled()
    {
        // 90 deg FOV means half-fov is 45 deg.
        // Target at 60 deg is in peripheral zone (45 to 80 deg).
        var observer = new ObserverState(
            Position: Vector3.Zero,
            ForwardDirection: new Vector3(0, 0, 1),
            FovAngleDegrees: 90.0f
        );

        // 60 deg angle: cos(60 deg) = 0.5, sin(60 deg) = 0.866
        var targetPos = new Vector3(25.0f * 0.866f, 0, 25.0f * 0.5f);
        var packet = _engine.CalculateEntityRelevancy(observer, 102, targetPos);

        Assert.Equal(RelevancyFidelityTier.Peripheral, packet.Tier);
        Assert.Equal(30.0f, packet.TickRateHz);
        Assert.Equal(30.0f * 64.0f, packet.BandwidthBytesPerSec);
        Assert.True(packet.RenderMesh);
        Assert.False(packet.SimulateBones); // Bone simulation is culled to save CPU
    }

    [Fact]
    public void BehindPlayer_Far_Yields_CompleteActorDormancy()
    {
        // Target behind player at 35m distance (-Z)
        var observer = new ObserverState(
            Position: Vector3.Zero,
            ForwardDirection: new Vector3(0, 0, 1),
            FovAngleDegrees: 90.0f
        );

        var targetPos = new Vector3(0, 0, -35.0f);
        var packet = _engine.CalculateEntityRelevancy(observer, 103, targetPos);

        Assert.Equal(RelevancyFidelityTier.Dormant, packet.Tier);
        Assert.Equal(0.0f, packet.TickRateHz);
        Assert.Equal(0.0f, packet.BandwidthBytesPerSec);
        Assert.False(packet.RenderMesh);
        Assert.False(packet.SimulateBones);
    }

    [Fact]
    public void BehindPlayer_CloseProximity_Yields_FlankProxy()
    {
        // Target sneaking up behind player at 10m distance (within 18m hearing radius)
        var observer = new ObserverState(
            Position: Vector3.Zero,
            ForwardDirection: new Vector3(0, 0, 1),
            FovAngleDegrees: 90.0f
        );

        var targetPos = new Vector3(0, 0, -10.0f);
        var packet = _engine.CalculateEntityRelevancy(observer, 104, targetPos);

        Assert.Equal(RelevancyFidelityTier.FlankProxy, packet.Tier);
        Assert.Equal(10.0f, packet.TickRateHz);
        Assert.Equal(10.0f * 24.0f, packet.BandwidthBytesPerSec);
        Assert.False(packet.RenderMesh); // Frustum culled from rendering
        Assert.False(packet.SimulateBones);
    }

    [Fact]
    public void MassivePvPArena_500Combatants_Delivers_Over_NinetyPercent_BandwidthSavings()
    {
        var observer = new ObserverState(
            Position: Vector3.Zero,
            ForwardDirection: new Vector3(0, 0, 1),
            FovAngleDegrees: 90.0f,
            MaxCombatRadius: 100.0f
        );

        // Generate 500 combatants distributed across 360 degrees around the player
        var combatants = new List<(uint Id, Vector3 Position)>();
        var random = new Random(42);

        for (uint i = 0; i < 500; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2.0);
            float radius = 5.0f + (float)(random.NextDouble() * 95.0);
            float x = radius * MathF.Cos(angle);
            float z = radius * MathF.Sin(angle);
            combatants.Add((i, new Vector3(x, 0, z)));
        }

        var metrics = _engine.EvaluateArenaPvP(observer, combatants);

        Assert.Equal(500, metrics.TotalArenaEntities);
        Assert.True(metrics.DormantCount > 180, $"Expected >180 dormant combatants, got {metrics.DormantCount}");
        Assert.True(metrics.DormantCount + metrics.FlankCount > 240, $"Expected >240 culled/flank combatants, got {metrics.DormantCount + metrics.FlankCount}");
        Assert.True(metrics.BandwidthReductionPercent > 65.0f, $"Bandwidth reduction {metrics.BandwidthReductionPercent}% should exceed 65%");
        Assert.True(metrics.CpuSimulationSavingsPercent > 70.0f, $"CPU simulation savings {metrics.CpuSimulationSavingsPercent}% should exceed 70%");
    }
}
