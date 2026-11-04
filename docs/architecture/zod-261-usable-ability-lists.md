# Content-backed ordinary AI lists — ZOD-261

Studied 2026-11-04 from managed base `bdd7572`; read-only fft_decomp checked
clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/ai/usable-ability-lists-source-v1.json) pins this
trace. No original or C execution; prepared SCUS executable identity was checked.

## Source construction and initialization

`usable_abilities_build` inserts Attack for human primary skillsets, then primary
actions, secondary actions and Frog Attack. It does not deduplicate equivalent
abilities or repeated primary/secondary skillsets. It writes a NONE terminator
only when space remains. The source reader stops at NONE or34 entries. These
are original storage constraints, not managed catalog/roster capacities.

`ability_skillset_list_build(ACTION)` preserves slot positions while zeroing
non-action IDs using original ID ranges, not SerializedClassRaw. The ordinary
adder examines the first16 human action positions, skips empty/AI-disabled
abilities and checks maximum MP (including Half MP), weapon requirements and
`ai_ability_is_known`. The innate-Innocent test reads the previously considered
ability's Faith flag, a retained-context quirk. Throw, Math, gun Charge, Jump
and monster fourth-slot behavior take separate paths and remain unsupported.

Known-bit lookup is not simply membership in a job. Monsters know everything;
skillsets5..23 map to learning rows0..18; other owned primary/base-job sets map
to row0, and non-owned other sets return known. Managed content has no complete
career/base-job/ENTD learning state. Require explicit resolved known-slot inputs
instead of inventing that state or treating learning cost/CanLearnWithJp as known.

Initialization counts every inserted entry, including unusable non-Frog Frog
Attack. MP/Silence/evasion numerators exclude Monster Skill-dependent entries.
Quarter shares use floor(numerator*4/count), zero for count0. Low usage bits
are recomputed while upper policy bits survive. Static byte4 EvadeWithMotion
does not become runtime ConsumeAimTiles. Trajectories bypass low-bit derivation;
range+area<3 evadeable Attack is exhaustive; AoE0 Accumulate forces consume aim.
Frog Attack's primary AI evade flag is clear for the numerator, but its secondary
evadeable flag is set: initialization recomputes its exhaustive-origin bit.
The actor-only preparation pass derives consume policy and clears usability
after reach/count summaries when ACT is already used.

Prepared data confirms Accumulate AI bytes `[9,128,0,128]`, Stone
`[130,2,65,129]`, Frog Attack `[130,0,1,129]`. Attack uses a derived ordinary sword
summary, not ability0 metadata. The executable is356352 bytes, SHA-256
`287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Only selected semantic facts enter evidence; prepared files remain ignored.

## Existing content and selected children

Content already owns immutable job/skillset/learning metadata, explicit action
slots, closed immediate/targeted effect companions and per-unit executable
grants. It does not identify general command-menu types, actual learned rows,
innate statuses or retained AI usage bytes. Preserve explicit ordinary-human/
default-menu and clean eligibility/usage context assertions for those gaps.
Keep AbilityToggles and other owner definition drafts unchanged.

- **ZOD-262 — Build semantic ordinary human lists.** Add a pure owned list
  builder in AI using stable IDs and explicit known/AI-eligible action slots.
  Preserve primary/secondary order, holes, duplicates, appended Frog and
  initialized versus ordinary-usable counts. Reject eligible unsupported actions
  rather than silently omit them. Return immutable data without RNG/authority.
- **ZOD-263 — Bind content lists into fresh preparation.** Add an outer Content
  setup adapter validating metadata/effect identity, selected job/skillset
  instances and explicit known slots. Interpret the original first16 action
  positions only in that compatibility adapter; do not constrain catalogs or
  truncate semantic lists. Decode existing raw metadata at this boundary; do
  not consolidate owner flags. Supply owned lists to preparation and derive
  the existing canonical Attack/Accumulate/Stone/Frog summaries and actor entry
  policy from them, replacing BothInitializedLists for this entry. Validate
  exact executable grants. Reject other list shapes/order before refresh.

The AI assembly must retain its Framework-only production dependency. Content,
the outer setup layer, may depend on AI to build its semantic inputs; AI never
reads content files. Existing certified APIs stay supported and unchanged.
The new configuration flows through existing scenarios, workspace-aware search,
commands, post-ACT and exact recovery without a second preparation or cached
result input. No general list traversal is claimed until a later bounded child
removes the four-entry consumer restriction.

Tests should cover ordering/duplicates/known versus AI-disabled entries, source
denominators, content mismatch, non-inference of learning, canonical preparation
parity before/after ACT, literal RNG and full-turn/workspace recovery. Further
menu types, full learning/equipment state, roster expansion, host/editor work,
replacement interfaces, frame RNG and ZOD-106 remain separate or deferred.
