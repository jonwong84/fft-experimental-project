"""Identify frozen BATTLE event blocks and suspended waits; no execution or original-byte output."""
import argparse
import json
from pathlib import Path
import struct
import sys

import analyze_banner_precision as precision
import capture_banner_continuation as c
import compare_banner_hide as hide
import compare_visual_checkpoints as v
import trace_banner_draw_state as d

ARCHIVE_SHA = '107b109254ccbb150aca7baf336b632837b22e340677e2b9e746ac61b3de4405'
BATTLE_SHA = '7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94'
LENGTHS_SHA = '5f50d72e0e4d2a62215be25bc01f9123c0727a2abdcb9a4ff038511c569d915f'
CODE = (
    (0x80143bd8, 9120, 'f5e240270b084a495426b78bb62b981948943db94841969f4b2305b75301a53f'),
    (0x8013c9c0, 176, '300656fe0572dc7f7fb881758622416a1d3c91833a4e0ebdafdb8f09dc682187'),
    (0x80145f78, 140, '86e825112a99969c4437f207a4c82894f9c0284669a4c9bd7d72da50c8b79ac2'),
    (0x8014c858, 72, 'e33123533d666f7170fe364c17f5fa829fd46c0e0e861171c40813a76af85106'),
    (0x8014ca80, 200, '7243793c2998187ad65ff51653243654c913e6f751edfc172c82adf8d3b7154b'),
    (0x8014a018, 724, '71d9b10690838d9e55ad37be7c34b5f154e8352f16a64145671c992b70cfe1c0'),
    (0x8013b590, 180, '92bfa64b3cd0e089bd1968db0989a0f529325f61de3ade4689d473718f2cd4a3'),
    (0x8013bd6c, 40, '889b5b3e5f2d0343858f4954407eb202a81072173c76dd4d7606e7e54f76d0cd'),
)
BLOCK, VARIABLES, THREADS = 0x8004a6bc, 0x8005771c, c.THREADS_BASE


def read_ram(state):
    ram = precision.subsystem_records(precision.unpack_state(state), b'MAIN').get('MainRAM.data8', b'')
    v.require(len(ram) == d.RAM_SIZE, 'Missing main RAM')
    for address, size, digest in CODE:
        v.require(v.sha(ram[address & 0x1fffffff:(address & 0x1fffffff) + size]) == digest, 'Resident event code differs')
    lengths = ram[0x14d170:0x14d170 + 244]
    v.require(v.sha(lengths) == LENGTHS_SHA, 'Original instruction lengths differ')
    return ram, lengths


def packed_flag(ram, variable_id):
    v.integer(variable_id, 0x80, 0x35f, 'packed flag id')
    word = d.word(ram, VARIABLES + 4 * (0x80 + (variable_id - 0x80) // 32))
    return (word >> (variable_id & 31)) & 1


def instructions(raw, lengths):
    v.require(len(raw) == 8192 and len(lengths) == 244, 'Unexpected block/length table')
    end = struct.unpack_from('<I', raw)[0]
    v.integer(end, 5, 8191, 'raw text boundary')
    records, offset = [], 4
    while offset < end:
        opcode = raw[offset]
        v.require(opcode < len(lengths), 'Unknown opcode length')
        size = lengths[opcode]
        v.require(offset + 1 + size <= end, 'Operands cross text boundary')
        records.append((offset, opcode, raw[offset + 1:offset + 1 + size]))
        offset += 1 + size
        if opcode == 0xdb:
            v.require(offset == end, 'Sentinel does not end at text boundary')
            return end, records
    raise ValueError('Missing final sentinel')


def inspect(ram, archive, lengths):
    v.require(len(archive) == 4096000, 'Expected bounded 500-slot archive')
    v.require(d.word(ram, 0x80165f9c) == VARIABLES and d.word(ram, 0x80173ca4) == BLOCK
              and d.word(ram, c.THREADS_POINTER) == THREADS, 'Event/variable/thread pointer differs')
    current, loaded = d.word(ram, VARIABLES + 0x27 * 4), d.word(ram, 0x801696fc)
    v.integer(current, 0, 499, 'event id')
    v.require(current == loaded, 'Current and cached loaded events differ')
    live = ram[BLOCK & 0x1fffffff:(BLOCK & 0x1fffffff) + 8192]
    v.require(live[:4] == b'\xf2' * 4, 'Event is not loader-processed')
    matches = [index for index in range(500) if archive[index * 8192 + 4:(index + 1) * 8192] == live[4:]]
    v.require(matches == [current], 'Loaded payload has no unique matching current archive block')
    raw = archive[current * 8192:(current + 1) * 8192]
    boundary, records = instructions(raw, lengths)
    v.require(d.word(ram, 0x80173f8c) == BLOCK + boundary, 'Event text pointer differs')
    base = THREADS + 1024
    v.require([d.word(ram, base + off) for off in (0x48, 0x4c)] == [1, 68], 'Event slot is not an active interpreter')
    offset = d.word(ram, base + 0x40)  # native $fp carries instruction_offset, not a stack address
    selected = [(index, record) for index, record in enumerate(records) if record[0] == offset]
    v.require(len(selected) == 1, 'Saved instruction offset is not a command boundary')
    index, (_, opcode, operands) = selected[0]
    v.require(d.word(ram, base + 0x20) == opcode, 'Saved interpreter opcode differs')
    sp, pc = d.word(ram, base + 0x3c), d.word(ram, base + 0x44)
    v.require(sp % 4 == 0 and base + 0x6c <= sp and sp + 0x50 <= base + 0x400, 'Interpreter stack escapes slot')
    value = int.from_bytes(operands, 'little')
    if pc == 0x8014c87c and opcode == 0xf1 and len(operands) == 2:
        parent_sp = sp + 0x28
        v.require(parent_sp + 0x50 <= base + 0x400 and d.word(ram, sp + 0x20) == 0x80145a34,
                  'Frame-wait caller differs')
        v.require(not 0x19a <= current <= 0x1a9 and d.word(ram, base + 0x14) == value
                  and 1 <= d.word(ram, base + 0x10) <= value,
                  'Frame-wait count/progress differs')
        wait = dict(kind='FrameWait', count=value, yieldOrdinal=d.word(ram, base + 0x10))
    elif pc == 0x8014597c and opcode == 0xe5 and len(operands) == 2:
        parent_sp = sp
        v.require(d.word(ram, base + 0x14) == THREADS and d.word(ram, base + 0x28) == value,
                  'Task wait table/selector differs')
        wait = dict(kind='TaskWait', taskId=value)
    else:
        raise ValueError('Unsupported suspended interpreter site')
    v.require(d.word(ram, parent_sp + 0x4c) == 0x80143b98, 'Interpreter event-thread caller differs')
    banners = []
    for record_index, (at, op, args) in enumerate(records):
        if op == 0x78:
            v.require(len(args) == 2, 'Unexpected banner operand size')
            following = records[record_index + 1] if record_index + 1 < len(records) else None
            banners.append(dict(offset=at, condition=args[0], hold=args[1],
                followingWaitOffset=following[0] if following and following[1] == 0xe5 else None,
                followingWaitTaskId=int.from_bytes(following[2], 'little') if following and following[1] == 0xe5 else None))
    tasks = []
    for slot in range(1, 16):
        address = THREADS + slot * 1024
        if d.word(ram, address + 0x48) and d.word(ram, address + 0x4c) == 56:
            tasks.append(dict(slot=slot, condition=d.word(ram, address), hold=d.word(ram, address + 4),
                savedReturnAddress=d.word(ram, address + 0x44)))
    attribution = [banner for banner in banners if banner['followingWaitOffset'] == offset and wait == dict(kind='TaskWait', taskId=56)]
    if attribution:
        v.require(len(attribution) == 1 and len(tasks) == 1 and
                  (tasks[0]['condition'], tasks[0]['hold']) == (attribution[0]['condition'], attribution[0]['hold']),
                  'Adjacent banner command does not match the active worker')
    return dict(currentEventId=current, loadedEventId=loaded, uniqueArchiveMatch=matches,
        rawBlockSha256=v.sha(raw), processedBlockSha256=v.sha(live), textOffset=boundary,
        instructionCount=len(records), instructionOffset=offset, opcode=opcode, wait=wait,
        eventTaskId=68, eventSlot=1, savedReturnAddress=pc,
        frameCounter=d.word(ram, hide.FRAME_COUNTER), scenarioEventActive=d.word(ram, 0x80173cb0) & 0xffff,
        battleNotStarted=packed_flag(ram, 0x1fd), debugBattleStyle=packed_flag(ram, 0x1fe),
        bannerCommands=banners, activeBannerTasks=tasks, attributedBannerOffset=attribution[0]['offset'] if attribution else None,
        commonBattleStartEventObserved=current == 401)


def run(plan_path, output):
    plan_path, output = map(c.profile_tool.scratch_path, (plan_path, output))
    v.require(not output.exists(), 'Output already exists')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = v.parse(plan_path.read_bytes())
    v.fields(plan, 'format schemaVersion archive battle states', 'Event identity plan')
    v.require(plan['format'] == 'fft.banner-event-identity-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
              'Unsupported event identity plan')
    v.require(type(plan['states']) is list and 1 <= len(plan['states']) <= 32, 'Expected 1..32 frozen states')
    archive, battle = (hide.verified(plan[key]) for key in ('archive', 'battle'))
    v.require(v.sha(archive) == ARCHIVE_SHA and v.sha(battle) == BATTLE_SHA, 'Original archive/BATTLE identity differs')
    for address, size, digest in CODE:
        v.require(v.sha(battle[address - 0x80067000:address - 0x80067000 + size]) == digest, 'BATTLE code range differs')
    tools = [c.identity(Path(module.__file__)) for module in (sys.modules[__name__], precision, c, hide, v, d)]
    observations = []
    for item in plan['states']:
        ram, lengths = read_ram(hide.verified(item))
        observations.append(dict(state=item, **inspect(ram, archive, lengths)))
    for tool in tools:
        v.require(c.identity(c.profile_tool.ROOT / tool['localPath']) == tool, 'Tool changed during inspection')
    report = dict(format='fft.original-banner-event-identity', schemaVersion=1, referenceRevision=c.observed.REVISION,
        plan=c.identity(plan_path), inputs=plan, tools=tools,
        code=[dict(address=a, bytes=n, sha256=s) for a, n, s in CODE], observations=observations,
        limits=['Saved-state attribution and source study only; no emulator or original instructions executed.',
            'Earliest state is already inside the event; its entry/conditional selection is not observed.',
            'No complete event playback, current-event-401 fallback, encounter name, new pixel comparison or latency claim.'])
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
        print('Verified current/loaded/payload event identities: ' + ', '.join(str(item['currentEventId']) for item in result['observations']))
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(2, 'Event identity inspection failed: ' + str(error) + '\n')
