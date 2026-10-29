# Pure Throw Stone AI formula projection — ZOD-186

Implemented 2026-10-29 against read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df` and the
[ZOD-185 source/data contract](../architecture/zod-185-ai-ability-iteration.md).

`ThrowStoneAiFormulaProjection.Evaluate` accepts immutable battle state, a typed
targeted-ability command, existing executable/grant/profile bindings and explicit
decision RNG. It invokes shared read-only `TargetedAbilityBattleSimulator.ValidateCommand`
for current actor/unused ACT, explicit grant, living distinct target, range1..4
and the existing two-unit clear-flat profile. No weapon is required. Execution
uses the same freshly called validator; neither path accepts a cached admission
as authority. Diagonal front evasion is shared through `ThrowStoneFormula.IsFront`.

The result keeps accuracy separate from full midpoint damage. PA5 gives10 damage
and HP50→40 at both accuracy100 and25. Zero accuracy retains four evasion draws
but has no damage; PA6 can lethally clamp HP11→0. Every successful query retains
exactly four BIOS draws, including zero accuracy. Formula midpoint helpers add
no draw, critical/proc or knockback. Live units, CT, choices, positions, facing,
ticks and both RNG copies are unchanged; unsupported input or exhaustion returns
incoming decision RNG and no partial output. No authoritative action is called.

Five focused scenarios cover literal damage/RNG (word1/count7→3295386429/count11),
range/diagonal facing, exact budgets, shared admission and actor/order/RNG replay.
All 804 AI cases and the existing Framework regressions pass after a clean Release
build. Results/logs: ignored `scratch/ai-throw-stone-186-188/`. The existing pinned
source/data audit is clean. No C/original execution, runtime activation or UI work.

ZOD-187 adds one hypothetical Move/ACT candidate and the existing effect-free
clock; scoring and general ability-list search remain later bounded steps.
