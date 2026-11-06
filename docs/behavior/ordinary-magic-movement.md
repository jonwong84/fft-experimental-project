# Ordinary magic battle movement

## ZOD-395

After the prepared Fire/elemental/Cure execution milestones, replace the player's
one-adjacent-step limitation with confirmed cost1 flat movement. Reuse the existing
[flat range/backtrace](flat-ordinary-movement.md) and
[voluntary path settlement](confirmed-path-movement.md) rules through an internal
kernel shared with the current ordinary battle owner. Preserve old sword/AI APIs.

Reference remains clean/read-only at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. Existing movement receipts pin source
range/backtrace, confirmation and turn settlement. Fresh study additionally read
`placement.c::move_get_post_events` / `move_get_ability_events`,
`pickups.c::action_charge_check_and_cancel` and the controller's post-move dispatch.
Movement cancellation tests the **Charge skillset**, not every Charging unit.
The selected ordinary Black/White Magic pending casts therefore survive walking.
Mounts, traps, pickups and movement RSM benefits remain outside this authored
bare-human/no-special-ground profile. Do not infer their absence from a tile ID.

Carry copied Jump and explicit initial facing with the unit definition. A
confirmed path publishes its actual final walking heading, Move usage, position
and any resulting ordinary CT settlement without ticking charges or drawing RNG.
This is voluntary walking, not automatic end-turn facing, which stays deferred.
Rebuild legal destinations/path from current authoritative occupants; ally
transit is permitted, occupied endpoints and enemy transit are not. Reject
unsupported maps and exhausted publication counters without partial work.

`OrdinaryMagicMovement.Query` now shares the existing internal flat graph and
backtrace kernel. Its new concrete input entry avoids fabricating sword combat
stats or adding a public replacement interface. Existing sword/roster callers
retain their entry points and the same algorithm. Confirmed movement requires
explicitly declared matching cost1 ground; older definitions without that
declaration retain their one-step subset. The prepared profile authors its own
cost1 ground and never infers cost from an original or unclassified terrain ID.
Movement additionally enforces original1..16 dimensions; a wider spell-only map
does not gain an approximate movement fallback.

The new Move command rebuilds a path and atomically publishes position, actual
final heading, Move usage and any ordinary turn end. Rejections retain the input
state and empty events. Copied Jump and explicit initial facing are immutable
definition inputs; omitted facing in older prepared JSON defaults to East.
The original one-adjacent Step command is preserved and now also carries its
unambiguous walking heading. Moves draw nothing, tick no charge counter and do
not spend MP. Spell confirmation after a move rebuilds blue range from the new
caster position. Act-then-Move finishes both choices once and preserves pending
Black/White Magic. Voluntary heading does not activate automatic wait-facing.

Twelve new managed cases cover literal path/packed headings, ally transit,
enemy detours, invalid endpoints, Move/Act order, current range, charging,
ground/map/KO admission, copied caps and exhausted counters. All1,753 Framework
and1,217 AI cases pass; prepared-content and existing CLI/player routes pass.
The [new receipt](../evidence/combat/ordinary-magic-movement-source-v1.json) pins
eight files/nine functions. The current source-only verifier also rechecks the
older15-file/42-function flat and13-file/33-function voluntary contracts plus
their48/60 independent descriptions. Seven new cancellation descriptions cover
ordinary spell survival, Charge, counter0/255 and the alternate Charge skillset's
absence from this exact predicate. Two Python cases pass.

The historical flat verifier still expects retired root-level managed file
paths and cannot audit the reorganized worktree directly. Its evidence/fixtures
are preserved; the current verifier audits their unchanged source/description
portions rather than rewriting historical baselines.

ZOD-396 will expose full legal Move range, preview/confirmation, path/facing
display and native replay checks. Terrain breadth, path animation, tentative
movement undo and post-move effects remain separate.
