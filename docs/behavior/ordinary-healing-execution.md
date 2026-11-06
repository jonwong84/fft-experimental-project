# Ordinary healing execution

## ZOD-394

Source remains clean/read-only at `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [healing receipt](../evidence/combat/ordinary-healing-source-v1.json)
fingerprints six files/seven functions; the
[binding vectors](../fixtures/behavior/ordinary-healing-bindings-v1.json)
independently describe Cure and the retained Fire action. Earlier
[content](elemental-magic-execution.md), [clock/status](ordinary-magic-execution.md)
and formula receipts remain the owners of their shared rules.

Cure ID1 uses formula0x0C, power14, no element, range4/area1/vertical1, charge4,
MP6 and empty infliction0. Its secondary Silence/quote/Calculator/Reflect flags
match the selected ordinary profile, while Evadeable/CounterMagic are clear.
`formula_magical_heal` stores MA/Y, applies strengthen, Magic Attack Up/zodiac,
Faith and undead reversal. The living branch moves the stored damage amount to
HP healing, clears the HP-damage type and sets HP-healing. It does not call
magic evasion or the conditional proc roll. Undead reversal remains rejected by
the authoritative ordinary setup, so no corpse/undead behavior is activated.

The existing command owner now executes prepared Cure without a new damage or
healing calculator. Application clamps HP, updates Critical, preserves a living
target's Charging/pending cast and bases EXP on actual change. Healing an already
full target spends the cast's MP but grants no EXP/JP. Faith0 likewise spends MP
with no HP/Critical/reward change. A successful Cure can resolve at an exhausted
draw count if upkeep has already occurred and no level growth is due. Growth
still consumes its separate five draws. Queueing remains free; readiness pays
once from current MP.

`prepare-healing-magic-battle` checks38 original records, including Cure's primary /
secondary data and White Magic/default-menu slot. The authored Wizard has Fire
and a secondary White Magic Cure grant; its Priest career records50 total JP,
zero current after learning Cure. Casting still earns JP for the actor's current
Wizard job, not the spell's skillset. This is an explicit normalized demo loadout,
not campaign learning or the original job-command menu. Guard starts at HP30 /
max200 in Critical. Cure-first slot order across the two skillsets is authored.

Fourteen managed cases cover HP boundaries, full HP, Critical clearing, zero
Faith, pending-target preservation, exact draw limits, raw growth and unsupported
formula/flags/skillset/menu/learning data. Two additional Python cases validate
the healing binding. The fresh source/prepared audit verifies every imported
record and the runtime manifest. All **4,853 .NET /743 Python tests** pass with
a zero-warning/error Release build.

The native two-spell check browses both learned entries, resolves Cure at tick13
(Guard30→84, Mage75→69 MP, EXP95→0/level11, Wizard JP50→62), verifies7 total draws,
restarts, repeats Cure and then uses Fire twice to finish at first KO. Complete
receipts match headless replay. Start/Critical, healed and later Fire/Critical
captures were inspected. Rendering/time do not advance gameplay. Original media,
physical input, general magic AI and the earlier explicit deferrals remain open.

## Launch

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- prepare-healing-magic-battle --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/healing-battle
dotnet run --project src/Fft.Client -c Release -- --magic-battle --content-root scratch/healing-battle
```

Use fresh scratch output. N, F, click Guard, Enter, W, N, W, N resolves Cure.
On later Mage turns, F then Tab selects Fire. Existing controls and the
`--check <fresh-directory>` option are described in the
[player guide](../development/fire-battle-controls.md).
