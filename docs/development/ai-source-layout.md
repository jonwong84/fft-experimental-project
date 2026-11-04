# AI file organization — ZOD-219

Reorganized 2026-11-04. The [source map](../../src/Zodiac.Framework.Ai/README.md)
describes the four family folders and their mirrored test layout. The RNG tests
now sit in `tests/Zodiac.Framework.Tests/Random/`, with the generator test file
named `RandomStateGeneratorTests.cs`. Current coverage and Markdown links follow
the moves. Existing evidence JSON and archived planning retain their original
paths and managed revisions.

Locked restore, zero-warning/error full Release rebuild, all 4,110 .NET cases
(910 AI) and all 534 Python tests pass after the moves. Current coverage's 58
entries validate and its generated Markdown view matches. Seven full TRX files
are under ignored `scratch/ai-pair-execution-215-220/219-full-trx/`.

The move receipt under ignored `scratch/ai-pair-execution-215-220/moves.json`
records 111 individual file moves and verifies identical SHA256 before/after
each. No namespace, API, formula, ordering, assertion or test count changed as
part of these moves. SDK recursive compilation includes the new paths without
project-file lists. Matching folders make the pair implementation easier to
find while preserving shared kernels and the Framework/AI assembly boundary.

The owner's RNG migration was completed separately: remaining callers use
`Zodiac.Framework.Random.RandomState` and `RandomStateGenerator`; the generic
Tessera interface moved to `Interfaces/` without a namespace change. Constants,
output bits, counter width, checkpoint arithmetic and persisted algorithm ID
are unchanged. Unrelated content/editor drafts remain outside these commits.

Continue to group files by a useful behavior family or shared responsibility.
Do not create a project, namespace or abstraction just to reduce a folder's
file count. Broader Framework/content reorganization can be selected separately
as those areas mature; their evolving owner definitions are preserved.
