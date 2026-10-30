"""Controlled palette-transparent original probe and actual C# background blend comparison."""
import argparse
import base64
import json
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import zlib

import analyze_banner_precision as p
import capture_banner_continuation as continuation
import compare_observed_banner as b
import compare_visual_checkpoints as v
import prepare_retroarch_reference as profile_tool
import trace_banner_draw_state as trace
import verify_retroarch_boot as boot

PALETTE_OFFSET = (255 * 1024 + 384) * 2
CANVAS_BYTES = 256 * 240 * 4


def transparent_palette_state(state, palette):
    return replace_palette_state(state, palette, PALETTE_OFFSET)


def replace_palette_state(state, palette, palette_offset):
    """Change just the selected 16 VRAM words, preserving every other uncompressed byte."""
    v.require(type(palette_offset) is int and 0 <= palette_offset <= 1048576 - 32 and palette_offset % 2 == 0, 'Invalid palette location')
    v.require(len(palette) == 32 and any(palette), 'Expected nonempty 16-word banner palette')
    core = p.unpack_state(state); vram = p.gpu_records(core)['&GPURAM[0][0]']
    v.require(vram[palette_offset:palette_offset + 32] == palette, 'VRAM palette differs from prepared palette')
    # Uniqueness is mandatory: this is a bounded inspector, not a general save editor.
    v.require(core.count(vram) == 1, 'Ambiguous VRAM record location')
    offset = core.index(vram) + palette_offset
    changed = core[:offset] + bytes(32) + core[offset + 32:]
    raw, pos = bytearray(), 20
    # The existing bounded parser has already validated every RZIP chunk/RASTATE block.
    while pos < len(state):
        length = struct.unpack_from('<I', state, pos)[0]; pos += 4
        raw.extend(zlib.decompress(state[pos:pos + length])); pos += length
    v.require(raw.count(core) == 1, 'Ambiguous core record location')
    core_offset = raw.index(core); raw[core_offset:core_offset + len(core)] = changed
    result = bytearray(state[:20])
    for start in range(0, len(raw), 131072):
        block = zlib.compress(raw[start:start + 131072]); result.extend(struct.pack('<I', len(block))); result.extend(block)
    result = bytes(result)
    v.require(p.unpack_state(result) == changed, 'Patched state failed round trip')
    return result, dict(vramByteOffset=palette_offset, wordCount=16, byteCount=32,
        changedCoreBytes=sum(a != c for a, c in zip(core, changed, strict=True)),
        onlySelectedPaletteChanged=True, entryCoreSha256=v.sha(core), probeCoreSha256=v.sha(changed))


def section_hashes(core):
    # Validate framing through the common reader before inspecting section payloads.
    p.subsystem_records(core, b'MAIN')
    used = struct.unpack_from('<I', core, 20)[0]; pos, result = 32, {}
    while pos < used:
        name = core[pos:pos + 32].split(b'\0', 1)[0].decode('ascii')
        size = struct.unpack_from('<I', core, pos + 32)[0]; pos += 36
        result[name] = v.sha(core[pos:pos + size]); pos += size
    return result


def isolation(original_state, probe_state):
    original_core, probe_core = p.unpack_state(original_state), p.unpack_state(probe_state)
    left, right = section_hashes(original_core), section_hashes(probe_core)
    v.require(set(left) == set(right), 'State subsystem sets differ')
    non_gpu = {key: left[key] == right[key] for key in left if key != 'GPU'}
    original_gpu, probe_gpu = p.gpu_records(original_core), p.gpu_records(probe_core)
    v.require(set(original_gpu) == set(probe_gpu), 'GPU record sets differ')
    gpu_registers = all(original_gpu[key] == probe_gpu[key] for key in original_gpu if key != '&GPURAM[0][0]')
    ram = p.subsystem_records(original_core, b'MAIN')['MainRAM.data8']
    return dict(nonGpuSubsystemsUnchanged=all(non_gpu.values()), nonGpuSubsystems=non_gpu,
                gpuRegistersUnchanged=gpu_registers, mainRamSha256=v.sha(ram),
                originalSubsystemSha256=left, probeSubsystemSha256=right)


def compose(background, definition, indices, palette, *, partial_triplet=False):
    v.require(len(background) == CANVAS_BYTES, 'Expected one native background')
    v.require(all(background[at + 3] == 255 and not ((background[at] | background[at + 1] | background[at + 2]) & 7)
                  for at in range(0, CANVAS_BYTES, 4)), 'Background must be opaque zero-padded RGB5')
    b.transfer_bounds(definition, indices, palette, partial_triplet=partial_triplet)
    rgba = bytearray(background); words = struct.unpack('<16H', palette)
    for at in range(0, CANVAS_BYTES, 4):
        for channel in range(3): rgba[at + channel] >>= 3
    for piece in definition['pieces']:
        for row in range(piece['height']):
            for x in range(piece['width']):
                word = words[indices[(piece['v'] + row) * 256 + piece['u'] + x]]
                if not word: continue
                colors = [p.quad_vertex_channel(piece['colors'][0][c], piece['colors'][2][c], x, row, piece['width'], piece['height']) for c in 'rgb']
                at = ((piece['y'] + row) * 256 + piece['x'] + x) * 4
                for channel in range(3):
                    source = p.shade5((word >> (channel * 5)) & 31, colors[channel])
                    old = rgba[at + channel]
                    rgba[at + channel] = source if not word & 0x8000 else min(31, old + source) if piece['material'] == 'add' else max(0, old - source)
    for at in range(0, CANVAS_BYTES, 4):
        for channel in range(3): rgba[at + channel] <<= 3
    return bytes(rgba)


def diagnostics(original, background, native, definition, indices, palette, state_isolation):
    expected = compose(background, definition, indices, palette)
    _, _, touched, _ = b.transfer_bounds(definition, indices, palette)
    low, high = p.native_bounds(definition, indices, palette)
    invariant = {at for at in touched if low[at * 4:at * 4 + 3] == high[at * 4:at * 4 + 3]}
    canvas = set(range(256 * 240)); sensitive = touched - invariant
    outside = b.samples(original, background, canvas - touched)
    checks = dict(fullCanvas=b.samples(original, native, canvas), touched=b.samples(original, native, touched),
        backgroundSensitive=b.samples(original, native, sensitive), foregroundInvariant=b.samples(original, native, invariant),
        pythonModel=b.samples(expected, native, canvas), probeOutsideBanner=outside,
        probeChangedBannerPixels=b.samples(original, background, touched)['changedPixels'])
    failures = []
    if not state_isolation['nonGpuSubsystemsUnchanged'] or not state_isolation['gpuRegistersUnchanged']: failures.append('state-isolation')
    if not outside['exactEquality']: failures.append('probe-footprint')
    if not checks['pythonModel']['exactEquality']: failures.append('native-model-mismatch')
    if not checks['fullCanvas']['exactEquality']: failures.append('original-composition-mismatch')
    return dict(checks=checks, failures=failures, passed=not failures,
        controlledProbe=True, unalteredRetailBackgroundCapture=False, originalFidelityEstablished=False,
        scope='Full-visible six-piece banner over a palette-transparent controlled original background; untouched pixels are inherited.',
        diagnosticToleranceRgb=[0, 0, 0], timingComparisonPerformed=False, generalTriangleRasterizationEstablished=False)


def native_capture(root, background):
    raw = (root / 'check.json').read_bytes(); receipt = v.parse(raw)
    v.fields(receipt, 'format schemaVersion renderer precision backgroundPolicy sourceKind referenceRevision backgroundSha256 rgbaSha256 logicalTick phase revealCompleted holdCompleted hideCompleted ditherEnabled framebufferOrigin pieces gpuReadbackPerformed originalComparison', 'CPU blend receipt')
    v.require(receipt['format'] == 'fft.banner-background-cpu-check' and type(receipt['schemaVersion']) is int and receipt['schemaVersion'] == 1 and
              receipt['renderer'] == 'FiveBitGeneralBannerPresentation.ComposeOver' and receipt['precision'] == 'five-bit' and
              receipt['sourceKind'] == 'verified-retail' and receipt['referenceRevision'] == b.REVISION, 'Unsupported native CPU profile')
    v.text(receipt['backgroundPolicy'], 'background policy')
    v.require(receipt['backgroundSha256'] == v.sha(background) and receipt['ditherEnabled'] is False and
              receipt['framebufferOrigin'] == [0, 0] and all(type(x) is int for x in receipt['framebufferOrigin']) and
              receipt['gpuReadbackPerformed'] is False and receipt['originalComparison'] == 'pending', 'Invalid explicit background/draw state')
    for key, value in (('logicalTick', 29), ('revealCompleted', 20), ('holdCompleted', 9), ('hideCompleted', 0)):
        v.require(type(receipt[key]) is int and receipt[key] == value, 'Wrong native logical anchor')
    v.require(receipt['phase'] == 'Holding' and receipt['pieces'] == [dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)], 'Expected full-visible anchor')
    v.require(all(type(item[key]) is int for item in receipt['pieces'] for key in ('PieceIndex', 'CropTop', 'VisibleRows')), 'Invalid native geometry fields')
    image_path = root / 'composed.rgba'
    v.require(image_path.stat().st_size == CANVAS_BYTES, 'Native canvas exceeds/wrong size')
    rgba = image_path.read_bytes(); v.require(v.sha(rgba) == receipt['rgbaSha256'], 'Native pixels differ from receipt')
    v.require(all(rgba[at + 3] == 255 and not ((rgba[at] | rgba[at + 1] | rgba[at + 2]) & 7)
                  for at in range(0, CANVAS_BYTES, 4)), 'Native canvas is not opaque zero-padded RGB5')
    return rgba, receipt


def capture(profile, profile_root, output, source, frames=24):
    output.mkdir(); (output / 'states').mkdir(); (output / 'saves').mkdir()
    state_name = Path(profile['cue']).stem + '.state'; copied = output / 'states' / state_name
    copied.write_bytes(source); shutil.copyfile(profile_root / 'core-options.cfg', output / 'core-options.cfg')
    settings = dict(input_driver='null', input_joypad_driver='null', global_core_options='true',
        core_options_path=(output / 'core-options.cfg').as_posix(), savestate_directory=(output / 'states').as_posix(),
        sort_savestates_enable='false', savestates_in_content_dir='false', savefile_directory=(output / 'saves').as_posix(), savestate_auto_save='true')
    (output / 'restore.cfg').write_text(profile_tool.config(settings), encoding='utf-8', newline='\n')
    args = continuation.command(profile, profile_root, output, frames)
    (output / 'command.json').write_text(json.dumps(args, indent=2) + '\n', encoding='utf-8', newline='\n')
    startup = subprocess.STARTUPINFO(); startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW; startup.wShowWindow = 0
    result = subprocess.run(args, cwd=Path(profile['executable']).parent, capture_output=True, timeout=55, startupinfo=startup)
    (output / 'stdout.txt').write_bytes(result.stdout); (output / 'stderr.txt').write_bytes(result.stderr)
    v.require(result.returncode == 0, 'Original process failed; inspect retained logs')
    log = (output / 'retroarch.log').read_text(encoding='utf-8', errors='replace')
    v.require('[State] Loading state' in log and 'Controller 1: Unplugged' in log and 'Controller 2: Unplugged' in log, 'State/input isolation not confirmed')
    desired, effective = boot.options((profile_root / 'core-options.cfg').read_text()), boot.options((output / 'core-options.cfg').read_text())
    v.require(all(effective.get(k) == value for k, value in desired.items()), 'Core options changed')
    v.require(copied.read_bytes() == source, 'State copy changed')
    saved = output / 'states' / (state_name + '.auto')
    v.require(saved.stat().st_size <= p.MAX_STATE, 'Exit state exceeds budget')
    state = saved.read_bytes(); png_path = output / 'frame.png'
    v.require(png_path.stat().st_size <= 32 * 1024 * 1024, 'Screenshot exceeds budget')
    pixels = b.image(png_path.read_bytes(), (280, 240))
    # Preserve the already reviewed fixed 12-column border mapping, never search for alignment.
    pixels = b.align(pixels, bytes(CANVAS_BYTES), [12, 0, 256, 240], [0, 0, 256, 240])[0]
    metadata = dict(frontendFrames=frames, inputStateSha256=v.sha(source), exitState=continuation.identity(saved),
        png=continuation.identity(png_path), command=continuation.identity(output / 'command.json'),
        config=continuation.identity(output / 'restore.cfg'), effectiveOptions=continuation.identity(output / 'core-options.cfg'),
        log=continuation.identity(output / 'retroarch.log'), diagnostics=[x for x in log.splitlines() if '[ERROR]' in x or '[WARN]' in x])
    return state, pixels, metadata


def review(original_png, background_png, native_png):
    cards = ''.join('<section><h2>' + title + '</h2><img width="512" height="480" src="data:image/png;base64,' +
        base64.b64encode(png).decode('ascii') + '"></section>' for title, png in
        (('Unmodified original', original_png), ('Controlled transparent-palette background', background_png), ('Actual C# composition', native_png)))
    return ('<!doctype html><meta charset="utf-8"><title>Banner background blend check</title><style>'
        'body{font:16px sans-serif;background:#20242a;color:#eee;margin:24px}main{display:flex;flex-wrap:wrap;gap:24px}'
        'h2{font-size:18px}img{image-rendering:pixelated;border:1px solid #666}section{max-width:512px}</style>'
        '<h1>Full-visible banner over a controlled original background</h1><p>The middle view comes from '
        'an intentionally altered palette in a state copy. The right view uses that background and prepared '
        'artwork in the actual C# CPU compositor. Native 256x240 crops shown at 2×, no resampling in metrics.</p>'
        '<p>Matching inherited background pixels do not prove map rendering. Reveal/hide pixels, screenshot/task '
        'latency, general rasterization and event identity remain open.</p><main>' + cards + '</main>\n')


def run(plan_path, profile_root, output):
    from PIL import Image, __version__
    v.require(sys.platform == 'win32' and __version__ == '12.3.0', 'Expected Windows/Pillow 12.3.0 reference environment')
    output, profile_root, plan_path = map(profile_tool.scratch_path, (output, profile_root, plan_path))
    v.require(not output.exists(), 'Output already exists; choose a fresh scratch directory')
    v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    inputs, _, _, definition, _, _ = b.load(v.parse(plan_path.read_bytes()))
    source = inputs['original']['state']; palette = inputs['prepared']['palette']
    ram = continuation.read_ram(source); baseline = continuation.inspect_ram(ram, definition)
    v.require(baseline['logicalTick'] == 18 and baseline['phase'] == 'Revealing', 'Unexpected entry anchor')
    draw_trace = trace.trace(source, definition)
    v.require(all(trace.word(ram, trace.BANNER_BASE + bank * 2080 + i * 52 + 12) >> 16 == 0x3fd8
                  for bank in (0, 1) for i in range(6)), 'Unexpected banner palette binding')
    probe, intervention = transparent_palette_state(source, palette)
    profile_path = profile_root / 'profile.json'
    v.require(profile_path.read_bytes() == inputs['original']['profile'], 'Profile differs from original observation')
    profile = v.parse(inputs['original']['profile'])
    tools = [continuation.identity(Path(module.__file__)) for module in (sys.modules[__name__], p, continuation, b, v, profile_tool, trace, boot)]
    tool_sources = [profile_tool.ROOT / name for name in ('src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs',
        'src/Fft.Client/Presentation/GeneralBannerPresentation.cs', 'tools/banner-blend-check/Program.cs',
        'tools/banner-blend-check/BannerBlendCheck.csproj', 'tools/banner-blend-check/packages.lock.json')]
    tools += [continuation.identity(path) for path in tool_sources]
    pinned = [continuation.identity(plan_path), continuation.identity(profile_path)]
    for item in profile['inputs']:
        v.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Pinned emulator/content input changed')
    for name, digest in profile['generatedSha256'].items():
        v.require(name in ('retroarch.cfg', 'core-options.cfg', 'launch.ps1') and profile_tool.file_hash(profile_root / name) == digest, 'Prepared profile changed')
    v.require(profile_tool.file_hash(profile_root / 'system/scph5501.bin') == profile_tool.BIOS_HASH, 'Actual BIOS changed')
    normal = {x['path']: profile_tool.file_hash(Path(x['path'])) for x in profile['normalConfigIdentities']}
    output.mkdir(parents=True)
    try:
        (output / 'probe.state').write_bytes(probe)
        left_state, original, left = capture(profile, profile_root, output / 'original', source)
        right_state, background, right = capture(profile, profile_root, output / 'probe', probe)
        observations = [continuation.inspect_ram(continuation.read_ram(state), definition, baseline['slot']) for state in (left_state, right_state)]
        v.require(observations[0] == observations[1] and observations[0]['phase'] == 'Holding' and observations[0]['logicalTick'] == 29, 'Wrong/unmatched task anchor')
        original_vram = p.gpu_records(p.unpack_state(left_state))['&GPURAM[0][0]']
        probe_vram = p.gpu_records(p.unpack_state(right_state))['&GPURAM[0][0]']
        v.require(original_vram[PALETTE_OFFSET:PALETTE_OFFSET + 32] == palette and probe_vram[PALETTE_OFFSET:PALETTE_OFFSET + 32] == bytes(32), 'Palette overwritten during continuation')
        state_isolation = isolation(left_state, right_state)
        (output / 'background.rgba').write_bytes(background)
        prepared = v.scratch_file(v.parse(plan_path.read_bytes())['prepared']['definition']['path']).parent
        args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
                '--', str(prepared), str(output / 'background.rgba'), str(output / 'native')]
        (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n', encoding='utf-8')
        result = subprocess.run(args, cwd=profile_tool.ROOT, capture_output=True, timeout=55)
        (output / 'native-stdout.txt').write_bytes(result.stdout); (output / 'native-stderr.txt').write_bytes(result.stderr)
        v.require(result.returncode == 0, 'C# CPU host failed; build its Release target first')
        native, receipt = native_capture(output / 'native', background)
        report = diagnostics(original, background, native, definition, inputs['prepared']['indices'], palette, state_isolation)
        report.update(format='fft.banner-background-comparison', schemaVersion=1, referenceRevision=b.REVISION,
            sourcePlan=pinned[0], captureProfile=pinned[1], entryStateSha256=v.sha(source), probeState=continuation.identity(output / 'probe.state'),
            intervention=intervention, originalCapture=left, probeCapture=right, taskObservation=observations[0],
            stateIsolation=state_isolation, frozenDrawStateTrace=draw_trace, nativeReceipt=receipt,
            nativeReceiptIdentity=continuation.identity(output / 'native/check.json'), nativePixels=continuation.identity(output / 'native/composed.rgba'),
            tools=tools, selectedCoreOptionsRetained=True, normalConfigsUnchanged=True, replayPlaybackVerified=False,
            fullRevealObserved=False, originalEvent401Verified=False, gpuReadbackPerformed=False)
        for name, pixels in (('original.png', original), ('background.png', background), ('native.png', native)):
            Image.frombytes('RGBA', (256, 240), pixels).save(output / name)
    finally:
        for path, digest in normal.items(): v.require(profile_tool.file_hash(Path(path)) == digest, 'Installed configuration changed')
        for name, digest in profile['generatedSha256'].items(): v.require(profile_tool.file_hash(profile_root / name) == digest, 'Prepared policy changed')
        for item in tools + pinned:
            v.require(profile_tool.file_hash(profile_tool.ROOT / item['localPath']) == item['sha256'], 'Tool/metadata changed during capture')
    (output / 'review.html').write_text(review(*[(output / name).read_bytes() for name in ('original.png', 'background.png', 'native.png')]), encoding='utf-8', newline='\n')
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
    print(f'Actual C# blend exact={report["passed"]}; {report["checks"]["backgroundSensitive"]["pixels"]} background-sensitive pixels; controlled probe, wider fidelity open')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--profile', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try: sys.exit(run(args.plan, args.profile, args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, zlib.error, subprocess.SubprocessError) as error:
        parser.exit(2, 'Background comparison failed (diagnostic files retained): ' + str(error) + '\n')
