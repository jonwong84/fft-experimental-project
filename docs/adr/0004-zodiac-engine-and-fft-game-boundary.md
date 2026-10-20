# ADR 0004: Separate the reusable Zodiac engine from FFT game content

- **Status:** Proposed for review
- **Date:** 2026-10-20

## Context

The project has two goals: create Zodiac as a reusable managed C#/.NET tactical game engine, and run Final Fantasy Tactics on it with behavior faithful to the North American PS1 release. Treating FFT's rules and data as the engine would make reuse mostly nominal. Generalizing every concept before a second use case exists would create speculative abstractions and risk losing compatibility details.

The current `Fft.*` solution is an early FFT-first prototype. In particular, its map and movement types include FFT-informed concepts and are not yet a clean engine API.

## Decision

Use a one-way dependency: FFT game/content code depends on Zodiac engine contracts; Zodiac does not depend on FFT definitions, data, scripts, asset formats, or naming.

Zodiac owns reusable deterministic simulation facilities and extension points. The FFT layer owns FFT-specific rules, data definitions, compatibility profiles, script behavior, asset import and interpretation, and campaign content. Platform presentation and I/O remain separate adapters/hosts.

Generalize a concept only when its invariant behavior can be stated independently of FFT and validated. Keep compatibility-specific policy in FFT-owned implementations. Keep the PS1 baseline distinct from Lion War/WotL content and intentional rule differences.

The current project/namespace names may remain `Fft.*` while these seams are explored. Assembly moves and namespace renames should be separate, reviewable changes after the contracts stabilize.

## Consequences

- Engine-level tests can validate deterministic contracts without loading FFT assets or definitions.
- FFT regression tests can validate its rules and data against the decomp and observations while running through Zodiac.
- A small independent sample game/content set should eventually prove that Zodiac is reusable and does not contain hidden FFT assumptions.
- `JobDefinition`, FFT formula catalogs, PS1 flags, text keys, sprite records, and ENTD/script definitions belong to the FFT content/tooling side unless a second game demonstrates a genuinely shared contract.
- Generic tactical concepts such as commands, simulation ticks, entity identity, deterministic randomness, and policy interfaces are candidates for Zodiac, but their APIs must be justified by concrete usage.
- Reuse will be demonstrated by a second content set, not by renaming FFT assemblies or using generic-sounding names.

## Alternatives considered

- Treat `Fft.Core` as the reusable engine unchanged: rejected as the final architecture because FFT-specific models would leak into other games.
- Generalize every current type immediately: rejected because there is only one real game use case and the rules are still being discovered.
- Keep one FFT-only application and call it a reusable engine: rejected because it would not establish a practical content/rules boundary.

## Validation of this decision

- A Zodiac-owned test project can instantiate and advance a minimal tactical scenario without referencing FFT assemblies or game assets.
- An FFT-owned content/rules layer can supply FFT definitions and select source-traced PS1 behavior through the engine contracts.
- Later, a minimal independent sample content set can run without adding FFT-specific conditionals to Zodiac.
