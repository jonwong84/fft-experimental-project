"""ZOD-135 offline rank kernels/source receipt; no C or managed AI execution."""
import argparse
import copy
import json
from pathlib import Path
import subprocess

from verify_ai_decision_contract import rank_compare, signed16
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

EVIDENCE = 'docs/evidence/ai/rank-admission-contract-v1.json'
ROOT = Path(__file__).resolve().parents[2]


def move_only_end_ct(ct, already_moved, destination_changed):
    """Unused action, no restrictions; supplied coordinates have canonical padding."""
    return min(60, ct + 20 + (0 if already_moved or destination_changed else 20))


def baseline_rank(tile_pattern):
    return dict(decided=0, priority=signed16(-tile_pattern), hit=100)


def higher(first, second):
    comparison = rank_compare(first, second)
    return comparison > 0 or comparison == 0 and first['proximity'] < second['proximity']


def insert_best(best, baseline, ranked):
    """Isolated supplied-record kernel, NOT a public managed cached-score API.

    The diagnostic dictionaries flatten rank and proximity; opaque payload stands
    for the full copied action. None models absence in the admitted managed slice.
    """
    require(len(ranked) == 8, 'Eight source slots')
    result = copy.deepcopy(ranked)
    inserted = None
    if best is not None and rank_compare(best, baseline) > 0 and higher(best, result[-1]):
        for index, entry in enumerate(result):
            if higher(best, entry):
                result.insert(index, copy.deepcopy(best))
                result.pop()
                inserted = index
                break
    reset = None
    if best is not None:
        reset = copy.deepcopy(best)
        reset.update(decided=0, priority=-32767, hit=0, waitFacing=5, reflected=False)
    return dict(ranked=result, insertedIndex=inserted, resetBest=reset, additionalDraws=0)


def audit(reference):
    evidence = json.loads((ROOT / EVIDENCE).read_text(encoding='utf-8'))
    revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
    require(revision == evidence['referenceRevision'], 'Reference revision mismatch')
    require(not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(),
            'Reference must be clean')
    for entry in evidence['sourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
    for entry in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    return dict(referenceRevision=revision, referenceClean=True, files=len(evidence['sourceFiles']),
                functions=len(evidence['sourceFunctions']), originalExecuted=False, cExecuted=False,
                managedExecuted=False)


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
