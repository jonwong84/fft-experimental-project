# Content-backed ordinary AI lists — ZOD-262/263

Implemented 2026-11-04 from managed base `bdd7572`, following the
[ZOD-261 study](../architecture/zod-261-usable-ability-lists.md). Read-only
fft_decomp was checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/usable-ability-lists-source-v1.json) pins
11 functions across seven files and selected prepared executable facts; the
[implementation receipt](../evidence/ai/usable-ability-lists-implementation-v1.json)
records managed validation. No original or C program was executed.

## Owned semantic list construction — ZOD-262

`AiHumanAbilityLists.Build` consumes explicit ordinary-human/default-menu and
clean eligibility/usage context, unit IDs and primary/secondary skillset sources.
Each source copies its semantic action slots, validates distinct nonnegative
indices, then visits them in source slot order. Known is resolved supplied
learning state. Neither job membership, cost nor learnability flags grant it.

The owner inserts Attack, eligible known primary actions, eligible known
secondary actions and Frog Attack. It preserves duplicate ability occurrences
and repeated primary/secondary sets. Empty or AI-disabled slots and actions
not marked known are skipped; a known AI-enabled unsupported action rejects rather than silently
disappearing. Only Accumulate/Stone action kinds and ordinary sword/Frog summaries
are admitted. Monster, inventory, Math, Charge, Jump and other menus are not
approximated by this default-menu path.

The immutable output contains initialized entries and source/slot provenance,
including unusable ordinary non-Frog Frog Attack. Counts, evasion quarter share,
harmful/helpful counts, trajectory and low initialization policy follow admitted
entry kinds. For Attack/self/Stone/Frog the denominator is4, evasion share2,
harmful2 and helpful1. Adding another self occurrence gives denominator5 and
evasion share1. Initialization is distinct from ordinary usability and the
actor-only second pass. Static EvadeWithMotion never supplies runtime ConsumeAim.
The owner copies inputs, adds no RNG and grants no command authority.

Semantic storage is not an original packed list or a new catalog capacity.
The production consumer below separately restricts accepted list shapes. The
original 34-entry traversal/conditional terminator and out-of-profile storage
behavior are not generalized by an unbounded managed collection.

## Outer content setup and fresh preparation — ZOD-263

`OrdinaryHumanAiAbilityListSetup.Create` in Content accepts the selected metadata,
closed immediate/targeted effect companions, existing base-job setup and explicit
per-unit `OrdinaryHumanAiUnitSelection`. It validates companion identity/profile,
the selected job/primary-skillset instances, secondary references and known slots.
Known state must resolve exactly the setup units and nonempty selected slots.
Repeated skillset selections share the same known-slot fact but retain both
list occurrences. It reads the original first16 human action positions in this
compatibility adapter only; catalog definitions retain their existing limits.
The adapter does not classify arbitrary stable IDs using SerializedClassRaw.

Existing raw AI bytes are decoded here into semantic inputs. AI-disabled metadata
remains disabled even with an executable effect and a known-slot selection.
Known enabled self/Stone bindings must match the traced first three AI bytes;
static byte4 may differ in EvadeWithMotion only. No ability-name inference,
secondary-record default, metadata schema migration or owner-flag consolidation
is introduced. A noncanonical eligible effect rejects instead of guessing.

`AccumulateAiPreparationConfiguration.AbilityLists` optionally carries this owned
data. With it present, preparation ignores `BothInitializedLists` and validates
the actual list for every combatant: Attack → self → Stone → Frog, exact request
IDs and exact executable grants. Missing, duplicated, reordered or extra actions
reject as UnsupportedList before refresh, even if the old certificate is true.
Without owned lists the original certified entry and results remain supported.

Admitted lists derive initialization/usable summaries, actor scan/consume policy,
enemy threat counts and minimum reaches. Post-ACT still computes summaries/reach
before clearing actor usability. Existing refresh, scenarios, entry evaluation,
ranking, lottery, real commands, priority knowledge and recovery remain owners;
no repeated refresh or cached search result is introduced. The semantic builder
can describe other lists, but production preparation intentionally rejects those
until the variable-list search child is selected and validated.

Content now references AI as an outer setup consumer. AI still references only
Framework and never depends on Content, client/editor or file I/O; Framework
does not reference AI. The existing dependency regression remains unchanged.
Tests reference Content to exercise this composition. Lock updates add project
dependencies only, with no external package version/hash changes. The client/
editor can transitively carry the assembly, but no running-game AI activation,
editor UI or deployment behavior was selected.

## Validation and remaining work

Twelve new managed cases cover slot/source ordering, holes/duplicates and same-set
secondary entries, initialized denominators, known versus disabled/unsupported,
input isolation, missing context, content identities and malformed grants, zero
synthetic AI bytes, static-motion separation, unsupported-list rejection and
canonical before/after-ACT parity for both admitted teams and actor identities.

Literal preparation starts at word1/count7 and ends word1103527590/count8. A
Move0/word7 self turn ends word2073554906/count34, actual PA6, with two fresh
decisions and published priority knowledge. The retained range3/Move1 case with
16 draws remaining publishes its first Move and fresh priority0, rejects the
seven-draw retained ACT, and resumes only that failed stage with unchanged
knowledge, no new decisions/events and no repeated selection.

Locked restore and full Release build pass with zero warnings/errors;
**4,272 .NET cases** (1,467 Framework, 1,060 AI), **539 Python tests**, pinned
source/prepared-data, algorithm-guide and coverage checks pass. Logs and seven
full TRX reports are in ignored `scratch/ability-lists-261-263/`. Follow the
[existing Python/Pillow setup](accumulate-ai.md#validation). Historical checkpoint
snapshots were not rerun. No original behavioral equivalence or UI review claim.

Next ZOD-264 traces variable supported-action lists through preparation/search
before broadening the four-entry consumer. General learning rows, special
menus, full rosters/effects/maps/policies, terminal encounter routing and frame
RNG remain separate. Owner drafts including AbilityToggles, further editor work,
replacement interfaces and ZOD-106 stay deferred and unchanged.
