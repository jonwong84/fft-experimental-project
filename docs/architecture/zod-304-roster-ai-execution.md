# ZOD-304: authoritative roster AI integration prerequisites

Reference clean/read-only `11464943bbdbd2a0af022d90cb7f10455cf911df`;
managed base `ad34551`. This study bridges the
[pure full-list decision](../behavior/roster-basic-skill-ai-selection.md) to
actual commands without granting authority to cached public plans.

`ai_command_build` runs refresh/preparation/selection when actual choices change,
marks the selected action decided and ends hypothetical simulation. Move comes
first when Move is unused and either ACT was already used or the plan is
Move-first, provided destination differs from the actual origin. Otherwise
ACT is emitted if unused and the selected skillset is nonzero; otherwise END.
Move-first retains its selected command for continuation. ACT-first requires a
fresh decision from the actual result before unused Move/END; projected damage,
PA, KO, coordinates or midpoint RNG must never become actual battle state.

## Current managed boundaries

Framework `RosterFacingTurnBattle` already owns immutable roster sessions,
actual Move/Attack/Advance/Wait/END and explicit fixed/kept facing. Revision,
lineage, exact battle and pending-receipt checks prevent stale or foreign
completion. The old pair `FacingDecisionBattle` privately issues proposals,
verifies callback ownership and RNG continuity, stages actual state and commits
one command atomically. Its Move continuation freezes the selected command.
The AI pair complete-turn driver preserves accepted progress on later failures
and requires exact-session recovery. None of these pair types accepts the new
roster plan or roster session; do not cast, fabricate receipts or loosen guards.

`ImmediateAbilityBattleSimulator.Apply` already admits a complete roster with
one explicit zero-bonus binding per unit, PA1..99 and no initial Dead. It changes
only the actor's PA through min99(PA+1), consumes ACT via the shared choice owner
and draws nothing. It does not impose a pair count. A roster session wrapper
can reuse this kernel through the existing private Apply/Publish/receipt owner.
No new effect, renderer, decision RNG or authority mechanism is needed.

`TargetedAbilityBattleSimulator` retains an actual two-unit admission. Actual
Stone performs physical evade with short-circuit draws, then a real damage draw
and two Brave rolls; source knockback chooses Y on equal horizontal magnitudes,
checks the destination against every occupant and preserves actual KO ordering.
AI instead consumes four evades, uses midpoint full damage and no knockback.
Do not execute AI projections or obtain a roster result by repeatedly invoking
the pair command on detached opponents.

The live `projectile_direct_check_to_target` differs from AI's between-units
query: it excludes source but includes the selected target in the obstacle list,
and can return an intervening unit or terrain failure. This affects actual target
resolution and cannot be silently replaced with the AI boolean. Before actual
roster Stone, choose a source-backed clear-path admission or implement the exact
interception path. The exact SDK/height sampling owner currently lives in AI;
Framework must never reference AI. A real second consumer justifies moving the
pure geometry kernel/metadata to the authoritative lower layer, retaining a
compatible AI facade rather than duplicating algorithms or adding friend access.

## Selected children and later boundary

- **ZOD-305:** expose the existing actual self kernel through the roster session's
  private command publisher. Verify both orders, PA99, unused-Move refund,
  complete peer/RNG preservation, rejected grant/setup, pending receipts,
  revision exhaustion and foreign receipt rejection. Explicit facing remains.
- **ZOD-306:** trace actual roster Stone interception, collision/knockback and
  admission before selecting its runtime child. Preserve pair behavior and the
  AI/live formula distinction; keep extracted data ignored.
- After actual abilities: Framework-owned verified roster decision transaction,
  retained Move continuation, AI first/post-ACT dispatch and exact-session turn
  recovery, each as bounded source-backed children. Publish priorities only
  with accepted command progress; failures before a command remain atomic.

Eight independent self-settlement vectors separate actual PA/choice/CT/no-RNG
behavior from AI rank credits. Future execution tests must also use source-traced
actual Stone RNG/occupied landing vectors, late failure and continued partial
progress. The existing pair full-turn evidence is a foundation, not roster proof.
Full corpse lifecycle, terminal routing, automatic facing, frame-interleaved RNG,
host/editor, owner drafts, replacement interfaces and ZOD-106 remain separate.
