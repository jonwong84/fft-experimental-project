"""ZOD-424 source descriptions for the certified fixed-origin aim-list boundary; never executes C."""
import argparse
import json
from pathlib import Path
import subprocess
from verify_spell_aim_search import aim_panels, require
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/spell-aim-enumeration-v1.json'
RECEIPT = ROOT / 'docs/evidence/ai/spell-aim-enumeration-source-v1.json'


def usage(data):
    require(0 <= data['ct'] <= 255 and data['actorDelay'] >= 0, 'Considered CT and actor delay')
    return data['force'] or not data['exhaustive'] and (data['ct'] >= data['actorDelay'] or data['selfNone'])


def deadline(data):
    ct, speed, charge = data['ct'], data['speed'], data['charge']
    require(0 <= ct <= 254 and 1 <= speed <= 50 and 0 <= charge <= 255, 'Ordinary living clock inputs')
    delay = max(0, 100 - ct) // speed + (max(0, 100 - ct) % speed != 0)
    return dict(delay=delay, moves=charge != 0 and charge >= delay)


def mask(data):
    require(data['range'] > 0 and data['range'] + data['area'] <= 7, 'Certified source row-template radii')
    panels = [[x, y, 0] for y in range(data['depth']) for x in range(data['width'])]
    cx, cy, _ = data['caster']
    selected = [p for p in panels if abs(p[0] - cx) + abs(p[1] - cy) <= data['range']]
    return aim_panels(dict(data, walkable=panels, rangePanels=selected))


def validate(document):
    require(document['schemaVersion'] == 1 and document['referenceRevision'] == REVISION and
            document['originalExecuted'] is False and document['cExecuted'] is False, 'Pinned source-only provenance')
    seen = set()
    for kind, describe in dict(usage=usage, deadlines=deadline, masks=mask).items():
        for row in document[kind]:
            require(row['id'] not in seen, 'Unique source vector')
            seen.add(row['id'])
            require(describe(row['input']) == row['expected'], 'Source description: ' + row['id'])
    return dict(usageDescriptions=len(document['usage']), deadlineDescriptions=len(document['deadlines']),
                maskDescriptions=len(document['masks']), originalExecuted=False, cExecuted=False)


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
