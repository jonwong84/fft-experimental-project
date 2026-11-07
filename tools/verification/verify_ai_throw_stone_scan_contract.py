"""ZOD-190 source/description audit; never executes C, original or managed AI."""
import argparse
import json
from pathlib import Path

from verify_ai_ability_iteration_contract import audit as audit_abilities, prepared_facts
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/throw-stone-scan-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-throw-stone-scan-contract-v1.json'


def describe_scan(data):
    """Isolated ranked-origin ordering with supplied path/result facts, after movement scoring.

    Does not compute geometry, projectile paths, scores, formula RNG, clock or a full search.
    The description envelope is not a claim about original corrupt-input guards.
    """
    require(all(type(data[k]) is bool for k in ('usable', 'targetActive')), 'Gate booleans')
    rows = data['origins']
    require(len({tuple(r['tile']) for r in rows}) == len(rows), 'Unique origins')
    for row in rows:
        require(len(row['tile']) == 3 and all(type(v) is int and v >= 0 for v in row['tile']), 'Logical coordinates')
        require(type(row['tilePriority']) is int and -32768 <= row['tilePriority'] <= 32767 and
                type(row['proximity']) is int and 0 <= row['proximity'] <= 255, 'Source key widths')
        require(type(row['distance']) is int and row['distance'] >= 1 and type(row['clearPath']) is bool and
                row['disposition'] in ('Scored', 'NotValuable'), 'Supplied path/result description')
    eligible = [r for r in rows if r['distance'] <= 4] if data['usable'] and data['targetActive'] else []
    # origin_next_take scans layer/y/x ascending and uses >=, so later coordinates win equal keys.
    ordered = sorted(eligible, key=lambda r: (r['tilePriority'] * 65536 + r['proximity'],
                                            -r['tile'][2], -r['tile'][1], -r['tile'][0]))
    visited = []
    selected = None
    for row in ordered:
        visited.append(row['tile'])
        if row['clearPath']:
            selected = row
            break  # Prepared consume-aim policy applies even when the fresh score is NotValuable.
    origin = selected['tile'] if selected else None
    act_first = origin == data['actor'] if selected else None
    return dict(ordered=[r['tile'] for r in ordered], visited=visited, origin=origin,
                destination=(data['destination'] if act_first else origin), actBeforeMove=act_first,
                disposition=selected['disposition'] if selected else None,
                aimRemaining=int(selected is None and bool(eligible)),
                entryGateDraws=0, formulaDraws=4 if selected else 0)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-190' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False and
            fixture['kind'] == 'isolated-source-descriptions', 'Source-only Throw Stone scan fixture')
    ids = []
    for row in fixture['vectors']:
        require(describe_scan(row['input']) == row['expected'], 'Throw Stone scan vector: ' + row['id'])
        ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique nonempty scan descriptions')
    return dict(throwStoneScanDescriptions=len(ids))


def selected_facts(path):
    row = next(r for r in prepared_facts(path) if r['abilityId'] == 148)
    data = path.read_bytes()  # Edition/hash already checked by prepared_facts; record only semantic facts.
    offset = 0x8005fbf0 + 148 * 14 - 0x80010000 + 0x800
    secondary = data[offset:offset + 14]
    return dict(row, vertical=secondary[2], cannotTargetSelf=bool(secondary[3] & 1),
                cannotHitCaster=bool(secondary[4] & 1), noFollowTarget=bool(secondary[4] & 0x10))


def audit(reference, prepared=None):
    report = audit_abilities(reference, prepared)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Scan reference')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Scan source mismatch: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(actual['normalizedLfSha256'] == row['normalizedLfSha256'], 'Scan function mismatch: ' + row['symbol'])
    if prepared is not None:
        require(selected_facts(prepared) == evidence['selectedDataFacts'], 'Scan prepared facts')
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                throwStoneScanFiles=len(evidence['sourceFiles']), throwStoneScanFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
