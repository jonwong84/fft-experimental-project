# One ordinary spell origin/pass — ZOD-439

`OrdinarySpellAiOriginEvaluationQuery.Evaluate` now evaluates the aims at one
freshly derived potential origin and pass. It shares existing preparation,
formula/application, clock and scoring code. Best-aim selection and movement
baseline admission are separate diagnostics; neither grants command authority.

Managed base: `2963ebc011d638aa7983627969118b4c9717ae39`. The clean read-only
reference is `11464943bbdbd2a0af022d90cb7f10455cf911df`. Read the
[origin study](ordinary-spell-movement-origins.md),
[movement baseline](ordinary-spell-movement-baselines.md) and
[potential enumerator](ordinary-spell-cast-origins.md) for caller/mask boundaries.
The [receipt](../evidence/ai/spell-origin-evaluation-implementation-v1.json)
pins source routines, managed files and final validation. No C or original
executable was run.

## Entry point and simulation order

Start at
[`OrdinarySpellAiOriginEvaluationQuery`](../../src/Zodiac.Framework.Ai/OrdinarySpells/OrdinarySpellAiOriginEvaluationQuery.cs).
The request selects a current owned slot, Main/Complement scenario, pass kind
and cast-origin coordinates. The query freshly binds `OrdinarySpellAiWork` once
and calls internal `OrdinarySpellAiOriginQuery.Build`. An unavailable origin/pass,
unknown ranked priority or unsupported context rejects with the entry RNG.
No caller-supplied origin result, rank or movement record is accepted.

`ForOrigin` narrows valuable membership to the selected pass, retains all peers
for formula recipients and scoring, and privately places the caster at the
cast origin. This mirrors `origin_aim_tiles_evaluate`, which places the actor
before `ai_action_simulate_and_score` saves its baseline. The original live
position remains available separately for movement/refund comparisons.

`OriginAimTilesEvaluate` walks that origin's source-ordered aims through shared
`AiActionSimulateAndScore`. Each hypothesis starts from the same private unit
baseline while RNG continues from movement preparation and earlier attempts.
The shared simulation now supports the selected origin's order:

- MOVE-first applies `MovementPickupSimulate`, then prepares and resolves or
  charges the spell at the placed origin.
- Immediate ACT-first prepares and resolves at the original cast origin. An
  ordinary NotValuable result exits before movement/clock. Otherwise movement
  follows the hit, then the turn ends and the clock advances.
- Charged ACT-first prepares at the cast origin, moves, then advances the charge
  clock. Ready area/recipient calculation uses the caster's final position.

The admitted `MovementPickupSimulate` only places the private caster. Crystal,
treasure and other effects remain gated. MovementTaken compares the final
destination with the **cached original live origin**, not the temporarily placed
caster. Thus MOVE-first still consumes Move even though its snapshot is already
at the cast tile. Unused Move refunds only when genuinely unused. ACT is marked
for the existing shared clock; all other units retain their original choices.

The existing current-origin queries keep their no-movement behavior when no
selected-origin view exists. Confirmation now uses the freshly owned spell and
that view's caster through Framework's existing choice/capture/preparation
owners. No synthetic physical battle or second spell formula implementation is
introduced.

## Ranks, exits and rollback

Best selection starts at fresh marker 0/priority -32767/hit 0 and uses shared strict
priority/hit comparison. Every candidate here has the same final destination,
so destination proximity cannot distinguish an exact rank tie; the first equal
aim stays best. The output reports final-destination proximity independently of
the cast-origin visitation key.

An unresolved charge's hit 255 score is compared **before** the origin stops.
The candidate can become best and pass an equal-priority baseline with hit 100.
Normal NotValuable attempts retain their formula/clock draws, produce no best,
and do not stop later aims. Baseline admission compares only rank against D0's
fresh baseline; better destination proximity alone cannot pass an equal rank.

Unsupported work discards all diagnostics and restores the complete query's
entry RNG, including movement preparation and prior valid candidates. Success
also leaves the input battle, all peers, MP, learned rows, choices and live RNG
unchanged. Returned private units are diagnostics. A NotValuable result retains
the pre-simulation cast-origin baseline; it does not adopt partial resources.

Initial admission remains the certified offensive normal general, dry flat
lower-layer domain. Immediate caster KO retains the existing unsupported
terminal-clock boundary. Existing enemy-KO plain-corpse upkeep remains supported
without enabling general death counters, Reraise, terminal handling or frame
continuations. Healer/mixed lists, retained priorities, broader statuses,
equipment, terrain, mounts, Golem, editor and cone work retain their gates.

## Evidence and next work

Seventeen new managed cases verify ACT-first and MOVE-first placement, no false
Move refund, ready caster position, self-collateral order, independent candidate
state, strict ties, baseline admission, unconsumed potential aims, nonvaluable
RNG continuation, empty scans, hit 255 exits, late rollback, invalid selections
and both KO boundaries. The existing congested setup is shared with current-
origin tests through a test-only visibility change.

The Release build has zero warnings/errors; all 5,234 .NET tests and 802 Python
tests pass. The receipt records seven project reports and source/prepared audits.

Source-literal examples: ordinary Fire leaves the enemy at 146 HP, caster MP 69
and score 49. Instant ACT-first self-collateral at area 2 leaves caster HP 46 and
score -22; charge 2 moves out of that area before readiness, leaving caster HP 100
and score 49. Four tile aims retain the first score 49 at (3,1), consume 39 total
draws including the 15-draw prefix, and end seed 400935288 from 57. Zero-cost,
zero-power damage ties the baseline rank and fails admission despite better
destination proximity. A congested unresolved origin stops after 255 candidate
draws with priority -6774/hit 255 and passes its equal-priority movement baseline.

The owner's concurrent `AiTurnController` edit is preserved exactly in a
separate formatting-only commit; non-whitespace text is unchanged. Ten owner
drafts/immutable JobStats and disabled CI remain intact. Historical evidence
retains its recorded controller blob; this receipt records the new owner blob.

ZOD-440 will evaluate one fresh pass across its cast origins, tracing dynamic
caster-driven aim consumption before porting it. Shared remaining aims,
destination-proximity ties and per-origin unresolved exits belong there. Whole
ability dispatch, cross-pass/list composition, insertion/lottery, actual magic
commands and autonomous turns remain later milestones. No native presentation
was rerun for this pure diagnostic entry.
