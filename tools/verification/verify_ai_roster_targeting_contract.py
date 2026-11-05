"""ZOD-271 isolated roster descriptions, not original/C/managed execution or a status simulator."""
import argparse
import json
from pathlib import Path
import subprocess

from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/roster-targeting-source-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-roster-targeting-contract-v1.json'
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'
FACTS = {'present', 'crystal', 'treasure', 'mount', 'transparent', 'rider', 'riderMountTransparent',
         'petrified', 'bloodSuck', 'confused', 'dead', 'critical', 'deathSentence',
         'enemy', 'untargetable', 'deadWithoutReraise', 'considered', 'disabledThroughActorDelay'}


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, 'Integer fact domain')
    return value


def position(value):
    require(type(value) is list and len(value) == 3, 'Three coordinates')
    return tuple(integer(v, 0, 255) for v in value)


def flag(unit, name):
    return unit.get(name, name == 'present')


def live_excluded(u):
    return (not flag(u, 'present') or any(flag(u, k) for k in ('crystal', 'treasure', 'mount'))
            or flag(u, 'enemy') and flag(u, 'transparent') and
            (not flag(u, 'rider') or not flag(u, 'riderMountTransparent')))


def out_of_play(u):
    return any(flag(u, k) for k in ('untargetable', 'deadWithoutReraise', 'petrified'))


def distance(first, second):
    return abs(first[0] - second[0]) + abs(first[1] - second[1])


def describe(kind, data):
    units = data['units']; require(type(units) is list and 1 <= len(units) <= 21, 'Original source-description slot extent')
    ids, orders = [], []
    for unit in units:
        require(isinstance(unit['id'], str) and bool(unit['id']), 'Explicit ID')
        ids.append(unit['id']); orders.append(integer(unit['order'], 0, 0x7fffffff))
        position(unit['position']); integer(unit.get('delay', 0), 0, 0x7fffffff)
        for name in FACTS & unit.keys(): require(type(unit[name]) is bool, 'Boolean source fact')
    require(len(set(ids)) == len(ids) and len(set(orders)) == len(orders) and data['actor'] in ids, 'Distinct ordered units and represented actor')
    cached = integer(data['cachedActorDelay'], 0, 255)
    units = sorted(units, key=lambda u: u['order']); actor = next(u for u in units if u['id'] == data['actor'])
    if kind == 'membership':
        return [dict(id=u['id'], liveUntargetable=live_excluded(u), outOfPlay=out_of_play(u)) for u in units]
    if kind == 'nearest':
        members = data['targetable']; require(type(members) is list and set(members) <= set(ids), 'Resolved working membership')
        eligible = [u for u in units if not live_excluded(u) and not flag(u, 'petrified') and flag(u, 'enemy')
                    and not flag(u, 'deadWithoutReraise') and u['id'] in members and flag(u, 'considered') and not flag(u, 'bloodSuck')]
        picked = min(eligible, key=lambda u: distance(actor['position'], u['position']), default=None)
        return dict(target=picked['id'] if picked else data['actor'],
                    distance=distance(actor['position'], picked['position']) if picked else None,
                    eligible=[u['id'] for u in eligible])
    if kind == 'active':
        mode = integer(data['mode'], -0x80000000, 0x7fffffff)
        require(type(data['excludeActor']) is bool, 'Actor exclusion fact')
        selected, inactive = [], []
        for u in units:
            active = not flag(u, 'untargetable') and (flag(u, 'considered') or flag(u, 'confused'))
            active &= not (data['excludeActor'] and u['id'] == data['actor'])
            if mode == 1: active &= flag(u, 'enemy')
            elif mode == 2: active &= not flag(u, 'enemy')
            elif mode == 3: active &= u['id'] == data['specific']
            elif mode == 4: active &= any(flag(u, k) for k in ('dead', 'petrified', 'critical', 'deathSentence'))
            (selected if active else inactive).append(u['id'])
        return dict(active=selected, inactive=inactive)
    if kind == 'proximity':
        enemies = [u for u in units if not out_of_play(u) and flag(u, 'enemy') and
                   u.get('delay', 0) <= cached and not flag(u, 'disabledThroughActorDelay')]
        tiles = sorted(set(position(p) for p in data['scenario0']), key=lambda p: (p[2], p[1], p[0]))
        return dict(enemies=[u['id'] for u in enemies], writes=[dict(tile=list(p),
            value=sum(max(0, 10-distance(p, u['position'])) for u in enemies) & 255) for p in tiles])
    raise ValueError('Unknown roster description kind')


def validate(fixture):
    require(fixture['schemaVersion'] == 1 and fixture['ticket'] == 'ZOD-271' and fixture['referenceRevision'] == REVISION
            and fixture['kind'] == 'isolated-source-roster-descriptions' and fixture['productionImplemented'] is False,
            'Source-only roster receipt')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['kind'], row['input']) == row['expected'], 'Roster description: ' + row['id'])
        ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique roster description IDs')
    return dict(rosterDescriptions=len(ids), originalExecuted=False, cExecuted=False, managedExecuted=False)


def audit(reference):
    receipt = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(receipt['referenceRevision'] == subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Pinned reference')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean read-only reference')
    for row in receipt['sourceFiles']: require(fingerprint(reference, row['path']) == row, 'Source file: ' + row['path'])
    for row in receipt['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(all(row[k] == v for k, v in actual.items()), 'Source function: ' + row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))), referenceRevision=REVISION, referenceClean=True,
                sourceFiles=len(receipt['sourceFiles']), sourceFunctions=len(receipt['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True); parser.add_argument('--output', type=Path)
    args = parser.parse_args(); report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream: json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
