# ZOD-102 — Authored world-menu navigation

Persistence follow-up (2026-10-26): [ZOD-069](world-save-load-ui.md) extends the
normal interactive menu with explicit service-backed storage commands. The
navigation-only model/check profile below retains its earlier no-I/O scope.

Follow-up (2026-10-26): [ZOD-103's Windows host](windows-world-navigation.md)
is complete with finite native checks, owner gallery approval and owner-reported
manual controls/resize/close/focus checks. The model validation below is historical.

Selected 2026-10-26 at managed `d0f53cb`. Read-only reference HEAD:
`11464943bbdbd2a0af022d90cb7f10455cf911df` (clean). This is the first bounded
child of ZOD-042. It proves application navigation before a Windows host
(ZOD-103) and slot screens (ZOD-069). The original UI epic remains open.

## Source context and separation

Static inspection at that revision identifies three distinct original paths:

- `include/fft/title.h` (`title_menu_entry_e`) and `src/open/title.c`
  (`main_menu_update`) define New Game, Continue, Tutorial and Sound. Continue
  calls `save_screen_run(0)`, rebuilds the title after no load, and routes a
  successful load according to next-scenario/formation-return flags. Circle or
  Start begins the selected title transition. This is not an in-memory Resume.
- `src/wldcore/menu_levels.c` (`menu_stack_dispatch_update`,
  `menu_stack_dispatch_suspend`, `menu_stack_dispatch_resume`) dispatches only
  the top level and suspends/resumes parent presentation/input. Its complete
  menu stack, arithmetic, display pools and all callers are not translated here.
- `src/world/menu/input.c` (`menu_input_message_state_update`) suppresses input
  during selected scale/text/help/fade phases. `src/event/card/input.c`
  (`save_input_update_controller`, `save_update_input`) derives press/repeat
  masks and suppresses them during fades. `src/event/card/entry.c`
  (`card_entrypoint`) separately owns card scan/list/write/leave dispatch.
  Neither repeat timing nor these thread/card loops are ported by this proof.

These are source-study findings, not executable/original-screen comparisons.
Original fonts, title art, sounds, name/birthday, tutorial, save availability,
formation operations and menu timing need separate traces/assets/checks. No
original content is required for this authored model. Existing intro/battle
entry points remain separate. No new Engine/Framework interface is selected.

## Selected contract before coding

The application model owns one existing `WorldCheckpointService`, its current
immutable checkpoint and six closed screen identities. It performs no storage
operation. Views publish immutable entries, focus, notice and checkpoint;
renderers never write campaign fields. New uses the authored content factory;
Travel delegates to the existing service. No clock, RNG, input polling, graphics
or original reference code enters navigation.

1. Home starts without a checkpoint: New, disabled Resume, Exit. New starts the
   invented Harbor checkpoint and opens World. Resume means the live session,
   never a disk load. Returning Home retains it and focuses Resume.
2. World offers Travel (the other authored location), Party and Home. Travel
   spends seven funds/adds eleven seconds through ZOD-068, returning its exact
   state/events or rejection. Failed travel preserves the live checkpoint.
3. Party exposes the checkpoint read-only and a Back entry. Returning to World
   restores Party focus. No jobs, equipment, inventory or progression edits.
4. New with a live checkpoint opens ConfirmNew with Cancel focused. Only Accept
   replaces the session; Back/Cancel preserves it and restores Home/New focus.
5. Exit/Back from Home and an explicit host exit request from Home/World/Party
   open ConfirmExit with Cancel focused. Cancel restores its origin screen and
   focus. Accept closes navigation without writing, saving or clearing state.
   Exit requests during another modal are rejected. Closed rejects all actions.
6. Up/Down wrap through enabled entries only. Confirm activates the focused
   entry. Back is screen-specific; modal Back cancels. One call represents one
   logical action, and its transition cannot also activate the destination.
   Direct stable-action activation (future mouse input) uses the same checks.
   Unknown inputs, off-screen or disabled actions cannot mutate state.
7. Modal screens expose only Cancel/Accept, with the same underlying checkpoint.
   Parent actions cannot run through a modal. Remembered snapshots remain valid
   after navigation/travel/new. Focus and notice are application state, not saves.

## Validation and next boundary

Add regressions for fresh/New/Resume, party/parent restoration, safe confirmation
defaults, modal isolation, explicit close requests, disabled/unknown actions,
focus wrapping, insufficient funds, complete travel state/events and repeatable
logical action scripts. Prove navigation leaves the selected save directory
absent and legacy battle/intro/codec paths unchanged. Run locked restore,
Release build and the managed regression suite; preserve manual checks separately.

ZOD-103 must bind this model to one finite Windows host, keyboard/mouse and
controller edges, focus-loss baselines, resize-safe hit tests and rendering.
Select bounded native capture/check budgets before launching it. Physical device
review remains separate from programmed input. Only after that prerequisite does
ZOD-069 add slot lists, I/O decisions and recovery/error presentation. No
placeholder Save/Load entry claims those capabilities here.

## Executed validation — 2026-10-26

`Fft.Client.Navigation.WorldMenuSession` now implements the closed authored
model. **18 new client tests pass**, including full state/events comparison
against the existing checkpoint transition, immutable retained views, all origin
screens for host exit, fresh/retained Home, read-only Party and rejected actions.
Two independently composed logical scripts produce identical complete views and
travel results; each finishes closed with funds 86/elapsed 64 after two trips.
The selected save directories remain absent.

Locked restore and Release build pass with zero warnings/errors. The full
managed suite passes **2,285 tests**: Engine 24, Framework 830, Content 644,
AssetTool 202, CLI 211 and Client 374. A pre-existing save-test analyzer warning
was corrected by using `Assert.Single`'s predicate overload; its assertion and
production save behavior are unchanged. All **281 Python tests pass** (no new
Python cases). The regression result and source,
log and TRX identities are recorded in the
[executed metadata](../evidence/navigation/synthetic-world-navigation-v1.json).
Historical ZOD-068 evidence retains its original source identities.

```powershell
dotnet restore Fft.sln --locked-mode
dotnet build Fft.sln -c Release --no-restore
dotnet test Fft.sln -c Release --no-build --no-restore
```

No native window, visual/device/focus-latch check, original UI observation or
Save/Load UI was executed or implemented. The no-argument bootstrap, existing
CLI routes, Engine/Framework/Content production, save schemas, packages and
prepared introduction/battle code are unchanged. ZOD-102 is complete at the
application-model boundary; ZOD-103 is Ready and ZOD-069 remains Waiting.
