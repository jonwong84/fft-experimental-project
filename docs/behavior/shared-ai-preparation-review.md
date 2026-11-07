# Shared AI preparation retrospective — ZOD-431

Completed 2026-11-07 as a bounded batch of the
[ZOD-428 programme](../development/documentation-review-programme.md), following
the [simulation review](shared-ai-simulation-review.md). Two confirmed defects
are corrected: HP usefulness without a side preference, and admission of special
menus into the ordinary spell path. This does not complete the original AI port.

Managed baseline: `05cb5e6c82da12bfa8a080d18e9e91896960d1bf`. The read-only
reference was rechecked clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [receipt](../evidence/ai/shared-preparation-review-v1.json) pins source,
current managed inputs, prepared menu bytes and validation. Fingerprints include
dependencies with broader branches than this review's admitted domain.

## Entry points and distinct state

`AiHumanAbilityLists.Build` owns semantic physical lists; the content adapter
supplies resolved known slots, selected effects and decoded primary metadata.
It preserves Attack, primary/secondary occurrences and initialized Frog Attack.
Unknown or AI-disabled actions are omitted; an eligible unsupported effect is
rejected. `RosterAiPreparation.Evaluate` owns fresh roster refresh and summaries.
Older sword/Attack-Stone/Accumulate/variable-list preparation paths retain their
narrower certificates and restored-state preparation boundaries.

Magic uses `OrdinarySpellAiLists.Build` and `OrdinarySpellAiParameters.Load`.
`OrdinarySpellAiCatalog` binds exact definition-owned spells, generic primary
skillsets and explicit learned rows. `OrdinarySpellAiWork` freshly loads the
selected entry, checks current MP and derives useful targets. Ready/candidate/
aim/search queries reuse it; cached masks cannot authorize battle mutations.

The source path is `usable_abilities_build` ->
`ability_usable_from_skillset_add`/`ai_ability_is_known` ->
`ai_unit_abilities_init` -> fresh `decision_flags_refresh` ->
`ability_considerations_set` -> team/target policy -> `useful_targets_filter`.
Initialization, current usability, usefulness and actual execution are separate.
Primary preferences neither select formulas nor grant actions. Static fourth-byte
flags are replaced with working entry usage when loading the considered record.
Owner toggle drafts remain untouched.

## Confirmed fixes

**HP without a side preference.** After absolute exclusions, source
`ai_target_classify_ability_effect` accepts an HP effect when neither Target
Allies nor Target Enemies is set. The managed classifier returned None. It now
returns Useful on either side, including full HP. Restrictions, Dead and caster
exclusion still precede this branch. With both preferences, the earlier enemy
branch still wins in this profile. Formula kind and numeric Faith do not choose
the preference.

Two classifier cases and a composed ready-hit case failed before the fix. The
composed case now reaches formula/resource work without mutating its input:
Fire projects enemy HP200->146, caster MP75->69 and four AI evasion draws.
Priority49 is HP128+equipment357+MP14 minus enemy HP93+equipment357. Neither side
preference adds side credit or penalty. General aim search still rejects useful
self-target alternatives pending their separate source pathway.

**Default-menu admission.** Ordinary ability ID/type did not establish a
default-menu skillset. The path could accept a specially routed skillset while
applying ordinary spell rules. Hash-verified SCUS table0x80065CB4 identifies17
non-default human IDs:1,2,3,4,6,8,17,18,19,20,21,52,56,57,59,62,156. Their paths
cover Attack, Defend/equipment/unknown menus, Item, Charge, Elemental, Jump,
Draw Out, Throw and Math Skill, including special-character copies.

One shared admission helper now rejects these IDs, empty loader IDs and monster
sets in lists, the loader and catalogs. A zero list still means no entries.
Default-menu special IDs remain supported by pure list/learning helpers; the
catalog's generic-primary restriction stays stronger. This is a compatibility
guard, not an editable content-table owner or a new menu implementation.
All17 source-literal cases failed before the fix and now reject through primary,
secondary and direct-loader entries. Three positive cases retain default IDs5,
74,175 and their known-row aliases.

## Verified rules within admission

- Source action filtering uses ability ID<422, then the first16 human positions.
  Known rows use skillsets5..23 -> rows0..18. An owned primary/base outside that
  interval uses row0; unowned special sets and monsters take the known shortcut.
  Primary/secondary repetitions remain distinct. The semantic physical builder
  accepts resolved facts; its content adapter applies original action positions.
- Initialization compares maximum MP after Half MP, not current MP. Managed
  nonnegative integer costs retain original byte imports. AI loading retains raw
  CT: Monster Skill halves before Defend's zeroing branch; Persevere bypasses
  both. Actual Short Charge/Non-Charge setup remains different. The source stale
  considered-Faith/innate-Innocent quirk remains outside the clean eligibility
  certificate and ordinary empty-status magic domain.
- Frog remains in initialized denominators despite later non-Frog disabling.
  Paid-cost extrema exclude zero; no paid entry retains minimum255/maximum0.
  Monster Skill entries are denominator-only for initialized shares. Primary AI
  Evadeable controls shares; secondary combat Evadeable can select short-range
  exhaustive origins. Trajectory precedes shape/range initialization; low search
  bits are rebuilt while higher working restrictions remain separate.
- Physical first-pass counts/reaches precede the actor's post-ACT usability
  clear. Helpful share is floor(helpful*128/total), MostlySupport starts at77,
  and the first source-order minimum ally is cleared. Three helpful/two harmful
  gives76; five/three gives80, but a sole minimum still loses the flag. Enemy
  retained MostlySupport is not invented. Attack guarantees hostile reach Move+1;
  self-stat range0 gives allied reach Move. Move<=124 avoids source byte/reach
  spread mismatch in these downstream profiles.
- Stable teams derive Enemy relative to the actor. Fresh refresh retains the
  certified ConserveCT/TeamSupportUnavailable policies, with strict
  HP<floor(maxHP/2) and HP*4<maxHP*3 flags. Team support needs two eligible allies
  and one non-Critical/non-Frog ally. Retained unavailability exits before counts;
  otherwise failure propagates to non-enemies only for non-Blue. Blue preserves
  prior policy. The admitted general prefix rejects Blue healer focus below45/128,
  conditional retreat and support-heavy Fading Life instead of substituting focus.
- Plain HP usefulness preserves odd-maximum half thresholds and branch order.
  Accumulate separately rolls one real parity coin per active allied source
  slot, including out-of-range allies; enemies do not roll. Filtering is fresh
  per entry, independent of PA saturation. Late failure restores entry RNG with
  no partial mask. Geometry, target selection and hit accounting remain separate.

## Validation and remaining work

All5,181 rebuilt .NET tests pass, including1,384 AI; all794 Python tests pass.
The45 focused list/parameter/usefulness cases include23 new cases. Existing source
audits pass16 variable-list,12 roster-list,13 preparation,6 refresh and18
Accumulate descriptions, plus ordinary learning/charge/MP descriptions. These
offline audits describe source; current managed tests execute C#. No reference
C, original executable or native presentation was executed.

The owner's controller edit, ten owner draft blobs, immutable JobStats and approved
targeting formatting are preserved. Historical receipts remain unchanged; current
corrections are recorded separately here. General status/equipment/item policies,
full origin search and frame RNG remain deferred. ZOD-433 next reviews target
selection/scenario admission; ZOD-426 remains a queued supporting origin study.
ZOD-432 remains the required readability walkthrough/refactor after the full port.
