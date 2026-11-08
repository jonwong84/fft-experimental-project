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
at `0x001870fc` is now checked too. Conditional Status Proc Roll (19%) at
`0x001884c0` is next. ZOD-436 remains open.

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
