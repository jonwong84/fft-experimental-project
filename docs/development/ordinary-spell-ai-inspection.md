# Ordinary spell AI inspection

The read-only inspector loads the same hash-checked prepared battle and metadata
as the player host. Prepare Fire or the elemental profile with the existing
[preparation instructions](fire-battle-controls.md), then run:

```powershell
dotnet run --project src/Zodiac.AssetTool.Cli -c Release -- inspect-magic-ai --content-root scratch/fire-battle
```

It needs prepared files only. It does not read the original executable, BIN,
reference checkout or C code. It can run without a repository checkout when
launched from a published tool with a prepared content directory.

The JSON report identifies each known primary source slot, spell ID, private
HP/MP results, strike frames, absolute score terms and incoming/outgoing RNG.
Every case starts from the same private initial turn and decision stream. For
the checked Fire-only scene, Fire deals54, leaves actor MP69 and scores49; the
all-elemental scene's Fire scores78 because twelve initialized MP abilities
change the MP quarter share. Bolt2's cost10 differs from Fire2's12. Damage
consumes four AI evasion draws and no execution-only proc draw.

These are initial-aim/ready-hit hypotheses on current facts. The inspector does
not forecast charge ticks, movement, other unit turns or changing target positions,
enumerate every aim/origin, rank whole candidates, run the source lottery or issue
a command. An individual score should not be read as the AI's next decision.
No reward/growth/learning pass or live-state adoption occurs.

The bridge supports generic primary-only learned slots. Legacy schema1, unknown
static AI flags, special learned-row aliases and unrepresented secondary loadouts
fail closed. In particular the current Cure/Fire player's secondary grant lacks
the separate source learned-row/loadout capture needed by this AI slice; that
inspector rejection does not affect player Cure casting. Authored skillset IDs
need an explicit numeric compatibility mapping when using the library API.

[Behavior, source boundary and validation](../behavior/ordinary-spell-ai.md)
describe the supported flags, integer arithmetic and remaining charge-clock work.
