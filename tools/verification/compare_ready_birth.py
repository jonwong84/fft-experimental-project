"""Two finite Ready birth scanouts with separately proven BIOS IRQ resume contexts."""
import argparse
import json
from pathlib import Path
import struct
import subprocess
import sys
import zlib

import compare_ready_display as r
import inspect_ready_interrupt as irq

ROOT = r.ROOT
EXE_SHA = '287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562'


def snapshot(state, study, definition, indices, palette, archive, *, probe=False):
    core, ram, task, identity = r.snapshot_parts(state, study, definition, indices, palette, archive, probe=probe)
    context = irq.context(core, ram, task)
    context.update(event=identity, epoch=study['observations'][0]['state']['sha256'])
    return context


def source_inputs(state, bios, executable):
    r.v.require(len(bios) == 524288 and r.v.sha(bios) == r.profile_tool.BIOS_HASH, 'Wrong original BIOS')
    r.v.require(len(executable) == 356352 and r.v.sha(executable) == EXE_SHA and executable[:8] == b'PS-X EXE', 'Wrong original executable')
    ram = r.event.read_ram(state)[0]; irq.verify_code(ram)
    base = struct.unpack_from('<I', executable, 0x18)[0]
    r.v.require(base == 0x80010000, 'Unexpected original executable load address')
    for at, size, digest in irq.SOUND:
        r.v.require(r.v.sha(executable[0x800 + at - base:0x800 + at - base + size]) == digest, 'Executable/IRQ code mismatch')
    for at, size, digest, rom in irq.KERNEL:
        if rom is not None: r.v.require(r.v.sha(bios[rom:rom + size]) == digest, 'BIOS/RAM source mismatch')
    # Retail patches observed in RAM, independently present as templates in the original EXE.
    for destination, template, size in ((0xc80, 0x8001dad0, 56), (0x6444, 0x800288c4, 20)):
        r.v.require(ram[destination:destination + size] == executable[0x800 + template - base:0x800 + template - base + size],
                    'Observed exception patch differs from the original executable template')


def native_frames(root, backgrounds):
    receipt = r.v.parse((root / 'check.json').read_bytes())
    r.v.require(receipt['format'] == 'fft.ready-background-cpu-check' and receipt['policy'] == 'ready-glyph-rgb5-v1' and
                receipt['renderer'] == 'ReadyPresentation.ComposeOver' and receipt['originalComparison'] == 'pending' and
                receipt['gpuReadbackPerformed'] is False and len(receipt['checkpoints']) == 2, 'Wrong Ready birth CPU receipt')
    frames = []
    for tick, entry in enumerate(receipt['checkpoints']):
        file = f'tick-{tick:03}.rgba'; pixels = (root / file).read_bytes(); scale = (48, 56)[tick]
        r.v.require(entry['logicalTick'] == tick and entry['file'] == file and entry['drawScale'] == scale and
                    entry['backgroundSha256'] == r.v.sha(backgrounds[tick]) and entry['rgbaSha256'] == r.v.sha(pixels) and
                    [q['GlyphIndex'] for q in entry['draws']] == [5, 4, 3, 2, 1, 0] and
                    all(q['Scale'] == scale for q in entry['draws']), 'Wrong Ready birth CPU frame/scale/order')
        frames.append(pixels)
    return frames, receipt


def run(output):
    from PIL import Image
    output = r.profile_tool.scratch_path(output); r.v.require(not output.exists(), 'Choose fresh output')
    evidence = r.v.parse((ROOT / 'docs/evidence/events/ready-display-source-v1.json').read_bytes())
    study = json.loads(r.h.verified(evidence['reports'][0])); source = r.h.verified(study['observations'][0]['state'])
    for item in study['inputs']: r.h.verified(item)
    archive = (ROOT / 'scratch/all-data-extraction/EVENT/TEST.EVT').read_bytes()
    art = ROOT / 'scratch/ready-086-artwork-a'; definition, indices, palette = r.prepared(art, study)
    reference = ROOT.parent / 'fft_decomp'
    def reference_identity():
        r.v.require(subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip() == r.b.REVISION and
                    not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(), 'Reference changed')
    reference_identity()
    profile_root = ROOT / 'scratch/retroarch-reference-033-b'; profile_path = profile_root / 'profile.json'
    profile = r.v.parse(profile_path.read_bytes())
    for item in profile['inputs']: r.v.require(r.profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Original input changed')
    for key, name in (('executable', 'retroarch.exe'), ('coreLibrary', 'mednafen_psx_libretro.dll'), ('cue', 'fft-clean-base-game.cue')):
        matches = [i['path'] for i in profile['inputs'] if Path(i['path']).name.lower() == name]
        r.v.require(len(matches) == 1, 'Ambiguous runtime path'); profile[key] = matches[0]
    for name, digest in profile['generatedSha256'].items(): r.v.require(r.profile_tool.file_hash(profile_root / name) == digest, 'Profile changed')
    bios_path = profile_root / 'system/scph5501.bin'; exe_path = ROOT / 'scratch/all-data-extraction/SCUS_942.21'
    bios, executable = bios_path.read_bytes(), exe_path.read_bytes()
    normal = {i['path']: r.profile_tool.file_hash(Path(i['path'])) for i in profile['normalConfigIdentities']}
    # Inspect the archived failures before authorizing any new continuation.
    retained_paths = [ROOT / path for path in ('scratch/ready-displayed-087-preflight-a/original-006/states/fft-clean-base-game.state.auto',
                                               'scratch/ready-087-selection-c/frames-008/states/fft-clean-base-game.state.auto')]
    retained = []
    for path in retained_paths:
        r.v.require(path.stat().st_size <= r.p.MAX_STATE, 'Retained state exceeds budget')
        state = path.read_bytes(); source_inputs(state, bios, executable)
        retained.append(dict(state=r.c.identity(path), context=snapshot(state, study, definition, indices, palette, archive)))
    birth = r.snapshot(source, study, definition, indices, palette, archive)
    probe, intervention = r.bg.replace_palette_state(source, palette, r.PALETTE_OFFSET)
    tools = [r.c.identity(Path(m.__file__)) for m in (sys.modules[__name__], irq, r, r.h, r.p, r.c, r.bg, r.b, r.v, r.event, r.profile_tool, r.d)]
    tools += [r.c.identity(ROOT / path) for path in ('tools/banner-blend-check/ReadySequence.cs', 'tools/banner-blend-check/Program.cs',
        'tools/banner-blend-check/BannerBlendCheck.csproj', 'tools/banner-blend-check/packages.lock.json', 'src/Fft.Client/Presentation/ReadyPresentation.cs')]
    assemblies = [r.c.identity(ROOT / 'tools/banner-blend-check/bin/Release/net10.0' / (name + '.dll'))
                  for name in ('BannerBlendCheck', 'Fft.Client', 'Zodiac.Content', 'Zodiac.Framework')]
    pinned = [r.c.identity(bios_path), r.c.identity(exe_path), r.c.identity(profile_path)]
    output.mkdir()
    plan = dict(format='fft.ready-birth-finite-plan', schemaVersion=1, referenceRevision=r.b.REVISION,
        entryState=study['observations'][0]['state'], originalBudgets=[4, 6, 8], probeBudgets=[6, 8], displayedTicks=[0, 1],
        retained=retained, inputs=pinned, tools=tools, assemblies=assemblies, intervention=intervention,
        controllersDisconnected=True, muted=True, processTimeoutSeconds=55, sourceStatesPreserved=True, crop=[12, 0, 256, 240],
        binding='Closed timer-2 callback/BIOS PCB-TCB save/restore -> main VSync stack -> same-epoch two-earlier submitted framebuffer',
        limits='Only two observed IRQ sites; no arbitrary interrupts, hardware, Circle/display timing, audio fidelity or full event playback.')
    (output / 'plan.json').write_text(json.dumps(plan, indent=2) + '\n')
    (output / 'probe.state').write_bytes(probe)
    originals = {0: (source, None, birth, study['observations'][0]['state'])}; probes = {}
    try:
        for kind, counts, initial, target in (('original', (4, 6, 8), source, originals), ('probe', (6, 8), probe, probes)):
            for count in counts:
                state, pixels, receipt = r.bg.capture(profile, profile_root, output / f'{kind}-{count:03}', initial, count)
                observed = (r.snapshot if count == 4 else snapshot)(state, study, definition, indices, palette, archive, probe=kind == 'probe')
                if kind == 'original' and count in (6, 8):
                    archived = retained_paths[(count - 6) // 2].read_bytes()
                    r.v.require(state == archived, 'Repeated original interrupt endpoint differs from retained state')
                target[count] = (state, pixels, observed, receipt)
                print(f'{kind} {count}: frame={observed["frameCounter"]} queued={observed["task"]["logicalTick"]}', flush=True)
        backgrounds = [probes[n][1] for n in (6, 8)]; inputs = []
        for tick, pixels in enumerate(backgrounds):
            path = output / f'background-{tick:03}.rgba'; path.write_bytes(pixels)
            inputs.append(dict(logicalTick=tick, backgroundPath=str(path), backgroundSha256=r.v.sha(pixels)))
        (output / 'backgrounds.json').write_text(json.dumps(dict(format='fft.ready-birth-backgrounds', schemaVersion=1,
            eventRoot=str(ROOT / 'scratch/ready-086-event-60-raw'), checkpoints=inputs), indent=2) + '\n')
        args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
                '--', '--ready-birth-sequence', str(art), str(output / 'backgrounds.json'), str(output / 'native')]
        (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n')
        result = subprocess.run(args, cwd=ROOT, capture_output=True, timeout=55)
        (output / 'native-stdout.txt').write_bytes(result.stdout); (output / 'native-stderr.txt').write_bytes(result.stderr)
        r.v.require(result.returncode == 0, 'C# Ready birth composition failed; inspect logs')
        frames, receipt = native_frames(output / 'native', backgrounds); checkpoints = []; cards = []
        for tick, count, pixels in zip((0, 1), (6, 8), frames, strict=True):
            original, control, queued = originals[count], probes[count], originals[0 if tick == 0 else 4]
            r.v.require(original[2] == control[2], 'Control changes IRQ/event/frame context')
            binding = r.bind(original[2], queued[2]); r.v.require(binding['displayedLogicalTick'] == tick, 'Wrong displayed birth anchor')
            binding['method'] = plan['binding']
            isolated = r.bg.isolation(original[0], control[0])
            comparison = r.compare(original[1], control[1], pixels, r.footprint(definition['glyphs'], indices, palette),
                                   r.h.scanout(original[0]), r.h.scanout(control[0]), isolated)
            checkpoints.append(dict(logicalTick=tick, scale=(48, 56)[tick], displayed=binding, currentContext=original[2],
                queuedContext=queued[2], originalCapture=original[3], probeCapture=control[3], sourceSubmission=queued[3], stateIsolation=isolated, **comparison))
            row = []
            for label, rgba in (('original', original[1]), ('background', control[1]), ('csharp', pixels)):
                file = f'{tick:03}-{label}.png'; Image.frombytes('RGBA', (256, 240), rgba).save(output / file)
                row.append(f'<div>{label}<br><img width="512" height="480" src="{file}"></div>')
            cards.append(f'<h2>Displayed Ready tick {tick}, scale {(48,56)[tick]}: exact={comparison["passed"]}</h2><section>' + ''.join(row) + '</section>')
        report = dict(format='fft.ready-birth-comparison', schemaVersion=1, referenceRevision=r.b.REVISION,
            passed=all(c['passed'] for c in checkpoints), plan=r.c.identity(output / 'plan.json'), checkpoints=checkpoints,
            tools=tools, assemblies=assemblies, originalRetainedStatesRepeated=True, nativeReceipt=r.c.identity(output / 'native/check.json'),
            ownerReview='pending', timingComparisonPerformed=False, hardwareEquivalenceEstablished=False, audioFidelityEstablished=False)
        review = '<!doctype html><meta charset="utf-8"><title>Ready birth comparison</title><style>body{background:#20242a;color:#eee;font:16px sans-serif;margin:24px}section{display:flex;gap:16px;flex-wrap:wrap}img{image-rendering:pixelated;border:1px solid #666}</style><h1>Ready birth: original / controlled background / C#</h1><p>Two original interrupt endpoints bind through verified BIOS saved main VSync context. Native 256×240 at 2×; exact comparison. Map rendering, hardware, audio and input/display latency remain separate.</p>' + ''.join(cards)
    finally:
        for path, digest in normal.items(): r.v.require(r.profile_tool.file_hash(Path(path)) == digest, 'Normal configuration changed')
        for name, digest in profile['generatedSha256'].items(): r.v.require(r.profile_tool.file_hash(profile_root / name) == digest, 'Prepared profile changed')
        for item in profile['inputs']: r.v.require(r.profile_tool.file_hash(Path(item['path'])) == item['sha256'], 'Original input changed')
        for item in tools + assemblies + pinned + [x['state'] for x in retained]:
            path = ROOT / item['localPath']
            r.v.require(path.stat().st_size == item['bytes'] and r.profile_tool.file_hash(path) == item['sha256'], 'Pinned tool/input changed')
        r.v.require(r.h.verified(study['observations'][0]['state']) == source, 'Original source state changed')
        reference_identity()
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    (output / 'review.html').write_text(review, encoding='utf-8')
    print(f'Ready birth/next-frame exact={report["passed"]}', flush=True)
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--output', type=Path, required=True); args = parser.parse_args()
    try: sys.exit(run(args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, zlib.error, subprocess.SubprocessError) as error:
        parser.exit(2, 'Ready birth comparison failed (diagnostics retained): ' + str(error) + '\n')
