namespace Zodiac.Content.Definitions.Common
{
    using System.Text.Json.Serialization;

    /// <summary>Immutable element switches; use a with expression to create an edited copy.</summary>
    public sealed record ElementToggles
    {
        public bool Fire { get; init; }
        public bool Ice { get; init; }
        public bool Lightning { get; init; }
        public bool Wind { get; init; }
        public bool Earth { get; init; }
        public bool Water { get; init; }
        public bool Holy { get; init; }
        public bool Dark { get; init; }

        // New elements added in Zodiac
        public bool Gravity { get; init; }
        public bool Poison { get; init; }
        public bool Restore { get; init; }

        /// <summary>True when no element is enabled; neutral is not stored as a separate switch.</summary>
        [JsonIgnore]
        public bool Neutral =>
            !(Fire || Ice || Lightning || Wind || Earth ||
              Water || Holy || Dark || Gravity || Poison || Restore);
    }
}
