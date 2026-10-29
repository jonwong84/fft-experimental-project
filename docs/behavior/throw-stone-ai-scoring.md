# Fresh absolute Throw Stone candidate scoring — ZOD-188

Implemented 2026-10-29 against clean read-only fft_decomp
`11464943bbdbd2a0af022d90cb7f10455cf911df` and the
[ZOD-185 contract](../architecture/zod-185-ai-ability-iteration.md).

`ThrowStoneAiCandidateScoring.Evaluate` owns one fresh supplied
[candidate projection](throw-stone-ai-candidate.md) and applies the existing
absolute allied-minus-enemy HP/status/equipment arithmetic. It accepts original
state/candidate/profiles/RNG, never a caller-populated public outcome. No extra
RNG is consumed after projection. NotValuable has no score or clock output;
profile rejection precedes projection, and failed projection retains its typed
reason without a partial score or progressed whole-query RNG.

The second action family extracts neutral `AiScoreProfile`, `AiScoreFacts`,
`AiScoreUnitCertificate`, `AiCandidateScore`, `AiUnitScoreContribution` and scoring
disposition/rejection names. Repository callers are updated. The shared internal
`AiCandidateScoring` retains source order and signed16 narrowing after each unit.
Attack's scoring and certificates retain their arithmetic and semantics; no
parallel score implementation or replacement-policy interface is added. These
certificates assert human/unchanged-seven-slot/stable-opponent/zero-MP/Golem
facts absent from the live model; an ability ID alone does not establish them.

HP contributes floor(HP×128/maxHP), Critical -32, Dead -192, and unchanged human
equipment7×51 per unit. From actor HP50/max50 and target HP50/max50, projected
damage10 leaves HP40 and priority26. Accuracy100 and25 both retain priority26,
with different separate hit percentages. HP20→10/Critical gives priority135;
HP10→0/Dead gives priority320. Neither weighted damage nor Throw Stone AI
knockback is introduced.

Four scenario tests cover those literals, fresh-projection/RNG equivalence,
zero accuracy, certificate rejection, exact/late exhaustion and actor/order
replay. Shared clock/score matrices are reused. Across ZOD-186/187/188, 14 new
scenario tests pass with all 813 AI cases. Locked restore, zero-warning/error
Release build, **4,000 .NET cases / 519 Python tests** and the pinned source/data
audit pass. Logs and seven full TRX files: ignored `scratch/ai-throw-stone-186-188/`.
Owner content drafts are included in validation and excluded from these commits.
No C/original execution, authoritative command or host/UI activation.

ZOD-189 adds a fresh certified movement-only baseline and comparison for this
same ability profile. Candidate/list enumeration, multi-entry rank insertion,
lottery and live ability execution remain separate work.
