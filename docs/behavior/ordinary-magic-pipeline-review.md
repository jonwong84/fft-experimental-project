# Ordinary magic arithmetic, evasion and routing review — ZOD-436

Reviewed from managed `46ec95bdf7013567f3c07f2adca527bee973430a` against clean,
read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`. The
[review receipt](../evidence/combat/ordinary-magic-pipeline-review-v1.json) pins
source files/functions, current managed owners, source audits, new consumer
literals and validation. No new gameplay defect was confirmed within the
admitted domains. This completes the bounded arithmetic/evasion/result-routing
review, not all magic lifecycle or original AI work under ZOD-428.

The previously supplied FFHacktics traces remain supporting evidence in the
[amount](faith-magic-amounts.md), [evasion](magic-evasion.md),
[element](magic-elements-and-routing.md), [proc](magic-conditional-proc.md) and
[status-applicator](status-action-application.md) notes. Their annotation
corrections and original snapshot hashes are preserved. This pass follows the
shared consumers as well as those individual helpers.

## Formula families and arithmetic order

`formulas.c::formula_magical_damage` (0x08) runs magical evasion, MA/Y loading,
elemental strengthen, magical support/status/compatibility, stored product,
weather/affinities, Faith, absorption and conditional proc/status work.
Evasion and element nullification stop the appropriate tail; a failed proc
skips the status attempt without erasing damage.

`formula_magical_heal` (0x0C) loads MA/Y, strengthens, uses
`formula_calculate_magical_xa_times_ya`, applies Faith and reverses for Undead.
It does not enter evasion, target Magic Defense Up/Shell/Frog modifiers,
weather, target elemental affinities or the conditional proc branch.
Strengthen still uses the ability's elements, even on a custom healing formula;
vanilla non-elemental Cure data simply does not trigger it.

[FaithMagicAmountFormula](../../src/Zodiac.Framework/Battle/Combat/Magic/FaithMagicAmountFormula.cs)
now exposes these responsibilities as private routine methods:

- `ElementalStrengthen` precedes both families.
- Damage calls `MagicalSupportStatusCompat`, which calls `MagicAttackUp`,
  `MagicDefenseUp`, `TargetStatusAffectingXaMagical` and original `Compatibility`.
- Healing calls `MagicalXaTimesYa`, which uses only Magic Attack Up,
  compatibility and the shared stored multiplication.

Every multiplier truncates toward zero and stores signed16 XA independently.
Magic Defense Up and Shell are separate *2/3 stores. Chicken-or-Frog applies
one *3/2 increase. Original compatibility stores XA*25 in unsigned16 before
division, floors nonpositive XA to1, and skips both adjustment and floor for
Serpentarius. Classification preserves the original sign table, monster branch
and equality of the raw male/female bit pair. The secondadvent rewrite remains
deferred.

The product is stored signed16 before weather/affinities and Faith. Faith uses
the stored damage * target Faith * caster Faith /10000 with one division and
another signed16 store. Faith status sets a working value to100; Innocent then
sets it to0 independently for each unit. Neither changes the unit's Faith.
Full-byte Faith multiplication remains within signed32. Wrap and negative
amounts retain their source meaning; there is no early positive clamp.

The new consumer literals use MA7/Y11/Faith90/90. Damage XA is
`7 → 8 → 10 → 6 → 4 → 6 → 7`, then product77, storm57, half28, weak56 and
Faith45. The same strengthened healing inputs give XA12, product132 and
Faith106, despite the target's defenses and affinities. These literals were
checked independently and passed against the pre-refactor implementation;
the extraction is not presented as a behavioral fix.

## Elements, routing and finalization

[MagicDamageElements](../../src/Zodiac.Framework/Battle/Combat/Magic/MagicDamageElements.cs)
now reads `WeatherElementalEffects` followed by `ElementalDamageModification`.
Effective weather is an explicit input, not a new script/weather scheduler.
Storm/strong storm applies Fire *3/4 before Lightning *5/4; snowstorm/strong
snowstorm applies Ice *5/4. Plain Rain and Snow do not receive those bonuses.
Each store narrows before the next step.

The target affinity order is absorption marker, nullification exit, half, then
weakness. Half and weakness can both apply and retain intervening truncation.
Nullification clears the represented amount/effects and prevents Faith/proc
work. Oil/Fire and Earth/Float/rider contexts remain explicitly rejected; the
known Oil stored-damage quirk has not been silently repaired or activated.

Absorption transfers the post-Faith damage bits into healing and keeps the
damage formula's proc branch. It does not reverse again for Undead. Healing's
Undead reversal instead keeps HP damage and proposes the canceled-status-byte0
clear; the living branch transfers its amount to HP healing. Broader application
of that status-clear proposal remains outside ordinary live/AI publication.

`FaithMagicFormula.ResolveProjectedAfterHit` retains one shared ordered tail.
Execution draws the conditional proc even for empty status record0; Preview
requests the preserving empty-status attempt without setting AddedEffect; AI
does neither. Nullification and healing skip that draw. The source preserving
wrapper restores hit, miss type and unsigned16 accuracy even when no status
changes. A successful execution proc may still leave AddedEffect with no status
change, including lethal damage when no weapon proc is pending.

`action_finalize_target_result` and its caller map to the existing bounded
`MagicResultFinalization`: zero accuracy clears hit after formula/proc work,
positive signed16 HP fields cap at999 independently, and any retained special
effect adds pseudo-status typing even on a miss. Negative fields are not
lower-clamped or reclassified. Dead/Petrify/Wall, pending proc/knockback and Golem
branches retain positive admission guards. New tests confirm that a rounded
zero-accuracy AI frame can retain calculated weakness/damage diagnostics while
the hit-application consumer leaves HP unchanged.

## Evasion and modes

[MagicEvasion.MagicalEvadeCalculation](../../src/Zodiac.Framework/Battle/Combat/Magic/MagicEvasion.cs)
is the fresh named wrapper used by `FaithMagicFormula.Resolve`. It loads magical
equipment contributions, applies `TransparentCalculation`, `AbandonCalculation`
and `EvasionChangesDueToStatuses`, then uses the existing final-hit/roll owner.
Public `Load`, `Prepare` and `Resolve` APIs remain compatible.

Base hit begins at100; magic class evade is0. Accessory and qualified shields
provide contributions; hand weapons choose miss labels, not magic evade.
Transparent checks the attacker, not Concentrate. Qualified Abandon halves base
hit and records its marker without Brave RNG. Target disabling statuses clear
contributions before independent Defending halving; the Abandon marker survives.
`ActionUnrestricted` remains a caller-classified restriction fact, not a newly
implemented general terrain/mount predicate.

The secondary Evadeable flag gates the final calculation, not preparation.
With it set, all four sources draw in accessory/right/left/class order, including
zero contributions; only execution can stop on an actual evade roll. Preview
and AI still consume those real draws. A certain contribution sets hit/accuracy0
before rolling, while merely rounded accuracy0 can retain hit until finalization.
Formula0x0C bypasses this whole chain, even when a custom definition sets the flag.

## Shared consumers and admission

`FaithMagicInputs` derives current MA, effective Faith, original compatibility,
strengthen, target defenses, element and evade facts. `FaithMagicFormula.Resolve`
owns fresh-entry admission/evasion, and `ResolveAfterHit` is the explicit lower
entry. Pure amount projection may run privately for validation before evasion;
a miss exposes none of that projected work and keeps source-visible RNG order.

`MagicCastQuery.PrepareReady` checks ready/default-menu/resource facts once.
`ResolveTargets` certifies distinct target order, normalizes a self target to
the caster's MP proposal, then chains one RNG through the same current-input
formula for each target. Half MP truncates the integer cost; Execute and AI
propose one deduction, while Preview retains current MP. Later-target failure
rejects the entire private cast with entry RNG and no partial cost/results.
A nullified first target does not leak its flags or suppress the next target's
evasion/proc draws; the new cast regression uses nine draws for that pair.

Stored-aim/ready-plan and pure Execute publication queries delegate to these
same owners. Preview remains a query mode; the live host's complete preview
experience is not claimed here. `OrdinaryMagicCastExecution.Resolve` invokes
Execute at readiness, spends the one proposed cost, and applies finalized
frames using the admitted HP/status kernel. Only its formula/resource/result
edges were reviewed here; queue, charge lifecycle, rewards and learning need
their separate retrospective.

`DefaultAbilityAiProjection.Calculate` uses AI mode through the same ready-cast
owner, followed by `ActionHitsSimulate` / `StrikesSimulate` for candidate work.
It admits a narrower ordinary bare-human profile than the low-level formula:
no supports, equipment affinities, strengthen, changing teams, mounts or general
statuses. Actual ordinary encounters likewise exclude broader current-unit
facts and use explicit no-weather input. Metadata supplies primary AI behavior;
secondary Evadeable is carried by the executable spell formula. Unknown static
AI bits/special command menus do not authorize this path. No inactive owner
toggle draft is read or consolidated.

## Findings and validation

No bounded gameplay defect or new historical erratum was confirmed. Named-method
extraction preserves public signatures, source widths/order, flags, draws,
atomicity, current content and admission. Eight consumer regressions add mode,
family, result-isolation and application coverage without asserting private layout.

Nine source/prepared-data audits pass 365 descriptions. They freshly validate 25
source file and 64 function fingerprints and eight prepared Cure/Fire tier
formula/power rows, plus the empty status record. These audits describe pinned
source behavior; the managed regressions execute C#. Neither C nor the original
game was run. Earlier exhaustive helper receipts remain historical evidence.

Release build passes with zero warnings/errors; all 5,189 .NET tests (1,835
Framework, 1,384 AI) and 794 Python tests pass. Eight new managed cases also
passed before the method extraction. The receipt records preservation of all
ten owner drafts, approved controller formatting and the disabled CI gate.
Full spell
lifecycle/learning/rewards, general statuses/Reflect/Golem, broader formulas/
shapes, origin search, autonomous magic commands, frame RNG and ZOD-432 remain
separate. Next is the supporting ZOD-426 movement-origin/baseline source study.
