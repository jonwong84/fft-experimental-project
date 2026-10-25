# ZOD-091 — Prepared introduction to the playable battle

One Windows host now runs the prepared general-banner → Ready introduction and
then opens the existing authored two-unit battle. A pure ownership gate holds
the battle closed until the introduction's accepted event exit. The transition
runs no battle command, advances no CT and consumes no battle RNG. The first
turn still requires an explicit Next Turn command.

Date: 2026-10-25. Managed base: `fc69ffc` (clean on entry). Read-only reference
HEAD, freshly verified and clean:
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Validation metadata](../evidence/events/intro-battle-handoff-v1.json) binds source,
prepared inputs, compiled assemblies, regression results and finite captures.
No new emulator/original capture was made. This is an authored integration;
original EventEnd, dark-screen release, startup and input timing remain separate.

## Source boundary and implementation

The [ZOD-090 source trace](../architecture/zod-090-ready-battle-handoff.md) remains
the original-behavior contract: after Ready the original event must release/wait
for dark-screen work and restore scheduling before EventEnd cleanup, possible
scenario chaining, event-frame completion and turn selection. A player-controlled
turn is conditional on later unit/menu gates. Rechecked source fingerprints and
retained event-4 states preserve that evidence; no original operation is silently
omitted under an original-compatibility claim.

The authored ZOD-089 descriptor remains unchanged. Its selected five-operation
program ends with a synthetic exit. `BattleIntroductionState` in Zodiac Framework
attaches to a fresh admitted sequence and delegates every tick to the existing
atomic `BannerReadyExecutor`. It publishes availability only when the accepted
event has exited, both display workers are terminal and the scene has no live
tasks. Hidden Ready, child clear and dispatcher clear cannot open the gate.
Post-handoff tick attempts reject without another publication. An inner budget
or input rejection returns the exact gate object and the original empty failure
trace, so the caller can retry without identity/tick consumption.

`IntroductionBattleSession` owns that gate and the existing `BattleInteraction`
and `BattleUiRouter`. Only its gated Key/Action/Click methods route commands;
the mutable controllers are internal to the client. Before availability, even
selection, modes, cursor, restart and save/load keys leave battle state/history
unchanged. Intro advancement accepts only its Circle sample and never calls a
battle route. On accepted handoff it records one availability tick/publication.
The existing battle command journal, formulas, RNG, turn controller and save
schema have no implementation changes. Sessions have one client-thread owner.

The shared `PlayableBattleWindow` now has an optional introduction, implemented
in a focused partial class. The existing battle renderer/layout and finite UI
check are reused. `BattleInputBuffer` adds queue ownership around the existing
`BattleInputLatch`: baseline clears queued presses and records held keys/mouse.
At the ownership boundary the window baselines actual held buttons and returns
from that update, so it cannot submit a battle command in the transition call.
A held button needs release and a new press. Focus handlers use the same buffer;
battle Restart resets only the existing battle, without replaying the intro.

Both prepared packages load and validate before constructing the window. All
intro and battle graphics resources exist before logical admission. The new
entry point creates its check directory only after readiness/start succeeds.
The normal host requests one explicit intro tick after the preceding snapshot
has been presented, pauses advancement when inactive and maps a fresh C press
to Circle. This is an authored presentation cadence, not original elapsed time.
Battle presentation time never advances CT. Existing standalone entry points
and strict prepared formats remain independently available.

## Reproduce and review

With the existing ignored prepared packages:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build -- --intro-battle --sequence-root scratch/banner-ready-089-prepared-b --sequence-id sample:sequence/banner-ready --general-artwork-root scratch/banner-078-a --ready-artwork-root scratch/ready-086-artwork-a
```

The intro plays automatically; C supplies a fresh skip sample. After it finishes,
use the existing battle keyboard/mouse controls (N Next Turn, M Move, A Attack,
W Wait, R Restart, Esc close). The battle is the existing local two-player,
first-KO diagnostic scenario. No actual original campaign map/menu is implied.

Add `--check <fresh-scratch-root>` for the finite intro plus the established
battle check. `--circle-ticks 5,7,8` selects a reproducible logical skip schedule
in check mode. Raw intro captures remain 256×240; the battle uses its existing
960×640 canvas. No new aspect correction, CRT, resolution or audio policy is added.

Review `scratch/intro-battle-091-review/review.html`. Play flow previews the
natural or skipped path; Show handoff jumps just before availability. Check that
the banner/Ready finish cleanly, the battle appears with no turn started, and
the subsequent battle views retain their appearance. The terminal intro checker
is captured offscreen for auditing; the host switches directly to the battle
view at exit. The gallery models that switch and labels its slower review speed.
**Owner visual review is pending.**

## Executed acceptance and validation

All twelve independently authored
[IBH contract cases](../evidence/events/intro-battle-contract-v1.json) now have
checks mapped in the validation metadata. The historical specification keeps
its original `executed: false` flags; this implementation's separate evidence
records the executed checks and their scope.

Locked restore and zero-warning/error Release build pass. All **1,909 .NET tests**
pass: Engine 24, Framework 667, Content 516, AssetTool 192, CLI 196, Client 314.
The **41 new managed tests** cover event/worker ownership gaps, natural/zero/max
holds, Circle birth/release, late rollback, pre-handoff route rejection, held
input/focus baselines, one-time availability, repeated/omitted rendering and
existing battle/headless replay. All **215 Python tests** pass, including seven
new receipt boundary/negative checks. The shared independent banner/Ready model
now exposes a common frame audit; its old public parser still rejects the new
receipt format.

Six finite native runs cover repeated natural 60/60, Circle ticks 5/7/8, zero
holds, maximum holds and a Ready skip at 97. All **1,049 intro PNGs** agree exactly
with CPU composition, actual GPU readback and the independent prepared-data
pixel/timeline model. The two natural runs repeat **all 170 artifacts**, including
the complete receipt. The new audit independently checks availability only at
event exit, a stable initial battle digest and zero journal entries at every
intro tick. Natural exit is 162; zero is 42; maximum is 552; skipped is 18.

Each run then routes the established UI scenario through the same battle
controller: **12 accepted / 2 rejected** attempts match headless and journal
replay, and all six battle PNGs equal the historical published battle output
byte for byte (**36 comparisons**). Resource recreation at intro tick 5, programmed
focus handlers before/after handoff, a held-key/mouse boundary probe and the
existing battle resize check preserve state. These are production handler/latch
calls with programmed input, not physical input injection or latency evidence.

Eight real entry-point negatives cover each missing/corrupt package, unavailable
SDL driver and preexisting output. They create no new check output and preserve
the preexisting marker. The standalone battle repeats six historical PNGs and
its replay result; standalone banner/Ready repeats all **164** historical files.
An additional isolated working directory containing only copied prepared
packages and generated output reproduces all **170** natural artifacts. Host
libraries remain in the built application directory; this is not a standalone
deployment or virgin-machine test.

Original assets, captures, logs, generated reports and inventories remain ignored
under scratch. Committed evidence contains identities, scalar observations and
authored implementation/tests only. Full original startup, original first player
input, map rendering, campaign state and hardware/audio fidelity remain open.
