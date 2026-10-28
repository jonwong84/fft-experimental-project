# Fresh ordinary preparation — ZOD-161

Implemented against read-only fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.
The [source contract](../architecture/zod-160-ai-preparation.md) records the
selected NonBlue, already-moved ordinary Attack-only context and remaining
source-list/special-behavior/cache assertions.

`SwordAiPreparation.Evaluate` owns one fresh `SwordAiRefresh` invocation. It never
accepts a cached refresh output. After success it derives the current-tile origin,
focused opponent position, restored live actor/target delays, proximity and raw
Move+1/Move enemy/ally reach. Single-ally support preparation leaves retreat support
and SingleEnemyRemaining false, sets the NonBlue actor's support-unavailable
policy, and preserves the peer's policy. Manual autobattle, active target and zero
random-use threshold follow from the selected ordinary ability/policy context.
Earlier refresh diagnostics retain their earlier policy values; the separate
Policies output is post-team-update.

No new movement search, score or RNG draw follows refresh. Success can consume the
final available draw. Failure returns the original hypothetical RNG and no partial
output, including refresh counter/tick failures. Input battle/turn state remains
unchanged; no session, receipt or command authority is added. Blue and unknown team,
uncertain source context and raw Move above the current 124 scenario domain reject.

Eight focused cases cover both cached support paths, Critical, restored CT versus
projected CT, delay/equality boundaries, either actor/order replay and counter/
context rejection. All 700 AI tests pass after a warning-free AI project build.
Logs: ignored `scratch/ai-preparation-161/`. The previous full solution run is
ZOD-159's 3,863 .NET / 504 Python; it is not rerun at this checkpoint. Full batch
validation follows ZOD-162's pure composition. No original/C execution or UI review.
