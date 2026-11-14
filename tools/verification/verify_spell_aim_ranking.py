"""Source descriptions for ZOD-425 fixed-origin candidate iteration, not a formula or original-execution oracle."""
import argparse
import json
from pathlib import Path
import subprocess
from verify_spell_aim_search import require
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/spell-aim-ranking-v1.json'
RECEIPT = ROOT / 'docs/evidence/ai/spell-aim-ranking-source-v1.json'


def trace(passes):
    best, rank, calls, stopped, draws, seed = None, (-32767, 0), [], [], 0, 57
    for p, candidates in enumerate(passes):
        for i, candidate in enumerate(candidates):
            status = candidate['status']
            require(status in ('scored', 'not-valuable', 'unresolved'), 'Candidate disposition')
            require(0 <= candidate['draws'] <= 1024, 'Bounded description draw count')
            calls.append([p, i])
            for _ in range(candidate['draws']):
                seed = (seed * 0x41c64e6d + 0x3039) & 0xffffffff
            draws += candidate['draws']
            if status != 'not-valuable':
                key = candidate['priority'], candidate['hit']
                require(-32768 <= key[0] <= 32767 and
                        (0 <= key[1] <= 100 if status == 'scored' else key[1] == 255), 'Fresh source rank widths')
                if key > rank:
                    best, rank = [p, i], key
            if status == 'unresolved':
                stopped.append(p)
                break
    return dict(best=best, calls=calls, stopped=stopped, draws=draws, seed=seed)


def validate(document):
    require(document['schemaVersion'] == 1 and document['referenceRevision'] == REVISION and
            document['originalExecuted'] is False and document['cExecuted'] is False, 'Pinned source-only provenance')
    require(len({r['id'] for r in document['traces']}) == len(document['traces']), 'Unique descriptions')
    for row in document['traces']:
        require(trace(row['passes']) == row['expected'], 'Source description: ' + row['id'])
    return dict(rankIterationDescriptions=len(document['traces']), originalExecuted=False, cExecuted=False)


def audit(reference):
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Revision')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean reference')
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
    require(receipt['referenceRevision'] == REVISION, 'Source receipt revision')
    for row in receipt['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source file ' + row['path'])
    for row in receipt['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol']) ==
                {k: v for k, v in row.items() if k != 'path'}, 'Source function ' + row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))), referenceRevision=REVISION,
                sourceFiles=len(receipt['sourceFiles']), sourceFunctions=len(receipt['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', required=True, type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(result))
