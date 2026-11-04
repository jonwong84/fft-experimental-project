namespace Zodiac.Content.Definitions.Item
{
    /// <summary>Immutable weapon behavior switches; compatibility switches do not grant unit abilities.</summary>
    public sealed record WeaponBehaviorToggles
    {
        public bool Striking { get; init; }
        public bool Lunging { get; init; }
        public bool Direct { get; init; }
        public bool Arc { get; init; }
        public bool DualWield { get; init; }
        public bool Doublehand { get; init; }
        public bool Throwable { get; init; }
        public bool ForcedDoublehand { get; init; }
    }
}
