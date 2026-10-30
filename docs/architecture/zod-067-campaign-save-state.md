# ZOD-067 — Campaign save state and original checkpoints

Implementation follow-up (2026-10-26): [ZOD-068](../development/synthetic-world-checkpoint.md)
now proves the selected synthetic idle-world Save/Load service. The original-game
field/lifetime investigation below remains historical evidence; its original
checkpoint, RNG and full campaign gaps are not closed by the synthetic proof.

Date: 2026-10-23. Target: North American PS1 `SCUS-94221`.
Read-only reference: `C:\repositories\fft_decomp`, verified HEAD
`11464943bbdbd2a0af022d90cb7f10455cf911df`.

**Use an FFT-owned, versioned logical campaign snapshot and explicit checkpoint
kind. Replace the card browser with Windows Save/Load application actions.** An
original campaign save is a roster/world checkpoint, not a suspended battle or
serialized event interpreter. The existing [two-unit battle save proof](../development/two-unit-battle-saves.md)
has a deliberately different checkpoint and RNG contract.

This is static source evidence and a design recommendation. No original save,
emulator session, campaign, memory-card image or C executable was run or imported.
No runtime code, UI or packed-card converter is implemented here. All decomp paths
and symbols below refer to the pinned revision; offsets describe source evidence,
not a proposed managed file layout. Names and sizes of unknown fields are not
evidence of their game meaning.

## Primary source paths

- [Save layout and summary](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/save.h):
  `save_image_t`, `save_summary_t`, resident globals.
- [WORLD serializer, loader and browser](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/world/card_save.c):
  `save_image_build`, `save_image_restore`, `load_update`, `save_screen_run`.
- [CARD serializer and browser](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/card/entry.c):
  `save_image_build`, `card_entrypoint`, `save_write_update`.
- [Party record](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/unit_party.h)
  and [resident party conversion](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/party.c):
  `party_unit_t`, `party_unit_save`, `party_clear`.
- [Logical variable IDs](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/include/fft/script_variable.h),
  [variable access](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/variables.c)
  and [control/RNG](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/lib/event/script/control.c).
- [Resident game loop](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/main/entry.c),
  [title Continue](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/open/title.c)
  and [scenario selection](https://github.com/adamrt/fft_decomp/blob/11464943bbdbd2a0af022d90cb7f10455cf911df/src/event/attack/scenario_setup.c).

Other source paths below are relative to that same reference checkout, not to
this managed repository.

## Read/write boundary and reconstruction

CARD and WORLD both build the same `0x1e00`-byte image. WORLD implements its
inverse. The payload copies resident globals rather than the live battle objects.
Its fixed roster/table counts are original storage evidence, not limits to impose
on new content or a generic engine.

The original full restore first rejects an incomplete slot marker, a format other
than 4, or mismatched parity. It checks 128-byte blocks 3 through 59; header and
summary blocks 0 through 2 are skipped. It then copies party, inventory, item flags,
the unknown byte, the entire script backing bank and options; applies the sound
mode; restores sort/page orders and side-content records; finally reconstructs
hours/minutes/seconds from elapsed seconds. It does not reconstruct a running
battle, replay commands, regenerate the party, or call an RNG seed function.

The copy into script storage bypasses script setters. Replaying setters during a
managed restore could cap funds, invoke profile/progress write vetoes or update
dependent values. Validation and reference binding must precede an atomic candidate
state replacement; deriving presentation comes afterward.

`src/world/tutorial.c::tutorial_start` uses `save_image_build(-1)` to snapshot the
current game in RAM, loads `FFTSAVE.DAT`, and calls `save_image_restore(1)`; tutorial
completion restores the saved snapshot with the same partial path. Partial load
still checks slot/version but skips the parity sweep and play-time restoration.
This internal tutorial swap is not a player save checkpoint or a reason to weaken
native file validation.

For the ownership map below, recommended managed restore stages are:

1. **R1 — Resolve and validate:** caller-selected profile/content versions, stable
   IDs, schema, checksum, limits and checkpoint support; no live mutation.
2. **R2 — Construct campaign candidate:** party identities/progression/equipment,
   inventory, logical world state and side-content relationships together.
3. **R3 — Bind continuation:** resolve the supported world/scenario route and its
   explicit RNG policy; validate cross-field invariants before commit.
4. **R4 — Publish application state:** atomically replace the session, rebuild
   formation/world views, apply settings and emit a success result. File I/O and
   presentation remain outside Core. No audio/render callbacks run during R1–R3.

## Logical ownership and lifetime map

Every group below is copied by both serializers and restored by WORLD unless
explicitly identified as derived or absent. Grouping related fields does not make
their interpretations complete.

- **Party identities and roster membership:** `g_party_units`, `party_records` at
  `+0x484`; 20 source slots, each copying only the first `0xe0` of a `0x100`-byte
  `party_unit_t`. Identity, sprite/palette, gender/egg flags, birthday/zodiac and
  name/name-ID fields survive overlay and battle changes. `src/main/party.c`
  creates/converts records; `src/event/require/party_changes.c` commits battle
  survivors/joins; `src/world/formation/roster.c` dismisses/generates units.
  **Owner/lifetime:** FFT campaign party, campaign lifetime. **Restore:** R2,
  stable instance IDs and explicit roster/guest membership before cross-references.
  **Unknowns:** unused bytes are not new semantics; omitted tail bytes cannot be
  assumed zero or persistent. `party_clear` marks slots empty without clearing
  their other bytes. Do not serialize an empty slot's stale bytes as a live unit.

- **Party growth and progression:** the same record contains job, level,
  experience, permanent Brave/Faith, raw growth stats, career/unlocked jobs,
  learned abilities, job levels/JP, plus proposition availability and egg data.
  `party_unit_save` copies permanent data out of unit state; definitions are in
  `include/fft/unit_party.h` and the career types in `include/fft/job.h`.
  **Owner/lifetime:** FFT party progression, campaign lifetime. **Restore:** R2,
  validate job/ability references against content before deriving battle stats.
  **Unknowns:** exact validity/width constraints and permanent-vs-battle projection
  for every field need individual behavior tickets; this is not a full stat codec.

- **Equipment and selected abilities:** party equipment, secondary skillset,
  reaction/support/movement ability IDs; `party_unit_save` and WORLD formation
  item actions write them. **Owner/lifetime:** FFT party loadout, persistent until
  changed. **Restore:** R2 after unit identity; bind item/ability IDs without
  running equip actions or adding/removing inventory a second time. **Unknowns:**
  job-specific restrictions and all seven source equipment bytes are not yet a
  complete managed loadout contract; do not conflate them with the five ordinary
  formation equipment actions or silently discard unmodeled fields.

- **Inventory and Fur Shop stock:** `g_item_quantities` / `item_quantities` at
  `+0x1604`, `g_item_poached_quantities` / `poached_item_quantities` at `+0x1704`.
  `src/lib/inventory.c::item_inventory_count_adjust` and
  `src/world/shop/inventory.c::fur_shop_stock_adjust` are logical writers; the
  latter clamps 0..255. WORLD shop/equipment actions call these writers.
  `src/main/new_game.c::item_new_game_inventory_init` seeds consumables;
  `src/main/menu_sort_init.c::menu_sort_tables_init` clears poached stock.
  **Owner/lifetime:** FFT campaign inventory/stock, campaign lifetime. **Restore:**
  R2 with equipment, using item IDs, keeping carried and equipped counts distinct.
  **Unknowns:** the source byte ceiling is observed stock behavior, not a generic
  collection capacity; additional item-family rules need their own trace.

- **Move-Find collection flags:** `g_item_location_flags`, `+0x1804`, `0x80`
  copied bytes. `src/battle/combat/pickups.c::item_flag_test_or_set` uses map ID / 2
  and four bits per map, with an unfound flag test before collection. New sessions
  clear the storage in `src/main/status_check_data.c::status_check_data_init`.
  **Owner/lifetime:** FFT campaign map discoveries, persists across revisits.
  **Restore:** R2 before starting a map. **Unknowns:** only the first `0x40` bytes
  have the documented Move-Find interpretation; the second half is explicitly
  unresolved. Native IDs should identify map/pickup entries, not bit addresses.

- **Funds, calendar, birth date, casualties and story progress:** all reside in
  `g_script_variable_storage`, copied at `+0x1884` as `0x100` signed 32-bit words.
  `include/fft/script_variable.h` names funds `2c`, month/day `2e/2f`, birthday
  `5f/60`, injured/casualties `61/62`, story progress `6e`, shop availability `6f`.
  `src/main/save_state.c::save_state_init` clears the bank and initializes 1/1;
  `src/main/new_game.c::party_new_game_init` sets funds to 2000.
  `src/wldcore/calendar.c::calendar_day_advance` advances day, propositions,
  character birthdays and breeding. **Owner/lifetime:** FFT campaign/calendar.
  **Restore:** R2, without advancing a day or reapplying new-game defaults.
  **Unknowns:** variable `67` is a specifically traced counter, not an established
  Gregorian year; calendar-year jumps and this counter have differing behavior.

- **World flags, routes and optional progress:** the same backing bank contains
  packed bits and nibbles: logical `000..07f` are full words, `080..35f` bits,
  `360..3ff` nibbles. The 128 words, 736 bits and 160 nibbles occupy only the
  first 684 of the 1024 saved bytes; the remaining raw storage is copied too.
  Flags include Deep Dungeon, Goug/Cloud/Reis/Nelveska paths,
  rumor/treasure/land/proposition/person-profile ranges. `src/wldcore/map/location_script.c`
  and scenario interpreters read/write them; the ID header documents consumers.
  **Owner/lifetime:** FFT campaign event/world state. **Restore:** R2, retain
  unrelated neighboring flags; R3 resolves the selected world profile.
  **Unknowns:** this bank is not 1024 independent int32 values; unknown logical
  flags and trailing storage require evidence before dropping, normalizing or
  assigning typed meaning. Raw backing word `ff` is distinct from logical flag
  `ff`; `src/main/save_state.c::overlay_call_battle_return_zero` illustrates that
  distinction even though that wrapper is uncalled.

- **Continuation and transient values captured in the script bank:** current
  event `27`, location `31`, ENTD/map `32/33`, formation-return event `55`,
  next-scenario `64`, and save-in-progress `51` participate in resume routing.
  Camera, input masks, dialog scratch and scenario locals are also copied because
  the whole bank is copied. `scenario_load_conditionals` explicitly clears
  logical locals `70..8f` on normal scenario setup. **Owner/lifetime:** FFT
  campaign flow for continuation; FFT scene/session for scratch. **Restore:** R3
  resolves a named checkpoint route; R4 reconstructs the scene. **Unknowns:**
  serialization alone does not confer campaign lifetime on each variable.
  A native format must not treat every saved scratch value as authoritative live
  UI, nor assume it may delete all captured scratch without tracing resume use.

- **Collection dates and character ages:**
  `g_brave_story_treasure_acquisition_date_bits` (`+0x124`, `0x35` bytes),
  `g_brave_story_land_discovery_date_bits` (`+0x159`, `0x12` bytes),
  `g_proposition_last_attempt_date_bits` (`+0x16b`, `0x6c` bytes), and
  `g_brave_story_character_ages` (`+0x3f8`, `0x40` bytes).
  `src/wldcore/proposition/report.c` writes dated discoveries/results;
  `src/wldcore/brave_story/collections.c` and `propositions.c` read them;
  `calendar.c::birthdays_advance` updates ages. **Owner/lifetime:** FFT campaign
  history, campaign lifetime. **Restore:** R2 keyed by collection/person IDs,
  paired with corresponding event flags. **Unknowns:** the last-attempt-named
  array is documented as written only on successful results; retain that quirk.
  Padding and date absence sentinels need field-specific validation.

- **Propositions:** `g_proposition_states` (`+0x1d7`), count (`+0x438`) and active
  records (`+0x439`), with participants' party `proposition_status` and script
  nibble flags. `include/fft/proposition.h` defines failure/cooldown bits and each
  active record's flags, ID, elapsed/assigned days, location and participant indices.
  `src/wldcore/proposition/dispatch.c`, `report.c`, `results.c` and
  `proposition_day_advance` create/advance/complete them. **Owner/lifetime:** FFT
  campaign side activity, across travel and saves until resolved. **Restore:** R2
  after party identities; validate participants/counts and status relationships.
  **Unknowns:** source indices are not native stable IDs; report/menu scratch and
  random reward computation are not present as resumable active UI state.

- **Sound-novel readings and completion:** five reading records at `+0x238`,
  read-section bits at `+0x350`, saved flags at `+0x3f0`.
  `src/main/save_tables.c::save_data_tables_init` sets initial readings;
  `src/wldcore/sound_novel/script.c` snapshots/resumes them. Readings contain
  section/text history, picture/background/brightness, sound/music, counters and
  raw pointer words. Resume replaces `buffer`, rebinds `script_words` from the
  loaded section and resets `word_index`; raw pointers are not portable identity.
  **Owner/lifetime:** FFT optional-content reading progress, campaign lifetime.
  **Restore:** logical data R2, content/section binding R3, pictures/audio R4.
  **Unknowns:** field semantics remain provisional; code presence does not prove
  availability of every novel in SCUS-94221. Unsupported readings stay explicit.

- **Game options:** `g_game_options`, four bytes at `+0x1c84`;
  `include/fft/option.h` defines 13 semantic options; `src/world/options.c`
  edits them. Full/partial restore both apply sound mode via
  `suzuki_sound_set_type`. **Owner/lifetime:** FFT campaign presentation preferences
  in the original; native application owns their application. **Restore:** typed
  values R2; presentation effects R4 after success. **Unknowns:** Windows display
  resolution, key bindings and device selection are separate machine preferences,
  absent from this image. Avoid resetting them when loading campaign options.

- **Sort/page preferences:** unit-list, formation and five item sort orders,
  plus weapon/helmet/armor/accessory/item page orders at `+0x1c89..+0x1dbd`.
  `src/main/menu_sort_init.c` supplies defaults; WORLD formation/item menus edit
  them. Restore rejects the unit-list sequence if its first 11 values contain 11,
  similarly checks formation's first seven, and forces final `-1` sentinels.
  **Owner/lifetime:** FFT menu preferences, persisted for this campaign.
  **Restore:** R2 validate recognized sort keys/item IDs, R4 rebuild lists.
  **Unknowns:** these original compatibility guards are not a complete native
  ordering validator; do not deserialize menu pointers/cursors/scroll positions.

- **Elapsed time and summary:** serializers derive elapsed whole seconds from
  resident hours/minutes/seconds; the frame remainder is absent. WORLD restores
  only those three counters. Name/job/level/date/location are duplicate summary
  projections, not independent gameplay authority. CARD selects the first Ramza,
  falling back to the last roster slot; WORLD uses its formation list and falls
  back to list entry zero. **Owner/lifetime:** application play-time accounting
  and save catalog metadata. **Restore:** validated duration R2, derive display
  summary after R3; no simulation tick advancement. **Unknowns:** an original
  slot display caps hours at 99, while the live counter has another limit;
  displayed hours must not truncate native elapsed duration.

- **Unknown byte and storage-only fields:** `g_save_unknown_byte` is copied at
  `+0x1c88`; its purpose is unknown (header documents original value `c5`). Image
  padding, parity, PS1 icon/title/header and slot marker are storage artifacts.
  **Owner/lifetime:** unresolved compatibility data for the byte; platform codec
  for transport fields. **Restore:** no invented native gameplay field. Keep an
  explicitly versioned compatibility extension only when a supported profile
  needs it; native checksum/envelope replaces card transport. **Unknowns:** do
  not name the byte an RNG seed or infer behavior from its numeric value.

## Original availability and resume routes

- **Title Continue:** `src/open/title.c` requests an empty party context through
  `new_game_party_init(PARTY_NEW_GAME_EMPTY, 1)`, then `save_screen_run(0)`.
  Zero disables save/load mode selection and loads only. Cancel/error returns to
  title. A successful load selects WORLD if formation-return is nonzero or
  next-scenario is zero; otherwise it goes directly to BATTLE through MAIN.
- **World-map Save menu:** `src/world/screen/runtime.c::world_map_save_request`
  stages the request; `save_request_update` fades/context-switches and calls
  `save_screen_run(1)`. The mode selector permits Save or Load. Saving/cancel
  returns to the world view; loading sets `WORLD_MAP_FLAG_RESTART`.
  `src/wldcore/menu_levels.c` chooses restart-vs-battle using the same
  formation-return/next-scenario condition. `src/wldcore/entry.c` reconstructs
  map state instead of loading a saved menu stack.
- **Authored story/inter-battle checkpoint:** BATTLE and WORLD
  `src/*/event/interpreter.c::script_execute_event` implement
  `CallFunction(OPEN_SAVE_MENU = 6)`, unless progress effects are suppressed.
  They stop helper slots 2..14, set next-scenario to 1 except at `EVENT_ID_EPILOGUE`,
  set save-in-progress and request CARD. Their frame loop calls
  `card_entrypoint(0)`, which offers saving, not loading. The caller clears
  next-scenario again after the menu returns. Thus a saved checkpoint can contain
  a pending next-scenario route different from the still-running source session.
  `scenario_load_conditionals` advances that route on load, including special
  Military Academy, chapter-change and post-funeral/epilogue branches.
- **Not an arbitrary battle suspend:** these serializers omit CT, active unit,
  battlefield positions/HP/status/timers, action queues, interpreter PC/stacks,
  cooperative threads, dialog phase and input. The source Save operation above
  is an authored checkpoint; it does not establish saving during an ordinary
  unit turn or restoring the same instruction. A special event that offers a
  save must be traced with its script and party-commit order before support.
- **Not yet enumerated:** this investigation establishes entry mechanisms and
  route state, not a retail list of every script offering Save, every world-menu
  suppression condition, or all multi-battle/ending/optional-content checkpoints.
  Those require decoded verified event/location/menu content and independent
  original/emulator observations. Do not advertise “save anywhere” or blanket
  save availability in all cutscenes/locations from these source functions.
  Tutorial RAM swaps and sound-novel internal reading snapshots are separate.

## RNG: absence is not reset

Two streams have different owners/lifetimes. Neither actual generator state is
copied by either save-image serializer; copying script variable `21` does not
capture the generator supplying reads of that variable.

### BIOS rand stream

- `src/main/startup.c::boot_startup_run` calls `srand(1)` once on power-on, after
  installing the VSync callback. `boot_session_reset`, new-game party creation,
  `save_image_build` and `save_image_restore` do not seed it.
  `src/main/entry.c` calls startup once; soft reset re-enters the game loop,
  not that startup call. Choosing a new game in a running process is therefore
  not a demonstrated `srand(1)` boundary.
- `src/main/system_callbacks.c::system_vsync_callback` calls `rand()` on every
  callback and then advances play time. Save/load screens themselves present
  frames. Loading preserves the **then-live BIOS generator**, advanced by time
  and other callers, rather than restoring the saved generator or reconstructing
  it from elapsed seconds. Successful load replaces whole-second counters but
  leaves the current `g_save_play_time_frames` remainder untouched. There is no
  saved draw count. Failed/canceled loads also consume menu time.
- `src/world/breeding.c::breeding_update`, called by daily calendar advance,
  explicitly reseeds BIOS rand with `frames * 101 + seconds * 10 + minutes + hours`
  before egg/breeding work. This later gameplay reseed is not a load reset.
  Saved whole seconds alone cannot reproduce its unsaved frame input.
- The reference `src/platform/psyq/libapi/rand.c` and `srand.c` are BIOS veneers;
  algorithm verification is recorded separately in
  [the BIOS RNG investigation](../behavior/bios-rng-verification.md). This ticket
  does not re-execute that oracle or infer generator state from save padding.

### Event-script stream

- `src/lib/event/script/control.c` owns static `s_script_rand16_state` in the
  scene overlay. `script_random_seed` assigns `VSync(-1)`; `random16_next`
  calculates `(state * 0x6255 + 0x3619) & 0xffff` and supplies odd variable commands
  whose source is logical `EVENT_SCRIPT_VAR_RANDOM_VALUE` (`21`). Initial seed
  storage is signed 32-bit; draws reduce it to the 16-bit recurrence. It is not
  the resident script-bank word or BIOS stream. `target/battle.yaml` and
  `target/world.yaml` locate distinct scene-overlay copies.
- `src/lib/event/scene/runtime.c::menu_subsystems_reset` calls that seed function.
  WORLD `screen_world_text_init`, `screen_open_text_init`, resource setup and
  `screen_context_reset` call this reset. New-game party creation first calls
  world-text setup; name-entry flow also uses open-text setup. These are actual
  reseeding callers, not a rule that “new game means event seed 1.”
- The WORLD Save/Load browser calls `formation_display_init`, which calls
  `screen_context_reset`; browser exit resets context again. World request
  handling resets before/after too. Opening, saving, loading **or canceling**
  that browser can reseed event RNG from current VSync. Title Continue shares
  that browser. Direct `save_image_restore`, including tutorial partial restore,
  does not itself reseed.
- BATTLE resume uses `map_load_run -> deploy_open -> menu_subsystems_init ->
  menu_subsystems_reset` and therefore reseeds for deployment/scene setup.
  CARD's own entry/graphics/write path does not call `menu_subsystems_reset` or
  seed the parent scene generator; its source caller resumes the scene after
  returning. Future scene transitions can reseed again. No saved PC/thread state
  exists to require resuming the saved point in the event random sequence.

**Native policy decision remains explicit.** A deterministic native continuation
may preserve RNG states and draw counters as the battle proof does. That would
be a deliberate native checkpoint contract, not a faithful claim about original
campaign load. Original compatibility requires modeling which live streams
persist, which scene boundaries reseed, and externally supplied VSync/frame seed
inputs/draw ordering. Do not derive a seed from a file timestamp, wall-clock
time, checksum or saved whole seconds. Before original campaign RNG support,
record a named policy and replayable seed inputs, with independent observations
of title load, in-session load, canceled browser, new game and daily breeding.
The engine owns only generic RNG interfaces; FFT owns both algorithms/policies.

## Proposed native format and Windows workflow

Keep three responsibilities separate: Core owns logical campaign state and pure
validation/restoration; Content owns profile bindings, explicit persistence DTOs,
version migration and file access; Client owns Save/Load commands, slots, prompts
and presentation. Zodiac gains no campaign/card/UI responsibility.

- An envelope identifies format/schema, selected profile/content compatibility
  versions and checksum. A file cannot select arbitrary content or executable
  scripts. Continue using stable game IDs and validate against caller-selected
  definitions. ZOD-049/053 own the broader content dependency/version policy.
- A checkpoint carries a named kind, resolved location/scenario/continuation ID,
  party/loadouts/progression, inventory/stock, declared world variables/flags,
  calendar/history/proposition state, applicable preferences and elapsed duration.
  Add typed side-content records only as their profiles are supported. Preserve
  unknown compatibility values in an explicitly bounded extension if required;
  do not silently discard them while claiming full campaign saves.
- Keep a native deterministic replay continuation separate from original campaign
  reload semantics. An algorithm/policy ID and explicit seed/state inputs are
  required whenever a supported profile uses RNG. Omit the RNG capability for
  a proof that performs no random operations; never fabricate a PS1 seed.
- Allow saving only after the session publishes a supported atomic checkpoint.
  Reject unsupported mid-action, script, travel, reaction or battle-suspend phases
  with a reason. A first world checkpoint is not an implementation of every
  original save opportunity. Autosave/quicksave/save-anywhere are later opt-in
  product changes with their own recovery/checkpoint contracts.
- Windows slots use an application save directory under the user's local data
  folder and application-generated file names. Slot identity is separate from
  display names and filesystem paths. Show character/location/date/play time,
  checkpoint kind, content/profile and compatibility/error status; derive/validate
  summary against the snapshot instead of trusting a separately editable label.
- Save requires an explicit chosen slot, overwrite confirmation and a snapshot
  captured on the session boundary. Serialize/validate before writing; keep
  the previous file until the new sibling is flushed and promoted. Extend the
  existing safe-write approach with an explicit backup/recovery policy under
  ZOD-053. Report failure without losing the live game or last valid save.
- Load validates/migrates into a separate candidate and resolves all definitions
  before replacing the session. In-game Load confirms discarding unsaved progress;
  cancel/failure leaves it intact. Recovery from a verified backup is explicit
  and never silently rewrites the damaged source. Keyboard/mouse/controller
  navigation belongs to Client, with no PS1 card slot/format prompts required.

## First implementation child and validation

**Selected child ZOD-068: prove one synthetic world checkpoint with a headless
Save/Load application service.** Dependencies: ZOD-035's codec/file foundations,
this investigation, and a narrowly declared campaign state/content profile under
ZOD-041/039. Do not block it on completing those whole epics. The child must
define the few immutable job/item/location IDs it uses before serializing them.
It is independent of ZOD-064's decoder and does not require a full event executor.

Bound it to an idle synthetic world location, a small authored party, equipment,
inventory/funds, one calendar date, one progress flag and elapsed duration. Give
the checkpoint a stable route ID and reject all other phases. Add no encounters,
propositions, sound novels, arbitrary scene-state restore or original-card import.
No RNG draws occur in this first profile; RNG capability is explicitly unsupported
until its policy/seed inputs are selected. This cannot be labeled an original
campaign save or accepted as complete ZOD-053.

Acceptance checks for that child:

- Fresh composition resolves every stable ID and reconstructs identical supported
  state; a subsequent synthetic command has identical state/events. No regeneration,
  equip side effects, day advancement or hidden random draw occurs during load.
- Party/equipment/inventory/flags/date/route cross-field validation rejects
  mismatches, duplicates, unknown IDs, unsupported checkpoints and limits before
  live mutation. Report unsupported families instead of ignoring them.
- Commit invented predecessor/current schema fixtures; migrate known versions
  without rewriting the input. Reject unknown future versions, wrong content/
  profile, checksum failures, malformed/truncated/oversized input and stale summary.
- Validate safe create/overwrite and preservation on serialization, flush/promotion,
  access/sharing failures. Failed/canceled load leaves the live session unchanged;
  failed writes preserve the previous file; recovery selects only validated data.
- Prove slot catalog entries cannot supply filesystem traversal or select content;
  concurrent/stale overwrite protection has an explicit service contract.

**Following UI child ZOD-069:** after that service and ZOD-042's minimal client navigation
flow exist, add a Windows slot list, details, Save/Load, overwrite/discard prompts
and error/recovery states. It should call the tested service and honor its
checkpoint availability; it must not invent campaign state through UI code.
Exercise keyboard/mouse/controller navigation separately from headless state tests.

## Verification and remaining evidence

Source inspection cross-checked both serializer field lists against `save_image_t`,
WORLD's inverse and the selected global writers/readers; traced full/partial load,
title/world/story callers and all source `srand`/`script_random_seed` call sites.
An independent text check confirmed matching sets of 33 top-level image members
in CARD/WORLD serializers and no RNG seed call in direct WORLD restore. All 11
pinned source links resolve to local reference files; document links and whitespace
checks passed. These checks validate the investigation, not original execution.
The reference HEAD was verified read-only. No original data was written or added
to Git. Application code/tests are unaffected by this document.

Still required before original campaign support: verified content-level checkpoint
enumeration and party-commit ordering; original/emulator save/load/new-game RNG
observations; every unmodeled field/side-content schema and its validation;
explicit Windows RNG continuation policy; and native UI/recovery implementation.
ZOD-035's thirteen battle checkpoints do not substitute for any of those checks.
