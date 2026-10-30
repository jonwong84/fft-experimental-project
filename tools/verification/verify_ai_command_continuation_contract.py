"""ZOD-165 pinned command-builder branch descriptions; no managed/C/original execution."""
import argparse
import json
from pathlib import Path

from verify_ai_preparation_contract import audit as audit_preparation
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/command-continuation-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-command-continuation-contract-v1.json'


def dispatch(data):
    """Selected-stage scalars only; selection-required stops before the source refresh/search."""
    decided = data['decided']
    require(type(decided) is int and 0 <= decided <= 255, 'Source decided byte')
    moved, acted = data['current']
    if data['stage'] == 'entry':
        if decided == 255 and data['current'] == data['saved']:
            return dict(kind='EndTurn', recordCommand=False, selectionRequired=False)
        if decided == 0 or acted:
            return dict(kind='SelectionRequired', recordCommand=False, selectionRequired=True)
    else:
        require(data['stage'] == 'after-selection' and decided == 255, 'Fresh selected-stage description')
    kind = ('Move' if not moved and (acted or not data['actFirst']) and not data['sameTile']
            else 'EndTurn' if acted or not data['hasSkillset'] else 'Attack')
    return dict(kind=kind, recordCommand=True, selectionRequired=False)


def audit(reference):
    report = audit_preparation(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Continuation reference')
    for entry in evidence['additionalSourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: '+entry['path'])
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: '+entry['symbol'])
    fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
    require(fixture['referenceRevision'] == report['referenceRevision'], 'Vector reference')
    for row in fixture['vectors']:
        require(dispatch(row['input']) == row['expected'], 'Dispatch vector: '+row['id'])
    return dict(**report, continuationFunctions=len(evidence['additionalSourceFunctions']),
                continuationVectors=len(fixture['vectors']))


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
