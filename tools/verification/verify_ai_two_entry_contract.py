"""ZOD-192 isolated two-entry rank/RNG descriptions; no C, original or managed execution."""
import argparse
import json
from pathlib import Path

from verify_ai_throw_stone_scan_contract import audit as audit_scan
from verify_ai_rank_admission_contract import insert_best
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/two-entry-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-two-entry-contract-v1.json'


def describe(data):
    """Supplied evaluation costs and ranks, not a managed scan or formula-cost oracle."""
    require([e['kind'] for e in data['entries']] == ['Attack', 'ThrowStone'], 'Certified two-entry order')
    ranked = [dict(decided=0, priority=-32767, hit=0, proximity=0, payload='empty') for _ in range(8)]
    word, count = data['seed'], 0
    require(type(word) is int and 0 <= word <= 0xffffffff, 'Seed width')
    states, inserted = [], []
    for cost in [data['prefixDraws'], *[e['draws'] for e in data['entries']]]:
        require(type(cost) is int and 0 <= cost <= 256, 'Supplied description cost')
        for _ in range(cost):
            word = (word * 1103515245 + 12345) & 0xffffffff
            count += 1
        states.append([word, count])
    for entry in data['entries']:
        best = entry['best']
        if best is not None:
            require(best['decided'] == 0 and -320 <= best['priority'] <= 320 and
                    1 <= best['hit'] <= 100 and 0 <= best['proximity'] <= 10, 'Closed fresh rank')
        result = insert_best(best, data['baseline'], ranked)
        ranked = result['ranked']; inserted.append(result['insertedIndex'])
    return dict(rngAfterStages=states, ranked=[e['payload'] for e in ranked if e['hit'] != 0],
                insertionIndices=inserted, rankDraws=0)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-192' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False,
            'Source-only two-entry fixture')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['input']) == row['expected'], 'Two-entry vector: ' + row['id'])
        ids.append(row['id'])
    require(len(ids) > 0 and len(ids) == len(set(ids)), 'Unique two-entry descriptions')
    return dict(twoEntryDescriptions=len(ids))


def audit(reference, prepared=None):
    report = audit_scan(reference, prepared)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Two-entry revision')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Two-entry file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol'])['normalizedLfSha256'] == row['normalizedLfSha256'],
                'Two-entry function: ' + row['symbol'])
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                twoEntryFiles=len(evidence['sourceFiles']), twoEntryFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args(); report = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
