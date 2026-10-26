# ZOD-111 prepared Basic Skill controls

## Finite native plan (recorded before execution, 2026-10-26)

Use only authored, manifest-verified prepared content and the existing managed
Windows battle renderer. No original executable, emulator, imported media, editor
or battle-save workflow is needed. Reference checkout remains read-only at
11464943bbdbd2a0af022d90cb7f10455cf911df (freshly checked clean).

1. Build/test the shared router, compact map projection, prepared loader and finite
   typed-command plan before opening a window. Keep legacy layouts/routes intact.
2. Prepare the three authored metadata/self/targeted JSON files plus manifest twice
   into fresh scratch/basic-skill-111-prepared-a and -b; compare every byte.
3. Launch exactly two programmed native check processes, sequentially, with fresh
   scratch/basic-skill-111-native-a and -b outputs. Each is bounded to 30 seconds
   and at most 300 routed inputs. Do not launch a third without recording a revised
   plan and reason. Preserve any failed run's output.
4. Exercise keyboard and pointer targeting, select/confirm/cancel, empty/self/
   inactive/ungranted/used/out-of-range rejection, availability and range probes,
   Accumulate PA6, damage/HP and flat pushes, map-edge non-push, first KO and restart.
   Derive all battle changes from ordinary typed commands, with no state injection.
5. Compare every routed state (including both RNG copies), command acceptance and
   events against a separate headless execution. Replay each journal before restart.
   Assert selection/cancel/probes/drawing, resize, and programmed focus baselining
   preserve battle state/history. Focus callbacks/buffers are programmed checks;
   physical OS focus transitions remain an owner check.
6. Record a fixed capture inventory, inspect every GPU image, independently audit
   command/damage/RNG/state and image/hash inventories, and compare both native
   outputs byte for byte. Fixes require rerunning relevant automated checks.

TST-029..033 may be recorded only with passing evidence. TST-034 remains pending
owner visual/physical-input approval. Programmed routing is not physical input or
original-game equivalence. Editor work and ZOD-106 checkpoints remain deferred.

The fixed native inventory is check.json and 28 PNGs: start, not-ready, ready,
range, cancelled, self-rejected, inactive-rejected, empty-target, out-of-range,
range-restarted, boosted, stone-act-used, act-used, moved, no-grant, stone-1
through stone-6, terminal-1 through terminal-6, and restarted. Each PNG is a
960x640 offscreen GPU capture; the backbuffer changes to 1280x720. The audit
script owns the same explicit inventory. Both journals replay before restart.

## Result and limits

Implemented; TST-029..033 pass, **TST-034 owner approval is pending**. Locked
isolated restore and Release build passed with zero warnings. All **2,723 .NET /
311 Python** tests pass (23 new managed, six Python). The first focused run found
an assertion comparing immutable-array identity; corrected to compare its single
element. Failed and passing TRX remain under ignored scratch.

Exactly two native checks completed 121 inputs/137 rendered frames each. They
matched every typed headless state, command acceptance and event, including both
RNG copies, with two complete journal replays before restart. Four prepared files
and 29 native files (28 GPU PNGs plus diagnostic JSON) repeat byte for byte.
Independent Python audits repeat exactly and check damage, RNG, pushes, range,
availability, atomic rejection, hashes and PNG inventories. All 28 images were
inspected in four contact sheets, with the range image also inspected at full size.
The native route contains six PA6 hits (6/12 damage), three pushes, 13 atomic
command rejections, empty-target feedback without a command, and restart to PA5.

The final lethal hit occurs at the map edge. This demonstrates a no-push result
there, but does not independently distinguish lethal suppression from off-map
suppression. Nonlethal off-map suppression remains pure-rule test evidence from
ZOD-109. Programmed focus callbacks/baselines do not establish physical OS focus
behavior. The authored scene is not an original encounter or projectile-animation
port. Existing sword limits/facing discrepancy, ability save guards, editor and
ZOD-106 deferrals remain in force. No third native process was launched.

See [evidence](../evidence/combat/throw-stone-window-v1.json). Local review gallery:
scratch/basic-skill-111-audit-a/review.html. All generated files stay ignored.

## Reproduce and launch

From the repository in PowerShell (choose fresh output names for preparation or
checks). These commands use isolated output so an already-open editor can remain
running:

~~~powershell
dotnet restore Fft.sln --locked-mode -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/basic-skill-111-artifacts
dotnet build Fft.sln -c Release --no-restore --artifacts-path C:/repositories/fft-experimental-project/scratch/basic-skill-111-artifacts
dotnet test Fft.sln -c Release --no-build --no-restore -p:UseArtifactsOutput=true -p:ArtifactsPath=C:/repositories/fft-experimental-project/scratch/basic-skill-111-artifacts
dotnet scratch/basic-skill-111-artifacts/bin/Zodiac.AssetTool.Cli/release/Zodiac.AssetTool.Cli.dll prepare-basic-skill-battle --output C:/repositories/fft-experimental-project/scratch/basic-skill-111-prepared-new
dotnet scratch/basic-skill-111-artifacts/bin/Fft.Client/release/Fft.Client.dll --basic-skill-battle --content-root C:/repositories/fft-experimental-project/scratch/basic-skill-111-prepared-new
~~~

The existing prepared-a directory can be used immediately; do not prepare over it.
For a finite programmed check, append --check <fresh-directory> to the client
command. It exits on completion and rejects existing output. The client validates
all three prepared packages and their manifest hashes before creating a window
or check output. It never reads a BIN, decompilation or original executable.

~~~powershell
python tools/verification/verify_basic_skill_window.py --prepared scratch/basic-skill-111-prepared-a --prepared-repeat scratch/basic-skill-111-prepared-b --first scratch/basic-skill-111-native-a --repeat scratch/basic-skill-111-native-b --output scratch/basic-skill-111-audit-new
~~~

## Owner interactive review (pending)

1. Open the gallery or launch using prepared-a. Confirm the whole map, both cards,
   HP/PA/positions, action buttons and bottom hints are readable.
2. Press N. A becomes active. Press T: purple tiles show geometric range 1..4;
   orange shows a legal occupied target. Press Backspace: targeting cancels and
   Act stays open. Select cards with 1/2/Tab or click; selection spends no turn.
3. Select A, press T, Right twice, then Enter to target B at (3,2). B loses 5/10 HP
   at starting PA5 and may move. Alternatively click Throw Stone and then B's
   sprite or occupied tile to confirm. The button is muted when there is no legal
   target; attempting confirmation still displays the pure command's rejection.
4. Press R, N, then C. A's battle PA rises 5 to 6, consumes Act and prevents another
   Act this turn. M, Right, Enter moves A to (2,2) after Accumulate. N hands off to
   B. B has no learned Throw Stone: T, Left, Enter rejects without changing state.
5. W ends the selected active turn; N advances readiness. Whenever B is active,
   W/N again until A is active. Then T, aim at B's current coordinates with arrows,
   Enter; or use the Throw Stone button and click B. Damage is now 6/12. Repeat
   legal turns to see pushes toward the edge and eventual first KO. No action
   changes battle state after completion. R restores HP50, PA5 and positions.
6. Try an empty tile, self target and inactive selection. Confirm clear feedback.
   Resize the window, switch away/back, and hold/release T across focus changes;
   confirm no unintended action, duplicate press or battle-state change.

C is the temporary in-battle Accumulate boost. It resets with a new/restarted
battle and does not imply campaign persistence. Backspace cancels; Esc closes.
Report visual/physical approval separately; programmed checks do not supply it.
