"""Offline ZOD-116 helper/description audit; not an executable FFT AI or C oracle."""
import argparse
import hashlib
import json
from pathlib import Path
import struct
import subprocess

from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/ai-decision-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/decision-contract-v1.json'


def verify_prepared_record(path, record):
    data = path.read_bytes()
    require(len(data) == record['exeByteLength'] and hashlib.sha256(data).hexdigest() == record['exeSha256'], 'Prepared EXE identity')
    require(data[:8] == b'PS-X EXE', 'PS-X EXE header')
    load, payload = struct.unpack_from('<II', data, 0x18)
    require(load == record['loadAddress'] and payload == record['payloadByteLength'], 'Payload bounds')
    offset = record['address'] - load + 0x800
    require(offset >= 0x800 and offset + record['byteLength'] <= min(len(data), 0x800 + payload), 'Record bounds')
    require(hashlib.sha256(data[offset:offset + record['byteLength']]).hexdigest() == record['recordSha256'], 'Attack metadata identity')


def draws(seed, count):
    word = seed
    values = []
    for _ in range(count):
        word = (word * 1103515245 + 12345) & 0xffffffff
        values.append((word >> 16) & 32767)
    return word, values


def signed16(value):
    return ((value + 32768) & 65535) - 32768


def rank_compare(first, second):
    """Rank layout is decided:u8, priority:s16, hit:u8; proximity is a separate tie."""
    def key(row):
        return row['decided'], signed16(row['priority']), row['hit']
    return (key(first) > key(second)) - (key(first) < key(second))


def direction(origin, target):
    # Admitted source map coordinates are below signed-byte wrap, with no overlap.
    require(all(type(value) is int and 0 <= value <= 17 for value in origin + target), 'Coordinate domain')
    x, y = target[0] - origin[0], target[1] - origin[1]
    if y - x <= 0 and y + x < 0:
        return 'South'
    if y - x > 0 and y + x <= 0:
        return 'West'
    if y - x >= 0 and y + x > 0:
        return 'North'
    if y - x < 0 and y + x >= 0:
        return 'East'
    return 'Overlap'


def calculate(family, data):
    if family == 'rank':
        comparison = rank_compare(data['first'], data['second'])
        return dict(comparison=comparison, firstIsHigher=comparison > 0 or
                    comparison == 0 and data['first']['proximity'] < data['second']['proximity'])
    if family == 'destination':
        # Supplied cells are already in source level/y/x scan order.
        best = None
        for tile in data['tiles']:
            if tile['distance'] > data['limit']:
                continue
            score = signed16(tile['priority']) * 65536 + tile['proximity']
            key = (score, tile['distance']) if data['order'] == 'priority-first' else (tile['distance'], score)
            if best is None or key <= best[0]:
                best = (key, tile['id'])
        return dict(selected=None if best is None else best[1])
    if family == 'ranked-lottery':
        chances = data['chances']
        require(len(chances) == 8 and all(type(chance) is int and 0 <= chance <= 100 for chance in chances), 'Eight ranked slots')
        word, raw = draws(data['seed'], 8)
        for index, chance in enumerate(chances):
            if raw[index] % 100 < chance:
                actual_word, used = draws(data['seed'], index + 1)
                return dict(selected=index, fallback=False, rngWord=actual_word, draws=len(used), raw=used)
        return dict(selected=0 if any(chances) else None, fallback=any(chances), rngWord=word, draws=8, raw=raw)
    if family == 'sword-formula-projection':
        # Formula-level closed slice only: four real evade draws, no execution crit/proc.
        pa, power, hp = data['pa'], data['power'], data['hp']
        require(0 <= pa <= 255 and 0 <= power <= 255 and (2 * max(1, pa) - 1) * power <= 32767, 'Sword envelope')
        require(0 < hp <= 65535 and 0 <= data['evade'] <= 100, 'Projection domain')
        chance = 100 - (data['evade'] if data['front'] else 0)
        damage = min(999, max(1, pa) * power) if chance else 0
        word, raw = draws(data['seed'], 4)
        return dict(hitPercent=chance, damage=damage, hp=max(0, hp - damage),
                    critical=False, knockback=False, rngWord=word, resolutionDraws=4, raw=raw)
    if family == 'state-score':
        total = 0
        for unit in data['units']:
            require(0 <= unit['hp'] <= unit['maxHp'] and unit['maxHp'] > 0, 'HP domain')
            # Given post-simulation status; this helper does not implement status transitions.
            weight = {'None': 0, 'Critical': -32, 'Dead': -192}[unit['status']]
            score = unit['hp'] * 128 // unit['maxHp'] + weight + unit['keptEquipmentSlots'] * 51
            total = signed16(total + (-score if unit['enemy'] else score))
        return dict(priority=total)
    if family == 'command-dispatch':
        if not data['compactRecordsValid'] or data['decided'] and data['flagsUnchanged']:
            return dict(command='EndTurn', redecide=False)
        redecide = not data['decided'] or data['acted']
        if not data['moved'] and (data['acted'] or not data['actBeforeMove']) and not data['sameTile']:
            return dict(command='Move', redecide=redecide)
        return dict(command='EndTurn' if data['acted'] or not data['hasSkillset'] else 'Act', redecide=redecide)
    if family == 'wait-facing':
        if data['hint'] != 'ChooseAtWait':
            return dict(facing=data['current'] if data['hint'] == 'KeepCurrent' else data['hint'])
        chosen = direction(data['actor'], data['enemy'])
        require(chosen != 'Overlap', 'Original overlap lookup is unresolved stack-byte behavior')
        require(data['viable'], 'Vector must provide at least one viable direction')
        return dict(facing=chosen if chosen in data['viable'] else data['current']
                    if data['current'] in data['viable'] else data['viable'][-1])
    if family == 'reach-randomization':
        if not data['nonPlayerTeam'] or data['inReach']:
            return dict(reach=data['reach'], draws=0, rngWord=data['seed'])
        word, raw = draws(data['seed'], 1)
        return dict(reach=data['reach'] // 2 if raw[0] % 7 == 0 else data['reach'], draws=1, rngWord=word)
    raise ValueError('Unsupported helper family: ' + family)


def validate(contract):
    require(contract['format'] == 'fft.ai-decision-source-description' and contract['schemaVersion'] == 1, 'Description format')
    require(contract['referenceRevision'] == REVISION and contract['execution'] == 'offline-helper-description', 'Reference/execution')
    require(contract['fullAiExecuted'] is False and contract['productionInterfaceImplemented'] is False, 'Implementation claims')
    seen = set()
    families = set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector')
        seen.add(row['id']); families.add(row['family'])
        require(calculate(row['family'], row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
    require(families == {'rank', 'destination', 'ranked-lottery', 'sword-formula-projection', 'state-score',
                         'command-dispatch', 'wait-facing', 'reach-randomization'}, 'Family inventory')
    require(len(seen) == 50, 'Vector inventory')
    cases = contract['implementationCases']
    require([row['id'] for row in cases] == [f'AIC-{n:03}' for n in range(1, 21)] and
            all(row['status'] == 'unexecuted' for row in cases), 'Unexecuted implementation inventory')
    return dict(vectors=len(seen), families=len(families), implementationCasesUnexecuted=len(cases))


def audit(root, reference=None, check_managed_baseline=False, prepared_exe=None):
    evidence = json.loads((root / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket'] == 'ZOD-116' and evidence['reference']['revision'] == REVISION, 'Evidence identity')
    require(evidence['fixture'] == fingerprint(root, FIXTURE), 'Fixture identity')
    result = validate(json.loads((root / FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']:
        require(fingerprint(root, row['path']) == row, 'Historical file changed: ' + row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']), managedBaselineFilesChecked=0,
                  referenceFilesChecked=0, referenceFunctionsChecked=0, retailRecordsChecked=0)
    if prepared_exe is not None:
        verify_prepared_record(prepared_exe, evidence['retailAttackMetadata'])
        result['retailRecordsChecked'] = 1
    if check_managed_baseline:
        for row in evidence['managedBaseline']:
            require(fingerprint(root, row['path']) == row, 'Managed baseline changed: ' + row['path'])
        result['managedBaselineFilesChecked'] = len(evidence['managedBaseline'])
    if reference is not None:
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        status = subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference, row['path']) == {key: value for key, value in row.items() if key != 'functions'},
                    'Reference file changed: ' + row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference / row['path'], function['symbol']) == function,
                        'Reference function changed: ' + function['symbol'])
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    result.update(format='fft.ai-decision-source-audit', schemaVersion=1, passed=True,
                  fullAiExecuted=False, productionInterfaceImplemented=False, originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path)
    parser.add_argument('--check-managed-baseline', action='store_true')
    parser.add_argument('--prepared-exe', type=Path, help='Optional verified prepared SCUS-94221; read eight-byte metadata only, never execute')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2], args.reference, args.check_managed_baseline, args.prepared_exe)
    with args.output.open('x', encoding='utf-8') as output:
        output.write(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__':
    main()
