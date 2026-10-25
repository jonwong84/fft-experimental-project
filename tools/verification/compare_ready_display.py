"""Finite source-bound Ready scanouts versus the actual prepared C# compositor.

Developer validation only. Original bytes and every generated image remain in scratch.
No alignment/scale search, tolerance, live controller input or runtime reference dependency.
"""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import sys
import zlib

import analyze_banner_precision as p
import capture_banner_continuation as c
import compare_banner_background as bg
import compare_banner_hide as h
import compare_observed_banner as b
import compare_visual_checkpoints as v
import inspect_banner_event as event
import prepare_retroarch_reference as profile_tool
import trace_banner_draw_state as d

ROOT = profile_tool.ROOT
TICKS = (8, 10, 60, 64, 66, 67)
CURRENT = (22, 26, 126, 134, 138, 140)
ORIGINAL = tuple(sorted(set(CURRENT) | {n - 4 for n in CURRENT}))
PALETTE_OFFSET = (254 * 1024 + 416) * 2
EXTRA_CODE = ((0x8014c958, 60, '23aa4a3549fe6cb707b4f4e7a19e7f36356ce2411a96dfbb3d64c6fc5d1c2392'),
              (0x8014c9d0, 76, '29370957c26343c793a1c82447b0b7e40b38718e28ed7dcca189978d9b8b37f9'))


def phase(tick):
    v.integer(tick, 0, 69, 'bounded native elapsed tick')
    if tick < 10: return 'FadeIn', 48 + tick * 8, 0x801ca440
    if tick < 60: return 'Holding', 128, 0x801ca524
    if tick < 67: return 'FadeOut', 128 - (tick - 60) * 12, 0x801ca5f4
    return ('ExtraYield', None, 0x801ca608) if tick == 67 else ('ChildComplete', None, 0x8014c984)


def threads(ram, birth_frame):
    tick = d.word(ram, h.FRAME_COUNTER) - birth_frame
    name, scale, pc = phase(tick)
    v.require(d.word(ram, c.THREADS_POINTER) == c.THREADS_BASE, 'Thread table changed')
    child, dispatcher = c.THREADS_BASE + 4096, c.THREADS_BASE + 3072
    word = lambda base, offset: d.word(ram, base + offset)
    v.require([word(child, x) for x in (0, 4, 8, 0x44, 0x48, 0x4c)] ==
              [0, 0, 0 if tick < 60 else 2, pc, int(tick < 68), 0], 'Ready child identity/phase differs')
    v.require(word(child, 0x3c) == (0x8016abfc if tick < 68 else 0x8016abe4) and
              d.word(ram, 0x8016abfc + 0x5c) == 0x801c9fac,
              'Ready child stack/caller changed')
    if tick >= 68: v.require(d.word(ram, 0x8016abe4 + 0x10) == 0x801ca610, 'Child exit caller differs')
    if scale is not None:
        # s3 is reused for buffer * sizeof(bank) during the constant-128 hold.
        saved_scale = scale + 8 if tick < 10 else scale - 12 if tick >= 60 else ((tick + 1) & 1) * 2080
        v.require(word(child, 0x1c) == saved_scale and word(child, 0x20) == 6 and word(child, 0x2c) == 6,
                  'Ready saved scale/range differs (yield delay slot)')
    else: v.require(word(child, 0x1c) == 44, 'Unexpected completed fade-out counter')
    bank = (tick + 1) & 1 if scale is not None else 1
    v.require(word(child, 0x24) == bank, 'Ready draw bank differs')
    expected_pc = 0x801c3bc8 if tick < 60 else 0x8014c9e8 if tick < 69 else 0x8014c984
    v.require([word(dispatcher, x) for x in (0, 4, 8, 0x44, 0x48, 0x4c)] ==
              [0, 60, 0, expected_pc, int(tick < 69), 56 if tick < 69 else 0], 'Dispatcher identity/phase differs')
    if tick < 60:
        v.require(word(dispatcher, 0x10) == tick and word(dispatcher, 0x14) == 60 and
                  word(dispatcher, 0x3c) == 0x8016a7fc and d.word(ram, 0x8016a81c) == 0x801cb054, 'Input-wait caller/counter differs')
    elif tick < 69:
        v.require(word(dispatcher, 0x10) == 4096 and word(dispatcher, 0x3c) == 0x8016a80c and
                  d.word(ram, 0x8016a820) == 0x801cb120, 'Child-wait caller/selector differs')
    live_dispatchers = [slot for slot in range(1, 16) if word(c.THREADS_BASE + slot * 1024, 0x48) and
                        word(c.THREADS_BASE + slot * 1024, 0x4c) == 56]
    v.require(live_dispatchers == ([3] if tick < 69 else []), 'Ambiguous live dispatcher')
    return dict(logicalTick=tick, phase=name, scale=scale, childPc=pc, childSlot=4, dispatcherSlot=3, bank=bank, draws=[])


def event_identity(ram, archive, lengths):
    """Selected event-4 E5 remains suspended even after task 56 clears; do not relax older inspector."""
    v.require(len(archive) == 4096000 and v.sha(archive) == event.ARCHIVE_SHA, 'Wrong original archive')
    v.require(d.word(ram, 0x80165f9c) == event.VARIABLES and d.word(ram, 0x80173ca4) == event.BLOCK and
              d.word(ram, event.VARIABLES + 0x27 * 4) == d.word(ram, 0x801696fc) == 4, 'Loaded/current event differs')
    block = event.BLOCK & 0x1fffffff
    live = ram[block:block + 8192]; raw = archive[4 * 8192:5 * 8192]
    matches = [i for i in range(500) if archive[i * 8192 + 4:(i + 1) * 8192] == live[4:]]
    v.require(live[:4] == b'\xf2' * 4 and matches == [4], 'Processed payload is not uniquely event 4')
    boundary, records = event.instructions(raw, lengths)
    v.require(d.word(ram, 0x80173f8c) == event.BLOCK + boundary and
              (810, 0x78, bytes([0, 60])) in records and (813, 0xe5, bytes([56, 0])) in records, 'Ready command/barrier differs')
    at = c.THREADS_BASE + 1024; sp = d.word(ram, at + 0x3c)
    v.require([d.word(ram, at + x) for x in (0x14, 0x20, 0x28, 0x40, 0x44, 0x48, 0x4c)] ==
              [c.THREADS_BASE, 0xe5, 56, 813, 0x8014597c, 1, 68] and
              sp == 0x80169ff4 and d.word(ram, sp + 0x4c) == 0x80143b98, 'Event barrier stack/context differs')
    return dict(eventId=4, commandOffset=810, waitOffset=813, uniqueArchiveMatch=matches,
                rawBlockSha256=v.sha(raw), processedBlockSha256=v.sha(live))


def packets(ram, task, glyphs):
    current = d.word(ram, d.CURRENT_BUFFER)
    v.require(current in (d.BUFFER_BASE, d.BUFFER_BASE + d.BUFFER_STRIDE), 'Unexpected render buffer')
    ot = d.word(ram, current + 0xe5b4)
    v.require(ot in (0x80056b18, 0x80057118) and d.word(ram, d.SCRIPT_OT) == ot, 'Unexpected script/render OT')
    chain, nodes = d.walk(ram, ot + 0x17f * 4); mode = None; selected = []
    for address, data in chain:
        op = data[0] >> 24
        if op == 0xe1:
            v.require(len(data) == 2 and (data[1] == 0 or data[1] >> 24 == 0xe2), 'Unknown mode payload')
            mode = (address, data[0], data[1])
        for bank in (0, 1):
            delta = address - (d.BANNER_BASE & 0xffffff) - bank * d.BANNER_BANK_STRIDE
            if delta not in range(0, 6 * 52, 52): continue
            v.require(op == 0x3e and len(data) == 12 and task['scale'] is not None and bank == task['bank'], 'Unexpected Ready packet/bank')
            v.require(mode is not None and mode[0] in ((d.BURST_MODE_BASE & 0xffffff), (d.BURST_MODE_BASE & 0xffffff) + 12) and
                      mode[1] == 0xe1000006 and mode[2] in (0, 0xe2000000), 'Ready lacks exact preceding mode/window')
            index = delta // 52; g = glyphs[index]
            for vertex, (x, y) in enumerate(((0, 0), (g['width'], 0), (0, g['height']), (g['width'], g['height']))):
                rgb, xy, uv = data[vertex * 3:vertex * 3 + 3]
                color = g['colors'][vertex]
                expected = sum((color[ch] * task['scale'] // 128) << (8 * k) for k, ch in enumerate('rgb'))
                v.require(rgb & 0xffffff == expected, 'Ready scaled corner color differs')
                v.require(struct.unpack('<hh', struct.pack('<I', xy)) == (g['x'] + 128 + x, g['y'] + y), 'Ready XY differs')
                v.require(uv & 0xffff == (g['u'] + x) | (g['v'] + y) << 8, 'Ready UV differs')
            v.require(data[2] >> 16 == 0x3f9a and data[5] >> 16 == 6, 'Ready CLUT/page/material differs')
            selected.append(dict(glyphIndex=index, scale=task['scale'], bank=bank, address=address, mode=list(mode)))
        if op != 0xe1 and mode is not None:
            vertices = 4 if op & 8 else 3
            size = 1 + vertices * (1 + bool(op & 4)) + ((vertices - 1) if op & 16 else 0)
            if not (0x20 <= op <= 0x3f and len(data) == size): mode = None
    v.require([q['glyphIndex'] for q in selected] == (list(reversed(range(6))) if task['scale'] is not None else []), 'Ready reverse submission order differs')
    return dict(draws=selected, dmaNodes=nodes, otag=ot, renderBuffer=current)


def snapshot(state, study, definition, indices, palette, archive, *, probe=False):
    core, ram, task, identity = snapshot_parts(state, study, definition, indices, palette, archive, probe=probe)
    cpu = p.subsystem_records(core, b'CPU'); ra = struct.unpack('<32I', cpu['GPR'])[31]
    context = h.context(core, ram, task, first_vsync_wait=ra == 0x8001dc40)
    context.update(event=identity, epoch=study['observations'][0]['state']['sha256'])
    return context


def snapshot_parts(state, study, definition, indices, palette, archive, *, probe=False):
    """Verify common Ready identity/assets/queued work; this alone never binds a displayed frame."""
    ram, lengths = event.read_ram(state); core = p.unpack_state(state)
    for address, size, digest in h.CODE + EXTRA_CODE:
        at = address & 0x1fffffff
        v.require(v.sha(ram[at:at + size]) == digest, 'Resident frame/thread code differs')
    for item in study['code'] + study['assets']['ranges']:
        at = item['address'] & 0x1fffffff
        v.require(v.sha(ram[at:at + item['bytes']]) == item['sha256'], 'Resident Ready code/table differs')
    identity = event_identity(ram, archive, lengths)
    task = threads(ram, study['observations'][0]['sourceFrame'])
    gpu = p.gpu_records(core); vram = gpu['&GPURAM[0][0]']
    v.require(vram[PALETTE_OFFSET:PALETTE_OFFSET + 32] == (bytes(32) if probe else palette), 'Ready palette overwritten')
    for g in definition['glyphs']:
        for y in range(g['v'], g['v'] + g['height']):
            for x in range(g['u'], g['u'] + g['width']):
                v.require((vram[(y * 1024 + 384) * 2 + x // 2] >> (4 * (x & 1))) & 15 == indices[y * 256 + x], 'Resident Ready texture differs')
    for key in ('tww', 'twh', 'twx', 'twy', 'MaskSetOR', 'MaskEvalAND', 'TexDisable'):
        v.require(not any(gpu[key]), 'Unsupported Ready GPU state: ' + key)
    task.update(packets(ram, task, definition['glyphs']))
    return core, ram, task, identity


def bind(current, queued):
    v.require(current['epoch'] == queued['epoch'], 'Independent capture histories cannot bind')
    return h.bind_display(current, queued)


def footprint(glyphs, indices, palette, visible=True):
    groups = dict(opaque=set(), stp=set(), transparent=set())
    if visible:
        for g in glyphs:
            for y in range(g['height']):
                for x in range(g['width']):
                    word = struct.unpack_from('<H', palette, indices[(g['v'] + y) * 256 + g['u'] + x] * 2)[0]
                    group = 'transparent' if word == 0 else 'stp' if word & 0x8000 else 'opaque'
                    groups[group].add((g['y'] + y) * 256 + g['x'] + x)
    return groups


def compare(original, background, native, groups, original_scanout, probe_scanout, isolation):
    for pixels in (original, background, native, original_scanout, probe_scanout):
        v.require(len(pixels) == bg.CANVAS_BYTES and all(pixels[at + 3] == 255 and
                  not ((pixels[at] | pixels[at + 1] | pixels[at + 2]) & 7) for at in range(0, len(pixels), 4)),
                  'Ready comparison requires complete opaque zero-padded RGB5 canvases')
    canvas = set(range(256 * 240)); touched = groups['opaque'] | groups['stp']
    checks = {name: h.metrics(original, native, points) for name, points in dict(fullCanvas=canvas, **groups).items()}
    checks.update(outsideProbeFootprint=h.metrics(original, background, canvas - touched),
                  originalPngScanout=h.metrics(original, original_scanout, canvas), probePngScanout=h.metrics(background, probe_scanout, canvas))
    failures = [name for name, check in checks.items() if check.get('compared') and not check['exactEquality']]
    if not isolation['nonGpuSubsystemsUnchanged'] or not isolation['gpuRegistersUnchanged']: failures.append('probe-isolation')
    return dict(passed=not failures, failures=failures, checks=checks, visibleArtworkCompared=bool(touched),
                stpChangesBackground=h.metrics(background, original, groups['stp']), toleranceRgb=[0, 0, 0])


def prepared(root, study):
    manifest = v.parse((root / 'assets.manifest.json').read_bytes())
    for item in manifest['assets']:
        v.require(item['relativePath'] in ('artwork.json', 'indices.bin', 'palette-2.bin'), 'Unexpected prepared file')
        data = (root / item['relativePath']).read_bytes()
        v.require(len(data) == item['byteLength'] and v.sha(data) == item['sha256'], 'Prepared manifest mismatch')
    definition = v.parse((root / 'artwork.json').read_bytes()); indices = (root / 'indices.bin').read_bytes(); palette = (root / 'palette-2.bin').read_bytes()
    v.require(len(indices) == 51200 and max(indices) <= 15 and v.sha(palette) == study['assets']['paletteSha256'], 'Prepared image/palette mismatch')
    v.require(len(definition['glyphs']) == len(study['assets']['pieces']) == 6, 'Expected six Ready glyphs')
    for g, s in zip(definition['glyphs'], study['assets']['pieces'], strict=True):
        v.require([g[k] for k in ('u', 'v', 'width', 'height')] == s['uv'] and [g[k] for k in ('x', 'y', 'width', 'height')] == s['screen'] and
                  [[color[ch] for ch in 'rgb'] for color in g['colors']] == s['colors'], 'Prepared glyph does not match original selection')
    return definition, indices, palette


def run(output):
    from PIL import Image
    output = profile_tool.scratch_path(output); v.require(not output.exists(), 'Choose a fresh output')
    evidence = v.parse((ROOT / 'docs/evidence/events/ready-display-source-v1.json').read_bytes())
    study = json.loads(h.verified(evidence['reports'][0])); source = h.verified(study['observations'][0]['state'])
    reference = ROOT.parent / 'fft_decomp'
    def reference_identity():
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        clean = not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        v.require(revision == study['referenceRevision'] == b.REVISION and clean, 'Reference revision/tree changed')
    reference_identity()
    for item in study['inputs']: h.verified(item)
    battle_path = ROOT / 'scratch/all-data-extraction/BATTLE.BIN'
    v.require(battle_path.stat().st_size == 1397096 and profile_tool.file_hash(battle_path) == event.BATTLE_SHA, 'BATTLE identity changed')
    archive = (ROOT / 'scratch/all-data-extraction/EVENT/TEST.EVT').read_bytes()
    art = ROOT / 'scratch/ready-086-artwork-a'; definition, indices, palette = prepared(art, study)
    birth = snapshot(source, study, definition, indices, palette, archive)
    v.require(birth['task']['logicalTick'] == 0, 'Entry is not Ready birth')
    probe, intervention = bg.replace_palette_state(source, palette, PALETTE_OFFSET)
    v.require(snapshot(probe, study, definition, indices, palette, archive, probe=True) == birth, 'Entry probe changes context')
    profile_root = ROOT / 'scratch/retroarch-reference-033-b'; profile_path = profile_root / 'profile.json'
    profile = v.parse(profile_path.read_bytes())
    for item in profile['inputs']: v.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Emulator/content identity changed')
    for key, name in (('executable', 'retroarch.exe'), ('coreLibrary', 'mednafen_psx_libretro.dll'), ('cue', 'fft-clean-base-game.cue')):
        matches = [i['path'] for i in profile['inputs'] if Path(i['path']).name.lower() == name]
        v.require(len(matches) == 1, 'Ambiguous runtime path'); profile[key] = matches[0]
    for name, digest in profile['generatedSha256'].items(): v.require(profile_tool.file_hash(profile_root / name) == digest, 'Prepared profile changed')
    v.require(profile_tool.file_hash(profile_root / 'system/scph5501.bin') == profile_tool.BIOS_HASH, 'BIOS changed')
    normal = {i['path']: profile_tool.file_hash(Path(i['path'])) for i in profile['normalConfigIdentities']}
    tools = [c.identity(Path(m.__file__)) for m in (sys.modules[__name__], p, c, bg, h, b, v, event, profile_tool, d)]
    tools += [c.identity(ROOT / path) for path in ('tools/banner-blend-check/ReadySequence.cs', 'tools/banner-blend-check/Program.cs',
        'tools/banner-blend-check/BannerBlendCheck.csproj', 'tools/banner-blend-check/packages.lock.json',
        'src/Fft.Client/Presentation/ReadyPresentation.cs', 'src/Zodiac.Framework/Events/ReadyDisplayState.cs')]
    assemblies = [c.identity(ROOT / 'tools/banner-blend-check/bin/Release/net10.0' / (name + '.dll'))
                  for name in ('BannerBlendCheck', 'Fft.Client', 'Zodiac.Content', 'Zodiac.Framework')]
    output.mkdir()
    plan = dict(format='fft.ready-displayed-finite-plan', schemaVersion=1, referenceRevision=b.REVISION,
        entryState=study['observations'][0]['state'], profile=c.identity(profile_path), originalBudgets=ORIGINAL, tools=tools, assemblies=assemblies,
        probeBudgets=CURRENT, displayedTicks=TICKS, intervention=intervention, controllersDisconnected=True, muted=True,
        crop=[12, 0, 256, 240], processTimeoutSeconds=55, sourceStatePreserved=True,
        binding='Exact pre-submission stack/frame counter, same restoration epoch and polarity, two-earlier submitted back buffer',
        missingObservations='Displayed birth/next tick: frontend budgets 6/8 stop in interrupt handling, outside the closed VSync classifier; selection-c also rejects budget 7. No inferred pixels or relaxed stack check.',
        scope='Six Ready scanouts; palette-transparent control is altered original state. No hardware, input timing, map renderer or full event equivalence.')
    # This closed finite plan is retained before any emulator process is launched.
    (output / 'plan.json').write_text(json.dumps(plan, indent=2) + '\n')
    (output / 'probe.state').write_bytes(probe)
    originals = {0: (source, None, birth, study['observations'][0]['state'])}; probes = {}
    try:
        for kind, budgets, entry, target in (('original', ORIGINAL, source, originals), ('probe', CURRENT, probe, probes)):
            for count in budgets:
                state, pixels, receipt = bg.capture(profile, profile_root, output / f'{kind}-{count:03}', entry, count)
                context = snapshot(state, study, definition, indices, palette, archive, probe=kind == 'probe')
                target[count] = (state, pixels, context, receipt)
                print(f'{kind} {count}: native={context["frameCounter"]} queued={context["task"]["logicalTick"]}', flush=True)
        inputs = []; bindings = []; isolations = []
        for tick, count in zip(TICKS, CURRENT, strict=True):
            original, control = originals[count], probes[count]
            v.require(original[2] == control[2], 'Probe changes Ready/event/frame context')
            binding = bind(original[2], originals[0 if count == 6 else count - 4][2])
            v.require(binding['displayedLogicalTick'] == tick, 'Unexpected displayed anchor')
            isolations.append(bg.isolation(original[0], control[0])); bindings.append(binding)
            path = output / f'background-{tick:03}.rgba'; path.write_bytes(control[1])
            inputs.append(dict(logicalTick=tick, backgroundPath=str(path), backgroundSha256=v.sha(control[1])))
        native_plan = dict(format='fft.ready-backgrounds', schemaVersion=1, eventRoot=str(ROOT / 'scratch/ready-086-event-60-raw'), checkpoints=inputs)
        (output / 'backgrounds.json').write_text(json.dumps(native_plan, indent=2) + '\n')
        args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
                '--', '--ready-sequence', str(art), str(output / 'backgrounds.json'), str(output / 'native')]
        (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n')
        result = subprocess.run(args, cwd=ROOT, capture_output=True, timeout=55)
        (output / 'native-stdout.txt').write_bytes(result.stdout); (output / 'native-stderr.txt').write_bytes(result.stderr)
        v.require(result.returncode == 0, 'C# Ready composition failed; inspect retained logs')
        receipt = v.parse((output / 'native/check.json').read_bytes())
        v.require(receipt['format'] == 'fft.ready-background-cpu-check' and receipt['policy'] == 'ready-glyph-rgb5-v1' and
                  receipt['renderer'] == 'ReadyPresentation.ComposeOver' and receipt['originalComparison'] == 'pending' and
                  receipt['gpuReadbackPerformed'] is False and len(receipt['checkpoints']) == len(TICKS), 'Wrong C# receipt')
        checkpoints = []; cards = []
        for i, (tick, count, entry) in enumerate(zip(TICKS, CURRENT, receipt['checkpoints'], strict=True)):
            original, control = originals[count], probes[count]
            name = f'tick-{tick:03}.rgba'; native = (output / 'native' / name).read_bytes()
            scale = originals[0 if count == 6 else count - 4][2]['task']['scale']
            v.require(entry['logicalTick'] == tick and entry['file'] == name and entry['drawScale'] == scale and
                      entry['backgroundSha256'] == v.sha(control[1]) and entry['rgbaSha256'] == v.sha(native) and len(native) == bg.CANVAS_BYTES and
                      [q['GlyphIndex'] for q in entry['draws']] == (list(reversed(range(6))) if scale is not None else []), 'C# snapshot/identity differs')
            groups = footprint(definition['glyphs'], indices, palette, scale is not None)
            comparison = compare(original[1], control[1], native, groups, h.scanout(original[0]), h.scanout(control[0]), isolations[i])
            checkpoints.append(dict(logicalTick=tick, scale=scale, displayed=bindings[i], currentContext=original[2],
                queuedContext=originals[0 if count == 6 else count - 4][2], originalCapture=original[3], probeCapture=control[3],
                sourceSubmission=originals[0 if count == 6 else count - 4][3], stateIsolation=isolations[i], **comparison))
            images = []
            for label, pixels in (('original', original[1]), ('background', control[1]), ('csharp', native)):
                file = f'{tick:03}-{label}.png'; Image.frombytes('RGBA', (256, 240), pixels).save(output / file)
                images.append(f'<div>{label}<br><img src="{file}" width="512" height="480"></div>')
            cards.append(f'<h2>Ready tick {tick}, scale {scale}: exact={comparison["passed"]}</h2><section>' + ''.join(images) + '</section>')
        report = dict(format='fft.ready-displayed-comparison', schemaVersion=1, referenceRevision=b.REVISION,
            passed=all(x['passed'] for x in checkpoints), plan=c.identity(output / 'plan.json'), study=evidence['reports'][0],
            preparedInputs=[c.identity(art / name) for name in ('assets.manifest.json', 'artwork.json', 'indices.bin', 'palette-2.bin')],
            checkpoints=checkpoints, tools=tools, assemblies=assemblies, battle=c.identity(battle_path), nativeReceipt=c.identity(output / 'native/check.json'),
            newOriginalCaptures=True, controlledPaletteProbe=True, gpuReadbackPerformed=False, originalTimingEstablished=False,
            hardwareEquivalenceEstablished=False, fullEventPlaybackEstablished=False, ownerReview='pending')
        review = '<!doctype html><meta charset="utf-8"><title>Ready original comparison</title><style>body{background:#20242a;color:#eee;font:16px sans-serif;margin:24px}section{display:flex;gap:16px;flex-wrap:wrap}img{image-rendering:pixelated;border:1px solid #666}</style><h1>Ready: original / palette-transparent control / C#</h1><p>Native 256×240 crops at 2×. Exact zero-tolerance RGBA comparison. Background is inherited from an altered state copy; map rendering, hardware, input timing and full event playback remain separate.</p>' + ''.join(cards)
    finally:
        for path, digest in normal.items(): v.require(profile_tool.file_hash(Path(path)) == digest, 'Installed configuration changed')
        for name, digest in profile['generatedSha256'].items(): v.require(profile_tool.file_hash(profile_root / name) == digest, 'Prepared profile changed')
        for item in profile['inputs']: v.require(profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Original runtime/input changed')
        for item in tools + assemblies: v.require(profile_tool.file_hash(ROOT / item['localPath']) == item['sha256'], 'Comparison tool/assembly changed')
        reference_identity()
        v.require(h.verified(study['observations'][0]['state']) == source, 'Archived source changed')
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    (output / 'review.html').write_text(review, encoding='utf-8')
    print(f'Six Ready comparisons passed={report["passed"]}', flush=True)
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try: sys.exit(run(args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, zlib.error, subprocess.SubprocessError) as error:
        parser.exit(2, 'Ready comparison failed (diagnostics retained): ' + str(error) + '\n')
