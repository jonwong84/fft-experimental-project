# Fresh pair post-ACT preparation — ZOD-211

Implemented 2026-10-30 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, following the
[post-ACT source design](../architecture/zod-209-pair-post-act.md).

`AttackThrowStoneAiPreparation.EvaluateAfterAct(state, request, configuration, rng)`
accepts the ZOD-210 immutable inputs and actual post-command state. It owns one
`SwordAiRefresh.EvaluateAfterAct` and shared restored-state preparation. The active
actor must have ACT used and Move unused. Initialized pair-list, NonBlue, clean
delay-cache and fresh-entry assertions remain explicit; ACT used alone does not
prove the source no-progress guard passed. Selection-profile booleans are not
consumed by this preparation entry.

The new pure `TargetedAbilityBattleSimulator.ValidateAbilitySetup` shares actual
ability binding, grant and closed setup checks independently of live choice/target
availability. Both units still need Throw Stone grants; matching family profiles,
actor weapon ownership/envelope, current PA 1..99 and ordinary geometry remain
required. No ACT-unused snapshot or duplicate geometry validator is introduced.
Actual `ValidateCommand` retains its previous guard order, including
`ActionAlreadyTaken` before grant/setup/range validation. Setup admission grants
no command authority and permits a target displaced beyond current cast range.

The pre-ACT entry keeps its signature, choice admission and command-based
validation. Both entries share profile/weapon checks and result construction.
Post-ACT returns the same immutable result types: initialization and first-pass
two-harmful/zero-helpful/ranged summaries, then actor Attack and Stone entries
with Usable=false. Enemy minimum reach stays Move+1, ally reach Move; these are
computed before actor usability clears. Actor-only usage policies are not
manufactured for the peer. Common.ActorAttackUsable is also false.

Refresh temporarily ends CT at min(60, liveCT+20) and retains one upkeep draw under
the closed living profile. Focus, delays, proximity and full ordinary reachability
use actual restored HP/status/positions/CT rather than temporary clock units.
Preparation adds no further draws or movement scores. Terminal/unsupported/choice
rejection and draw/tick exhaustion return incoming hypothetical RNG and no partial
outcome. The published ACT and live RNG copies remain unchanged; no command,
receipt, continuation or Move is issued.

## Validation

Five scenario families exercise real commands before the query. Named case data
explains full RNG words and cumulative draw counters:

- Actual Attack hit `(995405759,49)` refreshes to `(2102736524,50)`.
- Stone hit `(3823029677,27)` refreshes to `(3914758370,28)`.
- Stone miss `(675780521,31)` refreshes to `(744964398,32)`.
- Stone push `(2928661168,27)` refreshes to `(3381560617,28)` and focuses the
  displaced tile. Move0's minimum reach1 does not become the ability's range4.

CT15/95 and Speed5/5 temporarily end at35 and report actor40/peer0 after one tick;
restored delays stay17/1. Actual Critical HP and pushes rebuild flags/occupancy,
including a vacated reachable tile and a target pushed beyond current Stone range.
Other checks cover both grants, invalid PA on either unit, unknown ability,
family/certificate/choice guards, real KO rejection, exact draw MAX, exhausted
draw/tick rollback, capped CT, zero-tick MAX and either actor/order replay.
An initially invalid both-spent active fixture was corrected to use Framework's
real turn-ending Move.

Locked restore, zero-warning/error Release builds, **4,090 .NET cases** across
seven projects (890 AI, 1,455 Framework), **534 Python tests**, pinned post-ACT
source/prepared-data and unified guide audits pass. Logs and seven full TRX files
are under ignored `scratch/ai-pair-post-act-preparation-211/`. Owner drafts remain
untouched and excluded. Old evidence is unchanged; no original/C execution or
presentation review is claimed.

Next, ZOD-213 derives pair post-ACT movement scenarios through shared clock/scoring
kernels and explicit reach branches. Keep D0=D1, source masks/provenance and
out-of-reach reach-draw ordering; do not assert Attack-only scenario policy for
the pair. Lottery, dispatch, execution and host activation remain subsequent work.
Terminal battle routing and other deferred gates stay open.
