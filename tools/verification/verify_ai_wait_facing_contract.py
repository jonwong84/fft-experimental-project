"""Pinned Wait-facing source receipt and bounded directional arithmetic; no C/managed execution."""
import argparse
import json
from pathlib import Path

from verify_ai_movement_scenario_contract import audit as audit_scenario_source
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/wait-facing-contract-v1.json'
NAMES = ('South', 'West', 'North', 'East')
STEPS = ((0, -1), (-1, 0), (0, 1), (1, 0))


def direction(target, origin):
    """Source scalar branches on ordinary 0..15 coordinates, not a full Wait resolver."""
    dx, dy = target[0] - origin[0], target[1] - origin[1]
    a, b = dy - dx, dy + dx
    if a <= 0:
        if b < 0:
            return 0
    elif b <= 0:
        return 1
    if a >= 0 and b > 0:
        return 2
    if a < 0 and b >= 0:
        return 3
    return 4


def audit(reference, settlement=False):
    report = audit_scenario_source(reference, out_of_reach=True)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Wait reference revision')
    for entry in evidence['sourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
    for entry in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    for case in evidence['directionVectors']:
        require(NAMES[direction(case['target'], case['origin'])] == case['expected'], 'Direction vector: ' + case['expected'])
    pairs = 0
    points = [(x, y) for y in range(16) for x in range(16)]
    for origin in points:
        for target in points:
            if origin == target:
                continue
            step = STEPS[direction(target, origin)]
            neighbor = (origin[0] + step[0], origin[1] + step[1])
            require(all(min(origin[i], target[i]) <= neighbor[i] <= max(origin[i], target[i]) for i in (0, 1)),
                    'Directional neighbor must remain in the pair bounding box')
            require(sum(abs(target[i] - neighbor[i]) for i in (0, 1)) == sum(abs(target[i] - origin[i]) for i in (0, 1)) - 1,
                    'Directional neighbor must approach target')
            pairs += 1
    require(direction((5, 5), (5, 5)) == 4, 'Overlap direction; stack-dependent viability deliberately unresolved')
    result = dict(**report, waitSourceFiles=len(evidence['sourceFiles']), waitSourceFunctions=len(evidence['sourceFunctions']),
                directionVectors=len(evidence['directionVectors']), directionPairs=pairs, overlapViabilityResolved=False)
    if settlement:
        extension = json.loads((ROOT / 'docs/evidence/ai/wait-settlement-contract-v1.json').read_text(encoding='utf-8'))
        require(extension['referenceRevision'] == report['referenceRevision'], 'Settlement reference revision')
        for entry in extension['additionalSourceFiles']:
            require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
        for entry in extension['additionalSourceFunctions']:
            actual = function_fingerprint(reference / entry['path'], entry['symbol'])
            require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
        result['settlementFiles'] = len(extension['additionalSourceFiles'])
        result['settlementFunctions'] = len(extension['additionalSourceFunctions'])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--settlement', action='store_true', help='Also audit the ZOD-152 settlement receipt')
    args = parser.parse_args()
    report = audit(args.reference, args.settlement)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
