"""ZOD-176 source/ACT-first supplied-cost descriptions; no C or managed execution."""
import argparse
import json
from pathlib import Path

from verify_ai_command_continuation_contract import dispatch
from verify_ai_decision_contract import draws
from verify_ai_execution_contract import tape
from verify_ai_unmoved_preparation_contract import audit as audit_unmoved
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/act-first-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-act-first-contract-v1.json'


def act_first_tape(data):
    """The fixture supplies four movement cells and one five-draw projected Attack, not a search oracle."""
    require(data['entry'] == dict(word=1, count=7), 'Authored tape entry')
    word, _ = draws(1, 1)  # One retained fresh refresh draw before the supplied selection costs.
    return tape(dict(data, entry=dict(word=word, count=8)))


def post_act_facts(data):
    """Closed living unrestricted actor, Move unused/ACT used. Reaches are computed before usable is cleared."""
    ct, move = data['ct'], data['move']
    require(data['moved'] is False and data['acted'] is True, 'Post-ACT unused Move')
    require(type(ct) is int and 0 <= ct < 100 and type(move) is int and 0 <= move <= 124, 'Ordinary envelope')
    return dict(refreshEndCt=min(60, ct + 20), movingEndCt=min(60, ct), stayingEndCt=min(60, ct + 20),
                enemyReach=move + 1, attackUsable=False, baselineCopiesAttackDestination=True)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-176' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False,
            'Source-only ACT-first fixture')
    ids = []
    for family, calculate in [('dispatchVectors', dispatch), ('postActVectors', post_act_facts), ('tapes', act_first_tape)]:
        for row in fixture[family]:
            require(calculate(row['input']) == row['expected'], 'ACT-first vector: ' + row['id'])
            ids.append(row['id'])
    require(len(ids) == len(set(ids)), 'Unique ACT-first vector IDs')
    return dict(actFirstDispatchVectors=len(fixture['dispatchVectors']), postActFactVectors=len(fixture['postActVectors']),
                actFirstSuppliedTapes=len(fixture['tapes']))


def audit(reference):
    report = audit_unmoved(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'ACT-first reference')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source mismatch: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(actual['normalizedLfSha256'] == row['normalizedLfSha256'], 'Function mismatch: ' + row['symbol'])
    vectors = validate(json.loads(FIXTURE.read_text(encoding='utf-8')))
    return dict(**report, **vectors, actFirstFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
