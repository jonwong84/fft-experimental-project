# Learn on hit

ZOD-398 pins the clean, read-only reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/combat/learn-on-hit-source-v1.json) fingerprints
`outcomes.c`, the turn controller, menu dispatcher and RNG wrapper. This is
source study with independent arithmetic checks, not an observed retail run.

## Source contract

`reward_grant` applies EXP/JP and growth before the learning report phase.
`ability_find_learn_on_hit_unit` exits without scanning for Hamedo, ability0,
or an ability without primary LearnOnHit. It scans original unit slots in order,
excluding the actor. An Affected marker is **replaced** by LearnChecked before
checking human/reaction eligibility or primary-skillset membership. Already
checked and merely Targeted units are skipped. Dead ordinary hit results retain
Targeted, so cannot qualify through this path.

The current job selects a three-byte learned row: jobs below Squire74 alias row0;
otherwise row=job-74. Bits are MSB first, slot0 at byte0 bit7. The source loops
24 bits, but human skillset lookup returns zero beyond slot21. Each matching
unknown slot draws `rand()*100/32768 < rawRate`; rates0 and >=100 still draw.
Duplicate ability slots can roll repeatedly until one succeeds. The first
success returns immediately, leaving later units' Affected markers untouched.

The controller creates at most one offer. YES invokes `ability_learn_used`,
which sets **every** matching unknown slot using the row captured before its
walk. NO and UNRESOLVED_FF advance the phase without learning. Neither response
rescans other units. Acceptance spends no JP or RNG. Definitions remain immutable.
Disabled non-player menus require the follow-up close-response trace in ZOD-402;
team color must not be used as a substitute for player control.

## Managed boundary and validation

`LearnOnHit.Scan` is a pure captured-row calculation; it cannot publish battle
state. It orders by source slot and retains slot identity independently of
ability identity. `Accept` unions matching slots. Counter exhaustion throws
without publishing partial candidates. An owned battle/report path is ZOD-399;
prepared eligible recipients stay guarded until ZOD-400.

Eleven managed cases cover marker ordering, first success, duplicates, acceptance,
known/ineligible/already-checked recipients, raw rates0/29/30/100/255, Hamedo,
disabled metadata and exhaustion. The independent Python audit checks all32768
BIOS outputs at seven rates (229376 comparisons), seed57 and duplicate-slot
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
Its learning profile admits generic original jobs74–93; special-job row aliases
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
known spells, command gates/stale replies, duplicate acceptance/decline, rate0,
ineligible marker publication, instant casts, restart and atomic rollback when
the learning draw exhausts the counter. The full Framework suite passes 1,773
cases. Prepared packages still retain their guards until ZOD-400 binds metadata.

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

`prepare-learning-magic-battle` imports only Fire16 and Fire4=19 using the same
formula08. Its37 verified records include unchanged primary rate30, secondary
power32/MP48/CT10/area2, Wizard80/BlackMagic11 slot3 and progression records.
The authored scene uses a Wizard learner with HP200/MP75, no spell grants, an
empty current-job row and explicit seed10. Mage starts with slots0/3; the seed
makes the first learning report reproducible without modifying the original rate.
The scene's positions, bare equipment, units, seed and first-KO objective are
invented setup, not an original encounter claim.

The independent content audit now accepts `--selection learning` and verifies
both asset hashes, both raw records and metadata, all37 provenance records,
source slot indices and explicit learner setup. Full Content876 and CLI375
cases pass, including actual prepared Fire4 acceptance/decline through the
Framework command path. Client display and journal snapshots are ZOD-401.
