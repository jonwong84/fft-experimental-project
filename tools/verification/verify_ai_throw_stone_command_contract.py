"""ZOD-203 supplied execution-checkpoint descriptions; no C, C# or authoritative transaction execution."""
import argparse
import json
from pathlib import Path

from verify_ai_pair_out_of_reach_contract import audit as audit_pair
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/throw-stone-command-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-throw-stone-command-contract-v1.json'


def execution(data):
    """Closed flat two-unit execution arithmetic from a supplied RNG checkpoint, before facing/turn orchestration.

    No ability-list search, grants/adoption, movement path, actual command, receipt, clock or frame work is executed.
    Failure rollback describes the proposed managed boundary; it is not original C rewind.
    """
    def integer(value, low, high):
        require(type(value) is int and low <= value <= high, 'Closed integer domain')
        return value
    width, depth = [integer(v, 1, 16) for v in data['mapSize']]
    def tile(value):
        require(len(value) == 2, 'Logical lower-layer tile')
        return [integer(value[0], 0, width - 1), integer(value[1], 0, depth - 1)]
    actor, target = tile(data['actor']), tile(data['target'])
    dx, dy = target[0] - actor[0], target[1] - actor[1]
    require(1 <= abs(dx) + abs(dy) <= 4, 'Admitted cast range')
    pa, hp = integer(data['pa'], 1, 99), integer(data['hp'], 1, 65535)
    evade = integer(data['classEvade'], 0, 100)
    brave_a, brave_b = integer(data['actorBrave'], 10, 100), integer(data['targetBrave'], 10, 100)
    facing = data['targetFacing']; require(facing in ('East', 'North', 'West', 'South'), 'Facing')
    horizontal = dx > 0 and facing == 'West' or dx < 0 and facing == 'East'
    vertical = dy > 0 and facing == 'South' or dy < 0 and facing == 'North'
    front = horizontal if abs(dx) > abs(dy) else vertical if abs(dy) > abs(dx) else horizontal or vertical
    word, count = integer(data['word'], 0, 0xffffffff), integer(data['count'], 0, 0xffffffffffffffff)
    raw = []
    def draw():
        nonlocal word, count
        if count == 0xffffffffffffffff: raise OverflowError
        word = (word * 1103515245 + 12345) & 0xffffffff; count += 1
        value = (word >> 16) & 32767; raw.append(value)
        return value
    try:
        hit = True
        for chance in (0, 0, 0, evade if front else 0):
            if draw() * 100 // 32768 < chance: hit = False; break
        damage, attempted = 0, False
        if hit:
            damage = (draw() * 2 // 32768 + 1) * pa
            attacker_roll, target_roll = draw(), draw()
            attempted = target_roll * brave_b // 32768 < attacker_roll * brave_a // 32768
    except OverflowError:
        return dict(accepted=False, rng=[data['word'], data['count']], outcome=None)
    remaining = max(0, hp - damage); destination = target.copy()
    if attempted and remaining:
        step = [target[0] + (1 if dx > 0 else -1), target[1]] if abs(dx) > abs(dy) else [target[0], target[1] + (1 if dy > 0 else -1)]
        if 0 <= step[0] < width and 0 <= step[1] < depth and step != actor: destination = step
    return dict(accepted=True, rng=[word, count], outcome=dict(hit=hit, damage=damage, hp=remaining,
                attempted=attempted, destination=destination, raw=raw))


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-203' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False and
            fixture['kind'] == 'supplied-command-checkpoint-descriptions', 'Source/design-only fixture')
    ids = []
    for row in fixture['vectors']:
        require(execution(row['input']) == row['expected'], 'Execution checkpoint: ' + row['id']); ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique descriptions')
    return dict(throwStoneCommandDescriptions=len(ids))


def audit(reference, prepared=None):
    report = audit_pair(reference, prepared); evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Command reference revision')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Command source file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol'])['normalizedLfSha256'] == row['normalizedLfSha256'],
                'Command source function: ' + row['symbol'])
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                throwStoneCommandFiles=len(evidence['sourceFiles']), throwStoneCommandFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True); parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path); args = parser.parse_args(); report = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream: json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
