# Learn on hit

ZOD-398–402 implement bounded ordinary-magic learning. The study pins the clean, read-only reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/combat/learn-on-hit-source-v1.json) fingerprints
`outcomes.c`, the turn controller, menu dispatcher and RNG wrapper. This is
source study with independent arithmetic checks, not an observed retail run.

## Source contract

`reward_grant` applies EXP/JP and growth before the learning report phase.
`ability_find_learn_on_hit_unit` exits without scanning for Hamedo, ability 0,
or an ability without primary LearnOnHit. It scans original unit slots in order,
excluding the actor. An Affected marker is **replaced** by LearnChecked before
checking human/reaction eligibility or primary-skillset membership. Already
checked and merely Targeted units are skipped. Dead ordinary hit results retain
Targeted, so cannot qualify through this path.

The current job selects a three-byte learned row: jobs below Squire 74 alias row 0;
otherwise row=job-74. Bits are MSB first, slot 0 at byte 0 bit 7. The source loops
24 bits, but human skillset lookup returns zero beyond slot 21. Each matching
unknown slot draws `rand()*100/32768 < rawRate`; rates 0 and >=100 still draw.
Duplicate ability slots can roll repeatedly until one succeeds. The first
success returns immediately, leaving later units' Affected markers untouched.

The controller creates at most one offer. YES invokes `ability_learn_used`,
which sets **every** matching unknown slot using the row captured before its
walk. NO and UNRESOLVED_FF advance the phase without learning. Neither response
rescans other units. Acceptance spends no JP or RNG. Definitions remain immutable.
Disabled non-player menus follow the source automatic YES path described below;
team color is independent of player control.

## Managed boundary and validation

`LearnOnHit.Scan` is a pure captured-row calculation; it cannot publish battle
state. It orders by source slot and retains slot identity independently of
ability identity. `Accept` unions matching slots. Counter exhaustion throws
without publishing partial candidates. The owned battle/report path and prepared binding are described below.

Eleven managed cases cover marker ordering, first success, duplicates, acceptance,
known/ineligible/already-checked recipients, raw rates 0/29/30/100/255, Hamedo,
disabled metadata and exhaustion. The independent Python audit checks all 32,768
BIOS outputs at seven rates (229,376 comparisons), seed 57 and duplicate-slot
vectors, and row `80 80 04`. Run:

```powershell
python tools/verification/verify_learn_on_hit.py --reference C:/repositories/fft_decomp
dotnet test tests/Zodiac.Framework.Tests/Zodiac.Framework.Tests.csproj -c Release --filter FullyQualifiedName~LearnOnHitTests
```

No reactions, monster combat, job switching, general AI, original report graphics,
presentation-frame RNG or save/checkpoint support is activated by this rule.

## Owned battle reports (ZOD-399)

`OrdinaryMagicBattleDefinition` optionally owns a validated learning catalog,
with exact executable ability coverage and stable current-job primary slots.
Its learning profile admits generic original jobs 74–93; special-job row aliases
stay rejected until separately modeled. Each unit supplies an explicit initial
current-job slot set. Battle state owns immutable per-job learned rows, separate
from immutable initial secondary spell grants. `KnownSpells` derives available
casts from both. Legacy definitions without metadata retain their old path.

Actual spell application runs learning after rewards and growth. Ineligible
affected targets still publish LearnChecked without draws. An internal report
identifies the successful learner, ability, current job and creating command
revision. `AnswerLearning` accepts that exact pending report once or rejects
without mutation. Acceptance sets all matching slots; decline sets none. Both
resume the existing clock phase without rescanning. A pending report gates every
other command; even an instant cast can resume its unfinished active turn.
The authored first-KO objective settles only after the report closes, if present.

Nine composed cases cover reward/growth draw ordering, immutable definitions,
known spells, command gates/stale replies, duplicate acceptance/decline, rate 0,
ineligible marker publication, instant casts, restart and atomic rollback when
the learning draw exhausts the counter. Prepared packages bind these facts as described below.

## Prepared learning content (ZOD-400)

Metadata companions now populate Framework's immutable execution copy from
`AbilityMetadataDefinition.LearnOnHit` and `LearnOnHitRateRaw`, plus source-indexed
primary skillset slots. The old eligible-recipient/repeated-slot guards are
removed. Schema2 converts its initial grants to all matching primary slots;
schema1 retains its explicit legacy effect-only behavior. Schema3 requires an
explicit unique `learnedPrimarySlots` array for every unit, independently of
initial secondary grants. Missing, duplicate, negative, unbound or oversized
slots fail closed. No editable duplicate flag source or AbilityToggles change
is introduced. Main and companion assets remain hash checked and deeply captured.

`prepare-learning-magic-battle` imports only Fire 16 and Fire 4 (ID19) using the same
formula 0x08. Its 37 verified records include unchanged primary rate 30, secondary
power 32, MP 48, CT 10 and area 2, Wizard 80/Black Magic 11 slot 3 and progression records.
The authored scene uses a Wizard learner with HP200/MP75, no spell grants, an
empty current-job row and explicit seed 10. Mage starts with slots 0/3; the seed
makes the first learning report reproducible without modifying the original rate.
The scene's positions, bare equipment, units, seed and first-KO objective are
invented setup, not an original encounter claim.

The independent content audit now accepts `--selection learning` and verifies
both asset hashes, both raw records and metadata, all 37 provenance records,
source slot indices and explicit learner setup. Actual prepared Fire4 acceptance/decline uses the Framework command path;
client display and journal snapshots are described below.

## Player report and replay (ZOD-401)

The ordinary client presents the owned learner/ability report and pauses action
selection until Enter accepts or Back declines. Pointer buttons use those same
routes. The choice is an ordinary recorded Framework command; presentation cannot
construct a report or adopt a learning result. Newly learned primary spells enter
the normal prepared-order magic list immediately. Restart resets learned rows,
reports, career, RNG and the command journal. Snapshots now include the report,
every per-job learned row and derived known spells, sorted deterministically;
receipt hashes therefore intentionally change from the older snapshot shape.

Four client cases exercise acceptance, decline, unrelated intent rejection,
restart, learned spell execution and exact command replay. A finite Windows GPU check on the verified learning package exercises
resized pointer controls, full movement, invalid input, Fire4 HP200→75/MP75→27,
level 11 and draw 14, explicit decline, identical restart/offer, acceptance without
new draws and the learner's normal cast (Mage HP100→22; learner MP75→27).
All six complete checkpoints match headless replay and drawing preserves state.
Offer/result captures were visually inspected. The retained native Fire/movement
check also passes. These are programmed native key/pointer routes, not physical
input injection or original graphics/animation/timing observations.

## Disabled reports and multiple recipients (ZOD-402)

The source's non-player result is automatic acceptance. The controller passes
learner `UNIT_TEAM_FLAG_PLAYER_CONTROLLED` to `menu_request_dispatch`. With
`enabled==0`, `request_dispatch` selects `MENU_PROMPT_CLOSE` (entry8), whose
menu ID is signed NONE=-1 and worker is `mini_menu_cancel_delay_thread`. After
two presentation frames that worker writes pending command7. The resolver's
31 directory IDs are u16, so none can equal signed -1; the no-record fallback
is also7. The report handler aliases7 to PROMPT_YES and learns the ability.
This differs from explicitly canceled UNRESOLVED_FF, which learns nothing.
The source receipt pins the entire call chain, lookup widths and constants.

`PlayerControlled` is explicit unit setup, independent of team, and defaults to
true for the existing local two-player packages. Disabled learning reports
apply source automatic YES once, emit an automatic learning event and create
no pending player offer. They spend no additional RNG or JP. The two menu
presentation frames are omitted from this deterministic command model; no
frame-RNG equivalence is claimed. This flag does not implement autonomous turns.

Eleven further Framework cases exercise actual multi-target actions, both teams
and control modes, first success leaving later markers Affected, no second scan
after acceptance/decline, known first recipients, a KO plus a living learner,
Critical eligibility and another charging recipient's retained pending cast.
Critical/Charging's prepared status flags do not set PreventReaction; their two
raw records and the edition hash are checked independently. Two Content cases
round-trip control separately from team; a Client case replays automatic learning
without a player-answer command.

## Final validation

[Implementation receipt](../evidence/combat/learn-on-hit-implementation-v1.json)
records locked restore, a Release build with zero warnings/errors, **4,931 .NET
cases and 749 Python tests**. Fresh Fire/elemental/Cure/learning packages check
35/57/39/37 original records and both asset hashes. All four finite Windows GPU
checks pass full command replay, state-preserving drawing, movement and restart;
the learning check includes decline, acceptance and the new spell's execution.
The source arithmetic audit checks 229,376 boundaries and all 65,536 possible
unsigned menu IDs against signed NONE. No original/C execution is claimed.

Ten owner-file blobs and the separate unstaged targeting-format edit are
preserved. All 465 namespace-level public Framework declarations have matching
files, with existing partial parts retained. MP costs stay `int`; AbilityToggles,
JobStats, Knight slot mapping, editor, automatic wait-facing, broader terrain,
full statuses/reactions/corpses/Golem/terminal/frame RNG, general magic AI,
ZOD-106 and custom cone targeting remain separate.
