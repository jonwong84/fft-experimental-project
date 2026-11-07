# ZOD-119 — Bounded turn-facing implementation

Date: 2026-10-26. Managed base: 26ee4ad, clean on entry. Read-only decomp
was freshly checked clean at 11464943bbdbd2a0af022d90cb7f10455cf911df before
translation and after validation. See the [source/design contract](../architecture/zod-118-turn-facing-contract.md)
and [implementation evidence](../evidence/ai/turn-facing-implementation-v1.json).
This implements a facing boundary, not a full original AI or movement port.

## Entry and typed choices

`FacingTurnBattle.Begin(snapshot, flatProfile)` returns an immutable FacingTurnState
with revision 0 and no pending receipt. Begin validates the existing strict
FlatSwordBattleProfile: exactly two nonoverlapping ordinary units, certified open,
dry, flat lower ground, body height 6, normal movement/no traits, and the existing
bounded combat fields. It also requires healthy initial units, AddCt phase,
zero elapsed battle ticks, no active unit or consumed choices, and map width/depth
at most 128. There is no attach, restore or public state/receipt constructor.

The supplied RNG word **and draw count are preserved**. DrawCount counts outputs
since seeding, not elapsed battle turns; Begin must not reset it or require a new
stream. Tests start at count 500, advance to 501, and reject readiness atomically at
ulong.MaxValue. This clarifies the source contract's fresh-setup requirement.
Revision is the new wrapper's local mutation counter and starts at 0 independently.
Begin throws for invalid setup; transition rejections return the identical input
wrapper, no events and typed reasons.

FacingChoice has three closed forms:

- Fixed(BattleFacing), checked for a defined managed cardinal value.
- KeepCurrent, preserving the actor's supplied settled combat facing.
- ChooseClosedEnemy(enemyId), explicitly certifying the other unit as an enemy.
  Unknown/self IDs are unsupported; a dead opponent returns NoLivingEnemy. There
  is no team inference from IDs, general roster or cached imminent-enemy model.

Probe is a pure query for the active or pending actor. It returns a typed facing
or rejection without changing revision, RNG, receipt or battle. Fixed/KeepCurrent
can finish after the opponent dies; the chooser cannot. Overlapping setup is
rejected at Begin, so the source's unsafe overlapping chooser is never admitted.
Fixed facing can point off-map; facing does not require a legal movement tile.

The source ai_target_facing_find compares dy-dx and dy+dx, with exact diagonal
ties SW=South, NW=West, NE=North and SE=East. Map names to managed enums rather
than casting original ordinal values. The coordinate bound prevents signed-byte
wrap. On this certified flat rectangle the direction toward its sole living
enemy is viable, including edges and diagonals. The source imminent/any searches
therefore select the same enemy and direction. General terrain/target retries,
multi-unit slot ties and normalized status/team/imminence remain outside this
production profile; their historical description vectors are unchanged.

## Completion and receipt ownership

Advance, Move and Attack delegate to existing rules. Accepted mutations increment
revision once, and legacy rejections propagate without issuing a receipt or
publishing RNG/choice progress. Move retains its selected prototype movement
policy; this wrapper does not establish original path legality or heading.

If a successful Move/Attack ends the turn, the wrapper finds its TurnEndedEvent
only after the complete legacy result returns. It then mints a pending
FacingCompletionReceipt for that actor/revision, bound to the exact settled
snapshot and private wrapper lineage. A critical Attack receipt already contains
HP/status/KO, forced displacement and all execution RNG progress. Do not capture
the snapshot at the earlier clock event inside combat resolution.

While pending, Advance, Move, Attack and another WaitWithFacing reject with
FacingPending. Unsupported facing leaves the committed battle and receipt intact.
FinishFacing checks receipt identity, expected snapshot, lineage, revision and
actor, and requires no active clock unit. It changes only the actor's facing,
clears the receipt and increments revision. It emits one FacingSettledEvent,
including a same-facing completion, and no second TurnEndedEvent or movement
event. CT/choice flags, ticks, both RNG copies, positions, PA/HP/KO and all other
combat fields remain. It does not call Wait, repeat combat or consume RNG.

WaitWithFacing instead validates/resolves the choice on the current active actor,
then atomically publishes one legacy Wait/refund and one FacingSettledEvent.
Ordinary CT 15 leaves 55 when neither choice was used, 35 with one used, 15 with both,
capped at 60. Automatic completion has already applied the cap; FinishFacing
preserves that result. The source's repeated turn_end call is a no-op after
has_turn clears; calling the managed active-unit Wait again would reject.

Receipts are internal in-memory capabilities, not serialized IDs. Same actor and
revision numbers from another branch/run do not authorize completion. A consumed
receipt cannot be used on the returned authoritative state, including a later
turn of that actor. Replaying an old immutable pending input is intentionally
valid and repeatable; no global consumed-token set is used. Replay creates new
receipt objects and compares semantic state/events/revisions. Keep the latest
returned wrapper; an extracted legacy snapshot cannot be reattached.

Every mutating operation checks revision capacity before publishing effects.
Queries still work at max revision; zero-draw Wait/Finish work at max RNG count
if revision capacity remains. Tests use friend-assembly access to construct
unreachable counter/invariant cases without exposing a public injection API.
Existing production/test files and public APIs are unchanged; the friend
attribute is a new assembly metadata file.

## Validation and compatibility limits

Locked isolated restore and zero-warning Release build pass. **3,415 .NET tests
pass**, including 144 new managed cases; **359 Python tests pass**. All
FTF-001..018 have implementation coverage recorded in the new evidence. Twelve
admitted direction IDs execute across both actors/collection orders, plus eight
fixed/KeepCurrent/closed-chooser IDs. Remaining historical general-terrain,
multi-unit and proposed-gate descriptions are not newly claimed as full production
algorithms. The ZOD-118 fixture/evidence retains its historical unexecuted labels;
its audit's productionImplemented=false refers to that original contract snapshot.
This evidence owns the new implementation mapping.

Tests cover both choice orders, actual pushed target coordinates, current stats,
CT refunds/cap, same-facing consumption, missing/stale/wrong/cross-branch/cross-run
and earlier-turn receipts, exact snapshot binding, pending barriers, all critical
draw budgets, zero-draw counter boundaries, fresh RNG carry, unsupported geometry,
and prefix/suffix replay. The first focused run had nine failures sharing test
setups that requested an unsupported zero-distance Move. Corrected the setups to
use an actual move before the final passing suite; legacy movement behavior stays.

Two repeated source/history audits match 12 files / 32 functions, 51 historical files
and 13 managed baseline files. Four managed headless CLI processes repeat 14 sword
save files and four Basic Skill files against historical outputs. Repeated legacy
audits preserve four prepared files and 29 historical native files. No original
game, emulator, C oracle or new native window ran. Expectations are source-derived
and managed regression evidence, not full original-game equivalence.

The wrapper is not selected by TwoUnitBattleScenario, content, save or native
paths. The authored first-KO terminal policy and death scheduling limits remain.
KeepCurrent after Move preserves managed facing; original walking publishes a
path heading that current movement does not retain. No net-displacement heading
is invented. Full original movement/search composition, AI roster/policy,
candidate clock/upkeep, scoring, decision RNG commit/continuations and the proposed
IBattleDecisionPolicy remain future work. Editor and ZOD-106 stay deferred.

Recommend ZOD-120: specify the bounded original flat two-unit AI movement/path
composition, including occupied traversal, stay candidates, scan/path ties and
settled heading, before implementing search over current prototype movement.

## Reproduction

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/turn-facing-119-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/turn-facing-119-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/turn-facing-119-artifacts --logger trx --results-directory scratch/turn-facing-119-tests-final
python -m unittest discover -s tools/verification -p "test_*.py"
python tools/verification/verify_turn_facing_contract.py --reference C:/repositories/fft_decomp --check-managed-baseline --output scratch/turn-facing-119-source-final-a.json
dotnet scratch/turn-facing-119-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/turn-facing-119-save-a
dotnet scratch/turn-facing-119-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/turn-facing-119-save-b
dotnet scratch/turn-facing-119-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/turn-facing-119-basic-a
dotnet scratch/turn-facing-119-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/turn-facing-119-basic-b
python tools/verification/verify_sword_facing.py --save-baseline scratch/sword-facing-113-save-a --save-first scratch/turn-facing-119-save-a --save-repeat scratch/turn-facing-119-save-b --basic-baseline scratch/throw-stone-110-headless-a --basic-first scratch/turn-facing-119-basic-a --basic-repeat scratch/turn-facing-119-basic-b --output scratch/turn-facing-119-legacy-a.json
~~~

Use fresh report/output paths on rerun. Ordinary tests need neither reference
checkout nor original assets; source/artifact audits are optional read-only checks
against those local inputs. Python here uses the bundled runtime with TEMP/TMP
under scratch. TRX/reports and the evidence builder stay ignored under
scratch/turn-facing-119-* and scratch/build_turn_facing_119_evidence.py.
