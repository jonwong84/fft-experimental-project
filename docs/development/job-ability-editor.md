# ZOD-047 — First job/ability editor

**Current revision:** The owner found this first UI rough and requested separate
job/ability workspaces using FFTPatcher as a workflow reference. It was not
approved. Use the [workspace revision and current launch/review guide](editor-workspaces.md).
The contract, runs and gallery below are retained as historical first-implementation
evidence; they do not describe the revised screen layout.

Selected 2026-10-26 at managed `673bd62`. The reference checkout was read-only,
clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`. This milestone translates no
new original rule or record field. ZOD-014/016/017/018 retain their historical
source provenance and supported metadata/base-job movement limits.

## Host decision and bounded contract

Use a separate `Fft.Editor` Windows Forms executable, with no new NuGet UI
dependency. Native text controls, tab navigation, file dialogs, scrollbars and
field validation suit a small desktop metadata tool. Reusing the game's
MonoGame host would require rebuilding these editing facilities. A web host
would introduce an additional runtime and packaging path. Windows Forms is
confined to the editor; platform-neutral authoring logic lives beside the
existing content APIs in `Zodiac.Content.Authoring`. Headless simulation and
the game client retain their existing dependencies and behavior. The editor
cross-compiles with `EnableWindowsTargeting`; it executes only on Windows.

Open a prepared baseline metadata JSON package; imported original packages stay
under ignored `scratch/`, with their existing importer provenance sidecar. Show
the source path, package/profile, SHA-256 and read-only identity. Search job IDs,
select a job, inspect its skillset/ordered slots/innates and linked abilities.
Source learning flags and opaque bytes remain inspectable and preserved.

Create one explicit authored add-on with a distinct namespace. Copy the selected
job, primary skillset and referenced ability closure, remapping their namespaces
and retaining ordered/null/repeated references. Edit Base Move, Base Jump and
each copied ability's JP cost. No metadata edit executes an ability or derives
equipment/status/growth stats. Validate through `ContentPackage.ReadJson` and
explicit baseline/add-on composition; the editor adds no alternative rule
validator. Invalid text remains in the draft and produces a field-specific error.

Export uses existing deterministic `ContentPackage.Save` after validation and
composition. Refuse the source file and any reparse-point destination path;
never export into the source directory. Reopening an authored add-on is explicit
and validates its supported shape against the selected source job. Failed open,
copy, validation or export preserves the current draft and previous output.
Creating/opening another draft or closing with unsaved edits offers Cancel first,
then explicit discard; export is a separate action. The source is never writable
through this tool. No autosave, package overrides or arbitrary C# loading.

A fixed synthetic flat 9x9 movement preview creates fresh baseline/authored
setups through `BaseJobBattleSetup`, probes commands through `BattleSimulator`,
and shows both reachable sets. Base Jump is copied but has no effect on this flat
preview. Stepping-stone jobs show an explicit unsupported-preview message. The
preview is separate from campaign state and never mutates existing battle state.

## Validation plan recorded before native execution

Add platform-neutral regressions for copy/remapping, flags and ordered references,
source isolation, search, invalid/range input, reload/composition, dirty lifecycle,
deterministic export, source/directory/reparse protection, locked-output failure,
and exact movement results before/after reload. Run locked restore, zero-warning
Release builds, all .NET/Python tests and the baseline inventory checker.

Use exactly two finite editor check processes in fresh scratch directories,
maximum 30 seconds each. Drive real controls and shared form handlers, capture
native control renderings with `DrawToBitmap`, and record the action/state trace.
Check search/select, copying, invalid text/field error, successful validation,
Move/Jump/JP edits, preview, export/reopen, failed export, unsaved-close cancel
and discard, resize and closure. Repeat JSON/trace/image bytes must match in the
same environment. Record the input package's hash and verify it remains intact.
These programmed checks are separate from physical keyboard/mouse and owner
visual/manual review. No game GPU launch is required. Revise the plan explicitly
before exceeding two native launches; keep failed artifacts.

Plan revision before further launches: the first successful run
`scratch/editor-047-a` exposed a wrapped Move label in its native rendering.
Retain that discovery run. Widen the numeric-field columns and reserve space for
error icons, then allow exactly two final repeated runs (`editor-047-b/c`), for
three total processes, still at most 30 seconds each. No additional launches are
authorized by this revision. The first run's behavior passed; its layout is not
the final visual evidence.

Original metadata and generated captures/exports remain under `scratch/`.
Synthetic tests require no original media. Replacement contracts, renderer,
aspect/CRT/settings/audio, quirk fixes and broad ZOD-048 authoring remain deferred.

## Launch and owner review

From the repository, launch with the retained local imported Squire package:

```powershell
dotnet run --project src/Fft.Editor -c Release --no-build --no-restore -- --source scratch/imported-squire-check-a/content.package.json
```

Without `--source`, use **Open baseline** to select a prepared metadata package.
For a media-free example, select `docs/fixtures/content/metadata-synthetic.json`.
The editor needs the .NET 10 Windows Desktop runtime (included with the Windows
SDK used here). It has no game/MonoGame, BIN, C-toolchain or emulator dependency.

1. Inspect the source job and ability selector. Search by ID and clear the search.
   Source values and provenance stay read-only. Scroll source slots if necessary.
2. Enter a distinct package namespace such as `myjobs:package/squire-copy`, then
   **Create authored copy**. The package text is an input to creating a new copy;
   editable draft fields are Move, Jump and the selected copied ability's JP.
3. Enter invalid Move text and **Validate**; check the field error, then fix it.
   Change Move to 2, Jump to 4 and a copied ability's JP. **Preview movement**
   compares source and authored ranges; Jump is copied but does not affect this
   flat preview. Learning metadata does not implement combat effects.
4. **Export add-on** into a new authored directory under `scratch/`, outside the
   source directory. Export creates/replaces a file after native overwrite
   confirmation. **Reopen add-on** with the matching source job selected; verify
   values, abilities and preview. Reopen supports this editor's copied graph,
   rather than arbitrary package edits. Exporting into the source tree rejects.
5. Make an edit, close, and choose **Cancel** (also the default Enter/Escape
   action); edits should remain. Export or explicitly discard to close. Try tab
   navigation, text selection/copy/paste, mouse selection, resize/scrolling and
   both file dialogs. Canceling dialogs must leave the draft intact.

Owner visual and interactive approval are pending. The gallery is
`scratch/editor-047-review/review.html`. Controller navigation is not selected
for this desktop authoring tool; the game client's device workflow is unchanged.

## Executed validation — 2026-10-26

Locked restore and Release solution build pass with zero warnings/errors. All
**2,346 .NET tests** pass: Engine 24, Framework 830, Content 673, AssetTool 202,
CLI 211 and Client 406. The **29 new Content tests** cover source/draft isolation,
complete reference closure, exact flags/opaque bytes, invalid and boundary values,
dirty reversion, strict supported-shape reopen, deterministic exports, real
Windows locked-file failure, source-directory and junction rejection, and exact
movement sets before/after reload. All **291 Python tests** pass (4 new).

Three native processes ran under the explicitly revised budget: one retained
successful discovery run and two final runs after the label correction. Each
final run captures nine native control images and completes thirteen behavior
checks, including real Cancel-first modal cancel/discard and a locked-output
failure. All eleven final image/JSON files repeat exactly. An independent audit
reconstructs the copy from the input package, retaining all ordered/null/repeated
references, learning flags and opaque bytes except supported explicit edits.
The input package hash stays
`62d7a4c41e163f6f5d7514f3ef900cc51ac9724afd26255ac665f2f8d7438435`.
The saved copy has Move2/Jump4/first-ability JP123; the final unsaved draft has
Move1, and failed export preserves the earlier file. Native screenshots use
`DrawToBitmap`, not screen scanout or GPU comparison. The agent inspected final
preview, error, resized and Cancel-first images. Native file dialogs, physical
input/accessibility, mixed-monitor DPI, clean-machine launch and remote Linux
cross-compilation/CI remain unobserved. No original-game equivalence is claimed.

The first Python attempt used a system interpreter without Pillow and failed
imports. The final full suite uses the bundled runtime with existing dependencies;
no dependency was installed or changed. The initial sandboxed .NET build could
not access the installed compiler normally; unsandboxed builds supplied the
recorded results. Earlier transient failures remain in scratch logs.

[Executed metadata](../evidence/authoring/job-ability-editor-v1.json) records
source identities, native budgets, artifacts, test logs and outstanding review.
This is an implemented review candidate, not a completed M3/manual approval.
