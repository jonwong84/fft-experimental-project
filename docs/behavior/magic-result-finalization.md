# Bounded magic result finalization — ZOD-341

Implemented 2026-11-05 from managed base
`b78bbb6399901431d25c721ac66b03279ee9b2f0`, clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/combat/magic-finalization-source-v1.json): eight
files/15 functions. [Twenty raw and20 composed descriptions](../fixtures/behavior/magic-finalization-contract-v1.json)
record literal results independently of the managed implementation. No original
or C code was executed.

## Source order and supported domain

`action_calculate_target_result` runs the selected formula, calls
`action_finalize_target_result`, then ORs `ACTION_TYPE_PSEUDO_STATUS` when any
special-effect bit remains. That final OR does not test hit or accuracy.

The selected slice requires an explicitly living target without Petrify or Wall,
no Golem interception, ordinary primary context, a known empty status-infliction
record, no pending weapon proc/reaction or knockback, and only the represented HP
fields/types/effects. Other work must not be silently discarded. Unsupported
context returns rejection without an outcome. These positive certificates are
managed admission policy, not additional original-game conditions.

Within that domain the finalizer:

- Clears hit when unsigned16 accuracy is zero, preserving accuracy and the
  original miss label. It never restores an already-false hit.
- Caps each signed16 HP field independently only when greater than999. Negative
  and zero values retain their bits, field and type. There is no lower clamp or
  inference of damage/healing direction from sign.
- Preserves absorption, weakness and added-effect markers; any of these adds the
  pseudo-status type, even on a miss. An existing pseudo-status type remains set.
- Retains the deferred canceled-status-byte0 clear from undead healing. It does
  not clear a live status byte.

The source also checks lethal damage against current HP. Its two calls cancel
pending knockback and clear a pending weapon proc. Both are no-ops under this
slice's explicit no-pending facts, so the pure API needs no fabricated current HP
input. In particular, `formula_clear_pending_proc` removes the added-effect bit
only when weapon-proc-pending is nonzero. A successful magic proc against empty
record0 therefore retains that bit even if its damage is lethal. Dead/Petrify/
Wall cancellation and Golem branches remain outside the admitted domain.

## Pure API and composition

`MagicResultFinalization.Evaluate` accepts immutable `MagicResultFrame` data and
the positive profile. Named boolean type/effect records avoid exposing packed
source bytes. Known miss labels0–13 can be copied as data; this does not implement
reflection, Catch, evasion, or any other behavior associated with those labels.
Undefined labels and null required inputs are API errors.

`EvaluateAfterHit` accepts an opaque element-aware Faith projection, explicit
Undead/accuracy/mode facts, profile and immutable RNG. It privately composes raw
HP routing, the existing conditional-proc decision, empty-status preservation,
caps and special-effect typing. Absorbed damage keeps the damage formula's proc
tail; reversed healing keeps healing's no-proc path. Zero accuracy is handled
after the tail, so it does not optimize away an execution draw. Element
nullification supplies its canceled frame with zero accuracy, nullified miss
label and cleared effects.

The prepared executable's verified status record0 is six zero bytes; four Fire
tiers bind it. `formula_apply_status` saves/restores hit, accuracy and miss around
the status attempt. The empty record changes no status set or HP value. A
successful execution roll still sets added-effect; preview requests the empty
status attempt without setting that flag. Consequently preview does not acquire
a pseudo-status type merely because the attempt was requested.

Complete flag output needs a known-clear added-effect flag at entry, including
preview and healing composition. The standalone proc query legitimately allows
preview without that fact because it only returns a set delta. The composed
query must know the entire final flag value. Nullification already proves the
flags clear, but still requires the full target/work profile. The raw evaluator
instead receives the complete post-proc flags explicitly, so it does not require
the entry clear-flag certificate.

Admission occurs before a private draw. Unsupported context or an exhausted
UInt64 draw counter returns the incoming RNG with no partial frame or proc
outcome. Counter exhaustion is managed policy, not original behavior. Accepted
results return diagnostics and a candidate RNG continuation; no global stream,
HP, status, command or live state is published.

## Validation and next work

Fourteen new Framework cases cover40 literal/composed descriptions, every scope
certificate, preserved miss labels, preview knowledge and invalid inputs. Eight
Python tests independently check descriptions, signed caps, flags, ordering,
admission and tampering. Locked restore and the Release build pass with zero
warnings/errors. All **4,613 .NET tests** (1,572 Framework) and **635 Python tests**
pass. Source/prepared, prior Faith/element/proc, guide, coverage, local-link and
owner-draft audits pass. [Implementation receipt](../evidence/combat/magic-finalization-implementation-v1.json);
ignored logs: `scratch/magic-finalization-341/`.

This completes bounded result data. MP/charge, live MA/Faith, full status/corpse/
Golem/terminal behavior, targeting, spell commands and magic AI activation still
need separate work. **Next ZOD-342:** trace magical evasion and pre-formula
admission for the shared0x08/0x0C families, then select an independently checkable
implementation child. Owner drafts, JobStats, host/editor, automatic facing,
frame RNG, broad interfaces and ZOD-106 remain deferred.
