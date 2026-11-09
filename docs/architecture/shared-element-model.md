# Shared boolean element model — ZOD-448

Owner-selected maintenance from `e42c6ee`. Read-only fft_decomp remains
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The existing
[element trace](../behavior/magic-elements-and-routing.md) and
[source receipt](../evidence/combat/magic-elements-source-v1.json) establish the
original flag mapping, overlap tests and arithmetic order. The
[maintenance receipt](../evidence/maintenance/shared-element-model-v1.json) records
the renewed fingerprint check, migration and validation. No C or original game
was executed.

## One shared definition

[`ElementToggles`](../../src/Zodiac.Framework/Battle/Combat/Elements/ElementToggles.cs)
now lives in `Zodiac.Framework`, replacing the runtime `MagicElement` enum and the
former Content-owned element type. Content already depends on Framework; the
dependency remains one-way. C# callers use the shared namespace, with no duplicate
legacy alias/type. Ability elements, current elemental strengthening, each target
affinity set, and weapon definitions all use the same immutable record.

```csharp
var elements = new ElementToggles { Fire = true, Ice = true };
var changed = elements with { Lightning = true };
```

The eight original properties and the owner's Gravity/Poison/Restore properties
remain. Neutral is computed from an empty set, rather than stored independently.
`Overlaps` tests shared enabled booleans; battle calculations do not pack or test
an element byte. Adding further elements requires explicit properties and rules,
but does not exhaust an eight-bit runtime field.

`MagicElementAffinities` retains four independent sets. Default struct fields
normalize to empty sets when read, and equality/hash code compare the normalized
values. Thus `default` and explicitly empty affinities remain equivalent. Omitted
current-unit strengthening likewise becomes the immutable empty set. Editing a
copy cannot mutate those shared defaults.

## Compatibility boundaries

[`OriginalElementEncoding`](../../src/Zodiac.Framework/Compatibility/OriginalElementEncoding.cs)
owns the original byte/name mapping for import/export boundaries. Original bits
still mean Dark1, Holy2, Water4, Earth8, Wind16, Ice32, Lightning64 and Fire128.
Both existing importers use it; original input leaves custom properties false.
The running battle code consumes boolean sets, not that encoding.

The prepared magic format still uses original name strings such as `"Fire"` or
`"Dark, Ice"`, with the previous canonical ordering, case/combination reading and
neutral name. A scoped Content JSON converter translates those strings to/from
the shared model. The existing schema versions, asset/profile IDs and flat unit
data do not change. The neutral-only sword schemas still write `"element": 0`;
this does not activate elemental weapons or expand sword import coverage.

Byte/name adapters reject custom flags instead of truncating them. The original
damage-element resolver, current-input projection and encounter definitions also
reject newly represented custom elements. These properties remain available as
data for future explicitly selected custom rules; their mechanics are not
implemented or assigned original encodings by this refactor.

## Preserved rules and validation

An element match remains any overlap, not damage divided between enabled elements.
Storm still applies Fire then Lightning; snow checks Ice. Each arithmetic store
retains its original signed16 behavior. Absorption is marked first; nullification
clears it and exits; resistance and weakness then apply independently. Strengthen
overlap, Oil/Fire and Float/Earth admission, Faith order, healing's elemental
bypass, evasion, procs and RNG ordering retain their existing owners and behavior.

Release build: zero warnings/errors. **5,354 .NET tests** and **810 Python tests**
pass. Eight added tests cover every original byte combination, all65,536 pairwise
overlap outcomes, immutable/normalized defaults, each custom flag, all256 prepared
name encodings against the old enum serializer, JSON round trips and malformed/
unsupported element input. Existing literal amount/routing/AI/importer tests keep
their expected values; only their input representation changes. The former two
enum-valued test arguments are now equivalent element-name strings.

Historical evidence/fixtures retain their old paths and hashes. Follow the new
receipt's mapping for moved/retired types. Owner drafts, JobStats, controller
layout and disabled CI remain preserved. The full content/import epic, custom
elements, elemental weapon delivery, new formulas and full AI review remain open.
