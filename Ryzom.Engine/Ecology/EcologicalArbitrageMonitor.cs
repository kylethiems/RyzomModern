using System;
using System.Collections.Concurrent;

namespace Ryzom.Engine.Ecology;

public record EcoZoneStatus(
    string ZoneName,
    float InitialBiomassMbf,
    float CurrentBiomassMbf,
    float DepletionPercentage,
    bool CarbonArbitrageViable,
    float KitinSwarmAggressionMultiplier,
    int ActiveKitinRaiders);

/// <summary>
/// Ecological Stability & Biomass Arbitrage Engine (Kami Conservation vs Karavan Industry).
/// Derived from the Swanson Bid Pilot Carbon Arbitrage and Standing Biomass models.
/// Dynamically regulates creature aggression and faction favor based on player deforestation rates.
/// </summary>
public class EcologicalArbitrageMonitor
{
    private readonly ConcurrentDictionary<string, (float Initial, float Current)> _zones = new();

    public EcologicalArbitrageMonitor()
    {
        // Initialize core Atys ecological zones
        RegisterZone("The Witherings", 25000f);
        RegisterZone("Aeden Aqueous", 42000f);
        RegisterZone("Verdant Heights", 65000f);
        RegisterZone("Fyros Oases", 12000f);
    }

    public void RegisterZone(string zoneName, float initialBiomassMbf)
    {
        _zones[zoneName] = (initialBiomassMbf, initialBiomassMbf);
    }

    /// <summary>
    /// Records player harvesting in a zone, depleting local standing biomass.
    /// </summary>
    public void RecordHarvest(string zoneName, float mbfExtracted)
    {
        if (_zones.TryGetValue(zoneName, out var data))
        {
            float newCurrent = MathF.Max(100f, data.Current - mbfExtracted);
            _zones[zoneName] = (data.Initial, newCurrent);
        }
    }

    /// <summary>
    /// Records Kami ecological restoration rites (sap infusion to roots).
    /// </summary>
    public void RestoreBiomass(string zoneName, float mbfRestored)
    {
        if (_zones.TryGetValue(zoneName, out var data))
        {
            float newCurrent = MathF.Min(data.Initial * 1.25f, data.Current + mbfRestored);
            _zones[zoneName] = (data.Initial, newCurrent);
        }
    }

    /// <summary>
    /// Evaluates current ecological arbitrage status and Kitin retaliatory threat.
    /// </summary>
    public EcoZoneStatus GetZoneStatus(string zoneName)
    {
        if (!_zones.TryGetValue(zoneName, out var data))
        {
            return new EcoZoneStatus(zoneName, 1000f, 1000f, 0f, true, 1.0f, 0);
        }

        float depletionPct = (1.0f - (data.Current / data.Initial)) * 100.0f;

        // Swanson Carbon Arbitrage Principle:
        // When standing biomass is healthy (depletion < 25%), ecological preservation
        // grants greater long-term economic yield (Kami favor & mana regeneration) than clear-cutting.
        bool carbonArbitrageViable = depletionPct < 25.0f;

        // Ecosystem Backlash:
        // As deforestation exceeds 40%, Kitin swarm hives wake up and launch defensive raids on player outposts.
        float aggressionMultiplier = 1.0f;
        int activeRaiders = 0;

        if (depletionPct > 40.0f)
        {
            aggressionMultiplier = 1.0f + ((depletionPct - 40.0f) / 10.0f);
            activeRaiders = (int)MathF.Round((depletionPct - 40.0f) * 2.5f);
        }

        return new EcoZoneStatus(
            zoneName,
            data.Initial,
            data.Current,
            depletionPct,
            carbonArbitrageViable,
            aggressionMultiplier,
            activeRaiders);
    }
}
