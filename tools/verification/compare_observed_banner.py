"""Bounded original/managed full-visible banner diagnostics, not a fidelity verdict.

Uses the existing developer Pillow environment. Original media and reports stay in
scratch. The synthetic-only visual-capture contract remains unchanged.
"""
import argparse
import base64
import io
import json
from pathlib import Path
import struct
import sys
import uuid

import compare_visual_checkpoints as v

REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'
ARTIFACT_PINS = {
    'definition': ('e1c61e66de5ca10f84743f286ec8fb7f34c0bdd14e41b5eb0cab704a629ffdac', 4071),
    'indices': ('515778839288e154c76e51db57a44e82c63a8e27c079bf3a1803136e389191dd', 51200),
    'palette': ('8aa38621ccfcd6386e23466a803999ef5ed44a1afc415f447a9fcc0054027739', 32),
}
WIDTH, HEIGHT = 256, 240


def image(data, size):
    from PIL import Image
    v.require(data[:8] == b'\x89PNG\r\n\x1a\n' and len(data) >= 33 and data[24] == 8 and data[25] in (2, 6),
              'Expected native RGB8/RGBA8 PNG')
    v.require(data[12:16] == b'IHDR' and (int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')) == size,
              'Unexpected original/managed PNG dimensions')
    with Image.open(io.BytesIO(data)) as im:
        v.require(im.format == 'PNG' and im.size == size and im.mode in ('RGB', 'RGBA') and not getattr(im, 'is_animated', False),
                  'Unsupported image; no resampling/color correction allowed')
        rgba = im.convert('RGBA').tobytes()
    v.require(all(rgba[i] == 255 for i in range(3, len(rgba), 4)), 'Expected opaque capture')
    return rgba


def align(original, candidate, original_rect, candidate_rect):
    # This profile has one explicit measured mapping, never an alignment search.
    v.require(original_rect == [12, 0, 256, 240] and candidate_rect == [0, 0, 256, 240], 'Unsupported alignment')
    v.require(len(original) == 280 * 240 * 4 and len(candidate) == 256 * 240 * 4, 'Invalid pixel buffers')
    for y in range(240):
        for x in (*range(12), *range(268, 280)):
            v.require(original[(y * 280 + x) * 4:(y * 280 + x) * 4 + 3] == b'\0\0\0', 'Declared black border contains image content')
    return b''.join(original[(y * 280 + 12) * 4:(y * 280 + 268) * 4] for y in range(240)), candidate


def transfer_bounds(definition, indices, palette):
    """Managed-policy sensitivity mask, not an independent original renderer.

    Add/subtract with saturation and opaque replacement are monotone. Evaluating
    black/white backgrounds bounds every channel for all possible backgrounds.
    Coincident endpoints select only managed-background-invariant pixels.
    """
    v.require(len(indices) == 51200 and len(palette) == 32 and all(i < 16 for i in indices), 'Invalid prepared payload')
    words = struct.unpack('<16H', palette)
    low, high = bytearray(WIDTH * HEIGHT * 3), bytearray([255]) * (WIDTH * HEIGHT * 3)
    touched = set()
    pieces = definition['pieces']
    v.require(len(pieces) == 6 and [p['sourceIndex'] for p in pieces] == list(range(6)), 'Expected six ordered pieces')
    for p in pieces:
        for name in ('x', 'y', 'u', 'v', 'width', 'height'):
            v.integer(p[name], 0 if name not in ('width', 'height') else 1, 256, 'piece ' + name)
        x0, y0, u, tex_v, w, h = (p[k] for k in ('x', 'y', 'u', 'v', 'width', 'height'))
        v.require(x0 + w <= WIDTH and y0 + h <= HEIGHT and u + w <= 256 and tex_v + h <= 200, 'Out-of-bounds piece')
        v.require(p['material'] in ('add', 'subtract'), 'Unsupported material')
        v.require(len(p['colors']) == 4 and p['colors'][0] == p['colors'][1] and p['colors'][2] == p['colors'][3], 'Expected vertical gradients')
        for color in p['colors']:
            v.fields(color, 'r g b', 'vertex color')
            for channel in color.values(): v.integer(channel, 0, 255, 'vertex channel')
        for row in range(h):
            for col in range(w):
                word = words[indices[(tex_v + row) * 256 + u + col]]
                if word == 0: continue
                at = (y0 + row) * WIDTH + x0 + col
                touched.add(at)
                for c, key in enumerate('rgb'):
                    t = (word >> (5 * c)) & 31
                    t = (t << 3) | (t >> 2)
                    numerator = p['colors'][0][key] * (2 * h - 2 * row - 1) + p['colors'][2][key] * (2 * row + 1)
                    shade = min(255, t * numerator // (256 * h))
                    for target in (low, high):
                        i = at * 3 + c
                        target[i] = shade if not word & 0x8000 else min(255, target[i] + shade) if p['material'] == 'add' else max(0, target[i] - shade)
    invariant = {at for at in touched if low[at * 3:at * 3 + 3] == high[at * 3:at * 3 + 3]}
    return low, high, touched, invariant


def samples(original, candidate, points):
    points = sorted(points)
    point_set = set(points)
    v.require(bool(points), 'Empty diagnostic sample set')
    delta = [tuple(original[at * 4 + c] - candidate[at * 4 + c] for c in range(3)) for at in points]
    return dict(pixels=len(points), changedPixels=sum(any(d) for d in delta),
        minOriginalMinusManaged=[min(d[c] for d in delta) for c in range(3)],
        maxOriginalMinusManaged=[max(d[c] for d in delta) for c in range(3)],
        maxAbsoluteDelta=[max(abs(d[c]) for d in delta) for c in range(3)],
        sumAbsoluteDelta=[sum(abs(d[c]) for d in delta) for c in range(3)],
        bounds=[min(at % WIDTH for at in points), min(at // WIDTH for at in points),
                max(at % WIDTH for at in points) - min(at % WIDTH for at in points) + 1,
                max(at // WIDTH for at in points) - min(at // WIDTH for at in points) + 1],
        sampleMaskSha256=v.sha(bytes(at in point_set for at in range(WIDTH * HEIGHT))),
        exactEquality=not any(any(d) for d in delta))


def compare(original, candidate, definition, indices, palette):
    low, high, touched, invariant = transfer_bounds(definition, indices, palette)
    v.require(bool(invariant), 'No background-invariant diagnostic pixels')
    # Guard the analysis transcription against the actual C# capture before using its mask.
    v.require(all(candidate[at * 4:at * 4 + 3] == low[at * 3:at * 3 + 3] for at in invariant),
              'Managed capture disagrees with diagnostic transfer model')
    rows = []
    for name, group in (('first-line', definition['pieces'][:3]), ('second-line', definition['pieces'][3:])):
        left, top = min(p['x'] for p in group), min(p['y'] for p in group)
        right, bottom = max(p['x'] + p['width'] for p in group), max(p['y'] + p['height'] for p in group)
        selected = {at for at in touched if left <= at % WIDTH < right and top <= at // WIDTH < bottom}
        core = selected & invariant
        rect = dict(id=name, category='palette', rect=[left, top, right - left, bottom - top],
                    maxChannelDelta=[0, 0, 0, 0], maxChangedPixels=0,
                    rationale='Raw exact diagnostic only; battle and checker backgrounds differ. No fidelity tolerance is selected.')
        rows.append(dict(id=name, rawRectangle=v.region_metrics(original, candidate, WIDTH, rect),
            managedTextureSupport=samples(original, candidate, selected),
            managedBackgroundInvariant=samples(original, candidate, core),
            backgroundSensitiveSupportPixels=len(selected - invariant)))
    return dict(format='fft.observed-banner-report', schemaVersion=1, mode='observed-original-diagnostic',
        originalComparisonPerformed=True, originalFidelityEstablished=False, simulationCompared=False,
        exactOriginalLogicalTickVerified=False, timingComparisonPerformed=False, fullFrameComparisonPerformed=False,
        automaticRootCauseDiagnosis=False, geometryVerdict='manual inspection required; no alignment search',
        colorAcceptanceTolerance=None, strictInvariantEquality=all(row['managedBackgroundInvariant']['exactEquality'] for row in rows),
        rows=rows, limits=[
            'Fully visible artwork anchor only; original and managed timing domains are not equated.',
            'Mask is derived from the prepared managed material model; it is not an independent original geometry oracle.',
            'Selected core sample equality cannot detect all missing/extra artwork outside that mask.',
            'Raw rectangles/support include unmatched backgrounds; they are diagnostics, not fidelity pass/fail.',
            'PS1 precision/dithering/rasterization and transparency/order are not adjudicated by this report.'])


def load(plan):
    v.fields(plan, 'format schemaVersion profile original candidate prepared alignment anchorRationale', 'observed banner plan')
    v.require(plan['format'] == 'fft.observed-banner-comparison' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
              'Unsupported observed comparison format/version')
    v.require(plan['profile'] == 'general-0-full-visible-v1', 'Unsupported observed profile')
    v.text(plan['anchorRationale'], 'visual anchor rationale')
    v.fields(plan['alignment'], 'originalRect candidateRect rationale', 'alignment')
    v.text(plan['alignment']['rationale'], 'alignment rationale')
    for key, expected in (('original', 'png observation state profile'), ('candidate', 'png receipt tick'), ('prepared', 'definition indices palette')):
        v.fields(plan[key], expected, key)
    inputs = {}
    for group in ('original', 'candidate', 'prepared'):
        inputs[group] = {key: v.verified_file(record, 32 * 1024 * 1024 if key in ('png', 'state') else 65536)
                         for key, record in plan[group].items() if key != 'tick'}
    for name, (digest, length) in ARTIFACT_PINS.items():
        v.require(v.sha(inputs['prepared'][name]) == digest and len(inputs['prepared'][name]) == length,
                  'Prepared artwork differs from the reviewed general-0 profile')
    observation = v.parse(inputs['original']['observation'])
    profile = v.parse(inputs['original']['profile'])
    v.require(observation['format'] == 'fft.original-banner-state-observation' and observation['schemaVersion'] == 1 and
              observation['edition'] == 'SCUS-94221' and observation['repeatRestoredImagesByteIdentical'] is True and
              observation['selectedCoreOptionsRetained'] is True, 'Expected bounded original state observation')
    for key in ('state', 'profile'):
        v.require(observation[key]['sha256'] == v.sha(inputs['original'][key]), 'Original provenance binding mismatch')
    v.require(any(item['sha256'] == v.sha(inputs['original']['png']) for item in observation['restoreImages']), 'Original PNG is not a bound restored checkpoint')
    v.require(profile['format'] == 'fft.retroarch-reference-profile' and profile['schemaVersion'] == 1 and profile['edition'] == 'SCUS-94221', 'Unexpected original environment')
    definition = v.parse(inputs['prepared']['definition'])
    v.require(definition['source']['referenceRevision'] == REVISION, 'Unexpected artwork source revision')
    receipt = v.parse(inputs['candidate']['receipt'])
    v.integer(plan['candidate']['tick'], 0, 2**63 - 1, 'managed tick')
    v.require(receipt['referenceRevision'] == REVISION and receipt['rendererTaskAcknowledgments'] == 0, 'Unexpected managed capture receipt')
    captures = [c for c in receipt['captures'] if c['tick'] == plan['candidate']['tick']]
    v.require(len(captures) == 1, 'Missing/duplicate managed checkpoint')
    checkpoint = captures[0]
    v.require(checkpoint['phase'] == 'Holding' and checkpoint['pieces'] == [dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)],
              'Managed checkpoint must hold all six full-height pieces')
    v.require(Path(plan['candidate']['png']['path']).name == checkpoint['file'] and
              Path(plan['candidate']['png']['path']).parent == Path(plan['candidate']['receipt']['path']).parent, 'Managed PNG/receipt location mismatch')
    a, b = image(inputs['original']['png'], (280, 240)), image(inputs['candidate']['png'], (256, 240))
    v.require(v.sha(b) == checkpoint['rgbaSha256'], 'Managed PNG pixels differ from receipt')
    a, b = align(a, b, plan['alignment']['originalRect'], plan['alignment']['candidateRect'])
    return inputs, a, b, definition, profile, receipt


def review_html(report, inputs):
    urls = ['data:image/png;base64,' + base64.b64encode(inputs[group]['png']).decode('ascii') for group in ('original', 'candidate')]
    equality = 'match exactly' if report['strictInvariantEquality'] else 'differ'
    return '''<!doctype html><html lang="en"><meta charset="utf-8"><title>FFT banner comparison</title>
<style>body{font:16px system-ui;margin:32px;background:#171b20;color:#edf0f2;max-width:1140px}h1{font-size:24px}p{max-width:1000px;line-height:1.5}.pair{display:flex;gap:28px;flex-wrap:wrap}.view{width:512px;height:480px;overflow:hidden;border:1px solid #69727b}.view img{image-rendering:pixelated;height:480px}pre{white-space:pre-wrap;font:13px monospace}summary{cursor:pointer}</style>
<h1>Original / managed banner: fully visible artwork</h1>
<p>Diagnostic comparison. Original logical timing is unverified. Text placement requires visual review; strict sampled colors ''' + equality + '''. No fidelity tolerance or full-scene equivalence is claimed.</p>
<div class="pair"><section><h2>Original restored state</h2><p>280×240 raw; measured 12-column black borders excluded.</p><div class="view"><img alt="Original battle banner" style="width:560px;margin-left:-24px" src="''' + urls[0] + '''"></div></section>
<section><h2>Managed tick ''' + str(report['managedTick']) + '''</h2><p>256×240 authored checker; fully visible Holding anchor.</p><div class="view"><img alt="Managed banner preview" style="width:512px" src="''' + urls[1] + '''"></div></section></div>
<p>Both views displayed at 2× with nearest-neighbor CSS scaling. Report metrics use original pixels, without resampling. The diagnostic subset uses only pixels invariant to the managed background model; shadows and other background-sensitive pixels remain separately reported.</p>
<details><summary>Raw diagnostic report</summary><pre>''' + json.dumps(report, indent=2).replace('&', '&amp;').replace('<', '&lt;') + '</pre></details></html>'


def run(plan_path, output):
    from PIL import __version__
    v.require(__version__ == '12.3.0', 'Review changes to Pillow 12.3.0 explicitly')
    source, destination = v.scratch_file(plan_path), v.scratch_file(output)
    v.require(not destination.exists(), 'Output already exists; choose a fresh directory')
    v.require(source.stat().st_size <= 65536, 'Plan exceeds budget')
    raw = source.read_bytes(); plan = v.parse(raw)
    inputs, a, b, definition, profile, receipt = load(plan)
    report = compare(a, b, definition, inputs['prepared']['indices'], inputs['prepared']['palette'])
    report.update(plan=plan, planSha256=v.sha(raw), originalEnvironment=profile,
                  managedPolicy=receipt['policy'], managedTick=plan['candidate']['tick'],
                  tools=dict(python=sys.version.split()[0], pillow=__version__, harnessSha256=v.sha(Path(__file__).read_bytes()),
                             sharedHarnessSha256=v.sha(Path(v.__file__).read_bytes())))
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.observed-banner-' + uuid.uuid4().hex); staging.mkdir()
    try:
        (staging / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
        (staging / 'review.html').write_text(review_html(report, inputs), encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists():
            for name in ('report.json', 'review.html'): (staging / name).unlink(missing_ok=True)
            staging.rmdir()
    print('Observed banner diagnostic written; strict invariant equality=' + str(report['strictInvariantEquality']) + '; fidelity unestablished')
    return 0 if report['strictInvariantEquality'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try: return run(args.plan, args.output)
    except (ValueError, OSError, KeyError, TypeError, IndexError, RecursionError, ImportError) as error:
        print('Invalid/unavailable observed banner evidence: ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
