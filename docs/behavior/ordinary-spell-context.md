# Ordinary spell query context — ZOD-443

The ability caller now exposes `Evaluate(context, request, rng)`. A narrow,
immutable `DefaultAbilityAiContext` owns the captured battle state and its matching
catalog; the request owns actor, primary slot, profile declaration and scenario.
RNG remains an explicit input/result. Search mechanics, candidates, ordering,
scores, baseline gates and resource/RNG behavior are unchanged.

Managed starting revision: `673f55adeb099e540e7cf99dfe2597ce405a1945`. The owner's
formatting was committed separately as `973b39e` before the API refactor. The
read-only reference remains clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`. This is a managed API ownership
adapter following the [ZOD-442 study](ordinary-spell-list-integration.md), not a
new translation of an original gameplay rule. The
[receipt](../evidence/ai/spell-context-implementation-v1.json) pins current files,
source-study validation, test results and preservation.

## Entry and use

Start at
[`DefaultAbilityAiContext`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiContext.cs),
then
[`DefaultAbilityAiAbilitySearchQuery.Evaluate`](../../src/Zodiac.Framework.Ai/DefaultAbilities/DefaultAbilityAiAbilitySearchQuery.cs).
The call now looks like this, using current captured inputs:

```csharp
var context = new DefaultAbilityAiContext(state, catalog);
var request = new DefaultAbilityAiAbilitySearchRequest(
    actorId,
    primarySlot,
    new DefaultAbilityAiMovementProfile(NormalFocusedScenario: true),
    DefaultAbilityAiOriginScenario.Main);

var result = DefaultAbilityAiAbilitySearchQuery.Evaluate(context, request, rng);
```

The profile remains an explicit declaration of the supported search domain; it
does not switch on missing mechanics or prove the remaining current facts.
There is no permissive profile default. Main remains the default diagnostic
scenario when omitted; Complement remains an explicit diagnostic mask and does
not add a second general dispatch pass.

This replaces the old five-argument ability-caller signature. Its local caller
was migrated, and all sixteen existing ability-search cases retain their exact
assertions. Other ordinary query APIs retain their current signatures and scopes.

## Construction and failure contract

`DefaultAbilityAiContext` is a sealed class with only two get-only public
properties, `State` and `Catalog`. Its constructor stores the exact captured
objects after validation; it does not derive or cache lists, considered entries,
target masks, movement, priorities, best/ranked records or RNG progress.

Construction rejects null state/catalog with `ArgumentNullException`. It rejects
different definition instances with `ArgumentException` naming `catalog`, even
when both definitions have the same content IDs. Definition reference identity
is the invariant, as in the prior internal work admission. Invalid pairs now
fail at context construction before evaluation; they cannot be supplied through
this new typed boundary as a normal rejected-search result. This is an intentional
API boundary change.

Evaluation throws `ArgumentNullException` for null context, request or nested
`Profile` (parameter names `context`, `request`, `Profile`). Unsupported current
state/request/profile and simulation failures still return `Accepted=false`,
no outcome and the whole entry RNG. These include invalid/unavailable actor or
slot, unsupported scenario, profile without the required declaration, unsuitable
clock/turn state, changed learning/MP and late overflow.

Fresh `ValidateRoster`, `DefaultAbilityAiWork`, origin/movement preparation and
`AiAbilityOutcomeEvaluate` still run on each evaluation. The internal definition
guard remains because other query entry points also use that work owner.
Context construction does not pre-admit an actor, phase, profile or resources.

## Captured-state lifetime

Reusing the same context with the same request/RNG repeats a pure query of that
captured state. Reusing it after a failed query also starts fresh: no partial
preparation, consumed aims, candidates or RNG are retained in the context.

Actual commands or clock advancement produce a new immutable battle state.
Create a new context for that state, reusing the definition-bound catalog when
the definition is unchanged. The old context continues to support replay of its
old snapshot; it does not follow newer state and grants no authority to issue a
command against it. RNG belongs to each call, not to the context.

The complete supported-turn entry remains `AiTurnController` → `AiDecisionMaker`
→ selection → `AiCommandExecutor`. This ordinary spell caller remains diagnostic,
outside that complete-turn pathway. The context does not connect it to dispatch,
ranked insertion, lottery or commands.

## Validation and review

Thirteen new cases in
[`DefaultAbilityAiContextTests`](../../tests/Zodiac.Framework.Ai.Tests/DefaultAbilities/DefaultAbilityAiContextTests.cs)
cover null/mismatched inputs, identical content IDs with foreign definition
identity, explicit profile/scenario/actor/slot rejection, current MP/action/phase/
learning admission, pure reuse after rejection and actual Wait-command lifetime.

The actual-command test creates a new context after Wait and rejects the now
unavailable actor turn, then replays the old snapshot with its original result
and RNG. The late-failure vector fits prefix13 plus unit5 at the draw-count limit,
rejects on the tile formula, then reuses the same context successfully. The
existing source vector still spends132 draws with seed57→399997557, including
when the entry draw counter starts at11 and exits at143. No expected search
values were regenerated for this refactor.

Full validation passes: zero-warning/error Release build, **5,276 .NET tests**
(1,471 AI;1,835 Framework) and **810 Python tests**. The inherited source audit
checks eight pinned files,32 functions and20 list/scenario/rank descriptions.
The owner's query formatting style is retained in the changed wrapper, and the
battle-state formatting is preserved exactly in its separate commit. Ten owner
drafts/immutable JobStats, controller formatting and disabled CI are unchanged.
No C, original executable or native presentation was run.

The next checkpoint is the owner's review. Subsequent query migrations or shared
full-list preparation need their own bounded scope. Full list/Attack integration,
scenario dispatch, ranked insertion/lottery, magic command/turn activation,
useful-self/healer/Reflect paths, retained workspaces, general status/equipment/
terrain/mount/pickup/Golem/terminal/corpse/frame behavior and broad interfaces
remain separate. ZOD-428 and the full-port ZOD-432 readability review remain open.
