using System.Collections.Generic;
using Ryzom.Engine.Audio;
using Xunit;

namespace Ryzom.Tests;

public class AudioFileModernizationTests
{
    private readonly AudioFileModernizationEngine _engine = new();

    [Fact]
    public void ModernizeAsset_UpsamplesSampleRate_AndReconstructsHarmonics()
    {
        // 2004 Legacy Ogg Vorbis 22.05 kHz sound file
        var profile = _engine.ModernizeAsset(
            assetId: "sfx_combat_sword_swing_01",
            legacyFormat: LegacyAudioFormat.OggVorbis22k,
            modernFormat: ModernAudioFormat.NeuralSuperRes96k
        );

        Assert.Equal(22050, profile.OriginalSampleRateHz);
        Assert.Equal(96000, profile.RestoredSampleRateHz);
        Assert.Equal(24, profile.RestoredBitDepth);
        Assert.True(profile.HasBweHighFrequencySynthesis);
        Assert.True(profile.SnrImprovementDb > 18.0f);
        Assert.True(profile.DynamicRangeExpansionDb > 20.0f);
    }

    [Fact]
    public void PartitionAudioStems_DifferentiatesCombatVsMagicSpectrograms()
    {
        var swordStems = AudioFileModernizationEngine.PartitionAudioStems("sfx_blade_greatsword_slash");
        var spellStems = AudioFileModernizationEngine.PartitionAudioStems("sfx_spell_fireball_ignite");
        var ambientStems = AudioFileModernizationEngine.PartitionAudioStems("ambient_wind_pyr_dunes");

        // Sword blade has sharp attack transient
        Assert.True(swordStems.TransientEnergyRatio > 0.50f);
        Assert.True(swordStems.TransientEnergyRatio > swordStems.EnvironmentTailRatio);

        // Spell has deep acoustic sustain and lingering tail
        Assert.True(spellStems.EnvironmentTailRatio > 0.25f);
        Assert.True(spellStems.BodyResonanceEnergyRatio > 0.40f);

        // Ambient natural sound has balanced distribution
        Assert.Equal(0.40f, ambientStems.TransientEnergyRatio);
        Assert.Equal(0.40f, ambientStems.BodyResonanceEnergyRatio);
    }

    [Fact]
    public void BatchProcessLegacyAssets_ComputesStorageSavings_AndAggregateMetrics()
    {
        var legacyAssets = new List<(string, LegacyAudioFormat)>
        {
            ("sfx_axe_chop_root_01", LegacyAudioFormat.OggVorbis22k),
            ("sfx_magic_cold_burst", LegacyAudioFormat.Mp3_44k16Bit),
            ("sfx_kitin_mandible_click", LegacyAudioFormat.AdpcmWav22k),
            ("ambient_prime_roots_drip", LegacyAudioFormat.OggVorbis22k),
            ("voice_fyros_warrior_shout", LegacyAudioFormat.AdpcmWav22k)
        };

        var report = _engine.BatchProcessLegacyAssets(legacyAssets);

        Assert.Equal(5, report.TotalFilesModernized);
        Assert.True(report.AverageSnrGainDb > 15.0f, $"Average SNR gain was {report.AverageSnrGainDb} dB");
        Assert.True(report.MemoryCompressionRatio >= 3.0f);
        Assert.True(report.TotalStorageSavedMb > 5.0f);
        Assert.Equal(5, _engine.ModernizedAssetCount);
    }
}
