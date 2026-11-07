"""Audit pinned movement-scenario source functions; never executes original/C code."""
import argparse
import json
from pathlib import Path

from verify_ai_selection_contract import audit as audit_selection_source
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/movement-scenario-contract-v1.json'


def audit(reference, out_of_reach=False):
    report = audit_selection_source(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Scenario reference revision')
    for entry in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'],
                'Function mismatch: ' + entry['symbol'])
    result = dict(**report, scenarioFunctions=len(evidence['sourceFunctions']))
    if out_of_reach:
        extension = json.loads((ROOT / 'docs/evidence/ai/out-of-reach-scenario-contract-v1.json').read_text(encoding='utf-8'))
        require(extension['referenceRevision'] == report['referenceRevision'], 'Out-of-reach reference revision')
        for entry in extension['additionalSourceFiles']:
            require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
        for entry in extension['additionalSourceFunctions']:
            actual = function_fingerprint(reference / entry['path'], entry['symbol'])
            require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
        result['outOfReachAdditionalFiles'] = len(extension['additionalSourceFiles'])
        result['outOfReachAdditionalFunctions'] = len(extension['additionalSourceFunctions'])
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    parser.add_argument('--out-of-reach', action='store_true', help='Also audit the ZOD-146 extension receipt')
    args = parser.parse_args()
    report = audit(args.reference, args.out_of_reach)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
