# Documentation and decompilation review programme — ZOD-428

Owner direction: review all work that predates systematic use of the supplied
FFHacktics documentation, extending the previously queued Throw Stone/Dash
review. This is the current review priority. Existing decompilation traces,
fixtures and passing tests are inputs to revalidation; they do not automatically
close a review batch.

[ZOD-427](../behavior/physical-pipeline-review.md) completes the first bounded
physical-pipeline batch, including one corrected formula/fixture assumption.
The [progress receipt](../evidence/maintenance/documentation-review-progress-v1.json)
records reviewed paths and integration limits. No complete inventory group is
closed yet; ZOD-429 is the next bounded review of Attack/Accumulate.

## Scope and historical boundary

There is no reliable single first-documentation cutoff in repository history.
Older notes already cite FFHacktics, and the supplied material arrived across
several conversations. The conservative historical comparison is
`52f8f22f6ce1d1e28e83395bb77c021ebc693e26`, before the equipment/formula
documentation phase recorded in `b177993`. This is a scope marker, not a claim
that earlier work lacked source study or that every later file has been reviewed.

The [scope inventory](../evidence/maintenance/documentation-review-scope-v1.json)
includes **every tracked file at `8f87533987e53502f474201db804b725f3ec22ec`**:
2,138 current paths, plus all 38 historical paths absent at the same location.
The historical tree contains 2,060 paths. Renamed, consolidated and later-added
implementations remain in scope, so reorganizing a file cannot exempt its
behavior. These are file counts, including tests, evidence and project files,
not counts of implemented mechanics or completed reviews.

The inventory covers Framework, AI, Content, Engine, import/asset tooling,
client/editor, events, campaign/save work, all tests, verification tools,
documentation and engineering configuration. Every group is initially pending.
The path census is complete for its snapshot; behavioral review is not complete.
Supporting evidence must be associated with the implementation it validates.
Git rename detection is proposed lineage, not proof of equivalent behavior.

The read-only reference revision at inventory time is
`11464943bbdbd2a0af022d90cb7f10455cf911df`, freshly checked clean. Each later
review must record its actual reference revision and concrete symbols. Neither
reference C nor the original game was executed for this planning inventory.

## Review order and coverage

1. **ZOD-427: Throw Stone/Dash and shared physical rules.** Check formula0x37,
   reusable formula assignment including0x32, preparation, delivery, evasion,
   modifiers, result finalization, RNG and knockback through actual, preview
   and AI pathways. Follow shared sword/Attack dependencies into the next batch.
2. **Shared battle foundations and other implemented physical effects.** Recheck
   sword/Attack, Accumulate, source widths, state/command boundaries, turn/CT/RNG,
   movement, facing, targeting and projectile assumptions. Review their current
   shared owners and older compatibility adapters together.
3. **AI pipeline.** Review lists/metadata, useful masks, preparation, movement
   origins/scenarios, simulation/snapshots, strike accounting, scoring, ranks,
   lottery and checked execution/continuations. Include magic integration that
   depends on earlier shared work. ZOD-426's origin/baseline source trace remains
   queued as supporting work for this review.
4. **Magic and action lifecycle.** Recheck formula0x08/0x0C, evasion, elements,
   Faith/compatibility, status-routing guards, resource charging, preparation,
   stored aims, readiness, recipient order and result application. Trace callers
   and shared state boundaries in addition to individual arithmetic helpers.
5. **Definitions, progression, campaign and saves.** Review imported versus
   authored metadata, learning/grants, jobs/growth, rewards, unlocks, prepared
   packages, campaign checkpoints and save/restore behavior. Distinguish actual
   implemented rules from scaffolding and explicitly invented demo objectives.
6. **Assets, event execution and presentation.** Review extraction/decoders,
   prepared-only loading, event/script scheduling, banner/Ready/dark-screen,
   geometry/rendering/input and editor behavior using the appropriate source
   routines and retained native evidence. Record missing original observations.
7. **Engineering and evidence closure.** Check build/dependency reproduction,
   serialization/versioning, test/oracle assumptions, stale coverage claims and
   documentation against the reviewed implementation. Use relevant platform or
   format specifications where a battle wiki routine does not apply. Account
   for every inventory path and historical replacement before closing the epic.

These are review batches, not one class per ability or a blanket refactor.
Choose a bounded ticket and independently checkable result for each execution
step. Cross-cutting findings attach to all affected pipelines. Current-source
changes after the inventory snapshot must be included when reviewing those
owners; the snapshot does not freeze development files or exclude descendants.

## What closes a bounded review

- Map the documentation's routine/call path to pinned decompilation symbols,
  callers, data records and current managed entry points. Preserve arithmetic
  widths, ordering, branching, RNG and observable original quirks.
- Separate primary AI preferences, secondary execution flags, working state,
  content parameters and formula-owned behavior. Check both player and AI uses
  of shared rules and the explicit admission guards around unsupported cases.
- Resolve documentation/decompilation disagreements in a written finding;
  inspect original instructions or further evidence where needed. Record an
  unanswered question as unresolved rather than guessing or calling it verified.
- Check source-derived literals and boundary scenarios independently of the
  implementation. Existing tests can preserve the same mistaken assumption as
  the code; passing them alone is not sufficient. Keep original-game/native
  observations distinct from source descriptions and synthetic demos.
- Record findings as verified within an explicit domain, a confirmed defect,
  unresolved evidence, or deferred/unimplemented behavior. Add targeted
  regressions and source-backed fixes for confirmed bounded defects; create
  follow-up tickets for wider work. Rerun checks appropriate to actual changes.
- Link reviewed paths/lineage, evidence, remaining limits and validation to a
  batch receipt. Keep earlier receipts historical; do not rewrite their hashes
  to make current code appear retrospectively validated.

## Preserved constraints

All ten owner files remain byte-preserved, including immutable JobStats and the
inactive flag/enum drafts. Reviewing an unused draft records its role and current
consumers; it does not authorize activating or consolidating it. The approved
formatting edit, data-driven definitions and formula reassignment remain intact.

Existing deferred mechanics remain deferred unless a separately selected ticket
authorizes them. The review can identify a gap without implementing equipment,
general statuses, Reflect, Golem, terminal/frame RNG, new shapes/cone or broad
editor work. Compatibility mode retains original quirks; custom content and
invented demo behavior stay explicitly separate. The reference checkout remains
read-only, prepared original files stay ignored, and no C/BIN runtime dependency
is introduced.
