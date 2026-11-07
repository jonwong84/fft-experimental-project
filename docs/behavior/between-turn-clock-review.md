# Supplied between-turn clock controller — ZOD-430 supporting trace

The owner's In between turn control routine is `turn_clock_run`, starting at
0x801827F0. It is the shared battle-clock state machine used by actual gameplay
and AI simulation. This supporting trace does not complete ZOD-430 or activate
its unimplemented status/charge/Mime branches.

Reference `11464943bbdbd2a0af022d90cb7f10455cf911df` was rechecked clean/read-only.
The [trace receipt](../evidence/ai/between-turn-clock-trace-v1.json) pins seven
source/metadata files and six function bodies. All546 supplied instruction
words match the prepared BATTLE.BIN (1,397,096 bytes, load0x80067000, SHA-256
`7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94`).
The pasted split word `afb0 0018` is normalized to `afb00018`; malformed
assembly operand text does not change the verified instruction words.
Neither C nor the original game was executed.

## Dispatch and ordering

The jump table at0x80174174 has20 valid entries, indices0..19. All20 words
match the executable. The unsigned `<20` guard rejects index0x14: the supplied
word at0x801741C4 is adjacent data, not another controller phase. Unassigned
indices within0..19 reach the common continuation/budget tail.

The normal phase flow is:

- Add CT, then select the highest eligible CT>=100, retaining the first source
  slot on ties. This is not a corpse-only scan. Slow takes precedence over Haste:
  effective tick Speed is floor(Speed/2), or Speed+floor(Speed/2), respectively.
  The widened CT sum caps at254;255 remains reserved for Quick.
- A selected jumping unit waits at99. Other selections reduce CT modulo100,
  or reset Quick255 to0, before upkeep. Upkeep can stage a status/revival/death
  result rather than grant an ordinary turn. Its unconditional RNG draw still
  occurs for an otherwise effect-free selection and for a plain corpse in AI.
- When no ordinary unit is ready, decrement eligible charge counters, then
  emit ready abilities in source-slot order. Performing reloads its counter;
  other resolved counters become0xFF. The subsequent phase checks Quick255
  before Mimes and resumption of charge scanning.
- After charge work, expire timed statuses, collect pending action-result
  flags and emit their events, then return to CT addition.
- Active-turn completion validates current status/choices and invokes the
  shared End of Turn helper when required. It then processes Poison/Regen,
  terrain poison, Transparent removal, and Mime/resumption work. Several
  phases advance their saved state before yielding an event; a port must not
  restart the phase sequence for each call.

The wiki labels for phases0x0F and0x11 are swapped: they are terrain poison
and Transparent removal respectively. Phase0x13 is Poison/Regen. The source's
FIND_ACTIVE_UNIT branch at0x801828F0 also agrees with the actual `beq`; it returns
the first present slot without the excluded statuses, not the highest CT or
necessarily a unit currently owning a turn.

Mode1 resets CT/turn/ability-outcome/charge state, clears Golem and the
mimic-pending field, then calls `ai_simulation_end` to restore the saved attacker
block and execution mode before returning0xE000. Mode2 performs that first-slot
lookup, returning0xD000|id or0xDF00. Normal calls yield event type|unit ID:
UnitReady0x100, AbilityReady0x200, ActionResult0x300 or Mime0x500. 0xFF00 means
no returned event. The1201-counted-phase-iteration per-call budget is distinct
from CT ticks, the outer AI limit256 (at most255 clock calls), and VSync/frame
suspension. Those must remain separate counters and responsibilities.

## Direct helper map and useful next documentation

All18 JAL sites resolve to12 distinct helpers at pinned metadata addresses:

- Eligibility and turn lifecycle: `turn_status_flags_calculate`0x801832CC,
  `upkeep_action_build`0x80183078, `turn_should_end`0x80183D10 and
  `turn_end`0x80183C6C. The End of Turn page is already confirmed in the
  [preparation/simulation note](../architecture/ai-preparation-and-simulation.md).
- Status aftermath: `status_update_expiring`0x8018D910,
  `status_prepare_poison_and_regen`0x8018D7C8,
  `status_prepare_terrain_poison`0x8018DA88 and
  `status_prepare_transparent_removal`0x8018D868.
- Other lifecycle work: `action_mimic_set_ability`0x8018F038,
  `unit_ability_ct_set`0x8005E778, `unit_casualties_count`0x8005E670 and
  `ai_simulation_end`0x8018E628.

Address/name correspondence is not full numerical validation of every helper.
The most useful next supplied pages are **Status checks** at0x001832CC and
**Death sentence, crystalization, treasurization, etc** at0x00183078. The former
defines eligibility/CT-freeze and haste/slow flags; the latter stages upkeep,
including its draws and execution-versus-simulation differences. Their linked
dependencies can then be reviewed in caller order without duplicating the
already confirmed End of Turn page.

## Current managed boundary

`AiSimulationClock` deliberately collapses effect-free phases under its ordinary
positive-Speed certificates. It preserves source-slot ties, CT modulo/caps and
upkeep draws, including plain simulated corpses without death-counter decay.
`OrdinarySpellAiClock` adds its bounded immediate/delayed spell hypothesis and
outer255-call behavior. Neither is a general port of this complete controller.
Quick, general status expiry/upkeep, Mimes, terrain effects, performing actions
and original frame suspension remain separate. This supplied trace identifies
no correction within the current admitted domain.

No runtime code, fixture or historical receipt changed, and unchanged gameplay
tests were not repeated for this source-only confirmation. The owner's local
`AiTurnController.cs` edit is preserved. Source/byte checks and documentation
review do not establish original-game or full-clock equivalence.
