# ZOD-139 — Ranked lottery and movement fallback

Source/design checkpoint, 2026-10-27. Read-only fft_decomp HEAD:
**11464943bbdbd2a0af022d90cb7f10455cf911df**, clean at inspection.
[Evidence](../evidence/ai/selection-contract-v1.json) extends the composition source
receipt with four functions and four headers. No C# selection API or original/C
execution is added here. The bounded implementation follow-up is ZOD-140.

## Original selection after composition

`turn.c::general_select` finishes `ability_list_evaluate` before calling
`ranked_select`. The loop evaluates entries until the source terminator/capacity;
one completed Attack pass is not automatically the end of that loop. To continue
the one-pass composition truthfully, require that this Attack is the terminal
entry and no further ability work occurs before lottery. Multi-ability selection
remains separate; do not choose early from a partially searched list.

`ranked_select` visits eight slots in order. Each visited slot calls `rand()`,
then tests **raw % 100 < hitPercent**. Hit is u8, promoted into s32; admitted
ordinary hit percentages are 1..100 and reset slots are 0. These are not the
multiply/divide evade rolls used during combat. Equality fails. Zero-hit slots
still draw. Success copies the entire 32-byte action record and returns at once.
If every roll fails but any slot had nonzero hit, copy **slot 0** and return 1.
If all hits are zero, return 0 without copying any action.

In ZOD-138's fresh-empty-list/one-pass domain, only slot 0 can hold a real Attack.
Its hit is positive: zero accuracy yields NotValuable, not a scored ranked entry.
Therefore an admitted Attack is ultimately selected **even if its first roll
fails**. First success costs one draw; first failure visits seven empty slots,
then selects the same Attack after eight draws. Hit=100 always takes one draw.
An empty list takes eight draws and requests movement fallback. Priority zero is
still a valid Attack score when it beat a negative baseline; it is not absence.
Do not simplify away the lottery just because the eventual Attack is certain.

## Fallback writes, support and selected payload

When lottery returns 0, `general_select` sets movement_scenario=0 and calls
`fallback_select`. It writes only:

- selected `act_before_move=0` (move before any action);
- selected coordinates from **D0**, not the Attack destination D1;
- selected skillset=Defend (2) when support ability is Defend (479), else 0.

It performs no draw, movement simulation, scoring, CT advance or best search.
Source support is u16 (read into s32), skillset is u8, and coordinates copy as u32;
Defend's 479 support ID is not a byte-sized flag or the skillset's numeric value.
It does not copy the scored baseline action. Other fields retain their earlier
contents. On fresh ordinary selection, `ai_action_rank_reset` left selected rank
at priority=-32767/hit=0/decided=0, reflected=false, wait hint=5 (ChooseAtWait).
Unused ability/item/target payload can remain stale; skillset=0 makes it inactive.
Typed managed MoveOnly output should omit that unused payload, rather than
inventing an Attack target or claiming the baseline score is its selected rank.
The fresh baseline remains available as a separate diagnostic.

The live bounded snapshot has no original support-ability field. The smallest
next slice must explicitly certify **NoDefendSupport**; it cannot infer that fact
from zero MP, equipment, PA or IDs. Defend is documented by the offline source
kernel but excluded from the proposed managed selector. Unknown support is not
permission to silently choose MoveOnly. Movement fallback can also mean staying
at the current tile; it consumes no real Move/ACT choice at this query boundary.

For Attack, preserve the freshly ranked candidate's order, destination, target
panel/identity and score diagnostics. Lottery does not re-project, resolve a hit,
apply damage or probability-weight priority. In the admitted normal context the
current action retained ChooseAtWait/reflected=false from reset; certify those
fresh ordinary defaults rather than assuming them for inherited/special policies.

`ai_command_build` marks decided=255 **after** `action_select` returns, ends
simulation, then dispatches MOVE/ACT/END_TURN and records moved/acted flags. That
command lifecycle is outside this selection slice; selected-plan diagnostics
grant no authority or already-decided state. `evaluation.c::ai_wait_facing_choose`
resolves hint 5 later, against settled positions when waiting. Preserve a semantic
deferred hint; do not run the facing chooser on hypothetical movement/KO outcomes
or translate it to KeepCurrent. Existing facing execution stays separate.

## Minimal fresh C# continuation: ZOD-140

Take the same original inputs and explicit decision RNG as ZOD-138, plus named
boolean certificates (default false) for **TerminalAttackEntry**,
**NoDefendSupport**, **FreshOrdinarySelectionDefaults**. Terminal means this pass
completes the original ability loop, not merely that it currently has the highest
score. Defaults cover both selected/current action hints/reflection and fresh
selected reset provenance. Validate all new facts before any composition work.

Call `SwordAiComposition.Evaluate` exactly once privately. Do not accept public
composition/scan results, ranked records or intermediate RNG checkpoints as
trusted input. Run lottery from that fresh result's progressed stream, drawing
one checked output per visited slot. Record slot/raw/modulo/chance/success for
diagnostics. Explicit empty slots have chance 0 and still consume their draws.
Keep a distinct selection reason for first-roll success, all-rolls-failed use-first
and no-ranked-action movement fallback; the latter two are different behaviors.

Return immutable composition/lottery diagnostics and a typed Attack or MoveOnly
plan, with actor/final destination, an optional fresh Attack reference and the
deferred wait hint. MoveOnly refers to D0 and has no scored Attack payload. A
public cached result must never become a command receipt. Do not expose general
arbitrary-list selection merely to test slots 1..7 with real actions.

Successful total RNG cost is the fresh composition's cost plus **one or eight**
lottery draws. Fallback adds none. Selection adds no ticks; composition retains
its original per-evaluation clocks. On any new-profile, composition or lottery
failure, publish no partial composition/selection/plan and return the whole
query's original RNG. Preserve nested composition failure or failed lottery slot
index. This is synchronous managed transaction policy, not original C rewind or
VSync resume. Check only draws actually visited: do not reserve eight in advance
and reject a supported one-draw selection at MAX.

Full scenario/ability/roster search, Defend, inherited/special selected payloads,
the replaceable decision interface, command/RNG commitment and native/editor
activation stay outside this milestone. Existing standalone APIs stay unchanged.

## Focused acceptance cases for ZOD-140

These **unexecuted managed proposals** use certified synthetic inputs, not
observed original-game decisions. Reuse existing composition coverage rather than
copying all CMP cases or testing private selector shape.

- **SLC-001:** Already-moved actor adjacent to a living target; S={actor}, D0=D1=
  actor, PA/Power=5, both HP=50/50, actor CT=15/target CT=95, both Speed=5.
  Movement plus one nonlethal Attack costs six draws. From seed=1/count=7, lottery
  entry is word=2516284547/count=13; next raw23010 has modulo 10. Hit=11 (front
  evade=89) selects Attack in one draw: word=3655513600/count=14.
- **SLC-002:** Same inputs, hit=10 (front evade=90). Equality fails; all eight
  lottery slots are visited, but the same Attack is selected, never MoveOnly.
  Final word=2937909499/count=21. No hit/damage re-resolution occurs.
- **SLC-003:** No admitted entry, including equal-baseline and real score-absence
  paths, consumes eight lottery draws then returns MoveOnly at D0. D0/D1 may
  differ; do not substitute D1, a target tile or a scored Attack. Inactive/no-origin
  composition with one movement draw finishes at word=3210001534/count=16 from
  seed=1/count=7. Fallback adds zero draws/ticks.
- **SLC-004:** Improving negative and genuine zero-priority admitted Attacks are
  selected normally. Preserve fresh candidate order, target and deferred hint;
  never execute projected damage, Move, Wait or facing changes.
- **SLC-005:** Each new certificate false/unknown rejects before composition,
  including a would-be certain Attack. Document the source Defend branch without
  treating it as supported MoveOnly. Existing invalid-input cases propagate.
- **SLC-006:** Exact-MAX completion succeeds for actual one/eight-draw paths.
  Lottery exhaustion after a successful composition restores original query RNG
  and publishes no partial output; identify the failed slot. Earlier composition
  failure is not retried and no lottery work runs.
- **SLC-007:** Replays, both actor IDs, collection order and explicit RNG carry
  preserve original snapshot/live RNG. An already-ready clock can complete at
  MAX ticks; the selection tail itself reserves no ticks.

## Reproduction

Five focused [Python checks](../../tools/verification/test_ai_selection_contract.py)
reuse the existing lottery arithmetic and add supplied-tail counter boundaries,
effective fallback writes and the exact Defend source branch. They execute no
fresh managed selection, original code or original assets. Source receipt audit
is explicit and separate from routine tests:

```powershell
python -m unittest discover -s tools/verification -p 'test_ai_selection_contract.py'
python tools/verification/verify_ai_selection_contract.py --reference C:/repositories/fft_decomp --output <new scratch report>
```
