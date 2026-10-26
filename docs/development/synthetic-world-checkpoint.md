# ZOD-068: Synthetic idle-world Save/Load

Follow-up (2026-10-26): [ZOD-069](world-save-load-ui.md) now exposes this service
through native slot screens. The owner approved the gallery; interactive slot
review remains pending. The
headless validation and limits below retain their historical scope.

Entry: 2026-10-26, managed `755dd37936ea6d81dc886771bb6819687d88d0f5`.
The read-only reference is clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The existing
[ZOD-067 field/lifetime investigation](../architecture/zod-067-campaign-save-state.md)
provides context. This milestone translates no additional original rule or card
layout. All definitions, travel costs, limits, fixtures and continuation behavior
below are invented. No original campaign, random encounter or suspended battle
is implemented.

## Selected scope before implementation

Framework owns immutable idle-world state: one supported continuation route,
location, complete authored party IDs with job/equipment, unequipped inventory,
funds, a month/day, one named progress flag and explicit elapsed seconds. Small
immutable definitions bind the supported jobs/equippable items/locations and
ownership limits. Reject unknown/duplicate IDs, invalid equipment and combined
carried/equipped ownership limits before publishing state. This proof does not
replace the existing full job/item schema work.

The one synthetic command travels between two authored locations, spends seven
funds and adds eleven explicit elapsed seconds, preserving date/party/progress.
It consumes no RNG and reads no clock. Reject same/unknown destinations,
insufficient funds and elapsed overflow with the original state and no events.
The synthetic profile permits up to 64 party members, 256 item stacks, one million
funds, a 12-by-30 calendar and ten years of elapsed seconds. These are declared
proof budgets, not original-game limits.

Content owns a strict 64-KiB UTF-8 JSON envelope, SHA-256 over canonical typed
snapshot data, explicit profile/content versions, idle checkpoint and unsupported
RNG policy. Schema 2 stores checkpoint/RNG-policy identities; invented schema 1
implicitly used these two known values. Validate the predecessor checksum before
migration, bind freshly composed definitions, and compare derived slot summary
against the saved summary. Unknown fields/versions, bad IDs, mismatched route/
profile/summary and malformed/oversized input fail before live publication.

The application service uses ten named synthetic slots (`slot-01`..`slot-10`).
Default Windows storage is `%LOCALAPPDATA%/FFTReimplementation/Saves/SyntheticWorld`;
tests/developer checks pass explicit isolated directories. This milestone does
not write to the actual user directory during validation. Slot names never come
from JSON. Catalogs validate content before exposing summaries and also report
backup identity/diagnostics; files cannot select profiles or arbitrary paths.

Every cooperating writer/load/catalog uses a per-slot exclusive lock file.
Lock files remain in place to avoid splitting lock identity during contention.
Writers compare the caller's expected raw-file SHA-256 under that lock; null
means expect absence. A changed file or busy lock rejects rather than overwrites.
This coordinates this service's writers, not arbitrary external programs editing
the directory or deleting lock files.

The revision identifies raw file content; an identical-content rewrite retains
the same token. One application owner serializes live-session commands; separate
service instances coordinate through the slot lock. File flush/promotion checks
do not establish behavior under power loss or on every remote filesystem.

Serialize first, write/flush a unique sibling, then atomically create or replace
the primary. Replacement retains the previous validated primary as `.bak`.
Invalid primary content cannot be silently overwritten. A small internal file
publisher enables create/flush/promotion fault tests without selecting a public
filesystem interface. Reject existing reparse paths; this is not an adversarial
filesystem sandbox.

Load constructs a validated candidate before changing the live session. Cancel,
stale selection, validation/access failure and failed saves preserve the session.
Recovery explicitly selects a validated backup and never silently rewrites the
damaged primary; save recovered state to another slot if necessary. No fallback
to an unvalidated backup. Native slot navigation remains ZOD-069 after minimal
menu flow; existing battle saves and all introduction/rendering code stay intact.

## Validation plan

Commit invented schema-1/2 fixtures. Test every supported state field and the next
travel result/events against fresh definitions; invalid definitions/state,
duplicate and cross-field references, route/checkpoint/RNG/summary mismatch,
limits, legacy migration, future/unknown/duplicate/null/malformed/oversized JSON
and checksum errors. Test slot path rejection, catalog/backup metadata, stale
and concurrent writers, canceled/failed loads, successful explicit recovery and
invalid backups. Inject serialization/create/flush/promotion/access failures and
exercise real Windows sharing failure; old bytes and live state must survive.
Run locked restore, Release build, managed regressions and a finite headless
save/continue/catalog/recovery CLI check. No graphics process, emulator, original
asset, user-directory write or manual visual approval is required for this proof.

## Executed validation — 2026-10-26

Locked restore and Release build pass with zero warnings/errors. **2,267 .NET
tests pass** (94 new): Engine 24, Framework 830, Content 644, AssetTool 202,
CLI 211 and Client 356. All **281 Python tests pass** (3 new).

The new cases exercise full state/next-event continuation, independently authored
schema-1/2 fixtures, required/unknown/duplicate/null fields, canonical ordering,
cross-field/equipment/ownership and summary validation, version/checksum/size
limits, safe slots, stale revisions, competing writers, busy locks, cancellation,
corrupt/oversized/wrong-profile files and explicit backup recovery. Publishing
faults cover serialization, create/access, flush and promotion; the actual Windows
delete-sharing failure also preserves the previous file. Temp-file cleanup is
checked. No real user save directory is created by these checks.

Two finite headless CLI runs produce **14 identical files each**. The independent
Python audit binds both fixtures, validates checksums/summaries and confirms the
next Harbor-to-Hill result (funds 93, elapsed 53), preserved corrupt primary,
validated backup and recovery to a new slot. The ten empty coordination lock
files are intentional. There is no new graphics process or visual approval gate.
[Executed metadata](../evidence/saves/synthetic-world-checkpoint-v1.json) binds
sources, fixtures, TRX files, logs and local check outputs.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-save-check scratch/world-save-example-a
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --world-save-check scratch/world-save-example-b
python tools/verification/verify_world_checkpoint.py --first scratch/world-save-example-a --repeat scratch/world-save-example-b --fixtures docs/fixtures/saves --output scratch/world-save-example-audit.json
```

Each check needs a fresh directory. A failed check may retain diagnostic output;
do not reuse it or point the check at real save storage. Application code uses
`WorldCheckpointService` with a caller-selected profile/store: inspect validated
slot metadata, pass its revision when saving/loading, and set `discardConfirmed`
only after the application's decision. Set `backup: true` explicitly to recover.
The service never supplies overwrite/discard confirmation through a UI itself.

Original checkpoints, party/stat/catalog breadth, campaign routes, live/seeded RNG
continuation, event/battle suspension, original-card import and native menu flow
remain unsupported. ZOD-069 still waits for minimal navigation under ZOD-042.
This completes the synthetic proof, not ZOD-041/053 or original campaign fidelity.
