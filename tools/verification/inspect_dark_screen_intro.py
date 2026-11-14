"""Offline source/event/held-layer audit for ZOD-096; no emulator or original execution.

This is a closed inspector, not an interpreter or implementation of the future scene.
Original bytes and generated reports remain under ignored scratch.
"""
import argparse
import json
from pathlib import Path
import re
import struct
import subprocess
import sys

import inspect_dark_screen as dark
import inspect_ready_handoff as handoff

event, v, d = dark.event, dark.event.v, dark.event.d
ROOT = event.c.profile_tool.ROOT
REVISION = dark.REVISION
SOURCES = tuple(sorted(set(dark.SOURCES + handoff.SOURCES + (
    'src/event/require/conditions.c',
    'src/platform/psyq/libgpu/DrawOTag.c',))))


def selected_sequence(raw, lengths, start, event_id):
    v.require(event_id in (4, 401), 'Only selected event shapes are supported')
    boundary, records = event.instructions(raw, lengths)
    matches = [i for i, item in enumerate(records) if item[0] == start]
    v.require(len(matches) == 1, 'DarkScreen is not at an instruction boundary')
    selected = records[matches[0]:]
    gap = [(0xe5, b'\x04\0'), (0x63, b'\xca'), (0x19, None)] if event_id == 401 else []
    expected = [(0x76, dark.SELECTED), (0xe5, b'\x36\0'), (0x78, b'\x08\x3c'), (0xe5, b'\x38\0')]
    expected += gap + [(0x78, b'\0\x3c'), (0xe5, b'\x38\0'), (0x77, b''), (0xe5, b'\x36\0'), (0x1c, b'\x01')]
    if event_id == 401: expected += [(0x63, b'\x18')]
    expected += [(0xdb, b'')]
    v.require(len(selected) == len(expected), 'Missing or extra selected command')
    for (_, op, args), (wanted, operands) in zip(selected, expected, strict=True):
        v.require(op == wanted and (args == operands if operands is not None else len(args) == 16), 'Selected command/operand shape differs')
    ready = selected[4 + len(gap)][0]
    tail = handoff.tail(raw, lengths, ready)
    return dict(eventId=event_id, rawBlockSha256=v.sha(raw), textOffset=boundary,
        darkOffset=start, entryWait=selected[1][0], generalOffset=selected[2][0], generalWait=selected[3][0],
        readyOffset=ready, readyWait=tail['readyBarrierOffset'], releaseOffset=tail['releaseOffset'],
        releaseWait=tail['releaseBarrierOffset'], speedOffset=tail['restoreSpeedOffset'], endOffset=tail['endOffset'],
        cameraGap=[dict(offset=at, opcode=f'{op:02X}', operandBytes=len(args), operandSha256=v.sha(args))
                   for at, op, args in selected[4:4 + len(gap)]],
        cameraRestore=tail['hasCameraCurveRestore'], completePlaybackEstablished=False)


def queued_layers(ram, kind):
    v.require(kind in ('General', 'Ready'), 'Unknown selected display')
    current = d.word(ram, d.CURRENT_BUFFER)
    v.require(current in (d.BUFFER_BASE, d.BUFFER_BASE + d.BUFFER_STRIDE), 'Render buffer differs')
    ot = d.word(ram, current + 0xe5b4)
    v.require(ot in (0x80056b18, 0x80057118) and d.word(ram, d.SCRIPT_OT) == ot, 'Script/render OT differs')
    chain, nodes = d.walk(ram, ot + 0x17f * 4)
    tile_addresses = {(dark.TILES & 0x1fffffff) + i*72 + bank*24 for i in range(288) for bank in (0, 1)}
    v.require(not any(at in tile_addresses for at, _ in chain), 'Held flash cannot also submit transition tiles')
    flash_addresses = {(dark.FLASH & 0x1fffffff) + bank * 24: bank for bank in (0, 1)}
    glyph_addresses = {(d.BANNER_BASE & 0x1fffffff) + bank * d.BANNER_BANK_STRIDE + i * 52: (bank, i)
                       for bank in (0, 1) for i in range(6)}
    flashes = [(pos, at, words) for pos, (at, words) in enumerate(chain) if at in flash_addresses]
    glyphs = [(pos, at, words) for pos, (at, words) in enumerate(chain) if at in glyph_addresses]
    v.require(len(flashes) == 1 and len(glyphs) == 6, 'Expected one held flash and six display packets')
    pos, at, words = flashes[0]; bank = flash_addresses[at]
    v.require(pos > 0 and chain[pos-1] == ((dark.MODES & 0x1fffffff) + bank * 12, [0xe1000006, 0xe2000000]), 'Missing held material')
    v.require(words == [0x2a102830, 128, 384, (256 << 16) | 128, (256 << 16) | 384], 'Held flash differs')
    v.require([g[0] for g in glyphs] == list(range(pos+1, pos+7)), 'Interleaved or wrong relative layer order')
    v.require(len({glyph_addresses[g[1]][0] for g in glyphs}) == 1, 'Mixed display banks')
    order = [glyph_addresses[g[1]][1] for g in glyphs]
    v.require(order == (list(range(6)) if kind == 'General' else list(reversed(range(6)))), 'Wrong display packet order')
    for _, _, words in glyphs:
        v.require(len(words) == 12 and words[0] >> 24 == 0x3e, 'Wrong textured display packet')
        v.require(words[5] >> 16 in ((0x26, 0x46) if kind == 'General' else (6,)), 'Wrong display texture page/material')
        if kind == 'Ready': v.require(words[2] >> 16 == 0x3f9a, 'Wrong Ready palette')
    return dict(kind=kind, otag=ot, dmaNodes=nodes, flashPosition=pos, flashBank=bank,
        displayPositions=[g[0] for g in glyphs], displayBank=glyph_addresses[glyphs[0][1]][0],
        displayOrder=order, compositionOrder=['Background', 'DarkScreen', kind], queuedOnly=True,
        combinedDisplayedPixelsCompared=False)


def run(plan_path, reference, output):
    plan_path, output = map(event.c.profile_tool.scratch_path, (plan_path, output))
    v.require(not output.exists() and plan_path.stat().st_size <= 32768, 'Existing output or oversized plan')
    plan = v.parse(plan_path.read_bytes())
    v.fields(plan, 'format schemaVersion archive battle overlay executable bonus states', 'intro source plan')
    v.require(plan['format'] == 'fft.dark-screen-intro-source-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1, 'Wrong plan')
    v.require(type(plan['states']) is list and [x['kind'] for x in plan['states']] == ['General', 'Ready'], 'Expected the two selected held-layer states')
    def reference_check():
        v.require(subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip() == REVISION and
            not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(), 'Reference differs or is dirty')
    reference_check()
    sources = [dict(path=p, bytes=len(raw), sha256=v.sha(raw)) for p in SOURCES for raw in [(reference/p).read_bytes()]]
    inputs = {key: event.hide.verified(plan[key]) for key in ('archive', 'battle', 'overlay', 'executable', 'bonus')}
    expected = dict(archive=event.ARCHIVE_SHA, battle=event.BATTLE_SHA,
        overlay='97112c50c0b037b2cb5c9d93ae9e2a7506b582c5edbd86a1debfb7ab8e777630',
        executable='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562',
        bonus='c5d881b1fb62c04d1d7f0e5238faff4131676165c08e35b5cadb68b21c89165f')
    for key, raw in inputs.items(): v.require(v.sha(raw) == expected[key], 'Original identity differs: ' + key)
    functions = []
    groups = [('battle.yaml', 'battle', 0x80067000, ['script_conditions_display', 'gfx_draw_or_append_gpu_primitive', 'script_event_frame_run',
                  'script_execute_event', 'script_dark_screen_thread', 'thread_task_find', 'thread_current_exit',
                  'thread_frames_wait', 'gfx_append_gpu_primitive_to_secondary_otag']),
              ('event.yaml', 'overlay', 0x801bf000, ['conditions_show_general', 'conditions_special_thread', 'require_conditions_entrypoint',
                  'require_dark_screen_entrypoint', 'conditions_load_banner_image'])]
    for mapping, key, base, names in groups:
        text = (reference/'target'/mapping).read_text(encoding='utf-8')
        for name in names:
            matches = re.findall(r'addr: (0x[0-9a-f]+), size: (\d+), name: ' + name + r'[,}]', text)
            v.require(len(matches) == 1, 'Ambiguous original function: ' + name)
            at, size = int(matches[0][0], 16), int(matches[0][1]); raw = inputs[key][at-base:at-base+size]
            v.require(len(raw) == size, 'Function escapes original file')
            functions.append(dict(name=name, address=at, bytes=size, sha256=v.sha(raw)))
    at, size = 0x8014ca80, 308; raw = inputs['battle'][at-0x80067000:at-0x80067000+size]
    v.require(v.sha(raw) == '2f09dae8797f8acbde873608b889474322bad8efbce867c640d5ef74cdb1289a', 'Full native switch region differs')
    functions.append(dict(name='thread_yield_full_region', address=at, bytes=size, sha256=v.sha(raw)))
    table = inputs['executable'][dark.SQRT-0x8000f800:dark.SQRT-0x8000f800+384]
    tiles = dark.layout(struct.unpack('<192h', table))
    sincos = inputs['executable'][dark.SINCOS-0x8000f800:dark.SINCOS-0x8000f800+16384]
    observations = []; commands = None
    for item in plan['states']:
        v.fields(item, 'kind state', 'selected state')
        raw = event.hide.verified(item['state']); ram, lengths = event.read_ram(raw)
        for f in functions:
            at = f['address'] & 0x1fffffff
            v.require(v.sha(ram[at:at+f['bytes']]) == f['sha256'], 'Resident code differs: ' + f['name'])
        identity = event.inspect(ram, inputs['archive'], lengths)
        v.require(identity['currentEventId'] == 4, 'Only retained event 4 is observed')
        slot = event.THREADS + 3*1024; word = d.word
        v.require([word(ram, slot+x) for x in (0, 4, 0x48, 0x4c)] ==
            [8 if item['kind'] == 'General' else 0, 60, 1, 56], 'Display identity differs')
        v.require(word(ram, event.THREADS+1024+0x40) == (807 if item['kind'] == 'General' else 813), 'Parent barrier differs')
        if item['kind'] == 'Ready':
            v.require([word(ram, event.THREADS+4096+x) for x in (0x48, 0x4c)] == [1, 0], 'Ready child differs')
        hold = dark.inspect_hold(ram, tiles, sincos)
        selected = [selected_sequence(inputs['archive'][i*8192:(i+1)*8192], lengths, start, i) for i, start in ((4,794),(401,154))]
        v.require(commands is None or selected == commands, 'Event records differ across observations'); commands = selected
        observations.append(dict(state=item['state'], event=identity, heldWorker=hold, layers=queued_layers(ram, item['kind'])))
    reference_check()
    for f in sources:
        raw = (reference/f['path']).read_bytes(); v.require(len(raw) == f['bytes'] and v.sha(raw) == f['sha256'], 'Source changed during audit')
    for key in inputs: v.require(event.hide.verified(plan[key]) == inputs[key], 'Original input changed')
    report = dict(format='fft.dark-screen-intro-source-audit', schemaVersion=1, referenceRevision=REVISION,
        managedBase=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(), plan=event.c.identity(plan_path),
        sourceFiles=sources, residentFunctions=functions, commands=commands, observations=observations,
        tools=[event.c.identity(Path(m.__file__)) for m in (sys.modules[__name__],dark,handoff,event,d)],
        specificationOnly=True, futureImplementationCasesExecuted=False, runtimeChanged=False,
        newOriginalCaptures=False, emulatorLaunched=False, originalDurationEstablished=False, combinedDisplayedPixelsCompared=False)
    output.mkdir(parents=True)
    (output/'report.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('plan', 'reference-root', 'output'): parser.add_argument('--'+flag, type=Path, required=True)
    args = parser.parse_args()
    try:
        report = run(args.plan, args.reference_root, args.output)
        print(f"Audited {len(report['observations'])} retained held-layer states and both event tails; specification only.")
    except (ValueError, KeyError, OSError, TypeError, IndexError, struct.error, subprocess.SubprocessError) as error:
        parser.exit(2, 'Dark-screen intro audit failed: '+str(error)+'\n')
