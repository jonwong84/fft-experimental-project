# Reproducible two-unit headless battle (ZOD-023)

Date: 2026-10-23. Managed project base: `796d047624895cabe4ba4e18b55602739671eab6`.
Read-only reference `C:\repositories\fft_decomp` HEAD rechecked as
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

This completes the bounded M1 start-to-result integration. **The first KO ends this
scenario and awards victory to the surviving unit.** That is an invented scenario
objective, not a recovered FFT victory condition. All units, effective stats,
weapon and map content are synthetic and use `sample:*` IDs.

## Launch and setup

From the repository root after the normal Release build:

```powershell
dotnet run --project src/Fft.Client -c Release --no-build --no-restore -- --scenario two-unit-battle
```

Exit 0 means the scripted scenario reached its expected first-unit victory. An
unexpected rejected command or a different final outcome returns 1. Invalid
arguments return 2. The existing no-argument bootstrap and prepared-asset loading
arguments remain available. The output is a developer command/event/state trace,
not an interactive game UI or a saved replay format.

Implementation follow-up (2026-10-23): [ZOD-032](../development/playable-battle.md) now
plays this same supported scenario in a separate Windows UI. The `--scenario` command
remains the deterministic headless script described here; Core behavior is unchanged.

`Fft.Content.Samples.SyntheticTwoUnitBattle` composes an explicit synthetic metadata
dependency and sword package, validates setup and resolves its weapon projection
through `AttackBattleSetup`. It creates a 2-by-2, single-layer dry flat height-10
map with ground entry cost 1. Both units have Move 1, Jump 2, ordinary body height 6,
Normal movement, no traits, HP/maxHP 50, PA 5, class evade 10, Brave 70, the same
zodiac sign and one power-5 sword. MP/equipment evade/supports/reactions are zero
or absent. The first unit starts at (0,0), facing East; the second at (1,1), facing
West. Both start with CT 0, Speed 10; tie keys are 0 and 1. RNG is explicitly
seeded to unsigned word 1, draw count 0, before the first readiness/upkeep draw.

The scenario selects `OrdinaryAdjacentStepPrototype` explicitly. The combat-aware
Move wrapper now accepts that existing policy; its default remains the flat-grid
prototype, preserving prior callers and fixtures. This integrates ZOD-010's bounded
geometry/cost/occupancy checks, not complete original movement or allegiance rules.
The chosen positions make every attack's possible knockback destination off-map.

This launch needs no BIN, BIOS, original prepared media, reference executable,
C toolchain, network, rendering, wall clock or input polling. The client dispatches
an explicit typed command list to managed simulation. No file/console access is
introduced into the simulation or authored setup factory.

## Lifecycle and ownership

`Fft.Core.TwoUnitBattleDefinition` binds exactly two distinct named units, the
immutable initial battle/RNG and explicitly selected sword projections. Setup
requires healthy units, an initial AddCt phase with both choices unused, resolved
equipment and the attack arithmetic envelope. It preflights the selected movement
policy, every unblocked surface's terrain cost (including distant tiles), and
`Move + maximum traversable entry cost <= Int32.MaxValue`. This is a conservative
managed arithmetic bound. Invalid Jump, missing costs and unsafe additions fail
at definition construction, before the scenario starts.

`definition.Start()` creates a fresh `TwoUnitBattleState` over the original
immutable setup. Calling it again is the explicit restart operation; the completed
state, original setup and earlier event history remain untouched. Outcome is
InProgress, FirstUnitWon or SecondUnitWon, with an explicit nullable winner ID.

`TwoUnitBattleScenario.Apply` accepts typed Advance, Move, Attack and Wait commands.
It delegates to the existing clock/attack/movement transitions. Failed commands
return the same scenario state, no events and the specific underlying rejection.
Unknown command types are rejected explicitly. Null arguments remain programmer
errors, consistent with the other simulation APIs.

After an accepted command causes one unit to reach zero HP, the scenario preserves
the complete attack result and appends one `TwoUnitBattleCompletedEvent` with the
scenario, winner and loser IDs. It does not force a Wait, refund CT, remove the body
or draw additional randomness at completion. Underlying ordered events are carried
unchanged by `TwoUnitBattleTransitionEvent`.

Every subsequent nonnull command, including Advance, returns AlreadyCompleted,
the exact terminal state and no events/draws. Completion takes precedence over
command-specific validation. The scenario therefore stops before the unsupported
post-KO clock/body lifecycle. This is an explicit terminal policy; no death
countdown, revival or crystal/treasure behavior is implied.

The lifecycle is FFT-owned managed application/simulation code. The authored
records/script are in the Content sample factory; console formatting stays in
the Client. Zodiac receives no FFT victory, equipment or scenario assumptions.

## Authored command sequence and receipts

The fixed script contains twelve accepted commands:

1. Advance: first unit becomes ready at tick 10.
2. First unit moves from (0,0) to (0,1).
3. First attacks second: second HP 50 to 25; both choices consumed, turn ends.
4. Advance: second is already ready, so it starts at the same tick 10.
5. Second attacks first: first HP 50 to 25; movement remains available.
6. Second Waits: unused movement refunds 20 CT.
7. Advance: second becomes ready again at tick 18, before first's CT reaches 100.
8. Second moves from (1,1) to (1,0).
9. Second Waits: unused action refunds 20 CT.
10. Advance: first becomes ready at tick 20.
11. First moves from (0,1) to (0,0).
12. First attacks second: HP 25 to 0, Dead and KO count 1; then scenario completion.

Ready units are first, second, second, first, with upkeep outputs
`[16838, 7419, 32225, 17543]`. Turn-end CT receipts are `[0, 20, 20, 0]`.
Each attack hits for 25 without a critical. The attack RNG checkpoints are word
3655513600/count 7, word 2937909499/count 14, and word 2473755763/count 22.
The final proc roll is below 19 but the explicitly empty effect set adds no status.

Final state: first HP 25 at (0,0), second HP 0/Dead at (1,0), first-unit victory,
ticks 20, CT first 0/second 40, neither unit holding a turn, RNG word **2473755763**
with **22 draws**. Final ordered events preserve action consumption, turn end,
attack resolved, HP changed, status changed and KO, then append scenario completed.
Completion itself consumes no RNG and does not alter the underlying battle snapshot.

These receipts combine the already implemented source-derived rules:
`src/battle/turn/clock.c::turn_run_clock`, `turn_move_act_taken_set`, `turn_end`;
`src/battle/combat/formulas.c::formula_weapon_damage`; and damage/evasion/result
helpers traced in [ZOD-022](bounded-sword-implementation.md). Movement source and
conservative exclusions are recorded in [ZOD-010](adjacent-step-implementation.md).
All current C references use the pinned revision above. Raw generator arithmetic
was calculated independently using the separately verified BIOS recurrence; an
independent source review checked the composite timeline before implementation.

## Validation and limits

`TwoUnitBattleScenarioTests` checks every command's expected HP, CT, ticks and
RNG receipts; ordered readiness/turn/attack/completion events; complete snapshot
immutability; restart and independently composed replays; invalid-command recovery;
terminal rejection; setup preflight; and an authored lower-HP variant in which the
second unit wins. Snapshot comparisons include map tiles, profiles/costs, positions,
HP/status/equipment, facing, turn flags and RNG. Replay checks the complete ordered
event sequence, not just the final winner.

Two actual headless CLI runs produced identical full transcripts, with exit 0 and
the expected winner/RNG. The local diagnostic capture is ignored at
`scratch/two-unit-battle-transcript.txt`. Bootstrap and invalid-scenario usage were
also checked. Source/prototype expectations remain source-derived, and the
objective/content/command/result contracts are synthetic. No original FFT C battle
or independent original/emulator battle was executed by this milestone.

M1 demonstrates a bounded deterministic headless battle, not complete FFT combat
or a playable Windows game. The opponent's choices are explicitly scripted; no AI
is implemented. General movement, knockback, reactions, original victory logic,
death scheduling, rewards, campaign, menus, rendering/audio/input and retail global
RNG integration remain separate work. Future modified mechanics, including the
owner's knockback redesign, must remain explicit profile/rule choices alongside
the original compatibility behavior; this scenario adds no modification framework.

Final Windows validation on 2026-10-23: locked solution restore, Release build with
zero warnings and all 632 solution tests passed. Existing v1/v2/v3 behavior fixtures
remain unchanged; no package versions or production dependency references changed.
