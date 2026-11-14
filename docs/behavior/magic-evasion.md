# Magic evasion and formula entry — ZOD-342, ZOD-344–347

The [ZOD-436 shared-consumer review](ordinary-magic-pipeline-review.md) is now
complete within its stated domains. Fresh formula entry calls
`MagicEvasion.MagicalEvadeCalculation`; preparation now names
`TransparentCalculation`, `AbandonCalculation` and `EvasionChangesDueToStatuses`.
Public query behavior is preserved. The supplied-trace milestones below retain
their original checkpoint scope.

## Supplied magical-evade wrapper — ZOD-436 support

Owner-supplied FFHacktics documentation cross-checked on 2026-11-07 against
clean read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df` and verified
prepared BATTLE.BIN. The [supporting receipt](../evidence/combat/magical-evade-supplied-trace-v1.json)
records all16 matching instruction words at `0x801885b8`–`0x801885f4`, five direct
calls, the24 listed caller checks and one corrected return address. This is
supporting evidence for the still-open ZOD-436 review.

The wrapper's dependency order is:

1. `0x801851c4` — Equipment Evasion Setting (Magical),
   `formula_evade_store_magical`: reset base hit100/all four contributions,
   then load eligible accessory/shield magic evades. C# owner: `MagicEvasion.Load`.
2. `0x801854b8` — Transparent Calculation,
   `formula_evade_calculate_transparent`: clear all four contributions when the
   **attacker has Transparent**. C# owner: `MagicEvasion.Prepare`.
3. `0x8018537c` — Abandon Calculation,
   `formula_evade_calculate_abandon`: an unrestricted target with Abandon halves
   base hit and records the reaction marker. C# owner: `MagicEvasion.Prepare`.
4. `0x801853f4` — Evasion Changes due to Statuses,
   `formula_evade_apply_target_statuses`: clearing statuses run before Defending's
   base-hit halving. C# owner: `MagicEvasion.Prepare`.
5. `0x80188488` — Calculate Hit %, `evade_roll`: invoke final-hit calculation and
   return whether the result's hit field is zero. C# owner: `MagicEvasion.Resolve`.

The supplied Transparent annotation says Concentration. That is a copy error:
the pinned source checks Transparent status, and the magical wrapper does not call
the separate Concentrate helper. The existing C# uses `AttackerTransparent`.
The wrapper itself does not gate preparation on Evadeable; its final-hit helper
owns that later gate. A returned zero means the hit field remains set, not that
displayed accuracy must be positive. Actual versus preview/AI draw ordering and
the zero-accuracy distinction remain as documented below.

All24 listed callers resolve to this wrapper after correcting formula0x10's
return address from `0x801890e8` (the call delay slot) to `0x801890ec`:
the call is at `0x801890e4`. Also, the supplied display label "3E" beside
`0x8018a430` should read **4E**, consistent with its linked page and the source
formula table. The other23 listed return addresses match directly. These address
checks do not establish managed support for the other formula families.

The owner subsequently supplied the Transparent helper itself. All17 instruction
words at `0x801854b8`–`0x801854f8` and both listed returns (`0x8018536c` from
Dark/Confuse and `0x801885d0` from magical evade) match the prepared bytes; the
same supporting receipt records this check. The helper reads attacker byte
`0x5a`, masks `0x10`, and branches at `0x801854d4` to the epilogue when that mask
is **zero**. Otherwise it calls `mem_zero` at `0x8005e644` with destination
`0x801938de` and length4, set in the call's delay slot. It clears exactly
accessory/right-hand/left-hand/class contributions through `0x801938e1`.
Base hit is unchanged, no RNG is drawn, and no return value is specified.
`MagicEvasion.Prepare` already implements this condition through
`AttackerTransparent`; no runtime correction is required.

The supplied Equipment Evasion Setting (Magical) listing also matches all72
instruction words at `0x801851c4`–`0x801852e0` and its return `0x801885c8`.
The same receipt records its four branches, reset stores and item-table inputs.
Base hit100 and all four zero contributions are stored **before** testing the
monster bit (`unit+0x06`, mask`0x20`). A set bit exits at `0x801852dc`, retaining
those defaults. Otherwise accessory slot`0x1c` requires type bit`0x08`; shield
slots`0x1e`/`0x20` require bit`0x40`. These are bit-membership checks, not exact
type-byte equality. A missing bit skips only that contribution. Primary item
records have stride12; their secondary ID selects stride2 accessory/shield
records, whose byte1 is unsigned magic evade. Class and weapons are never loaded.

`MagicEvasion.Load` already creates fresh qualified contributions and class0,
with monster contributions zero; `Prepare` supplies base hit100. Its separate
blocking-item labels belong to the later final-hit reporting rules, not an extra
weapon evade contribution in this loader. The seven focused .NET evasion tests
(including eight literal equipment descriptions) and eight Python evasion tests
pass. All ten owner draft blobs are unchanged; no runtime or fixture change is
needed.

The supplied Abandon Calculation matches all30 instruction words at
`0x8018537c`–`0x801853f0`, its dependency call and all three listed returns.
`Check If Unit Can Evade` at `0x8018130c` is named
`unit_get_action_restriction` in the pinned decompilation. A **nonzero** return
skips Abandon; zero continues to target reaction byte`0x8e`, bit`0x10`. A clear
bit also skips the effect. Otherwise the unsigned base-hit byte is shifted
right1 and stored, then target action `reaction_id` at offset`0x0e` becomes
`0x01c1` (449). The routine neither doubles the evasion bytes nor rolls Brave/RNG.
It records result work rather than running a separate reaction action.

`MagicEvasion.Prepare` already checks `TargetActionUnrestricted && TargetAbandon`,
halves base hit and preserves the diagnostic through `AbandonTriggered`.
`FaithMagicInputs` carries caller-classified `ActionUnrestricted` and additionally
excludes Don't Act. This is not a complete map/mount restriction implementation:
the source helper also checks ridden-mount and deep-water circumstances, with
status/movement/rider exceptions. Its name must not be used to turn it into a
universal gate for all shield/accessory evasion; later target-status clearing
remains separate.

The wiki's "Physical Evade Calculation (Charge)" listed caller maps to
`formula_evade_calculate_physical_without_weather`. Its call to Abandon is verified;
the parenthetical claim that the caller is never called is not established here.
The same supporting receipt records these Abandon checks. No runtime or fixture
change is required, and ZOD-436 remains open.

### Supplied action-restriction dependency

The owner then supplied Check If Unit Can Evade itself. All45 instruction words
at `0x8018130c`–`0x801813bc` match, including five early-exit branches and the
map-location call. The same supporting receipt records its source/managed inputs
and11 caller checks. The name `unit_get_action_restriction` fits its wider use
in action menus, MP/use checks and reactions. Its return codes follow this order:

1. Don't Act (`status+0x5c`, bit`0x04`) returns1.
2. Being ridden (`mount_info+0x182`, bit`0x40`) returns1.
3. Tile depth0 or1 returns0.
4. At depth2 or more, Float/Chicken/Frog (`status+0x5a`, mask`0x46`), or Move in
   Water/Walk on Water/Float movement (`movement+0x95`, mask`0xc8`), returns0.
5. Otherwise a rider (`mount_info` bit`0x80`) returns0; the remaining deep-water
   case returns2. The rider exception cannot override the earlier restrictions.

Depth comes from the upper three bits of tile byte3, using stride8 tile records.
Map Location Calculation at `0x801810a0` maps to
`battle_map_tile_calculate_unit_index`: higher layer*256 + y*terrain width + x.
The source helper is pinned in the receipt; no new coordinate implementation is
needed for this study. Notably, Move Underwater bit`0x10` is **absent** from the
`0xc8` exception mask. Sleep/Stop and the turn's action-taken flag are not checked
here, and there is no RNG draw. Zero means no restriction from this classifier;
it does not establish universal evasion eligibility or a successful hit.

Nine listed return addresses match directly. Two list the call instruction
rather than its return: `0x80140534` should be `0x8014053c`, and `0x8018caa0`
should be `0x8018caa8`. After these corrections all11 map to verified direct
calls, including Player Control's menu selection, physical Weapon Guard and
Abandon. The physical equipment loader uses the result to qualify Weapon Guard,
not to suppress every shield/accessory contribution.

For the bounded magic API, `TargetActionUnrestricted` represents result==0.
Map/mount classification remains caller-owned; `FaithMagicInputs` also excludes
Don't Act. The current executable ordinary-magic encounter rejects restricted,
mounted and support-bearing input profiles, rather than implementing the full
source classifier. All15 focused .NET evasion/current-input tests and16 matching
Python tests pass; all ten owner draft blobs are unchanged. This check adds no
broad terrain, status or reaction support. ZOD-436 remains open.

### Supplied target-status changes

Evasion Changes due to Statuses matches all49 instruction words at
`0x801853f4`–`0x801854b4`, its four-byte clearing call and all three listed
returns (`0x80188540`, `0x80188598`, `0x801885e0`). The supporting receipt pins
the exact words and five branch targets. The target status masks are:

- Byte`0x5c`, mask`0x14`: Don't Act or Sleep.
- Byte`0x5b`, mask`0x02`: Stop.
- Byte`0x59`, mask`0x10`: Confusion.
- Byte`0x58`, mask`0x09`: Charging or Performing.

Any matching clearing status causes `mem_zero(0x801938de,4)`, clearing accessory,
both hands and class contributions. It does not remove any target status. The
`beq` instructions skip their respective present-status action when the tested
value is zero; terse "If Target has" inline annotations are not branch conditions.

The routine then separately reloads target byte`0x58` and tests Defending
bit`0x02`. If set, it shifts the current unsigned base-hit byte right1 and stores
it. There is no early return after clearing: Defending still applies when any
clearing status is also present. Abandon followed by Defending therefore produces
base hit100 ->50 ->25. Clearing contributions does not undo an earlier Abandon
reaction marker. No RNG is drawn, and the restriction classifier is not called
again here.

`MagicEvasion.Prepare` already ORs the six clearing flags, then independently
applies `TargetDefending`, retaining `AbandonTriggered`. Existing literal
preparations cover each status, stacked halvings and a later clear that retains
the marker. All seven focused .NET evasion tests and eight Python tests pass;
all ten owner draft blobs are unchanged. No runtime or fixture change is needed.
Together with the previously
supplied final-hit/roll documentation, this completes the supplied-document
dependency checklist for the magical-evade wrapper. It does not complete ZOD-436
or activate general statuses, reactions or terrain rules. The next useful page
for formula0x08, Magical Support/Status/Compat, has now also been
[cross-checked](faith-magic-amounts.md#supplied-magical-modifier-wrapper--zod-436-support);
its individual helper pages are the next useful documentation.

Calculate Hit %, Calculate Final Hit % and the per-source roll have already been
supplied during the [physical-evasion review](physical-pipeline-review.md);
their shared behavior should be reused without importing physical-only preparation.
The existing magic source audit passes8 load,15 preparation and24 roll
descriptions. No runtime or fixture change is needed for this wrapper cross-check;
all46 focused magic-contract Python tests pass and all ten owner draft blobs are
unchanged. General status/reaction admission and other deferrals remain unchanged.

## Original source-study checkpoint

Source study 2026-11-05 from managed base
`c1fcf1ad11d85d753eb31cf7603371f9c24221b6`; read-only fft_decomp clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Receipt](../evidence/combat/magic-evasion-source-v1.json): 13 files/21 functions.
[47 independent descriptions](../fixtures/behavior/magic-evasion-contract-v1.json)
cover eight equipment loads, 15 preparations and 24 ordered roll tapes. No
original, C or managed code is executed by the Python description checker.

## ZOD-342

Source trace and selected implementation children.

`action_calculate_target_result` selects the formula and runs
`evasion_reactions_check` before dispatch. Reflect, pending reactions, Blade Grasp
and Arrow Guard can change that entry. They require separate tracing/implementation
before admitting those contexts. A future bounded entry must positively certify
fresh hit/accuracy100 result work and no earlier reaction/reflection effects;
ordinary primary/no-pending and finalizer scope facts remain necessary.

Formula0x08 calls `formula_evade_calculate_magical` before MA/Y or any amount work.
Formula0x0C bypasses this entire chain. Magical evasion loads equipment, applies
attacker Transparent, target Abandon, target statuses, then final accuracy/rolls.
It does **not** apply physical Concentrate, attacker Darkness/Confusion, facing,
bow weather, Weapon Guard, or physical class evade. Damage weather later in the
amount calculation remains a separate stage.

`formula_evade_store_magical` resets unsigned-byte base hit to100 and all four
unsigned-byte contributions to0. Monsters return there. Other targets load
accessory magic evade only for a primary Accessory type, and each shield slot's
magic evade only for a primary Shield type. Job/class contribution remains0.
There is no weapon magic-evade contribution. Blocking-item labels are a separate
concern: the roll reports the accessory item; each hand reports its weapon if it
is not `ITEM_ID_NONE` (255), otherwise its shield. Item0 is distinct from None.
The label can therefore be a weapon even when the evade came from a shield.

Transparent clears contributions. Abandon halves base hit and writes result
reaction Abandon449 only when `unit_get_action_restriction` returns None. That
function checks Don't Act, mount, deep water, statuses/movement and rider facts;
the pure slice will take its classified result explicitly, not reimplement map
or mount rules. Target Don't Act, Sleep, Stop, Confusion, Charging or Performing
clear contributions; Defending then halves base hit. Both halvings stack to25.
Abandon's result marker survives a later status clear or earlier Transparent.
It is diagnostic result data, not execution of a reaction. Caller-supplied status
and classified restriction facts must describe the same target.

## Accuracy, RNG and ordering

The secondary execution Evadeable flag determines whether final checks run for
these nonzero spell abilities. The primary AI Evadeable metadata is separate.
Preparation still runs when checks are disabled, including Abandon's marker.
Disabled checks leave incoming hit/accuracy unchanged and draw nothing.

If any contribution is at least base hit, final checks set hit=false and
accuracy0. Otherwise each factor is integer `(base-evade)*100/base`; the four
factors multiply, divide by1,000,000, then scale the incoming unsigned16 accuracy
by that percentage/100. Do not combine divisions or add an aggregate accuracy
roll. Products fit signed32 in this domain. An accuracy rounded to0 by this
product does **not** itself clear hit here. This differs from the certain-evade
branch, even though the later result finalizer clears hit for either accuracy0.

Roll order is accessory, right hand, left hand, class. Each calls
`random_roll_pass_fail(base, evade)`, using real `rand()*base/32768`, then tests
execution state. Thus zero contributions still draw, and AI/preview draw four
times. Their random outcomes never clear hit or label a miss; the earlier
certain-evade branch can still have cleared hit. Execution stops at its first
successful evade and records the corresponding miss label/item. The class0
check draws even though it cannot evade. For base100/evade19, raw6225 evades and
6226 does not; this is scaled arithmetic, not modulo.

Zero-accuracy work can still enter the damage formula: seed1981 with three90
evades passes all execution checks, reports accuracy0 with hit still true, then
reaches the amount/proc tail before finalization. By contrast a preview with a
certain contribution still draws four times but skips amount/proc work because
hit is already false. A source-accurate coordinator must test hit, not accuracy,
at that branch.

Managed UInt64 counter exhaustion is not a PS1 rule. The planned pure query must
roll back all private RNG/results on exhaustion. It cannot require room for four
draws in advance: an execution evade can exit successfully on the last available
draw. A later composed query must also roll back earlier evade draws when a proc
tail subsequently exhausts the counter. No live stream is adopted by these APIs.

## Reuse and acceptance

The existing `PhysicalWeaponEvasion` is only a Weapon Guard contribution rule.
Sword/Stone helpers assume physical facing/class domains and their own admitted
states. Reusing their entire pipelines would import incorrect magic rules.
Reuse the verified RNG, existing miss labels, pure amount/proc/finalizer owners
and shared formula coordinator; leave existing physical/AI behavior untouched.
Keep the new magic stages in one cohesive `MagicEvasion` owner rather than
creating per-spell coordinators or a universal formula interface.

Selected bounded children:

- **ZOD-344:** load immutable contributions and blocking-item labels from resolved
  slot/type/byte facts, with monster and None255 behavior; no status or draws.
- **ZOD-345:** prepare base hit/contributions and Abandon diagnostic marker from
  explicit Transparent, classified restriction, target statuses and Defending.
- **ZOD-346:** resolve fresh supplied hit/accuracy through secondary Evadeable,
  exact products/ordered rolls, mode behavior and atomic RNG rejection.
- **ZOD-347:** add a shared formula entry before evasion, retaining the already-hit
  API, fresh/no-earlier-effects certificates, healing bypass, hit-versus-accuracy
  branch semantics and whole-query rollback. Return result/diagnostic data only.

The source-trace milestone passes eight new Python tests (47 descriptions plus
width/mode/tamper guards). Existing managed code is unchanged at ZOD-342; its last
full validation remains 4,618 .NET tests. Later implementation checkpoints and
validation are recorded below as each child completes.

MP/charge, live stats/equipment extraction, prepared spell grants, target geometry
(including the agreed future cone), actual commands/HP/status publication, magic
AI activation, owner drafts, JobStats, host/editor, automatic facing, broader
corpse/Golem/terminal/frame RNG, broad interfaces and ZOD-106 remain deferred.

## ZOD-344

`MagicEvasion.Load` now accepts immutable resolved slot/type/byte facts and returns
opaque `MagicEvadeContributions`. Monsters and wrong types contribute zero;
the independent item labels retain weapon preference, including item0 versus
None255. `MagicEvadeValues.Class` is always0. It does no lookup, I/O, status work
or RNG. Two focused Framework cases pass all eight literal descriptions and the
null-input guard; existing rules/tests/fixtures remain unchanged. This milestone
does not claim a new full .NET run. Logs: `scratch/magic-evasion-342-347/focused-344.log`.

## ZOD-345

`MagicEvasion.Prepare` now produces opaque `MagicEvadePrepared` work from the
loaded contributions and explicit `MagicEvadeModifiers`. It preserves the magic
source order and reports Abandon as diagnostic data; no status or reaction is
published. Its constructors prevent callers from fabricating a zero base hit or
nonzero class evade. Four cumulative focused Framework cases pass the eight
loads and15 preparations plus null guards; two cases are new for this child.
Prior inputs and item labels remain immutable. No RNG is consumed. Logs:
`scratch/magic-evasion-342-347/focused-345.log`.

## ZOD-346

`MagicEvasion.Resolve` now owns the exact fresh-work percentage and ordered real
draws. It takes secondary execution Evadeable and explicit evaluation mode; no
physical or AI metadata flags are inferred. `MagicEvasionOutcome` preserves hit,
unsigned16 accuracy, miss label, raw blocking-item label and Abandon diagnostic.
The prepared opaque work guarantees a positive base and class0. A zero rounded
percentage is retained independently of hit. Counter exhaustion returns the
incoming RNG and no outcome, including after earlier private draws; no blanket
four-draw preflight rejects a legitimate early exit. No stream is adopted.

Seven cumulative focused Framework cases pass all47 descriptions plus input,
replay and immutability guards; three cases are new for this child. Tests include
partial/certain/non-evadeable modes, full-byte/unsigned16 limits, first-success
labels, scaled19 boundaries, no fake midpoint RNG, and the final available draw.
Logs: `scratch/magic-evasion-342-347/focused-346.log`. No spell entry is connected
by this milestone; ZOD-347 owns that bounded composition.

## ZOD-347

`FaithMagicFormula.Resolve` now composes the existing shared calculation from
before evasion. `FaithMagicResolutionRequest` supplies the earlier formula request,
positive `FaithMagicEntryProfile`, secondary execution Evadeable and resolved
equipment/modifier facts. The existing `ResolveAfterHit` and projection/raw APIs
retain their behavior and signatures. The ordered amount/proc/finalizer tail still
has one owner; no per-spell coordinator or universal interface was added.

The entry requires a positively known fresh result, no earlier reaction or
reflection effects, a known nonzero ability, accuracy100 and all existing
living/empty-status/no-pending finalizer facts with known-clear entry flags.
`FreshResultKnown` includes zero supported HP/type/special/blocking-item work and
a known-clear stored result reaction. The original initializer preserves the
reaction field, so running that initializer alone cannot certify this context.
Ability0 always checks evasion in the original regardless of secondary Evadeable;
this selected nonzero-ability entry rejects an uncertified ID context rather than
silently importing the wrong branch. These certificates are managed admission,
not new original rules.

The coordinator validates/projects immutable amount inputs privately before any
RNG. This has no side effects or draws. Source-visible amount/element/proc results
are returned only when the evasion hit branch enters the formula. Thus an early
miss exposes no precomputed amount or affinity flags. Required argument errors
still throw even if a later evade would miss; unsupported certified context
rejects before any draws. This is explicit managed input validation, not a claim
that the original computes missed damage. Healing bypasses all evasion inputs,
which may be absent; damage still prepares work when checks are disabled.

`FaithMagicResolutionResult` returns a finalized frame, separate evade diagnostics
(including blocking-item/Abandon facts), proc diagnostics and candidate RNG. On
an evade miss proc diagnostics are absent because the tail never entered. The
coordinator branches on hit, not accuracy: a rounded-zero hit still reaches the
proc and only later becomes a miss at finalization. A certain AI/preview evade
draws four times then skips the formula. Element nullification happens after
surviving evasion and skips the proc. Absorption retains the damage proc family.
Healing has neither evade draws nor a damage conditional proc.

If evasion or the later proc exhausts the managed counter, the entire query
returns the incoming RNG with no frame, evade or proc output. Four successful
private evade draws therefore cannot leak when the fifth proc draw is unavailable.
Successful early evade exits can use the final available counter slot.

[24 further literal entries](../fixtures/behavior/magic-entry-contract-v1.json)
cover those combinations plus fresh/nonzero/earlier-effects/result-domain guards.
Five new Framework cases exercise all24 entries, every existing result scope fact,
healing without evade/element context, input isolation/replay and invalid required
arguments. Six new Python cases independently verify the entry descriptions,
hit/accuracy ordering, no leaked private amount and prefix rollback.

Full batch validation: locked restore, zero-warning/error Release build,
**4,630 .NET tests** (1,589 Framework, 1,217 AI, 838 Content, 328 CLI) and **649
Python tests**. That adds12 managed cases and14 Python cases across the five
tickets, with71 new source descriptions. All prior test expectations/fixtures,
source receipts, owner drafts, JobStats, content/AI/host/project/dependency code
remain unchanged. Pinned13-file/21-function evasion/entry audits, prepared empty
status/four-Fire-tier checks, prior Faith/element/proc/finalizer, guide, coverage,
links and preservation audits pass. Seven TRX reports and logs are under ignored
`scratch/magic-evasion-342-347/`.
[Implementation receipt](../evidence/combat/magic-evasion-implementation-v1.json).

[ZOD-348–357](magic-current-inputs-and-casts.md) now project current inputs and
compose MP/timing/supplied-target cast queries while preserving these APIs.
**Next ZOD-358:** trace original targeting geometry. These remain pure results
without command, HP/status mutation or live RNG authority. Cone geometry and the other documented deferrals remain separate.
