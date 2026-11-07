# Actual roster self ability session — ZOD-305

Implemented 2026-11-05 from `ad34551`, following the
[execution prerequisite study](../architecture/zod-304-roster-ai-execution.md).
Reference remains clean/read-only at `11464943bbdbd2a0af022d90cb7f10455cf911df`;
[receipt](../evidence/ai/roster-execution-source-v1.json): 79 functions/26 files.

`RosterFacingTurnBattle.UseImmediateAbility` exposes the existing complete-roster
`ImmediateAbilityBattleSimulator.Apply` through the existing private session
Apply/Publish owner. It requires the real typed effect and actor grant, exact
zero-bonus unit bindings, PA1..99, unused ACT and the existing living ordinary
domain. It changes only actor PA by +1 capped99, consumes ACT, and draws nothing.
The rule already supported rosters; no pair guard or formula was broadened.

ACT with unused Move leaves that choice open. After an actual Move, ACT ends
the turn and issues the normal completion receipt. Unused Move followed by Wait
uses the existing CT refund/cap. Only the exact lineage/revision/battle/actor
receipt can settle explicit fixed/kept facing. Missing grants, incomplete setup,
pending facing or revision exhaustion leave the current session unchanged;
an earlier accepted Move remains accepted when the later ACT rejects.

Eight independent [actual settlement vectors](../fixtures/behavior/roster-execution-contract-v1.json)
cover PA1/5/98/99, both choice states and CT cap/refund. Four managed cases cover
those vectors, both actual orders, foreign receipts, grant/setup/revision guards,
accepted Move preservation, collection-order replay and exact parity with the
existing actual self kernel. Three Python cases verify source vectors without
running managed/original/C code.

Validation: locked restore, zero-warning/error Release build, **4,400 .NET cases**
(1,487 Framework, 1,168 AI), **578 Python tests**, source/guide/coverage/link and
unchanged owner-draft audits. Logs: `scratch/roster-execution-304-305/`;
[implementation receipt](../evidence/ai/roster-execution-implementation-v1.json).

This grants no AI decision RNG adoption, cached-plan authority, roster automatic
facing or complete-turn driver. ZOD-306 next traces actual roster Stone
interception/knockback admission before its runtime child. All other documented
corpse/terminal/frame RNG, host/editor, owner-draft, interface and ZOD-106
deferrals remain separate.
