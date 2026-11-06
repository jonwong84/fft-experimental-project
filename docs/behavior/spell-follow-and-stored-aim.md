# Unit-follow and stored spell aims

## ZOD-363

This source trace is pinned to clean read-only `fft_decomp` revision
`11464943bbdbd2a0af022d90cb7f10455cf911df`, studied from managed baseline
`306ab8ee781be6da3921c1bac2852750327b0095`. The
[source receipt](../evidence/combat/spell-follow-source-v1.json) fingerprints
19 files and26 functions. The [42 literal descriptions](../fixtures/behavior/spell-follow-contract-v1.json)
and offline verifier describe selected source branches independently; they run
neither original/C nor managed code. Controller, clock, menu and weapon routines
are caller/boundary evidence, not claims that their complete behavior is described.
There are no production code, command or scheduling changes in this ticket.

### Choice and command storage

`target_range_set_for_action` derives blue panels and returns a choice mode.
For ordinary Default/Monster abilities, `flags_2.NoFollowTarget` (`0x10`) makes
the nonempty result TILE_ONLY1 instead of UNIT_OR_TILE0. No panels returns NONE3.
Auto with range0 and no weapon-range flag returns NO_TARGETING2 first. Weapon,
Item, Throw and Jump menu branches force tile-only and remain separate slices.

`battle_state_targeting_range_update` admits a player's cursor tile only when its
blue ability-range mark is set. `target_tile_select` initially writes TILE5 and
cursor coordinates for a player, or keeps an AI command's prior target. An action
with no targeting writes the actor tile. It computes green preview panels, then
confirmation chooses what will actually be stored. AI display handling does not
constitute the player's blue-mark validation path.

`battle_state_confirm_action_enter` chooses a prompt from the choice mode and
cursor occupant: option1 permits following an occupant, option4 is an occupied
tile-only action, option2 is an empty tile, option3 is an occupied self-centred
action. `request_dispatch` chooses the actual prompt variant, also considering
the selected CT and item. These are presentation choices, not resolution guards.

`battle_state_confirm_action_update` stores TILE5 verbatim for the tile answer.
For UNIT6 or YES7, an occupied cursor stores the **answer itself** and target ID;
an empty cursor falls back to TILE5. The ordinary resolver follows only exact6,
so YES7 uses the stored tile despite having a target ID. An AI confirmation starts
the attack without rewriting its chosen type. Cancel does not commit an aim.
Do not normalize YES7 to UNIT6 or apply NoFollowTarget again during resolution.
These observations establish the branch behavior, not every prompt's reachability.

`unit_find_selectable_at_tile` uses runtime-list positions and requires battle
stats, an occupied slot and no Crystal/Treasure/Jump. With overlaps it prefers the
source runtime unit, then a rider, then list order; its unbounded16-entry candidate
array is not a new managed limit. This cursor selection is distinct from the
source-slot ordered affected-unit list used by combat.

The original20-byte `action_command_t` stores actor, targeting type and target ID
as unsigned bytes; target coordinates are signed16 `SVECTOR` values in X/layer/Y
order. `unit_command_state_t` overlays the24-byte turn command: cursor `target_panel`
at offset16 aliases the action's `target_position` (turn payload offset4 plus
action offset12). Thus confirmation's cursor-coordinate write updates the stored
action coordinates too. It is not a second independently authoritative aim.
`action_command_copy` preserves all20 bytes. Managed immutable aim data should
represent the behavior without copying this memory overlay.

### Preparation, charge and resolution

`action_execute_ability` calls `action_commit_preparation`, which runs
`action_prepare` phase1 into the actor's stored command. Before ordinary ability/
CT work it rejects an invalid/inactive actor and, for exact UNIT6, an out-of-range
or inactive target slot. It does not replace stored coordinates with current
target coordinates. The existing MP/charge trace owns CT widths/support behavior;
this ticket adds no spend or countdown rule. `turn_clock_run` decrements admitted
charge counters and later emits readiness; the command aim is not recomputed in
that countdown branch. Commence-attack copies the stored command back to runtime.

At readiness, `action_announce_next` calls `action_init_current_data`: actor/
reaction checks and the MP helper precede strike initialization and mimic capture.
There is no repeat of `action_prepare`'s target-active check there. Actual state
publication and mimic/reaction work remain outside the pure magic query.

`action_resolve_strike` resets nearest-first and calls `action_tiles_calculate`.
For the admitted ordinary nonweapon/nondirect/nonrandom slice, that dispatches
`target_area_set_for_action` -> `ability_area_set`. The wrapper checks the actor
slot/activity and, only for exact UNIT6, the target slot bound. It does **not**
check the target's current activity or combat status before reading its position.

For ordinary nondirectional abilities the centre is selected as follows:

- Exact UNIT6 reads target **current** X/Y/terrain sheet, ignoring stored coords.
- Every other type reads each stored signed16 coordinate narrowed to unsigned8.
- The resulting X/Y must fit the map and sheet must be below2; a blocked centre
  rejects. An untargetable centre does not reject this selection stage, although
  green collision filtering subsequently clears untargetable affected panels.

No current caster blue-range, NoFollowTarget, target-team, alive or active test is
inserted into centre selection. A target moving during charge can move the area
beyond the caster's current range and onto another sheet. A tile aim remains at
its stored location, possibly affecting another occupant. An inactive/Jump anchor
can still centre the area on its stored current position, then be excluded by
`target_is_unit_untargetable_and_store_tile`; eligible bystanders can remain.
Dead/Petrify/Wall are not excluded by that occupant filter. Their formula/status
handling is separate, and the existing bounded magic query rejects unsupported
contexts atomically rather than silently dropping those occupants.

The source's fixed21-slot validation is a compatibility guard in these offline
descriptions, not a managed roster size cap. Inactive source slots still contain
position fields. A future managed resolver must require an explicit known anchor
snapshot and identity, even for an inactive anchor; absence of a snapshot must
reject, never use a default position or fall back to the old tile. Slot reuse and
generation mapping need separate lifecycle evidence before live command adoption.

### Exceptions and inconsistent consumers

Directional flags choose the actor's current tile before the ordinary centre
branch; the wrapper's exact-unit ID check still runs first. These branches are
diagnostic descriptions only. Direct/weapon targeting uses
`target_validate_for_weapon`, whose local follow-position copy is not used by
the later tests reading the actor's stored position. Do not route it through the
ordinary follow helper. Item/Throw/Attack/Jump/Charge's single-tile area helper,
RandomFire, Calculator, unit-follow AI planning, reactions, reflection, Mime and
full scheduling remain separate.

`cursor_move_to_action_target` uses the opposite discriminator: exact TILE5 reads
the signed stored coordinates into the signed32 cursor globals; every other type
attempts a runtime-unit lookup. Thus a raw YES7 may position that cursor using a
unit while the ordinary area resolver uses its tile. Preserve consumer-specific
tests at any future raw-command adapter; do not infer combat rules from a cursor.

### Selected implementation child

**Next ZOD-364: Resolve certified ordinary stored spell aims.** Add one pure,
source-traced aim query that distinguishes a stored map aim from an explicit
stable-ID follow anchor, resolves current coordinates with original narrowing at
the raw-data boundary, validates map/sheet/blocked-centre admission and returns
immutable aim data or an explicit rejection. Require known current anchor data
without treating target activity as centre eligibility. Keep preparation admission
and earlier blue validation separate. Preserve existing map-aim APIs and geometry/
formula owners; no command, clock, MP, RNG or live-state authority. Integration
with the ready-cast geometry bridge is a subsequent bounded milestone after the
aim primitive's vectors pass. An imported type7 must preserve the source's tile
classification; unsupported raw types may be rejected by an explicit managed
admission boundary rather than normalized into unit-follow behavior.

Owner drafts, AbilityToggles, JobStats, host/editor, automatic facing, full corpse/
Golem/terminal/status/frame RNG, broad interfaces and ZOD-106 remain deferred.
The future custom forward1/3/5 cone is unchanged and unimplemented. The runtime
continues to use managed code/prepared files, never C or the BIN.

### Validation and preservation

Eight focused Python cases and all **695 Python tests** pass. The pinned19-file/
26-function source audit also rechecks the previous58 geometry descriptions.
Algorithm-guide, coverage, local-link and preservation audits pass. The
[review receipt](../evidence/combat/spell-follow-review-v1.json) records baseline
and ignored report locations. All existing production/build/managed tests and
prior behavior fixtures, including nine owner drafts and JobStats, are unchanged.
The last **4,661 .NET tests** and locked Release build remain the ZOD-362 baseline;
managed tests/build were not rerun for this trace-only change. No original/C
execution or UI review was performed.
