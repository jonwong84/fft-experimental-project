# Status action application — supplied source cross-check

This is supporting evidence for ZOD-436, not a general managed status-system
implementation. The owner's Apply status (to action) listing matches all 217
instruction words at `0x80187f24`–`0x80188284` in the verified prepared BATTLE.BIN.
All 17 listed direct callers match, as does formula `0x38` in the one-based
dispatch table at `0x8018f614`, returning through `0x8018b97c`.
The [supporting receipt](../evidence/combat/formula08-supplied-trace-v1.json)
pins clean read-only decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`, source
functions, supplied-text hash and prepared bytes. No C/original execution is
claimed by this cross-check.

## Branch order and work ownership

The routine reads the ability-work status type and five status bytes. Separate
first divides the action's accuracy by four. Then, when either Random or
Separate is set and action state is nonzero, the **local type register** becomes
All or Nothing. This does not store a replacement type in ability work. The
accuracy division happens before that conversion, including preview and AI.

The mutually selected branches have this priority, even for combined flags:

1. All or Nothing (`0x80`): OR all five bytes into existing action inflictions.
2. Random (`0x40`): enumerate the 40 source status IDs in MSB-first order, draw
   once through `formula_get_random`, then select `raw * count / 32768`.
3. Separate (`0x20`): visit configured statuses in ascending ID order and call
   Pass/Fail with `(100, 24)` for each. Failed bits are removed from **ability
   work itself**. OR the remaining bytes into action inflictions afterward.
4. Cancel (`0x10`): OR the ability bytes into action removals.

These merges preserve previously staged action bits. A Separate failure does
not remove the same bit if it was already present in action inflictions.
Random has no empty-list guard before reading its stack list. An execution
record with Random but no statuses therefore has no established deterministic
result here; do not invent a no-op, miss or clamped choice for that input.

The confirmation stage checks all accumulated infliction/removal bytes. When
nonempty, it calls the validator with argument 0. Empty work, or validator return
0, forces an attack miss. Otherwise attack type is ORed with `0x08`. If Invite
remains in inflictions (second byte, bit `0x40`), effects are ORed with `0x0040`.
The routine stages action changes; live-unit status settlement is separate.
Direct formula `0x38` invocation does not inherit the preserving wrapper's
restore step. An empty result can therefore remain a miss for that caller.

## Direct dependencies

- `0x8018eea0`, `formula_get_random`: execution calls `rand`; nonexecution returns
  midpoint `0x4000`. Its source/ordering contract is already recorded in the
  [RNG study](rng-state-and-ordering.md). The local mode conversion prevents the
  Random branch from using this shortcut on ordinary preview/AI entry.
- `0x8005e0cc`, `random_roll_pass_fail`: the already checked shared helper.
  Separate keeps scaled results below 24, not 25. Raw 7864 scales to 23 and
  succeeds; 7865 scales to 24 and fails. Displayed quarter accuracy is separate
  from that literal threshold.
- `0x80184b24`, `status_modify_inflictions(0)`: validates the accumulated action
  sets. Its immunity, innate, current-status, mount and table rules need their
  own detailed documentation cross-check. This is the next useful supplied page,
  **Validate Status Changes**.
- `0x8018430c`, `formula_force_attack_miss`: clears hit and 16-bit accuracy and
  assigns miss type 7. Its source is already pinned in the
  [preserving-wrapper check](magic-conditional-proc.md#supplied-preserving-status-wrapper).

## Documentation corrections and signed accuracy

The summary's Invitation effect `0x10` disagrees with the actual OR `0x0040`.
The status-work upper address is `0x8019390b`, not `0x9019390b`. Separate's notes
say 25%, but the instruction threshold is 24. The negative accuracy correction
adds 3 to the loaded value before shifting; it does not force a positive minimum.

The actual accuracy load at `0x80187f58` is signed LH. Its division truncates
toward zero, then stores the low 16 bits. All 65,536 input patterns were checked
offline. The pinned decomp declares the action field as `s16` and agrees with
that division. For example, input bits `0x8000` produce `0xe000`, whereas an
incorrect unsigned division would produce `0x2000`. The preserving wrapper's
unsigned saved copy retains these bits; it does not change the inner arithmetic
to unsigned. Keep that distinction when implementing Separate later.

## Current managed boundary

Prepared Fire tiers use status record 0, whose six bytes are zero. Admitted
action status sets also start empty. This selects no status branch, performs no
status RNG/filter call, and reaches forced miss. The preserving wrapper restores
hit, miss type and accuracy; HP work and the earlier proc marker survive.
`FaithMagicFormula.ResolveProjectedAfterHit` already models this case, and
`MagicResultFinalization` requires `EmptyStatusRecord`. Separate's signed
accuracy calculation is not reached on that admitted path.

All 29 focused .NET proc/composition/finalization tests and 16 Python tests pass.
The existing source audits verify the proc/finalization descriptions and prepared
Fire tier/empty-record bindings. All ten owner drafts, the approved controller
edit and the disabled CI gate remain unchanged.

No runtime, fixture, owner definition or general status admission changes are
required here. Full formula `0x38`, nonempty sets, immunity/filter semantics and
AI status usefulness remain separate work. ZOD-436 stays open for its broader
shared-consumer review.
