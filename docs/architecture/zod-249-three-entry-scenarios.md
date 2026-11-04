# Three-entry pre-ACT scenarios and score provenance — ZOD-249

Source/design study, 2026-11-04, managed base `d9375d2`. Read-only fft_decomp
was freshly checked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/derived-three-entry-source-v1.json) pins the
selected functions and inherits the [three-entry order](zod-233-three-entry-ai.md)
and [retained-cell trace](zod-199-pair-out-of-reach.md). No original or C program
was executed and no prior workspace contents were reconstructed.

## Source ownership

The closed lists still contain Attack, Accumulate, Throw Stone, unusable Frog
Attack and terminator. Fresh preparation derives harmful2/helpful1, ranged=true,
enemy minimum range1 plus Move and ally minimum range0 plus Move. The added self
entry does not change the focused enemy's minimum reach, source movement score
arithmetic or ordinary ranged D0 policy. Both admitted teams retain their current
control/HP restrictions and raw Move<=124 domain.

`action_select` resets ranks and fills proximity across original reachable M0.
`general_select` calls `scenarios_build` once, seeds the baseline, then evaluates
entries in source order and performs one lottery. In-reach S filters M0 by
Move+target distance; ordinary flat geometry admits the whole reachable set by
the triangle bound. D1 uses priority/proximity then distance with later-coordinate
ties. Before ACT, ranged D0 uses max(3,distance-1) and its established fallback.

Out-of-reach S filters by distance-1, restoring M0 if no tile matches. Only S
receives fresh score writes and hypothetical clocks. NonBlue's modulo7 reach
draw follows those scores; Blue skips it. D1 chooses within effective reach or
uses distance-first fallback, and D0 copies D1. Scenario2 is M0 XOR S before
actor insertion; final scenario1 is S union actor and may overlap scenario2.
Adding actor neither writes its priority nor consumes a draw.

Priority is read as signed16, scaled by65536 and combined with fresh byte
proximity. `origin_next_take` favors the later coordinate on a complete tie.
Rank reset is not tile-grid reset; shared event/status staging can alias the
same source memory. An unwritten cell remains unknown, not zero or necessarily
an earlier AI score. The earlier retained-cell study owns the alias evidence.

## Reads by the three entries

The ordinary non-arc Stone cast mask intersects scenario1 with the useful hostile
target's range4 diamond before ranked origins read keys. Attack range1 is a
subset. In this single-clear-target profile, Stone consumes the aim even when
NotValuable; a different first origin can therefore change its result and RNG.
Reject a derived action search if any unwritten final scenario1 cell is within
range4. At larger distance the added actor cannot cast either damage action and
may be omitted from the fresh scan mask without assigning its priority. Move0's
empty closer filter instead scores the actor, so it can remain a safe cast key.

Accumulate follows the map-target path: self target, one usefulness filter and
ACT-first/Move-first candidates at D1. It does not enumerate cast-origin keys,
so the damage-action unread proof must not remove self evaluation. Keep the
Attack → self coin/clocks → Stone order, independent actual-state candidates,
shared ranks and single terminal lottery. No actor rescore, repeated refresh or
movement prefix may be inserted between entries.

## Selected children

- **ZOD-250 — Shared fresh scenario derivation.** Extract the established pair
  in/out-of-reach mask/score/destination/provenance kernel; route both existing
  pair wrappers through it without changing their result types or streams.
  Add a three-entry scenario owner using its own fresh preparation, with automatic
  reach and explicit fresh-versus-unwritten cells. Do not forge pair lists or
  accept public cached prefixes. This query stops before action scans.
- **ZOD-251 — Derived three-entry composition.** Own those fresh scenarios,
  prove cast-key writes or non-reads, derive scan/baseline policies from preparation
  and pass only the private fresh prefix to the existing entry/rank owner.
  Preserve old supplied-scenario composition. Failure hides the entire prefix.
- **ZOD-252 — Derived terminal selection.** Reuse the existing lottery owner on
  derived fresh ranks. Return scenario/provenance plus typed selection, with
  source default/terminal/no-Defend assertions and whole-query rollback.
- **ZOD-253 — Derived first-command execution.** Own that query inside the same
  Framework callback and reuse the current typed dispatch/proposal owner. Add no
  authority or public cached-result input; preserve per-command adoption/recovery.
- **ZOD-254 — Derived complete turns.** Reuse the existing stage/recovery owner
  with a frozen derived entry profile rather than supplied S/D0/D1. Preserve
  post-ACT automatic reach, actual boosts/damage, exact tokens/receipts, event
  isolation and terminal failure boundaries. Keep all old turn APIs and streams.

Use immutable profile inputs with explicit ordinary/general/no-charge entry and
clear direct-trajectory facts. Newly owned masks/score cells/prepared scan policy
must be derived, rather than asserted as user-supplied scenario truth. Unknown
cast keys are an explicit rejection even if a self candidate might later win.
Tests should include in-reach parity, range4 rejection/range5 unread proof,
Move0 fallback, actor/scenario overlap, both teams, exact/late budgets, complete
turns and retained recovery without mutating input or definition drafts.

This does not reconstruct original workspace history, broaden policies/rosters/
maps, activate host/editor work or battle saves, or settle original frame/VSync
RNG. ZOD-106 remains deferred and boosted PA remains battle-only.
