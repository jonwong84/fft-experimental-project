# Stored magic elements, composition and routing — ZOD-337–339

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
