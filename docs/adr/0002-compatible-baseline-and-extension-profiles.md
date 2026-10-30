# ADR 0002: Keep the compatible baseline distinct from extensions

- **Status:** Proposed for review
- **Date:** 2026-10-20

## Context

The primary compatibility target is the North American PS1 release `SCUS-94221`. The long-term goal also includes additional content and The Lion War, whose exact variant and feature scope are not yet selected. Mixing additions into the baseline would make it difficult to determine whether a mismatch is a bug, an intentional change, or a mod feature.

## Decision

Keep baseline-compatible content and rules identifiable and selectable. Additions load through explicit, validated content packages or profiles, with stable IDs and documented overrides/deviations. Declarative content and a typed set of effects come before executable plugins or general scripting. The Lion War is a separate profile/content set once its version and scope are pinned.

An observed original quirk is preserved in the baseline when it affects behavior. Any extension may choose different behavior only through an explicit profile/rule choice, not by silently changing the baseline.

## Consequences

- Regression scenarios can name the exact rules/content profile they validate.
- New jobs, abilities, items, characters, monsters, maps, encounters, and campaigns need not inherit original fixed table capacities.
- Package conflict policy and extensible mechanic APIs remain open until the first real package use case.
- No claim is made yet that every mod mechanic can be represented declaratively.

## Alternatives considered

- Make one expanded ruleset the only mode: rejected because it blurs the PS1 compatibility target.
- Support arbitrary managed DLL mods from the start: deferred because it raises compatibility, trust, and sandboxing costs before stable APIs exist.
