# Stored magic elements, composition and routing — ZOD-337–339

## Supplied Elemental XA * YA wrapper — ZOD-436 support

The owner's listing matches all18 instruction words at `0x8018877c`–`0x801887c0`
and all four listed return locations. The
[formula0x08 supporting receipt](../evidence/combat/formula08-supplied-trace-v1.json)
pins prepared BATTLE.BIN, clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`, helper functions and managed inputs.
Its three ordered dependencies are:

1. `0x80186568`, `formula_store_xa_times_ya_damage`: store the signed16 product
   XA*YA as target HP damage and set the HP-damage type. Managed arithmetic owner:
   `FaithMagicArithmetic.MultiplyStored`, composed by `FaithMagicAmountFormula`.
2. `0x80186ed0`, `formula_element_apply_weather`: modify the **stored amount**
   using effective weather and ability elements. Managed owner:
   `MagicDamageElements.Transform`'s weather stage.
3. `0x80186ff8`, `formula_element_apply`: Oil/Fire, Float/Earth with mount/rider
   handling, then target affinities. The admitted affinity stage is also owned
   by `MagicDamageElements.Transform`; Oil/Float/rider contexts remain explicitly
   rejected rather than silently omitted.

The wrapper then loads the target action hit byte and returns `hitByte < 1`:
1 for a cleared flag,0 for a nonzero flag. It does not test damage, displayed
accuracy or absorption. A legitimate zero stored amount is not nullification.
The source's pre-Faith product store and weather/affinity stores remain separate;
elemental strengthening changes XA earlier, outside this wrapper.

Affinity work marks absorption but does not yet move damage into the healing
field. In formula0x08 the wrapper must return0 before Faith runs; the later
absorption/proc helper then performs conversion. The managed projection likewise
skips Faith output on nullification and lets `FaithMagicHpRouting` convert a
surviving absorbed amount after Faith. This wrapper has no direct RNG call.
Verifying its other callers does not admit those formula families or status
side effects into managed execution.

The existing source audit passes24 transform,12 projection and14 routing
descriptions. All16 focused .NET element/projection/routing tests and eight Python
tests pass. No runtime or fixture change is needed; all ten owner draft blobs are
unchanged and the unrelated `.circleci/config.yml` owner edit is excluded.
ZOD-436 remains open.

### Supplied XA * YA product helper

The owner's listing matches all13 words at `0x80186568`–`0x80186598` and all nine
listed returns. The same supporting receipt records its operand loads and two
target-action writes. It zero-extends XA/YA with `lhu`, multiplies, assigns the
action-type byte at offset`0x25` to `0x80` (HP damage), then stores the product's
low16 bits at HP-damage offset`0x04`. The type write is an assignment, not an OR.
The `sh` store is in `jr r31`'s delay slot and completes before the caller resumes.
No rounding, cap, Faith, weather, element processing, RNG or live-HP mutation is
performed by this helper.

Unsigned operand loads agree with the existing signed-short C# calculation:
each signed/unsigned interpretation differs by a multiple of65,536, so the
products have identical low16 bits for every pair. An offline check also covers
all65,536 raw XA values against ten selected boundary YA values (655,360 pairs),
not an exhaustive enumeration of every pair. For example,255*255 stores signed
damage-511, and raw XA`0xffff` times2 stores-2. The result is subsequently read
as signed16 by the weather/Faith stages. `FaithMagicArithmetic.MultiplyStored`
already preserves this behavior with its unchecked short cast.

Later XA/YA changes cannot retroactively update the stored damage. This explains
the original Oil/Fire ordering quirk; it does not justify a general assumption
that later explicit reads or recomputation are pointless. A future admitted Oil
implementation must preserve the original ordering. Oil/Fire remains excluded
from the current managed profile; no Oil behavior is activated or repaired here.
All ten focused .NET arithmetic/element-pipeline tests and16 Python tests pass;
all ten owner draft blobs remain unchanged. No runtime or fixture change is
needed.

### Supplied Weather Elemental effects

The owner's listing matches all64 instruction words at `0x80186ed0`–`0x80186fcc`,
all four listed returns and its one dependency call. The same supporting receipt
pins the source, prepared bytes, effective-weather dependency and managed inputs.
`weather_get_effective` at `0x8018e660` supplies the weather value; the damage
routine then reads the already-stored signed16 target HP-damage field:

- Weather3/4 (storm/strong storm): Fire bit`0x80` applies *3/4, then Lightning
  bit`0x40` applies *5/4. Both can apply, with a signed16 store between them.
- Weather6/7 (snowstorm/strong snowstorm): Ice bit`0x20` applies *5/4.
- Weather0/1/2/5 does not alter damage; ordinary rain and snow grant no boost.
  Other element bits do not affect these checks.

Each negative numerator adds3 before arithmetic shift2, implementing division
by4 toward zero. The branches annotated "never?" are meaningful for negative
stored amounts and cannot be removed. The `addiu` adds3 to the numerator; it
does not replace damage with3. An offline comparison across all65,536 signed
amounts, eight admitted effective-weather values and eight relevant element
combinations passes4,194,304 cases, including narrowing after every active step.
This is integer arithmetic evidence, not original/C execution or broader battle
admission. A combined Fire/Lightning amount5 in a storm becomes3 rather than4
from a fused *15/16 operation. `MagicDamageElements.Transform` already agrees.

This changes stored damage only, without changing XA/YA, hit/type flags,
affinities, live HP or RNG. The pinned source dependency reads script weather,
returns0 when the map ignores weather, and adds the snow offset3 to weather>=2
when the snow-map flag is set. The pure transform accepts **already-effective**
weather; map/script weather selection is separate and must not be applied twice.
All16 focused .NET element/projection/routing tests and eight Python tests pass;
all ten owner draft blobs and the unrelated CI edit are unchanged. No runtime or
fixture change is needed. ZOD-436 remains open.

### Supplied effective-weather dependency

The owner's `BATTLE_Get_Weather` listing matches all22 words at
`0x8018e660`–`0x8018e6b4`, its script-variable call and both listed returns
(`0x80174380` from weather movement cost, `0x80186eec` from damage weather).
The same supporting receipt records three branch/delay-slot checks and source
fingerprints for the getter and its bounded script-variable read.

It first calls `script_variable_get(0x23)` at `0x8013b590`; the argument is set
in the call delay slot. This read occurs even when weather is subsequently
ignored. Then it loads the flag word at `0x800b6698` and applies these rules:

1. Ignore-weather bit`0x02` returns0, including when Snow is also set.
2. Otherwise a **signed** weather value below2 returns unchanged.
3. Otherwise Snow bit`0x01` adds3; a clear bit leaves the value unchanged.

This preserves clear/none values even on a snow map. Rain2 becomes Snow5,
Storm3 becomes Snowstorm6, and StrongStorm4 becomes StrongSnowstorm7. The notes'
label "Snowstorm" for value5 is a typo: pinned source and the existing
`MagicDamageWeather.Snow` agree that it is ordinary Snow, which does not boost
ice damage. The supplied inline branch comments need their zero/nonzero polarity
and delay-slot assignments to be read together.

The receipt checks20 ordinary raw-weather/flag combinations and16 source-boundary
descriptions. The source adds no upper clamp; already-converted or out-of-range
script values must not be silently normalized. Boundary values and signed32
`addiu` wrap are source descriptions only, not new managed weather admission.
The current pure transform accepts validated **already-effective** weather0–7;
there is no new raw map/script weather classifier in this milestone.

The generic getter reads through script variable commands and restores comparison
variable0. For this fixed0x23 request it does not take the special random-variable
0x21 path. This dependency is traced sufficiently for the current read boundary;
a general event-VM port or additional recursive documentation is not required
here. All16 focused .NET element/projection/routing tests and eight Python tests
pass; all ten owner draft blobs and the unrelated CI edit are unchanged. No
runtime or fixture change is needed. ZOD-436 remains open.

### Supplied Ability Elemental? routine

The owner's listing matches all65 instruction words at `0x80186ff8`–`0x801870f8`,
its six conditional branches and all four listed returns. The same supporting
receipt pins the three callees and exact call/delay-slot instructions at the
unchanged source/prepared baseline. The ordered dependencies are:

1. Modify Status Inflictions, `status_modify_inflictions(0)` at `0x80184b24`,
   conditionally for Oil/Fire after staging the Oil removal bit.
2. Elemental Nullification, `formula_nullify_action` at `0x80184e40`,
   conditionally for Earth against the relevant unit's Float status.
3. Elemental Damage Modification, `formula_element_modify_damage` at
   `0x80184e98`, always called last with the saved ability-element byte.

Oil/Fire checks target byte`0x5a` bit`0x80` and Fire bit`0x80`. It doubles
signed16 XA, not the already-stored HP/MP damage. It stages Oil removal in target
action byte`0x22` before the status-filter call (the store is in the call delay
slot). The argument0 requests the full filter. That filter considers all pending
status work, removes innate/ineligible removals and returns whether any changes
remain; it is not merely an Oil-specific test. A nonzero result **assigns** the
action-type byte to`0x08`; it does not OR that bit into its prior value. Actual
unit status removal is later. The suggested Oil damage fix is not adopted.

The routine next chooses the unit whose Float status matters. A rider
(`mount_info` bit`0x80`) uses its ridden partner (`mount_info &0x1f`, original
unit-record stride`0x1c0`); other targets use themselves. Float is status
byte`0x5a` bit`0x40`; Earth is element bit`0x08`. When both apply, the routine
calls the nullifier and then writes miss type7 (`ForcedFailure`). It **continues**
to common affinity processing. Thus a later affinity-nullification call can
replace label7 with the nullifier's label5; an early return would change behavior.

The affinity stage marks absorb, checks cancellation, then applies half and weak.
Absorbed damage is converted to healing by the later helper, not this stage.
The current `MagicDamageElements.Transform` implements only the admitted fresh
affinity path. Relevant Fire/Oil and Earth/Float-or-rider work remains positively
excluded; this study adds no live status filtering, mount lookup or Float routing.
Existing admission tests must continue rejecting uncertified relevant contexts.
All16 focused .NET element/projection/routing tests and eight Python tests pass;
all ten owner draft blobs and the unrelated CI edit remain unchanged. No runtime
or fixture change is required.

The next useful supplied page is Elemental Damage Modification, then Elemental
Nullification. Modify Status Inflictions is captured as a dependency; its broader
status implementation belongs to separate work before admitting Oil/status
side effects. ZOD-436 remains open.

## Original implementation checkpoint

Implemented 2026-11-05 from managed base
`be973305b348b0b1b41c418b2be4082860e8bf08`, clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Source receipt](../evidence/combat/magic-elements-source-v1.json): nine files and
14 functions. [50 independent descriptions](../fixtures/behavior/magic-elements-contract-v1.json)
extend the earlier [Faith trace](../architecture/zod-334-faith-magic.md).
No original/C execution or runtime reference-code dependency.

## ZOD-337: stored weather and affinity transform

`MagicDamageElements.Transform` accepts signed16 stored damage, a named element
set, already-effective weather and explicit absorb/nullify/half/weak sets. Known
element combinations preserve the source's eight bits; these are semantic sets,
not copied unresolved ability-toggle flags. Weather values0–7 match the pinned
source; unsupported values reject rather than silently behaving as clear weather.

`formula_element_apply_weather` modifies the stored signed amount. Storm and
strong storm apply fire *3/4, then lightning *5/4. Snowstorms strengthen ice *5/4;
ordinary rain/snow do not change it. Every step divides toward zero and stores
signed16 separately. Combined fire/lightning with amount5 therefore produces3,
rather than the4 produced by a combined15/16 multiplier. Overflow and negative
amounts retain the source stores. The caller supplies effective weather:
`weather_get_effective` handles ignored-weather maps and snow-map offsets in the
reference; map/script weather selection is not activated by this helper.

`formula_element_modify_damage` marks absorption, checks nullification, then
applies half and weak in order. Nullification clears damage and special effects,
including an absorption mark set immediately before it, and skips half/weak.
The source also clears hit/accuracy and drops pending proc/knockback work. This
raw helper marks cancellation; publishing those lifecycle effects belongs to
later action integration, rather than treating zero damage as a complete miss.
Half+weak is not an identity on odd values. Multiple matching element bits still
apply each affinity step once. Zero amount retains the applicable weak/absorb
marker. Output preserves the weather intermediate for diagnostics.

The supported slice requires explicitly fresh element work. Fire also requires
known no Oil; earth requires known no Float or rider. False profile facts mean
uncertified, not neutral. Irrelevant status facts are not required for other
elements. Oil/Float/rider cases remain rejected: Oil changes XA only after damage
was already stored and can stage status removal, so an assumed fire-damage double
would be wrong. Float's earth nullification consults a rider's mount before target
affinities. No status/mount behavior is invented here. Null contexts/profiles reject.

## ZOD-338: element-aware amount composition

`FaithMagicAmountFormula.ProjectWithElementsAfterHit` shares the original modifier
and product owner with the legacy `ProjectAfterHit` API. For damage, it transforms
the stored product, then applies Faith to the transformed value. Nullification
returns no Faith amount, distinct from a legitimate zero amount. Original XA and
product plus the element intermediate remain available as diagnostics. Known
empty elements cannot contradict an asserted elemental strengthening step.

Healing bypasses the entire element context, including missing or unsupported
weather/status context, matching0x0C's source chain. The old API, signatures and
strict neutral-damage certificate remain unchanged. All22 previous amount vectors
also pass through a known unchanged element step with identical results; all44
prior source descriptions remain valid. Shared Python descriptions likewise reuse
one pre-Faith product owner instead of duplicating modifier arithmetic.

`FaithMagicElementAmount` has an internal constructor and immutable diagnostics;
only the owning pipeline creates its valid canceled/non-canceled shape. It is raw
formula work and confers no Framework command, receipt or RNG authority.

## ZOD-339: raw HP routing

`FaithMagicHpRouting.Route` maps the post-Faith raw amount into signed damage or
healing fields. Damage with elemental absorption becomes healing even against an
undead target; `formula_element_apply_absorption` does not invoke undead reversal.
Healing formula0x0C instead routes to damage against undead and requests clearing
the **entire first canceled-status byte**, not just Dead. The result exposes this
as a named deferred operation and mutates no status set. Living-target healing
does not request that operation. Nullified damage has neither HP field populated.

Zero values preserve their route, and negative signed values preserve their bits
through the source's unsigned temporary/signed field copies. No absolute value,
lower clamp or upper999 cap is introduced. Final eligibility, caps, procs and live
HP publication remain later stages. The original formula family is retained on
the projection: an HP healing route may originate from damage absorption, so it
cannot determine the original family's proc behavior.

## Validation and next work

24 transform,12 composed and14 routing descriptions pass independent Python and
managed checks, including rounding, wrap, cancellation and undead differences.
Sixteen new Framework cases add relevant/irrelevant scope guards, null inputs,
healing context bypass and legacy equivalence. Eight new Python cases check
independent checkpoints, order, tampering, numeric boundaries and source identity.
Full validation: locked restore, zero-warning/error Release build, **4,589 .NET
tests** (1,548 Framework) and **619 Python tests**. Pinned-source, prior prepared
eight-tier/Faith, algorithm-guide, coverage and local-link audits pass. Nine owner
drafts and all prior fixtures/test expectations remain unchanged.
[Implementation receipt](../evidence/combat/magic-elements-implementation-v1.json);
ignored reports: `scratch/magic-elements-337-339/`.

[ZOD-340](magic-conditional-proc.md) now implements the isolated conditional-proc
decision; [ZOD-341](magic-result-finalization.md) now supplies bounded living-target/
empty-status finalization. [Magic evasion and fresh formula entry](magic-evasion.md) are now implemented;
[Current-input and ready-cast queries](magic-current-inputs-and-casts.md) now
compose these owners; ZOD-358 next traces original targeting geometry.
The original next-step boundary was: trace and implement the proc decision with
explicit execution/preview/AI modes and exact RNG ownership. Keep the original
formula family and cancellation context; absorption does not turn0x08 into0x0C.
No full status application, result eligibility/caps, MP/charge, live MA/Faith,
spell targeting, actual command or magic AI integration is enabled by this batch.
Owner toggles, JobStats, host/editor, automatic facing, corpse/terminal/frame RNG,
broad interfaces and ZOD-106 remain deferred. Content authoring/export contracts,
package schemas, project references and dependencies are unchanged.
