"""Optional ZOD-033 PNG comparison; authored and same-environment evidence only.

No game, GPU or original assets are required. All capture inputs/output stay in scratch/.
This tool does not infer a root cause from pixels or claim original-game equivalence.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
MAX_PIXELS = 4_194_304
CHANNELS = 'RGBA'
CATEGORIES = {'geometry', 'palette', 'transparency-order'}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def fields(value, expected, label):
    require(type(value) is dict and set(value) == set(expected.split()), f'Unknown/missing fields in {label}')


def integer(value, lower, upper, label):
    require(type(value) is int and lower <= value <= upper, f'Invalid {label}')


def text(value, label):
    require(type(value) is str and 0 < len(value) <= 512 and value.strip(), f'Invalid {label}')


def hex_hash(value):
    require(type(value) is str and len(value) == 64 and all(c in '0123456789abcdef' for c in value), 'Expected lowercase SHA-256')


def sha(data):
    return hashlib.sha256(data).hexdigest()


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate field: ' + key)
        result[key] = value
    return result


def parse(data):
    require(len(data) <= 65536, 'Metadata exceeds 64 KiB')
    return json.loads(data, object_pairs_hook=unique, parse_constant=lambda value: require(False, 'Invalid JSON constant'))


def scratch_file(value):
    text(value, 'local path')
    require('\\' not in value and ':' not in value and not Path(value).is_absolute() and
            all(p not in ('', '.', '..') for p in value.split('/')), 'Expected safe repository-relative path')
    path = ROOT / value
    require(path.resolve().is_relative_to((ROOT / 'scratch').resolve()), 'Capture path must stay below scratch/')
    for part in (path, *path.parents):
        require(not part.is_symlink() and not part.is_junction(), 'Reparse capture paths are unsupported')
    return path


def verified_file(record, budget):
    fields(record, 'path sha256', 'file identity')
    hex_hash(record['sha256'])
    path = scratch_file(record['path'])
    require(path.stat().st_size <= budget, 'Capture file exceeds budget')
    data = path.read_bytes()
    require(len(data) <= budget and sha(data) == record['sha256'], 'Capture SHA-256 mismatch: ' + record['path'])
    return data


def validate_plan(plan):
    fields(plan, 'format schemaVersion mode width height checkpoints regions', 'comparison plan')
    require(plan['format'] == 'fft.visual-comparison' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
            'Unsupported comparison format/version')
    require(plan['mode'] in ('authored-reference', 'same-environment'), 'Original-reference comparisons need a separate observed-capture contract')
    width, height = plan['width'], plan['height']
    integer(width, 1, 4096, 'width'); integer(height, 1, 4096, 'height')
    require(width * height <= MAX_PIXELS, 'Canvas exceeds pixel budget')
    require(type(plan['checkpoints']) is list and 1 <= len(plan['checkpoints']) <= 64, 'Expected 1..64 checkpoints')
    ids = set()
    for checkpoint in plan['checkpoints']:
        fields(checkpoint, 'id tick timingDomain', 'checkpoint')
        text(checkpoint['id'], 'checkpoint ID'); integer(checkpoint['tick'], 0, 2**63 - 1, 'tick')
        require(checkpoint['id'] not in ids, 'Duplicate checkpoint ID'); ids.add(checkpoint['id'])
        require(checkpoint['timingDomain'] in ('logical-tick', 'presentation-frame'), 'Unsupported timing domain')
    require(type(plan['regions']) is list and 1 <= len(plan['regions']) <= 16, 'Expected 1..16 regions')
    ids = set()
    for region in plan['regions']:
        fields(region, 'id category rect maxChannelDelta maxChangedPixels rationale', 'region')
        text(region['id'], 'region ID'); text(region['rationale'], 'threshold rationale')
        require(region['id'] not in ids, 'Duplicate region ID'); ids.add(region['id'])
        require(region['category'] in CATEGORIES, 'Unsupported declared region category')
        rect = region['rect']
        require(type(rect) is list and len(rect) == 4, 'Expected [x,y,width,height] region')
        x, y, w, h = rect
        integer(x, 0, width - 1, 'region X'); integer(y, 0, height - 1, 'region Y')
        integer(w, 1, width - x, 'region width'); integer(h, 1, height - y, 'region height')
        delta = region['maxChannelDelta']
        require(type(delta) is list and len(delta) == 4, 'Expected RGBA channel tolerances')
        for value in delta:
            integer(value, 0, 255, 'channel tolerance')
        integer(region['maxChangedPixels'], 0, w * h, 'changed-pixel tolerance')


def validate_capture(capture):
    fields(capture, 'format schemaVersion kind width height environment viewBindingSha256 checkpoints', 'capture manifest')
    require(capture['format'] == 'fft.visual-capture' and type(capture['schemaVersion']) is int and capture['schemaVersion'] == 1,
            'Unsupported capture format/version')
    require(capture['kind'] in ('authored-expectation', 'managed-capture'), 'Unsupported capture evidence kind')
    integer(capture['width'], 1, 4096, 'capture width'); integer(capture['height'], 1, 4096, 'capture height')
    require(capture['width'] * capture['height'] <= MAX_PIXELS, 'Capture exceeds pixel budget')
    text(capture['environment'], 'environment'); hex_hash(capture['viewBindingSha256'])
    require(type(capture['checkpoints']) is list and 1 <= len(capture['checkpoints']) <= 64, 'Expected capture checkpoints')
    ids = set()
    for checkpoint in capture['checkpoints']:
        fields(checkpoint, 'id tick timingDomain simulationSha256 png receipt', 'capture checkpoint')
        text(checkpoint['id'], 'capture checkpoint ID')
        require(checkpoint['id'] not in ids, 'Duplicate capture checkpoint'); ids.add(checkpoint['id'])
        integer(checkpoint['tick'], 0, 2**63 - 1, 'capture tick')
        require(checkpoint['timingDomain'] in ('logical-tick', 'presentation-frame'), 'Unsupported capture timing domain')
        hex_hash(checkpoint['simulationSha256'])
        for kind in ('png', 'receipt'):
            fields(checkpoint[kind], 'path sha256', kind)
            text(checkpoint[kind]['path'], kind + ' path'); hex_hash(checkpoint[kind]['sha256'])


def region_metrics(reference, candidate, width, region):
    x, y, w, h = region['rect']
    maximum = [0] * 4
    sums = [0] * 4
    changed = exceeded = alpha_changed = color_changed = 0
    left, top, right, bottom = width, y + h, -1, -1
    for row in range(y, y + h):
        for column in range(x, x + w):
            at = (row * width + column) * 4
            delta = [abs(reference[at + c] - candidate[at + c]) for c in range(4)]
            for c in range(4):
                maximum[c] = max(maximum[c], delta[c]); sums[c] += delta[c]
            if any(delta):
                changed += 1
                left, top, right, bottom = min(left, column), min(top, row), max(right, column), max(bottom, row)
            color_changed += any(delta[:3]); alpha_changed += delta[3] != 0
            exceeded += any(delta[c] > region['maxChannelDelta'][c] for c in range(4))
    return dict(id=region['id'], declaredCategory=region['category'], rect=region['rect'], pixels=w * h,
                changedPixels=changed, colorChangedPixels=color_changed, alphaChangedPixels=alpha_changed,
                aboveTolerancePixels=exceeded, maxAbsoluteDelta=maximum, sumAbsoluteDelta=sums,
                changedBounds=None if not changed else [left, top, right - left + 1, bottom - top + 1],
                maxChannelDelta=region['maxChannelDelta'], maxChangedPixels=region['maxChangedPixels'],
                rationale=region['rationale'], passed=exceeded <= region['maxChangedPixels'])


def compare(plan, reference, candidate, pixels):
    """Pure report from validated metadata and an injected image loader; no GPU or simulation operations."""
    validate_plan(plan); validate_capture(reference); validate_capture(candidate)
    require(candidate['kind'] == 'managed-capture', 'Candidate must be a managed capture')
    require(reference['kind'] == ('managed-capture' if plan['mode'] == 'same-environment' else 'authored-expectation'),
            'Reference evidence does not match comparison mode')
    expected_ids = {c['id'] for c in plan['checkpoints']}
    require(all({c['id'] for c in capture['checkpoints']} == expected_ids for capture in (reference, candidate)),
            'Missing/extra checkpoint evidence')
    reports = []
    for requested in plan['checkpoints']:
        pair = [next(c for c in capture['checkpoints'] if c['id'] == requested['id']) for capture in (reference, candidate)]
        failures = []
        if any((c['width'], c['height']) != (plan['width'], plan['height']) for c in (reference, candidate)):
            failures.append('canvas-mismatch')
        if reference['viewBindingSha256'] != candidate['viewBindingSha256']:
            failures.append('view-binding-mismatch')
        if plan['mode'] == 'same-environment' and reference['environment'] != candidate['environment']:
            failures.append('environment-mismatch')
        if pair[0]['simulationSha256'] != pair[1]['simulationSha256']:
            failures.append('simulation-mismatch')
        if any((c['tick'], c['timingDomain']) != (requested['tick'], requested['timingDomain']) for c in pair):
            failures.append('timing-mismatch')
        regions = []
        if not failures:
            a, b = (pixels(c, plan['width'], plan['height']) for c in pair)
            require(len(a) == len(b) == plan['width'] * plan['height'] * 4, 'Invalid decoded RGBA byte length')
            regions = [region_metrics(a, b, plan['width'], region) for region in plan['regions']]
            if not all(r['passed'] for r in regions):
                failures.append('visual-mismatch')
        reports.append(dict(id=requested['id'], requested=requested, reference=pair[0], candidate=pair[1],
                            failures=failures, regions=regions, passed=not failures))
    return dict(format='fft.visual-comparison-report', schemaVersion=1, mode=plan['mode'],
                originalComparisonPerformed=False, automaticRootCauseDiagnosis=False,
                passed=all(r['passed'] for r in reports), checkpoints=reports)


def read_pixels(checkpoint, width, height):
    from PIL import Image
    verified_file(checkpoint['receipt'], 65536)
    data = verified_file(checkpoint['png'], 32 * 1024 * 1024)
    require(len(data) >= 33 and data[:8] == b'\x89PNG\r\n\x1a\n' and data[24] == 8 and data[25] in (2, 6),
            'Only native RGB8/RGBA8 PNG captures are supported')
    require(data[12:16] == b'IHDR' and (int.from_bytes(data[16:20], 'big'), int.from_bytes(data[20:24], 'big')) == (width, height),
            'PNG header does not match the bounded comparison canvas')
    with Image.open(io.BytesIO(data)) as image:
        require(image.format == 'PNG' and image.size == (width, height) and image.mode in ('RGB', 'RGBA'),
                'Expected exact-size RGB8/RGBA8 PNG; no resizing, palette conversion or color correction')
        require(not getattr(image, 'is_animated', False), 'Animated capture is unsupported')
        return image.convert('RGBA').tobytes()


def run(args):
    from PIL import __version__
    require(__version__ == '12.3.0', 'Review changes to Pillow 12.3.0 explicitly')
    plan_path, ref_path, candidate_path = (scratch_file(p) for p in (args.plan, args.reference, args.candidate))
    destination = scratch_file(args.output)
    require(not destination.exists(), 'Output already exists; choose a fresh directory')
    payloads = [p.read_bytes() if p.stat().st_size <= 65536 else b'x' * 65537 for p in (plan_path, ref_path, candidate_path)]
    plan, reference, candidate = map(parse, payloads)
    # Verify evidence even when timing/state incompatibility prevents pixel comparison.
    for capture in (reference, candidate):
        validate_capture(capture)
        for checkpoint in capture['checkpoints']:
            verified_file(checkpoint['receipt'], 65536); verified_file(checkpoint['png'], 32 * 1024 * 1024)
    report = compare(plan, reference, candidate, read_pixels)
    report['inputs'] = dict(zip(('planSha256', 'referenceManifestSha256', 'candidateManifestSha256'), map(sha, payloads)))
    report['plan'] = plan
    report['referenceCapture'] = reference
    report['candidateCapture'] = candidate
    report['tools'] = dict(python=sys.version.split()[0], pillow=__version__, harnessSha256=sha(Path(__file__).read_bytes()))
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.visual-comparison-' + uuid.uuid4().hex)
    staging.mkdir()
    try:
        (staging / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists():
            (staging / 'report.json').unlink(missing_ok=True); staging.rmdir()
    print(('PASS' if report['passed'] else 'FAIL') + f': {len(report["checkpoints"])} checkpoints; original comparison not performed')
    return 0 if report['passed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('plan', 'reference', 'candidate', 'output'):
        parser.add_argument('--' + key, required=True, help='Repository-relative path beneath scratch/')
    try:
        return run(parser.parse_args())
    except (ValueError, OSError, TypeError, KeyError, RecursionError, ImportError) as error:
        print('Invalid/unavailable comparison evidence: ' + str(error), file=sys.stderr)
        return 2


if __name__ == '__main__':
    raise SystemExit(main())
