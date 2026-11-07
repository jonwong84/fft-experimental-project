# Supplied turn upkeep and RNG — ZOD-430 supporting trace

The owner's Death sentence, crystallization, treasurization, Reraise, Defend
removal and Chicken Brave routine is `upkeep_action_build` at0x80183078.
Reference `11464943bbdbd2a0af022d90cb7f10455cf911df` was rechecked clean/read-only.
The [receipt](../evidence/ai/turn-upkeep-trace-v1.json) pins nine source/metadata
files and four functions. All149 supplied instruction words, its three direct
call sites and the listed return0x80182B18 match the hash-verified prepared
BATTLE.BIN used by the [clock review](between-turn-clock-review.md).

This is pending-result preparation, not complete revival/death application.
The documented unit ID in r4 is unused by this body; r5 supplies the unit
pointer. The caller selects CT>=100 and reduces CT before entering upkeep.
The helper itself does not enforce a CT>100 precondition.

## Order and return values

First reset selected action-result fields through `action_clear_current_data`,
then call `rand()` unconditionally. Even a healthy unit with no upkeep effect,
an immortal corpse or a plain simulated corpse consumes this first draw.
The clear helper establishes hit1/accuracy100 and clears effect/status work;
it does not zero every byte (reaction ID and last-received-attack are retained).

The source then follows these branches in order:

1. **Dead, non-Undead, with Reraise:** stage removal of Dead/Reraise and healing
   floor(maxHP/10), returning2. This precedes the execution-mode and immortal/
   formation guards, so it also stages in AI/preview. There is no second draw
   or minimum-one healing rule: maxHP1..9 stages0 healing.
2. **Dead during execution:** immortal and save/load-formation units return0.
   Otherwise decrement the death counter as a byte. Old1 becomes0 and returns0;
   expiration happens when the incoming counter is already0 and the local
   decrement wraps to255. That255 is not written back on expiration.
3. **Expired Undead:** an odd first raw roll stages revival, then a second
   `rand()` supplies `floor(raw * maxHP / 32768) + 1` healing; return2. An even
   first roll falls through to crystal/treasure. Undead does not use the
   ordinary Reraise-status branch above.
4. **Crystal/treasure:** use the first roll again. Nonzero `roll & 0x10`
   selects Crystal; the unit's initial player-controlled flag changes the mask
   to0x110. Otherwise select Treasure. Return1 stages the status change;
   it does not award inventory or itself remove the unit.
5. **Remaining cases:** decrement Death Sentence as a byte. Incoming1 expires;
   incoming0 wraps to255. Expiry stages Death Sentence removal and, for a
   non-Undead unit, Dead with return3. Undead proceeds to the following effects.
   Stage Defending removal when present. Stage Brave+1 whenever Brave<10,
   without testing the Chicken status bit. Return2 if the resulting attack
   type is nonzero, otherwise0.

The wiki's "Not Dead" heading does not describe every path into the last block:
dead AI/preview units skip the execution-only death-counter section and can
reach Death Sentence/Defending/low-Brave work. Counter updates can occur even
when the function returns0. Other status/HP effects remain staged until the
caller processes the action result. Return2 is not proof that revival occurred.

## Arithmetic and original quirks

The crystal masks select exactly16,384/32,768 and24,576/32,768 possible raw
outputs respectively (50% and75%). The controlling bit is in **initial** team
flags, not a test of whether the current caller is AI. Simulation never enters
this expiration branch merely because its upkeep draw has those bits set.

Reraise uses integer division by10. Undead recovery uses a15-bit raw value
including0, not a generic inclusive random-range API. For maxHP<=32768 the
upper result can equal maxHP; larger raw field values need not reach it.
For example, maxHP65535/raw32767 calculates65534. Its signed16 healing field
then stores-2. Raw16384 with the same maxHP calculates32768 and stores-32768.
The product itself remains within signed32 for the unsigned16 maxHP/15-bit raw
domain. These are raw-width source boundaries, not claims of normal retail
reachability or newly admitted managed revival inputs. A future port must
preserve or explicitly exclude them rather than silently widen the result field.

## RNG dependency and evidence

Both call sites target FFT's `rand` veneer at0x8002230C. The prepared SCUS
executable confirms its jump to BIOS table A0 with selector0x2F. The decompilation
contains the veneer, not the BIOS generator body. The existing
[BIOS verification](bios-rng-verification.md) and unchanged
[numeric vectors](../fixtures/rng/scph5501-rng-vectors.json) already establish
the selected US v3.0 implementation:

`state = (state * 0x41C64E6D + 0x3039) mod 2^32`

`raw = (state >> 16) & 0x7FFF`

Current `RandomStateGenerator` preserves that full32-bit state and updated-state
output. All16 existing managed RNG tests pass against the numeric observations;
the original BIOS was not executed again for this review. Upkeep uses the raw
BIOS stream even in AI, rather than the formula helper's non-execution midpoint.
The second draw is conditional only on successful expired-Undead revival.
No status-dependent early exit skips the first draw.

The linked **Random Number Generator** wiki page would be useful to cross-check
this call boundary, seeding and range description. Game-wide draw placement and
VSync/frame interleaving remain a separate unresolved compatibility boundary;
generator arithmetic alone does not settle them. No new BIOS/C dependency is
introduced into the application.

## Managed boundary

The existing ordinary clocks preserve the healthy/plain-corpse draw and modulo
behavior under their explicit status-free/effect-free certificates. General
Reraise, Undead, death counters, Defending, Death Sentence and low-Brave upkeep
remain outside those profiles. This source confirmation identifies no current
admitted-domain runtime defect and does not complete ZOD-430. No runtime code,
historical receipt or fixture changed; the owner's local turn-controller edit
is preserved. Prepared originals remain ignored under scratch.
