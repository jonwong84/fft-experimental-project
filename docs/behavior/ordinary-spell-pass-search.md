# One ordinary spell pass across origins — ZOD-440

`DefaultAbilityAiPassSearchQuery.Evaluate` now evaluates one freshly derived
offensive spell pass across its source-ordered cast origins. The pass shares
remaining aims and RNG; each hypothesis restores private units. Its best result
and movement-baseline admission are diagnostics without command authority.

Managed base: `35643aecfdcc77822489d0339a2ff7fdc242c204`. The clean read-only
reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`. Read the
[origin study](ordinary-spell-movement-origins.md),
[potential origins](ordinary-spell-cast-origins.md) and
[single-origin simulation](ordinary-spell-origin-evaluation.md) for the inherited
admission, masks, placement and clock rules. The
[receipt](../evidence/ai/spell-pass-search-implementation-v1.json) pins source
functions, managed files, source-derived literals and validation. No C or original
executable was run.

## Entry point and routine flow

Start at
[`DefaultAbilityAiPassSearchQuery`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiPassSearchQuery.cs).
Supply the actor, owned primary slot, Main/Complement scenario and caller pass.
It binds `DefaultAbilityAiWork` and internally derives fresh potential aims and
movement through `DefaultAbilityAiOriginQuery.Build` once. No cached caller
outcome, movement record, rank or command authority is accepted.

Then follow
[`DefaultAbilityAiPassEvaluation`](../../src/Zodiac.Framework.Ai/DefaultAbilities/Simulation/DefaultAbilityAiPassEvaluation.cs):

1. `OriginReachableEvaluate` owns the pass-wide remaining-aim mask, shared RNG,
   source origin order and best candidate.
2. `OriginAimTilesEvaluate` creates a private `ForOrigin` baseline and visits
   only that origin's selectable aims still in the shared mask.
3. Shared `AiActionSimulateAndScore` performs ordered movement, preparation,
   strikes, clock and scoring. Immediate/delayed ACT-first and MOVE-first retain
   their existing semantics.
4. `AiRankIsHigher` compares priority and hit, then strict lower **final movement
   destination** proximity. Exact ties retain the first candidate.
5. The movement-baseline gate compares priority/hit only, independently of the
   best candidate's destination. Equal baseline rank is not admitted.

The existing single-origin query now uses the same runner with consumption
disabled and one selected origin. Its public result and all seventeen prior
regressions are preserved. Public pass diagnostics contain per-origin RNG
intervals, remaining/consumed aims, evaluated candidates and unresolved exits.

## Proof of dynamic consumption flags

At the pinned revision, `action_hits_simulate` marks `ability_processing_done`
before calling `strikes_simulate`. Fresh accounting resets hit count, accumulated
hit percent and valuable membership. `strikes_simulate` sets local `self_hit`
after these exclusions: positive accuracy, targetability, followed-anchor movement
and non-allied targets' move opportunities. It then tests valuable membership
separately. In the admitted single-strike, fresh-accounting domain, the existing
`Accounting.IncludedTargets` therefore supplies exactly the source self-hit
membership. Neither raw hit frames nor `Accounting.Valuable` can replace it.

`action_init_current_data` checks actor/resource/reaction initialization; an
empty recipient list does not fail initialization. The admitted living actor,
available MP and absent reactions establish successful processing even when
there are no recipients. Thus an empty, nonvaluable attempt can consume an aim
without spending formula draws. A nonvaluable attempt with an included self hit
can instead retain it. The new regressions distinguish both cases.

Every evaluated aim starts with the pass's explicit consumption setting.
For ranked origins, adverse self effect plus **no included caster hit** also
forces consumption. The source's useful-self override is still gated with its
separate pathway. Exhaustive origins clear the explicit setting and do not use
the ranked dynamic override.

Rank comparison happens before the `ability_processing_done` check. An
unresolved charge is compared with hit255, then stops only that origin **before
consumption**. Later origins continue with the same remaining aims and RNG.
Ranked scans stop before taking another origin when no aims remain; exhaustive
scans retain map order. Normal nonvaluable attempts retain actual draws. Any
unsupported work or overflow rejects the entire query with its entry RNG and no
partial best result.

## Independent regression evidence

Thirteen new cases in
[`DefaultAbilityAiPassSearchTests`](../../tests/Zodiac.Framework.Ai.Tests/DefaultAbilities/DefaultAbilityAiPassSearchTests.cs)
use literal geometry/order and source draw accounting:

- Default unit aim is dynamically consumed at origin(1,3): prefix15 plus4
  delayed formula draws gives19; immediate casting adds one clock draw for20.
- Explicit moving-by-deadline consumption survives an included self hit:
  prefix13 plus8 formula draws gives21 and score-22.
- Retained tile aims traverse origins(0,4), (0,0), (1,4), (0,3), (0,1), (1,0),
  (1,3). Evaluation counts are7/5/3/1/1/0/1; consumption counts4/3/2/1/1/0/1.
  Cumulative draws60/93/112/117/122/122/127 prove skips, an empty visit and shared
  RNG. Every hypothesis starts with fresh HP/MP.
- Equal rank at a later ACT-first origin replaces an earlier result because
  its final destination is closer:39 draws. Exhaustive map order keeps the
  first exact tie:40 draws. Equal baseline rank still fails admission.
- A caster frame excluded after a move opportunity does not count as self hit:
  prefix2 plus1 upkeep and8 formula draws gives11; the aim is consumed.
- Six congested unresolved origins each spend255 clock draws, retain all15
  aims despite an explicit consumption setting, and compare hit255 before exit:
  prefix7 plus6×255 gives1,537 draws.
- Nonvaluable included self hits, empty recipient lists, empty/unavailable
  passes and late overflow verify membership, restoration and atomic rejection.

Final seed values are cross-checked independently using affine composition of
the BIOS recurrence, rather than replaying the managed generator. Full Release
validation: zero warnings/errors, **5,247 .NET tests** (1,442 AI;1,835 Framework)
and **802 Python tests** pass. The source audit validates24 origin descriptions,
four prepared routine ranges and thirteen instruction anchors; the prior rank
audit validates four iteration descriptions. These support the admitted domain,
not general retail equivalence.

## Remaining boundaries

This is one selected pass in the inherited fresh offensive, lower-flat,
bare-human domain. Cross-pass composition is next under ZOD-441; source reset,
remaining-mask and insertion ownership must be traced before composing callers.
Full ability-list/scenario dispatch, rank insertion/lottery and autonomous magic
commands remain separate. Mixed/healer lists, useful-self paths, retained
workspaces, broader statuses/equipment/terrain/mounts/pickups/Golem, immediate
caster-KO terminal handling and full corpse/Reraise/frame RNG retain their gates.
Owner drafts/JobStats, Knight slot mapping, editor and custom cone are unchanged.
The AI review programme and ZOD-432 readability walkthrough remain open.
