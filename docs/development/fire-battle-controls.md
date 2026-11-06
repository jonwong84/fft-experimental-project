# Playable Fire encounter

ZOD-387–391 connects one original ordinary spell end to end. The runtime owns
target choice, stored charge, current-input resolution, HP/MP/status/RNG,
EXP/JP/raw growth/unlocks and command replay. Original Fire and selected job /
progression data are prepared offline; the 7x5 map, units, careers, learned grant,
seed and first-KO objective are authored demonstration setup.

## Prepare and launch

From the repository root, using the already-extracted verified local executable:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- prepare-fire-battle --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/fire-battle
dotnet run --project src/Fft.Client -c Release -- --fire-battle --content-root scratch/fire-battle
```

Choose a fresh preparation directory. It contains strict prepared JSON, a SHA-256
asset manifest and offline import provenance. All stay ignored under scratch.
The running client needs only the manifest and prepared content; it never opens
the executable, BIN or C reference checkout.

## Controls

- **N / Next event:** advance to a ready turn or spell. Time passes only here.
- **F / Magic:** on Mage's turn, open learned spells and show blue selection range.
- **Tab / Down / Up, or click a spell row:** select a learned spell in prepared
  slot order. Its MP cost and charge time are displayed. Changing spell clears aim.
- **Click a blue tile:** aim; orange tiles show the resolution area.
- **U / Unit or tile:** follow the chosen unit or retain the selected tile.
  An empty tile always retains its position.
- **Enter / Confirm:** queue the cast. MP is paid once when it resolves.
- **W / Wait:** finish the active unit's turn. Control both units locally;
  Guard has no learned spells and can move or wait.
- **M / Step:** select and confirm one free adjacent tile, consuming Move.
  This demo does not activate full movement/facing commands.
- **Backspace / Cancel:** cancel selection; **R / Restart:** reset the encounter.
  **Esc:** cancel an open selection, or close while browsing.

For the initial cast: N, F, click Guard, Enter, W, N, W, N. Mage queues at tick10
without paying MP; Guard takes its turn at tick13 before the last charge tick.
Fire then resolves at tick13: Guard HP200→146, Mage MP75→69, Wizard JP50→62 /
total100→112, EXP95→0 and level10→11. Five growth draws refresh HP/MP maxima.
The starting seed57 produces 12 gameplay draws through this first resolution.
Continuing to cast Fire on Guard finishes the authored encounter at its first KO.

## Elemental tiers

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- prepare-elemental-magic-battle --exe scratch/all-data-extraction/SCUS_942.21 --output scratch/elemental-battle
dotnet run --project src/Fft.Client -c Release -- --magic-battle --content-root scratch/elemental-battle
```

This separate authored setup grants Fire/Bolt/Ice tiers1–4 through the same shared
formula and commands. Each spell retains its own power/cost/charge/geometry,
including Bolt2's10 MP cost and tier4's radius2 caster exclusion. Its starting
Wizard career is total5000/current260 after the selected grants, so a first Fire
cast earns26 JP rather than the Fire-only setup's12. The map and first-KO objective
are unchanged. [Source and execution](../behavior/elemental-magic-execution.md).

## Validation and limits

```powershell
dotnet run --project src/Fft.Client -c Release -- --fire-battle --content-root scratch/fire-battle --check scratch/fire-native-check
```

Use a fresh check directory. The finite Windows check captures start, turn, aim,
charge, other-unit turn, resolution, Critical and KO screens. It exercises the
same key and mouse-coordinate routes, including a resized letterboxed window,
out-of-range/turn/closed-encounter rejection, restart and four successful casts.
Complete gameplay receipts match a fresh headless command replay at resolution
and KO; rendering preserves state. `check.json` records adapter/runtime and
receipt hashes. These are programmed UI routes, not physical input injection.

The first local GPU run passed and its aim/resolution/KO captures were inspected.
Updated native checks pass both packages; the larger list visits all12 spells
through keys and first/last rows through resized pointer input. All12 actual cast
effects are covered by managed command tests. The updated list/results were inspected.
Original artwork, spell animations, audio and original encounter comparison are
not supplied by this view. This is a human bare-equipment/no-RSM ordinary profile;
general magic AI, full statuses, reactions, corpse/crystal scheduling and frame
RNG equivalence remain open. The first-KO objective is explicitly invented.
See [behavior and source evidence](../behavior/ordinary-magic-execution.md).
