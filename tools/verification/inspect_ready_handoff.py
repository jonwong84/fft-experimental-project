"""Offline, closed post-Ready source/state audit; never execute an event or emulator.

Original blocks, states and generated reports stay under scratch. This inspector
recognizes the two selected tail shapes, not an executable event profile.
"""
import argparse
from collections import Counter
import json
from pathlib import Path
import re
import struct
import subprocess
import sys

import inspect_banner_event as event
import compare_ready_display as ready

REVISION = event.c.observed.REVISION
ROOT = event.c.profile_tool.ROOT
SOURCES = (
    'src/battle/event/interpreter.c', 'src/battle/event/frame.c',
    'src/battle/entry.c', 'src/battle/frame.c', 'src/battle/turn/controller.c',
    'src/battle/turn/clock.c', 'src/lib/event/script/scenario.c',
    'src/lib/event/script/scene_commands.c', 'src/lib/event/script/control.c',
    'src/lib/event/scene/control.c', 'src/lib/event/scene/menu_sessions.c',
    'src/lib/event/scene/menu_dispatch.c', 'src/lib/menu/menu_actions.c',
    'src/lib/thread/scheduler.c', 'src/event/require/entry_conditions.c',
    'src/event/require/entry_dark_screen.c', 'src/event/attack/scenario_setup.c',
    'include/fft/script.h', 'include/fft/script_variable.h',
    'include/fft/menu.h', 'include/fft/menu_input.h', 'include/fft/battle_state.h',
    'target/battle.yaml', 'target/main.yaml')
FUNCTIONS = ('script_execute_event', 'script_event_frame_run', 'scenario_finish_operation_get',
             'battle_state_event_update', 'turn_next_event_start', 'battle_state_change_turn_update',
             'battle_state_open_action_menus_enter', 'battle_state_open_action_menus_update',
             'battle_input_update', 'script_event_input_update')


def tail(raw, lengths, ready_offset):
    """Require Ready/E5 then the selected release/speed/[curve]/EventEnd shape."""
    event.v.integer(ready_offset, 4, 8191, 'Ready command offset')
    boundary, records = event.instructions(raw, lengths)
    selected = [i for i, record in enumerate(records) if record[0] == ready_offset]
    event.v.require(len(selected) == 1, 'Ready offset is not an instruction boundary')
    chosen = records[selected[0]:]
    ops = [record[1] for record in chosen]
    event.v.require(ops in ([0x78, 0xe5, 0x77, 0xe5, 0x1c, 0xdb],
                           [0x78, 0xe5, 0x77, 0xe5, 0x1c, 0x63, 0xdb]), 'Unsupported post-Ready tail')
    args = [record[2] for record in chosen]
    event.v.require(len(args[0]) == 2 and args[0][0] == 0 and args[1] == b'\x38\x00'
                    and not args[2] and args[3] == b'\x36\x00' and args[4] == b'\x01'
                    and not args[-1] and (len(chosen) == 6 or len(args[5]) == 1),
                    'Post-Ready operands differ')
    return dict(readyOffset=ready_offset, hold=args[0][1], readyBarrierOffset=chosen[1][0],
                releaseOffset=chosen[2][0], releaseBarrierOffset=chosen[3][0],
                restoreSpeedOffset=chosen[4][0], endOffset=chosen[-1][0], textOffset=boundary,
                remainingRecordCount=len(chosen)-2,
                remainingOpcodeCounts={f'{op:02X}': n for op, n in sorted(Counter(ops[2:]).items())},
                restoresSpeedOne=True, hasCameraCurveRestore=len(chosen) == 7,
                immediateControlAfterReady=False)


def context(ram, event_id):
    """Read source-mapped widths; this is an observation, never a handoff decision."""
    event.v.require(len(ram) == event.d.RAM_SIZE, 'Missing main RAM')
    event.v.integer(event_id, 0, 499, 'bounded event id')
    def read(fmt, address):
        return struct.unpack_from(fmt, ram, address & 0x1fffffff)[0]
    mode = read('<H', 0x80165ff6)
    event.v.require(mode in (0, 1, 2), 'Unsupported menu input mode')
    return dict(eventId=event_id, finishWord=read('<H', 0x8004e5d0 + event_id * 2),
                battleState=read('<I', 0x800960e4), menuInputMode=mode,
                menuOverlayState=read('<H', 0x80166048), scriptThreadsIdle=read('<h', 0x8016d9c0),
                inputSuppression=read('<H', 0x80166050),
                inputPrevious=read('<I', 0x80045948), inputPressed=read('<I', 0x8004594c),
                playerControlObserved=False)


def run(plan_path, reference, output):
    plan_path, output = map(event.c.profile_tool.scratch_path, (plan_path, output))
    event.v.require(not output.exists(), 'Output already exists')
    event.v.require(plan_path.stat().st_size <= 16384, 'Plan exceeds budget')
    plan = event.v.parse(plan_path.read_bytes())
    event.v.fields(plan, 'format schemaVersion archive battle states', 'Handoff plan')
    event.v.require(plan['format'] == 'fft.ready-handoff-plan' and type(plan['schemaVersion']) is int
                    and plan['schemaVersion'] == 1, 'Unsupported handoff plan')
    event.v.require(type(plan['states']) is list and 1 <= len(plan['states']) <= 4, 'Expected 1..4 retained states')
    revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
    event.v.require(revision == REVISION, 'Reference revision differs')
    event.v.require(not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(),
                    'Reference checkout is dirty')
    sources = [dict(path=name, bytes=len(data), sha256=event.v.sha(data))
               for name in SOURCES for data in [(reference / name).read_bytes()]]
    archive, battle = (event.hide.verified(plan[key]) for key in ('archive', 'battle'))
    event.v.require(event.v.sha(archive) == event.ARCHIVE_SHA and event.v.sha(battle) == event.BATTLE_SHA,
                    'Original archive/BATTLE identity differs')
    symbols = (reference / 'target/battle.yaml').read_text(encoding='utf-8')
    functions = []
    for name in FUNCTIONS:
        matches = re.findall(r'addr: (0x[0-9a-f]+), size: (\d+), name: ' + name + r'[,}]', symbols)
        event.v.require(len(matches) == 1, 'Ambiguous function source mapping: ' + name)
        address, size = int(matches[0][0], 16), int(matches[0][1])
        code = battle[address - 0x80067000:address - 0x80067000 + size]
        event.v.require(len(code) == size, 'Mapped function escapes BATTLE')
        functions.append(dict(name=name, address=address, bytes=size, sha256=event.v.sha(code)))
    observations = []
    tails = None
    for item in plan['states']:
        ram, lengths = event.read_ram(event.hide.verified(item))
        for function in functions:
            start = function['address'] & 0x1fffffff
            event.v.require(event.v.sha(ram[start:start+function['bytes']]) == function['sha256'],
                            'Resident handoff function differs: ' + function['name'])
        # Reuse the separately closed Ready classifier: unlike the older active-
        # banner inspector, it covers the dispatcher-clear observation gap.
        identity = ready.event_identity(ram, archive, lengths)
        worker = ready.threads(ram, 9003)
        current_tails = [dict(eventId=id, rawBlockSha256=event.v.sha(raw), **tail(raw, lengths, at))
                         for id, at in ((4, 810), (401, 192))
                         for raw in [archive[id*8192:(id+1)*8192]]]
        event.v.require(tails is None or tails == current_tails, 'Tail interpretation changed across states')
        tails = current_tails
        active = [dict(slot=slot, taskId=event.d.word(ram, event.THREADS + slot*1024 + 0x4c))
                  for slot in range(1, 16) if event.d.word(ram, event.THREADS + slot*1024 + 0x48)]
        observations.append(dict(state=item, event=identity, ready=worker,
                                 frameCounter=event.d.word(ram, event.hide.FRAME_COUNTER),
                                 battleNotStarted=event.packed_flag(ram, 0x1fd),
                                 scenarioEventActive=struct.unpack_from('<h', ram, 0x173cb0)[0],
                                 gates=context(ram, 4), activeThreads=active))
    report = dict(format='fft.ready-handoff-source-audit', schemaVersion=1, referenceRevision=revision,
                  managedBase=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                  plan=event.c.identity(plan_path), sourceFiles=sources, originalInputs=plan,
                  residentFunctions=functions, tails=tails, observations=observations,
                  tools=[event.c.identity(Path(module.__file__)) for module in
                         (sys.modules[__name__], event, ready, event.hide, event.c, event.v, event.d)],
                  newOriginalCaptures=False, emulatorLaunched=False, runtimeChanged=False,
                  playerControlObserved=False, originalCompletionDurationEstablished=False)
    output.mkdir(parents=True)
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--reference-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        report = run(args.plan, args.reference_root, args.output)
        print(f"Audited {len(report['tails'])} selected tails and {len(report['observations'])} retained states; no player-control observation.")
    except (ValueError, OSError, KeyError, TypeError, subprocess.CalledProcessError) as error:
        parser.exit(2, 'Handoff source audit failed: ' + str(error) + '\n')
