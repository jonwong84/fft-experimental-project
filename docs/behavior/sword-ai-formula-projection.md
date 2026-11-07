# ZOD-117 — Ordinary-sword AI formula projection

Date: 2026-10-26. Managed base: 7a284c3, clean on entry. Read-only decomp
was freshly checked clean at 11464943bbdbd2a0af022d90cb7f10455cf911df before
translation and after validation. See the [source/interface map](../architecture/zod-116-ai-decision-contract.md)
and [implementation evidence](../evidence/ai/sword-formula-implementation-v1.json).
Expectations are source-derived; no original game, emulator or C oracle ran.

## API and admission

`SwordAiFormulaProjection.Evaluate(snapshot, command, weapon, flatProfile)` is
a pure Zodiac Framework query. Its `SwordAiFormulaResult` returns Accepted,
Rejection, nullable Outcome and explicit RngState. `SwordAiFormulaOutcome`
contains HitPercent, NormalDamage and TargetHp; HasAccuracy is HitPercent > 0.
NormalDamage is capped at 999 before remaining-HP clamping, and can exceed the
target's remaining HP. There is no probability weighting or execution roll.

The caller explicitly selects the existing `FlatSwordBattleProfile`, certifying
ordinary units/ground and a neutral empty-proc sword. Its strict validator is
shared with execution: exactly two nonoverlapping ordinary units, one completely
open dry flat height 10 lower-layer rectangle on the certified surface. Body
height 6, normal movement/no traits, zero equipment evade/MP, neutral same-sign
zodiac, current PA 0..255, weapon power 0..255 and class evade 0..100 remain the
existing admitted combat domain. Unknown original flags cannot be inferred from
an ID. Interior and edge attacks use the same query; no landing tile is needed.

This first API requires the actor's current active turn, unused Act, a distinct
living target, matching equipped sword and cardinal same-layer range 1. Unknown
IDs, inactive/already-acted actors, dead/self targets, unsupported geometry or
weapon, and out-of-range actions reject before any draws. Null snapshot/command/
profile are programmer errors. A missing weapon is an unsupported setup.
The worst-critical signed16 envelope `(2*max(1,PA)-1)*power <= 32767` is retained
deliberately, even though this AI formula cannot crit; broader domains require
their own contract. Current PA and HP are read from the supplied snapshot.
The Accumulate integration test checks PA6 on a subsequent turn of the same
battle; it does not imply that battle PA persists into campaign state.

This is a legal-current-action query, not a claim that a future AI search can
call it on arbitrary origins or completed turns. Candidate construction must
establish its own validated state and lifecycle before using this boundary.

## Source ordering and RNG

At the pinned revision, combat/formulas.c::formula_weapon_damage resolves
physical evasion before weapon damage. formula_core.c::
formula_evade_calculate_final_hit_percent computes the four-source accuracy,
then calls evade_source_roll in accessory/right/left/class order. That helper
calls random_roll_pass_fail before testing execution state. AI therefore draws
four times, including zero evades and certain evasion; partial evasion never
randomly misses during AI simulation. Only front class evasion applies in this
domain, so HitPercent is 100 minus that evade, or 100 from the side/back.

combat/outcomes.c::formula_get_random returns fixed 0x4000 outside execution.
formula_calculate_critical_hit consequently sees 50%, fails the <4% condition
and adds no critical, bonus draw or push. formula_modifiers.c::
conditional_status_proc_roll_inner short-circuits outside execution; an empty
proc adds no selection draw either. Same-sign ordinary zodiac floors XA to 1.
Normal damage is `min(999,max(1,currentPA)*power)` when accuracy is positive.
resolution.c::action_finalize_target_result supplies the cap; evaluation.c::
strikes_simulate ignores zero-accuracy outcomes and otherwise applies the full
result, leaving TargetHp at `max(0,currentHP-NormalDamage)`.

Seed 57, PA 5/power 5, target HP 50, front evade 10 returns hit 90/damage 25/HP 25 and
RNG 3587663605/count 4. Execution on that seed crits 45 and consumes seven draws.
Front evade 100 returns an **accepted** zero-accuracy/damage 0/HP 50 outcome with
the same four draws. It cannot count as a valuable target hit. Zero power with
positive accuracy remains distinct from certain evasion; HasAccuracy does not
mean that the candidate has a favorable score.

The query never changes either snapshot RNG copy, HP/status/KO/facing, position,
clock or Move/Act choices, and emits no battle transition/events. Repeated probes
from the same input are equal. Successful RNG progress must belong to the real
decision even if its candidate is discarded; a test explicitly composes two
zero-accuracy results through both RNG copies and reaches count 8. That test-only
composition is not a production commit runner. Exhaustion on any of the four
draws returns no outcome and the incoming RNG, without publishing partial draws.

Execution Attack APIs and their arithmetic remain byte-identical. The new query
reuses the existing profile validator, immutable inputs and BIOS generator;
the small normal-damage/facing expressions are explicit in each mode so that
execution's variable draw branches cannot leak into projection. No general
combat-mode switch or broad formula refactor was introduced.

## Validation and limits

Locked isolated restore and Release build pass with zero warnings/errors.
**3,271 .NET tests pass**, including 227 new managed cases; **342 Python tests
pass**. The seven unchanged ZOD-116 formula vectors execute in managed tests.
The additional cases cover 128 cardinal/facing/actor combinations, both collection
orders, partial/certain evade, literal raw endpoints 0/32767, all draw budgets,
nonzero/max counts, signed-envelope neighbors, HP/cap/zero boundaries, current
PA after Accumulate, both Move/Act orders, repeat probes and atomic rejection.
One initial dead-target test supplied an inconsistent status; its setup was
corrected before the final passing run. No implementation defect was found by
that failure.

Two final source/history audits match: 16 reference files / 71 functions, one
verified prepared Attack metadata record, 37 historical files and all seven
managed baseline files. Exactly four managed headless CLI processes repeat
14 sword-save and four Basic Skill files against historical outputs. Two legacy
audits match, including the four prepared files and 29 historical native files.
No new native/original execution or asset output is claimed.

The old source contract's 50 description vectors and 20 unexecuted AIC production
cases retain their historical status. This implementation executes only its
seven sword-formula vectors and the new managed cases; it does not complete an
AIC whole-decision case. Four draws describe this formula only. Candidate clock/
upkeep, scoring, enumeration, roster/teams/policy, authoritative RNG commit and
continuations, turn-facing, interface and full selector are still unimplemented.
No scenario/content/native/save/editor activation. ZOD-106 and editor work remain
deferred.

Recommend ZOD-118: specify the bounded end-of-turn facing transition, including
automatic Move+Act completion and stale/repeated completion rejection, before
implementing its lifecycle. Existing Wait only accepts an active unit ID; adding
a facing parameter there alone cannot cover the original completion path.

## Reproduction

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-ai-117-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/sword-ai-117-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/sword-ai-117-artifacts --logger trx --results-directory scratch/sword-ai-117-tests-final
python -m unittest discover -s tools/verification -p "test_*.py"
python tools/verification/verify_ai_decision_contract.py --reference C:/repositories/fft_decomp --prepared-exe scratch/all-data-extraction/SCUS_942.21 --check-managed-baseline --output scratch/sword-ai-117-source-final-a.json
dotnet scratch/sword-ai-117-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/sword-ai-117-save-a
dotnet scratch/sword-ai-117-artifacts/bin/Fft.Client/release/Fft.Client.dll --save-check scratch/sword-ai-117-save-b
dotnet scratch/sword-ai-117-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/sword-ai-117-basic-a
dotnet scratch/sword-ai-117-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-check scratch/sword-ai-117-basic-b
python tools/verification/verify_sword_facing.py --save-baseline scratch/sword-facing-113-save-a --save-first scratch/sword-ai-117-save-a --save-repeat scratch/sword-ai-117-save-b --basic-baseline scratch/throw-stone-110-headless-a --basic-first scratch/sword-ai-117-basic-a --basic-repeat scratch/sword-ai-117-basic-b --output scratch/sword-ai-117-legacy-a.json
~~~

Use fresh report/output paths on rerun. Original data/reference and historical
scratch artifacts are only needed for the optional source/artifact audits;
ordinary tests need neither. Python here uses the bundled runtime with TEMP/TMP
directed to scratch. Full TRX, reports and the evidence builder remain ignored.
