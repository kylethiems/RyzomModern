using System;
using Ryzom.Core.Math3D;

namespace Ryzom.Engine.Ecology;

public enum TimberGrade
{
    GreenBranchwood,    // Common young wood (bows, handles)
    FlexibleSapwood,    // Standard flexible wood (armor backing, shields)
    SeasonedHeartwood,  // High-density aged core (master staves, cuirasses)
    AmberCrystallized   // Ancient high-DBH prime timber (legendary artifacts)
}

public record TreeMetrics(
    Vector3f Position,
    float DbhInches,
    float HeightMeters,
    float TaperFactor,
    float SapVolumeLiters,
    TimberGrade PrimeGrade,
    int BoardFeetYield);

/// <summary>
/// Botanical Forest Vegetation Simulator (FVS) Stem Taper & Living Flora Engine.
/// Derived from the Swanson Bid Pilot ITD (Individual Tree Detection) algorithms.
/// Computes physical tree diameter, height, taper curves, and harvestable timber/sap yields
/// dynamically based on terrain slope and Topographic Position Index (TPI).
/// </summary>
public static class BotanicalTaperEngine
{
    /// <summary>
    /// Computes accurate botanical growth metrics for Atys giant root flora.
    /// Trees growing in sheltered, low-TPI benches develop thicker trunks and higher heartwood yields,
    /// while exposed ridge crowns produce dense, taper-hardened flexible wood.
    /// </summary>
    public static TreeMetrics CalculateTree(
        Vector3f position,
        float slopeDegrees,
        float topographicPositionIndex,
        float ageYears = 50.0f)
    {
        // 1. Diameter at Breast Height (DBH in inches)
        // Mid-slope benches (low TPI) nurture wider girth, ridges limit trunk diameter
        float baseDbh = 24.0f - (topographicPositionIndex * 4.5f);
        float ageGain = MathF.Log(MathF.Max(1.0f, ageYears)) * 6.5f;
        float dbh = Math.Clamp(baseDbh + ageGain - (slopeDegrees * 0.15f), 6.0f, 120.0f);

        // 2. FVS Taper Factor: exp(-0.03 * DBH)
        float taperFactor = MathF.Exp(-0.025f * dbh);

        // 3. Tree Height (Allometric scaling law H = 1.3 + (a * DBH) / (b + DBH))
        float height = Math.Clamp(1.3f + (55.0f * dbh) / (25.0f + dbh), 8.0f, 65.0f);

        // 4. Board-Feet Volume Yield (Scribner Decimal C Log Rule approximation)
        // Volume = 0.79 * (DBH - 2)^2 * (Height / 16)
        float usableLength = height * (1.0f - taperFactor * 0.4f);
        float scribnerMbf = MathF.Max(10.0f, 0.079f * MathF.Pow(dbh - 2.0f, 2.0f) * (usableLength / 16.0f));
        int boardFeet = (int)MathF.Round(scribnerMbf * 10.0f);

        // 5. Sap Volume (resinous vitality proportional to crown volume and wetness)
        float sapLiters = Math.Clamp(dbh * 0.85f * (1.0f - slopeDegrees / 90.0f), 5.0f, 150.0f);

        // 6. Quality Grade Classification based on DBH maturity
        TimberGrade grade = dbh switch
        {
            > 70.0f => TimberGrade.AmberCrystallized,
            > 45.0f => TimberGrade.SeasonedHeartwood,
            > 25.0f => TimberGrade.FlexibleSapwood,
            _ => TimberGrade.GreenBranchwood
        };

        return new TreeMetrics(position, dbh, height, taperFactor, sapLiters, grade, boardFeet);
    }
}
