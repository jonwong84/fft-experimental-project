# ZOD-156 — Ordinary AI refresh before general selection

Source/design, 2026-10-28. Managed base
**ba25bdad815f77a2437776dfb78010b4ed57266a**; read-only clean fft_decomp
**11464943bbdbd2a0af022d90cb7f10455cf911df**. The
[source receipt](../evidence/ai/refresh-contract-v1.json) extends ZOD-154's audit.
No production refresh query, original/C execution or host activation is added.
ZOD-159 implements the closed refresh query specified below.

## Initialization is selective

`ai_workspace_init` initializes compact indexing, selected.decided, resume and
specific defaults; it does not clear the whole workspace. Present units call
`ai_unit_abilities_init`, which computes cached ability modifiers and copies five
ENTD AI bytes into the decision record. Conserve CT and team-support-unavailable
are therefore known cached policy inputs, not values to infer from a managed ID
or assume false. Two present opponents fit the original compact/slot checks;
the managed model need not adopt the original fixed capacities.

`ai_command_build` has unchanged-command, compact-index and decision/reuse guards
before refresh. `acting_unit_init` captures current actor coordinates/team/Move,
current actor identity and cached remaining delay; only current_action.coords.zero
is normalized. It does not reset the candidate's x/y/layer or pickup cache.
The delay is computed before refresh and can read cached targeting flags. Do not
silently recompute that earlier cache from the post-refresh state. The proposed
query starts at the admitted fresh refresh call, not at workspace initialization
or the start of command building.

## Refresh source behavior

For each targetable unit, `decision_flags_refresh` writes enemy from the acting
unit's current team versus the other unit's **initial** team, with actor always
non-enemy. It masks targeting_flags_2 to retain only Conserve CT (0x04) and team
support unavailable (0x08). It then rebuilds dead/Reraise, HP and MP facts. In
the stable living None/Critical, zero-MP closed profile:

- `hp < max_hp / 2` uses integer division **before** comparison. HP24/max49 is
  not below half under this expression; `hp*2 < max_hp` would be a wrong rewrite.
- Below three quarters is `hp*4 < max_hp*3`, preserving its strict cross-product
  comparison. The UInt16 HP domain keeps these products within signed32 range.
- Zero current/max MP does not prove mp_ability_mod is zero: a non-blue actor
  can also compare current MP with cached highest MP cost. Require known zero
  cached MP-ability modifiers in the first child rather than guessing this bit.
- Considered is cleared, then set for non-enemies or units charging an action.
  Enemy with no charge is initially unconsidered.

Untargetable units take a different path that preserves other cached fields;
`ai_target_is_untargetable` even consults the earlier enemy flag for Transparent.
Explicitly certify both units targetable; living HP/flat terrain alone is not
that proof.

Refresh then sets act_before_move=1, skillset=0 and REFRESH_UNIT_STATUS_FLAGS.
In `ai_action_simulate_and_score`, the act-first branch calls movement/pickup
simulation only inside the nonzero/non-Defend skillset block. Here that block is
skipped entirely. **Stale candidate coordinates and pickup kind are not read by
refresh's movement path.** No synthetic move, action, pickup, formula or evade
roll is needed. Current rank is initialized, but refresh does not score a rank.

The source temporarily ends the actor's turn. Already-moved/unused-ACT ordinary
CT becomes `min(60, ct+20)` in the UInt16 intermediate, then the clock runs from
original UNIT_TURN/resume PICK_UNIT. Effect-free end phases precede PickUnit;
an already-ready peer is checked before AddCt. Highest CT wins, then original
slot order; readiness reduces CT modulo100 and calls upkeep, which draws even
when there is no visible status effect. Selected turn choices reset temporarily.

With no charges, Mimes, status/terrain/script effects and two positive-Speed
living units, the first clock call returns one ordinary UNIT_READY. The simulator
notes its movement opportunity, clears its temporary has_turn, scans for charges
and breaks. **Exactly one upkeep draw** occurs; only the required CT additions
occur. Source 256-event/1201-step escape limits do not bind this closed case.

The refresh-only tail clears/rebuilds transient Dead/Petrified/Jump bits and adds
eligible surviving enemies to Considered. Both admitted units therefore finish
Considered=true, all three transient bits=false, and Enemy unchanged. Other
decision flags retain their prior values. This includes only the two represented
present units; it does not pretend to reproduce every absent slot's workspace.

Unit baseline fields, action records, team Golem and between-turn globals are
restored. Workspace refresh flags survive; REFRESH_UNIT_STATUS_FLAGS is cleared
by the caller, and the flag-only pass returns0, not a score failure. RNG is not
restored. No live turn choice, CT, HP, position, facing, revision or receipt is
committed by a pure query.

## Smallest child: ZOD-159

Add a pure `SwordAiRefresh` query in `Zodiac.Framework.Ai` for an active already-
moved actor with unused ACT and its living stable opponent. Reuse existing
candidate/score certificates for original slot order, stable unchanged teams,
ordinary geometry and effect-free UNIT_TURN/resume PICK_UNIT. Require a new
false-default refresh profile for **FreshRefreshEntry**, **BothUnitsTargetable**
and **ZeroCachedMpAbilityModifiers**. Fresh entry certifies that command guards
selected this stage, not a suspended/resumed call.

Supply exactly one immutable policy row per represented unit, keyed by stable
ID, with explicit boolean ConserveCt and TeamSupportUnavailable values. These
two retained bits are inputs. Derive actor/opponent roles only under the existing
stable-opposition certificate; never turn numeric ID ordering into team flags.

Return semantic refresh-owned flags, temporary end CT/clock units, first ready
unit, upkeep raw value, elapsed/absolute hypothetical ticks and progressed RNG.
Use named booleans rather than packed flags in managed outputs; unrepresented
decision flags are outside this output, not silently set to false. Do not return
a score, rank, command, trusted session or receipt. The return value must call a
successful flag-only refresh accepted despite the original helper's return0.

Reuse the private simulation clock with fresh private units and the admitted
no-action end CT. Do not call an Attack projection or a movement-score query just
to throw away its score. Check counter/tick limits when work actually occurs;
MAX ticks is valid when a peer is already ready. Failure returns the input RNG,
typed reason and no partial clock/flags. Replay/order/both actor identities must
preserve the original snapshot. Existing selector/executor signatures remain.

The query accepts explicit hypothetical RNG like the other pure AI queries.
Future composition must own one fresh invocation and carry its actual output
into selection inside the Framework-owned command transaction. That integration
is separate; do not publish an RNG-only session or remove
GeneralSelectionEntryContext yet. Ability considerations, team/target policy,
selected-action history and earlier cached delay still need their own admission/
derivation. A refresh result alone does not prove full general-entry provenance.

## Acceptance and evidence limits

The [vectors](../fixtures/behavior/ai-refresh-contract-v1.json) are source-derived
closed arithmetic descriptions, not an original or managed refresh execution.

1. CT15/95, Speed5/5, actor already moved and seed word1/count7 temporarily ends
   actor CT35; one addition picks peer at100. Raw16838 advances to
   word1103527590/count8. Temporary actor CT40/peer CT0 and elapsed1 are diagnostic;
   original CT15/95, choices, HP and ticks remain unchanged.
2. Already-ready peer CT100 needs zero additions, including at MAX ticks. High
   actor CT clamps to60; a 60/60 tie after additions favors original TieKey, not
   collection order. Actor or peer can be the first ready unit; one draw remains.
3. Preserve the two policy bits, clear owned stale transient/target-state bits,
   rebuild Considered, and cover odd maxHP boundaries plus strict three-quarter
   equality. Reject unknown context, mismatched policy rows, unsupported units/
   geometry and wrong turn/choice phase before exposing partial results.
4. Exactly one available draw succeeds at MAX count. No available draw rejects;
   required tick overflow rejects without RNG progress. Check immutable replay
   and both actor IDs/collection order; do not duplicate prior guard matrices.

One extra draw can change later branching: the supplied ZOD-154 hit11 tape takes
one lottery draw without refresh, but eight after refresh raw16838 is retained.
Its supplied post-refresh decision ends word2111915288/count22 and actual miss
ends word3794901700/count26. This is a draw-schedule illustration with supplied
candidate costs, not fresh full-prefix/managed command equivalence.

Reproduction:

```powershell
python tools/verification/verify_ai_refresh_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
python -m unittest discover -s tools/verification -p 'test_ai_refresh_contract.py'
```

Validation: the clean pinned source audit checks three additional files, eighteen
functions and six clock vectors, plus inherited receipts
(`scratch/ai-refresh-156/source.json`). All **504 Python tests** pass, including
four new refresh checks (`scratch/ai-refresh-156/python.txt`).

No C# changed here. The last complete .NET run remains ZOD-158's 3,845 cases;
all proposed ZOD-159 refresh cases remain unexecuted until that child. Frame-
interleaved BIOS draws, general command dispatch/continuations, broader statuses,
actual original observations and native/editor activation remain outside scope.
