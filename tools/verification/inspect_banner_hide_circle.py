"""Paired original hide-phase Circle witness; reads frozen states, never injects input."""
import argparse
from pathlib import Path
import json
import sys

import inspect_banner_circle as circle

c, v, d = circle.c, circle.v, circle.d


def witness(before, after, definition):
    initial = c.inspect_ram(before, definition)
    v.require(initial['phase'] == 'Hiding' and initial['phaseCounter'] < 9,
              'Expected hide yield before its last iteration')
    current = c.inspect_ram(after, definition, initial['slot'])
    v.require(current['phase'] == 'Hiding' and current['phaseCounter'] == initial['phaseCounter'] + 1,
              'Circle did not preserve the next normal hide yield')
    v.require(all(value == 0 for value in circle.inputs(before).values()), 'Pre-press input is not neutral')
    v.require(all(value == 0x20 for value in circle.inputs(after).values()), 'Expected only newly pressed/held Circle')
    v.require(d.word(after, circle.h.FRAME_COUNTER) == d.word(before, circle.h.FRAME_COUNTER) + 1,
              'Expected exactly one source frame')
    base = c.THREADS_BASE + initial['slot'] * 1024
    v.require(d.word(before, base + 0x3c) == d.word(after, base + 0x3c), 'Hide stack changed')
    v.require(d.word(after, base + 0x2c) == (d.word(before, base + 0x2c) ^ 1), 'Hide buffer did not alternate')
    v.require(len(definition['pieces']) == 6 and all(piece['height'] == 31 for piece in definition['pieces']),
              'Expected six 31-row pieces')
    for observed in (initial, current):
        crop = min(31, max(0, observed['phaseCounter'] - 1) * 8)
        v.require(observed['draws'] == [dict(PieceIndex=index, CropTop=crop, VisibleRows=31 - crop) for index in range(6)],
                  'Hide counter does not explain source crop')
    return dict(before=initial, after=current, beforeInput=circle.inputs(before), afterInput=circle.inputs(after),
        beforeFrameCounter=d.word(before, circle.h.FRAME_COUNTER), afterFrameCounter=d.word(after, circle.h.FRAME_COUNTER),
        evidence='Neutral -> Circle-only input over one source update; same active hide task/stack, next loop count, alternating buffer and exact six-piece crops. No skip yield.')


def run(plan_path, output):
    plan_path, output = map(c.profile_tool.scratch_path, (plan_path, output))
    v.require(not output.exists(), 'Output already exists')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = v.parse(plan_path.read_bytes())
    v.fields(plan, 'format schemaVersion definition before after', 'Hide Circle plan')
    v.require(plan['format'] == 'fft.banner-hide-circle-observation-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
              'Unsupported hide Circle plan')
    tools = [c.identity(Path(module.__file__)) for module in (sys.modules[__name__], circle, c, circle.h, d, v)]
    definition = v.parse(circle.h.verified(plan['definition']))
    before, after = (circle.read_state(circle.h.verified(plan[key])) for key in ('before', 'after'))
    observed = witness(before, after, definition)
    for tool in tools:
        v.require(c.identity(c.profile_tool.ROOT / tool['localPath']) == tool, 'Tool changed during inspection')
    report = dict(format='fft.original-banner-hide-circle-observation', schemaVersion=1,
        referenceRevision=c.observed.REVISION, plan=c.identity(plan_path), tools=tools, inputs=plan, witness=observed,
        observations=[dict(observed[key], exitCause=None, isTerminal=False) for key in ('before', 'after')],
        limits=['One owner-operated hide-phase Circle press; physical provenance is the owner report plus frozen input fields.',
            'One source update only; no subsequent natural completion, USB/display latency, input-movie playback or original event identity claim.',
            'Queued draw geometry is verified; screenshots are context, not a new pixel comparison.'])
    output.mkdir(parents=True)
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = run(args.plan, args.output)
        print(f'Hide Circle ignored: logical {result["witness"]["before"]["logicalTick"]} -> {result["witness"]["after"]["logicalTick"]}; source witness verified.')
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(2, 'Hide Circle observation failed: ' + str(error) + '\n')
