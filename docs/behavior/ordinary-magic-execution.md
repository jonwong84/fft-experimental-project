# Ordinary magic execution

## ZOD-387

Trace starts at managed `695355fa2b3e5ef86a791f23d1851a01e5f5bdb1` and clean
read-only reference `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[source receipt](../evidence/combat/status-refresh-source-v1.json) fingerprints
eight files/15 functions and selected verified status records. The independent
[refresh descriptions](../fixtures/behavior/status-refresh-contract-v1.json)
are source-model checks, not execution of C or the original game.

`status_current_update` is exactly innate OR inflicted; immunity does not remove
an already-inflicted status here. `stats_refresh` first rebuilds current, saves
current/inflicted, reloads job and equipment status definitions, restores saved
inflicted, temporarily removes formation membership, reconciles, restores
membership and rebuilds current. Initializing mode is separate and uncalled in
this checkout; formation refresh skips Critical and display notifications.

Reconciliation adds movement Float to innate and removes innate bits from
immunity. It rebuilds current as innate OR inflicted. Only inflicted timed
statuses (Poison through Death Sentence) with zero CT are initialized. Existing
nonzero CT survives; innate-only timed statuses are not seeded by that loop.
`status_ct_set` never restarts an already-counting Death Sentence, clears timed
CT on removal and resets Dead's death counter even on removal (3 normally,
255 for immortal/save/load-formation flags). No random draw occurs here.

Normal refresh sets or clears **inflicted** Critical at HP<=MaxHP/5, then rebuilds
current. Innate Critical can therefore remain when inflicted Critical clears.
Display differences compare the initially rebuilt current with the final current
in ascending source status order; handler index is status ID+1. Only Execute
publishes display flags; Preview/AI still calculate state. Status timers and
gameplay state are independent of presentation timing.

Target-result application uses a different path: pending removals/inflictions
are filtered through immunity, current/innate, cancel and can't-stack rules;
removals clear inflicted bits/timers, inflictions may cancel others. Then Dead
forces HP0, non-Charging/Jump/Performing clears charge CT, and control/team flags
are reconciled. The pending action change record is restored afterward. Plain
refresh must not be substituted for that action applicator. The selected verified
Dead/Charging/Chicken/Critical records establish the bounded Fire path; broad
status handlers, riders, Golem and reactions remain separate.

## End-to-end acceptance

Owner direction on 2026-11-06: complete this trace, then make one spell work end
to end before continuing development. Fire is the selected ordinary spell.
Completion requires prepared verified spell content, explicit learned grant,
player aim/confirmation, stored charge and deterministic readiness, current-input
formula/evasion/proc calculation, one MP payment, actual HP/status/RNG and reward
publication, replay/restart and a runnable client path. A further private proposal
or only a calculation test does not meet this acceptance.

Use a dedicated coherent ordinary-magic battle state; preserve existing sword/
AI sessions and private projection APIs. Admit only explicitly represented source
facts. Handle Critical, KO and paid empty-target/resource failures explicitly;
do not refund an executed failure by calling it unsupported. A synthetic first-KO
encounter may end the demo before deferred corpse/crystal/objective scheduling.
Keep original spell rules/data separate from that invented encounter objective.
Native host work is authorized by this concrete gameplay need; no editor,
automatic facing, broad interfaces, save checkpoint or custom cone is activated.
Knight equipment-break mapping and all owner drafts/JobStats remain preserved.

### Selected work

- ZOD-388: bounded ordinary status state/application and refresh for Fire's known
  empty-status, no-reaction, no-innate/RSM/equipment-special context.
- ZOD-389: coherent magic battle commands, charge clock, actual vital/RNG/reward
  adoption and replay, including paid fizzles and KO.
- ZOD-390: verified prepared Fire content and grants without original runtime I/O.
- ZOD-391: runnable player casting UI and exact headless/replay checks.

After Fire acceptance, continue source-traced development within the owner's
five-hour work window. Full status breadth, corpse/Golem/terminal/frame RNG,
general magic AI and earlier documented deferrals remain separate.

## ZOD-388

An internal ordinary application kernel now admits only coherent living
Critical/Charging state with Brave10..100, no innate/immune/timed/special status
context and known empty-result work. It reuses signed vital arithmetic, applies
Critical crossings and KO, clears Critical/Charging on KO, wraps the byte KO
counter and sets death counter3. The HP-zero early return retains TARGETED while
normal changed targets become AFFECTED. Misses retain fields/counters but assign
TARGETED. This helper grants no live publication authority; the following battle
definition/command boundary owns admission and adoption. Full status handling is
not enabled by a caller-provided flag.

## ZOD-389

`OrdinaryMagicBattleDefinition` now admits coherent human bare-equipment setups,
copied raw/growth/multiplier/base facts, complete careers/unlock rules and explicit
learned spell grants. It validates numeric maxima and rejects unsupported
statuses/RSM/affinities/equipment instead of silently dropping them. New immutable
battle state owns current HP/MP/statuses, careers, one RNG checkpoint, ordinary
turn choices and pending charge plans. Only Framework commands create successors;
no public state constructor or adoption of arbitrary projection results is added.
The old sword/AI state and private magic projection API remain unchanged.

The [clock/command source receipt](../evidence/combat/ordinary-magic-clock-source-v1.json)
pins seven files/16 functions. The bounded event pump adds CT, serves all ready
units by CT/source order with one upkeep draw each, decrements **all** charge
counters, then resolves zero counters in source order without ticking the
remaining zero counters twice. No normal turn is consumed at delayed readiness.
The source Quick/Mime/timer/terrain branches are outside this admitted profile.
Queueing consumes ACT and stores Charging without spending MP; Step is an
explicitly bounded one-tile Move and Wait settles the turn. Current anchor
positions drive follow targeting without repeating old blue range checks.

Readiness clears Charging, performs the current MP check, calculates complete
source-ordered frames through existing formula/evasion/proc owners and publishes
HP/MP/status/RNG/careers. A valid empty area spends MP and resolves with no formula
draws; insufficient current MP ends the pending cast without spending MP or
granting rewards. Initialization failure does not enter the controller's normal
post-strike reward path. Fire's separate miss actor work assigns TARGETED even
after self-target work. Source reward owners then update EXP/JP, all eligible
recipient slots, semantic unlocks and any five-draw raw growth/derived Critical
refresh. Full numeric/career adoption now happens in this command path.

The demo ends at its first KO, explicitly an invented encounter objective; it
clears remaining demo commands before deferred corpse/terminal scheduling. The
ordinary target's source KO count, death counter, markers and player-fallen flag
are retained. Diagnostic counter exhaustion rejects an entire private command
and exposes no state/events; this is managed admission policy, not a claim that
original sequential mutation was atomic. Nine managed scenario cases cover
charge/readiness, real HP/MP/EXP/JP publication, moving follow/tile fizzles, two
simultaneously ready casts, KO, level growth/unlocks, counter-prefix rollback,
admission, resource failure and deterministic replay/restart. Prepared Fire and
the player host are the next acceptance gates.

## ZOD-390

The offline `prepare-fire-battle` command verifies SCUS-94221 executable length
and SHA-256 before reading 34 selected records: Fire's primary/secondary data,
empty infliction record0, Black Magic/default-menu binding, Wizard/Squire jobs,
19 unlock rows and eight JP thresholds. The pinned reference above supplies the
layouts. Fire has power14, Fire element, range4/area1/vertical1, charge4 and MP6.
Its secondary Evadeable and Silence flags drive the existing runtime rules.
Unsupported spell flags/status work or selected job innates/affinities fail closed.
Raw records and exact input provenance stay under ignored scratch.

The output contains a versioned strict JSON document and SHA-256 manifest.
Runtime loading reads only these prepared files. Immutable `JobDefinition` /
`JobStats` supplies copied growth/multiplier inputs without changing owner types.
Explicit careers and learned spell IDs are validated against the runtime catalog;
the loader snapshots all mutable wire collections. MP costs remain integers.
The flat map, Mage/Guard units, starting careers, Fire grant, seed and first-KO
objective are authored demonstration setup, not an imported original encounter.
Mage starts at EXP95 so the first successful cast also exercises raw growth.

Nineteen content cases cover strict schema, nested validation, immutable copies,
manifest integrity, integer costs and actual loaded-content battle effects.
Eleven CLI cases cover synthetic layout decoding, provenance, unsupported source
dependencies and wrong-edition failure before publication. The real verified
local input prepares successfully; no prepared assets are tracked in Git.

## ZOD-391

The dedicated `--fire-battle` client loads the prepared package before opening a
native window. Its player controller derives blue range/area displays and submits
Cast/Step/Wait/Advance commands; it does not apply private formula projections.
Unit-follow/tile selection, confirmation, pending charge, one-time MP payment,
HP/Critical/KO, careers and level growth are visible. Rendering and elapsed host
time never advance the battle. A press-edge buffer and pointer/letterbox mapping
serve keyboard and clickable controls. Restart creates a fresh definition state.
The existing sword/AI clients are unchanged.

Six managed player-route cases cover selection without publication, current spell
effects, rejected inputs, paid empty-tile resolution, restart, KO and exact replay.
The Windows GPU check exercises those real routes through a resized window,
captures eight checkpoints and compares complete gameplay receipts with headless
replay at initial resolution, restarted resolution and KO. Seed57's first cast
hits for54, pays6 MP, earns10 EXP/12 JP and raises Mage from level10/EXP95 to
level11/EXP0, including five raw-growth draws. Four casts finish the authored
first-KO encounter. Aim, resolved and finished captures were visually inspected.

This completes the owner's bounded Fire end-to-end acceptance. The prepared data
and rules are original; the encounter and diagnostic presentation are authored.
No original-game executable/C execution, physical input injection, original
artwork/animation/audio or full-game equivalence is claimed. Full statuses,
reactions, generic magic AI and prior explicit deferrals remain open.
[Launch, controls and verification](../development/fire-battle-controls.md).
