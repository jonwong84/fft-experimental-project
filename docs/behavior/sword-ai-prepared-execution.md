# Fresh prepared command execution — ZOD-163

Read-only reference `11464943bbdbd2a0af022d90cb7f10455cf911df`. This implements
the [prepared-selector handoff](sword-ai-prepared-selection.md) through the existing
[Framework transaction](../architecture/ai-command-boundary.md), preserving the
[post-prefix executor](sword-ai-execution.md).

`SwordAiExecution.ExecutePreparedAlreadyMoved` owns fresh NonBlue ordinary
refresh/preparation/selection inside the Framework's synchronous callback. The
session's current RNG is its only entry RNG. It accepts source requests/profiles,
not a cached result, decision RNG or GeneralSelectionEntryContext assertion.
The old entry retains its API and certificates. Both entries share session guards,
selected-plan admission and actual Attack/Wait publication.

Success publishes one actual command/revision. Framework validates RNG progression,
adopts both snapshot copies privately and issues any receipt from the actual settled
battle. Preparation adds no revision, clock event or live hypothetical CT change.
MoveOnly fallback stays at the actor origin, resolves facing and refunds unused
ACT once. Attack uses actual combat rolls, HP/KO/knockback and a pending completion
receipt; projected damage is diagnostic only. A later facing failure cannot undo
a committed lethal Attack; the existing living-opponent facing limitation remains.

Failures return the identical input session, empty events and no decision/command
outcome. Prepared-selector failure exposes only its typed reasons; actual Attack
exhaustion rolls back refresh and selection too. No intermediate progressed RNG
or trusted state is published. Success diagnostics include immutable Preparation;
old post-prefix success leaves that property null.

At source-derived word1/count7, CT15/95 and front evade89, fresh selection reaches
word2111915288/count22 after eight lottery draws. Actual miss ends
word3794901700/count26 and leaves HP50, despite projected HP25. Exactly 19 available
draws succeed; eighteen fail during actual Attack and restore the original session.
Zero-power fallback finishes with fifteen draws and no Wait draw. Seed8 full-hit
critical vectors publish damage45/push or damage54/KO before the settled receipt.

Seven new managed cases plus all existing AI cases pass (714 AI tests), with a
warning-free AI build. Logs: ignored `scratch/ai-prepared-execution-163/`. Last full
solution validation remains ZOD-162's 3,878 .NET / 507 Python; the batch reruns it
after ZOD-164. No C/original execution or host/editor activation. Out-of-reach
prepared selection/Wait is the next bounded child; broader AI remains separate.
