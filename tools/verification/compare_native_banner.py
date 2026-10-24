"""Compare actual five-bit Client captures against the frozen original glyph subset."""
import argparse
import json
from pathlib import Path
import sys
import uuid
import zlib

import analyze_banner_precision as p
import compare_observed_banner as b
import compare_visual_checkpoints as v
import trace_banner_draw_state as trace


def capture(root):
    path = v.scratch_file(root)
    def record(name, budget):
        file = path / name
        value = dict(path=file.relative_to(v.ROOT).as_posix(), sha256='0' * 64)
        file = v.scratch_file(value['path']); v.require(file.stat().st_size <= budget, 'Native capture exceeds budget')
        data = file.read_bytes(); v.require(len(data) <= budget, 'Native capture exceeds budget')
        value['sha256'] = v.sha(data)
        return value, data
    receipt_identity, raw = record('check.json', 65536); receipt = v.parse(raw)
    v.fields(receipt, 'format schemaVersion precision policy ditherEnabled framebufferOrigin referenceRevision completionTick phase captures originalComparison rendererTaskAcknowledgments', 'native receipt')
    v.require(receipt['format'] == 'fft.general-banner-native-capture' and type(receipt['schemaVersion']) is int and
              receipt['schemaVersion'] == 1 and receipt['precision'] == 'five-bit' and type(receipt['ditherEnabled']) is bool,
              'Unsupported native capture profile')
    v.text(receipt['policy'], 'native rendering policy')
    v.require(receipt['framebufferOrigin'] == [0, 0] and all(type(x) is int for x in receipt['framebufferOrigin']), 'Unsupported preview dither origin')
    v.require(receipt['referenceRevision'] == b.REVISION and type(receipt['completionTick']) is int and receipt['completionTick'] == 91 and
              receipt['phase'] == 'Completed' and type(receipt['rendererTaskAcknowledgments']) is int and receipt['rendererTaskAcknowledgments'] == 0,
              'Expected completed bounded native host check')
    v.require(type(receipt['captures']) is list and len(receipt['captures']) == 15, 'Expected bounded checkpoint schedule')
    ids = set()
    for item in receipt['captures']:
        v.fields(item, 'tick phase file rgbaSha256 pieces drawStates', 'native checkpoint')
        v.integer(item['tick'], 0, 91, 'checkpoint tick'); v.hex_hash(item['rgbaSha256'])
        v.require(item['tick'] not in ids, 'Duplicate native tick'); ids.add(item['tick'])
    v.require(ids == {0, 1, 2, 5, 9, 10, 11, 14, 20, 80, 81, 82, 85, 90, 91}, 'Unexpected native checkpoint schedule')
    checkpoint = next(c for c in receipt['captures'] if c['tick'] == 20)
    expected = [dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)]
    v.require(checkpoint['phase'] == 'Holding' and checkpoint['pieces'] == expected and checkpoint['file'] == 'tick-020.png',
              'Native checkpoint must hold six full-height pieces')
    expected_states = [dict(pieceIndex=i, ditherEnabled=receipt['ditherEnabled'], framebufferX=0, framebufferY=0) for i in range(6)]
    v.require(checkpoint['drawStates'] == expected_states and all(type(x['ditherEnabled']) is bool and
              type(x['pieceIndex']) is int and type(x['framebufferX']) is int and type(x['framebufferY']) is int
              for x in checkpoint['drawStates']), 'Missing/mismatched explicit native draw state')
    png_identity, png = record('tick-020.png', 32 * 1024 * 1024)
    rgba = b.image(png, (256, 240))
    v.require(v.sha(rgba) == checkpoint['rgbaSha256'], 'Native pixels differ from receipt')
    return rgba, receipt, dict(png=png_identity, receipt=receipt_identity)


def compare(original, native, definition, indices, palette, dither):
    _, _, _, historical_mask = b.transfer_bounds(definition, indices, palette)
    low, high = p.native_bounds(definition, indices, palette, dither=dither)
    mask = {at for at in historical_mask if low[at * 4:at * 4 + 3] == high[at * 4:at * 4 + 3]}
    foreground, model = b.samples(original, native, mask), b.samples(low, native, mask)
    regions = []
    for name, pieces in (('first-line', definition['pieces'][:3]), ('second-line', definition['pieces'][3:])):
        x, y = min(q['x'] for q in pieces), min(q['y'] for q in pieces)
        w, h = max(q['x'] + q['width'] for q in pieces) - x, max(q['y'] + q['height'] for q in pieces) - y
        region = dict(id=name, category='palette', rect=[x, y, w, h], maxChannelDelta=[0, 0, 0, 0], maxChangedPixels=0,
            rationale='Raw exact diagnostic with unmatched battle/checker backgrounds; not a full-region fidelity verdict.')
        points = {at for at in mask if x <= at % 256 < x + w and y <= at // 256 < y + h}
        regions.append(dict(id=name, foreground=b.samples(original, native, points), rawRegion=v.region_metrics(original, native, 256, region)))
    return dict(originalComparisonPerformed=True, originalFidelityEstablished=False, timingComparisonPerformed=False,
        simulationCompared=False, nativeDitherEnabled=dither, diagnosticThresholdRgb=[0, 0, 0], fidelityTolerance=None,
        historicalCorePixels=len(historical_mask), excludedNativeBackgroundSensitivePixels=len(historical_mask - mask),
        foreground=foreground, nativeModelAgreement=model, regions=regions,
        passed=foreground['exactEquality'] and model['exactEquality'])


def run(plan_path, native_root, output):
    from PIL import __version__
    v.require(__version__ == '12.3.0', 'Review Pillow changes explicitly')
    plan_file, destination = v.scratch_file(plan_path), v.scratch_file(output)
    v.require(not destination.exists() and plan_file.stat().st_size <= 65536, 'Existing output or oversized plan')
    raw = plan_file.read_bytes(); plan = v.parse(raw)
    inputs, original, _, definition, _, _ = b.load(plan)
    reconstructed = trace.trace(inputs['original']['state'], definition)
    native, receipt, identity = capture(native_root)
    report = compare(original, native, definition, inputs['prepared']['indices'], inputs['prepared']['palette'], receipt['ditherEnabled'])
    report.update(format='fft.native-banner-comparison', schemaVersion=1, plan=plan, planSha256=v.sha(raw), nativeCapture=identity,
        nativeReceipt=receipt, drawStateTrace=reconstructed,
        nativeStateMatchesFrozenTrace=all(x['precedingMode']['ditherEnabled'] == receipt['ditherEnabled'] for x in reconstructed['banners']),
        limits=['Selected full-visible glyph samples only; background-sensitive blending and general rasterization remain unverified.',
                'Original draw state is reconstructed from frozen RAM, not a live GPU command trace.',
                'Original logical tick, reveal/hide timing and event 401 activation remain unverified.'],
        tools=dict(python=sys.version.split()[0], pillow=__version__, **{name: v.sha(Path(module.__file__).read_bytes())
            for name, module in (('precisionStudySha256', p), ('observedComparatorSha256', b), ('sharedSha256', v), ('traceSha256', trace))},
            comparatorSha256=v.sha(Path(__file__).read_bytes())))
    report['failures'] = []
    if not report['nativeStateMatchesFrozenTrace']: report['failures'].append('draw-state-mismatch')
    if not report['foreground']['exactEquality']: report['failures'].append('foreground-color-mismatch')
    if not report['nativeModelAgreement']['exactEquality']: report['failures'].append('native-model-mismatch')
    report['passed'] = not report['failures']
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.native-banner-' + uuid.uuid4().hex); staging.mkdir()
    try:
        (staging / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
        review = b.review_html(dict(report, strictInvariantEquality=report['foreground']['exactEquality'], managedTick=20),
            dict(original=inputs['original'], candidate=dict(png=v.verified_file(identity['png'], 32 * 1024 * 1024))))
        (staging / 'review.html').write_text(review.replace('Managed tick 20', 'Managed five-bit tick 20'), encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists():
            for name in ('report.json', 'review.html'): (staging / name).unlink(missing_ok=True)
            staging.rmdir()
    print(f'Native foreground exact={report["foreground"]["exactEquality"]}; {report["foreground"]["pixels"]} samples; full fidelity unestablished')
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True); parser.add_argument('--native-root', required=True); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try: sys.exit(run(args.plan, args.native_root, args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, RecursionError, zlib.error) as error:
        parser.exit(2, 'Invalid/unavailable native banner comparison: ' + str(error) + '\n')
