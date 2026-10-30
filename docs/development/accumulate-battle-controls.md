# ZOD-107 — Accumulate in the Windows battle controls

## Scope and owner sequencing — 2026-10-26

The owner accepted proceeding with gameplay after clarifying battle saves.
ZOD-106 is deferred until a concrete debugging/checkpoint or explicitly selected
battle-suspension requirement justifies it. It is not a prerequisite for ZOD-107.
The original campaign checkpoint path remains separate. Accumulate's PA increase
is temporary battle state; restarting derives the starting PA. Campaign battle-end
transfer is not implemented here. Keep the existing ability-profile save guard.

Expose the validated ZOD-105 action in a separately prepared authored battle using
the existing Windows window/router, with direct self action, availability/current
PA and success/cap feedback. Preserve ordinary sword-only and introduction flows.
Keep editor, save formats, Engine and simulation rules unchanged.

## Native validation plan before execution

Prepare the synthetic metadata/effect companion and manifest twice into fresh
`scratch/accumulate-107-prepared-a/b`; compare all files and audit hashes. Use a
separate strict loader through the existing hash-verified prepared store, with
bounded UTF-8 parsing and shared definition/grant validation before window creation.
Negative loading and output preflight must create no native process/output.

Allow **exactly two** new ability-window check processes, each at most **30 seconds**,
with fresh `scratch/accumulate-107-native-a/b` outputs. No emulator or original
assets/reference executable are launched. A finite scripted keyboard/canvas-pointer
sequence uses the same interaction/router as physical input. One action per frame
keeps the repeated-use proof within the budget; cap reached through legal commands,
without changing live PA or injecting snapshots. Capture:

- Starting view and PA5; first readiness; mouse Accumulate to PA6.
- Rejected duplicate Act, inactive selection and an active unit without the grant.
- Move after Accumulate, later sword damage24, and unchanged resolution draw count.
- Repeated legal turns reaching PA99, usable at-cap action, consumed no-change Act.
- Mid-check resize with identical simulation; same routed pointer action afterward.
- First-KO outcome, rejected terminal action, Restart with PA5 and cleared journal.

Compare every attempted command's state/events/RNG with independent headless
execution and replay the session journal before Restart. Record per-step UI,
selection/mode/availability, state and events in diagnostic JSON; these are not
save files or a public persistent replay format. Repeated JSON/captures must match.
Drawing, resize and availability probes must preserve state/history/RNG. Inspect
the actual GPU captures and create an ignored review gallery. Preserve failed
runs; revise this plan explicitly before any additional native launch.

ACC-028..030 cover prepared launch, self-action routes and cap feedback. ACC-031
is revised by owner sequencing to compare in-memory replay/headless continuation;
its original save-continuation clause remains deferred with ZOD-106. Historical
ZOD-104 evidence is unchanged. **ACC-032 remains an owner visual/physical-input
gate**, separate from programmed native checks. Do not mark ZOD-107 complete until
the bounded owner review is approved.

Run locked restore, zero-warning Release build, full .NET/Python suites and the
coverage/link/evidence checks. Existing battle fixtures and UI routes retain their
regressions. No new original behavior or visual/timing equivalence is claimed.

## Implementation and validation result

Complete following owner approval (ACC-032), 2026-10-26: "Looks good. Let's keep
going." The owner accepted the bounded review after receiving the launch guide
and gallery. Individual physical-input steps were not separately reported or
agent-observed. Implementation tests and native captures were not repeated for
this approval; their limits below remain unchanged.
The reference was rechecked clean/read-only at
11464943bbdbd2a0af022d90cb7f10455cf911df before work; this slice translates no
additional original rule. It uses ZOD-105 resolution and the existing input router.
The prepared metadata/effects are invented definitions, with authored map, units,
sword and grants. They do not reconstruct an original encounter. No original
executable, BIN or C reference code is read by the running scene.

Preparation validates both packages through shared content/setup validation before
publishing an atomic three-file manifest bundle. Loading verifies hashes, bounded
strict UTF-8 and the closed profile before native creation. Existing output and
invalid input are rejected. Ordinary battle and introduction layouts retain their
existing behavior; C is a no-op without the selected ability.

Both planned native processes completed 560 programmed inputs in 576 frames each:
95 accepted uses (94 PA increases and one capped Act), four atomic rejections,
Move after Act, damage24 at PA6, legal turns to PA99, capped availability/feedback,
resize, first KO and restart. State/events match headless execution at every
command; the journal replays exactly. Both three-file preparations and all sixteen
native files match byte-for-byte. Drawing/probes preserve state/history/RNG.
All 15 actual GPU captures were inspected: feedback and action/card layout remain
readable, including rejection and terminal states. These are render-target images,
not desktop scanout, physical input or original presentation evidence.

Locked isolated restore and Release build pass with zero warnings. The full suites
pass **2,459 .NET / 299 Python tests**, adding 21 managed cases and six independent
audit cases. An initial Python sandbox run encountered five temporary-directory
permission errors; rerunning with TEMP/TMP under scratch passed. No production
change was needed for that environment issue. Full managed TRX, captures, manifests
and audit stay ignored under scratch; committed identities are in the
[evidence record](../evidence/combat/accumulate-window-v1.json).

## Launch and bounded owner review

From C:\repositories\fft-experimental-project, use the already validated build:

~~~powershell
dotnet scratch/accumulate-107-artifacts/bin/Fft.Client/release/Fft.Client.dll --accumulate-battle --content-root scratch/accumulate-107-prepared-a
~~~

For a new preparation after a fresh build, choose an unused output directory:

~~~powershell
dotnet scratch/accumulate-107-artifacts/bin/Zodiac.AssetTool.Cli/release/Zodiac.AssetTool.Cli.dll prepare-accumulate-battle --output scratch/accumulate-review-new
~~~

Pass that directory as --content-root. To reproduce the isolated build, use:

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/accumulate-107-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/accumulate-107-artifacts
~~~

The local visual gallery is scratch/accumulate-107-review/review.html. For a short
physical-input review:

1. Launch the scene. Confirm both unit cards show PA5. Press N (or click Next Turn)
   to ready A, then press C (or click Accumulate). A shows PA6 and Act Used;
   the status says PA5 to6. A remains selected; there is no target-selection step.
2. Press C again. It rejects without changing PA. Select B with 2 or its card and
   try C during A's turn: inactive selection also rejects.
3. Select A with 1. Press M, Down, Enter to move after the ability. Press N for B;
   C rejects because B has no grant. Press W then N; if B is still active, W then
   N again. On A's turn press A then click B to attack. The first hit does 24
   damage using PA6. Both units are controlled manually in this authored sample.
4. Resize and try a mouse action to check pointer placement. Press R (or Restart);
   PA returns to5 and the battle starts over. Escape closes the window.

The automated route covers the lengthy PA99 repeat-use/cap/KO cases; there is no
need to repeat 94 boosts manually. Owner approval should cover readability and
actual keyboard/pointer operation. Record approval separately; no approval is
inferred from these tests. The owner's subsequent approval is recorded above.
Editor work and battle suspension remain deferred.

## Reproduce the independent evidence audit

After two fresh native check outputs (follow a finite plan before launching):

~~~powershell
python tools/verification/verify_accumulate_window.py --prepared scratch/accumulate-107-prepared-a --prepared-repeat scratch/accumulate-107-prepared-b --first scratch/accumulate-107-native-a --repeat scratch/accumulate-107-native-b --output scratch/accumulate-107-review-new
~~~

The audit checks every accepted ability delta (only PA and Act may change), zero
resolution RNG draws, all rejected state/events, cap availability, later damage,
restart, manifest hashes, exact output inventory, repeated bytes and PNG sizes.
Its diagnostic JSON is not a new battle-save or persistent replay format.
