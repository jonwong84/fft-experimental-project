# Framework file organization — ZOD-332

Organized 2026-11-05 from managed base
`78df755e8aa3377214708dedd7f4a43a15a09bf1`, following the owner's request to
improve navigation before further feature work. The read-only reference remains
clean at `11464943bbdbd2a0af022d90cb7f10455cf911df`; no original rule was translated.

51 Framework source files move from the project root into `Battle/State`,
`Battle/Movement`, `Battle/Combat`, `Battle/Turns` and `Battle/Commands`.
53 test files move into matching battle groups, `Events` and `Campaign`.
Existing production Events/Campaign/Random folders, test Random/Fixtures folders,
assembly metadata and the shared root `AssetId`/`AssetIdTests` stay in place.
The [source map](../../src/Zodiac.Framework/README.md) explains the groups.

All 104 moved C# files are byte-for-byte identical to their managed-base files.
Namespaces, APIs, assembly references, visibility, formulas, ordering, assertions,
fixtures, dependencies and project files are unchanged. Related records and
rules within a file remain together. SDK recursive compilation picks up the new
paths without a source-file list. The organization creates no new simulation,
policy or command boundary. Namespace migration and class splitting are separate.

Current Markdown references and coverage paths follow the moves. Historical
evidence JSON, behavior fixtures and archived planning retain their recorded
paths/hashes and managed revisions. No historical receipt is regenerated to
match the new layout. The [move receipt](../evidence/maintenance/framework-source-layout-v1.json)
records all paths and SHA256 values; local validation logs and an independent
baseline/current byte comparison are under ignored `scratch/framework-layout-332/`.

Validation: locked restore, zero-warning/error Release build, all 4,531 .NET
cases (1,521 Framework, 1,217 AI, 833 Content) and 596 Python cases, current
coverage/source-path and local-link audits. No test case or fixture is added.
The owner drafts, AI implementation, Content, host and editor code are unchanged.
ZOD-329 remains the next bounded feature; all documented deferred work remains
deferred.
