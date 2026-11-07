"""ZOD-209 supplied post-ACT facts; no C/C# search, movement, transaction or original execution."""
import argparse
import json
from pathlib import Path

from verify_ai_pair_preparation_contract import describe as prepare
from verify_ai_throw_stone_command_contract import audit as audit_command
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/pair-post-act-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-pair-post-act-contract-v1.json'


def describe(data):
    """One closed refresh/preparation description from supplied actual state; no scored destination or full query."""
    require(data['moved'] is False and data['acted'] is True, 'Post-ACT unused Move')
    require(data['decided'] is True, 'Supplied previously decided source entry')
    require(data['savedChoices'] in ([False, False], [False, True]), 'Ordinary saved choices')
    require(all(type(v) is bool for v in data['savedChoices']), 'Boolean saved choices')
    word, count = data['rng']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and
            0 <= count <= 0xffffffffffffffff, 'RNG checkpoint')
    require(type(data['hp']) is int and type(data['maxHp']) is int and 0 <= data['hp'] <= data['maxHp'] and
            data['maxHp'] in (10, 50), 'Supplied actual HP')
    require(len(data['actor']) == len(data['target']) == 2 and all(type(v) is int and 0 <= v < 8
            for v in data['actor'] + data['target']) and data['actor'] != data['target'], 'Flat coordinates')
    if data['savedChoices'] == [False, True]:
        return dict(kind='NoProgressEnd', queryDraws=0, rng=[word, count])
    if data['hp'] == 0:
        # Closed dead-without-Reraise sole enemy: nearest eligible enemy search retains its actor default.
        # Managed living-pair admission rejects; this does not determine original battle-end/controller routing.
        return dict(kind='TerminalUnsupported', sourceNearestDefault='Actor', queryDraws=0, rng=[word, count])
    require(count < 0xffffffffffffffff, 'One refresh draw available')
    distance = sum(abs(a - b) for a, b in zip(data['actor'], data['target']))
    facts = prepare(dict(move=data['move'], ct=data['ct'], speed=data['speed'], targetCt=data['targetCt'],
                         targetSpeed=data['targetSpeed'], distance=distance, acted=True))
    end_ct = min(60, data['ct'] + 20)
    ticks = (100 - data['targetCt'] + data['targetSpeed'] - 1) // data['targetSpeed']
    require(0 <= data['targetCt'] < 100 and end_ct + ticks * data['speed'] < 100,
            'Closed peer-first refresh clock')
    word = (word * 1103515245 + 12345) & 0xffffffff
    return dict(kind='FreshLivingPreparation', refreshRng=[word, count + 1], refreshEndCt=end_ct,
                refreshActorCt=end_ct + ticks * data['speed'],
                refreshPeerCt=(data['targetCt'] + ticks * data['targetSpeed']) % 100,
                elapsedTicks=ticks, actorDelay=facts['actorDelay'], targetDelay=facts['targetDelay'],
                initializedEntries=facts['initializedEntries'], evadeModifier=facts['evadeModifier'],
                usableBeforeActClear=facts['usableEntriesBeforeActClear'],
                attackUsable=facts['attackUsable'], throwStoneUsable=facts['throwStoneUsable'],
                hasRangedAbility=facts['hasRangedAbility'], enemyHarmful=facts['enemyHarmful'],
                enemyReach=facts['enemyReach'], allyReach=facts['allyReach'], distance=distance,
                branch='InReach' if distance <= facts['enemyReach'] else 'OutOfReach',
                d0CopiesD1=True, hypotheticalMoveEndCt=min(60, data['ct']),
                hypotheticalStayEndCt=end_ct, abilityFormulaDraws=0, emptyLotteryDraws=8)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['schemaVersion'] == 1 and fixture['ticket'] == evidence['ticket'] == 'ZOD-209' and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False and
            fixture['kind'] == 'supplied-post-act-fact-descriptions', 'Source/design-only fixture')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['input']) == row['expected'], 'Pair post-ACT: ' + row['id'])
        ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique descriptions')
    return dict(pairPostActDescriptions=len(ids))


def audit(reference, prepared=None):
    report = audit_command(reference, prepared)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Post-ACT reference revision')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Post-ACT file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol'])['normalizedLfSha256'] == row['normalizedLfSha256'],
                'Post-ACT function: ' + row['symbol'])
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                pairPostActFiles=len(evidence['sourceFiles']), pairPostActFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
