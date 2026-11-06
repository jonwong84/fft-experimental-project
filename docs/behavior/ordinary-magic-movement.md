# Ordinary magic battle movement

## ZOD-395 — selected next

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

The next player child will expose full legal Move range, preview/confirmation,
path/facing display and native replay checks. Terrain breadth, path animation,
tentative movement undo and post-move effects remain separate.
