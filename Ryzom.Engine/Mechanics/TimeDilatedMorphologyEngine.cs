using Ryzom.Core.Math3D;

namespace Ryzom.Engine.Mechanics;

public record LifeCycleCareerProfile(
    string RoleName,
    float DailySwingsAverage,
    float DailySpellCastsAverage,
    MagicElement FavoredElement,
    float DailySunExposureHours,
    int AnnualCombatSkirmishes
);

public record ContinuousMorphologyState(
    float ChronologicalAgeYears,
    float DominantArmScale,
    float ShoulderBreadthScale,
    float SkinWeatheringPatina,
    float HairMelaninGreying,
    int AccumulatedMicroScars,
    Vector3f EyeIrisColor,
    float OcularLuminescence,
    float PosturalCompression,
    string VisualDescription
);

public class TimeDilatedMorphologyEngine
{
    private float _chronologicalAgeYears;
    private long _cumulativeSwings;
    private long _cumulativeSpellCasts;
    private float _cumulativeSunHours;
    private int _accumulatedMicroScars;
    private MagicElement _dominantElement = MagicElement.None;

    // Subtle, continuous allometric rate constants (non-dramatic, gradual adaptation)
    private const float HypertrophyRateConstant = 0.0000045f;
    private const float MaxArmHypertrophy = 0.28f;       // Max +28% muscle volume over career
    private const float MaxShoulderSpread = 0.12f;      // Max +12% shoulder breadth
    private const float WeatheringRateConstant = 0.000035f;
    private const float GreyingOnsetAge = 35.0f;
    private const float GreyingFullAge = 62.0f;

    public float ChronologicalAgeYears => _chronologicalAgeYears;
    public long CumulativeSwings => _cumulativeSwings;
    public long CumulativeSpellCasts => _cumulativeSpellCasts;
    public int AccumulatedMicroScars => _accumulatedMicroScars;

    public TimeDilatedMorphologyEngine(float startingAgeYears = 20.0f)
    {
        _chronologicalAgeYears = Math.Max(16.0f, startingAgeYears);
    }

    public void SimulateTimeDilation(float yearsElapsed, LifeCycleCareerProfile profile)
    {
        if (yearsElapsed <= 0f) return;

        float days = yearsElapsed * 365.25f;
        _chronologicalAgeYears += yearsElapsed;

        // Continuous accumulation of routine interactions
        long newSwings = (long)(profile.DailySwingsAverage * days);
        long newSpells = (long)(profile.DailySpellCastsAverage * days);
        float newSunHours = profile.DailySunExposureHours * days;
        int newSkirmishes = (int)(profile.AnnualCombatSkirmishes * yearsElapsed);

        _cumulativeSwings += newSwings;
        _cumulativeSpellCasts += newSpells;
        _cumulativeSunHours += newSunHours;

        if (profile.FavoredElement != MagicElement.None)
        {
            _dominantElement = profile.FavoredElement;
        }

        // Probabilistic skirmish scars (approx 1 permanent micro-scar per 4 skirmishes)
        _accumulatedMicroScars += newSkirmishes / 4;
    }

    public void FineTunePlayerAction(string actionType, float intensity = 1.0f)
    {
        // Slow micro-metabolic adaptation for live player gameplay
        if (actionType.Equals("MeleeSwing", StringComparison.OrdinalIgnoreCase))
        {
            _cumulativeSwings += Math.Max(1, (long)intensity);
        }
        else if (actionType.StartsWith("Cast", StringComparison.OrdinalIgnoreCase))
        {
            _cumulativeSpellCasts += Math.Max(1, (long)intensity);
        }
    }

    public ContinuousMorphologyState EvaluateMorphology()
    {
        // 1. Asymptotic Musculoskeletal Hypertrophy (Smooth logarithmic saturation)
        float armGrowthProgress = 1.0f - MathF.Exp(-HypertrophyRateConstant * _cumulativeSwings);
        float armScale = 1.0f + (MaxArmHypertrophy * armGrowthProgress);
        float shoulderScale = 1.0f + (MaxShoulderSpread * armGrowthProgress);

        // 2. Continuous Environmental Weathering (UV exposure, desert sand abrasion)
        float weathering = 1.0f - MathF.Exp(-WeatheringRateConstant * _cumulativeSunHours);

        // 3. Natural Follicle Greying based on life-cycle timeline
        float greying = 0.0f;
        if (_chronologicalAgeYears > GreyingOnsetAge)
        {
            greying = Math.Clamp((_chronologicalAgeYears - GreyingOnsetAge) / (GreyingFullAge - GreyingOnsetAge), 0.0f, 1.0f);
        }

        // 4. Subtle Postural Compression (veteran stoic stance under heavy armor)
        float postureCompression = Math.Clamp((_chronologicalAgeYears - 20.0f) * 0.0018f, 0.0f, 0.06f);

        // 5. Ocular Elemental Tinting (Gradual deep iris saturation from career spell channeling)
        Vector3f baseEye = new(0.35f, 0.22f, 0.14f); // Natural dark chestnut
        Vector3f targetEye = _dominantElement switch
        {
            MagicElement.Fire => new Vector3f(0.95f, 0.45f, 0.08f), // Ember amber
            MagicElement.Cold => new Vector3f(0.20f, 0.75f, 0.95f), // Cryo cyan
            MagicElement.Shock => new Vector3f(0.80f, 0.30f, 0.95f), // Ion purple
            MagicElement.RotSap => new Vector3f(0.25f, 0.88f, 0.30f), // Atys sap green
            _ => baseEye
        };

        float spellInfusion = 1.0f - MathF.Exp(-0.000008f * _cumulativeSpellCasts);
        Vector3f eyeColor = Vector3f.Lerp(baseEye, targetEye, spellInfusion);
        float ocularGlow = spellInfusion * 0.45f; // Soft ambient inner glow

        string ageCategory = _chronologicalAgeYears switch
        {
            < 28.0f => "Youthful Journeyman",
            < 45.0f => "Seasoned Outpost Veteran",
            < 60.0f => "Grizzled Master Overseer",
            _ => "Venerable Atys Elder"
        };

        string desc = $"{ageCategory} ({_chronologicalAgeYears:F1} yrs) | Arm Scale: {armScale:F3}x | Sun Patina: {(weathering * 100):F0}% | Grey Factor: {(greying * 100):F0}% | Micro-Scars: {_accumulatedMicroScars}";

        return new ContinuousMorphologyState(
            ChronologicalAgeYears: _chronologicalAgeYears,
            DominantArmScale: armScale,
            ShoulderBreadthScale: shoulderScale,
            SkinWeatheringPatina: weathering,
            HairMelaninGreying: greying,
            AccumulatedMicroScars: _accumulatedMicroScars,
            EyeIrisColor: eyeColor,
            OcularLuminescence: ocularGlow,
            PosturalCompression: postureCompression,
            VisualDescription: desc
        );
    }
}
