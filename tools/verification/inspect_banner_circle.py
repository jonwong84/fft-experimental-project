"""Source-bound neutral -> Circle skip -> task-clear witness; no input injection or emulator launch."""
import argparse
import json
from pathlib import Path
import sys

import capture_banner_continuation as c
import compare_banner_hide as h
import compare_visual_checkpoints as v
import trace_banner_draw_state as d

INPUT_GETTER = (0x8014a578, 32, '1827206fea7090f98835a0fbd5b1dc9f3ebf5caf7f48c5f60d76b6e772439c1f')
INPUTS = dict(residentCurrent=0x80045944, residentPrevious=0x80045948, residentPressed=0x8004594c,
              eventPressed=0x801697d0, eventHeld=0x8016d9a0)


def read_state(data):
    ram = c.read_ram(data)
    for address, length, digest in (*h.CODE, INPUT_GETTER):
        offset = address & 0x1fffffff
        v.require(v.sha(ram[offset:offset + length]) == digest, 'Frame/input getter code differs')
    return ram


def inputs(ram):
    return {key: d.word(ram, address) for key, address in INPUTS.items()}


def no_banner(ram):
    current = d.word(ram, d.CURRENT_BUFFER)
    v.require(current in (d.BUFFER_BASE, d.BUFFER_BASE + d.BUFFER_STRIDE), 'Unexpected render buffer')
    ot = d.word(ram, current + 0xe5b4)
    v.require(ot in (0x80056b18, 0x80057118), 'Unexpected ordering table')
    packets, _ = d.walk(ram, ot + 0x17f * 4)
    v.require(not any((packet - (d.BANNER_BASE & 0xffffff) - bank * d.BANNER_BANK_STRIDE) in range(0, 6 * 52, 52)
                     for packet, _ in packets for bank in (0, 1)), 'Skipped/cleared task still submits banner packets')


def witness(before, after, definition):
    initial = c.inspect_ram(before, definition, early_reveal=True)
    v.require(initial['phase'] in ('Revealing', 'Holding'), 'Expected a pre-skip reveal or hold yield')
    v.require(all(value == 0 for value in inputs(before).values()), 'Pre-press input is not neutral')
    v.require(all(value == 0x20 for value in inputs(after).values()), 'Expected only newly pressed/held Circle')
    v.require(d.word(after, c.THREADS_POINTER) == c.THREADS_BASE, 'Unexpected thread table')
    base = c.THREADS_BASE + initial['slot'] * 1024
    v.require([d.word(after, base + x) for x in (0, 4, 0x44, 0x48, 0x4c)] == [8, 60, 0x801c9f68, 1, 56],
              'Expected retained condition-8 task at the shared extra yield')
    sp = d.word(after, base + 0x3c)
    v.require(sp == d.word(before, base + 0x3c) and base + 0x6c <= sp and sp + 0x58 <= base + 0x400 and sp % 4 == 0,
              'Skip stack changed or escaped its slot')
    v.require([d.word(after, sp + x) for x in (0x10, 0x18, 0x20)] == [60, initial['phaseCounter'], 6],
              'Skip changed the loop counter/hold/piece count')
    v.require(d.word(after, 0x801d208c) == INPUTS['eventPressed'], 'Banner input pointer differs')
    v.require(d.word(after, h.FRAME_COUNTER) == d.word(before, h.FRAME_COUNTER) + 1, 'Expected exactly one source frame')
    no_banner(after)
    return dict(slot=initial['slot'], before=initial,
        after=dict(logicalTick=initial['logicalTick'] + 1, phase='ExitYield', revealCompleted=initial['revealCompleted'],
            holdCompleted=initial['holdCompleted'], hideCompleted=0, exitCause='CircleSkipped', isTerminal=False, draws=[]),
        beforeFrameCounter=d.word(before, h.FRAME_COUNTER), afterFrameCounter=d.word(after, h.FRAME_COUNTER),
        beforeInput=inputs(before), afterInput=inputs(after), savedReturnAddress=0x801c9f68,
        evidence='Paired pre-phase, unchanged loop counter, Circle-only event/resident input, shared yield and empty queued banner list; return address alone is insufficient.')


def cleared(ram, observed):
    base = c.THREADS_BASE + observed['slot'] * 1024
    v.require(d.word(ram, c.THREADS_POINTER) == c.THREADS_BASE, 'Unexpected thread table')
    v.require([d.word(ram, base + x) for x in (0, 4, 0x44, 0x48, 0x4c)] == [8, 60, 0x8014c984, 0, 0],
              'Expected selected slot cleared at scheduler exit, without reuse')
    v.require(d.word(ram, h.FRAME_COUNTER) == observed['afterFrameCounter'] + 1, 'Task clear is not the next source frame')
    v.require(all(value == 0 for value in inputs(ram).values()), 'Expected neutral no-input continuation')
    no_banner(ram)
    # The dead thread stack is not an active loop counter. Retain facts from the paired witness.
    return dict(observed['after'], logicalTick=observed['after']['logicalTick'] + 1, phase='Completed', isTerminal=True)


def run(plan_path, output):
    plan_path, output = map(c.profile_tool.scratch_path, (plan_path, output))
    v.require(not output.exists(), 'Output already exists')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = v.parse(plan_path.read_bytes()); v.fields(plan, 'format schemaVersion definition before after exits', 'Circle plan')
    v.require(plan['format'] == 'fft.banner-circle-observation-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
              'Unsupported Circle plan')
    v.require(type(plan['exits']) is list and len(plan['exits']) == 2 and plan['exits'][0]['localPath'] != plan['exits'][1]['localPath'], 'Expected two distinct continuation captures')
    tools = [c.identity(Path(module.__file__)) for module in (sys.modules[__name__], c, h, d, v)]
    definition = v.parse(h.verified(plan['definition']))
    before, after = (read_state(h.verified(plan[key])) for key in ('before', 'after'))
    observed = witness(before, after, definition)
    exits = [cleared(read_state(h.verified(item)), observed) for item in plan['exits']]
    v.require(exits[0] == exits[1] and plan['exits'][0]['sha256'] == plan['exits'][1]['sha256'], 'Continuation results differ')
    for tool in tools: v.require(c.profile_tool.file_hash(c.profile_tool.ROOT / tool['localPath']) == tool['sha256'], 'Tool changed during inspection')
    report = dict(format='fft.original-banner-circle-observation', schemaVersion=1, referenceRevision=c.observed.REVISION,
        plan=c.identity(plan_path), tools=tools, inputs=plan, witness=observed, terminal=exits[0],
        observations=[dict(observed['before'], exitCause=None, isTerminal=False), observed['after'], exits[0]],
        limits=['One source-bound controller press observation; physical action comes from the owner report plus saved input fields.',
            'No USB-to-screen latency, repeatable BSV playback, other physical input phase or original event identity claim.',
            'Images are context only; queued draw absence is not instantaneous visible disappearance.'])
    output.mkdir(parents=True); (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        report = run(args.plan, args.output)
        print(f'Circle skip: logical {report["witness"]["before"]["logicalTick"]} -> {report["witness"]["after"]["logicalTick"]} -> {report["terminal"]["logicalTick"]}; source witness verified.')
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(2, 'Circle observation failed: ' + str(error) + '\n')
