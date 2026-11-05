# Readable ability AI authoring — ZOD-330

Implemented from `66821b3` at the owner's request, following the
[static flag study](../architecture/zod-326-ai-behavior-metadata.md). Reference
remains clean/read-only at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/ai/ai-behavior-source-v1.json) pins five files/six
functions; the existing37 independent bit/retail descriptions remain equal.
No original/C program was executed.

## Author semantic definitions

`AbilityMetadataDefinition` now stores `AiBehavior`, `UsableByAi` and
`AiEvadeWithMotion` as authoritative immutable semantic values. Its preferred
constructor accepts named traits rather than a packed byte array:

```csharp
var stone = new AbilityMetadataDefinition(
    new("fft:ability/throw-stone"),
    jpCost: 90, learnOnHitRateRaw: 80, serializedClassRaw: 1,
    aiBehavior: new AiAbilityBehavior(
        TargetsEnemies: true, AffectsHp: true, Evadeable: true,
        PhysicalAttack: true, DirectTrajectory: true),
    usableByAi: true, aiEvadeWithMotion: true, showName: true);
```

The explicit AI-usable argument avoids granting eligibility through an implicit
default. Working usage/consume policy and actual combat effects remain separate.
No setters, new effect implementation or broader accepted AI policy combination.

The four original area-shape bits cannot be replaced by one boolean without
losing information. `AiAbilityAreaShape` names TripleBracelet, TripleAttack, Cone
and Linear, preserving combinations. `HasExhaustiveAreaShape` remains a derived
query property. Original odd combinations survive rather than being normalized;
an exposed shape does not imply implemented geometry. Invalid semantic enum
values reject. The original static fourth-byte residual is preserved in
`UnresolvedAiBitsRaw`; known usable/motion bits must be supplied as booleans,
so overlapping residual bits reject instead of creating conflicting sources.

## Compatibility boundary

`OriginalAbilityAiFlags` owns decoding, encoding and residual-bit validation.
`FromOriginalAiFlags` explicitly marks original import/legacy-package input.
The existing raw-byte constructor remains a compatibility entry point, while
`AiFlagsRaw` is now an immutable generated export view. It no longer supplies
the definition's primary state. Original flags are reconstructed exactly,
including all area bits and unresolved static bits.

The version1 package reader uses the named original-data factory, and its writer
keeps the existing raw wire field. Existing files, deterministic JSON exports,
import provenance, companion checks and package schemas remain unchanged.
This refactor improves **C# definition authoring**; it does not silently introduce
a new JSON format. A future readable JSON format requires its own versioned
contract. UnknownTypeBit4 and original numeric classification remain preserved.

The offline metadata importer now uses the explicit boundary factory. Editor
copy/remap code copies semantic fields and residual provenance directly rather
than unpacking/repacking application logic. Its existing roundtrip preserves
all original data; no editor controls or UI behavior are changed. Content-backed
AI test fixtures now author their default self/Stone definitions with booleans.

## Verification

Nine new Content cases cover named Stone equivalence, immutable edited copies,
six literal area-shape/composite encodings, all1,024 individual-byte patterns
against nonzero surrounding bytes through semantic construction and legacy JSON,
and invalid/ambiguous semantic input. The37 source bit vectors still verify each
independent trait; existing import/companion/editor-copy/full AI turn/replay
suites remain equal. Decoder and encoder agreement alone is not the evidence:
literal original Stone/shape encodings and pinned source vectors check meanings.

Locked restore, zero-warning/error Release build, **4,530 .NET cases** (1,521
Framework, 1,217 AI, 832 Content), **596 Python tests**, pinned source/guide,
coverage/links and nine unchanged owner drafts pass. Logs are ignored under
`scratch/ai-authoring-330/`; [implementation receipt](../evidence/ai/readable-ai-authoring-v1.json).

ZOD-329 remains next for prepared Dash content/effect/grant binding. Owner draft
consolidation, JobStats changes, general edited-policy support, host/editor,
automatic facing, full corpse/terminal/frame RNG, broad interfaces and ZOD-106
remain separate/deferred.
