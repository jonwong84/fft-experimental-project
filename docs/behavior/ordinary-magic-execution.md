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
