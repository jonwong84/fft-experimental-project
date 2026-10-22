# Bounded attack snapshots and behavioral fixtures v2 (ZOD-056)

Date: 2026-10-22. Reference checkout HEAD was rechecked as
`611b504d25d4a9b03fab8023d7dbea75fe381d5d`. The field inventory follows
[ZOD-021's physical attack trace](ordinary-physical-attack.md), including
`include/fft/unit.h`, `src/battle/formula_weapons.c` and
`src/battle/action_resolution.c::action_apply_target_result` at that revision.
This ticket supplies state, setup validation and test infrastructure. It does
**not** implement damage, attack execution, death scheduling or a status framework.

## Runtime state and content boundary

`AttackUnitState` is an immutable FFT-owned combat projection, separate from
job/ability/sword definitions. Every constructor input is explicit:

- Runtime unit ID; HP/maxHP in unsigned16 range with maxHP positive and HP within
  maxHP; MP/maxMP explicitly zero for this slice.
- Effective physical attack in byte range, supplied class physical evade0..100,
  and explicit right-hand/left-hand/accessory evades all zero. These are effective
  scenario inputs, not calculations from equipment, job growth or learning data.
- Brave10..100, zodiac sign0..11, cardinal facing, supported status and byte KO
  count. All units must share a zodiac sign for neutral compatibility. The chosen
  percentage/Brave envelope is a supported-domain restriction, not a clamp or a
  replacement for the source's raw byte widths.
- Nullable right-hand logical weapon ID (`AssetId`), with null meaning explicitly
  empty. Other equipment, support/reaction modifiers, affinities and statuses are
  outside this closed contract; they cannot be silently supplied as extra fields.

Facing and status enums are managed symbols, not serialized PS1 bit encodings.
The supported coherent result projection is Dead at HP0, Critical at positive
HP<=maxHP/5, otherwise None. No Dead+Critical combination is accepted. KO count is
an explicit observed value, never inferred merely from HP0. Constructor validation
does not simulate an HP change or a KO increment.

`AttackBattleSnapshot` composes these projections with `TurnBattleState` and
requires exactly one combat record per existing unit. Positions/movement profiles
and CT/Speed/tie keys/active turn/action availability stay in that existing state;
RNG word/count remain synchronized by its constructor. An active unit cannot be
Dead. Caller collections are copied, while immutable profiles can be shared.
`WithTurnBattle` carries the combat projection across a separately computed turn
or movement result without applying any attack effect.

`ValidateInitialSetup` requires healthy ordinary units, distinct targetable,
stoppable lower-layer positions, body height6, Normal movement/no traits, and the
traced dry flat height10 map (other layers must be blocked). Result snapshots may
represent Dead/Critical, but startup cannot pretend the healthy turn cycle already
supports their upkeep or death countdown. Resuming turns after combat statuses
requires later explicit behavior; this ticket never runs such a replay.

`Fft.Content.BattleSetup.AttackBattleSetup.Create` additionally validates the
selected sword/metadata package dependency and resolves every equipped ID before
returning a setup. Weapon evade must be zero. It rejects configurations whose
largest possible critical product, `max(0,2*PA-1)*power`, exceeds signed16 positive
range32767. This is a conservative setup envelope justified by the traced critical
bonus and narrowing; it performs no damage calculation, HP clamp or RNG draw.
Unresolved content and unsupported configuration fail before simulation. No
content or filesystem dependency is added to `Fft.Core` or Zodiac.

Future contracts name `AttackUnitCommand`, typed rejection reasons and ordered
resolved/HP/status/KO event records. They are data contracts, not an attack API
implementation. Rejected command, miss, hit and new KO have distinct fixture
results; a miss is an accepted resolution rather than a rejected command.

## Version and envelope

The existing [v1 fixture contract](fixture-format.md) and its reader are unchanged:
movement-only commands, null RNG, existing policy meanings and event semantics.
There is no automatic upgrade. The separate test-only `AttackFixtureRunner` reads
`format: "fft.behavior-fixture"`, `schemaVersion: 2` and policy
`fft:combat/bounded-sword-contract`. Each reader rejects the other's version.
Only existing strict JSON/evidence/comparison helpers are shared; their visibility
is internal to the test assembly.

V2 retains v1's required root envelope: fixture ID, execution, purpose, target,
reference, evidence, preconditions/exclusions/unknowns, scenario, sourceCases,
expectations and captures. Edition is SCUS-94221; source revisions must match the
reference SHA. Evidence kind/confidence, relative source/capture paths and resolved
assertion evidence follow v1. `sourceCases` is explicitly empty in this bounded
version; source expectations attach to assertion groups. Fixtures are limited to
8MiB UTF-8, depth32 and1000 ordered commands. Duplicate, missing, unknown, malformed
or unsupported fields fail validation before execution, including late assertions.

The scenario is `{weaponIds, initialState, commands}`. `weaponIds` declares unique
logical references for fixture validation; it is not a replacement for resolving
actual `SwordContentPackage` definitions at production startup.

Initial state requires `{map, units, rngState, turns, combatants}`:

- `map` and movement `units` use the full v1 fields; expected states replace the
  full immutable map with `mapId`. Map fields are compared with the initial map.
- `rngState` requires `{algorithmId,value,drawCount}`. Algorithm ID is exactly
  `fft.ps1:rng/bios-v3-us`; value is UInt32 and drawCount UInt64, both exact JSON
  integers. There is no implicit seed or missing-counter default.
- `turns` requires UInt64 `ticks`, symbolic `phase`, and ordered `units`, each
  containing `id,speed,ct,tieKey,hasTurn,movementTaken,actionTaken` as in ZOD-020.
- Ordered `combatants` contain every camelCase `AttackUnitState` field, including
  explicit nullable `rightHandWeaponId`. Constructors enforce identity, numeric,
  status and supported-domain invariants. Ordering is observable.

Commands have contiguous `index` and one exact shape: `advance`; `move-unit` with
`unitId,destination`; `wait` or synthetic `confirm-act` with `unitId`; or future
`attack-unit` with `actorId,targetId`. Confirm-act consumes the existing scheduler
choice only and never serves as a fake successful attack.

Each expectation has `commandIndex` and exactly two assertion groups:

- `result`: `accepted,turnRejection,movementRejection,attackRejection,attackOutcome,
  evidenceIds`. Accepted results have all rejection fields None; rejected results
  have exactly one reason. Outcomes are none for existing nonattack commands, and
  hit/miss/ko/rejected for future attack descriptions.
- `state-and-events`: `stateRelation,state,events,evidenceIds`. State is the full
  expected projection above. Rejection retains the same instance; acceptance
  produces a new snapshot. Input immutability is checked independently.

Event kinds and fields are strict and ordered: unit-moved(unitId/from/to),
clock-advanced(tick), unit-ready(unitId/ctBefore/ctAfter/upkeepRoll),
choice-consumed(unitId/choice), turn-ended(unitId/ctBefore/ctAfter), and future
attack-resolved(actorId/targetId/hit/critical/damage), hp-changed(unitId/before/after),
status-changed(unitId/before/after), unit-knocked-out(unitId/koCount). Values are
exact; no event sorting, tolerance, object hash or persistent .NET object graph is
used. RNG delta is the difference in explicit absolute counters; readiness also
compares its output roll. The runner reports the first differing field and evidence.

## Executed and unexecuted evidence

[The synthetic replay](../fixtures/behavior/combat-turns.v2.synthetic.json) executes
Move, Wait and ready-peer Advance. It compares the complete combat projection,
ordered events and first seed1 upkeep transition while preserving HP/equipment.
Deliberate HP, PA, facing, equipment, RNG and event-order changes produce failures.

[The KO description](../fixtures/behavior/sword-ko.v2.source-description.json)
specifies the traced seed1 normal25damage result with target HP25: six draws,
Dead and KO count1. Its formula values are static source-derived, its managed
event contract synthetic, and its BIOS generator evidence separate. Any fixture
with an attack command, or execution description-only, returns **not-executed**
after full validation. It cannot contribute an executed combat pass. ZOD-022 must
connect actual attack resolution before that claim changes.

Tests also cover startup content resolution, conservative arithmetic rejection,
snapshot independence, missing/duplicate combat records, unsupported modifiers,
unsigned boundaries, status coherence, malformed late expectations, version
separation and the existing v1 movement regression. Normal validation needs no
BIN, BIOS, C executable or emulator.

Windows validation on 2026-10-22: locked restore, Release build with zero warnings/
errors, and all 498 solution tests passed. Earlier movement fixtures continue to
pass through their unchanged v1 entry point.
