# ZOD-185 — Ordinary AI ability-entry iteration

Source/data investigation, 2026-10-29. Read-only clean fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base
`a80032fcea352f5be16a4661c2ed480385e14799`. The
[source receipt](../evidence/ai/ability-iteration-contract-v1.json) pins 12 files /
29 function bodies and inherits the ACT-first receipts. The
[15 literal descriptions](../fixtures/behavior/ai-ability-iteration-contract-v1.json)
are isolated cursor/loader/gate/rank/arithmetic examples, **not** a full search,
managed implementation, RNG-cost oracle or original/C execution.

## Cursor, reset and retained state

`turn.c::action_select` resets the rank fields/defaults of eight ranked slots,
current/best/baseline/selected actions once on fresh entry; it retains other
payload. In the ordinary general branch, `general_select` builds movement
scenarios once, seeds D0's movement baseline with hit100, handles the no-charge
case and selects active targets before `ability_list_evaluate`. These are shared
across entries, not a fresh movement/selection query for every ability.

The list evaluator sets its cursor to zero on fresh entry. It reads the current
entry, stops at skillset **255** (`SKILLSET_ID_NONE`), loads it, and invokes the
outcome evaluator. Completion, including unusable skip, increments the cursor;
suspension returns -1 without increment. Resume jumps straight to the same
outcome evaluation: it does not reload the entry or reset working state. The
original loop stops after slot33 completes (34 entries). That compatibility
capacity is not a proposed managed authoring/content limit. The cursor itself
draws no RNG; evaluated work may draw. Resume/frame/VSync equivalence remains
outside the current managed pilot.

`ai_ability_entry_load` clears considered-ability storage and four calculator
command bytes, not the entire current action, target workspace, best action or
ranked list. Its packed ID has a **10-bit** ability mask (`0x3ff`) and physical
slot in the upper bits; this is distinct from the other source ability lookup's
`0x1ff` mask. Normal IDs/physical slots must be validated before importing this
packing. They do not replace managed stable IDs or compact-unit identities.

Default-menu loading copies static AI bytes1–3 but **replaces byte4 with the
entry's runtime usage byte**. Static EvadeWithMotion and runtime ConsumeAimTiles
share a bit position with different meanings. Usable, exclusive team restrictions,
Monster Skill and origin/aim consumption policy belong to this working record.
`abilities.c::ai_unit_abilities_init` recomputes low policy bits; preparation
later checks restrictions/MP and can clear actor usability after computing reach.
Static definitions and mutable AI working state must remain separate.

After its frame check, `ai_ability_outcome_evaluate` returns immediately when
the working sign/usable bit is absent. It performs no target or formula work.
Random-use entries draw `rand()%128` and proceed when the result is **<=** the
prepared threshold. Ordinary entries skip that gate draw. An admitted entry
copies current command fields, restores target eligibility from the active list,
then runs usefulness/origin/target passes. Aim-mask policy is updated at specific
pass boundaries; it is not a whole-workspace clear between entries.

`ai_best_rank_insert` requires best rank strictly above the movement baseline,
then inserts above a ranked slot using decided:u8, priority:s16, hit:u8 and lower
foe-proximity ties. Equal proximity is not an improvement. The ranked list
survives the next entry. After each insertion attempt, only best rank/defaults
reset to marker0/priority-32767/hit0/ChooseAtWait/not-reflected; payload remains.
Unusable/early-return entries do not themselves call that reset. The shared
lottery runs after the list, not after each ability.

Candidate simulations save/restore their actual baseline unit data, including
base/effective PA, HP, statuses, coordinates, choices and equipment. Decision RNG
is retained. The next entry must not inherit the previous hypothetical result or
restart its RNG; actual Accumulate PA remains battle state, not campaign growth.

## Selected child: Throw Stone formula projection

Rechecked prepared `scratch/all-data-extraction/SCUS_942.21`, size356352 and
SHA-256 `287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562`.
Only selected semantic facts are recorded; raw records stay ignored. This uses
prepared data, not the BIN, and changes no importer/schema or owner's flag drafts.

Ability148 Throw Stone has range4/AoE0, formula0x37/Y2, zero CT/MP and static
enemy/HP/physical/direct/evadeable facts. RandomUse, FollowTarget, Reflectable,
TargetMapTiles and AffectsStats are absent. Its closed executable rule/grants
already exist. Direct targeting still needs the existing certified clear flat
two-unit geometry; range alone does not establish a clear projectile path.

In `formula_damage_random_y_times_pa` → `formula_apply_damage_and_knockback`,
non-execution `formula_get_random` returns 16384 without a draw. For Y2 this
gives `2 * current effective PA` (PA5→10, PA6→12), with unsigned16 XA conversion
and signed16 damage harmless in the current PA1..99 envelope. The helper returns
before the execution-only Brave/knockback block: **AI projection never pushes**.
Four physical evasion calls still consume real BIOS draws before their execution
gate. They retain accuracy without stochastically rejecting the AI hit.

With positive accuracy the projected HP loses full normal damage, not damage
multiplied by hit chance: HP50, PA5, accuracy25 still gives HP40, accuracy25.
The percentage remains a separate rank/lottery field. Zero accuracy consumes
four draws but fails the single-target valuable-hit gate before clock/scoring.
Actual Throw Stone instead uses its existing live hit/damage/knockback RNG path;
invoking it on a copy would not implement the original AI projection.

Accumulate146 is retained for a later child. Its verified static facts include
ally/stat/map-tile behavior, and `ai_target_classify_ability_effect` adds an
even/odd `rand()` usefulness decision for it after earlier filters. That function
can run during preparation and multiple target passes; there is no established
one-extra-draw-per-ability schedule. Stat hits also add +3/-4 ranking terms and
require correct temporary PA restoration. Those differences make it a larger
next step than Throw Stone, not an unsupported gameplay ability.

**ZOD-186** implements only a pure Throw Stone AI formula/target-result query.
Use existing explicit grants and factor/reuse pure state/geometry validation;
keep command/session/receipt authority in Framework. Inputs use immutable state,
typed ability command/profile and decision RNG, with semantic facts expressed as
booleans, not another packed runtime flag API. Verify range1..4 including diagonal
facing, current PA, separate hit/damage, zero accuracy, exactly four retained
draws, no knockback, exact exhaustion, replay and live input isolation. No CT
clock, list traversal, candidate scan, ranking, selection, command execution or
host activation. Subsequent children can compose that checked primitive into
candidate scoring and multi-entry search without weakening the Attack APIs.

## Reproduction and limits

`verify_ai_ability_iteration_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new scratch report>`
checks pinned source fingerprints, all descriptions and optional selected data
facts. Omit the prepared input for source-only reproduction. Three Python cases
cover literals/input preservation, malformed description envelopes and tampering;
the verifier's envelope checks are not claims about C's corrupt-input guards.
All **519 Python tests** and the clean source/data audit pass. Logs:
`scratch/ai-ability-iteration-185/`. Runtime code/tests are unchanged; last managed
validation remains ZOD-184's clean build and 3,986 passing .NET cases. No C,
original or new managed execution, UI review, broad interface or activation claim.
