# ADR 0001: Keep simulation deterministic and headless

- **Status:** Proposed for review
- **Date:** 2026-10-20

## Context

The project needs to reproduce observable FFT behavior while also supporting tests, replay, and new content. Mixing battle rules with display timing, global randomness, file reads, or input polling would make results harder to reproduce and compare with the decompilation.

## Decision

Simulation transitions are driven by explicit state and commands, with deterministic random state supplied and returned explicitly. A transition returns the resulting state and descriptive events. Simulation code does not depend on rendering, audio, input devices, wall-clock time, operating-system services, asset-file I/O, or the C reference.

The current `Zodiac.Framework` project remains the initial home for the small domain/simulation model. We may split it when independently useful responsibilities emerge; the architecture does not require a project split now.

## Consequences

- Battle rules can run in headless unit tests and deterministic scenario replays.
- A UI can request actions and observe results without owning gameplay state.
- Compatibility-sensitive random draw order and arithmetic must be explicit and regression-tested.
- Presentation timing and animation may interpolate between simulation states but cannot alter them.
- This is a direction for future simulation work; the existing flat-grid movement prototype is explicitly simplified and does not yet implement the transition contract.

## Alternatives considered

- Put rules in the client loop: rejected because it couples behavior to rendering/input and weakens headless testing.
- Introduce a generic event-sourcing/ECS framework immediately: deferred because no current code needs its complexity.
