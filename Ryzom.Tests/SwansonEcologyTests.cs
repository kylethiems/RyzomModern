using System;
using Ryzom.Core.Math3D;
using Ryzom.Engine.Ecology;
using Xunit;

namespace Ryzom.Tests;

public class SwansonEcologyTests
{
    [Fact]
    public void BotanicalTaperEngine_CalculatesAccurateFvsMetrics()
    {
        // Tree A: Sheltered valley bench (low slope 5 deg, negative TPI -1.5)
        var valleyTree = BotanicalTaperEngine.CalculateTree(
            new Vector3f(100f, 0f, 100f),
            slopeDegrees: 5.0f,
            topographicPositionIndex: -1.5f,
            ageYears: 120.0f);

        // Tree B: Steep wind-exposed ridge (high slope 38 deg, positive TPI +2.5)
        var ridgeTree = BotanicalTaperEngine.CalculateTree(
            new Vector3f(200f, 50f, 200f),
            slopeDegrees: 38.0f,
            topographicPositionIndex: 2.5f,
            ageYears: 120.0f);

        // Valley tree should develop wider trunk (higher DBH) and higher volume
        Assert.True(valleyTree.DbhInches > ridgeTree.DbhInches,
            $"Valley DBH ({valleyTree.DbhInches}) not greater than ridge DBH ({ridgeTree.DbhInches})");
        Assert.True(valleyTree.BoardFeetYield > ridgeTree.BoardFeetYield);

        // Taper factor should obey FVS exponential decay (between 0.0 and 1.0)
        Assert.True(valleyTree.TaperFactor > 0.0f && valleyTree.TaperFactor < 1.0f);
        Assert.True(ridgeTree.TaperFactor > 0.0f && ridgeTree.TaperFactor < 1.0f);

        // Quality grade checks
        Assert.True(valleyTree.PrimeGrade >= TimberGrade.SeasonedHeartwood);
    }

    [Fact]
    public void ResidualStumpageValuator_DeductsHaulAndLoggingFromPondValue()
    {
        // Outpost A: Near capital (1.5 km), gentle slope (6 deg)
        var nearAppraisal = ResidualStumpageValuator.AppraiseHarvest(
            TimberGrade.SeasonedHeartwood,
            slopeDegrees: 6.0f,
            distanceToOutpostKm: 1.5f);

        // Outpost B: Deep treacherous territory (14.0 km), steep cliff (42 deg)
        var farAppraisal = ResidualStumpageValuator.AppraiseHarvest(
            TimberGrade.SeasonedHeartwood,
            slopeDegrees: 42.0f,
            distanceToOutpostKm: 14.0f,
            caravanRiskFactor: 1.4f);

        Assert.Equal(nearAppraisal.DeliveredPondValue, farAppraisal.DeliveredPondValue);

        // Haul and logging should be significantly higher for the far rugged outpost
        Assert.True(farAppraisal.MektoubHaulCost > nearAppraisal.MektoubHaulCost);
        Assert.True(farAppraisal.YardingExtractionCost > nearAppraisal.YardingExtractionCost);

        // Residual break-even value must be lower for the far rugged harvest
        Assert.True(nearAppraisal.BreakEvenResidualStumpage > farAppraisal.BreakEvenResidualStumpage);
        Assert.True(nearAppraisal.MarketOfferPrice > farAppraisal.MarketOfferPrice);
    }

    [Fact]
    public void EcologicalArbitrageMonitor_TriggersKitinRetaliationOnOverHarvest()
    {
        var monitor = new EcologicalArbitrageMonitor();
        string zone = "Verdant Heights";

        // Initial pristine state
        var initial = monitor.GetZoneStatus(zone);
        Assert.Equal(0.0f, initial.DepletionPercentage);
        Assert.True(initial.CarbonArbitrageViable);
        Assert.Equal(1.0f, initial.KitinSwarmAggressionMultiplier);
        Assert.Equal(0, initial.ActiveKitinRaiders);

        // Heavy clear-cutting harvest (extract 60% of standing biomass)
        monitor.RecordHarvest(zone, initial.InitialBiomassMbf * 0.60f);

        var deforested = monitor.GetZoneStatus(zone);
        Assert.True(deforested.DepletionPercentage >= 59.0f);
        Assert.False(deforested.CarbonArbitrageViable);

        // Ecosystem backlash: aggression multiplier > 1.0 and raiders spawned
        Assert.True(deforested.KitinSwarmAggressionMultiplier > 2.0f);
        Assert.True(deforested.ActiveKitinRaiders > 30);

        // Kami restoration rite: infuse sap back into the root network
        monitor.RestoreBiomass(zone, initial.InitialBiomassMbf * 0.50f);
        var restored = monitor.GetZoneStatus(zone);

        Assert.True(restored.DepletionPercentage < deforested.DepletionPercentage);
        Assert.True(restored.KitinSwarmAggressionMultiplier < deforested.KitinSwarmAggressionMultiplier);
    }
}
