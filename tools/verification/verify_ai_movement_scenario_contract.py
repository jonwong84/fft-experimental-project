"""Audit pinned movement-scenario source functions; never executes original/C code."""
import argparse
import json
from pathlib import Path

from verify_ai_selection_contract import audit as audit_selection_source
from verify_sword_knockback_contract import function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/movement-scenario-contract-v1.json'


def audit(reference):
    report = audit_selection_source(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Scenario reference revision')
    for entry in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'],
                'Function mismatch: ' + entry['symbol'])
    return dict(**report, scenarioFunctions=len(evidence['sourceFunctions']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
