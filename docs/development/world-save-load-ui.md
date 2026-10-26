# ZOD-069 — Authored Windows Save/Load slots

Selected 2026-10-26 at managed `d737098a633b76a7d5bf39a7be58bab4939edc44`.
ZOD-068's [checkpoint service](synthetic-world-checkpoint.md) and the completed
[ZOD-103 host](windows-world-navigation.md) remain the persistence/input/render
boundaries. Original save/checkpoint source context remains the pinned ZOD-067
investigation; no additional original rule, card layout or asset is translated.

## Contract selected before coding

Enable slots in normal `--world-menu`; retain the closed navigation-only profile
for historical `--world-menu-check` and its regressions. Add finite
`--world-slots-check <new-directory>` for the combined workflow. Home offers
Load; World offers Save/Load. Save is available only for the authored idle-world
checkpoint, independently of original-game save availability.

Two pages show five of the ten whitelisted slots, previous/next, Refresh and
Back. Details expose validated primary/backup status, summaries and source
schema, with Save, Load, Recover backup, Refresh and Back. Empty, invalid and
unavailable files have explicit status. Unsupported/corrupt primaries cannot be
overwritten, and only validated primaries/backups enable their load action.
UI views contain no arbitrary paths or unvalidated summaries.

Catalog refresh occurs only on explicit commands/entry, never during rendering
or view access. Selecting a row freezes its catalog record and expected revision.
Creating an empty slot is an explicit Save action; overwriting a valid primary
requires a Cancel-first confirmation. Loading over a live session requires a
Cancel-first discard confirmation. Backup recovery always needs its own
Cancel-first confirmation and leaves damaged primary bytes untouched. Save
recovered state to a different empty slot. A successful operation offers Continue
to World or Back to details; failed operations preserve live state/files and
offer Back/Refresh without silently retrying or repairing. Refresh is required
after an operation failure before retrying. Parent actions/close cannot bypass
a modal. Selected revision checks and all validation/publication remain in the
existing service; UI does not duplicate the codec or campaign rules.

## Validation plan before native execution

Headless regressions cover both pages, availability, parent focus, empty/save,
overwrite/cancel, load/discard/cancel, fresh-session restore and next travel
state/events, invalid/incompatible/unavailable metadata, stale file replacement,
missing files, real busy storage locks and explicit backup recovery/new-slot
save. Rendering and repeated view access must not write or refresh files.

After locked restore, Release build and managed tests, use exactly **two** new
native check processes (normal/repeat), each at most **1024 draws / 90 seconds**.
Both execute the same UI script in independent live/replay scratch directories:
page forward/back; save Harbor; travel; cancel/accept overwrite; travel again;
cancel/accept load; stale overwrite error/refresh; corrupt-primary details;
cancel/accept recovery; save recovered state to slot 2; missing-file load error;
busy-lock save error/refresh; resize and close. Test mutations affect only new
check directories and run identically for live/replay. Capture slot pages,
details, all confirmation kinds, success, invalid/error/recovery and resized
views. Full target pixels must match CPU output; dispatched results/views must
match fresh headless application execution, excluding no hidden I/O differences.
PNG/UI and storage artifacts must repeat exactly; independently audit state,
events, backups and preserved damaged bytes. Retain failed outputs; explicitly
revise this plan before exceeding two graphics launches. No physical input
automation or actual user save-directory writes.

Owner visual and interactive Save/Load keyboard/mouse/controller/cancel/recovery
checks follow separately. ZOD-103's prior manual approval does not approve the
new slot screens. Original campaign/card support, broader storage durability,
display correction/widescreen/CRT and new menu-framework contracts remain open.

## Launch and owner review

```powershell
Set-Location C:\repositories\fft-experimental-project
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-menu
```

Normal mode now offers Load at Home, and Save/Load at World. Explicit Save writes
the synthetic checkpoint under
`%LOCALAPPDATA%/FFTReimplementation/Saves/SyntheticWorld`; this location was not
written during automated validation. Use an empty slot for the first manual
check. Save, travel, then Load the slot: Cancel should keep the live session;
accept should restore the saved location/funds/time. A second Save to that slot
requires overwrite confirmation and retains the previous valid file as backup.
Recover backup has a separate Cancel-first confirmation and preserves the
primary file. Continue returns to World; Back permits reviewing details/pages.
Try keyboard/mouse/controller, held controls, both pages, resize and close/cancel
on the new screens. Error/corruption displays can be reviewed in the gallery;
no manual file corruption is needed.

The [local gallery](../../scratch/world-slots-069-review/review.html) and manual
slot workflow are pending owner review. Previous ZOD-103 navigation approval
remains valid for that earlier scope and does not approve these additions.

## Executed validation — 2026-10-26

Locked restore and zero-warning/error Release build pass. All **2,317 managed
tests** pass (17 new): Engine 24, Framework 830, Content 644, AssetTool 202,
CLI 211 and Client 406. All **287 Python tests** pass (3 new). New tests include
actual incompatible-profile bytes, predecessor migration without rewriting its
file, absent/root-file/busy storage, stale/missing loads, frozen metadata,
confirmation defaults/modal isolation, parent focus and full repeated script.

Exactly **two** new native checks completed, each in under six seconds with
**140 draws/samples and 22 captures**. Each executes six successful storage
operations, three rejected operations and two travel commands. All **44 GPU
target images** match CPU pixels (**27,033,600 pixels**); every action result,
event and view matches fresh application execution using separate replay files.
Actual backbuffers reach 1280x720 and 720x960; pointer routing and explicit
resource recreation preserve state. No additional graphics launches or failed
native attempts occurred. Existing-output and missing-argument probes reject
before window construction with exit codes 1/2 and preserve completed outputs.

The independent Python audit validates the full authored route, confirmation
defaults, stale refresh, invalid-primary/validated-backup metadata, complete live
checkpoint fields, two travel events, six successes/three failures, final Hill
funds 93/elapsed 53 and checksummed storage. Both live/replay stores retain the
damaged slot-1 primary, validated Hill backup and recovered slot-2 save; slot 3
never publishes and the scripted missing slot 4 remains absent. Each store has
ten empty coordination locks and three data files, with no temporary files.
All **51 UI/storage files** repeat exactly between native runs. The prior
ZOD-103 runs' **32 retained artifacts** remain unchanged; their source identities
remain historical and are not rewritten to describe this implementation.

The agent inspected the second page, corrupt-primary details and stale-error
PNGs for layout and legibility. Owner visual/interactive review remains pending.
Original UI/availability, full campaign persistence, PS1 cards, measured physical
timing, mixed-monitor DPI and forced device loss are not established. Engine,
Framework and Content production, save schemas, package locks and battle/intro
code are unchanged. One client test fixture copy is added; no package is added.

[Executed metadata](../evidence/navigation/world-save-load-ui-v1.json) binds
source, logs, TRX, current and retained local artifacts. The read-only reference
is freshly confirmed clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`;
the original source investigation remains ZOD-067's historical scope.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-slots-check scratch/slots-example-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-slots-check scratch/slots-example-b
python tools/verification/verify_world_slots.py --first scratch/slots-example-a --repeat scratch/slots-example-b --output scratch/slots-example-audit.json --review scratch/slots-example-review/review.html
```

All check/audit/review paths must be fresh. The check mutation adapter exists
only for the isolated developer plan; normal UI performs storage operations
solely through `WorldCheckpointService`. The navigation-only check remains its
historical profile, and normal interactive mode enables slots explicitly.
