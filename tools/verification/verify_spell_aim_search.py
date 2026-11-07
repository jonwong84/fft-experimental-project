"""ZOD-418 source descriptions, not a runtime AI implementation or a C/original oracle."""
import argparse
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/spell-aim-search-v1.json'
RECEIPT = ROOT / 'docs/evidence/ai/spell-aim-search-source-v1.json'
ROW_MASKS = (0x0100, 0x0380, 0x07c0, 0x0fe0, 0x1ff0, 0x3ff8, 0x7ffc, 0xfffe)


def require(condition, message):
    if not condition:
        raise ValueError(message)


def aim_panels(data):
    width, depth = data['width'], data['depth']
    radius = 0 if data['targeting'] == 6 else data['area']
    require(data['targeting'] in (5, 6) and 0 <= data['area'] <= 7, 'Bounded source mask radius')
    require(1 <= width <= 16 and 1 <= depth <= 16, 'Source mask dimensions')
    rows = [[0] * depth for _ in range(2)]
    for tx, ty, layer in data['targets']:
        require(0 <= tx < width and 0 <= ty < depth and layer in (0, 1), 'Target coordinates')
        for y in range(max(0, ty - radius), min(depth, ty + radius + 1)):
            template = ROW_MASKS[radius - abs(y - ty)]
            bits = (template << (7 - tx) if tx < 8 else template >> (tx - 7)) & 0xffff
            rows[0][y] |= bits
            rows[1][y] |= bits
    if data['follow'] and data['targeting'] == 5:
        for x, y, layer in data['targets']:
            rows[layer][y] ^= 0x8000 >> x
    walkable = set(map(tuple, data['walkable']))
    in_range = set(map(tuple, data['rangePanels']))
    return [[x, y, layer] for layer in range(2) for y in range(depth) for x in range(width)
            if rows[layer][y] & (0x8000 >> x) and (x, y, layer) in walkable & in_range]


def targeting_passes(data):
    stages = []
    if data['follow']:
        stages.extend(['unit'] if data['consume'] else ['unit-moves-by-deadline', 'unit-still-at-deadline'])
        if data['area'] == 0:
            return stages
    return stages + ['tile']


def rank_description(data):
    a, b = data['candidate'], data['incumbent']
    for value in (a, b):
        require(-32768 <= value['priority'] <= 32767 and 0 <= value['hit'] <= 255 and
                0 <= value['proximity'] <= 255, 'Source ranking widths')
    first, second = (a['priority'], a['hit']), (b['priority'], b['hit'])
    return dict(higherWithProximity=first > second or first == second and a['proximity'] < b['proximity'],
                beatsMovementBaseline=first > second)


def validate(document):
    require(document.get('schemaVersion') == 1 and document.get('originalExecuted') is False and
            document.get('cExecuted') is False, 'Source-only description provenance')
    require(document['referenceRevision'] == REVISION, 'Pinned source revision')
    owners = {'masks': aim_panels, 'passes': targeting_passes, 'ranks': rank_description}
    seen = set()
    for kind, describe in owners.items():
        for row in document[kind]:
            require(row['id'] not in seen, 'Unique vector identity')
            seen.add(row['id'])
            require(describe(row['input']) == row['expected'], 'Source description: ' + row['id'])
    return dict(aimMaskDescriptions=len(document['masks']), targetingPassDescriptions=len(document['passes']),
                rankDescriptions=len(document['ranks']), originalExecuted=False, cExecuted=False)


def audit(reference):
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Revision')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean reference')
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
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
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2)
            stream.write('\n')
    print(json.dumps(result))
