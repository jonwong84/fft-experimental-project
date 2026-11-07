# Shared elemental spell execution

## ZOD-392

After [Fire end-to-end acceptance](ordinary-magic-execution.md#zod-391), the next
bounded family is Fire/Bolt/Ice, tiers1–4. Source is pinned to clean read-only
`11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[content source receipt](../evidence/combat/ordinary-magic-content-source-v1.json)
fingerprints six files/five functions. Twelve independently reviewed
[binding vectors](../fixtures/behavior/elemental-spell-bindings-v1.json) record
the selected differences; the prepared files remain ignored local output.

All twelve IDs16–27 use formula0x08 and empty infliction0. The one-based table
in `resolution.c` dispatches to `formula_magical_damage`: magic evade, MA/Y,
strengthen/modifiers, element/weather, Faith and conditional status proc. These
spells reuse the existing rule and command owners; no per-spell execution classes
or duplicate damage calculators are introduced. Tier1–3 use area1 with vertical
tolerances1/2/3, powers14/18/24 and charges4/5/7. Tier4 uses power32, charge10,
area2/vertical3 and CannotHitCaster; its secondary attributes omit Reflectable,
Calculator and CounterMagic while retaining Silence/quote/Evadeable. The admitted
bare/no-RSM context makes those omitted branches inactive. Bolt2 costs10 MP;
Fire2/Ice2 cost12. The independently reviewed rows preserve that difference.

`OriginalOrdinaryMagicImporter` replaces the initial Fire-specific importer and
selects either Fire alone or the twelve elemental tiers. The existing
`prepare-fire-battle` command retains its one-spell encounter; the new
`prepare-elemental-magic-battle` prepares the larger selection. Exact edition/hash,
every selected secondary field, empty status, Black Magic slot/default-menu binding
and selected job/progression data are checked before publication. Unknown spell
profiles fail closed. The output preserves explicit source slot order and grants
all selected spells only to Mage. Its authored Wizard career starts with total5000
JP and deducts the4740 JP learning cost, leaving260; this is demonstration setup,
not a claim about an original unit's career or campaign learning flow.

The source learned menu enumerates action slots0–15 in slot order, filtering
learned bits before requirements and deriving current MP/CT. This work records
that ordering as immutable prepared `SpellOrder`; the following player selection
child consumes it. Original menu presentation, transformed/RSM requirements and
general magic AI remain separate. Runtime never reads original tables.

Seventeen new managed cases run every tier through charge/readiness and real
damage/MP/reward/growth publication, exercise the three radius2 caster exclusions,
explicit grant/career accounting and invalid slot/flag rejection. Two Python
cases validate source descriptions; the fresh source/prepared audit verifies all
twelve records and manifest integrity. Long CT10 casts also reach Mage's ordinary
tick18 turn while charging; Wait preserves the cast, and the additional upkeep
draw is included. No runtime formula/clock/status breadth is added here.

Later [primary metadata binding](ordinary-spell-metadata.md) identifies the tier4
LearnOnHit post-action stage as a remaining compatibility gap. Numeric execution
above does not establish complete post-learning marker/report equivalence.

## ZOD-393

The ordinary client now consumes prepared `SpellOrder`, filters it by the active
unit's explicit learned grants and displays the resulting list with MP/CT.
Keyboard Tab/Up/Down and pointer rows select a spell; changing selection clears
the old aim. Range and area come from that selected definition, and confirmation
submits its stable ID to the same authoritative battle command. Browsing neither
spends MP nor changes turns/RNG. The old `--fire-battle` path remains available;
`--magic-battle` selects the same player host for the twelve-spell package.

Four new player-route cases cover nonalphabetic authored slot order, filtered
grants, selection wrapping, distinct Bolt2 cost/power/charge, radius2 exclusion,
stale aim, insufficient MP and exact replay. Separate native runs pass for the
one-spell and twelve-spell packages. The latter visits all12 keyboard selections
and pointer rows before running the unchanged Fire/restart/first-KO scenario;
every elemental tier separately executes in managed tests. Resized input mapping,
rendering isolation and complete replay receipts pass. The menu and result
captures were visually inspected. These are programmed routes, not physical
input or original menu presentation equivalence.

Release builds with zero warnings/errors and all **4,839 .NET /741 Python tests**
pass. One initial Python run encountered Windows access denied while renaming an
unrelated banner test's temporary directory; a fresh complete run passes.
Owner drafts/JobStats, the owner's separate targeting-format edit, existing
sword/AI paths and prior deferrals are preserved. Next select Cure's ordinary
healing path using the same player/state owners.
