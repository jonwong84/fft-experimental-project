"""ZOD-185 source/description audit; no C, original or managed AI execution."""
import argparse
import hashlib
import json
from pathlib import Path

from verify_ai_act_first_contract import audit as audit_act_first
from verify_ai_rank_admission_contract import insert_best
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/ability-iteration-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-ability-iteration-contract-v1.json'


def cursor(data):
    """Isolated source cursor with supplied completion dispositions, not an ability evaluator.

    Envelope checks reject malformed descriptions; they are not new claims about C guards.
    Cursor/load operations draw nothing; supplied evaluation work can consume its own RNG.
    """
    require(type(data['resume']) is bool and type(data['counter']) is int, 'Cursor types')
    index = data['counter'] if data['resume'] else 0
    require(0 <= index < 34, 'Admitted source cursor')
    records = {row['index']: row for row in data['entries']}
    require(len(records) == len(data['entries']), 'Duplicate cursor records')
    loads, evaluations = [], []
    retry = data['resume']
    while index < 34:
        require(index in records, 'Missing described entry')
        row = records[index]
        if not retry:
            if row['skillset'] == 255:
                return dict(loads=loads, evaluations=evaluations, counter=index, endReason='terminator', cursorDraws=0)
            loads.append(index)
        retry = False
        require(row['completion'] in ('complete', 'skip-unusable', 'suspend'), 'Supplied evaluator completion')
        evaluations.append(index)
        if row['completion'] == 'suspend':
            return dict(loads=loads, evaluations=evaluations, counter=index, endReason='suspended', cursorDraws=0)
        index += 1
    return dict(loads=loads, evaluations=evaluations, counter=index, endReason='source-capacity', cursorDraws=0)


def load_entry(data):
    packed, static, usage = data['packedId'], data['staticAiBytes'], data['usage']
    require(type(packed) is int and 0 <= packed < 21 * 1024, 'Admitted packed physical slot')
    require(len(static) == 4 and all(type(b) is int and 0 <= b <= 255 for b in static), 'Static bytes')
    require(type(usage) is int and 0 <= usage <= 255, 'Usage byte')
    word = int.from_bytes(bytes(static[:3] + [usage]), 'little')
    return dict(abilityId=packed & 0x3ff, physicalSlot=packed >> 10, workingWordHex=f'{word:08x}',
                usable=bool(usage & 0x80), consumeAimTiles=bool(usage & 1))


def entry_gate(data):
    require(type(data['usable']) is bool and type(data['randomUse']) is bool, 'Gate booleans')
    if not data['usable']:
        return dict(evaluateTargets=False, gateDraws=0)
    if not data['randomUse']:
        return dict(evaluateTargets=True, gateDraws=0)
    threshold, raw = data['threshold'], data['raw']
    require(type(threshold) is int and 0 <= threshold <= 128 and type(raw) is int and 0 <= raw <= 32767, 'Random-use envelope')
    return dict(evaluateTargets=raw % 128 <= threshold, gateDraws=1)


def rank_sequence(data):
    """Reuse the existing independently described rank kernel across entry boundaries."""
    require(len(data['bestByEntry']) > 0, 'At least one described rank transition')
    ranked = [dict(decided=0, priority=-32767, hit=0, proximity=0, payload='empty') for _ in range(8)]
    indices = []
    for best in data['bestByEntry']:
        result = insert_best(best, data['baseline'], ranked)
        ranked = result['ranked']
        indices.append(result['insertedIndex'])
    return dict(rankedPayloads=[r['payload'] for r in ranked if r['hit'] != 0], insertionIndices=indices,
                lastBest=result['resetBest'], additionalRankDraws=0)


def throw_stone_ai(data):
    """Closed single-target non-execution arithmetic/zero-accuracy gate, before clock/scoring.

    Four evasion calls still draw. Midpoint formula helpers and the execution-only
    knockback block draw nothing here. Accuracy is not an HP-damage multiplier.
    """
    pa, hp, hit = data['pa'], data['targetHp'], data['hitPercent']
    require(type(pa) is int and 1 <= pa <= 99 and type(hp) is int and 1 <= hp <= 65535 and
            type(hit) is int and 0 <= hit <= 100, 'Closed Throw Stone description')
    damage = (16384 * 2 // 32768 + 1) * pa if hit else 0
    return dict(valuable=hit != 0, normalDamage=damage, targetHp=max(0, hp - damage), evasionDraws=4,
                formulaHelperDraws=0, knockback=False)


KERNELS = dict(cursorVectors=cursor, loaderVectors=load_entry, gateVectors=entry_gate,
               rankVectors=rank_sequence, throwStoneVectors=throw_stone_ai)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-185' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False and
            fixture['kind'] == 'isolated-source-descriptions', 'Source-only ability-iteration fixture')
    ids = []
    for family, calculate in KERNELS.items():
        for row in fixture[family]:
            require(calculate(row['input']) == row['expected'], 'Ability-iteration vector: ' + row['id'])
            ids.append(row['id'])
    require(len(ids) == len(set(ids)), 'Unique source description IDs')
    return dict(abilityIterationDescriptions=len(ids), selectedNextAbility='throw-stone')


def prepared_facts(path):
    data = path.read_bytes()
    require(len(data) == 356352 and hashlib.sha256(data).hexdigest() ==
            '287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562', 'Prepared SCUS-94221 hash/size')
    masks = dict(targetAllies=1, targetEnemies=2, affectsStats=8, affectsHp=128, evadeable=0x200,
                 randomUse=0x800, followTarget=0x1000, reflectable=0x4000, targetMapTiles=0x8000,
                 physicalAttack=0x10000, directTrajectory=0x400000, arcTrajectory=0x800000)
    rows = []
    for ability in (146, 148):
        primary = 0x8005ebf0 + ability * 8 - 0x80010000 + 0x800
        secondary = 0x8005fbf0 + ability * 14 - 0x80010000 + 0x800
        word = int.from_bytes(data[primary + 4:primary + 8], 'little')
        row = data[secondary:secondary + 14]
        rows.append(dict(abilityId=ability, staticAiFacts={name: bool(word & mask) for name, mask in masks.items()},
                         range=row[0], aoe=row[1], formula=row[8], x=row[9], y=row[10], ct=row[12], mp=row[13]))
    return rows


def audit(reference, prepared=None):
    report = audit_act_first(reference)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Ability iteration reference')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source mismatch: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(actual['normalizedLfSha256'] == row['normalizedLfSha256'], 'Function mismatch: ' + row['symbol'])
    if prepared is not None:
        require(prepared_facts(prepared) == evidence['selectedDataFacts'], 'Selected prepared facts mismatch')
    vectors = validate(json.loads(FIXTURE.read_text(encoding='utf-8')))
    return dict(**report, **vectors, abilityIterationFiles=len(evidence['sourceFiles']),
                abilityIterationFunctions=len(evidence['sourceFunctions']), preparedFactsVerified=prepared is not None)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
