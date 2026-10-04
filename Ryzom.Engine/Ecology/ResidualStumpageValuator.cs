using System;

namespace Ryzom.Engine.Ecology;

public record LogisticsAppraisal(
    TimberGrade Grade,
    float DeliveredPondValue,
    float YardingExtractionCost,
    float MektoubHaulCost,
    float OutpostRoadMaint,
    float BreakEvenResidualStumpage,
    float MarketOfferPrice);

/// <summary>
/// Economic Valuation & Trade Logistics Engine.
/// Derived from the Swanson Bid Pilot Residual Stumpage and Haul Cycle calculators.
/// Models the real-world delivered economics of raw materials transported by Mektoub caravans.
/// </summary>
public static class ResidualStumpageValuator
{
    /// <summary>
    /// Calculates the economic residual stumpage value of harvested wood, bark, or sap.
    /// Deducts logging extraction difficulty (slope yarding), Mektoub haul transit costs,
    /// and outpost trail maintenance from the central city delivered pond value.
    /// </summary>
    public static LogisticsAppraisal AppraiseHarvest(
        TimberGrade grade,
        float slopeDegrees,
        float distanceToOutpostKm,
        float caravanRiskFactor = 1.0f,
        float targetMarginPct = 12.0f)
    {
        // 1. Delivered City Pond Value (base wholesale market value at major capital)
        float pondValue = grade switch
        {
            TimberGrade.AmberCrystallized => 950.0f,
            TimberGrade.SeasonedHeartwood => 550.0f,
            TimberGrade.FlexibleSapwood => 320.0f,
            _ => 160.0f
        };

        // 2. Yarding & Logging Extraction Cost
        // Steeper slopes require complex cable rigging / dangerous cliff extraction
        float baseLogging = 80.0f;
        float slopePenalty = (slopeDegrees / 45.0f) * 65.0f;
        float loggingCost = baseLogging + slopePenalty;

        // 3. Mektoub Caravan Haul Cost
        // Haul cycle time scales with distance, steep mountain grades, and bandit/kitin threat level
        float baseHaulPerKm = 14.5f;
        float roadGradeFactor = 1.0f + (slopeDegrees / 60.0f);
        float haulCost = distanceToOutpostKm * baseHaulPerKm * roadGradeFactor * caravanRiskFactor;

        // 4. Outpost Road & Trail Maintenance
        float roadMaint = MathF.Max(10.0f, distanceToOutpostKm * 2.2f);

        // 5. Statutory Atys Empire Tariff (Guild trade tax)
        float statutoryTax = pondValue * 0.035f;

        // 6. Calculate Residual Stumpage (Breakeven)
        // Lumber Breakeven = Pond Value - Logging - Haul - Maintenance - Tax
        float breakEven = pondValue - loggingCost - haulCost - roadMaint - statutoryTax;

        // 7. Calculate "The Number" (Final profitable market offer to foragers)
        float theNumber = MathF.Max(15.0f, breakEven * (1.0f - (targetMarginPct / 100.0f)));

        return new LogisticsAppraisal(
            grade,
            pondValue,
            loggingCost,
            haulCost,
            roadMaint,
            breakEven,
            theNumber);
    }
}
