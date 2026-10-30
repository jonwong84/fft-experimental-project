# Shared-prefix Attack / Throw Stone decisions — ZOD-192

Investigated 2026-10-29 at clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df`; managed base `48a5aa8`.
The [receipt](../evidence/ai/two-entry-contract-v1.json) pins four files/ten
functions and inherits [Throw Stone scanning](zod-190-throw-stone-ai-scan.md)
and earlier cursor/rank/selection evidence. Five
[isolated descriptions](../fixtures/behavior/ai-two-entry-contract-v1.json)
use supplied ranks and evaluation costs; they do not execute a managed scan,
C or the original, calculate formula costs or establish a full decision schedule.

## Shared and per-entry state

`action_select` initializes eight rank slots and fresh working defaults once.
`general_select` builds movement scenarios, scores tiles and seeds D0 once;
no charging action survives in this closed profile. It selects active targets,
runs `ability_list_evaluate`, then the shared lottery. `usable_abilities_build`
places Attack first for a non-monster; a learned Throw Stone can appear later.
The bounded pair requires an explicit certificate that these are the two
consecutive admitted entries and that intervening work draws nothing. It does
not infer a real skillset/list or silently omit other usable entries.

The cursor increments after each completed entry; loading the next entry clears
considered-ability storage, not shared movement scores, D0/D1, proximity, ranked
actions or all target-setting flags. Each admitted entry restores active target
eligibility. The Attack exhaustive-origin pass clears consume-aim; prepared
Throw Stone sets it again. Cast/aim masks are rebuilt per pass. Original units
are restored between simulations; RNG carries forward through every supported
evaluation, including discarded/NotValuable candidates.

`ai_best_rank_insert` first compares best against D0 by marker/priority/hit,
without proximity. If strictly above, it inserts before the first lower-ranked
slot using priority, accuracy, then lower final-destination proximity. Equal
rank/proximity retains the earlier entry first; a later equal action can still
occupy the next empty slot. No destination/identity deduplication occurs. After
the insertion attempt the working best's rank/defaults reset, while the ranked
list survives. A second NotValuable pass must never reinsert the first best.

## ZOD-193: fresh two-entry ranking

Own a single supplied movement-score/scenario mask, D0 and D1 and original
actor/target. Admit both existing family profiles before simulation. Require
the same ground, opponent IDs, baseline death counters, active target context,
fresh decision/best defaults, certified Attack-then-Throw Stone order and fresh
empty ranks. Existing weapon ownership, numeric envelope, ability grant,
normal-coordinate/source-cell and clear-direct-path guards remain in force.

Extract shared internal movement/proximity work and internal fresh scan
continuations. Public APIs continue to own fresh work and accept no cached
results. Compute movement scores/proximity once in source map order; run Attack
then Throw Stone on original units while carrying private RNG. Keep eight
compatibility slots with at most two real actions. Working best is local per
entry, so no payload from a preceding entry can become its new best.

Use a closed typed hypothetical payload for Attack versus Throw Stone, carrying
each family's validated candidate diagnostics. This is a real two-family
consumer of shared data, not a replacement-policy interface or executable
command. Preserve existing Attack APIs/admission; shared score/proximity values
must have the same results. Compare the selected final destination, including
ACT-first D1, when inserting Throw Stone. Return reasons only and whole-query
incoming RNG on failure, even if the earlier entry had already inserted.

Check literal ranking/order/costs, both stronger-entry directions, complete ties,
NotValuable, empty scans, profile disagreement, late failure and replay. No
generic list traversal, scenario derivation, blocked-path search or authority.

## ZOD-194: fresh pair selection

Own a fresh ZOD-193 query, then reuse a shared internal source lottery. Require
terminal Throw Stone, no other retained/searchable entries, no Defend support
and fresh ordinary selection defaults. The ranked array remains contiguous
with positive-hit real actions followed by empties. No public cached-rank input.

`ranked_select` draws real BIOS RNG for each visited slot, including empties,
and selects on `raw % 100 < hit`. A failed earlier entry can select the second.
If every roll fails but any action has nonzero hit, select the first; if all
slots are empty, consume all eight draws then return move-only at D0.
`fallback_select` would choose Defend when supported, so that remains excluded
by an explicit certificate. Keep deferred Wait facing and hypothetical order/
destination; do not consume live choices, adopt RNG or issue command receipts.
Lottery exhaustion restores the entire query RNG with no partial ranking/plan.

Factor the old Attack-only lottery through the same internal arithmetic while
preserving its public result/rejection types. Test second-slot selection,
all-failed first fallback, empty movement fallback, exact/short budgets and
input isolation. Original eight slots describe compatibility behavior rather
than an authored content/roster limit. Frame/VSync RNG equivalence stays open.

## Reproduction

`verify_ai_two_entry_contract.py --reference C:/repositories/fft_decomp
--prepared-executable scratch/all-data-extraction/SCUS_942.21 --output <new report>`
checks inherited receipts, pinned source, selected prepared facts and the five
rank/cost descriptions. Two Python scenarios check literals/input isolation,
malformed descriptions and tampering. Logs: ignored `scratch/ai-two-entry-192-194/`.
All 524 Python tests and the clean source/prepared-data audit pass. ZOD-192
contains no managed implementation; last full runtime validation remains
ZOD-191's clean build and 4,013 passing .NET cases.
