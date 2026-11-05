# Deferred content definition drafts

The owner-authored types below are preserved in version control as design drafts.
They are compiled with the content project but have no active import, package,
editor or rules consumers; `AttributeToggles` only references the draft
`EquipmentSlot` enum. Their presence does not establish implemented gameplay,
verified original mappings, complete authoring support or a stable public API.

- `Definitions/Ability/AbilityToggles.cs`
- `Definitions/Ability/AiBehaviorToggles.cs`
- `Definitions/Ability/AttributeToggles.cs`
- `Definitions/Common/EquipmentSlot.cs`
- `Definitions/Common/StatusToggles.cs`
- `Definitions/Item/ShopAvailabilityToggles.cs`
- `Definitions/Job/EquipmentOptionToggles.cs`
- `Definitions/Status/EffectBehaviorToggles.cs`
- `Definitions/Status/StatusSelectionToggles.cs`

Paths are relative to `src/Zodiac.Content/`. Cleanup preserved these files
byte-for-byte rather than renaming, merging or deleting them.

## Ability consolidation deferred

Owner direction on 2026-11-04: leave `AbilityToggles` unchanged and revisit its
consolidation later in development. Its LearnWithJp, DisplayAbilityName and
LearnOnHit properties overlap the implemented `AbilityMetadataDefinition`.
The latter remains authoritative for current learning metadata; the unused
draft is not a second active configuration source. UnknownTypeBit4 remains
opaque. [Semantic AI metadata](../behavior/semantic-ai-metadata.md) now derives
verified traits from the preserved AI bytes without activating these drafts.

When a gameplay/content milestone needs these drafts, trace the selected fields,
resolve naming and original/custom boundaries, choose immutable configuration
and versioned adapters, and add behavior coverage before wiring them into rules.
Similarly named AI preferences, absolute restrictions and execution flags must
retain their distinct source meanings. Do not infer gameplay behavior from an
FFTPatcher label or from these property names alone.

## Active definitions

The following are implemented content models rather than the unused drafts above:

- [Ability metadata](../architecture/zod-014-typed-definitions.md): immutable
  learning and derived AI configuration with original packing in adapters.
- [Weapon behavior and elements](../behavior/retail-sword-representation.md):
  immutable semantic records, retaining the current supported sword domain and
  unchanged package schemas 1/2. Other WeaponType values do not activate rules.
- [JobStats and limits](content-limits.md): constructor-validated Move/Jump with
  construction-only growth/multiplier/evade initialization. Additional stats
  remain scaffolding without import, package or gameplay support.

This organization selects no further editor work, battle-save feature or broad
semantic-flag migration. Existing backlog deferrals remain in effect.
