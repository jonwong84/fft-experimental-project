# Original FFT bug catalog

Collect reported or suspected bugs in the North American PS1 release
`SCUS-94221` here. This is the working list for [ZOD-070](BACKLOG.md#zod-070--plan-and-implement-selected-original-game-bug-fixes).
Fixes are later work; adding an entry does not change the current implementation
queue. Bugs in our managed implementation belong in separate development tickets.

Start with a name and a short description. Unknown details can stay `TBD`;
you do not need to research or prove an issue before adding it. Use stable IDs
`FFT-BUG-001`, `FFT-BUG-002`, and so on, without renumbering existing entries.

Useful details to add as they become available:

- What the original game does and how to reproduce it.
- Where the report came from: your observation, a community source, or source study.
- Whether it applies to US PS1, another edition/mod, or an unknown version.
- What you would like changed, keeping that proposal separate from observed behavior.
- Related mechanics, duplicate entries and eventual implementation tickets.

Use **Reported** initially, **Investigating** during research, **Confirmed** once
evidence supports it for the target edition, **Deferred** when deliberately parked,
and **Fixed in selected profile** when the correction is implemented and tested.
State the evidence kind explicitly: source-derived, executable-reference,
original/emulator observation, or unverified report. Source analysis should record
the exact decomp commit and function; original media/captures stay under ignored
`scratch/`, with only their identifying metadata linked here.

Original compatibility remains the default. Selected corrections belong to explicit
FFT rules/profile options, with separate original/corrected regressions. A report
does not establish that a behavior is unintended or that a proposed fix is final.

## Entries

Use the first entry as an example. Unknown details can remain `TBD`.

### FFT-BUG-001 — Blade Grasp evades ranged weapon attacks

- **Status:** Reported
- **Edition:** `SCUS-94221` (applicability to be verified).
- **Original behavior (reported):** Blade Grasp can activate against ordinary
  Attack commands made with ranged weapons and prevent those attacks from hitting.
- **Reproduction (to confirm):** Equip an attacker with a ranged weapon and a
  defender with Blade Grasp. Use the ordinary Attack command against the defender.
  Record the weapon, positions, relevant unit stats and whether Blade Grasp
  activates; an attack missing by itself does not establish the cause.
- **Evidence/source:** Unverified owner report. Source citation and controlled
  target-edition reproduction: `TBD`. Original design intent is not established.
- **Desired correction (proposed):** For ordinary Attack commands, restrict Blade
  Grasp eligibility to attacks made with melee weapons. Determine eligibility by
  weapon category, not attacker-to-target distance; a ranged weapon used against
  an adjacent target would remain ineligible. Preserve other activation conditions
  unless a separate change is selected.
- **Open questions:** Exact eligible weapon categories and any exceptions: `TBD`.
  Ability-based attacks are outside the initial proposal and need separate review.
- **Validation to add:** Reproduce the reported behavior in the original profile;
  test eligible melee and ineligible ranged weapon attacks in the corrected profile,
  including adjacent ranged attacks and unchanged activation conditions.
- **Related entries/ticket:** Parent: ZOD-070. Implementation child: not assigned.
