# ZOD-326: static ability AI metadata and content binding

Source study 2026-11-05 from managed `891f4c0`, read-only fft_decomp clean at
`11464943bbdbd2a0af022d90cb7f10455cf911df`.
[Receipt](../evidence/ai/ai-behavior-source-v1.json): five files/six functions.
[Thirty-seven independent descriptions](../fixtures/behavior/ai-behavior-contract-v1.json)
cover every static bit, retail Accumulate/Stone/Dash and zero/all-set records.
No original/C execution.

## Separate three meanings

The primary four AI bytes describe preferences and classification, not an
executable formula. `ai_unit_abilities_init` reads Silence/Evadeable for list
shares, while `ability_considerations_set` reads enemy/ally, HP and physical/
magical flags for summaries and policy. `ai_target_classify_ability_effect`
uses preferences separately from absolute enemy-only/ally-only working flags.
Execution flags and formula data are separate secondary records: changing an AI
evasion classification alone does not change the actual combat evasion rule.

`ai_ability_entry_load` copies the primary record but replaces its fourth byte
with the initialized entry's usage byte. `ai_unit_abilities_init` recomputes
exhaustive-origin/force-consume behavior from trajectory, area/range and secondary
combat flags. The static fourth byte's low bit is named EvadeWithMotion in the
pinned source; it must not become consume-evaluated-aim merely because those
bits overlap in memory. The static high bit controls inclusion as AI-usable.
Remaining static fourth-byte bits stay unresolved/preserved for this boundary.

Use positive `AffectedBySilence`, matching the source test. Throw Stone has it
clear. Do not canonize editor labels such as Ignore Silence or Non-Spear Target
into independent runtime policies without tracing their exact mapping. Likewise,
the original four third-byte area-shape bits feed the union tested by
initialization. [ZOD-330](../behavior/readable-ai-authoring.md) preserves the
individual named shapes for lossless authoring; it adds no spear/shape behavior.

## Selected implementation boundary

ZOD-327 adds immutable semantic `AiAbilityBehavior` in the existing AI assembly,
with original bit packing confined to a Content decoder. `AbilityMetadataDefinition`
owns its decoded view plus static usable/motion/unresolved facts. Its constructor
copies raw bytes; derived traits are not a second editable source. Keep package
schema1 and `aiFlagsRaw` serialization byte-for-byte compatible. Unknown type bit4
and all raw AI bytes survive round trips even when unsupported by an evaluator.

ZOD-328 carries explicit metadata traits through the Content adapter into owned
semantic slots/lists. Helpful/harmful, evade/Silence shares, ranged classification
and enemy physical summaries read these traits. Existing explicitly authored
slots retain documented retail defaults for compatibility. Supported-effect
guards check semantic compatibility rather than relying solely on effect ID or
comparing opaque primary byte arrays in the setup adapter.

The current evaluator supports retail Accumulate/Stone/Dash combinations only.
Modified enemy/ally, Silence/Faith/status/random-use, evasion or geometry policies
are not silently simulated as their retail ability. Reject incompatible entered
combinations until the corresponding policy/search support exists. Decoding a
trait does not establish its full behavior. Definition and editor import remain
able to preserve unsupported original values.

## Dash content gap and next child

This gap is now closed by [ZOD-329](../development/dash-content-binding.md). The
original scope below records the pre-binding state. Retail primary AI Evadeable
and secondary combat evasion flags are distinct and both are preserved.


Actual Dash and its AI entry exist, but `TargetedAbilityEffect` and its prepared
package currently bind only ThrowStoneFlat. The Content AI adapter accepts one
self effect and one Stone effect; its explicit known slots are separate from
learned-action grants. A Dash ID or AI bytes cannot grant or activate its effect.
ZOD-329 should add one bounded prepared Dash effect/grant/setup binding and feed
its decoded primary traits through this same semantic path, preserving existing
schemas/adapters and public pair guards. This is a concrete child, not a broad
effect plug-in contract.

No owner draft, JobStats mutability, runtime C/BIN dependency, automatic facing,
full corpse/terminal/frame RNG, host/editor activation, broad interfaces or
ZOD-106 is selected. Existing actual-rule classes remain separate from AI
decision-making; this change does not rename battle execution as AI.
