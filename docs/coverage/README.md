# SCUS-94221 feature/content coverage inventory (ZOD-045)

Start with the [readable entries](baseline-coverage.md). The authoritative
[JSON inventory](baseline-coverage.json) records stable coverage IDs, domain,
profile scope, status, responsible ticket/component, current capability, pinned
source entry points, implementation/evidence links, automated checks, manual
checkpoints and unresolved gaps. The backlog remains the work queue; this inventory
provides an acceptance map for its larger epics.

This first pass covers **58 planning entries in nine domains**, not 58 original
records or a measured completion percentage. Each entry is a family that may need
a deeper record, encounter, screen or resource subcatalog. No complete campaign
graph, exact original content count or exhaustive asset-conversion inventory is
claimed. Required gaps block original-game completeness even when a foundation
ticket for a bounded proof is marked complete.

## Evidence and scope

Snapshot: 2026-10-23, managed base `7990b5d` (ZOD-035 committed), read-only reference
`11464943bbdbd2a0af022d90cb7f10455cf911df` in `C:\repositories\fft_decomp`.
Source entry points identify where to investigate; only linked behavior notes with
their own callers/data/arithmetic traces establish a supported rule. Older notes
retain their historical reference SHAs; this inventory does not silently revalidate
them against the current checkout. File paths and source comments alone do not
establish full semantics, reachability or an implemented feature.

The latest recorded .NET baseline is **954 passing tests** from ZOD-035. These
documentation tasks do not rerun or enlarge that suite. The inventory has its own
structural/link/source-path checks. Neither a source-derived test nor a matching
workstation capture establishes independent original-game equivalence. The BIOS
RNG proof has separate supplied-BIOS executable evidence; its result does not
establish all gameplay RNG ordering. ZOD-040 also identifies a separate event RNG.

Status vocabulary:

- `partial`: the stated subset exists; full row remains open. A research-only
  subset, such as ZOD-040's script map, is named explicitly and is not runtime code.
- `absent`: no supported complete runtime path for that feature family.
- `slice-proven`: only the stated bounded proof is validated; the full baseline
  requirement remains subject to the row's gaps.
- `validation-pending`: implementation/configuration exists, with named gates
  still open, such as remote CI and a clean-machine launch.
- `planned`: future enhancement or optional capability with no implementation claim.
- `eligibility-unresolved`: source presence needs an edition/reachability audit.

`ownerTicket` is the responsible backlog work item; `ownerArea` is its architectural
component. These fields do not assign a person or agent. Every row has a manual
checkpoint and a future action even when no automated scenario exists yet. Existing
test links cover only the stated subset. Missing independent observations remain
pending. Record exact commands, environment and result provenance when updating
evidence; do not upgrade status simply because a file or test exists.

## Domain index and important boundaries

- [Simulation](baseline-coverage.md#simulation): RNG, CT, movement, targeting,
  action families, statuses/reactions, original knockback/death, AI, progression
  and original objectives/rewards.
- [Catalog](baseline-coverage.md#catalog): jobs/abilities, equipment/effective stats,
  characters/monsters/ENTD, maps, text and campaign content tables.
- [Campaign](baseline-coverage.md#campaign): new game, mandatory story/flags, world
  travel/calendar/random encounters, deployment/return, optional recruitment,
  Deep Dungeon, propositions/discoveries, breeding/poaching and ending/epilogue.
  Optional examples have source anchors, but the complete event/route list is an
  unresolved child inventory, not an asserted count.
- [Screens](baseline-coverage.md#screens): opening/title, battle menus/previews,
  formation/jobs/equipment, shops/bars, records/tutorial/help, Save/Load and options.
  Sound-novel/music/test code has a separate eligibility row; US availability is
  not inferred from source presence or community-edition support.
- [Presentation](baseline-coverage.md#presentation): map/palettes/lighting,
  sprites/animation, effects/camera, fonts/portraits, audio and streamed movies,
  plus the Windows input/graphics adapter.
- [Assets](baseline-coverage.md#assets): exact-edition import, prepared manifests,
  selected converters and remaining format families/mixed streams. A disc file
  count is not evidence that all resources are decoded, required or playable.
- [Persistence](baseline-coverage.md#persistence): the completed synthetic battle
  save proof, full campaign state and Windows storage/recovery. Original logical
  save availability and required data remain compatibility questions; file storage
  and the player-facing Windows interface replace platform card operations.
- [Validation](baseline-coverage.md#validation): source/synthetic regressions,
  independent captures, pending Windows CI/clean-machine gates and engine/import
  boundaries. M2 remains open because ZOD-026/033 and ZOD-034 evidence is incomplete.
- [Profiles](baseline-coverage.md#profiles): content isolation, the later exact
  Lion War target, native presentation enhancements, custom knockback, authoring
  and optional original-card interoperability. These rows are deliberately marked
  outside the original baseline or as platform replacements, not counted as vanilla
  gameplay coverage.

Outside the original baseline does not mean optional to the whole project:
required Windows interaction is COV-PRE-007/ZOD-061, while aspect/resolution/art
profiles coordinate with it. Campaign-save tracing must also establish which RNG
streams are saved, reconstructed or reset on load; the synthetic battle save proof
does not decide original campaign load behavior.

Original mode remains the target for observable rules, quirks and reachable content.
Zodiac owns proven reusable facilities. FFT owns its rules, script dialects,
campaign state, definitions and persistence contracts. Keep original assets,
decoded data, generated disc inventories and captures in ignored `scratch/`.
This planning metadata contains no game-media bytes, dialogue or original scripts.

## First concrete follow-ups

The parallel [ZOD-040 investigation](../architecture/zod-040-event-script-investigation.md)
selects a synthetic conditional event and separates offline decoding from semantic
execution. Its proposed scopes are now ZOD-064 (decoder), ZOD-065 (pure execution),
and ZOD-066 (real event activation/completion trace). Full EventEnd lifecycle,
concurrent tasks, text, presentation waits and event RNG remain unsupported.

ZOD-067 inventories original logical campaign-save fields/lifetimes and legal
checkpoints before expanding DTOs or building the Windows Save/Load UI. This can
proceed independently of the decoder. Original card import remains optional.
The first editor, ZOD-047, also has completed named prerequisites; its scoped
authoring workflow can be planned separately from campaign execution.

These children do not exhaust the broad combat/movement/content/UI/audio epics.
Use the remaining-gaps field to select and trace one family at a time; do not
translate entire reference modules from this index.

## Update and validate

Use Python 3.11+ with only its standard library from the repository root:

```powershell
python tools/verification/verify_baseline_coverage.py
python tools/verification/verify_baseline_coverage.py --domain persistence
python tools/verification/verify_baseline_coverage.py --status validation-pending
python tools/verification/verify_baseline_coverage.py --reference-root C:/repositories/fft_decomp
```

The optional reference check only reads the checkout and verifies its exact HEAD
and every source path. The ordinary check needs no C checkout, original media,
graphics, network, package installation or C toolchain. It validates unique IDs,
domains/scopes/statuses, backlog references, local evidence/code/test paths, explicit
gaps and checkpoints, and the current readable view. It does not execute linked
tests or certify that a cited file proves a behavior.

When evidence or scope changes, edit the authoritative JSON, preserve existing IDs,
update the date/revisions deliberately and regenerate the readable view:

```powershell
python tools/verification/verify_baseline_coverage.py --write-view
python tools/verification/verify_baseline_coverage.py --reference-root C:/repositories/fft_decomp
```

Keep new original-derived subcatalogs local until their safe commit scope is
explicit. Add bounded child tickets to the backlog and link them from affected
entries. Rechecking a newer decomp HEAD requires reviewing affected claims, not
merely replacing the root SHA or automatically rewriting expected behavior.

Validation on 2026-10-23: all 58 entries, nine domains, backlog/local file links,
the generated view and pinned reference HEAD/source paths passed. Nine negative
checks rejected duplicate IDs/JSON fields, unknown tickets/statuses, missing or
unsafe implementation paths, missing manual checkpoints/gaps and a missing major
domain. The ZOD-040 source-derived synthetic event check also passed. No .NET
suite or original-game observation was executed for this documentation milestone.
