# Minimal job and ability definition slice (ZOD-003)

Implementation follow-up: [ZOD-017's verified local import](original-metadata-import.md) now resolves the
selected retail records and Squire-to-Basic-Skill edge. The original design/evidence limits below describe
what was known during ZOD-003; raw imported records and generated packages remain under ignored scratch.

Date: 2026-10-22. Design and static source study only; no importer, catalog, combat rule or production schema is implemented. All decomp paths and symbols below refer to the read-only local checkout `C:\repositories\fft_decomp` at **`611b504d25d4a9b03fab8023d7dbea75fe381d5d`**, observed with `git rev-parse HEAD`. This is the SCUS-94221 compatibility investigation; no BIN edition/hash or original-game execution was verified in this task.

## Selected slice and evidence boundary

Select the generic **Squire**, `JOB_ID_SQUIRE = 74` (`0x4a`), rather than any named character's Squire. Its proposed stable ID is `fft.ps1:job/squire`. The minimal content workflow is inspecting one job's identity, movement values and ordered ability references, then inspecting an ability's learning metadata. Combat resolution, progression, job unlocking, equipment assignment, sprites and effect formulas remain separate slices.

Use **Basic Skill**, `SKILLSET_ID_BASIC_SKILL = 5`, proposed ID `fft.ps1:skillset/basic-skill`, as the candidate command set; **Accumulate**, `ABILITY_ID_BASIC_SKILL_ACCUMULATE = 146` (`0x92`), proposed ID `fft.ps1:ability/accumulate`, is the candidate ordinary ability. These numeric/name associations are established by `include/fft/job.h`, `skillset.h` and `ability.h`. They do **not** independently prove that retail job row 74 contains skillset 5, or that any particular slot of row 5 contains ability 146.

The repository declares the source tables but a search for their definitions/initializers found no C row values. `target/main.yaml` labels `g_job_data` at `0x800610b8`, `g_ability_human_skillset_abilities` at `0x80064a94`, `g_ability_data` at `0x8005ebf0`, and `g_ability_range_data` at `0x8005fbf0`. These are runtime addresses, **not** BIN offsets. The design deliberately leaves the Squire-to-Basic-Skill edge, actual slot membership, JP costs, innate abilities and all selected retail row bytes unresolved until a verified local import reads them. No familiar community values are substituted for evidence.

This is sufficient to choose a bounded schema/import milestone. It is not a completed retail Squire catalog or evidence that Accumulate executes correctly. If the imported row differs, preserve it and revise this candidate selection rather than correcting the data to match the proposal.

## Job record map

`include/fft/job.h::job_data_t` asserts a 48-byte (`0x30`) record; index 74 starts at table-relative byte `74 * 48 = 3552` (`0xde0`). `job_get_data` in `src/main/party.c` uses the index directly below 160, with no negative-index guard. An importer must validate `0 <= index < 160`; do not copy the unsafe indexing into a content API. Table extent is an original import constraint, not a maximum for new authored content.

Each field below gives original offset/width and proposed interpretation. Actual Squire values are unresolved, including zero/nonzero masks.

- **`0x00`, `skillset`:** unsigned byte, 0..255. Resolve through a profile-specific index-to-stable-ID map into `primarySkillsetId`. Human rows are 0..175; monster rows 176..223. ENTD uses 255 as none and has separate override logic; do not globally reinterpret all zero/255 bytes as an empty row without context. Consumer: `unit_copy_job_data`; `unit_calculate_encounter_abilities` can replace the runtime primary skillset when ENTD specifies neither 0 nor 255.
- **`0x01..0x08`, `innate_abilities`:** eight unsigned bytes encoding four little-endian unsigned 16-bit values, each raw range 0..65535. Decode to ordered `innateAbilityIds` only after validating references/sentinels; retain all four raw words. Consumer: `unit_calculate_encounter_abilities` reconstructs `low + (high << 8)` in its four-iteration loop; `unit_copy_job_data` copies the bytes. The usual ability space is 0..511, but that is not proof every 16-bit pattern is valid. Zero handling and out-of-range values require import diagnostics, not truncation.
- **`0x09..0x0c`, `equipment_categories`:** four unsigned mask bytes. Item type `t` (0..31) is enabled by `bytes[t >> 3] & (0x80 >> (t & 7))` (`include/fft/item.h`). Proposed semantic field: `equippableItemTypeIds`; preserve unclassified bits in the source envelope. Consumer: `unit_copy_job_data`, then `unit_set_equippable_items` in `party.c` modifies runtime permissions from abilities. Do not store the resulting unit permission mask back in the definition.
- **`0x0d..0x16`, `stat_growths`:** five pairs of unsigned bytes in **HP, MP, Speed, PA, MA** order; pair members `growth` then `multiplier`, each 0..255. Proposed future `statGrowths` keyed by those five names. Consumer: `unit_copy_job_growths_and_multipliers` and `unit_copy_job_data`. No growth formula, divisor validity, percentage interpretation or derived stat is claimed here; the next minimal schema can leave this block opaque.
- **`0x17`, `move`:** unsigned byte, 0..255; proposed `baseMove`. `unit_copy_job_data` copies it to runtime `move`; later equipment/ability processing can alter effective movement. Definition value is not effective battle movement.
- **`0x18`, `jump`:** unsigned byte, value bits `0x7f` (0..127) plus `0x80` stepping-stone flag. Proposed `baseJump` and `steppingStone`; preserve original packed byte. `unit_copy_job_data` masks the value and writes the flag to `unit->position.stepping_stone`. This field does not define the complete height/slope rule; see [movement inputs](movement-inputs.md).
- **`0x19`, `class_evade`:** unsigned byte, 0..255; proposed future `classEvadeRaw`, without imposing an unevidenced 0..100 validation rule. Consumer: `unit_set_equipment_stats` in `party.c` assigns `UNIT_EQUIPMENT_STAT_CLASS_PHYSICAL_EVADE` from this field.
- **`0x1a..0x28`, `status_sets`:** three five-byte unsigned masks, **innate, immunity, starting**, as `include/fft/status.h::initial_status_sets_t`. Proposed future separate definition masks. `unit_copy_job_data` copies the block to the unit's innate/immunity/current storage and can add immunity for immortal formation units. Starting definition statuses and current battle statuses must not share mutable storage. Individual status bits are outside this slice and stay opaque until separately mapped.
- **`0x29..0x2c`, `elemental_affinity`:** four unsigned bytes, each 0..255 as a bit mask; proposed future raw affinity groups. `unit_copy_job_data` copies these four groups, then explicitly clears the fifth, runtime-only `ELEMENTAL_AFFINITY_STRENGTHEN` group. There is no fifth Strengthen byte in the job record; preserve all four source bytes. Per-element mapping is deferred.
- **`0x2d`, `spritesheet_id`; `0x2e`, `job_portrait_palette`; `0x2f`, `monster_graphic`:** three unsigned bytes, each 0..255, copied by `unit_copy_job_data`. Keep as opaque presentation references in the import envelope until their asset lookup is traced; do not treat them as engine identities or user text.

## Skillset references and order

`src/main/party.c::ability_human_skillset_t` is three high-bit bytes followed by 22 low ID bytes (25 bytes total). The candidate row 5 starts at table-relative byte 125. Slots 0..15 are action slots; 16..21 are reaction/support/movement slots (`include/fft/skillset.h`). Slot number is part of compatibility data, not merely display order.

For slot `n` in 0..21, `ability_get_id_from_skillset` computes:

`low[n] | ((high[n / 8] << ((n % 8) + 1)) & 0x100)`.

Thus each ID is unsigned 9-bit, 0..511, with high bits read MSB first. The last high byte's low two bits do not contribute to the 22 slots; preserve them. Ability 0 is omitted by the ability-list consumer, so represent an imported empty slot explicitly as `abilityId: null` plus raw ID 0. Do not compact, sort or deduplicate slots: learned bits address the original slots. Preserve duplicate references if present.

`ability_store_skillset_abilities` emits 24 `u16` entries; slots 22 and 23 become zero through the lookup's upper-bound check. These are scratch-list/learned-bit conventions, not 24 independently encoded human slots. Negative slot or skillset inputs lack lower-bound guards in the original; managed import validation must reject those. Skillsets >=256 are redirected to row 0 by this caller, while direct lookup returns zero for IDs >=224. Preserve these distinctions if implementing those compatibility entry points later.

The list filter masks are action `0x01`, reaction `0x02`, movement `0x04`, support `0x08`; the random R/S/M selector reverses support/movement (`0x04` support, `0x08` movement). Do not share a serialized enum between them. An observable boundary quirk: `ability_store_skillset_abilities` treats **ID 486** as support (`<= ABILITY_ID_MOVEMENT_FIRST`), whereas `ability_get_data_and_type` classifies 486 as movement. ID 486 is named Move+1. Record that discrepancy rather than fixing it in a universal category property.

`src/event/jobstts/job_abilities.c::ability_list_build` obtains the job skillset, applies category ID ranges, and consumes 24 MSB-first learned bits for the selected career job. Entries with ID zero are omitted only when building the presentation list. Source slot membership and per-unit learning therefore need distinct representations.

## Minimum ability record map

`include/fft/ability.h::ability_data_t` asserts eight bytes. Candidate ability 146 starts at table-relative byte 1168 (`0x490`). Proposed `AbilityDefinition` contains identity and only the following learning metadata, with no effect execution promise:

- **`0x00..0x01`, `jp_cost`:** unsigned 16-bit, 0..65535; proposed `jpCost`. Preserve raw value even if it exceeds a unit's normal JP cap. `formation_ability_learn_list` in `src/world/ability_lists.c` deducts cost from the unit's job JP after confirmation.
- **`0x02`, `learn_rate`:** unsigned byte, 0..255; proposed `learnOnHitRateRaw`. Header describes an out-of-100 roll; do not clamp imported byte values to 100. It is not a generic job-learning success percentage. The selected slice does not implement learn-on-hit.
- **`0x03`, `type_flags`:** unsigned byte. Low nibble `0x0f` is serialized class 1..9; it is **not** the 0..8 `ability_type_e` returned by ID-range classification. Keep `serializedClassRaw`. Confirmed high flags: `0x20` learn on hit, `0x40` show name, `0x80` **cannot** learn with JP. Proposed `learnOnHit`, `showName`, `canLearnWithJp = (raw & 0x80) == 0`. Bit `0x10` is unnamed/unresolved. `ability_list_build` checks bit 7 to disable/hide unlearned entries; do not reverse its polarity based on a friendly editor label. Unknown class nibble values remain raw and diagnostic.
- **`0x04..0x07`, `ai_flags`:** four unsigned bytes. Keep `aiFlagsRaw`; individual bit meanings are not promoted by this slice. `ability_ai_flags_t` notes that AI working storage later replaces flags_4 with usage flags; static definitions must not hold that working record.

`ability_get_data_and_type` first masks an input with `0x1ff` and then selects a secondary table by ID range, rather than trusting the serialized nibble. For 146 it chooses `g_ability_range_data[146]`, a 14-byte secondary record at table-relative byte 2044 (`0x7fc`). Keep this secondary record opaque in the import envelope for now. Its byte offsets are range 0, AoE 1, vertical 2, flags 3..6, element 7, formula 8, x 9, y 10, status-infliction ID 11, CT 12, MP cost 13; all unsigned bytes. Unknown formula parameters and untraced flags are not zero/default behavior. This avoids incorrectly treating representable learning metadata as a usable battle ability. Implementing Accumulate requires a separate formula/target/status trace and behavioral vectors.

## Definition/state boundary and proposed schema

Following [ADR 0004](../adr/0004-zodiac-engine-and-fft-game-boundary.md), these contracts belong to `Fft.*`, not `Zodiac.Engine`. Immutable definitions do not contain unit HP/MP, current job, equipped items, job level, current/total JP, learned slots, runtime skillset overrides, effective movement, AI scratch data or current statuses. A unit refers to definitions by stable ID; career state stores its own job-keyed progression and learning. Original career slot 0 is the base job (including special characters), not globally synonymous with job index 74. `unit_career_data_t` encodes 20 levels as paired nibbles and JP as unsigned 16-bit current/total tables; these are future save/import concerns, not fields on `JobDefinition`.

The minimum next package schema should have a distinct `format: "fft.definition-design"`, `schemaVersion: 1`, explicit profile ID, `scope: "partial"`, and evidence kind. Required collections:

- `jobs`: unique stable `id`, optional imported `sourceIndex`, `primarySkillsetId`, `baseMove` (0..255), `baseJump` (0..127), `steppingStone`, and ordered innate ability references. No inference from localized names.
- `skillsets`: unique stable `id`, optional imported `sourceIndex`, ordered slots with unique explicit `sourceSlot` (0..21 for this human profile) and nullable `abilityId`. Imported baseline definitions require all 22 slots; a synthetic partial design sample declares its partial scope.
- `abilities`: unique stable `id`, optional imported `sourceIndex`, `jpCost` (0..65535), `learnOnHitRateRaw` (0..255), `serializedClassRaw` (0..15), `learnOnHit`, `showName`, `canLearnWithJp`, and four `aiFlagsRaw` bytes. This is metadata, not executable battle content.

Require ordinal unique IDs and resolved cross-references; validate integer widths, duplicate slots/IDs, known profile/version, full imported slot coverage and source-index uniqueness within each table. Do not accept arbitrary unknown JSON fields. A future implementation must distinguish a partial metadata package from battle-ready content and refuse to execute missing rules. New authored definitions do not need source indices or original table-size limits.

An importer-side provenance envelope (separate from semantic definition objects) must retain **all original 48 job bytes, 25 human skillset bytes, eight primary ability bytes and 14 secondary bytes**, original table/index/offset, full decomp SHA, edition/hash verification metadata and decode version. Keep game-derived bytes and manifests under ignored `scratch/`; commit only permitted metadata/synthetic examples. Decoded fields must agree with raw bytes; unknown masks may be exposed read-only and preserved on export, never cleared on edit. Do not claim byte round-trip until tests demonstrate it. Runtime uses prepared validated definitions and never reads the BIN or calls the decomp.

The inline sample below is invented schema-design data with a single explicit slot. It intentionally uses `sample:*` IDs and no source indices, so it cannot be mistaken for a reconstructed Squire row or a complete baseline package. It is **not** an `fft.behavior-fixture` and makes no state/event assertion under [fixture v1](fixture-format.md).

```json
{
  "format": "fft.definition-design",
  "schemaVersion": 1,
  "profileId": "sample:profile/job-metadata",
  "scope": "partial",
  "evidenceKind": "synthetic-expectation",
  "jobs": [{
    "id": "sample:job/recruit",
    "primarySkillsetId": "sample:skillset/practice",
    "baseMove": 3,
    "baseJump": 2,
    "steppingStone": false,
    "innateAbilityIds": []
  }],
  "skillsets": [{
    "id": "sample:skillset/practice",
    "slots": [{ "sourceSlot": 0, "abilityId": "sample:ability/focus" }]
  }],
  "abilities": [{
    "id": "sample:ability/focus",
    "jpCost": 25,
    "learnOnHitRateRaw": 0,
    "serializedClassRaw": 1,
    "learnOnHit": false,
    "showName": true,
    "canLearnWithJp": true,
    "aiFlagsRaw": [0, 0, 0, 0]
  }]
}
```

## Community-tool decision and next gate

No FFTPatcher checkout, executable, documentation or remote page was consulted. Therefore no exact FFTPatcher revision or PS1 edition-support assertion is made, and no tool field name is treated as authoritative. **Decision: source-reference only for this slice; reuse no FFTPatcher code, binary schema or runtime dependency.** A later optional comparison must pin its repository commit/release, inspect licensing before reuse, verify SCUS-94221 support and compare selected fields against this C layout and verified local bytes. Compare JP-learning polarity, serialized type versus ID classification, skillset packing/order and Jump's high bit in particular. Friendly labels/workflows can inform editor usability after that check; they do not establish behavior equivalence.

Next implementation scope: a bounded local importer/validator for the four selected record kinds, retaining raw bytes and emitting the minimal metadata above. Before claiming baseline content, verify the user BIN's edition/hash, resolve runtime labels to prepared-file offsets, capture Squire row 74 and its actual skillset/ability references locally, and replace the unresolved edges with evidence-backed metadata. Expand only the referenced learning definitions, not all jobs or all combat formulas. Keep uncertain bits opaque. ZOD-014/015/016 can use this design while treating retail-row verification as an explicit implementation gate.

## Checks and limits

The trace inspected headers, `party.c` lookup/initialization and equipment consumers, JOBSTTS ability-list learning checks, WORLD learning workflow and MAIN table labels at the pinned revision. Arithmetic for record offsets, packed ID decoding and flag masks was hand-checked. The synthetic JSON was parsed and its references/ranges checked separately. No source executable, emulator, BIN import, fixture runner or gameplay tests ran; documentation changes do not alter production behavior. Static source tracing is not original-game equivalence.
