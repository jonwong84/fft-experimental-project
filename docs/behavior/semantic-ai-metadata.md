# Metadata-backed semantic AI traits — ZOD-327/328

Implemented from `891f4c0`, following
[ZOD-326](../architecture/zod-326-ai-behavior-metadata.md), reference clean/read-only
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

[ZOD-330](readable-ai-authoring.md) now makes `AbilityMetadataDefinition.AiBehavior`
primary for authored definitions. Original input derives those immutable AI
preferences through the compatibility adapter. The Content decoder names verified target, effect,
Silence/Faith/evasion, physical/magical and trajectory traits. Static AI usability,
motion and unresolved fourth-byte facts remain separate from working search
policy. The shared AI assembly contains no original bit masks in this model.

`OrdinaryHumanAiAbilityListSetup` supplies that exact semantic object to owned
slots. The list builder validates it against the selected supported evaluator;
it cannot silently replace explicit metadata with kind-based assumptions.
Existing authored slot constructors retain retail defaults. List summaries now
read semantic helpful/harmful, evade/Silence and trajectory properties; roster
and variable-pair enemy physical counts read physical harmful entries rather
than assuming every harmful entry is physical. Retail results/RNG remain equal.

Only the currently traced combinations are executable. Removing Throw Stone's
enemy/physical/trajectory/evasion trait or adding unsupported Silence/other policy
does not automatically enable a different search; it explicitly rejects. Clearing
UsableByAi excludes a known action, while toggling the static motion bit leaves
working consumption policy unchanged. Full configurable policy variants remain
future work; metadata preservation and semantic decoding are already general.

Package schema1/export shape remains unchanged: encode the semantic values to
the existing raw wire field, reconstruct on load, and preserve unknown type/AI
bits. ZOD-330 adds named area-shape combinations to make this lossless.
No new dependency or production friend access. All nine unused owner drafts,
including AbilityToggles/AiBehaviorToggles, remain unchanged and unwired.

Thirty-eight new Content cases cover32 individual bits, three retail combinations,
zero/all-set records, copied input storage and unchanged round trips. Two AI cases
verify metadata identity through owned lists, source summaries, usability and
unsupported-edit/direct-slot rejection. Existing full command/turn/replay tests
preserve actual rules and source streams. Two Python cases validate37 independent
descriptions and static-versus-working separation/tamper rejection.

Initial ZOD-327/328 validation: locked restore, zero-warning/error Release build, **4,521 .NET cases** (1,521
Framework, 1,217 AI, 823 Content), **596 Python tests**, pinned source/guide,
coverage/link and owner-draft audits pass. See
[receipt](../evidence/ai/ai-behavior-implementation-v1.json); ignored logs under
`scratch/ai-flags-326-328/`. ZOD-329 next binds prepared Dash content and grants.
