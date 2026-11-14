# ZOD-160 — Ordinary preparation after refresh

Source/design, 2026-10-28. Read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base
`81d554e25e8c94305567476b9b8fe82fd47694ad` (ZOD-159).
The [preparation receipt](../evidence/ai/preparation-contract-v1.json) extends
the [refresh contract](zod-156-ai-refresh-prefix.md). No C/original execution.

## Source order and closed context

`ai_command_build` captures acting-unit state/delay before refresh, then calls
ability_considerations_set, reachable_tiles_build, team-policy update, target
consideration and action_select. `ai_simulation_begin/end` only switch simulation
mode and preserve the mimic-attacker block in this boundary; they add no draw.
These are synchronous logical stages; VSync suspension and frame BIOS draws
remain outside the managed query.

The smallest child is an already-moved, unused-ACT **NonBlue** actor, two living
stable ordinary human opponents and source ability lists containing Attack,
unusable Frog Attack and a terminator, with no additional entries. Both weapon
summaries certify range-one, zero-MP/CT/area, harmful physical ordinary sword
Attack without trajectory, random-use or status effects. Original list/cache
initialization is still an explicit input assertion, not an inferred property
of the managed weapon ID. The actor has no special decision behavior. Blue
direct-control and low-HP healer/crystal policy are excluded rather than treated
as equivalent to NonBlue. None/Critical remains supported.

`ability_considerations_set` clears ranged/healing hints on both present units;
ordinary Attack supplies one harmful physical ability each. The actor's helpful
share is zero, so MostlySupport is false; the enemy's cached MostlySupport is
not generally rewritten. Frog Attack is unusable for these non-Frog units. No
buff disabling or inventory/level branch applies. Actor enemy reach is raw
Move+1; ally reach is Move; random-use threshold is zero, MP recovery/Holy Water
and status-cancellation hints are empty. Neither ability preparation nor the
selected subsequent policy branches draws RNG. Attack usage policy remains
subject to the existing ordinary tile-policy certificate; do not infer it from
the computed reach alone.

`reachable_tiles_build` initializes water/walkable rows and clears scenario zero,
then movement_taken returns immediately with just the current actor tile.
There is no range spread, trap filtering or frame-budget wait on this branch.
Logical tile output need not reproduce original packed row dimensions.

Team preparation first clears single_enemy_remaining. A cached unavailable-support
flag exits early with retreat support false. Otherwise this two-unit profile has
one ally (the actor), hence no retreat support; NonBlue propagates unavailable
support to allies. The actor's resulting TeamSupportUnavailable is therefore
true in either path; the enemy's policy is preserved. **SingleEnemyRemaining is
false even though there is one enemy**, since its assignment occurs only in the
successful multi-ally support branch.

With no special behavior and MostlySupport=false, NonBlue target consideration
chooses manual autobattle, marks targetability, finds the nearest eligible enemy
and focuses it while clearing stay-near coordinates. The sole represented enemy
is living, targetable and Considered after fresh refresh. Nearest search has no
random tie selection. `action_select` resets ranks (not whole action payloads),
fills proximity, skips status-imposed/retreat/manual-autobattle actions and enters
general search. The living two-unit profile supplies two active units under ANY;
ordinary Attack subsequently targets the enemy. No charging branch runs. Attack
is the first and terminal usable entry; the unused Frog entry adds no draw.

The earlier cached actor delay needs separate proof: ai_unit_turn_delay_calculate
can return INT_MAX from a stale cached DeadWithoutReraise bit even for a living
unit. Require an explicit **CleanActorDelayCache** assertion. With it, actor and
target delay use restored live CT: ceil(max(0,100-CT)/Speed). Do not use the refresh
diagnostic CT or add hypothetical elapsed ticks to the live clock. Proximity at
the actor origin is max(0,10-ManhattanDistance) only when target delay <= actor
delay; otherwise zero. Original u8 actor-delay storage does not truncate 0..100.

## Selected children and acceptance

**ZOD-161:** add pure `SwordAiPreparation` which owns one fresh refresh call and
derives the closed reach, origin, focus, delays, proximity and policy hints. A
false-default profile requires OrdinaryAttackOnlyLists, NoSpecialBehavior and
CleanActorDelayCache; team is an explicit semantic NonBlue input. Reuse existing
candidate/score/refresh certificates and immutable retained policy rows. Return
only diagnostics and progressed RNG, never trusted state or general entry authority.
Reject unknown team/cache context atomically. Check both policy paths, Critical,
ready-peer/rounded delay boundaries and either actor/order replay. No extra draw
after refresh, including when its sole draw exhausts the available counter.

**ZOD-162:** compose fresh preparation with existing in-reach derived selection.
Carry actual preparation RNG into selection on the original live snapshot. Do not
accept a public cached preparation result. Derive focus/reach, fresh comparison,
target-active and initial ranking/first/terminal-entry facts from that owned call
and the closed list certificate. Keep normal scoring, canonical coordinates,
ordinary scenario/tile policy, target-selection equivalence and no-Defend as
explicit remaining assertions. Failures at either stage return the original RNG
and no partial diagnostics. Test literal full draw schedules (including lottery
changes), fallback, exact budgets and immutable replay. No command activation.

Existing selector/executor signatures and GeneralSelectionEntryContext remain.
After these children, actual Framework-owned fresh-prefix command integration
needs its own milestone; broader abilities/search, Blue policies, Move-first,
continuation, frame fidelity and native/editor activation stay deferred.

Validation: clean pinned audit covers 25 preparation functions plus inherited
receipts (`scratch/ai-preparation-160/source.json`); three focused Python checks
pass. No C# changed; prior full validation remains 3,863 .NET / 504 Python,
not rerun for this source-only checkpoint. Proposed managed cases await the children.
