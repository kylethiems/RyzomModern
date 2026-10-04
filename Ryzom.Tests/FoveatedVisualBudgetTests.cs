using Ryzom.Engine.Render;
using Xunit;

namespace Ryzom.Tests;

public class FoveatedVisualBudgetTests
{
    private readonly FoveatedVisualBudgetEngine _engine = new();

    [Fact]
    public void SixtyFps_FoveatedTrimming_Recovers_SignificantHeadroom()
    {
        var analysis = _engine.CalculateFrameBudget(
            cadence: TargetCadence.Fps60,
            visibleRatio: 0.30f,
            focalRatioOfVisible: 0.40f,
            enableGpuHiZCulling: true
        );

        // Baseline should be close to saturating 16.67ms (around 16.2ms)
        Assert.True(analysis.BaselineUnoptimized.TotalFrameTimeMs > 15.0f);

        // Foveated base before upgrades should drop to ~7-9ms
        Assert.True(analysis.FoveatedOptimized.TotalFrameTimeMs < 10.0f);

        // Recovered headroom should exceed 6.0 ms
        Assert.True(analysis.RecoveredHeadroomMs > 6.0f, $"Expected >6ms recovered, got {analysis.RecoveredHeadroomMs}ms");
    }

    [Fact]
    public void CinematicUpgrades_AreReallocated_WhileMaintainingSixtyFps()
    {
        var analysis = _engine.CalculateFrameBudget(
            cadence: TargetCadence.Fps60,
            visibleRatio: 0.30f,
            focalRatioOfVisible: 0.40f,
            enableGpuHiZCulling: true
        );

        // Reallocated cinematic upgrades should be funded with >3ms of compute
        Assert.True(analysis.Upgrades.TotalUpgradeInvestmentMs > 3.0f);
        Assert.True(analysis.Upgrades.MicroClusterTessellationMs > 0.8f);
        Assert.True(analysis.Upgrades.VolumetricSubsurfaceScatteringMs > 0.8f);
        Assert.True(analysis.Upgrades.ScreenSpaceGlobalIlluminationMs > 0.5f);

        // Must still meet 60 FPS target budget (16.667ms) with at least 1.5ms safety headroom
        Assert.True(analysis.MeetsTargetCadence);
        Assert.True(analysis.FinalFrameTimeWithCinematicsMs <= 16.667f);
        Assert.True(analysis.RemainingSafetyHeadroomMs >= 1.5f);
        Assert.True(analysis.EffectiveFps >= 60.0f);
    }

    [Fact]
    public void OneTwentyFpsCadence_MaintainsStrictBudget()
    {
        var analysis = _engine.CalculateFrameBudget(
            cadence: TargetCadence.Fps120,
            visibleRatio: 0.25f,
            focalRatioOfVisible: 0.35f,
            enableGpuHiZCulling: true
        );

        Assert.Equal(8.333f, analysis.TargetBudgetMs);
        Assert.True(analysis.MeetsTargetCadence);
        Assert.True(analysis.FinalFrameTimeWithCinematicsMs <= 8.333f);
        Assert.True(analysis.EffectiveFps >= 120.0f);
    }

    [Fact]
    public void GpuHiZCulling_ReducesCullOverheadVsCpuSoftwareQueries()
    {
        var gpuAnalysis = _engine.CalculateFrameBudget(enableGpuHiZCulling: true);
        var cpuAnalysis = _engine.CalculateFrameBudget(enableGpuHiZCulling: false);

        Assert.True(gpuAnalysis.FoveatedOptimized.VisibilityAndCullingMs < cpuAnalysis.FoveatedOptimized.VisibilityAndCullingMs);
        Assert.Equal(0.35f, gpuAnalysis.FoveatedOptimized.VisibilityAndCullingMs);
        Assert.Equal(0.85f, cpuAnalysis.FoveatedOptimized.VisibilityAndCullingMs);
    }
}
