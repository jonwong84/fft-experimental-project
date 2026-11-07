# Pure fresh post-ACT refresh — ZOD-178

Implements the [post-ACT source contract](../architecture/zod-176-ai-act-first.md)
at read-only clean fft_decomp `11464943bbdbd2a0af022d90cb7f10455cf911df`.

`SwordAiRefresh.EvaluateAfterAct` requires an active ordinary actor with Move
unused and ACT used. It shares existing flags, policy rows and private simulation
clock logic. Choice admission remains explicit: old Evaluate requires Move used/
ACT unused, and EvaluateUnmoved requires both unused. No synthetic ACT-unused
snapshot is created and no old guard is weakened.

The source turn_end calculation now names each actual unused-choice refund.
For this entry only unused Move adds20, yielding min(60, liveCT+20) before byte
storage. Refresh's action-first/skillset-zero simulation performs neither move
nor action. Actual post-ACT HP/status, facing, positions, CT and supplied decision
RNG are the baseline. One closed living upkeep draw is retained in the query
result; hypothetical CT/choices/ticks never replace live state. Failure returns
original hypothetical RNG and no partial flags/clock result.

FreshRefreshEntry, targetability, cached MP and ordinary source/profile facts
remain required. ACT used alone does not prove the earlier decided/saved-choice
no-progress guard passed. This pure entry neither inspects command history nor
grants command authority. Terminal/unsupported data retains current admission.

Real ACT-first full hit ends word3791773014/count24 and target HP25. Fresh
post-ACT refresh takes raw21183, ending word3535774167/count25. The low-accuracy
miss ends word2473755763/count29 and HP50; refresh takes raw20495 and ends
word1343201072/count30. Both temporarily end actor CT35, pick the peer after one
addition and expose actor40/peer0 diagnostically. Live actor CT15, ACT used/Move
unused, positions and settled East facing remain unchanged. HP25 is exactly
maxHP/2 and therefore does not set the source's strict below-half flag.

Four scenario tests cover these actual-input streams, choice-mode isolation,
source admission, terminal rejection, exact draw/tick limits, CT cap/tie and both
actor/order replay. Twenty-seven focused refresh/preparation cases pass. Locked
restore, zero-warning/error Release build and **3,953 .NET cases** pass. Logs and
seven full TRX files are ignored under `scratch/ai-post-act-refresh-178/`; the clean
pinned inherited/ACT-first source audit passes there. Python is unchanged; its
last full run remains **516 tests at ZOD-177**. Owner content drafts were included
in validation and excluded from this commit. No C/original execution or UI review;
bounded checks do not establish full original AI equivalence.

ZOD-179 composes this owned refresh with restored-state post-ACT preparation,
retaining reach computed before the actor's usable Attack bit is cleared. Scoring,
selection and live post-ACT handoff remain subsequent work.
