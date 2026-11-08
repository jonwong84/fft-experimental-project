# Shared AI simulation retrospective — ZOD-430

Completed 2026-11-07 as a bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md).
One admission defect was confirmed and corrected: ordinary magic candidate
processing admitted more active units than the original AI can represent.
This review does not complete the original AI port or the wider retrospective.

Managed baseline: `b2d66e0a3e1670a8431814635956654821d22c35`. The read-only
reference was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [receipt](../evidence/ai/shared-simulation-review-v1.json) identifies current
managed inputs, source symbols, validation and historical evidence limits.
Fingerprints identify inputs; they do not certify every branch of a dependency.

## Entry points and isolation

Physical pair/roster candidates reach `AiCandidateState`, `AiSimulationClock`
and `AiCandidateScoring` through damage, self-stat or effect-free movement
owners. `RosterAiRefresh` supplies the explicit supported baseline/clock facts.
Magic enters `DefaultAbilityAiCandidateQuery.Evaluate`, then fresh
`DefaultAbilityAiWork`, preparation, `DefaultAbilityAiSimulation`, stored-hit work,
accounting and absolute scoring. Ready-hit, aim-list and current-origin search
queries share that fresh work owner. These queries return diagnostics and RNG;
they do not execute a command or publish their hypothetical units into battle.

The source `simulation_unit_state_save/restore(0)` is a selective baseline
transfer, not a copy of every unit field or the AI workspace. It includes current
statuses/timers, identity, death counter, team, equipment, Brave, Faith/current
HP/current MP, base/effective attributes including CT, coordinates/facing,
charge, choice/mount/automatic-control fields, action data and team Golem HP.
The four-byte span starting at Faith also covers transparency bookkeeping and
both current HP bytes; it does not include maximum HP. The nonzero mode saves
only compact status/identity/death-timer facts while reinstating baseline facts
in those fields. It leaves other hypothetical values in place for suspension.
Separate clock globals and resume state are handled by the caller.

Neither transfer rewinds RNG. Immutable managed inputs/private working copies
provide stronger isolation, deliberately without exposing source scratch-state
resumption. A supported NotValuable simulation retains its real decision draws;
unsupported contexts or exhausted counters return entry RNG and no partial
result. This atomic rejection contract is not an original-game failure branch.

## Confirmed admission defect and correction

`UNIT_SLOT_COUNT` is21, but `AI_COMPACT_UNIT_COUNT` is16. Workspace initialization
assigns compact indices to present units in battle-slot order. Before considering
actions, `ai_command_build` emits End Turn if the actor or any active unit has no
representable compact index. A 21-slot clock is therefore not evidence that an
original AI candidate can score21 simultaneously active units.

The ordinary candidate/query family previously admitted17–21 active units and
even used21-unit candidate/search scenarios to exercise unresolved charges.
`DefaultAbilityAiWork` now rejects more than16 before constructing lists, formulas
or scores. This covers ready-hit, delayed candidate, aim enumeration and search
through one owner. Physical roster simulation already required at most16.
All16 may occupy sparse source slots through20; there is no new content table
size, stable-ID limit or player battle limit. The standalone clock retains its
21-slot model and its existing boundary fixture.

The two new oversized-roster cases failed before the fix. They now verify all
four query entries reject atomically without consuming RNG or changing battle
state. A third case accepts16 units including source slot20. Existing unresolved
candidate/search regressions now use16 active units. From source phase order,
15 peers at Speed50/CT99 yield15 readiness events at ticks11,13,...43; the255th
event leaves charge127-33=94. The Speed1 actor never becomes ready. Hit remains
255, no formula runs and MP is unspent. The unresolved damage priority is
501-15*485=-6774; the healing priority is501+15*344=5661. Actor501 is
HP128+equipment357+MP16; full enemy485 has no learned-MP share; injured ally344
is floor(30*128/200)-32+357. Search still ranks that result before stopping only
the current pass. This preserves the event-cap test within a reachable profile.

## Clock, strike and score findings

End Turn grants20 CT for each unused, permitted choice and caps the result60;
Performing blocks the Act refund. Current physical hypotheses consume Act and
project source-ordered peers, settled facing, HP-derived Critical/Dead and the
normal newly-dead counter3. Counter3 depends on the admitted ordinary, nonimmortal
profile; general death-counter rules remain separate.

Effect-free clocks select greatest CT>=100 with earlier source-slot ties, reduce
CT modulo100 and consume real upkeep RNG even for corpses. Dead units neither
decay counters nor yield AI turns in this profile. A living simulated readiness
clears the temporary turn directly, without issuing Wait or another CT refund.
Positive living Speed makes the collapsed effect-free phase loop safe.

Magic additionally preserves PICK_UNIT before charge/add-CT phases, immediate
hits before the required clock event, delayed charge readiness before that tick's
CT addition, and the255 outer-event cap. This is distinct from the source clock's
per-call phase budget. Quick, other charges, timed statuses, Mimes and staged
upkeep effects are excluded. See the supplied
[clock/status](between-turn-clock-review.md), [upkeep](turn-upkeep-review.md)
and [VSync/RNG](ai-vsync-rng-review.md) reviews for their broader source paths.

`action_hits_simulate` initializes counted accounting only in its acting-unit/
unset-hit context. Success needs a counted strike and a valuable target; mean
accuracy truncates. The alternate unset255 path is distinct. Current ordinary
hit work first projects formulas/resources for all recipients, then applies
targetability, movement/follow-anchor and valuable-mask rules. Excluded recipients
can still consume formula RNG. Stat hits credit+3/-4; other hits credit0/-1.
Totals retain unsigned16 and priority signed16 widths. Current single-strike,
bounded rosters cannot wrap the byte counter; this does not cover general
multi-hit/reaction accounting.

Absolute scoring retains HP*128/maxHP, Dead-192/Critical-32, seven unchanged
human equipment slots*51, enemy sign and signed16 narrowing after each unit.
Ordinary magic also retains the MP threshold discount followed by quarter-share
weighting. PA increases have no direct stat score term. Golem, changing teams,
general statuses/equipment and special target policies remain outside admission.

## Evidence, validation and follow-up

Existing candidate/score description audits each pass44 vectors. Their preserved
inputs use historical paths predating file organization: running the historical
scripts against today's root fails on those missing paths. For this review,
each manifest input was reconstructed under ignored scratch from its recorded
managed revision when current bytes did not match, and every recorded hash was
checked before auditing. These runs validate historical descriptions and pinned
source, not today's managed method bodies. Historical receipts/tools remain
unchanged. Roster preparation, refresh and magic-clock source audits also pass.

The current bodies were reviewed separately against the supplied routines and
pinned source. The rebuilt full suite passes5,158 .NET tests, including1,361 AI;
the794 Python tests pass. Owner drafts, immutable JobStats, the approved targeting
formatting and the owner's unstaged `AiTurnController.cs` edit are preserved.
No original executable, reference C or native presentation was run for this batch.

Next is ZOD-431, the bounded shared preparation/useful-target policy review.
Movement-origin study ZOD-426, full decision/ranking/command coverage, unsupported
effects and frame-callback RNG remain explicit later work. ZOD-432 records the
owner's required readability/walkthrough/refactor gate after the full AI port;
see the [completion roadmap](../development/ai-completion-roadmap.md).
