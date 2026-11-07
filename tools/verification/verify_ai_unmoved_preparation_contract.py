"""ZOD-171 offline unmoved refresh/preparation descriptions; no managed/C execution."""
import argparse
import json
from pathlib import Path

from verify_ai_candidate_contract import first_clock
from verify_ai_command_continuation_contract import audit as audit_continuation
from verify_ai_decision_contract import draws
from verify_flat_movement_contract import flat_route
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/unmoved-preparation-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-unmoved-preparation-contract-v1.json'
MAX = (1 << 64) - 1


def refresh_unmoved(data):
    """Two living ordinary units; both actor choices unused, no effects/restrictions."""
    word, count, ticks = data['word'], data['count'], data['ticks']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and
            0 <= count <= MAX and type(ticks) is int and 0 <= ticks <= MAX, 'Counter domain')
    require(data['moved'] is False and data['acted'] is False, 'Fresh unused choices')
    units = sorted((dict(u) for u in data['units']), key=lambda u: u['tieKey'])
    require(len(units) == 2 and len({u['tieKey'] for u in units}) == 2 and
            len({u['id'] for u in units}) == 2, 'Two distinct ordered units')
    require(all(type(u['ct']) is int and 0 <= u['ct'] <= 254 and type(u['speed']) is int
                and 1 <= u['speed'] <= 255 for u in units), 'Clock domain')
    actor = next((u for u in units if u['id'] == data['actor']), None)
    require(actor is not None and actor['ct'] < 100, 'Active ordinary actor CT')
    end_ct = min(60, actor['ct'] + 40)  # u16 source intermediate; both refunds before cap.
    actor['ct'] = end_ct
    clock = first_clock(dict(units=[dict(slot=i, ct=u['ct'], speed=u['speed'], status='None',
        moved=False, acted=False) for i, u in enumerate(units)]))
    if count == MAX or ticks > MAX - clock['elapsedTicks']:
        return dict(accepted=False, word=word, count=count)
    require(clock['upkeepDraws'] == 1, 'Closed living refresh upkeep')
    final_word, raw = draws(word, 1)
    return dict(accepted=True, endCt=end_ct, picked=units[clock['readySlot']]['id'],
                elapsed=clock['elapsedTicks'], ticks=ticks + clock['elapsedTicks'],
                ct={units[u['slot']]['id']: u['ct'] for u in clock['units']},
                raw=raw[0], word=final_word, count=count + 1)


def reachable_unmoved(data):
    """Reuse the independently traced flat movement description, without a move/score/draw."""
    require(data['moved'] is False and type(data['move']) is int and 0 <= data['move'] <= 124,
            'Unmoved ordinary raw Move envelope')
    result = flat_route(dict(data, destination=data['origin']))
    return dict(origins=result['aiOrigins'], rngDraws=result['rngDraws'])


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['format'] == 'fft.ai-unmoved-preparation-contract' and fixture['schemaVersion'] == 1 and
            fixture['ticket'] == evidence['ticket'] == 'ZOD-171' and
            fixture['referenceRevision'] == evidence['referenceRevision'] and
            fixture['productionImplemented'] is False, 'Source-only fixture boundary')
    ids = []
    for family, calculate in [('clockVectors', refresh_unmoved), ('reachVectors', reachable_unmoved)]:
        for row in fixture[family]:
            require(calculate(row['input']) == row['expected'], 'Unmoved vector: ' + row['id'])
            ids.append(row['id'])
    require(len(ids) == len(set(ids)), 'Unique vector IDs')
    return dict(unmovedClockVectors=len(fixture['clockVectors']), unmovedReachVectors=len(fixture['reachVectors']))


def audit(reference):
    report = audit_continuation(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Unmoved reference')
    for row in evidence['additionalSourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source mismatch: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(actual['normalizedLfSha256'] == row['normalizedLfSha256'], 'Function mismatch: ' + row['symbol'])
    vectors = validate(json.loads(FIXTURE.read_text(encoding='utf-8')))
    return dict(**report, **vectors, unmovedFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
