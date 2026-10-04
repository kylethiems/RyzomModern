using System;
using System.Collections.Generic;

namespace Ryzom.Engine.AI
{
    public enum InjuryLocation
    {
        VisorFace,
        ChestPlate,
        RightArm,
        LeftLeg
    }

    public enum InjuryType
    {
        KitinMandibleSlash,
        AcidSpitBurn,
        BluntCrush
    }

    public record SomaticVisualState(
        double BleedSeverity,       // 1.0 (fresh red bleed) -> 0.0 (healed)
        double ScabOpacity,         // 0.0 -> 1.0 (crusted dark scab) -> 0.0
        double PermanentScarFactor, // 0.0 -> 0.45 (weathered silvery battle scar)
        double LimpGaitFactor,      // Locomotion penalty
        bool NeedsBandage,
        string VisualDescription
    );

    public class SomaticInjury
    {
        public string InjuryId { get; }
        public InjuryType Type { get; }
        public InjuryLocation Location { get; }
        public double Severity { get; } // 1.0 (minor) to 5.0 (critical)
        public DateTime InflictedAtUtc { get; }
        public double HealingRateLambda { get; } // Recovery rate per hour

        public SomaticInjury(InjuryType type, InjuryLocation location, double severity, DateTime inflictedAt)
        {
            InjuryId = Guid.NewGuid().ToString("N")[..8];
            Type = type;
            Location = location;
            Severity = Math.Clamp(severity, 1.0, 5.0);
            InflictedAtUtc = inflictedAt;
            HealingRateLambda = 0.08 / Severity; // Severe wounds heal slower
        }

        public SomaticVisualState EvaluateVisualState(DateTime currentUtc)
        {
            double elapsedHours = Math.Max(0.0, (currentUtc - InflictedAtUtc).TotalHours);
            
            // Allometric recovery curve: R(t) = 1.0 - exp(-lambda * t)
            double healingProgress = 1.0 - Math.Exp(-HealingRateLambda * elapsedHours);

            double bleedSeverity = Math.Max(0.0, 1.0 - (healingProgress * 2.5));
            double scabOpacity = (healingProgress > 0.2 && healingProgress < 0.85) ? Math.Sin(healingProgress * Math.PI) : 0.0;
            double permanentScar = Math.Clamp(healingProgress * 0.45, 0.0, 0.45);
            double limpFactor = Location == InjuryLocation.LeftLeg ? Math.Max(0.0, (1.0 - healingProgress) * 0.7) : 0.0;
            bool needsBandage = healingProgress < 0.6;

            string desc = healingProgress switch
            {
                < 0.25 => $"Fresh open {Type} on {Location} (Active Bleeding)",
                < 0.60 => $"Bandaged {Type} on {Location} (Granulating tissue)",
                < 0.90 => $"Crusted scab over {Location} (Healing closing)",
                _ => $"Healed silvery battle scar on {Location} (Permanent Veteran Mark)"
            };

            return new SomaticVisualState(bleedSeverity, scabOpacity, permanentScar, limpFactor, needsBandage, desc);
        }
    }

    public class SomaticProfile
    {
        public List<SomaticInjury> ActiveInjuries { get; } = new();

        public void InflictInjury(InjuryType type, InjuryLocation location, double severity, DateTime timestamp)
        {
            ActiveInjuries.Add(new SomaticInjury(type, location, severity, timestamp));
        }

        public SomaticVisualState GetAggregateVisualState(DateTime currentUtc)
        {
            if (ActiveInjuries.Count == 0)
            {
                return new SomaticVisualState(0.0, 0.0, 0.0, 0.0, false, "Uninjured, pristine condition");
            }

            double maxBleed = 0.0;
            double maxScab = 0.0;
            double maxScar = 0.0;
            double maxLimp = 0.0;
            bool needsBandage = false;
            string latestDesc = string.Empty;

            foreach (var injury in ActiveInjuries)
            {
                var state = injury.EvaluateVisualState(currentUtc);
                if (state.BleedSeverity > maxBleed) maxBleed = state.BleedSeverity;
                if (state.ScabOpacity > maxScab) maxScab = state.ScabOpacity;
                if (state.PermanentScarFactor > maxScar) maxScar = state.PermanentScarFactor;
                if (state.LimpGaitFactor > maxLimp) maxLimp = state.LimpGaitFactor;
                if (state.NeedsBandage) needsBandage = true;
                latestDesc = state.VisualDescription;
            }

            return new SomaticVisualState(maxBleed, maxScab, maxScar, maxLimp, needsBandage, latestDesc);
        }
    }
}
