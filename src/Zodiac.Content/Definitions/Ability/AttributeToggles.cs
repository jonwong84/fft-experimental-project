using Zodiac.Content.Definitions.Common;

namespace Zodiac.Content.Definitions.Ability
{
    internal class AttributeToggles
    {
        public bool ForceSelfTarget { get; set; }
        public bool WeaponRange { get; set; }
        public bool LinearRange { get; set; }
        public bool VerticalThreshold { get; set; }
        public bool WeaponStrike { get; set; }
        public bool AutoTarget { get; set; }
        public bool AllowSelfTarget { get; set; }
        public bool HitEnemies { get; set; }
        public bool HitAllies { get; set; }
        public bool TopDownTarget { get; set; }
        public bool FollowTarget { get; set; }
        public bool RandomFire { get; set; }
        public bool LinearAoe { get; set; }
        public bool ThreeDirections { get; set; }
        public bool HitCaster { get; set; }
        public bool Reflectable { get; set; }
        public bool Arithmetics { get; set; }
        public bool IgnoreSilence { get; set; }
        public bool Mimicable { get; set; }
        public bool NormalAttack { get; set; }
        public bool Performing { get; set; }
        public bool Quote { get; set; }
        public bool AnimateOnMiss { get; set; }
        public bool NaturesWrath { get; set; }
        public bool ReturnMagic { get; set; }
        public bool StopAtObstacle { get; set; }
        public bool BladeGrasp { get; set; }
        public bool RequireSword { get; set; }
        public bool RequireMateriaBlade { get; set; }
        public bool Evadeable { get; set; }
        public bool SelectTarget { get; set; }

        // New custom properties added in Zodiac Content Definitions
        public bool TargetEquipmentSlot { get; set; }
        public EquipmentSlot EquipmentSlotTarget { get; set; } = EquipmentSlot.None;
    }
}
