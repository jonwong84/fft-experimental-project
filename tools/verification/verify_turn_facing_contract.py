"""ZOD-118 offline source/helper and proposed lifecycle contract audit; no C# or C execution."""
import argparse
import json
from pathlib import Path
import subprocess

from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/turn-facing-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/turn-facing-contract-v1.json'
FACES = ('South', 'West', 'North', 'East')
MANAGED = {'East': 0, 'North': 1, 'West': 2, 'South': 3}
OFFSETS = ((0, -1), (-1, 0), (0, 1), (1, 0))


def direction(origin, target):
    """Source signed-byte reads, deliberately distinct from a wide-coordinate atan2."""
    require(len(origin) == len(target) == 2 and
            all(type(v) is int and 0 <= v <= 255 for v in origin + target), 'Coordinate byte domain')
    ox, oy, tx, ty = (v if v < 128 else v - 256 for v in origin + target)
    dx, dy = tx - ox, ty - oy
    if dy - dx <= 0 and dy + dx < 0: return 'South'
    if dy - dx > 0 and dy + dx <= 0: return 'West'
    if dy - dx >= 0 and dy + dx > 0: return 'North'
    if dy - dx < 0 and dy + dx >= 0: return 'East'
    return 'Overlap'


def viability(data):
    """Single source tile plus four adjacent lower tiles; occupancy/half-height/depth are not consulted."""
    require(len(data['adjacent']) == 4, 'Four adjacent tiles')
    require(0 <= data['x'] < data['width'] <= 255 and 0 <= data['y'] < data['depth'] <= 255,
            'Map bounds')
    viable = []
    for face, (dx, dy), tile in zip(FACES, OFFSETS, data['adjacent']):
        x, y = (data['x'] + dx) & 255, (data['y'] + dy) & 255
        if x < data['width'] and y < data['depth'] and not tile['blocked'] and tile['height'] - data['sourceHeight'] < 3:
            viable.append(face)
    return dict(viable=viable or list(FACES), allBlockedFallback=not viable)


def choose(data):
    """Normalized eligible enemy inputs; this does not derive teams, timed statuses or the AI workspace."""
    hint, current = data['hint'], data['current']
    require(current in FACES and hint in FACES + ('KeepCurrent', 'ChooseAtWait'), 'Facing hint')
    if hint != 'ChooseAtWait':
        face = current if hint == 'KeepCurrent' else hint
        return dict(supported=True, facing=face, managedFacing=MANAGED[face], visited=[])
    viable = data['viable'] or list(FACES)
    require(len(set(viable)) == len(viable) and all(f in FACES for f in viable), 'Viability set')
    # Fallback is the last source direction, irrespective of caller list order.
    fallback = next(face for face in reversed(FACES) if face in viable)
    enemies = sorted((e for e in data['enemies'] if e['eligible']), key=lambda e: e['slot'])
    require(len({e['slot'] for e in enemies}) == len(enemies), 'Distinct original slots')
    distance = lambda e: abs(e['position'][0] - data['actor'][0]) + abs(e['position'][1] - data['actor'][1])
    imminent = sorted((e for e in enemies if e['imminent']), key=lambda e: (distance(e), e['slot']))
    visited = []
    for enemy in imminent:
        visited.append(enemy['slot'])
        face = direction(data['actor'], enemy['position'])
        if face == 'Overlap': return dict(supported=False, reason='OverlapStackByte', visited=visited)
        if face in viable: return dict(supported=True, facing=face, managedFacing=MANAGED[face], visited=visited)
    if not enemies: return dict(supported=False, reason='NoEnemyStackByte', visited=visited)
    # The any-enemy search ignores the exclusions made while trying imminent enemies.
    enemy = min(enemies, key=lambda e: (distance(e), e['slot']))
    visited.append(enemy['slot'])
    face = direction(data['actor'], enemy['position'])
    if face == 'Overlap': return dict(supported=False, reason='OverlapStackByte', visited=visited)
    if face not in viable: face = current if current in viable else fallback
    return dict(supported=True, facing=face, managedFacing=MANAGED[face], visited=visited)


def clock_end(data):
    """Ordinary no-restriction turn_end slice. Repeated source call is a no-op when has_turn is clear."""
    require(type(data['ct']) is int and 0 <= data['ct'] <= 254, 'CT domain')
    ct = data['ct']
    if data['hasTurn']:
        ct = min(60, ct + (0 if data['moved'] else 20) + (0 if data['acted'] else 20))
    return dict(ct=ct, hasTurn=False, moved=data['moved'], acted=data['acted'], rngDraws=0)


def completion_gate(data):
    """Proposed managed authorization model, not original C and not an implemented session runner."""
    command = data['command']
    require(command in ('WaitWithFacing', 'FinishFacing', 'Advance', 'Move', 'Act'), 'Completion command')
    if data['pending'] and command != 'FinishFacing': return dict(accepted=False, reason='FacingPending')
    if command == 'FinishFacing':
        if not data['pending']: return dict(accepted=False, reason='MissingCompletion')
        if not data['receiptMatches']: return dict(accepted=False, reason='StaleCompletion')
        if not data['actorMatches']: return dict(accepted=False, reason='WrongActor')
        if data['active']: return dict(accepted=False, reason='InconsistentCompletion')
    elif command == 'WaitWithFacing':
        if not data['active']: return dict(accepted=False, reason='InactiveActor')
        if not data['actorMatches']: return dict(accepted=False, reason='WrongActor')
    if not data['revisionAvailable']: return dict(accepted=False, reason='RevisionExhausted')
    if command in ('WaitWithFacing', 'FinishFacing') and not data['choiceSupported']:
        return dict(accepted=False, reason='UnsupportedFacing')
    if command in ('WaitWithFacing', 'FinishFacing'):
        return dict(accepted=True, operation='FacingOnly' if command == 'FinishFacing' else 'WaitThenFacing', rngDraws=0)
    return dict(accepted=True, operation='Delegate') # The delegated Advance/Act can consume its own RNG.


def calculate(family, data):
    if family == 'direction':
        face = direction(data['origin'], data['target'])
        return dict(facing=face, managedFacing=MANAGED.get(face))
    if family == 'viability': return viability(data)
    if family == 'chooser': return choose(data)
    if family == 'clock-end': return clock_end(data)
    if family == 'completion-gate': return completion_gate(data)
    raise ValueError('Unsupported family: ' + family)


def validate(contract):
    require(contract['format'] == 'fft.turn-facing-source-description' and contract['schemaVersion'] == 1 and
            contract['ticket'] == 'ZOD-118' and contract['referenceRevision'] == REVISION, 'Contract identity')
    require(contract['execution'] == 'offline-source-and-proposed-lifecycle-description' and
            contract['productionImplemented'] is False, 'Execution boundary')
    seen, families = set(), set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector')
        seen.add(row['id']); families.add(row['family'])
        require(calculate(row['family'], row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
        kind = 'managed-design-proposal' if row['family'] == 'completion-gate' else (
            'managed-admission-boundary' if row['family'] == 'chooser' and not row['expected']['supported']
            else 'source-derived')
        require(row['expectationKind'] == kind, 'Expectation classification')
    require(families == {'direction', 'viability', 'chooser', 'clock-end', 'completion-gate'}, 'Family inventory')
    require(len(seen) == contract['vectorCount'] == 70, 'Vector inventory')
    require([r['id'] for r in contract['implementationCases']] == [f'FTF-{n:03}' for n in range(1, 19)] and
            all(r['status'] == 'unexecuted' for r in contract['implementationCases']), 'Unexecuted implementation inventory')
    return dict(vectors=len(seen), families=len(families), implementationCasesUnexecuted=18)


def audit(root, reference=None, check_managed_baseline=False):
    evidence = json.loads((root / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket'] == 'ZOD-118' and evidence['reference']['revision'] == REVISION, 'Evidence identity')
    require(evidence['fixture'] == fingerprint(root, FIXTURE), 'Fixture fingerprint')
    result = validate(json.loads((root / FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']:
        require(fingerprint(root, row['path']) == row, 'History changed: ' + row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']), managedBaselineFilesChecked=0,
                  referenceFilesChecked=0, referenceFunctionsChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']:
            require(fingerprint(root, row['path']) == row, 'Managed baseline changed: ' + row['path'])
        result['managedBaselineFilesChecked'] = len(evidence['managedBaseline'])
    if reference is not None:
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        status = subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference, row['path']) == {k:v for k,v in row.items() if k != 'functions'},
                    'Reference file changed: ' + row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference / row['path'], function['symbol']) == function,
                        'Reference function changed: ' + function['symbol'])
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    result.update(format='fft.turn-facing-source-contract-audit', schemaVersion=1, passed=True,
                  productionImplemented=False, fullAiExecuted=False, originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--check-managed-baseline', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2], args.reference, args.check_managed_baseline)
    with args.output.open('x', encoding='utf-8') as output: output.write(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result))


if __name__ == '__main__': main()
