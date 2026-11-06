namespace Zodiac.Framework;

/// <summary>Positive application facts. Other result work excludes record deltas; derived XP/ability outcomes remain outside this projection.</summary>
public sealed record MagicLivingApplicationProfile(bool ActiveUnitKnown = false, bool TargetReactionsKnownAbsent = false,
    bool GolemAndDamageSharingKnownAbsent = false, bool OtherResultWorkKnownAbsent = false,
    bool OrdinaryStatusRulesKnown = false, bool CharmKnownAbsent = false);
