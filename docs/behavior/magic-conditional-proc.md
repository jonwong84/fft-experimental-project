# Magic conditional-proc decision — ZOD-340

## Supplied absorption/proc wrapper — ZOD-436 support

The owner's Elemental Absorb and status roll listing matches all ten words at
`0x8018871c`–`0x80188740` and all four listed returns. The
[formula0x08 supporting receipt](../evidence/combat/formula08-supplied-trace-v1.json)
pins clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the verified
prepared BATTLE.BIN, source functions and current managed inputs. The calls are:

1. `0x801870fc`, `formula_element_apply_absorption`: perform the element-marked
   damage/type routing after Faith.
2. `0x801884c0`, `formula_roll_conditional_status_proc`: decide whether the
   later status applicator should be attempted.

The wrapper forwards the second helper's return unchanged: zero requests the
later attempt, nonzero skips it. It does not apply statuses, inspect HP amount
or branch on the absorption result. Its normal formula0x08 caller already skipped
the wrapper if earlier element processing cleared hit; that gate is outside this
wrapper itself.

The "19%" summary describes the admitted fresh **execution** roll, not every
mode. Preview requests an attempt without drawing or setting AddedEffect; fresh
AI with the marker clear excludes it without a draw. A preexisting marker can
change non-preview behavior and remains outside current fresh/no-pending
composition. Zero return is not proof that a status changed, nor does it imply
that the helper set the flag in preview. Exact source thresholds remain below.

`FaithMagicFormula.ResolveProjectedAfterHit` already calls
`FaithMagicHpRouting.Route` before `MagicConditionalProc.EvaluateAfterHit`.
Absorption retains the original damage formula's conditional tail even when the
HP route becomes healing; routing alone must not select the formula family.
Existing atomic private-result/RNG rejection and admission boundaries remain.
The source/prepared audit passes six roll and26 decision descriptions plus four
Fire tier/empty-status bindings. All23 focused .NET routing/proc/composition tests
and16 Python tests pass; all ten owner draft blobs are unchanged. The approved
CircleCI gate is committed separately with its job definitions retained and a
false default. No runtime or fixture change is needed.
The supplied [Elemental Absorption helper](magic-elements-and-routing.md#supplied-elemental-absorption)
at `0x001870fc` is now checked too. ZOD-436 remains open.

## Supplied Conditional Status Proc Roll wrapper

Both owner-supplied annotated listings contain the same20 instruction words at
`0x801884c0`–`0x8018850c`, all matching the verified prepared BATTLE.BIN. All five
listed return locations and the one inner call match too. The same supporting
receipt pins the source, mode enum and eight literal return descriptions at the
unchanged reference revision.

The outer routine **always calls** `conditional_status_proc_roll_inner` at
`0x80187690` first. It then reads the scalar action-state word at `0x8018f5fc`:

- Preview2 branches directly to return0, with the zero assignment in the branch
  delay slot. It does not read the target effect marker on that path.
- Otherwise it reads unsigned16 target effects at offset`0x10` and returns0 if
  AddedEffect`0x0200` is set,1 if clear. Other effect bits do not imply success.

It does not return an inner RNG result directly. A preexisting marker can make
non-preview return0 even after the inner routine performs no roll or fails to
set a new marker. Preview's return0 is an attempt request rather than
proof of a roll, a flag delta or an applied status. The outer routine itself
does not publish HP or statuses; the inner routine owns its execution-only RNG
gate and flag update.

`MagicFormulaEvaluationMode` keeps Execute0, AiSimulation1 and Preview2.
`MagicConditionalProc.Evaluate` already preserves the admitted mode outcomes,
no-draw Preview/AI behavior and the known-clear marker requirement on non-preview
entry. Broader stale/pending contexts remain explicitly unsupported. The existing
source/prepared audit passes six roll and26 decision descriptions plus four Fire
tier/empty-status bindings. All15 focused .NET proc/composition tests and eight
Python tests pass; all ten owner draft blobs and the approved disabled-by-default
CI gate remain unchanged. No runtime or fixture change is required.
ZOD-436 remains open.

## Supplied execution-only inner proc routine

The owner's listing matches all21 instruction words at `0x80187690`–`0x801876e0`,
the chance-helper call and the listed return `0x801884d0`. The same supporting
receipt pins the call arguments, two early-exit branches, effect-word update,
seven literal mode/flag descriptions and five threshold descriptions at the
unchanged reference/prepared baseline.

The scalar action-state word must equal0 (Execute). Any nonzero state branches
to return **before** the RNG call. Execution calls `random_roll_pass_fail` at
`0x8005e0cc` with maximum100 and threshold19; the threshold is assigned in the
call delay slot. A nonzero returned value branches past the marker update.
Only return0 performs unsigned16 read/OR/write of AddedEffect`0x0200` in target
action offset`0x10`, preserving other flags. The supplied inline pseudocode's
`rollResult != 0` success condition is reversed.

The pinned helper scales raw rand0–32767 with `raw*100/32768`, producing0–99,
then returns1 when that result is at least19. The notes' inclusive "0–100" range
is inaccurate. Raw6225 maps to18 and succeeds;6226 maps to19 and fails. This is
not a modulo100 roll. Failure does not clear an old AddedEffect marker, which
explains the outer wrapper's sensitivity to stale flags. No non-execution path
draws or clears markers.

The inner routine does not check HP amount, hit, ability elements, the status
record or an existing marker before drawing. It sets a marker, not a live status
or an overall hit result. Empty status data or zero damage therefore does not
justify omitting its execution draw. Absorbed damage retains the same family
tail. Broader stale/pending contexts remain outside managed admission.

`MagicConditionalProc.Evaluate` already matches the fresh Execute draw/threshold,
set delta and no-draw Preview/AI paths. Its atomic draw-counter exhaustion
rejection is an explicit managed guard, not a PS1 counter rule. No runtime or
fixture change is required. All15 focused .NET proc/composition tests and eight
Python tests pass; all ten owner draft blobs and the approved disabled-by-default
CI gate remain unchanged. The supplied Pass/Fail Roll is now checked below;
existing root RNG evidence remains reusable. ZOD-436 remains open.

## Supplied Pass/Fail Roll

The owner's duplicated Pass/Fail Roll page matches all 21 instruction words at
`0x8005e0cc`–`0x8005e11c` in the verified prepared SCUS executable. The supporting
receipt pins clean reference `11464943bbdbd2a0af022d90cb7f10455cf911df`, the
executable hash, `random_roll_pass_fail`, the BIOS veneer and the corrected SCUS
caller. All 13 listed BATTLE.BIN return locations match. The listed SCUS address
`0x8005cfe0` is the JAL instruction in `unit_job_abilities_learn`; its return is
`0x8005cfe8`. The first word `27bdffe0` allocates 32 stack bytes, correcting the
accompanying `-0x0010` text.

The helper always calls `rand` once at `0x8002230c`, preserving threshold in the
call delay slot. That three-word veneer selects BIOS A0 service `0x2f`; the
existing [BIOS RNG verification](bios-rng-verification.md) supplies its raw
0–32767 output contract. The helper has no preview/AI shortcut or threshold
early exit. Even maximum 0 draws; callers own any execution-mode gates.

Signed MULT followed by MFLO retains the low 32 product bits. If that signed
product is negative, adding `0x7fff` before arithmetic shift 15 gives division
by 32768 with truncation toward zero. The branch tests the product, not the
unmultiplied RNG value. Signed SLT and XOR 1 return 0 below threshold and 1
otherwise. Arbitrary large maxima can overflow the product; the range summary
does not establish an unbounded mathematical contract. Current proc maximum 100
keeps every product safely inside signed 32 bits.

Offline checks compare all 32,768 raw values for seven nonoverflowing maxima
(229,376 products), plus 13 signed division boundaries and eight threshold
descriptions. For maximum 100 the scaled outputs span exactly 0–99; raw 6225
passes threshold 19, while 6226 fails. Threshold 0 always fails and threshold
100 always passes in that domain, with a draw in either case.

`MagicConditionalProc.Evaluate` already uses the same raw scaling and below-19
comparison after one `RandomStateGenerator.Next` transition. No runtime or
fixture change is required. All 25 focused .NET RNG/proc/composition tests and
eight Python tests pass; all ten owner drafts and the approved disabled CI gate
remain unchanged. Listed caller checks do not establish a complete
call graph or activate their other gameplay families. Frame/VSync RNG scheduling
and broader status sets remain separate. The preserving status wrapper at
`0x80187eb4` is now checked below. ZOD-436 remains open for the full
shared-consumer review.

## Supplied preserving status wrapper

The owner's listing matches all 28 instruction words at `0x80187eb4`–`0x80187f20`,
the inner call to `0x80187f24` and all 13 listed return locations in the verified
prepared BATTLE.BIN. The same supporting receipt pins clean reference
`11464943bbdbd2a0af022d90cb7f10455cf911df`, `formula_apply_status`, the inner
applicator and the forced-miss helper relevant to the admitted empty record.

Before calling the inner applicator, the wrapper saves unsigned byte miss type
at offset `0x02`, unsigned 16-bit accuracy at `0x2a`, and unsigned byte hit at
`0x00`. It unconditionally calls the inner routine, then restores hit, accuracy
and miss type in that order. The restored footprint is exactly four bytes:
`0x00`, `0x02`, `0x2a` and `0x2b`. Accuracy is not narrowed to a byte. Incoming
values are preserved rather than normalized to hit, 100% or a particular miss
type. Restoration rereads the global target-action pointer after the call.

The wrapper neither tests the original hit/accuracy nor rolls RNG itself.
Its inner routine owns status processing and any related draws. Only the three
saved fields are restored: status infliction/removal, HP/MP amounts, attack type,
special effects and ability work are not snapshotted here. Restoring the hit
fields therefore does not roll back a successful status change or refund a draw.
These are planned action results, not direct live-unit status publication.

In the current fresh empty-status context, the inner routine finds no infliction
or removal, calls `formula_force_attack_miss` and returns. That helper changes
only hit, miss type and accuracy; the wrapper restores all three. HP results and
the earlier AddedEffect marker survive. Empty status data therefore cannot turn
the damage into a miss or justify skipping the preceding proc draw. This does
not imply that a status was applied.

`FaithMagicFormula.ResolveProjectedAfterHit` already models that admitted empty
attempt by retaining its hit/accuracy/miss facts. `MagicResultFinalization`
requires `EmptyStatusRecord`; nonempty status work remains rejected. Later
finalization may still clear hit for zero accuracy, so the wrapper's restoration
is not an unconditional final-hit guarantee. No runtime or fixture change is
required. The source audits retain the four Fire tier/empty-record bindings.
All 29 focused .NET proc/composition/finalization tests and 16 Python tests pass;
the ten owner drafts, approved controller edit and disabled CI gate are unchanged.
The supplied [inner status applicator](status-action-application.md) at
`0x80187f24` is now checked too: its branches, four calls, 17 direct callers and
formula `0x38` entry are recorded without activating general statuses. Validate
Status Changes at `0x80184b24` is the next useful supplied listing. ZOD-436 remains open.

## Original implementation checkpoint

Implemented 2026-11-05 from managed base
`e8ffff7cacba283321a2e8568753e5aa0647d6cf`, clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/combat/magic-proc-source-v1.json): nine files/ten
functions. [Six roll and26 decision descriptions](../fixtures/behavior/magic-proc-contract-v1.json)
separate original behavior from explicit managed context/counter guards.
No original/C execution; BIOS RNG arithmetic retains its existing independent
[verification](bios-rng-verification.md).

## Source order and exact roll

For formula0x08, `formula_magical_damage` reaches
`formula_element_apply_absorption_and_status_proc` only after a hit survives
evasion and element nullification. Absorption runs before the conditional roll.
`conditional_status_proc_roll_inner` executes a real RNG draw only in execution
mode and ORs the added-effect bit0x0200 when the roll succeeds.
`random_roll_pass_fail(100,19)` calculates signed32 `rand()*100/32768`, then tests
whether that value is at least19. Success is the opposite: below19. Raw6225 is
the last success (18);6226 is the first failure (19). Raw32700 fails, despite its
modulo100 being0. This is distinct from the AI ranking lottery's modulo rule.

The helper does not use `formula_get_random`'s midpoint shortcut. Its inner roll
is execution-only. The outer `formula_roll_conditional_status_proc` returns
success unconditionally for preview, otherwise checks the added-effect flag.
In normal fresh AI work the flag is clear, so AI excludes the proc with no draw.
An already-set flag can change that result and can preserve success after a failed
execution roll. That stale/pending context remains outside the selected slice;
it must not be silently treated as a clear flag.

The roll does not inspect the status-infliction record or HP amount. Verified
prepared Fire1–4 use formula8 and status record0; that record is six zero bytes.
Execution still draws and can set the added-effect flag. `formula_apply_status`
later preserves hit/accuracy/miss type around its status attempt. No-op status
data therefore does not justify dropping the earlier RNG draw or flag delta.

Formula0x0C has no conditional-proc branch. Neither healing reversed into damage
by undead nor damage absorbed into healing changes the original formula family.
Misses before the formula and element nullification skip this decision altogether.

## Managed decision and admission

`MagicConditionalProc.Evaluate` takes an explicitly resolved family, invocation
path and `MagicFormulaEvaluationMode`, plus immutable RNG. `EvaluateAfterHit`
derives family/nullification from the opaque element-aware projection. It never
infers eligibility from the routed HP direction. A pre-formula miss can be
represented separately without fabricating a successful projection.

An entered damage tail requires ordinary primary context and no pending weapon
proc/reaction. Execution and AI also require a known-clear added-effect flag.
Preview returns before reading that flag, so its clear-flag certificate is not
required. Uncertified context rejects before drawing. Healing, misses and
nullification do not read these otherwise-unused certificates; their profile
may be absent. Invalid family/entry/mode values and the impossible healing plus
damage-element-nullification combination reject as API input errors.

Execution consumes exactly one existing `RandomStateGenerator.Next` draw and
returns full word/count plus raw/percent diagnostics. Preview requests the status
attempt without a draw or flag set; admitted AI excludes it without a draw.
The outcome distinguishes whether the source wrapper was invoked from whether
a random draw occurred. `SetAddedEffectFlag` is a **set delta**, never an instruction
to clear a flag on failure or an assertion that a status was applied.

UInt64 draw-counter exhaustion is the existing managed RNG guard, not a PS1 rule.
It returns rejection with the incoming RNG and no partial outcome. Skipped and
non-execution paths work even at that counter limit. The caller supplies the
checkpoint after earlier stages: a skipped proc does not undo earlier evasion
or decision draws. No live stream is adopted and no global state is changed.

## Validation and next work

Ten new Framework cases cover32 descriptions, exact threshold and full-word RNG
checkpoints, source family versus HP route, zero amount, nullification, explicit
scope and enum/input boundaries. Eight Python cases independently check modes,
roll math, draw ordering, status-record independence and tampering. Full validation:
locked restore, zero-warning/error Release build, **4,599 .NET tests** (1,558
Framework) and **627 Python tests**. Source/prepared audit pins nine files/ten
functions, confirms the empty status record and four Fire tier bindings. Prior
element/Faith, guide, coverage, local-link and owner-draft checks pass.
[Implementation receipt](../evidence/combat/magic-proc-implementation-v1.json);
ignored logs: `scratch/magic-proc-340/`.

This decides only whether the status applicator should be attempted. It does not
apply status sets, finalize eligibility/caps, use MP/charge, publish HP, add spell
commands or activate magic AI. [ZOD-341](magic-result-finalization.md) now adds
bounded living-target/empty-status result finalization, carrying special-effect/
accuracy facts and source upper999 caps. Full corpse/Golem/status behavior remains
outside that domain. [Magic evasion and fresh formula entry](magic-evasion.md) are now implemented;
[Current-input and ready-cast queries](magic-current-inputs-and-casts.md) now
compose these owners; ZOD-358 next traces original targeting geometry. Owner drafts, JobStats, host/editor, automatic facing,
corpse/terminal/frame RNG, broad interfaces and ZOD-106 remain deferred.
