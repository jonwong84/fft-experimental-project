# Ordinary spell primary metadata

## ZOD-397

The selected spells now bind to the existing immutable `AbilityMetadataDefinition`
and `SkillsetDefinition` models. No `AbilityToggles`, AI toggle draft, JobStats or
shared flag model is changed. Source remains clean/read-only at
`11464943bbdbd2a0af022d90cb7f10455cf911df`; the
[receipt](../evidence/combat/ordinary-spell-primary-metadata-source-v1.json)
pins six files/five functions. The source-reviewed elemental/Cure vectors now
also record primary learning/class/AI metadata.

### Primary and secondary roles

Primary AI preferences do not determine player legality or executable formula
behavior. Fire's primary AI Evadeable bit is clear while its secondary execution
Evadeable bit is set. Cure's primary MagicalAttack bit is clear despite using a
magic healing formula. These distinctions survive import, runtime loading and
round-trip export; the executable spell definitions retain their own source
parameters. Static AI byte4 supplies usable/motion/unresolved metadata. The AI
loader overwrites its working byte4 with usage flags, so those roles are not
interchangeable. This work does not activate a general magic AI evaluator.

The original metadata adapter preserves serialized class, raw learn rate,
LearnOnHit, ShowName, the negative CannotLearnWithJP polarity and unresolved type
bit4. Known behavior lives in named immutable fields; the established metadata
codec retains its compatibility byte export. Unknown bits retain their opaque
representation and acquire no guessed behavior.

### Prepared boundary

Original preparation now writes battle schema2 with a required metadata asset
reference. A separate hash-checked `ordinary-magic.metadata.json` uses the existing
content-package codec. Its abilities exactly match executable spell IDs; its
skillsets retain the selected original slot indices. Basic Skill explicitly has
an empty intersection with this selected spell catalog, not an empty original
ability list. The importer additionally reads that source row to verify it.
There are no duplicate job definitions in the companion; immutable jobs/growth
remain with the existing prepared battle data.

Schema2 rejects a missing/mismatched/add-on/incorrect-profile companion,
unresolved job primaries, extra/missing spell metadata or unbound slots. Both
assets are size-budgeted and hashed before use. Schema1 still loads without a
companion and exposes no inferred AI metadata. Preparation publishes only after
reloading the complete staged package; all original data and generated output
remain ignored under scratch. Current Fire/elemental/Cure imports contain
35/57/39 source records respectively, including the added Basic Skill row.

Twelve content and two importer cases cover companion identity/completeness,
legacy loading, hash/budget/missing-asset failure, source slots, one bound metadata
instance, independent AI/execution flags and raw unknown/learning-bit preservation.
The source/prepared auditor verifies every primary record, selected slot, asset
hash and offline receipt. Native Fire, elemental and Cure encounters retain exact
gameplay hashes from ZOD-396, including movement, charge, rewards, restart and KO.

### Selected follow-up: ZOD-398

The fourth elemental tiers have LearnOnHit set, with raw rate30. The source's
post-action scan marks an affected non-actor **LearnChecked even when its primary
skillset is ineligible**. An eligible unlearned recipient can then roll and receive
a learning report; report confirmation applies the current-job learned bits.
Those post-action learning steps are not implemented by this metadata work.
The current Squire opponent cannot learn these Black Magic spells, so no omitted
learning roll occurs in the checked demos. Their markers still describe the
pre-learning-report state; full tier4 post-action equivalence is not claimed.

Until ZOD-398 owns that state and report boundary, schema2 rejects potentially
eligible unlearned recipients and repeated learnable slot bindings. It does not
silently skip an available learning offer. Complete the marker pass, source
eligibility/ordering/RNG and explicit acceptance/decline state before extending
these encounters or activating broader magic AI. Keep existing Fire/Cure numeric
and native regressions, source rates above100, owner drafts and other deferrals.


ZOD-398–400 follow-up: [owned learning and prepared slot state](learn-on-hit.md)
now implement the selected source scan. The earlier schema2 eligibility and
repeated-slot guards above describe the ZOD-397 checkpoint; they have been
replaced by explicit owned learning rows/reports. Schema3 provides partial-slot
initial state. Client reporting and disabled-menu policy are the selected next
milestones; no general magic AI is activated.
