# Readable C# combat formulas and modification boundaries

Owner direction recorded 2026-10-24 during the banner-hide investigation, managed
entry `702cc4a`. This records desired structure and future refactoring criteria;
it does not expand today's combat domain or introduce a formula/plugin runtime.

The owner wants to **read weapon and ability formulas as C# code and modify them**.
Keep that workflow practical as original combat support grows. A visual formula
editor, a string-expression language and runtime C# compilation are not required
to make ordinary source edits possible.

## Current implementation

`src/Fft.Core/AttackBattleSimulator.cs` already implements the bounded sword attack
in C#. It currently combines validation, targeting/facing, ordered evade draws,
critical calculation, the damage expression, the proc draw and publication of
immutable state/events. Its source trace and strict limits are in
[bounded sword implementation](../behavior/bounded-sword-implementation.md).
It is not a general implementation of every weapon/ability formula.

`src/Fft.Content/Definitions/SwordDefinition.cs` stores validated immutable sword
data, including power and the supported formula selector. The Core projection is
`BoundedSwordWeapon`. `AbilityDefinition.cs` currently describes learning metadata;
it does not yet supply executable ability effects. Do not describe a complete
editable formula catalog as already implemented.

## Intended editing experience

Implement executable formulas as named, readable FFT-owned C# methods/classes.
Keep arithmetic and compatibility exceptions visible, with links to pinned source
traces and tests. For example, the existing bounded sword arithmetic can eventually
be extracted into a small method receiving the already adjusted attack value and
weapon power. That extraction must preserve its current supported ranges, cap and
ordered critical/RNG behavior; it is not permission to replace all physical attacks
with a generic multiplication.

Distinguish two kinds of modification:

- Tuning power, costs, range, elements, flags or a supported coefficient belongs
  in validated immutable definitions when those fields are genuinely data.
- Changing the algorithm or adding a new formula family belongs in readable C#.
  Assign an explicit stable formula/profile identity when dispatch becomes necessary.

For source-based personal modifications, the initial workflow can be edit C#,
build, run focused formula tests, then replay a small battle scenario. The owner
need not author formulas as opaque bytecode or copy original globals to modify a
calculation. Runtime loading of arbitrary assemblies, hot reload, a scripting VM
and executable external packages remain separate unselected capabilities.

## Refactoring criteria before broader combat support

When ZOD-038 selects the next formula family, first consider extracting the existing
bounded sword arithmetic from `AttackBattleSimulator` without changing behavior.
Use small typed input/result records or parameters appropriate to that family;
avoid one giant context exposing every mutable battle field.

Separate concerns where the traced behavior supports it:

- Formula arithmetic returns a value/result rather than mutating battle/UI state.
- Resolution owns admission/target validation, ordered modifiers, conditional RNG,
  and atomic application of HP/status/turn changes.
- Content selects supported formula identities and supplies validated parameters;
  it does not duplicate the arithmetic in the editor, AI or asset tool.

Reuse modifier/hit/element helpers only when source traces establish the same
semantics and ordering. Some weapon and ability families will need different
inputs or phases. Do not force them into a universal pipeline before those
differences are known. Preview/AI should share appropriate calculation code while
keeping observational calculation from consuming the live battle RNG or applying
effects.

RNG remains explicit and replayable. Preserve conditional draw counts and ordering;
do not pre-roll a uniform random bundle or use `System.Random` inside a formula.
Where a formula family requires randomness, define its interaction with explicit
deterministic state/rolls rather than hiding it in a convenience helper.

Original arithmetic is part of behavior: signedness/widths, truncation after each
division, multiplication order, caps, overflow and quirks must stay explicit.
An algebraically equivalent expression over real numbers may differ with integer
arithmetic. Refactors require unchanged baseline vectors, draw traces and battle
replays before any intentional rule change is introduced.

## Compatibility and custom variants

Preserve the original formula implementation and its regression evidence when
adding a custom variant. Select variants through an explicit FFT rules/content
profile rather than silently changing original mode. The generic Zodiac engine
continues to own no FFT formula catalog or compatibility rules.

ZOD-049 owns package/profile isolation and eventual save/replay bindings; algorithm
changes need a rules/profile version identity so an old replay/save is not silently
interpreted under a different rule. Design the exact dispatch/versioning contract
when a second real formula/profile needs it, rather than building a speculative
plugin framework now. ZOD-048 owns later inspection/editing UI, and ZOD-050 remains
the gate for needs that ordinary typed C# and declarative content cannot satisfy.

No combat runtime refactor is made by this note. The active banner validation
continues independently; these owner requirements guide the next combat milestones.
