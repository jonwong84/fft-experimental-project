# Equipment-effect selection — ZOD-419

Owner-requested source investigation, 2026-11-06. Managed baseline
`52f8f22f6ce1d1e28e83395bb77c021ebc693e26`; clean, read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/combat/equipment-selection-source-v1.json)
pins thirteen files and fifteen functions, including the selector, its three
formula callers, result application, inventory routing, equipment conversion
and AI scoring. This is source study, not original/C execution or a managed
equipment-break/steal implementation. All owner drafts remain unchanged.

## The original chooses a policy by ability ID

[`formula_select_targeted_equipment`](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/formula_modifiers.c#L523)
branches on the current `u16` ability ID. The secondary formula's X/Y values do
not select the equipment slot. The thirteen recognized IDs share five policies:

- **Head:** Battle Skill Head Break138, Steal Helmet110 and Mighty Sword
  Blastar Punch161 select the head slot.
- **Body:** Armor Break139, Steal Armor111 and Shellbust Stab160 select the body slot.
- **Accessory:** Steal Accessory114 and Icewolf Bite163 select the accessory slot.
- **Shield:** Shield Break140 and Steal Shield112 select the right shield when
  present, otherwise the left shield. Required level, power and price do not
  participate in this choice.
- **Weapon:** Weapon Break141, Steal Weapon113 and Hellcry Punch162 select the
  weapon with the higher item **required level**. Equal required levels select
  the right weapon. This does not compare weapon power, price, rarity or the
  unit's level. A sole left weapon can be selected.

The picker has no RNG. It reads the already-separated battle equipment slots;
it does not scan both physical hands and dynamically classify their items.
The item category was established at the loadout/conversion boundary.

Monsters are rejected before the ID branches, even if their equipment array
contains item IDs. Missing equipment or an unrecognized ability also returns
failure. Assigning this formula to another original ability ID does not itself
give that ID a target policy. An imported compatibility definition must preserve
that distinction; custom content can explicitly select a new combination.

Required levels are unsigned bytes, widened into signed32-bit comparisons.
An absent weapon has comparison value-1. Two absent weapons fail rather than
becoming a right-hand tie. A successful pick records one `u8` result mask and
one `u8` item ID. Failure clears the mask and returns-1, but does not clear the
previous `item_lost` field. Its transient initial mask0xFE is overwritten with
one slot or zero; it does not mean every item is selected for destruction.

## Slot, category and result encoding are separate

The source has several equipment representations:

- Menu/formation records have five slots: right hand, left hand, head, body,
  accessory. ENTD also has five, ordered head/body/accessory/right/left.
- Battle/party equipment has seven locations: head0, body1, accessory2,
  right weapon3, right shield4, left weapon5, left shield6.
- Action results encode those seven battle locations as bits0x80,0x40,0x20,
  0x10,0x08,0x04,0x02. These are masks, not slot ordinals.

`encounter_equipment_set` splits each human hand into weapon/shield locations
using item metadata and normalizes its supported empty-item paths. Menu preview
conversion merges each hand again; `equipment_commit_loadout` splits it when
committing edits. These are different adapters, not directly interchangeable
arrays. Two-handed rules affect loadout construction; the equipment-effect
picker itself does not expand a selected weapon into two removals.

The owner's `EquipmentSlot` draft has None/MainHand/OffHand/Head/Chest/Accessory.
Its values are semantic design placeholders. They match neither the source
battle indices nor result masks. MainHand/OffHand must not silently acquire the
meaning of physical RightHand/LeftHand or of Weapon/Shield. A five-slot managed
model can still work, but the adapter must preserve each hand's item category
and exact source hand order for these selectors and compatibility diagnostics.

## Selecting an item does not guarantee its removal

The selector tests for ITEM_ID_NONE255, without filtering unremovable equipment.
[`equipment_removal_apply`](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/battle/combat/resolution.c#L1537)
later skips item0 (Nothing), item255 (None), and items marked Unremovable. There
is **no fallback to the other hand** after the selected item fails these guards.
For example, an unremovable higher-required-level right weapon still wins over
a removable left weapon, and no item is removed. The fixture's raw item0 case
describes the source guard difference; it does not establish a reachable normal
loadout with item0 in a battle slot.

Maintenance is another separate guard. `formula_apply_maintenance` clears hit
and accuracy and marks forced failure; the result application returns before
equipment removal when hit is clear. Selection does not filter Maintenance out
or choose a different item because of it.

For an accepted removal, the source clears the selected battle location. Steal
then calls `item_give`, which awards inventory only during execution and for an
initially Blue attacker. At stock99 it routes through the war-funds helper,
which also checks the current team before adding quarter-price funds. Therefore
"give the item to the actor" must not mean equipping it or adding it to every
team's inventory. AI simulation removes equipment privately but awards no item;
its scoring compares all seven locations with the saved baseline, adding51 for
each unchanged location. Stat refresh is execution-only at this removal stage.

Removal of a selected weapon can also queue Charging removal, specifically for
an active Charge-skillset action with charged CT other than0xFF. This is not a
general cancellation of every charged ability. Full inventory, stats, Charge,
AI and status integration still require their own managed implementation work.

## Formula families retain their own ordering and failure behavior

The shared selector has three distinct callers:

- **Battle Skill equipment break, formula0x25:** select first. Failed selection
  recalculates a plain Attack, including for monsters/unrecognized IDs. Otherwise
  perform physical evasion, the PA/weapon-power/Y accuracy chain, Maintenance,
  then mark Break Equipment on a hit. Maintenance does not cause an Attack fallback.
- **Equipment steal, formula0x26:** select first. Failed selection forces failure.
  Otherwise physical evasion, the Speed/X physical-status accuracy chain,
  Maintenance, then mark Steal Item on a hit.
- **Mighty Sword, formula0x2E:** physical evasion happens **before** selection.
  Failed selection forces failure. Maintenance can cancel it before equipment
  break and the weapon-damage/element calculation proceeds.

The default-Attack helper restores the command skillset, ability ID and strike
counter after recalculation, and clears pending weapon-proc/reaction work.
The same target policy therefore does not imply interchangeable formulas,
failure handling or RNG order. This study pins those boundaries, without
claiming a complete accuracy/modifier/reaction or fallback-attack port.

## Agreed implementation direction

Shared code belongs to mechanics/formula families; individual abilities are
immutable content data. Proposed namespaces are `Zodiac.Framework.Formulas`
for calculations and `Zodiac.Framework.CombatRules` for reusable rules. Content
remains validated JSON loaded into typed immutable definitions. Formula handlers,
combat rules and geometry implementations stay C#; animation references stay in
presentation data. This is the agreed target, not an already-completed migration.

Equipment effect parameters should carry an **equipment selection policy**:
fixed head/body/accessory, right-first shield, or highest-required-level weapon
with right ties. Names here describe semantics, not a proposed source-byte enum.
A typed selection result identifies the exact current location and item. A slot
enum is useful for that result and fixed-location policies; it is insufficient
to express all selection policies on its own. A generic property or untyped
parameter bag is unnecessary. These are reusable mechanic definitions, not
separate BreakHelmet/StealWeapon classes.

The original adapter maps recognized ability IDs to these policies explicitly;
runtime selection uses the policy and fresh equipment/item data. This removes
ability-name switches from the shared managed rule while preserving imported
original behavior. An unresolved original ID/formula combination needs explicit
unsupported handling until its source failure path is implemented. Never infer
a default hand or category from the ability's display name.

Formula-specific typed parameters and failure behavior should compose alongside
existing metadata. `AbilityMetadataDefinition` remains authoritative for its
current learning/AI fields; primary AI flags and secondary execution flags retain
their separate meanings. AbilityToggles consolidation remains deferred. Do not
activate the draft EquipmentSlot enum, widen gameplay, or start a project-wide
namespace migration as part of this source investigation.

The first implementation child should resolve the selector from explicit current
equipment facts and item required levels, returning a typed result without
mutation or RNG. Only then integrate one equipment formula through hit, actual
removal and its applicable inventory/status/AI boundaries. The existing Dash/
Throw Stone family refactor can proceed independently using the agreed content
direction; this investigation does not activate more abilities or generic shapes.

## Validation

Nineteen [hand-authored descriptions](../fixtures/behavior/equipment-selection-v1.json)
cover fixed slots, monster/unknown/empty rejection,
shield precedence, single-hand weapons, level0/255, right ties, unremovable
selection without fallback and the item0/255 distinction. Eight Python cases
also check all thirteen recognized aliases, power/price independence, changed tie/priority
expectations, invalid widths/locations, source-only claims and unchanged inputs.
They are independent offline descriptions, not an executed original-game oracle.

Recheck the pinned source and descriptors without running C:

```powershell
python tools/verification/verify_equipment_selection.py --reference C:/repositories/fft_decomp --output scratch/equipment-selection-new.json
python -m unittest discover -s tools/verification -p test_equipment_selection.py
```

The [study receipt](../evidence/combat/equipment-selection-study-v1.json) records
the full Python regression result and preservation checks. No C# production code,
owner draft, prepared asset or runtime behavior changed. The previous5,044 .NET
tests and native checks remain the ZOD-417 baseline, not reruns for this study.
