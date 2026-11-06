# Shared ordinary preparation and AI simulation (ZOD-422)

The owner selected this bounded refactor after supplying FFHacktics disassembly
for preparation, strike simulation, candidate scoring and the snapshot helpers.
Ordinary live casting and AI candidate evaluation now use one immutable primary
preparation proposal. Existing AI admission, simulation, strike processing, hit
accounting and absolute scoring retain separate owners. This is an incremental
refactor of the supported ordinary spell path, not completion of all original AI.

## Source and ordering

The read-only reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`.
[The source receipt](../evidence/ai/ordinary-preparation-source-v1.json) pins eight
files and nine functions. Neither C nor the original game was executed.

- `action_prepare`, `0x8017C45C`, in `src/battle/combat/preparation.c` validates
  actor/followed-unit identity, resolves command-menu substitutions, prepares
  charge timing and performs phase-dependent state changes. Target type6 follows
  a unit; it is not a map-target flag. Validation here does not establish all
  action legality. Phase0 still writes a prepared destination command; nonzero
  phases store ordinary charge CT, and odd phases commit the applicable effects.
  The traced Mimic caller uses phase1, not the wiki's speculative0x0D.
- `action_commit_preparation`, `0x8017C3DC`, calls phase1 and clears action state
  only for an immediate primary action. It is shared with AI and actual actions,
  despite the wiki's AT-preview label. Result codes are invalid=-1, charge=0,
  immediate=1, equipment-change=2 and Jump=3; Defend also returns0.
- Ordinary timing masks raw CT with0x7F, rounds Short Charge upward when halving,
  then applies Non-Charge. Persevere bypasses both. The distinct AI entry-loading
  delay remains in `OrdinarySpellAiParameters`; it must not replace actual setup.
  Jump, Charge, Item, Throw, Math, Geomancy and Draw Out have separate command-menu
  paths. No new support for those paths is introduced here.
- `status_clear_action_state`, `0x80183DE0`, delegates to
  `status_action_state_set` in `src/main/unit_state.c`: it clears Charging, Jump,
  Defending and Performing and sets the unit's stored charge counter to0xFF.
  A pending ordinary plan's remaining counter0 means ready; it is not the
  original unit's separate no-charge sentinel.
- `ai_action_simulate_and_score`, `0x8019D37C`, owns candidate action/move order,
  hypothetical turn completion, charge-clock work and state scoring.
  `action_hits_simulate`, `0x8019DCBC`, owns conditional target/strike accounting;
  `strikes_simulate`, `0x8019DD8C`, owns strike processing and eligible reactions.
  The existing event cap permits at most255 clock calls. An unresolved charge
  can still receive a score with hit255. An immediate useful hit still advances
  one clock event before scoring.

## Managed responsibilities

- `Zodiac.Framework.CombatRules.OrdinaryMagicPreparation` accepts the existing
  certified `MagicChargePlanRequest` and returns a prepared plan and proposed
  caster state. It validates active identities and derives timing from the
  selected definition/current supports. It proposes Charging for a delayed
  primary cast, or cleared action statuses for an immediate one, preserving
  unrelated status values. It spends no MP and draws no RNG. Callers separately
  commit ACT usage using their existing turn-state owners.
- `MagicChargePlanQuery.Prepare` remains a compatible plan-only adapter. Existing
  public signatures and timing behavior are preserved.
- `OrdinaryMagicBattle.Queue` consumes the shared proposal before queuing or
  immediately resolving the spell. MP remains spent at the existing ready-hit
  boundary, not preparation.
- `OrdinarySpellAiWork` owns fresh admission, lists, usefulness and initial aim
  capture. Its candidate preparation delegates to the same combat rule. The
  ready-hit diagnostic still captures an aim without scheduling another action.
- `OrdinarySpellAiCandidateQuery` owns public admission and translates unsupported
  work to an atomic rejection. `OrdinarySpellAiSimulation` privately orchestrates
  preparation, the existing clock, ready strikes and scoring. It applies the
  proposed caster state only to immutable hypothetical units.
- `OrdinarySpellAiHitWork` has its own file and retains stored-aim resolution,
  formula projection and private result application. `OrdinarySpellAiAccounting`
  and `OrdinarySpellAiScoring` remain the existing accounting/scoring owners.

No command-family interfaces, raw PS1 memory structures or per-ability handlers
are added. Current original IDs, metadata ownership, formulas, evasion draws,
target order, source widths, events, schemas and command authority remain intact.

## Baseline and suspended-status boundary

The supplied transfer routines map to `simulation_unit_state_save` at0x8019E160
and `simulation_unit_state_restore` at0x8019E378. Mode0 saves/restores selected
baseline fields, action records and team Golem values. Max HP/MP are not saved.
The four bytes beginning at Faith are Faith, the transparency-removal flag and
current HP, not maximum HP.

Nonzero save mode stores only active identity, death counter, current statuses
and Death Sentence CT in compact records, then restores those fields from the
baseline. Nonzero restore mode reinstates those simulated fields. Other
simulated values remain in place across suspension. These helpers do not save
or rewind RNG or the whole AI workspace. The original VSync thresholds control
suspension; they are separate from gameplay charge ticks.

The current managed queries use private immutable values and do not suspend.
Their ordinary NotValuable outcomes retain decision RNG, while unsupported
contexts/counter exhaustion return the entry RNG with no partial outcome.
Porting the original suspension lifecycle, frame-interleaved RNG, reactions,
status handlers/display flags, pickups and broader clocks remains separately
gated. Do not replace that future lifecycle with a whole-world rollback.

## Verification

Existing ordinary AI regressions retain immediate/delayed ordering, excluded
target draws, hit accounting, literal scores, unresolved hit255, event limits and
late-failure isolation. Added public tests compare live/AI preparation for CT0,
CT4 and raw CT131; check deterministic replay and unchanged input; and exercise
Short Charge rounding, Non-Charge precedence, preserved unrelated statuses and
rejection without a partial proposal. The existing plan-only admission tests
continue to cover inactive identities, profiles, duplicate IDs and Mime.

Validation results are recorded in the current handoff. Owner drafts, immutable
JobStats and int MP costs remain preserved. ZOD-421 physical delivery ownership
and ZOD-418 ordinary aim search remain the next separately scoped work.
