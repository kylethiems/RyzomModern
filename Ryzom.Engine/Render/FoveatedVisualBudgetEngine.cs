using System;

namespace Ryzom.Engine.Render;

/// <summary>
/// Target display refresh cadence and hardware time budget.
/// </summary>
public enum TargetCadence
{
    Fps60 = 0,   // 16.67 ms frame budget
    Fps120 = 1   // 8.33 ms frame budget
}

/// <summary>
/// Breakdown of GPU hardware time per render pass in milliseconds.
/// </summary>
public record RenderPassBreakdown(
    float VisibilityAndCullingMs,
    float GeometryPassMs,
    float LightingAndShadowsMs,
    float PostFxAndVolumetricsMs,
    float TotalFrameTimeMs
);

/// <summary>
/// Fidelity upgrade allocations enabled by recovered GPU headroom.
/// </summary>
public record CinematicUpgradesAllocation(
    float MicroClusterTessellationMs,     // Film-grade bark micro-geometry in focal view (~128 tri clusters)
    float VolumetricSubsurfaceScatteringMs,// Deep skin/foliage light transport in focal view
    float ScreenSpaceGlobalIlluminationMs, // Multi-bounce diffuse ambient radiance in focal view
    float SoftPenumbraContactShadowsMs,   // Variable penumbra contact-hardening shadows
    float TotalUpgradeInvestmentMs
);

/// <summary>
/// Comparative frame time telemetry contrasting unculled baseline vs foveated visual reallocation.
/// </summary>
public record FrameBudgetAnalysis(
    float TargetBudgetMs,
    RenderPassBreakdown BaselineUnoptimized,
    RenderPassBreakdown FoveatedOptimized,
    float RecoveredHeadroomMs,
    CinematicUpgradesAllocation Upgrades,
    float FinalFrameTimeWithCinematicsMs,
    float RemainingSafetyHeadroomMs,
    bool MeetsTargetCadence,
    float EffectiveFps
);

/// <summary>
/// Hardware Frame Time Budget & Foveated Visual Reallocation Engine.
/// Reallocates GPU compute milliseconds saved from view frustum trimming,
/// Hi-Z cluster culling, and variable rate shading directly into focal cinematic fidelity.
/// </summary>
public class FoveatedVisualBudgetEngine
{
    public FrameBudgetAnalysis CalculateFrameBudget(
        TargetCadence cadence = TargetCadence.Fps60,
        float visibleRatio = 0.30f,         // 30% of world is within the camera's active view
        float focalRatioOfVisible = 0.40f,  // 40% of the visible area is in the high-density focal center
        bool enableGpuHiZCulling = true)
    {
        float targetBudgetMs = cadence == TargetCadence.Fps60 ? 16.667f : 8.333f;

        // 1. Baseline Unoptimized Frame Pipeline (No aggressive cluster culling, omnidirectional processing)
        float baseCullingMs = 0.10f; // Minimal coarse CPU AABB checks
        float baseGeomMs = cadence == TargetCadence.Fps60 ? 5.40f : 2.70f;
        float baseLightMs = cadence == TargetCadence.Fps60 ? 6.60f : 3.30f;
        float basePostMs = cadence == TargetCadence.Fps60 ? 4.10f : 2.05f;
        float baseTotalMs = baseCullingMs + baseGeomMs + baseLightMs + basePostMs;

        var baseline = new RenderPassBreakdown(
            baseCullingMs,
            baseGeomMs,
            baseLightMs,
            basePostMs,
            baseTotalMs
        );

        // 2. Foveated Optimized Pipeline with Cluster Culling & Variable Rate Shading
        // GPU Compute Hi-Z occlusion and cluster bounding box cull overhead
        float optCullMs = enableGpuHiZCulling ? 0.35f : 0.85f; // Compute shader dispatch costs 0.35ms

        // Geometry pass scales with visible clusters: 70% discarded before rasterization
        float optGeomMs = baseGeomMs * (visibleRatio + 0.05f); // Micro-cluster culling leaves ~35% geometry

        // Lighting pass scales with Variable Rate Shading (VRS):
        // Focal 100% full shading + Peripheral 2x2 quad downsampled + Culled 0%
        float foveatedShadingWeight = (visibleRatio * focalRatioOfVisible * 1.0f) +
                                      (visibleRatio * (1.0f - focalRatioOfVisible) * 0.40f);
        float optLightMs = baseLightMs * MathF.Max(0.25f, foveatedShadingWeight + 0.15f);

        float optPostMs = basePostMs * 0.85f; // Screen-space post-fx only on active viewport

        float optTotalBeforeUpgrades = optCullMs + optGeomMs + optLightMs + optPostMs;
        float recoveredHeadroomMs = MathF.Max(0.0f, baseTotalMs - optTotalBeforeUpgrades);

        var foveatedBase = new RenderPassBreakdown(
            optCullMs,
            optGeomMs,
            optLightMs,
            optPostMs,
            optTotalBeforeUpgrades
        );

        // 3. Reallocation into Focal Cinematic Upgrades
        // Reinvest ~75% of recovered headroom into cinema-grade rendering techniques
        float reinvestmentCapMs = MathF.Min(recoveredHeadroomMs * 0.75f, targetBudgetMs - optTotalBeforeUpgrades - 2.0f);
        reinvestmentCapMs = MathF.Max(0.0f, reinvestmentCapMs);

        float tessellationMs = reinvestmentCapMs * 0.32f;
        float subsurfaceMs = reinvestmentCapMs * 0.30f;
        float ssgiMs = reinvestmentCapMs * 0.22f;
        float softShadowsMs = reinvestmentCapMs * 0.16f;
        float totalUpgradesMs = tessellationMs + subsurfaceMs + ssgiMs + softShadowsMs;

        var upgrades = new CinematicUpgradesAllocation(
            tessellationMs,
            subsurfaceMs,
            ssgiMs,
            softShadowsMs,
            totalUpgradesMs
        );

        float finalFrameTime = optTotalBeforeUpgrades + totalUpgradesMs;
        float safetyHeadroom = targetBudgetMs - finalFrameTime;
        bool meetsCadence = finalFrameTime <= targetBudgetMs;
        float effectiveFps = finalFrameTime > 0.0f ? 1000.0f / finalFrameTime : 0.0f;

        return new FrameBudgetAnalysis(
            targetBudgetMs,
            baseline,
            foveatedBase,
            recoveredHeadroomMs,
            upgrades,
            finalFrameTime,
            safetyHeadroom,
            meetsCadence,
            effectiveFps
        );
    }
}
