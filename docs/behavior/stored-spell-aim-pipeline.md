# Stored spell aims and pure casting phases

Source is pinned to clean read-only `fft_decomp`
`11464943bbdbd2a0af022d90cb7f10455cf911df`, rechecked from managed base
`befa904548dc596c887a17c14d5a962451c35820`. The existing
[follow source receipt](../evidence/combat/spell-follow-source-v1.json) covers
19 files/26 functions; the [resource receipt](../evidence/combat/magic-resource-source-v1.json)
covers12 files/10 functions. Fresh fingerprint audits and their normalized
descriptions pass under ignored `scratch/spell-aim-364-369/`. No C/original code
is executed. The [prior trace](spell-follow-and-stored-aim.md) owns source detail.

The selected six children separate centre resolution, cast integration, blue
choice classification, certified confirmation capture, immutable charge planning
and ready-plan calculation. They reuse existing geometry, charge, MP and formula
owners. No commands, live state/RNG adoption, global clock, automatic facing,
status/corpse/Golem/terminal/frame RNG or magic AI activation are added. Source
slot reuse and runtime cursor preference remain caller-certified boundaries.
Owner drafts, AbilityToggles, JobStats, host/editor, broad interfaces and ZOD-106
remain separate. The custom forward1/3/5 cone remains unimplemented.

## ZOD-364

`SpellAimResolver` resolves exact targeting type6 from a known matching stable-ID
anchor's current position; all other byte types, including YES7, use stored
signed16 coordinates narrowed to unsigned8. It validates the admitted original
map dimensions/sheets, bounds and blocked centre. Untargetable centres remain
valid at this stage. It does not check anchor activity/status/team or current
caster range, and never falls back to the old tile when anchor facts are missing.

The positive profile certifies an active valid ordinary nondirectional actor and
ability. Original slot-index validation belongs before stable-ID adaptation;
the managed roster has no fixed21-unit cap. Unknown/mismatched anchor data reject
explicitly. This primitive returns only a centre, not green panels or a cast.
Three managed cases pass13 applicable original centre literals, identity/knowledge
guards and map/collision boundaries. Directional/invalid original-slot/actor cases
remain source descriptions outside this admitted primitive.

## ZOD-365

`StoredSpellCastQuery` finds a unique current anchor from certified complete
roster metadata, resolves the centre with `SpellAimResolver`, then delegates the
existing `OrdinarySpellTargeting.ResolveCast` and `MagicCastQuery` owners. It does
not recheck blue range or spend MP a second time. Inactive anchors may omit formula
snapshots while retaining explicit position data; they can centre bystanders.
Missing/duplicate anchors, unknown position, unsupported selected targets and late
RNG exhaustion return the incoming checkpoint with no cast outcome. Six cumulative
managed cases pass current-versus-stored position, order,34/68 damage, exact RNG,
inactive/Jump eligibility, replay and atomic-failure vectors.
