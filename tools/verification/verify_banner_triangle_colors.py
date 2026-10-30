"""Replay actual C# composition against hash-bound original reveal captures; no emulator launch."""
import argparse
import json
from pathlib import Path
import subprocess
import sys

import compare_banner_reveal as r


def archive():
    path = r.profile_tool.ROOT / 'docs/evidence/visual/early-banner-reveal-v1.json'
    r.v.require(path.stat().st_size <= 65536, 'Evidence exceeds budget')
    metadata = r.v.parse(path.read_bytes())
    r.v.require(metadata['format'] == 'fft.early-banner-reveal-evidence' and metadata['schemaVersion'] == 1 and
                metadata['referenceRevision'] == r.b.REVISION, 'Unexpected historical reveal evidence')
    report = json.loads(r.hide.verified(metadata['repeatValidation']['firstReport']))
    r.v.require(report['format'] == 'fft.banner-reveal-comparison' and report['schemaVersion'] == 1 and
                report['referenceRevision'] == r.b.REVISION and report['passed'] is False, 'Expected retained difference report')
    audit = json.loads(r.hide.verified(metadata['repeatValidation']['artifactAudit']))
    r.v.require(audit['firstRoot'] == metadata['repeatValidation']['firstRoot'] and
                audit['secondRoot'] == metadata['repeatValidation']['secondRoot'] and audit['byteIdentical'] is True and
                len(audit['artifacts']) == metadata['repeatValidation']['artifactCount'] == 107, 'Unexpected archive audit')
    records = {item['localPath']: item for item in audit['artifacts']}
    r.v.require(len(records) == 107, 'Duplicate archive bindings')
    for item in records.values(): r.hide.verified(item)
    return path, metadata, report, records


def run(plan_path, output):
    from PIL import Image
    plan_path, output = map(r.profile_tool.scratch_path, (plan_path, output))
    r.v.require(not output.exists(), 'Output already exists; choose a fresh scratch root')
    r.v.require(plan_path.stat().st_size <= 65536, 'Plan exceeds budget')
    plan = r.v.parse(plan_path.read_bytes()); inputs, _, _, definition, _, _ = r.b.load(plan)
    evidence_path, metadata, historical, records = archive()
    r.v.require(r.c.identity(plan_path) == metadata['sourcePlan'], 'Historical comparison plan changed')
    root = r.profile_tool.ROOT
    def read(path):
        key = path.relative_to(root).as_posix(); r.v.require(key in records, 'Unbound archived artifact')
        return r.hide.verified(records[key])
    tools = [r.c.identity(Path(module.__file__)) for module in (sys.modules[__name__], r, r.p, r.bg, r.hide, r.c, r.trace, r.b, r.v)]
    tools += [r.c.identity(root / name) for name in ('src/Fft.Client/Presentation/FiveBitGeneralBannerPresentation.cs', 'tools/banner-blend-check/Program.cs')]
    pinned = [r.c.identity(evidence_path), r.c.identity(plan_path)]
    original_root = root / metadata['repeatValidation']['firstRoot']; anchors = []
    r.v.require([(x['frontendFrames'], x['displayedLogicalTick']) for x in historical['checkpoints']] == list(r.SCHEDULE), 'Historical schedule differs')
    for entry in historical['checkpoints']:
        frames, tick = entry['frontendFrames'], entry['displayedLogicalTick']; folder = original_root / f'frame-{frames:03}'
        state = r.hide.verified(entry['originalCapture']['exitState']); current = r.snapshot(state, definition)
        queued_state = r.hide.verified(entry['queuedSource']); queued = r.snapshot(queued_state, definition)
        binding = r.hide.bind_display(current, queued)
        r.v.require(current == entry['sourceContext'] and binding == entry['binding'] and binding['displayedLogicalTick'] == tick, 'Archived source history differs')
        drawn = r.trace.trace(queued_state, definition, binding['displayedGeometry'])
        r.v.require(drawn == entry['drawTrace'], 'Archived draw state differs')
        probe_state = r.hide.verified(entry['probeCapture']['exitState'])
        r.v.require(r.snapshot(probe_state, definition) == current, 'Archived probe context differs')
        isolated = r.bg.isolation(state, probe_state); r.v.require(isolated == entry['stateIsolation'], 'Archived isolation differs')
        probe_gpu = r.p.gpu_records(r.p.unpack_state(probe_state))
        r.v.require(probe_gpu['&GPURAM[0][0]'][r.bg.PALETTE_OFFSET:r.bg.PALETTE_OFFSET + 32] == bytes(32), 'Probe palette differs')
        original_png, background_png = read(folder / 'original.png'), read(folder / 'background.png')
        original, background = r.b.image(original_png, (256, 240)), read(folder / 'background.rgba')
        r.v.require(r.b.image(background_png, (256, 240)) == background, 'Archived background PNG/raw mismatch')
        anchors.append(dict(tick=tick, frames=frames, state=state, probeState=probe_state, original=original, background=background,
            originalPng=original_png, backgroundPng=background_png, binding=binding, isolation=isolated,
            previousNative=read(original_root / f'native/tick-{tick:03}.rgba'), previousComparison=entry['comparison']))
    output.mkdir(parents=True)
    for item in anchors:
        folder = output / f'frame-{item["frames"]:03}'; folder.mkdir()
        (folder / 'original.png').write_bytes(item['originalPng']); (folder / 'background.png').write_bytes(item['backgroundPng'])
        (folder / 'background.rgba').write_bytes(item['background'])
    native_plan = dict(format='fft.banner-reveal-backgrounds', schemaVersion=1, checkpoints=[dict(logicalTick=x['tick'],
        backgroundPath=str(output / f'frame-{x["frames"]:03}/background.rgba'), backgroundSha256=r.v.sha(x['background'])) for x in anchors])
    (output / 'backgrounds.json').write_text(json.dumps(native_plan, indent=2) + '\n')
    prepared = r.v.scratch_file(plan['prepared']['definition']['path']).parent
    args = ['dotnet', 'run', '--project', 'tools/banner-blend-check/BannerBlendCheck.csproj', '-c', 'Release', '--no-build', '--no-restore',
        '--', '--reveal-sequence', str(prepared), str(output / 'backgrounds.json'), str(output / 'native')]
    (output / 'native-command.json').write_text(json.dumps(args, indent=2) + '\n')
    process = subprocess.run(args, cwd=root, capture_output=True, timeout=55)
    (output / 'native-stdout.txt').write_bytes(process.stdout); (output / 'native-stderr.txt').write_bytes(process.stderr)
    r.v.require(process.returncode == 0, 'Actual C# host failed; build Release first')
    receipt, native = r.native_frames(output / 'native', {x['tick']: x['background'] for x in anchors},
        {x['tick']: x['binding']['displayedGeometry'] for x in anchors})
    results = []
    for item in anchors:
        tick, frames = item['tick'], item['frames']
        comparison = r.hide.compare(item['original'], item['background'], native[tick], r.hide.scanout(item['state']), r.hide.scanout(item['probeState']),
            definition, r.padded_geometry(item['binding']['displayedGeometry']), inputs['prepared']['indices'], inputs['prepared']['palette'], item['isolation'], partial_triplet=True)
        old_delta = r.b.samples(native[tick], item['previousNative'], set(range(256 * 240)))
        results.append(dict(displayedLogicalTick=tick, frontendFrames=frames, binding=item['binding'], comparison=comparison,
            previousComparisonPassed=item['previousComparison']['passed'], correctedMinusPrevious=old_delta))
        Image.frombytes('RGBA', (256, 240), native[tick]).save(output / f'frame-{frames:03}/native.png')
        print(f'Reveal {tick}: exact={comparison["passed"]}; changed from previous={old_delta["changedPixels"]}', flush=True)
    for identity in tools + pinned:
        r.v.require(r.profile_tool.file_hash(root / identity['localPath']) == identity['sha256'], 'Tool/evidence changed during verification')
    report = dict(format='fft.banner-triangle-color-comparison', schemaVersion=1, referenceRevision=r.b.REVISION,
        historicalEvidence=r.c.identity(evidence_path), tools=tools, pinned=pinned, checkpoints=results,
        nativeReceipt=r.c.identity(output / 'native/check.json'), passed=all(x['comparison']['passed'] for x in results),
        originalCapturesReused=True, emulatorLaunched=False, originalFidelityEstablished=False)
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n'); (output / 'review.html').write_text(r.review(), encoding='utf-8')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        report = run(args.plan, args.output)
        sys.exit(0 if report['passed'] else 1)
    except (ValueError, OSError, KeyError, TypeError, subprocess.SubprocessError) as error:
        parser.exit(2, 'Triangle color verification failed (diagnostics retained): ' + str(error) + '\n')
