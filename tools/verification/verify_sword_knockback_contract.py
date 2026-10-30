"""Offline ZOD-114 source/description audit; never executes C or managed combat."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess

REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'
FIXTURE = 'docs/fixtures/behavior/sword-flat-knockback-contract-v1.json'
EVIDENCE = 'docs/evidence/combat/sword-knockback-contract-v1.json'
FACES = ('East', 'North', 'West', 'South')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def normalized_bytes(path):
    return path.read_text(encoding='utf-8').replace('\r\n', '\n').encode('utf-8')


def fingerprint(root, path):
    data = normalized_bytes(root / path)
    return dict(path=path, hashEncoding='utf8-lf', byteLength=len(data),
                sha256=hashlib.sha256(data).hexdigest())


def function_fingerprint(path, symbol):
    source = path.read_text(encoding='utf-8')
    pattern = r'^(?:static\s+)?[A-Za-z_][\w \t*]*\b' + re.escape(symbol) + r'\([^;{}]*\)\s*\{'
    match = re.search(pattern, source, re.MULTILINE)
    require(match is not None, 'Missing function: ' + symbol)
    # These inspected C definitions end with an unindented closing brace.
    end = re.search(r'^}', source[match.end():], re.MULTILINE)
    require(end is not None, 'Missing function end: ' + symbol)
    stop = match.end() + end.end()
    body = source[match.start():stop]
    return dict(symbol=symbol, startLine=source.count('\n', 0, match.start()) + 1,
                endLine=source.count('\n', 0, stop) + 1,
                normalizedLfSha256=hashlib.sha256(body.encode('utf-8')).hexdigest())


def critical_xa(pa, raw):
    require(type(pa) is int and 0 <= pa <= 255, 'PA range')
    require(type(raw) is int and 0 <= raw <= 32767, 'Raw range')
    base = max(1, pa)
    return base + raw * base // 32768


def critical_roll(raw):
    require(type(raw) is int and 0 <= raw <= 32767, 'Raw range')
    return raw * 100 // 32768 < 4


def envelope_supported(pa, power):
    return (type(pa) is int and type(power) is int and 0 <= pa <= 255 and
            0 <= power <= 255 and (2 * max(1, pa) - 1) * power <= 32767)


def resolve(inputs):
    """Independent arithmetic for only an admitted open flat cardinal two-unit case.

    This is an offline description calculator, not a production implementation,
    turn engine, terrain validator, preview model, or original executable oracle.
    """
    seed, count = inputs['seed'], inputs['drawCount']
    require(type(seed) is int and 0 <= seed <= 0xffffffff, 'Seed range')
    require(type(count) is int and 0 <= count <= 0xffffffffffffffff, 'Count range')
    pa, power = inputs['pa'], inputs['power']
    require(envelope_supported(pa, power), 'Signed-16 envelope')
    ax, ay = inputs['actor']; tx, ty = inputs['target']
    width, depth = inputs['mapSize']
    require(width > 0 and depth > 0 and all(0 <= x < width and 0 <= y < depth
            for x, y in ((ax, ay), (tx, ty))), 'Map coordinates')
    dx, dy = tx - ax, ty - ay
    require(abs(dx) + abs(dy) == 1, 'Cardinal adjacency')
    require(inputs['actorFacing'] in FACES and inputs['targetFacing'] in FACES, 'Facing')
    require(0 < inputs['hp'] <= 65535 and 0 <= inputs['classEvade'] <= 100, 'HP/evade range')
    require(all(10 <= inputs[key] <= 100 for key in ('actorBrave', 'targetBrave')), 'Brave domain')
    raw = []
    word = seed

    def draw():
        nonlocal word
        require(count + len(raw) < 0xffffffffffffffff, 'Draw count exhausted')
        word = (word * 1103515245 + 12345) & 0xffffffff
        value = (word >> 16) & 32767
        raw.append(value)
        return value

    front = 'West' if dx > 0 else 'East' if dx < 0 else 'South' if dy > 0 else 'North'
    evade = inputs['classEvade'] if inputs['targetFacing'] == front else 0
    hit = True
    for chance in (0, 0, 0, evade):
        if draw() * 100 // 32768 < chance:
            hit = False
            break
    critical = False
    damage = 0
    if hit:
        critical = critical_roll(draw())
        xa = critical_xa(pa, draw()) if critical else max(1, pa)
        damage = min(999, xa * power)
        draw()  # Empty conditional-status set still consumes the proc draw.
    hp = max(0, inputs['hp'] - damage)
    candidate = [tx + dx, ty + dy]
    in_bounds = 0 <= candidate[0] < width and 0 <= candidate[1] < depth
    moved = critical and in_bounds and hp > 0
    return dict(hit=hit, critical=critical, damage=damage, hp=hp,
                criticalKnockbackAttempted=critical,
                destinationEligible=critical and in_bounds,
                lethalCanceled=critical and in_bounds and hp == 0,
                target=candidate if moved else [tx, ty], moved=moved,
                actorFacing=inputs['actorFacing'], targetFacing=inputs['targetFacing'],
                rngWord=word, drawCount=count + len(raw), raw=raw)


def validate_contract(contract):
    require(contract['format'] == 'fft.sword-flat-knockback-description' and contract['schemaVersion'] == 1,
            'Description format')
    require(contract['referenceRevision'] == REVISION and contract['execution'] == 'description-only', 'Reference/execution')
    require(contract['productionImplementationExecuted'] is False, 'Unimplemented boundary')
    seen = set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector')
        seen.add(row['id'])
        require(resolve(row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
    required = {f'interior-{direction}-{face}' for direction in FACES for face in FACES}
    required |= {f'edge-{direction}' for direction in FACES}
    required |= {'normal', 'miss', 'side-hit', 'zero-power', 'zero-pa', 'lethal-equal',
                 'lethal-over', 'survive-one', 'cap', 'brave-low-high', 'brave-high-low',
                 'empty-proc-pass', 'critical-draw-remaining', 'normal-draw-remaining', 'miss-draw-remaining'}
    require(seen == required, 'Vector inventory')
    expected_boundaries = dict(criticalLastRaw=1310, noncriticalFirstRaw=1311,
                              pa5BonusLastZeroRaw=6553, pa5BonusFirstOneRaw=6554,
                              pa5CriticalXaMin=5, pa5CriticalXaMax=9,
                              pa255Power64WorstProduct=32576, pa255Power65WorstProduct=33085)
    require(contract['arithmeticBoundaries'] == expected_boundaries, 'Boundary inventory')
    require(critical_roll(1310) and not critical_roll(1311), 'Critical threshold')
    require(critical_xa(5, 6553) == 5 and critical_xa(5, 6554) == 6, 'Bonus threshold')
    require(critical_xa(5, 0) == 5 and critical_xa(5, 32767) == 9, 'Bonus endpoints')
    require(envelope_supported(255, 64) and not envelope_supported(255, 65), 'Envelope threshold')
    cases = contract['implementationCases']
    require([row['id'] for row in cases] == [f'SKB-{n:03}' for n in range(1, 21)] and
            all(row['status'] == 'unexecuted' and row['purpose'] for row in cases), 'Implementation inventory')
    return dict(vectors=len(seen), implementationCasesUnexecuted=len(cases))


def audit(root, reference=None, check_managed_baseline=False):
    evidence = json.loads((root / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['reference']['revision'] == REVISION and evidence['ticket'] == 'ZOD-114', 'Evidence identity')
    require(fingerprint(root, FIXTURE) == evidence['fixture'], 'Fixture fingerprint')
    contract = json.loads((root / FIXTURE).read_text(encoding='utf-8'))
    result = validate_contract(contract)
    for row in evidence['preservedFiles']:
        require(fingerprint(root, row['path']) == row, 'Preserved file changed: ' + row['path'])
    result['preservedFiles'] = len(evidence['preservedFiles'])
    result['managedBaselineFilesChecked'] = 0
    if check_managed_baseline:
        for row in evidence['managedBaseline']:
            require(fingerprint(root, row['path']) == row, 'Managed baseline changed: ' + row['path'])
            result['managedBaselineFilesChecked'] += 1
    result['referenceFilesChecked'] = 0
    result['referenceFunctionsChecked'] = 0
    if reference is not None:
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        status = subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            expected = {key: value for key, value in row.items() if key != 'functions'}
            require(fingerprint(reference, row['path']) == expected, 'Reference file changed: ' + row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference / row['path'], function['symbol']) == function,
                        'Reference function changed: ' + function['symbol'])
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    result.update(format='fft.sword-flat-knockback-audit', schemaVersion=1, passed=True,
                  productionImplementationExecuted=False, originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, help='Optional read-only pinned checkout; never executed')
    parser.add_argument('--check-managed-baseline', action='store_true',
                        help='Verify ZOD-114 managed baseline too; omit after intentional later implementation changes')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2], args.reference, args.check_managed_baseline)
    with args.output.open('x', encoding='utf-8') as output:
        output.write(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
