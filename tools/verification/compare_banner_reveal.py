"""Bounded source-selected early reveal and actual C# pixels; original media stay in scratch."""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import sys

import analyze_banner_precision as p
import capture_banner_continuation as c
import compare_banner_background as bg
import compare_banner_hide as hide
import compare_observed_banner as b
import compare_visual_checkpoints as v
import prepare_retroarch_reference as profile_tool
import trace_banner_draw_state as trace

TICKS = (1, 2, 3, 4, 5, 9, 10, 11, 12, 13, 14)
SCHEDULE = tuple((2 * tick + 4, tick) for tick in TICKS)
LOADER_CODE = (0x801c3ab0, 244, '83bdf7ea18ea62c44d0b5bcf4f0d12ba8a9d4ad7b3827200fddb225bf364e6ae')
WAITS = {0x801c3ad0: 'AllocationYield', 0x801c3b10: 'ReadRequestYield',
         0x801c3b40: 'ReadCompletionYield', 0x801c3b80: 'PostUploadYield'}


def snapshot(state, definition):
    ram = c.read_ram(state)
    for address, size, digest in (*hide.CODE, LOADER_CODE):
        offset = address & 0x1fffffff
        v.require(v.sha(ram[offset:offset + size]) == digest, 'Reveal/frame/loader code changed')
    task = c.inspect_ram(ram, definition, early_reveal=True)
    v.require(task['phase'] == 'Revealing', 'Expected reveal task')
    core = p.unpack_state(state); cpu = p.subsystem_records(core, b'CPU')
    v.require(len(cpu.get('GPR', b'')) == 128, 'Missing CPU registers')
    saved_return = struct.unpack('<32I', cpu['GPR'])[31]
    v.require(saved_return in (0x8001dc40, 0x8001dc64), 'Unknown VSync call site')
    return hide.context(core, ram, task, first_vsync_wait=saved_return == 0x8001dc40)


def loader_snapshot(state):
    """Only the four source yield sites, with the loader-to-general return chain."""
    ram = c.read_ram(state)
    address, size, digest = LOADER_CODE; offset = address & 0x1fffffff
    v.require(v.sha(ram[offset:offset + size]) == digest, 'Loader code changed')
    v.require(trace.word(ram, c.THREADS_POINTER) == c.THREADS_BASE, 'Unexpected thread table')
    slots = [i for i in range(16) if trace.word(ram, c.THREADS_BASE + i * 1024 + 0x4c) == 56]
    v.require(len(slots) == 1, 'Expected one condition task')
    base = c.THREADS_BASE + slots[0] * 1024
    read = lambda at: trace.word(ram, at)
    pc, sp = read(base + 0x44), read(base + 0x3c)
    v.require([read(base + x) for x in (0, 4, 0x48)] == [8, 60, 1] and pc in WAITS, 'Unknown loader task/yield')
    v.require(base + 0x6c <= sp and sp + 0x80 <= base + 0x400 and sp % 4 == 0, 'Invalid loader stack')
    v.require(read(sp + 0x1c) == 0x801c8fbc and read(sp + 0x14) == sp + 32 and read(sp + 0x18) == 60,
              'Loader/general caller chain differs')
    allocation = None
    if pc != 0x801c3ad0:
        allocation = read(base + 0x10)
        v.require(allocation == read(0x80010010) and 0x80000000 <= allocation <= 0x801f8000,
                  'Unexpected high-overlay reservation')
    return dict(slot=slots[0], savedReturnAddress=pc, stackPointer=sp, phase=WAITS[pc],
                logicalTick=None, allocation=allocation, generalCallerReturn=0x801c8fbc)


def padded_geometry(geometry):
    v.require(type(geometry) is list and len(geometry) in (3, 6), 'Expected three or six reveal submissions')
    v.require([g['PieceIndex'] for g in geometry] == list(range(len(geometry))), 'Reveal order differs')
    return geometry + [dict(PieceIndex=i, CropTop=31, VisibleRows=0) for i in range(len(geometry), 6)]


def native_frames(root, backgrounds, geometries):
    path = root / 'check.json'; v.require(path.stat().st_size <= 65536, 'Receipt exceeds budget')
    receipt = v.parse(path.read_bytes())
    v.fields(receipt, 'format schemaVersion renderer sourceKind referenceRevision precision ditherEnabled framebufferOrigin gpuReadbackPerformed originalComparison checkpoints', 'reveal receipt')
    v.require(receipt['format'] == 'fft.banner-reveal-cpu-check' and type(receipt['schemaVersion']) is int and receipt['schemaVersion'] == 1 and
        receipt['renderer'] == 'FiveBitGeneralBannerPresentation.ComposeOver' and receipt['sourceKind'] == 'verified-retail' and
        receipt['referenceRevision'] == b.REVISION and receipt['precision'] == 'five-bit' and receipt['ditherEnabled'] is False and
        receipt['gpuReadbackPerformed'] is False and receipt['originalComparison'] == 'pending' and receipt['framebufferOrigin'] == [0, 0] and
        all(type(x) is int for x in receipt['framebufferOrigin']), 'Unexpected native reveal profile')
    v.require(type(receipt['checkpoints']) is list and len(receipt['checkpoints']) == len(TICKS), 'Wrong reveal checkpoint count')
    pixels = {}
    for tick, item in zip(TICKS, receipt['checkpoints'], strict=True):
        v.fields(item, 'logicalTick phase revealCompleted holdCompleted hideCompleted file backgroundSha256 rgbaSha256 pieces', 'reveal checkpoint')
        for key, expected in (('logicalTick', tick), ('revealCompleted', tick), ('holdCompleted', 0), ('hideCompleted', 0)):
            v.require(type(item[key]) is int and item[key] == expected, 'Wrong native reveal counter')
        v.require(item['phase'] == 'Revealing' and item['pieces'] == geometries[tick] and
            all(type(g[key]) is int for g in item['pieces'] for key in ('PieceIndex', 'CropTop', 'VisibleRows')) and
            item['file'] == f'tick-{tick:03}.rgba' and item['backgroundSha256'] == v.sha(backgrounds[tick]), 'Native reveal projection/background differs')
        file = root / item['file']; v.require(file.stat().st_size == bg.CANVAS_BYTES, 'Wrong native canvas size')
        data = file.read_bytes(); v.require(v.sha(data) == item['rgbaSha256'], 'Native pixel hash differs')
        v.require(all(data[at + 3] == 255 and not ((data[at] | data[at + 1] | data[at + 2]) & 7)
                      for at in range(0, len(data), 4)), 'Expected opaque RGB5')
        pixels[tick] = data
    return receipt, pixels


def review():
    rows = ''.join(f'<section><h2>Displayed reveal {tick}</h2><div>' + ''.join(
        f'<figure><img src="frame-{frames:03}/{name}.png" width="512" height="480"><figcaption>{label}</figcaption></figure>'
        for name, label in (('original', 'Unmodified original'), ('background', 'Controlled background'), ('native', 'Actual C#'))) + '</div></section>'
        for frames, tick in SCHEDULE)
    return ('<!doctype html><meta charset="utf-8"><title>Early banner reveal</title><style>'
        'body{background:#20242a;color:#eee;font:16px sans-serif;margin:24px}div{display:flex;flex-wrap:wrap;gap:16px}'
        'figure{margin:0}img{image-rendering:pixelated}section{margin-bottom:24px}</style>'
        '<h1>Source-bound early banner reveal</h1><p>Native 256x240 crops at 2x. Selected pre-submission '
        'VSync states bind the displayed surface to an earlier queued submission; no image fitting. '
        'Inherited backgrounds do not validate managed scene rendering. Reveal 1 is degenerate and '
        'checks background preservation only. Owner review pending.</p>' + rows + '\n')


def run(plan_path, profile_root, output):
    from PIL import Image
    v.require(sys.platform == 'win32', 'Windows reference capture required')
    output, profile_root, plan_path = map(profile_tool.scratch_path, (output, profile_root, plan_path))
    v.require(not output.exists(), 'Output already exists; choose fresh scratch root')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = v.parse(plan_path.read_bytes()); inputs, _, _, definition, _, _ = b.load(plan)
    evidence_path = profile_tool.ROOT / 'docs/evidence/visual/original-pre-banner-anchor-v1.json'
    evidence = v.parse(evidence_path.read_bytes())
    v.require(evidence['format'] == 'fft.original-pre-banner-anchor' and evidence['schemaVersion'] == 1 and
              evidence['referenceRevision'] == b.REVISION, 'Unexpected earlier-anchor provenance')
    records = [x for x in evidence['checkpoints'] if x['frontendFrames'] == 340]
    v.require(len(records) == 1, 'Expected unique first-submission anchor')
    record = next(x for x in records[0]['artifacts'] if x['localPath'].endswith('.state.auto'))
    source = hide.verified(record); entry = snapshot(source, definition)
    v.require(entry['task']['logicalTick'] == 1, 'Expected first degenerate reveal submission')
    trace.trace(source, definition, entry['task']['draws'])
    probe, intervention = bg.transparent_palette_state(source, inputs['prepared']['palette'])
    profile_path = profile_root / 'profile.json'
    v.require(profile_path.read_bytes() == inputs['original']['profile'], 'Profile changed')
    profile = v.parse(inputs['original']['profile'])
    tools = [c.identity(Path(module.__file__)) for module in (sys.modules[__name__], p, c, bg, hide, b, v, profile_tool, trace)]
    tools += [c.identity(profile_tool.ROOT / name) for name in ('tools/banner-blend-check/Program.cs',
        'src/Fft.Client/Presentation/GeneralBannerPresentation.cs', 'src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs')]
    pinned = [c.identity(path) for path in (plan_path, profile_path, evidence_path)] + [record]
    for item in profile['inputs']: v.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Pinned input changed')
    for name, digest in profile['generatedSha256'].items():
        v.require(name in ('retroarch.cfg', 'core-options.cfg', 'launch.ps1') and profile_tool.file_hash(profile_root / name) == digest, 'Base policy changed')
    v.require(profile_tool.file_hash(profile_root / 'system/scph5501.bin') == profile_tool.BIOS_HASH, 'Actual BIOS changed')
    normal = {x['path']: profile_tool.file_hash(Path(x['path'])) for x in profile['normalConfigIdentities']}
    output.mkdir(parents=True); originals = {}; contexts = {}; entries = []; backgrounds = {}; geometries = {}
    budgets = sorted({frames for frames, _ in SCHEDULE} | {frames - 4 for frames, _ in SCHEDULE if frames > 6})
    try:
        for frames in budgets:
            saved, pixels, receipt = bg.capture(profile, profile_root, output / f'original-{frames:03}', source, frames)
            originals[frames] = saved, pixels, receipt; contexts[frames] = snapshot(saved, definition)
            print(f'Original restore {frames}: queued reveal {contexts[frames]["task"]["logicalTick"]}', flush=True)
        for frames, tick in SCHEDULE:
            state, original, original_receipt = originals[frames]; current = contexts[frames]
            queued_state = source if frames == 6 else originals[frames - 4][0]
            queued = entry if frames == 6 else contexts[frames - 4]
            binding = hide.bind_display(current, queued)
            v.require(binding['displayedLogicalTick'] == tick, 'Unexpected displayed reveal anchor')
            geometry = binding['displayedGeometry']; drawn = trace.trace(queued_state, definition, geometry)
            queued_ram = c.read_ram(queued_state)
            v.require(all(trace.word(queued_ram, x['packetAddress'] + 12) >> 16 == 0x3fd8 for x in drawn['banners']), 'Displayed CLUT differs')
            destination = output / f'frame-{frames:03}'; destination.mkdir()
            saved, background, probe_receipt = bg.capture(profile, profile_root, destination / 'probe', probe, frames)
            v.require(snapshot(saved, definition) == current, 'Probe frame/task context differs')
            gpu = p.gpu_records(p.unpack_state(saved))
            v.require(gpu['&GPURAM[0][0]'][bg.PALETTE_OFFSET:bg.PALETTE_OFFSET + 32] == bytes(32), 'Probe palette overwritten')
            isolation = bg.isolation(state, saved)
            backgrounds[tick], geometries[tick] = background, geometry
            (destination / 'background.rgba').write_bytes(background)
            for name, pixels in (('original.png', original), ('background.png', background)):
                Image.frombytes('RGBA', (256, 240), pixels).save(destination / name)
            entries.append(dict(frontendFrames=frames, displayedLogicalTick=tick, originalCapture=original_receipt,
                queuedSource=record if frames == 6 else originals[frames - 4][2]['exitState'],
                sourceContext=current, binding=binding, drawTrace=drawn, probeCapture=probe_receipt, stateIsolation=isolation))
            print(f'Isolated displayed reveal {tick}: {len(geometry)} pieces', flush=True)
        native_plan = dict(format='fft.banner-reveal-backgrounds', schemaVersion=1, checkpoints=[dict(logicalTick=tick,
            backgroundPath=str(output / f'frame-{frames:03}/background.rgba'), backgroundSha256=v.sha(backgrounds[tick])) for frames, tick in SCHEDULE])
        native_plan_path = output / 'backgrounds.json'; native_plan_path.write_text(json.dumps(native_plan, indent=2) + '\n')
        prepared = v.scratch_file(plan['prepared']['definition']['path']).parent
        args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
            '--', '--reveal-sequence', str(prepared), str(native_plan_path), str(output / 'native')]
        (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n')
        result = subprocess.run(args, cwd=profile_tool.ROOT, capture_output=True, timeout=55)
        (output / 'native-stdout.txt').write_bytes(result.stdout); (output / 'native-stderr.txt').write_bytes(result.stderr)
        v.require(result.returncode == 0, 'Native reveal host failed; build Release first')
        receipt, native = native_frames(output / 'native', backgrounds, geometries)
        for entry in entries:
            frames, tick = entry['frontendFrames'], entry['displayedLogicalTick']; state, original, _ = originals[frames]
            probe_state = hide.verified(entry['probeCapture']['exitState'])
            entry['comparison'] = hide.compare(original, backgrounds[tick], native[tick], hide.scanout(state), hide.scanout(probe_state),
                definition, padded_geometry(geometries[tick]), inputs['prepared']['indices'], inputs['prepared']['palette'], entry['stateIsolation'], partial_triplet=True)
            Image.frombytes('RGBA', (256, 240), native[tick]).save(output / f'frame-{frames:03}/native.png')
            print(f'Reveal {tick}: exact={entry["comparison"]["passed"]}, samples={entry["comparison"]["checks"]["touched"]["pixels"]}', flush=True)
    finally:
        for path, digest in normal.items(): v.require(profile_tool.file_hash(Path(path)) == digest, 'Installed configuration changed')
        for name, digest in profile['generatedSha256'].items(): v.require(profile_tool.file_hash(profile_root / name) == digest, 'Base policy changed')
        for item in tools + pinned: v.require(profile_tool.file_hash(profile_tool.ROOT / item['localPath']) == item['sha256'], 'Tool/evidence changed during capture')
    report = dict(format='fft.banner-reveal-comparison', schemaVersion=1, referenceRevision=b.REVISION, tools=tools, pinned=pinned,
        entryState=record, entryContext=snapshot(source, definition), intervention=intervention,
        originalCaptures=[originals[n][2] for n in budgets], checkpoints=entries, nativeReceipt=c.identity(output / 'native/check.json'),
        protectedInputsUnchanged=True, originalFidelityEstablished=False, passed=all(x['comparison']['passed'] for x in entries))
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n'); (output / 'review.html').write_text(review(), encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--profile', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True); args = parser.parse_args()
    try:
        result = run(args.plan, args.profile, args.output)
        sys.exit(0 if result['passed'] else 1)
    except (ValueError, OSError, KeyError, TypeError, subprocess.SubprocessError) as error:
        parser.exit(2, 'Reveal check failed (diagnostics retained): ' + str(error) + '\n')
