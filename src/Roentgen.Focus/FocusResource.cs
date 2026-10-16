using Roentgen.Core;
using Roentgen.Rpg;

namespace Roentgen.Focus;

public sealed class FocusResourceState : ICanonicalWritable
{
    public int CurrentMilliunits { get; set; } = 90_000;
    public int MaximumMilliunits { get; set; } = 90_000;
    public int RecoveryPerTickMilliunits { get; set; } = 25;
    public int ReservedMilliunits { get; set; }
    public ulong Revision { get; set; }
    public int AvailableMilliunits => Math.Max(0, CurrentMilliunits - ReservedMilliunits);
    public void ApplyDerivedStatistics(DerivedStatSnapshot statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        int spent = Math.Max(0, MaximumMilliunits - CurrentMilliunits);
        MaximumMilliunits = Math.Max(1, statistics[DerivedStatId.MaximumFocusMilliunits]);
        CurrentMilliunits = Math.Clamp(MaximumMilliunits - spent, ReservedMilliunits, MaximumMilliunits);
        RecoveryPerTickMilliunits = Math.Max(0, statistics[DerivedStatId.FocusRecoveryPerSecondMilliunits] / ProgressionRules.TicksPerSecond);
        Revision++;
    }
    public bool Reserve(int amount) { if(amount<0||amount>AvailableMilliunits)return false; ReservedMilliunits+=amount;Revision++;return true; }
    public void Release(int amount) { ReservedMilliunits=Math.Max(0,ReservedMilliunits-Math.Max(0,amount));Revision++; }
    public void ConsumeReserved(int amount) { amount=Math.Clamp(amount,0,ReservedMilliunits);ReservedMilliunits-=amount;CurrentMilliunits=Math.Max(0,CurrentMilliunits-amount);Revision++; }
    public void Recover(bool allowed) { if(!allowed)return; int next=Math.Min(MaximumMilliunits,CurrentMilliunits+RecoveryPerTickMilliunits);if(next!=CurrentMilliunits){CurrentMilliunits=next;Revision++;} }
    public void WriteCanonical(CanonicalWriter w){w.WriteInt32(CurrentMilliunits);w.WriteInt32(MaximumMilliunits);w.WriteInt32(RecoveryPerTickMilliunits);w.WriteInt32(ReservedMilliunits);w.WriteUInt64(Revision);}
}

public sealed record FocusCostBreakdown(int TotalMilliunits, IReadOnlyList<ForecastFactor> Factors);
public sealed class FocusCostCalculator
{
    public FocusCostBreakdown Calculate(FocusActionKind kind, int selectedPartRadiusMm, int weaponHandlingPermille=1000, int injuryPermille=0, int perkPermille=1000)
    {
        int baseCost = kind switch { FocusActionKind.Fire=>20_000, FocusActionKind.Melee=>24_000, FocusActionKind.Throw=>38_000, FocusActionKind.Reload=>30_000, FocusActionKind.SwitchWeapon=>10_000, _=>20_000};
        int partFactor = Math.Clamp(1600 - selectedPartRadiusMm/2, 900, 1700);
        int injuryFactor = 1000 + Math.Clamp(injuryPermille,0,1000)/3;
        long total=(long)baseCost*Math.Clamp(weaponHandlingPermille,500,2000)/1000*partFactor/1000*injuryFactor/1000*Math.Clamp(perkPermille,500,1500)/1000;
        return new((int)Math.Clamp(total,1000,100_000),[
            new("base",baseCost,"base action time"),new("target-size",partFactor-1000,"selected component size"),
            new("handling",weaponHandlingPermille-1000,"weapon handling"),new("injury",injuryFactor-1000,"actor injury"),new("perks",perkPermille-1000,"Focus modifiers")]);
    }
}
