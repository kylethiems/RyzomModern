using Ryzom.Engine.Combat;
using Ryzom.Engine.Stanzas;

namespace Ryzom.Tests;

public class StanzaGrammarTests
{
    [Fact]
    public void CompileRecipe_ScalesDamageWithQuality()
    {
        var lowQuality = new StanzaRecipe { QualityLevel = 20, Element = DamageType.Cold };
        var highQuality = new StanzaRecipe { QualityLevel = 100, Element = DamageType.Cold };

        var lowAction = StanzaGrammarEngine.CompileRecipe(lowQuality);
        var highAction = StanzaGrammarEngine.CompileRecipe(highQuality);

        Assert.True(highAction.BasePower > lowAction.BasePower);
        Assert.True(highAction.SapCost > lowAction.SapCost);
        Assert.Equal(DamageType.Cold, highAction.DamageType);
    }

    [Fact]
    public void CompileRecipe_AreaOfEffect_IncreasesSapCost()
    {
        var single = new StanzaRecipe { Target = TargetBrickType.SingleTarget, QualityLevel = 50 };
        var aoe = new StanzaRecipe { Target = TargetBrickType.AreaOfEffect, QualityLevel = 50 };

        var singleAction = StanzaGrammarEngine.CompileRecipe(single);
        var aoeAction = StanzaGrammarEngine.CompileRecipe(aoe);

        Assert.True(aoeAction.SapCost > singleAction.SapCost);
    }

    [Fact]
    public void CompileRecipe_InvalidQuality_ThrowsOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StanzaGrammarEngine.CompileRecipe(new StanzaRecipe { QualityLevel = 0 }));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            StanzaGrammarEngine.CompileRecipe(new StanzaRecipe { QualityLevel = 300 }));
    }
}
