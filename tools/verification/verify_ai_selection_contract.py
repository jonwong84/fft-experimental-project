"""ZOD-139 offline lottery/fallback descriptions; no fresh C# selection or C execution."""
import argparse
import copy
import json
from pathlib import Path

from verify_ai_composition_contract import audit as audit_composition_source
from verify_ai_decision_contract import calculate
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
MAX = (1 << 64) - 1
EVIDENCE = 'docs/evidence/ai/selection-contract-v1.json'


def lottery_tail(seed, count, hit_percent):
    """One SUPPLIED positive-hit ranked entry or explicit absence; not a composition runner.

    Counter exhaustion/rollback is a managed design proposal. The old isolated
    rank kernel supplies source modulo/strict-compare/visited-slot behavior.
    """
    require(type(seed) is int and 0 <= seed <= 0xffffffff and type(count) is int and 0 <= count <= MAX, 'RNG domain')
    require(hit_percent is None or type(hit_percent) is int and 1 <= hit_percent <= 100, 'Absent or positive-hit admitted entry')
    result = calculate('ranked-lottery', dict(seed=seed, chances=[hit_percent or 0] + [0] * 7))
    if count > MAX - result['draws']:
        return dict(accepted=False, failedLotteryIndex=MAX-count, outcome=None, rngWord=seed, rngCount=count)
    reason = 'NoRankedAction' if result['selected'] is None else 'AllRollsFailedUseFirst' if result['fallback'] else 'FirstRoll'
    return dict(accepted=True, failedLotteryIndex=None, outcome=dict(selectedIndex=result['selected'], reason=reason,
        raw=result['raw'], draws=result['draws']), rngWord=result['rngWord'], rngCount=count+result['draws'])


def source_fallback(selected, destination, support_id):
    """Source effective writes only; all other raw fields remain unchanged, including stale payload.

    Defend is documented here but excluded from the proposed no-Defend managed slice.
    """
    result = copy.deepcopy(selected)
    result['actBeforeMove'] = False
    result['coords'] = copy.deepcopy(destination)
    result['command']['skillset'] = 2 if support_id == 479 else 0
    return result


def audit(reference):
    report = audit_composition_source(reference)
    evidence = json.loads((ROOT / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Selection reference revision')
    for entry in evidence['additionalSourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    return dict(**report, selectionHeaders=len(evidence['additionalSourceFiles']),
                selectionFunctions=len(evidence['additionalSourceFunctions']),
                totalReferenceFiles=report['files']+len(evidence['additionalSourceFiles']),
                totalReferenceFunctions=report['functions']+report['additionalFunctions']+len(evidence['additionalSourceFunctions']))


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
