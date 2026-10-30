"""Source-bound displayed hide frames versus actual C# composition over isolated backgrounds."""
import argparse
import copy
import json
from pathlib import Path
import struct
import subprocess
import sys
import zlib

import analyze_banner_precision as p
import capture_banner_continuation as c
import compare_banner_background as bg
import compare_observed_banner as b
import compare_visual_checkpoints as v
import prepare_retroarch_reference as profile_tool
import trace_banner_draw_state as trace

SCHEDULE = ((132, 81), (134, 82), (136, 83), (138, 84), (140, 85))
CODE = ((0x80093a98, 344, '1b36797b7b673151d2baac9dc6f5219504ece941251149af47fcd25d33191055'),
        (0x80079a98, 1852, '8ada54ca495c6cab8d6372374082be64cffb75a11682a2e7f0a5a6bcca21939f'),
        (0x8001dba8, 328, '52593b17cb59da9fca39f2dc7b537615812a1c5bd54f4551bef0a6d1ae270aa2'),
        (0x8001dcf0, 156, '65614fd4dad4c750ef5a792b8f0533e19ade2d4ff23ffb7e2ed68445e7162347'))
POLARITY, FRAME_COUNTER, DRAW_ENVS, DISPLAY_ENVS = 0x8004597c, 0x80045994, 0x8004ea14, 0x8004eacc


def snapshot(state, definition):
    ram = c.read_ram(state); core = p.unpack_state(state)
    for address, size, digest in CODE:
        offset = address & 0x1fffffff
        v.require(v.sha(ram[offset:offset + size]) == digest, 'Frame/VSync source code changed')
    observation = c.inspect_ram(ram, definition)
    return context(core, ram, observation)


def context(core, ram, observation, *, first_vsync_wait=False):
    """Closed pre-submission VSync context; not a general GPU/frame classifier."""
    cpu = p.subsystem_records(core, b'CPU')
    v.require(len(cpu.get('GPR', b'')) == 128 and len(cpu.get('BACKED_PC', b'')) == 4, 'Missing CPU registers')
    registers = struct.unpack('<32I', cpu['GPR']); pc = int.from_bytes(cpu['BACKED_PC'], 'little'); sp = registers[29]
    vsync_return = 0x8001dc40 if first_vsync_wait else 0x8001dc64
    v.require(0x8001dcf0 <= pc < 0x8001dd8c and registers[31] == vsync_return, 'CPU is not in the selected VSync wait')
    return _frame_context(core, ram, observation, pc, sp, vsync_return)


def _frame_context(core, ram, observation, pc, sp, vsync_return):
    """Shared stack/environment checks, after a caller proves live or saved CPU context."""
    gpu = p.gpu_records(core)
    v.require(0x801f0000 <= sp and sp + 0x5c <= 0x80200000 and sp % 4 == 0, 'Unexpected main-stack frame')
    v.require([trace.word(ram, sp + x) for x in (0x18, 0x38, 0x58)] == [vsync_return, 0x80093b68, 0x8007a058],
              'VSync/frame-submit/entry return chain differs')
    polarity = trace.word(ram, POLARITY); v.integer(polarity, 0, 1, 'screen polarity')
    v.require(trace.word(ram, 0x80045980) == 2, 'Unexpected VSync interval')
    draw = struct.unpack_from('<6h', ram, (DRAW_ENVS & 0x1fffffff) + polarity * 92)
    display = struct.unpack_from('<4h', ram, (DISPLAY_ENVS & 0x1fffffff) + polarity * 20)
    front_y = polarity * 240
    v.require(draw == (-128, front_y, 384, 240, -128, front_y) and display == (0, 240 - front_y, 256, 240), 'Unexpected pending draw/display environments')
    scalar = lambda name: int.from_bytes(gpu[name], 'little', signed=True)
    v.require(scalar('DisplayMode') == 0 and scalar('DisplayFB_XStart') == 0 and scalar('DisplayFB_YStart') == front_y and
              scalar('OffsX') == -128 and scalar('OffsY') == 240 - front_y and gpu['dtd'] == b'\0', 'GPU front/back state differs from pending flip')
    v.require(gpu.get('DisplayOff') == b'\0' and gpu.get('InCmd') == b'\0' and gpu.get('BlitterFIFO.in_count') == bytes(4), 'Unsupported display/pending GPU command')
    return dict(frameCounter=trace.word(ram, FRAME_COUNTER), polarity=polarity, cpuPc=pc, cpuSp=sp,
        returnAddresses=[vsync_return, 0x80093b68, 0x8007a058], frontOrigin=[0, front_y], backOrigin=[0, 240 - front_y],
        pendingDrawOrigin=[0, front_y], pendingDisplayOrigin=[0, 240 - front_y], task=observation)


def bind_display(current, queued):
    v.require(current['frameCounter'] - queued['frameCounter'] == 2 and
              current['task']['logicalTick'] - queued['task']['logicalTick'] == 2 and
              current['polarity'] == queued['polarity'] and current['frontOrigin'] == queued['pendingDrawOrigin'],
              'Displayed frame does not bind to the source-ordered two-earlier submission')
    return dict(displayedLogicalTick=queued['task']['logicalTick'], currentQueuedLogicalTick=current['task']['logicalTick'],
        displayedSubmissionFrameCounter=queued['frameCounter'], currentFrameCounter=current['frameCounter'],
        sourceFrameDistance=2, frontOrigin=current['frontOrigin'], displayedGeometry=queued['task']['draws'],
        method='Pre-submission VSync stack + two-buffer source order + hash-bound prior submission; no pixel/offset search')


def scanout(state):
    gpu = p.gpu_records(p.unpack_state(state))
    x, y = (int.from_bytes(gpu[name], 'little') for name in ('DisplayFB_XStart', 'DisplayFB_YStart'))
    v.require(gpu['DisplayMode'] == bytes(4) and x == 0 and y in (0, 240), 'Unsupported scanout mode/origin')
    output = bytearray(bytes([0, 0, 0, 255]) * 256 * 240); words = gpu['&GPURAM[0][0]']
    for row in range(240):
        for col in range(256):
            word = struct.unpack_from('<H', words, ((y + row) * 1024 + x + col) * 2)[0]
            at = (row * 256 + col) * 4
            output[at:at + 3] = bytes(((word & 31) << 3, ((word >> 5) & 31) << 3, ((word >> 10) & 31) << 3))
    return bytes(output)


def cropped_definition(definition, geometry):
    v.require(type(geometry) is list and len(geometry) == 6, 'Expected six crop records')
    result = copy.deepcopy(definition); result['pieces'] = []
    for index, (piece, item) in enumerate(zip(definition['pieces'], geometry, strict=True)):
        v.fields(item, 'PieceIndex CropTop VisibleRows', 'displayed geometry')
        v.require(type(item['PieceIndex']) is int and item['PieceIndex'] == index, 'Displayed piece order differs')
        v.integer(item['CropTop'], 0, piece['height'], 'crop'); v.integer(item['VisibleRows'], 0, piece['height'], 'rows')
        v.require(item['CropTop'] + item['VisibleRows'] == piece['height'], 'Invalid half-open displayed crop')
        if item['VisibleRows'] == 0: continue
        projected = dict(piece, y=piece['y'] + item['CropTop'], v=piece['v'] + item['CropTop'], height=item['VisibleRows'])
        result['pieces'].append(projected)
    return result


def metrics(original, candidate, points):
    return dict(compared=True, **b.samples(original, candidate, points)) if points else dict(pixels=0, changedPixels=0, compared=False, exactEquality=None)


def compare(original, background, native, original_scanout, probe_scanout, definition, geometry, indices, palette, isolated, *, partial_triplet=False):
    projected = cropped_definition(definition, geometry)
    if projected['pieces']:
        expected = bg.compose(background, projected, indices, palette, partial_triplet=partial_triplet)
        _, _, touched, _ = b.transfer_bounds(projected, indices, palette, partial_triplet=partial_triplet)
        low, high = p.native_bounds(projected, indices, palette, partial_triplet=partial_triplet)
        invariant = {at for at in touched if low[at * 4:at * 4 + 3] == high[at * 4:at * 4 + 3]}
    else:
        v.require(len(background) == bg.CANVAS_BYTES and all(background[at + 3] == 255 and
            not ((background[at] | background[at + 1] | background[at + 2]) & 7) for at in range(0, len(background), 4)), 'Invalid fully hidden background')
        expected, touched, invariant = bytes(background), set(), set()
    canvas = set(range(256 * 240))
    checks = dict(fullCanvas=metrics(original, native, canvas), touched=metrics(original, native, touched),
        backgroundSensitive=metrics(original, native, touched - invariant), foregroundInvariant=metrics(original, native, invariant),
        pythonModel=metrics(expected, native, canvas), originalPngScanout=metrics(original, original_scanout, canvas),
        probePngScanout=metrics(background, probe_scanout, canvas), probeOutsideBanner=metrics(original, background, canvas - touched))
    failures = []
    if not isolated['nonGpuSubsystemsUnchanged'] or not isolated['gpuRegistersUnchanged']: failures.append('state-isolation')
    for key, failure in (('fullCanvas', 'original-composition-mismatch'), ('pythonModel', 'native-model-mismatch'),
            ('originalPngScanout', 'original-scanout-mismatch'), ('probePngScanout', 'probe-scanout-mismatch'), ('probeOutsideBanner', 'probe-footprint')):
        if not checks[key]['exactEquality']: failures.append(failure)
    return dict(checks=checks, failures=failures, passed=not failures, visibleArtworkCompared=bool(touched),
        originalFidelityEstablished=False, controlledProbe=True, toleranceRgb=[0, 0, 0])


def verified(record):
    path = profile_tool.scratch_path(profile_tool.ROOT / record['localPath'])
    v.require(path.stat().st_size == record['bytes'] and record['bytes'] <= p.MAX_STATE, 'Archived input size differs')
    data = path.read_bytes(); v.require(v.sha(data) == record['sha256'], 'Archived input digest differs')
    return data


def native_frames(root, backgrounds, geometries):
    receipt_path = root / 'check.json'; v.require(receipt_path.stat().st_size <= 65536, 'Receipt exceeds budget')
    receipt = v.parse(receipt_path.read_bytes())
    v.fields(receipt, 'format schemaVersion renderer sourceKind referenceRevision precision ditherEnabled framebufferOrigin gpuReadbackPerformed originalComparison checkpoints', 'hide receipt')
    v.require(receipt['format'] == 'fft.banner-hide-cpu-check' and type(receipt['schemaVersion']) is int and receipt['schemaVersion'] == 1 and
        receipt['renderer'] == 'FiveBitGeneralBannerPresentation.ComposeOver' and receipt['sourceKind'] == 'verified-retail' and
        receipt['referenceRevision'] == b.REVISION and receipt['precision'] == 'five-bit' and receipt['ditherEnabled'] is False and
        receipt['gpuReadbackPerformed'] is False and receipt['originalComparison'] == 'pending' and receipt['framebufferOrigin'] == [0, 0] and
        all(type(x) is int for x in receipt['framebufferOrigin']), 'Unsupported hide CPU profile/state')
    v.require(type(receipt['checkpoints']) is list and len(receipt['checkpoints']) == 5, 'Expected five hide frames')
    result = {}
    for i, entry in enumerate(receipt['checkpoints']):
        tick = 81 + i
        v.fields(entry, 'logicalTick phase revealCompleted holdCompleted hideCompleted file backgroundSha256 rgbaSha256 pieces', 'hide CPU checkpoint')
        for key, value in (('logicalTick', tick), ('revealCompleted', 20), ('holdCompleted', 60), ('hideCompleted', i + 1)):
            v.require(type(entry[key]) is int and entry[key] == value, 'Wrong native hide anchor/counter')
        v.require(entry['phase'] == 'Hiding' and entry['file'] == f'tick-{tick:03}.rgba' and
                  entry['backgroundSha256'] == v.sha(backgrounds[tick]) and entry['pieces'] == geometries[tick], 'Native background/projection mismatch')
        v.require(all(type(g[key]) is int for g in entry['pieces'] for key in ('PieceIndex', 'CropTop', 'VisibleRows')), 'Invalid native crop fields')
        file = root / entry['file']; v.require(file.stat().st_size == bg.CANVAS_BYTES, 'Wrong native canvas size')
        pixels = file.read_bytes(); v.require(v.sha(pixels) == entry['rgbaSha256'], 'Native pixel digest differs')
        v.require(all(pixels[at + 3] == 255 and not ((pixels[at] | pixels[at + 1] | pixels[at + 2]) & 7)
                      for at in range(0, len(pixels), 4)), 'Native pixels are not opaque RGB5')
        result[tick] = pixels
    return receipt, result


def run(plan_path, profile_root, output):
    from PIL import Image, __version__
    v.require(sys.platform == 'win32' and __version__ == '12.3.0', 'Expected Windows/Pillow 12.3.0')
    output, profile_root, plan_path = map(profile_tool.scratch_path, (output, profile_root, plan_path))
    v.require(not output.exists(), 'Output already exists; choose a fresh scratch directory')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget'); plan = v.parse(plan_path.read_bytes())
    inputs, _, _, definition, _, _ = b.load(plan)
    source = inputs['original']['state']; palette = inputs['prepared']['palette']
    probe, intervention = bg.transparent_palette_state(source, palette)
    profile_path = profile_root / 'profile.json'; v.require(profile_path.read_bytes() == inputs['original']['profile'], 'Profile differs from original evidence')
    profile = v.parse(inputs['original']['profile'])
    evidence_path = profile_tool.ROOT / 'docs/evidence/visual/original-banner-continuation-v1.json'
    evidence = v.parse(evidence_path.read_bytes())
    v.require(evidence['format'] == 'fft.original-banner-continuation-evidence' and evidence['schemaVersion'] == 1 and
              evidence['referenceRevision'] == b.REVISION and evidence['entryStateSha256'] == v.sha(source), 'Continuation archive provenance differs')
    records = {x['frontendFrames']: x for x in evidence['repeatValidation']['checkpointsIdentities']}
    tools = [c.identity(Path(module.__file__)) for module in (sys.modules[__name__], p, c, bg, b, v, profile_tool, trace)]
    tools += [c.identity(profile_tool.ROOT / name) for name in ('tools/banner-blend-check/Program.cs',
        'src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs', 'src/Fft.Client/Presentation/GeneralBannerPresentation.cs')]
    pinned = [c.identity(plan_path), c.identity(profile_path), c.identity(evidence_path)]
    for item in profile['inputs']: v.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Pinned source input changed')
    for name, digest in profile['generatedSha256'].items():
        v.require(name in ('retroarch.cfg', 'core-options.cfg', 'launch.ps1') and profile_tool.file_hash(profile_root / name) == digest, 'Prepared policy changed')
    v.require(profile_tool.file_hash(profile_root / 'system/scph5501.bin') == profile_tool.BIOS_HASH, 'Actual BIOS changed')
    normal = {x['path']: profile_tool.file_hash(Path(x['path'])) for x in profile['normalConfigIdentities']}
    # Validate all archived snapshots/bindings before launching any controlled process.
    anchors = []
    for frames, tick in SCHEDULE:
        original_state, queued_state = verified(records[frames]['state']), verified(records[frames - 4]['state'])
        current, queued = snapshot(original_state, definition), snapshot(queued_state, definition)
        binding = bind_display(current, queued); v.require(binding['displayedLogicalTick'] == tick, 'Unexpected displayed hide checkpoint')
        drawn = trace.trace(queued_state, definition, queued['task']['draws'])
        queued_ram = c.read_ram(queued_state)
        v.require(all(trace.word(queued_ram, item['packetAddress'] + 12) >> 16 == 0x3fd8 for item in drawn['banners']), 'Displayed banner CLUT differs')
        original_gpu = p.gpu_records(p.unpack_state(original_state))
        v.require(original_gpu['&GPURAM[0][0]'][bg.PALETTE_OFFSET:bg.PALETTE_OFFSET + 32] == palette, 'Original palette differs at displayed anchor')
        png = verified(records[frames]['png']); full = b.image(png, (280, 240))
        original = b.align(full, bytes(bg.CANVAS_BYTES), [12, 0, 256, 240], [0, 0, 256, 240])[0]
        anchors.append((frames, tick, original_state, original, binding, drawn, current))
    output.mkdir(parents=True); entries = []; backgrounds, geometries = {}, {}
    try:
        for frames, tick, original_state, original, binding, drawn, current in anchors:
            destination = output / f'frame-{frames:03}'; destination.mkdir()
            saved, background, probe_capture = bg.capture(profile, profile_root, destination / 'probe', probe, frames)
            probe_context = snapshot(saved, definition)
            v.require(probe_context == current, 'Probe task/frame context differs')
            gpu = p.gpu_records(p.unpack_state(saved))
            v.require(gpu['&GPURAM[0][0]'][bg.PALETTE_OFFSET:bg.PALETTE_OFFSET + 32] == bytes(32), 'Probe palette overwritten')
            geometry = binding['displayedGeometry']; backgrounds[tick] = background; geometries[tick] = geometry
            (destination / 'background.rgba').write_bytes(background)
            entries.append(dict(frontendFrames=frames, displayedLogicalTick=tick, binding=binding, sourceContext=current,
                queuedSource=records[frames - 4]['state'], originalState=records[frames]['state'], originalPng=records[frames]['png'],
                drawTrace=drawn, probeCapture=probe_capture, stateIsolation=bg.isolation(original_state, saved)))
            for name, pixels in (('original.png', original), ('background.png', background)):
                Image.frombytes('RGBA', (256, 240), pixels).save(destination / name)
            print(f'Captured hide {tick}: current queued tick {current["task"]["logicalTick"]}, displayed crop {geometry[0]["CropTop"]}', flush=True)
        native_plan = dict(format='fft.banner-hide-backgrounds', schemaVersion=1, checkpoints=[dict(logicalTick=tick,
            backgroundPath=str(output / f'frame-{frames:03}' / 'background.rgba'), backgroundSha256=v.sha(backgrounds[tick])) for frames, tick in SCHEDULE])
        (output / 'backgrounds.json').write_text(json.dumps(native_plan, indent=2) + '\n', encoding='utf-8')
        prepared = v.scratch_file(plan['prepared']['definition']['path']).parent
        args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
                '--', '--sequence', str(prepared), str(output / 'backgrounds.json'), str(output / 'native')]
        (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n', encoding='utf-8')
        result = subprocess.run(args, cwd=profile_tool.ROOT, capture_output=True, timeout=55)
        (output / 'native-stdout.txt').write_bytes(result.stdout); (output / 'native-stderr.txt').write_bytes(result.stderr)
        v.require(result.returncode == 0, 'CPU sequence host failed; build Release first')
        receipt, native = native_frames(output / 'native', backgrounds, geometries)
        for entry, anchor in zip(entries, anchors, strict=True):
            frames, tick, state, original, _, _, _ = anchor; destination = output / f'frame-{frames:03}'
            probe_state = verified(entry['probeCapture']['exitState'])
            entry['comparison'] = compare(original, backgrounds[tick], native[tick], scanout(state), scanout(probe_state),
                definition, geometries[tick], inputs['prepared']['indices'], palette, entry['stateIsolation'])
            Image.frombytes('RGBA', (256, 240), native[tick]).save(destination / 'native.png')
            print(f'Hide {tick}: exact={entry["comparison"]["passed"]}, samples={entry["comparison"]["checks"]["touched"]["pixels"]}', flush=True)
    finally:
        for path, digest in normal.items(): v.require(profile_tool.file_hash(Path(path)) == digest, 'Installed configuration changed')
        for name, digest in profile['generatedSha256'].items(): v.require(profile_tool.file_hash(profile_root / name) == digest, 'Base policy changed')
        for item in tools + pinned:
            v.require(profile_tool.file_hash(profile_tool.ROOT / item['localPath']) == item['sha256'], 'Tool/metadata changed during capture')
    report = dict(format='fft.banner-hide-comparison', schemaVersion=1, referenceRevision=b.REVISION,
        sourcePlan=pinned[0], captureProfile=pinned[1], continuationEvidence=pinned[2], entryStateSha256=v.sha(source),
        intervention=intervention, sourceFunctions=[dict(address=x, bytes=y, sha256=z) for x, y, z in CODE],
        checkpoints=entries, nativeReceipt=receipt, nativeReceiptIdentity=c.identity(output / 'native/check.json'), tools=tools,
        passed=all(x['comparison']['passed'] for x in entries), originalFidelityEstablished=False,
        displayedFrameCorrespondenceEstablished=True, universalFrameLagEstablished=False, fullRevealObserved=False,
        originalEvent401Verified=False, controlledProbe=True, gpuReadbackPerformed=False, normalConfigsUnchanged=True)
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    cards = ''.join(f'<h2>Displayed logical tick {tick}, crop {geometries[tick][0]["CropTop"]}</h2>' +
        '<div class="row">' + ''.join(f'<figure><figcaption>{label}</figcaption><img width="512" height="480" src="frame-{frames:03}/{name}.png"></figure>'
        for label, name in (('Unmodified original', 'original'), ('Controlled background', 'background'), ('Actual C#', 'native'))) + '</div>' for frames, tick in SCHEDULE)
    (output / 'review.html').write_text('<!doctype html><meta charset="utf-8"><title>Displayed banner hide comparison</title>'
        '<style>body{background:#20242a;color:#eee;font:16px sans-serif;margin:24px}.row{display:flex;gap:16px;flex-wrap:wrap}'
        'figure{margin:0}img{image-rendering:pixelated}figcaption{margin-bottom:8px}</style><h1>Displayed banner hide comparison</h1>'
        '<p>Display labels follow source-bound buffer history, not the newer queued task or a best-fit crop. '
        'The controlled background uses a transparent palette in a state copy. Unchanged background pixels do not validate managed map rendering.</p>' + cards + '\n', encoding='utf-8', newline='\n')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--profile', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try: sys.exit(run(args.plan, args.profile, args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, zlib.error, subprocess.SubprocessError) as error:
        parser.exit(2, 'Hide comparison failed (diagnostics retained): ' + str(error) + '\n')
