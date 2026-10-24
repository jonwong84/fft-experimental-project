"""Bounded original no-input banner continuation; original media remain below scratch/."""
import argparse
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys

import analyze_banner_precision as precision
import compare_observed_banner as observed
import compare_visual_checkpoints as visual
import prepare_retroarch_reference as profile_tool
import trace_banner_draw_state as dma
import verify_retroarch_boot as boot

SCHEDULE = (4, 6, 8, 24, 124, 126, 128, 130, 132, 134, 136, 138, 140, 142, 144, 146, 148)
THREADS_POINTER, THREADS_BASE = 0x80165f98, 0x8016986c
CODE = ((0x801c8f80, 4132, '231240f68206f06c842633bd08d76cb806eaac8b3e032ba673417f590ed12607'),
        (0x8014c958, 60, '23aa4a3549fe6cb707b4f4e7a19e7f36356ce2411a96dfbb3d64c6fc5d1c2392'))
PHASES = {0x801c9384: ('Revealing', 19), 0x801c9ad8: ('Holding', 59),
          0x801c9bc4: ('Hiding', 9), 0x801c9f68: ('ExitYield', 10)}


def read_ram(state):
    visual.require(0 < len(state) <= precision.MAX_STATE, 'State exceeds budget')
    core = precision.unpack_state(state)
    ram = precision.subsystem_records(core, b'MAIN').get('MainRAM.data8', b'')
    visual.require(len(ram) == dma.RAM_SIZE, 'Missing main RAM')
    for address, length, digest in CODE:
        offset = address & 0x1fffffff
        visual.require(visual.sha(ram[offset:offset + length]) == digest, 'Original banner/scheduler code changed')
    return ram


def inspect_ram(ram, definition, slot=None):
    """Decode only this function's suspended contexts, never a general task classifier."""
    visual.require(len(ram) == dma.RAM_SIZE and dma.word(ram, THREADS_POINTER) == THREADS_BASE,
                   'Unexpected battle thread table')
    if slot is None:
        slots = [i for i in range(16) if dma.word(ram, THREADS_BASE + i * 1024 + 0x4c) == 56]
        visual.require(len(slots) == 1, 'Expected one original ConditionDisplay task')
        slot = slots[0]
    visual.integer(slot, 0, 15, 'thread slot')
    address = THREADS_BASE + slot * 1024
    read = lambda offset: dma.word(ram, address + offset)
    visual.require((read(0), read(4)) == (8, 60), 'Selected task parameters changed/reused')
    pc, running, task = read(0x44), read(0x48), read(0x4c)
    if (pc, running, task) == (0x8014c984, 0, 0):
        phase, counter, reveal, hold, hide, tick = 'Completed', None, 20, 60, 10, 91
    else:
        visual.require(running == 1 and task == 56 and pc in PHASES, 'Unknown, suspended or reused task context')
        sp = read(0x3c)
        visual.require(address + 0x6c <= sp and sp + 0x58 <= address + 0x400 and sp % 4 == 0,
                       'Banner stack escapes its thread slot')
        visual.require(dma.word(ram, sp + 0x10) == 60 and dma.word(ram, sp + 0x20) == 6,
                       'Unexpected spilled hold/quad count')
        phase, maximum = PHASES[pc]
        counter = dma.word(ram, sp + 0x18)
        visual.integer(counter, 0, maximum, 'phase loop counter')
        visual.require(phase != 'ExitYield' or counter == 10, 'Exit yield before ten hide iterations')
        reveal = counter if phase == 'Revealing' else 20
        hold = counter if phase == 'Holding' else (0 if phase == 'Revealing' else 60)
        hide = counter if phase in ('Hiding', 'ExitYield') else 0
        tick = reveal + hold + hide
    current = dma.word(ram, dma.CURRENT_BUFFER)
    visual.require(current in (dma.BUFFER_BASE, dma.BUFFER_BASE + dma.BUFFER_STRIDE), 'Unexpected render buffer')
    ot = dma.word(ram, current + 0xe5b4)
    visual.require(ot in (0x80056b18, 0x80057118), 'Unexpected battle ordering table')
    packets, _ = dma.walk(ram, ot + 0x17f * 4)
    selected = []
    for packet, data in packets:
        for bank in (0, 1):
            delta = packet - (dma.BANNER_BASE & 0xffffff) - bank * dma.BANNER_BANK_STRIDE
            if delta in range(0, 6 * 52, 52): selected.append((delta // 52, bank, data))
    draws = []
    if phase == 'Completed':
        visual.require(not selected, 'Exited banner still appears in current ordering table')
    else:
        visual.require([i for i, _, _ in selected] == list(range(6)), 'Expected six ordered banner packets')
        bank = read(0x2c)
        visual.require(bank in (0, 1) and all(b == bank for _, b, _ in selected), 'Banner parity/packet bank mismatch')
        for index, _, data in selected:
            visual.require(len(data) == 12 and data[0] >> 24 == 0x3e, 'Expected shaded banner quad')
            piece = definition['pieces'][index]
            xy = [struct.unpack('<hh', struct.pack('<I', data[v * 3 + 1])) for v in range(4)]
            crop = xy[0][1] - piece['y']
            visual.integer(crop, 0, piece['height'], 'observed crop')
            rows = piece['height'] - crop
            expected_xy = [(piece['x'] + 128 + dx, piece['y'] + crop + dy)
                           for dx, dy in ((0, 0), (piece['width'], 0), (0, rows), (piece['width'], rows))]
            visual.require(xy == expected_xy, 'Unexpected banner geometry')
            for vertex, (dx, dy) in enumerate(((0, 0), (piece['width'], 0), (0, rows), (piece['width'], rows))):
                uv = data[vertex * 3 + 2] & 0xffff
                visual.require(uv == (piece['u'] + dx) | ((piece['v'] + crop + dy) & 255) << 8,
                               'Unexpected cropped texture coordinates')
            draws.append(dict(PieceIndex=index, CropTop=crop, VisibleRows=rows))
    return dict(slot=slot, savedReturnAddress=pc, phase=phase, phaseCounter=counter,
                logicalTick=tick, revealCompleted=reveal, holdCompleted=hold, hideCompleted=hide,
                taskRunning=bool(running), taskId=task, draws=draws)


def command(profile, profile_root, output, frames):
    visual.integer(frames, 4, 148, 'frontend frame budget')
    return [profile['executable'], '--config', str(profile_root / 'retroarch.cfg'), '--appendconfig', str(output / 'restore.cfg'),
            '-L', profile['coreLibrary'], '--no-patch', '--sram-mode', 'noload-nosave', '--nodevice', '1', '--nodevice', '2',
            '--entryslot', '0', '--verbose', '--log-file', str(output / 'retroarch.log'),
            '--max-frames', str(frames), '--max-frames-ss', '--max-frames-ss-path', str(output / 'frame.png'), profile['cue']]


def identity(path):
    return dict(localPath=path.relative_to(profile_tool.ROOT).as_posix(), bytes=path.stat().st_size,
                sha256=profile_tool.file_hash(path))


def review(checkpoints):
    cards = ''.join(f'<figure><img src="frames-{x["frontendFrames"]:03}/frame.png" width="280" height="240">'
                   f'<figcaption>{x["frontendFrames"]} frontend frames · {x["phase"]} · logical checkpoint '
                   f'{x["logicalTick"]}</figcaption></figure>' for x in checkpoints)
    return ('<!doctype html><meta charset="utf-8"><title>Original banner continuation</title>'
            '<style>body{background:#20242a;color:#eee;font:16px sans-serif;margin:24px}'
            'main{display:flex;flex-wrap:wrap;gap:16px}figure{margin:0;padding:12px;background:#30363e}'
            'img{image-rendering:pixelated}figcaption{max-width:280px;margin-top:8px}</style>'
            '<h1>Original banner continuation</h1><p>Phases/counters come from the exit save state, '
            'not image recognition or a frontend-frame conversion. PNGs are visual context; their '
            'display latency relative to the saved task is not calibrated. No pixel fidelity verdict.</p>'
            '<p>Separate no-input restores from a late-reveal anchor. Full reveal, Circle input, '
            'loading duration and event identity remain unverified.</p><main>' + cards + '</main>\n')


def run(plan_path, profile_root, output):
    visual.require(sys.platform == 'win32', 'Installed reference capture supports Windows only')
    profile_root, output = profile_tool.scratch_path(profile_root), profile_tool.scratch_path(output)
    visual.require(not output.exists(), 'Output already exists; choose a fresh scratch directory')
    tools = [identity(Path(module.__file__)) for module in
             (sys.modules[__name__], precision, observed, visual, profile_tool, dma, boot)]
    plan_path = profile_tool.scratch_path(plan_path)
    visual.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = visual.parse(plan_path.read_bytes())
    inputs, _, _, definition, _, _ = observed.load(plan)
    source = inputs['original']['state']
    baseline = inspect_ram(read_ram(source), definition)
    visual.require(baseline['phase'] == 'Revealing' and baseline['phaseCounter'] == 18, 'Unexpected entry anchor')
    profile_path = profile_root / 'profile.json'
    visual.require(profile_path.stat().st_size <= 65536, 'Profile exceeds budget')
    profile_bytes = profile_path.read_bytes()
    visual.require(profile_bytes == inputs['original']['profile'], 'Capture profile differs from original observation')
    profile = visual.parse(profile_bytes)
    pinned_metadata = [identity(plan_path), identity(profile_path)]
    visual.require(profile['format'] == 'fft.retroarch-reference-profile' and profile['schemaVersion'] == 1, 'Unsupported profile')
    for item in profile['inputs']:
        visual.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Pinned input changed')
    for name, digest in profile['generatedSha256'].items():
        visual.require(name in ('retroarch.cfg', 'core-options.cfg', 'launch.ps1') and
                       profile_tool.file_hash(profile_root / name) == digest, 'Prepared capture policy changed')
    visual.require(profile_tool.file_hash(profile_root / 'system/scph5501.bin') == profile_tool.BIOS_HASH, 'Actual BIOS changed')
    normal = {x['path']: profile_tool.file_hash(Path(x['path'])) for x in profile['normalConfigIdentities']}
    output.mkdir(parents=True)
    checkpoints = []
    try:
        for frames in SCHEDULE:
            destination = output / f'frames-{frames:03}'; destination.mkdir()
            (destination / 'states').mkdir(); (destination / 'saves').mkdir()
            state_name = Path(profile['cue']).stem + '.state'
            copied_state = destination / 'states' / state_name
            copied_state.write_bytes(source)
            shutil.copyfile(profile_root / 'core-options.cfg', destination / 'core-options.cfg')
            settings = dict(input_driver='null', input_joypad_driver='null', global_core_options='true',
                core_options_path=(destination / 'core-options.cfg').as_posix(), savestate_directory=(destination / 'states').as_posix(),
                sort_savestates_enable='false', savestates_in_content_dir='false', savefile_directory=(destination / 'saves').as_posix(),
                savestate_auto_save='true')
            (destination / 'restore.cfg').write_text(profile_tool.config(settings), encoding='utf-8', newline='\n')
            args = command(profile, profile_root, destination, frames)
            (destination / 'command.json').write_text(json.dumps(args, indent=2) + '\n', encoding='utf-8', newline='\n')
            startup = subprocess.STARTUPINFO(); startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow = 0
            result = subprocess.run(args, cwd=Path(profile['executable']).parent, capture_output=True, timeout=55, startupinfo=startup)
            (destination / 'stdout.txt').write_bytes(result.stdout); (destination / 'stderr.txt').write_bytes(result.stderr)
            visual.require(result.returncode == 0, 'Continuation process failed; inspect retained logs')
            log = (destination / 'retroarch.log').read_text(encoding='utf-8', errors='replace')
            visual.require('[State] Loading state' in log and 'Controller 1: Unplugged' in log and 'Controller 2: Unplugged' in log,
                           'State load/input isolation unconfirmed')
            desired = boot.options((profile_root / 'core-options.cfg').read_text())
            effective = boot.options((destination / 'core-options.cfg').read_text())
            visual.require(all(effective.get(k) == value for k, value in desired.items()), 'Selected core options changed')
            saved = destination / 'states' / (state_name + '.auto')
            visual.require(saved.stat().st_size <= precision.MAX_STATE, 'Saved state exceeds budget')
            observation = inspect_ram(read_ram(saved.read_bytes()), definition, baseline['slot'])
            # This observed cadence is checked, never used to decode the phase/counters.
            visual.require(observation['logicalTick'] == 17 + frames // 2, 'Observed frontend/task cadence changed')
            from PIL import Image
            with Image.open(destination / 'frame.png') as image:
                visual.require(image.size == (280, 240) and image.mode == 'RGB', 'Unexpected raw display mode')
            visual.require(copied_state.read_bytes() == source, 'Input state copy changed')
            checkpoints.append(dict(frontendFrames=frames, **observation, png=identity(destination / 'frame.png'),
                state=identity(saved), command=identity(destination / 'command.json'), config=identity(destination / 'restore.cfg'),
                log=identity(destination / 'retroarch.log'), effectiveOptions=identity(destination / 'core-options.cfg'),
                diagnostics=[line for line in log.splitlines() if '[ERROR]' in line or '[WARN]' in line]))
            print(f'Captured {frames} frontend frames: {observation["phase"]}, logical checkpoint {observation["logicalTick"]}', flush=True)
    finally:
        for path, digest in normal.items():
            visual.require(profile_tool.file_hash(Path(path)) == digest, 'Installed configuration changed')
        for name, digest in profile['generatedSha256'].items():
            visual.require(profile_tool.file_hash(profile_root / name) == digest, 'Prepared profile changed')
        for item in tools + pinned_metadata:
            visual.require(profile_tool.file_hash(profile_tool.ROOT / item['localPath']) == item['sha256'], 'Tool/metadata changed during capture')
    report = dict(format='fft.original-banner-continuation', schemaVersion=1, referenceRevision=observed.REVISION,
        sourcePlan=pinned_metadata[0], captureProfile=pinned_metadata[1], tools=tools,
        entryStateSha256=visual.sha(source), baseline=baseline, checkpoints=checkpoints,
        autoSaveOverride='true only in each private continuation configuration; base profile unchanged',
        controllersDisconnected=True, selectedCoreOptionsRetained=True, normalConfigsUnchanged=True,
        replayPlaybackVerified=False, fullRevealObserved=False, originalEvent401Verified=False,
        pixelComparisonPerformed=False, originalFidelityEstablished=False)
    # The tool itself lives outside scratch; identity only stores its path/hash, never media.
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    (output / 'review.html').write_text(review(checkpoints), encoding='utf-8', newline='\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True)
    parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        run(args.plan, args.profile, args.output)
    except (ValueError, OSError, KeyError, TypeError, subprocess.SubprocessError) as error:
        parser.exit(2, 'Continuation capture failed (diagnostic files retained): ' + str(error) + '\n')
