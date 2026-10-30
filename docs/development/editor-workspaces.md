# ZOD-047 — Job and ability workspace revision

**Owner direction after implementation (2026-10-26):** Further UI polish and
owner review are deferred until a concrete gameplay/content authoring need or
owner request. ZOD-047 remains unapproved and M3 remains open; this review does
not block unrelated gameplay development. The [live FFTPatcher reference](fftpatcher-desktop-reference.md)
records the corrected local v0.497.0/original-PSX session. The implementation and
review instructions below are retained for that later review.

Selected after owner review of `5c9ff93704d037c52651132e754ad183f6ea116e`.
The owner described the first UI as rough and requested separate job and ability
editing, considering FFTPatcher as a workflow reference. This is feedback requiring
revision, not approval of the first gallery. ZOD-047/M3 remain open.

## Workflow reference and boundary

Inspected Glain/FFTPatcher at
`26d45cc8c019194625f078398e0c6ef6b09c1a78` through its GitHub connector:

- [readme.txt](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/readme.txt)
  describes distinct Jobs, Abilities and Skill Sets pages.
- [FFTPatchEditor.Designer.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/Editors/FFTPatchEditor.Designer.cs)
  confirms separate top-level category tabs (blob
  `01af90cffa40bb32f1ed5da6ddb32ff4bbacbafe`).
- [AllJobsEditor.Designer.cs](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/Editors/Job/AllJobsEditor.Designer.cs)
  confirms a left record list and a fill-docked job inspector (blob
  `fcaefa5ff318dc8f97205c5a5837707aa8eb1039`).
- [COPYING](https://github.com/Glain/FFTPatcher/blob/26d45cc8c019194625f078398e0c6ef6b09c1a78/COPYING)
  identifies GPLv3; reviewed designer headers state GPLv3-or-later. This revision
  uses these general workflow observations. No FFTPatcher code, designer layout,
  resources, labels/catalogs or assets are copied or linked. No interoperability
  or patch-file/ISO support is inferred.

Keep the existing Windows Forms host, schemas, validators, source protection and
single-job copied package. This is a presentation/workflow correction, not the
broad ZOD-048 editor or a new ability-only package contract. It translates no new
original rule/record field; ZOD-017/018 retain their pinned historical provenance.

## Selected revision

- Separate **Jobs** and **Abilities** workspaces, each with its own searchable
  record list and selection. Switching pages/filters must preserve invalid and
  unsaved edits. Original/source and authored/copy are an explicit shared view.
- Jobs shows only job movement properties, source comparison and a structured
  skillset/innate reference list. Opening a referenced ability navigates to its
  record in Abilities. A separate job preview page holds the movement proof.
- Abilities shows its own catalog/record properties, JP editing and read-only
  learning metadata. Original view lists the supplied source catalog; authored
  view lists this copied package's closure. No ability effect editor is implied.
- Shared Open/Create copy/Reopen/Validate/Export actions; package creation uses a
  focused dialog. Provenance/hash/technical details are available on demand.
  Friendly labels derive from stable IDs, with numeric fallbacks for unnamed
  records; no untranslated game-name catalog is invented.
- Validation selects the offending workspace/record and field, clearing only
  the relevant filter. Short readable feedback remains in the main window;
  full package/path diagnostics are available through Details.

## Validation plan before native execution

Preserve prior native artifacts and historical metadata. Add portable regressions
for independent source/copied ability browsing and retained invalid edits. Add
native programmed checks for tab separation, independent filters/selections,
source read-only fields, reference navigation, invalid Job/Ability error routing,
retained edits across views/tabs, copy/export/reopen and existing failure/close
behavior. Run locked restore, zero-warning build and full .NET/Python suites.

Allow exactly two new native check processes, each at most 30 seconds, in fresh
`scratch/editor-workspaces-047-a/b`. Capture Jobs, Abilities, source/authoring,
reference navigation, both field errors, preview, package details, resized
layouts and Cancel-first unsaved decisions. Independently audit the copy and
repeat JSON/images. Revise this plan explicitly before any additional launch;
retain failures. Native control renderings/programmed actions remain separate
from owner visual/manual keyboard/mouse/dialog approval. All original metadata,
exports and generated media stay ignored under scratch. Deferred work stays
deferred.

Plan revision before further native launches: the successful first run in
`editor-workspaces-047-a` verified behavior but exposed overly wide numeric
fields and diagnostic-style learning flags. Retain it as discovery evidence.
Use fixed-width numeric controls, labelled search fields and named read-only
flag controls, then allow exactly two final repeated runs in
`editor-workspaces-047-b/c` (three total processes, at most 30 seconds each).
No further launch is selected by this revision.

## Launch and interactive review

The owner still had the first editor open. A normal build encountered its locked
DLLs; that process and its unsaved state were preserved. The validated revision
uses a separate .NET artifacts folder. From the repository, launch this revision:

```powershell
dotnet scratch/editor-workspaces-047-artifacts/bin/Fft.Editor/release/Fft.Editor.dll --source scratch/imported-squire-check-a/content.package.json
```

To reproduce that isolated build on Windows:

```powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/editor-workspaces-047-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/editor-workspaces-047-artifacts
```

Normal output commands work after the old editor is closed and rebuilt. Never
terminate a user's open editor to release DLL locks or discard their draft.

Review `scratch/editor-workspaces-047-review/review.html`, then:

1. Browse Jobs and Abilities independently. Each has its own search/list; source
   values are read-only. Package details exposes paths, hashes and raw metadata.
2. On Jobs, select Squire and **Create copy…**; enter a distinct namespace in the
   dialog. Authored copy becomes the current view. Change Move/Jump in Jobs.
3. Select a nonempty related-ability row and **Open selected ability** (or double
   click it). Abilities should open at that copied record. Change its JP cost.
4. Switch pages and Original/Authored views; unsaved job/ability values should be
   retained and original values unchanged. Filters are independent. Invalid Move
   or JP validation should select the appropriate workspace/record/field.
5. Jobs → Movement preview → Update preview compares source/copy movement.
   Export/reopen uses the existing package rules. Previously exported supported
   copies remain compatible. Pick an authored scratch directory outside source.
6. Try keyboard/tab, mouse/scroll/resize, cancel both file dialogs, and cancel an
   unsaved close before deliberately exporting or discarding. Both workspaces
   share one draft/export. Creating an ability-only package is not implemented.

The first UI is not approved; the owner requested this revision. New owner visual
and interactive approval remain pending. No approval is inferred from automated
checks or the FFTPatcher workflow study.

## Executed validation — 2026-10-26

Locked isolated restore and final Release solution build pass with zero warnings
and errors. All **2,349 .NET tests** pass (Content 676, Framework 830, Engine 24,
AssetTool 202, CLI 211, Client 406). Three new portable cases cover the complete
source versus copied ability lists, retained invalid Job/Ability text during
browsing, and independent edits across multiple copied abilities. All **293
Python tests** pass, including two new negative route/preservation audit cases.

Under the explicitly revised plan, three native processes succeeded: one retained
discovery run and two final runs with the numeric/flag presentation correction.
Each final run executes **22 checks** and captures **16 native control images**;
all **18 image/JSON files** repeat exactly. Native checks cover read-only source
catalogs, separated pages, both validation routes, filters and view switching,
reference navigation, preview, package details, export/reopen, real locked-output
failure, resized Jobs/Abilities and Cancel-first close/cancel/discard. Independent
source/copy comparison retains opaque bytes and ordered references, and verifies
saved Move2/Jump4/first JP123 versus final unsaved Move1. The source package hash
remains `62d7a4c41e163f6f5d7514f3ef900cc51ac9724afd26255ac665f2f8d7438435`.
Agent inspected final Jobs, Abilities, error and resized captures.

These are native `DrawToBitmap` renderings and programmed controls, not physical
input, desktop scanout or original-game equivalence. No game-client, Engine or
Framework production, content/save format or package-lock change. Previous
ZOD-047 artifacts and metadata stay historical; the new
[executed metadata](../evidence/authoring/editor-workspaces-v1.json) records this
revision. Remote CI/Linux cross-compilation, clean-machine/DPI/accessibility and
owner manual checks remain open. Deferred work remains deferred.
