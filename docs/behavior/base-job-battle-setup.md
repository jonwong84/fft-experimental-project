# Base-job-only battle setup (ZOD-018)

Date: 2026-10-22. Reference checkout `C:\repositories\fft_decomp`, observed HEAD
`611b504d25d4a9b03fab8023d7dbea75fe381d5d`. This is a bounded definition-to-unit
adapter, not complete equipment, status or effective-stat derivation.

`Fft.Content.BattleSetup.BaseJobBattleSetup.Create` accepts a validated
`DefinitionCatalog`, immutable map and explicit unit placements. It resolves every
job, primary skillset, innate and skillset ability before returning the initial
battle. Missing catalog references fail when constructing the catalog; missing job
IDs fail setup with the unit/job identity. No file loading occurs inside simulation.
Prepared metadata can be loaded and composed through the existing `ContentPackage`
API before calling setup; original records remain local under ignored scratch.

Definition bindings identify runtime units by `BattleUnitId`. Two units may share
the same immutable job/skillset/ability instances, while their position and movement
availability remain independent `BattleUnitState` snapshots. Bindings hold content
references, not a second mutable unit state. Evolving battle snapshots retain their
own state; changing a separately created authored catalog cannot alter old snapshots.

The mapping is deliberately narrow:

- Job `BaseMove` becomes movement-profile Move, and `BaseJump` becomes Jump.
  `src/main/party.c::unit_copy_job_data` copies Move and masks packed Jump with
  `JOB_JUMP_VALUE_MASK`; the importer already separates the value and stepping-stone
  flag. These are source-traced base values, not equipment-adjusted effective values.
  Later source equipment/ability paths can change them and cap Jump; those paths
  are excluded. The adjacent-step policy applies its separately traced capped-Jump
  geometry rule when explicitly selected.
- The caller supplies runtime unit ID, job ID, starting position, ordinary body
  height6 and all terrain movement costs. These are explicit scenario inputs.
  No cost/weather choice, equipment bonus, unit growth, innate effect, HP, PA, MA,
  Speed, evasion, zodiac, or combat formula is inferred from learning metadata.
- The setup supports Normal movement with no traits and rejects jobs with the
  stepping-stone flag. This explicit base-job prototype does not implement movement
  abilities or statuses merely because their learning definitions resolve.
- Units start with movement unused. The optional RNG snapshot is preserved exactly;
  setup draws nothing and never silently seeds a stream. The turn-cycle wrapper
  requires an explicit RNG snapshot and separately supplied current Speed/tie keys.

Setup rejects empty rosters, duplicate/default runtime IDs, shared/outside/blocked/
nonstoppable starting positions, unsupported body size/stepping stones, missing
terrain costs and a cost that blocks the starting terrain. Cost collections are
snapshotted. All unblocked map surfaces require an explicit cost, including a
deliberately blocked cost where intended. Selecting an actual movement policy is
still the caller's responsibility; this adapter does not expand that policy's
supported geometry domain.

Tests use synthetic definitions and maps. They show shared definitions with
independent unit movement, resolved ability references, authored Move changing a
reachable destination without modifying the simulator, Jump copying, immutable
cost inputs, explicit RNG preservation and startup validation failures. They do
not execute original code or claim a reconstructed equipped retail unit.

Integrated Windows validation: locked restore, Release build with zero warnings/
errors, and all 464 solution tests passed on 2026-10-22.
