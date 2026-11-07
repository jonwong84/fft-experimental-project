# Mode1 derived attribute calculations

## ZOD-385

Study starts from managed `cec57a61a7fce60e9e6244db410bb613c6e8b291` and
clean read-only `fft_decomp` `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source receipt](../evidence/combat/derived-attributes-source-v1.json) pins
`initial_stats_calculate`, its equipment/refresh callers, copied field widths
and armor/attribute records. The [descriptions](../fixtures/behavior/derived-attributes-contract-v1.json)
are independent literal expectations, not results from executing C or the game.

### Arithmetic and stage boundaries

Growth calls `unit_stats_refresh`; `stats_refresh` calls
`unit_equipment_attributes_set(unit,0)`, which calls `initial_stats_calculate` in
mode1. This recomputes HP/MP maxima while retaining base PA/MA/Speed. It uses the
unit's **copied** multiplier bytes, not a newly fetched job record. Raw values are
unsigned24; multiplication is unsigned32. Even0xffffff*255 fits that width.
Normal HP/MP use `(raw * multiplier / 100) >> 14`, then floor at1 and cap at999.
Eggs use divisor10 and cap65535; for original input widths the actual largest
result is26111. Apply division/shift before floor and cap; an egg maximum is not
necessarily ten times the already-rounded normal maximum. Zero multipliers
still produce a maximum of1. Mode1 leaves current HP/MP untouched at this stage.

The later equipment pass adds armor bonuses before clamping current HP/MP.
ZOD-385 exposes a numeric **without-equipment** projection with those final
clamps, plus one internal maxima owner shared with equipment composition.
ZOD-386 must retain incoming unclamped current fields until all equipment is
processed. Passing an already-clamped bare projection into that pass would lose
valid HP/MP that armor supports. Neither path refills current HP/MP or consumes
RNG. These numeric results do not perform status reconciliation or grant live
state/career authority.

### Managed ZOD-385 result

`Mode1BattleAttributes.CalculateWithoutEquipment` returns the bounded numeric
projection and shares its internal maxima calculation with the upcoming ordered
equipment pass. It preserves retained byte base attributes, accepts original-width
raw/multiplier/current fields and rejects out-of-storage raw captures. Three
managed cases pass 14 source literals, reward/growth/maxima composition and
immutable replay/invalid captures. Eight Python cases independently check all 28
mode1/equipment descriptions. Equipment implementation follows under ZOD-386.

### Selected ZOD-386 equipment work

`unit_equipment_attributes_set` clears equipment attribute totals. A nonmonster
visits the source equipment order, skipping item255. Each present item adds byte
PA/MA/Speed bonuses, saturating each accumulated bonus at255. Move saturates
at253 when its sum is254 or more; Jump saturates at7 when its sum is8 or more.
These caps run even for a present item's zero bonus, while an absent item skips
them. Initial Move/Jump inputs are already job/R/S/M-prepared by the caller.

Only Headgear/BodyArmor type flags enable their secondary record's byte HP/MP
bonuses. Each armor store caps at999, even for eggs and even with zero bonuses.
This can lower an egg's previously greater-than999 maximum. Equipment presence,
armor classification and attribute/armor record selection are independent facts;
do not infer item type from numeric bonuses. Monsters skip all item reads, but
still receive the final current HP/MP clamps and actual attribute caps.

After all items, current HP/MP clamp to the final maxima. Actual PA/MA are retained
base plus saturated equipment bonuses, capped at99; Speed caps at50. The base
fields themselves remain unchanged, including byte values above those caps.
Status/affinity ORs, equipped sword/Materia Blade flags, equipment evasion,
R/S/M reconstruction and equippability are separate work. The original seven-slot
layout belongs to the source adapter; the numeric contribution kernel does not
create item ownership, target equipment or activate the owner's `EquipmentSlot`.
[Knight equipment-break intent](../development/content-definition-drafts.md#equipment-break-slot-selection-deferred)
remains deferred and the draft unchanged.

### Remaining integration work

Complete refresh still needs status-current reconciliation, innate/inflicted
state, timer handling, Critical recomputation and presentation notifications.
ZOD-387 traces that boundary before selecting a bounded status implementation.
The existing magic vitals, reward, raw-growth and unlock helpers are useful
building blocks, but their individual outputs cannot be adopted as a complete
magic action. That path must own coherent unit/career/status state, actor work,
paid fizzles and delayed-cast versus ordinary-turn bookkeeping. Keep the current
query rejections distinct from real executed outcomes. No live commands, global
clock, magic AI, host/editor, automatic facing or custom cone activation. Owner
drafts/JobStats, full corpse/Golem/terminal/frame RNG, broad interfaces and ZOD-106
remain preserved/separate.

## ZOD-386

`EquipmentAttributeCalculation.CalculateMode1` uses the shared maxima owner and
ordered immutable numeric item contributions, preserving incoming unclamped HP/
MP until all armor work is complete. Presence and armor classification remain
independent of bonus values. It reproduces saturated bonus accumulation,
Move/Jump/actual-attribute caps, monster selective bypass and armor's egg cap.
Base attributes remain unchanged; no RNG is consumed. Six further managed cases
pass 14 equipment literals, the late-clamp regression, type/presence distinctions,
unknown-item monster bypass, invalid captures and replay of explicitly synthetic
extended contributions. Nine cumulative managed cases pass. The contribution
kernel imposes no original slot-count limit and activates no equipment ownership,
item import, status/affinity/flag handling or owner draft.

## Final validation

Locked restore and Release build pass with zero warnings/errors. All **4,767 .NET
cases** and **736 Python tests** pass, including nine new managed cases and eight
new Python cases. The 28 independent descriptions cover mode1 and ordered
equipment work. Fresh source fingerprints (seven files/seven functions), prior
growth/unlock/reward prepared-data checks, algorithm-guide, coverage, local-link,
layout and preservation audits pass. The
[implementation receipt](../evidence/combat/derived-attributes-implementation-v1.json)
records milestone bases and ignored reports under `scratch/derived-385-386/`.
Seven new public types have matching files in Battle/Progression. All pre-batch
production, managed tests and behavior fixtures, owner drafts/JobStats and the
Knight EquipmentSlot intent are preserved. No Content, AI, host, import,
project/dependency or activation changes; no original/C execution or UI review.

The numeric layer now composes raw growth, maxima and equipment contributions
through shared owners. The main integration gap is a coherent action path owning
status refresh, delayed-cast state, actor/reward work, paid failures and final
battle publication. After the bounded status trace, prioritize an end-to-end
ordinary magic scenario with explicit unsupported branches. These passing
calculation checks do not establish that player/AI magic execution is connected.
