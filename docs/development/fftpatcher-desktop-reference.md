# FFTPatcher desktop reference — 2026-10-26

The owner requested a brief inspection of the local FFTPatcher tool as a reference
for job and ability editing, while limiting further editor work to what advances
the rest of the project. Further UI polish and owner review are deferred; the
implemented ZOD-047 revision remains unapproved and M3 remains open. This does not
block unrelated gameplay development. Revisit the editor when a selected gameplay
or content milestone needs a supported authoring capability, or the owner asks.

## Reference identity and observation

- Executable: `D:\pi\temp\FFTPatcher 0.497\FFTPatcher.exe`.
- Observed window title: `FFTPatcher (v0.497.0)`.
- Executable length: 682,496 bytes; file version: `1.0.8535.31902`.
- Executable SHA-256:
  `fa64aaad459dacec625343904e154972004fba97840c6c4bf51dfca40d20a075`.
- The owner identified File → New PSX patch as the original SCUS-94221 dataset
  and authorized inspection of the already open session with those values loaded.
  The agent inspected that existing session; it did not reset it with New PSX
  patch or independently compare its defaults against the original BIN.
- This supersedes the initially suggested v0.495/TLW patch reference. No TLW
  values are selected as original-game evidence by this pass.
- Local decomp HEAD at this inspection:
  `11464943bbdbd2a0af022d90cb7f10455cf911df`. No decomp rule analysis or new rule
  translation was performed in this desktop pass.

Only window layout, category/record selections and reference navigation were
changed. No record fields were edited and no patch was opened, created or saved.
No tool code, assets or data catalogs were copied into the project.

## Observed workflow

The tool presents separate category pages with indexed, named record lists:

- **Jobs:** skillset reference, stat growth/multipliers, Move/Jump/evasion,
  innate abilities, equipment permissions, elemental properties, and separate
  innate/starting/immunity status groups. A reverse-use link identifies ENTD
  references. The inspected record was `01 Squire`, not ordinary Squire `4A`.
- **Skill Sets:** ordered action slots, a separate reaction/support/movement
  group, empty slots and a reverse-use job link. Clicking the inspected job's
  Skillset label navigated to `19 Guts` on this page.
- **Abilities:** learning cost/flags, AI options, effect reference, targeting
  dimensions, ticks/MP cost, elements, formula parameters and additional flags.
  `0001 Cure` displayed a reverse-use link to skill sets, including White Magic.

The useful future patterns are separate editors for these three relationships,
grouped fields, IDs alongside names, related-record navigation and “used by”
links. The existing editor already separates Jobs and Abilities; this observation
does not select another UI implementation or expand its package contract.

The loaded defaults are an owner-identified original-data reference. GUI labels
and values alone do not establish execution order, arithmetic widths, formula
behavior, RNG behavior or runtime equivalence. Any new implemented fields or
rules still require their own traced, bounded milestone and validation.

## Project disposition

Retain the [current editor guide and validation](editor-workspaces.md). Its last
implementation validation remains **2,349 .NET / 293 Python tests**, with owner
visual/manual approval pending. No implementation suites or native editor checks
were repeated for this reference/documentation pass. Broad ZOD-048 authoring,
public replacement contracts, display/settings/audio and quirk work retain their
documented scope and deferrals. Select the next bounded gameplay/source/data
milestone from the backlog independently of the deferred editor review.
