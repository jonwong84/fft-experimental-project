"""ZOD-156 source/closed-refresh descriptions; no original, C or managed refresh execution."""
import argparse
import json
from pathlib import Path

from verify_ai_decision_contract import draws
from verify_ai_execution_contract import audit as audit_execution
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-refresh-contract-v1.json'
EVIDENCE = ROOT / 'docs/evidence/ai/refresh-contract-v1.json'
MAX = (1 << 64) - 1


def flags(hp, maximum, enemy, old_flags, old_targeting):
    """Only targetable living uncharged stable opponents, zero cached MP modifier."""
    require(0 < hp <= maximum <= 65535, 'Living HP domain')
    return dict(enemy=enemy, decision=(old_flags & 0x78) | 0x80,
                targeting=(old_targeting & 0x0c) | (1 if hp < maximum // 2 else 0)
                | (0x80 if hp * 4 < maximum * 3 else 0))


def refresh_clock(data):
    """Authored two-unit, already-moved no-action arithmetic; counter rollback is managed policy."""
    original_word, original_count = data['word'], data['count']
    require(0 <= original_word <= 0xffffffff and 0 <= original_count <= MAX, 'RNG domain')
    require(0 <= data['ticks'] <= MAX, 'Tick domain')
    units = [dict(u) for u in data['units']]
    require(len(units) == 2 and len({u['tieKey'] for u in units}) == 2, 'Two ordered ordinary units')
    require(all(0 <= u['ct'] <= 254 and 1 <= u['speed'] <= 255 for u in units), 'Clock domain')
    actor = next(u for u in units if u['id'] == data['actor'])
    actor['ct'] = min(60, actor['ct'] + 20)
    end_ct = actor['ct']; ticks = data['ticks']; elapsed = 0
    while True:
        ready = sorted((u for u in units if u['ct'] >= 100), key=lambda u: (-u['ct'], u['tieKey']))
        if ready:
            if original_count == MAX:
                break
            picked = ready[0]; picked['ct'] %= 100
            word, raw = draws(original_word, 1)
            return dict(accepted=True, endCt=end_ct, picked=picked['id'], elapsed=elapsed, ticks=ticks,
                        ct={u['id']:u['ct'] for u in units}, raw=raw[0], word=word, count=original_count+1)
        if ticks == MAX:
            break
        ticks += 1; elapsed += 1
        for unit in units:
            unit['ct'] = min(254, unit['ct'] + unit['speed'])
    return dict(accepted=False, word=original_word, count=original_count)


def audit(reference):
    report = audit_execution(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Refresh reference revision')
    for entry in evidence['additionalSourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
    require(fixture['referenceRevision'] == report['referenceRevision'], 'Refresh vectors revision')
    for row in fixture['vectors']:
        require(refresh_clock(row['input']) == row['expected'], 'Refresh vector: ' + row['id'])
    return dict(**report, refreshFiles=len(evidence['additionalSourceFiles']),
                refreshFunctions=len(evidence['additionalSourceFunctions']), refreshVectors=len(fixture['vectors']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
