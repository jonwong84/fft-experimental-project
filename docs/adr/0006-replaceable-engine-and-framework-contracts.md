# ADR 0006: Support engine and framework replacement through independent contracts

- **Status:** Replacement goal accepted; contract/API/assembly design deferred
- **Date:** 2026-10-25
- **Owner direction:** The owner wants the option to replace Tessera Engine or
  Zodiac Framework, in addition to editing FFT formulas in C#.

## Context and current evidence

Ownership alone does not make an implementation replaceable. An interface in an
implementation assembly, or one returning implementation-owned state, still makes
its consumer depend on that implementation. Concrete immutable contract records
are appropriate; their assembly ownership is the relevant distinction.

Inspection at managed commit `4bda424c2f862661f6b8e244517c937a8baec432` finds:

- `Zodiac.Framework.csproj` references `Tessera.Engine`. Its
  `FlatGridMovementRange.Calculate` facade constructs engine grid values and
  calls the static engine calculation directly. This is a narrow candidate for
  the first replacement proof, not an already replaceable engine boundary.
- `IDeterministicRandom<TState>` expresses a useful independent operation, but
  currently lives in `Tessera.Engine`; `FftBiosRandom` implements it. Existing
  gameplay also constructs `FftBiosRandom` directly. An interface declaration
  alone does not prove implementation selection or replacement.
- `Zodiac.Content.csproj` references `Zodiac.Framework`; definition schemas and
  prepared projections are coupled to framework-owned types.
- `Fft.Client.csproj` references Content and Framework. For example,
  `Presentation/IntroductionBattleSession.cs` exposes `TwoUnitBattleState`,
  `BattleIntroductionState`, commands and traces from Framework. Replacing only
  its class with an interface would leave those dependencies intact.
- `Directory.Build.targets` protects Tessera from game project references. That
  ownership check does not yet enforce implementation-independent contracts.

This is local architecture evidence, not a new original-game behavior analysis.
It makes no decompilation, emulator, rendering or full-game equivalence claim.

## Decision

Owner clarification (2026-10-25): it is too early to define broad engine or
framework replacement contracts. Preserve replaceability as a design goal while
continuing to discover real gameplay and application needs. The earlier concrete
contract proposal was too prescriptive; it is not an approved API or assembly plan.

Support replacing either implementation through a compatible adapter and a
composition-root wiring change followed by a rebuild. The consuming rule or
application code should remain unchanged for the supported contract. Replacing
both layers must also be possible without retaining an old implementation solely
to obtain shared contract types. Runtime discovery, unloading, hot swapping and
arbitrary executable plugins are separate, unselected capabilities.

If a replacement boundary later justifies an independent contract assembly, it
can hold selected interfaces and their minimal immutable inputs, outputs,
identities and diagnostics. No assembly names, splits or signatures are selected
now. The following are constraints for that later design, not instructions to
introduce contracts today:

- Engine contracts describe game-neutral operations. Tessera or an alternative
  engine adapter implements them; Zodiac consumes them. FFT policy stays in
  Zodiac, including terrain meanings, CT rules, formulas and draw ordering.
- Framework contracts describe the FFT-facing capabilities that the application
  and content consumers actually require. Zodiac or a replacement framework
  adapter implements them. These contracts must not require `Zodiac.Framework`
  or `Tessera.Engine`, or expose their internal workers, VM or state layouts.
  Avoid depending on engine contracts at this boundary unless independently
  justified; replacing a framework should not force its engine model on the host.
- Contract projects do not reference implementation, Content, Client, graphics,
  operating-system or import projects. The consumer's needs determine the API;
  implementation classes do not determine it by automatic interface extraction.
- The application composition root may reference concrete implementations to
  instantiate and connect adapters. Other selected consumers depend on contracts.
  An adapter may reference both contracts and the implementation it translates.
  Manual constructor/factory wiring is sufficient; no DI container is required.

This target does not promise that an unrelated engine or framework can be dropped
in unchanged. Its adapter must implement the agreed capabilities and semantics.
Unsupported operations, profiles or content versions must fail explicitly.

## What belongs in a contract

Specify behavior as well as signatures: accepted inputs, validation, rejection
and rollback, deterministic ordering, explicit random state, result/event meaning,
resource budgets and lifecycle/ownership where relevant. Keep simulation contracts
headless and free of file I/O, input polling and wall-clock dependence.

Use concrete immutable values in contract assemblies when they describe the
public operation. Internal state can stay implementation-owned behind a session
or adapter. Public observations must be sufficient for the consumer and replay
checks without exposing every internal worker. Persisted state requires an
explicit versioned representation or migration, not serialization of private
objects or a cast back to the original implementation.

Save/replay compatibility is separate from C# API compatibility. Preserve stable
IDs, supported schema versions, rules/profile identity and required RNG behavior.
An alternative implementation claiming original mode must pass the same applicable
behavior vectors and replay/order checks. A custom rules implementation needs an
explicit distinct profile. A test stand-in proves wiring only, not FFT fidelity.

## When to revisit

The completed [ZOD-099 audit](../architecture/zod-099-replacement-boundary-audit.md)
records current coupling. It is useful evidence, not evidence that the current
flat-grid or two-unit prototype defines a suitable public replacement API.
ZOD-100 and ZOD-101 are deferred reminders, not Ready work.

Revisit a bounded contract when a real second implementation, independent
consumer/game, or demonstrated integration change needs substitution and the
operation's semantics are understood. Document the concrete need first; reassess
the candidate API rather than implementing the earlier sketch automatically.
No existing ticket's completion alone activates this work.

Then select the smallest useful boundary, prove that its actual consumer can use
an alternative without referencing the replaced implementation, and run shared
behavior/conformance checks. Add reference guards as a boundary becomes real.
Preserve existing FFT behavior and Relay, and keep full-game scope separate from
a bounded proof. Current ownership and determinism rules remain in force now.

The ZOD-098 internal introduction adapter, if useful, is a client reuse mechanism.
It is not by itself the framework replacement contract or a reason to expose
intro worker types in a new public contracts assembly. Such a local interface can
be justified by concrete client reuse without defining an engine/framework API.
ZOD-098 proceeds without a replacement-contract prerequisite.

Formula arithmetic remains readable C# with typed inputs/results. Preserve
original arithmetic, conditional RNG and atomic resolution during extraction;
select a formula strategy only where actual supported variants require it. See
[editable formulas](../development/editable-combat-formulas.md).

## Consequences and limits

Independent contracts and adapters add versioning and maintenance work. Keep the
surface small; do not create an interface per class, one universal engine service,
untyped property bags or a generic scheduler to hide incompatible behaviors.
Existing pure functions and internal immutable states need no blanket rewrite.

This decision extends [ADR 0004](0004-zodiac-engine-and-fft-game-boundary.md):
ownership and dependency direction remain; replacement now supplies a concrete
reason to investigate additional contract assemblies. No assembly extraction,
namespace/schema change, runtime interface, replacement proof or new test result
is claimed by this documentation change. The last implementation validation
remains ZOD-097's 2,111 .NET and 269 Python tests.
