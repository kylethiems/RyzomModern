using Ryzom.Core.Math3D;
using Ryzom.Engine.Combat;
using Ryzom.Engine.Entities;

namespace Ryzom.Tests;

public class CombatWaterfallTests
{
    [Fact]
    public void ActionCast_ConsumesSap_AndDamagesTarget()
    {
        var caster = new RyzomEntity { Id = 1, Name = "FyrosMage", Position = new Vector3f(0f, 0f, 0f), Sap = 100 };
        var target = new RyzomEntity { Id = 2, Name = "Kitin", Position = new Vector3f(10f, 0f, 0f), HitPoints = 500 };

        var fireballStanza = new ActionStanza
        {
            Name = "Fireball I",
            SapCost = 25,
            RangeMeters = 20.0f,
            BasePower = 150,
            DamageType = DamageType.Fire
        };

        var result = ActionWaterfall.ExecuteAction(caster, target, fireballStanza);

        Assert.Equal(ActionExecutionStatus.Success, result.Status);
        Assert.Equal(150, result.DamageDealt);
        Assert.Equal(350, target.HitPoints);
        Assert.Equal(75, caster.Sap);
        Assert.False(result.TargetKilled);
    }

    [Fact]
    public void ActionCast_TargetOutOfRange_FailsWithoutCost()
    {
        var caster = new RyzomEntity { Id = 1, Name = "MatisArcher", Position = new Vector3f(0f, 0f, 0f), Stamina = 200 };
        var target = new RyzomEntity { Id = 2, Name = "Gingo", Position = new Vector3f(50f, 0f, 0f) }; // 50m away

        var arrowStanza = new ActionStanza
        {
            Name = "Aimed Shot",
            StaminaCost = 50,
            RangeMeters = 30.0f, // Max 30m
            BasePower = 100
        };

        var result = ActionWaterfall.ExecuteAction(caster, target, arrowStanza);

        Assert.Equal(ActionExecutionStatus.TargetOutOfRange, result.Status);
        Assert.Equal(0, result.DamageDealt);
        Assert.Equal(200, caster.Stamina); // Stamina preserved
    }

    [Fact]
    public void ActionCast_InsufficientSap_FailsExecution()
    {
        var caster = new RyzomEntity { Id = 1, Name = "ZoraiShaman", Position = new Vector3f(0f, 0f, 0f), Sap = 10 };
        var target = new RyzomEntity { Id = 2, Name = "Torback", Position = new Vector3f(5f, 0f, 0f) };

        var shockStanza = new ActionStanza
        {
            Name = "Lightning Strike",
            SapCost = 50,
            RangeMeters = 20.0f,
            BasePower = 200
        };

        var result = ActionWaterfall.ExecuteAction(caster, target, shockStanza);

        Assert.Equal(ActionExecutionStatus.InsufficientSap, result.Status);
        Assert.Equal(10, caster.Sap);
    }
}
