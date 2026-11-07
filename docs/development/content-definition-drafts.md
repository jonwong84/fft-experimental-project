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
verified traits without activating these drafts. [Readable AI authoring](../behavior/readable-ai-authoring.md)
now stores semantic values directly, with original-byte encoding at the boundary.

When a gameplay/content milestone needs these drafts, trace the selected fields,
resolve naming and original/custom boundaries, choose immutable configuration
and versioned adapters, and add behavior coverage before wiring them into rules.
Similarly named AI preferences, absolute restrictions and execution flags must
retain their distinct source meanings. Do not infer gameplay behavior from an
FFTPatcher label or from these property names alone.

## Equipment-break slot selection deferred

Owner direction on 2026-11-06: `EquipmentSlot` was created in anticipation of
Knight's Break Helm, Break Armor, Break Weapon and Break Shield selecting an
equipment slot through an apparently hard-coded original value. Preserve this
intent when selecting their equipment-break formula/content work.

The owner subsequently requested the [ZOD-419 source investigation](../architecture/equipment-effect-selection.md).
It pins the original selector/callers and establishes right-first shields and
highest-required-level weapons with right ties. Menu slots, battle locations and
action masks are separate encodings. Missing-item fallback also differs between
Break and Steal. The enum remains unchanged and inactive: its ordinals are not
source slot numbers, and MainHand/OffHand must not imply Weapon/Shield. Managed
equipment effects still need typed selection policies, explicit compatibility
mapping and independently validated implementation before wiring in the draft.

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
