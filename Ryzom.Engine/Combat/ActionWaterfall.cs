using Ryzom.Core.Math3D;
using Ryzom.Engine.Entities;

namespace Ryzom.Engine.Combat;

public enum DamageType : byte
{
    Acid = 1,
    Cold = 2,
    Rot = 3,
    Fire = 4,
    Shock = 5,
    Poison = 6,
    Slash = 7,
    Pierce = 8,
    Blunt = 9
}

public enum ActionExecutionStatus : byte
{
    Success = 0,
    InsufficientSap = 1,
    InsufficientStamina = 2,
    TargetOutOfRange = 3,
    TargetDead = 4,
    CasterDead = 5
}

public class ActionStanza
{
    public string Name { get; set; } = string.Empty;
    public int SapCost { get; set; } = 0;
    public int StaminaCost { get; set; } = 0;
    public float RangeMeters { get; set; } = 25.0f;
    public int BasePower { get; set; } = 150;
    public DamageType DamageType { get; set; } = DamageType.Fire;
    public float CastTimeSeconds { get; set; } = 1.5f;
    public float CooldownSeconds { get; set; } = 2.0f;
}

public record ActionResult(
    ActionExecutionStatus Status,
    int DamageDealt,
    int TargetRemainingHp,
    bool TargetKilled);

public static class ActionWaterfall
{
    public static ActionResult ExecuteAction(RyzomEntity caster, RyzomEntity target, ActionStanza stanza)
    {
        if (!caster.IsAlive)
            return new ActionResult(ActionExecutionStatus.CasterDead, 0, target.HitPoints, false);

        if (!target.IsAlive)
            return new ActionResult(ActionExecutionStatus.TargetDead, 0, target.HitPoints, false);

        float distSq = Vector3f.DistanceSquared(caster.Position, target.Position);
        if (distSq > stanza.RangeMeters * stanza.RangeMeters)
            return new ActionResult(ActionExecutionStatus.TargetOutOfRange, 0, target.HitPoints, false);

        if (stanza.SapCost > 0 && !caster.ConsumeSap(stanza.SapCost))
            return new ActionResult(ActionExecutionStatus.InsufficientSap, 0, target.HitPoints, false);

        if (stanza.StaminaCost > 0 && !caster.ConsumeStamina(stanza.StaminaCost))
            return new ActionResult(ActionExecutionStatus.InsufficientStamina, 0, target.HitPoints, false);

        // Apply base damage
        int damage = stanza.BasePower;
        target.TakeDamage(damage);

        return new ActionResult(
            ActionExecutionStatus.Success,
            damage,
            target.HitPoints,
            !target.IsAlive);
    }
}
