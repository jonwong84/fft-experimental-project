"""ZOD-195 isolated preparation descriptions; no original, C or managed AI execution."""
import argparse
import json
from pathlib import Path

from verify_ai_two_entry_contract import audit as audit_pair
from verify_ai_ability_iteration_contract import prepared_facts
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/pair-preparation-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-pair-preparation-contract-v1.json'


def describe(data):
    """Closed arithmetic after supplied initialized-list facts, not refresh/scenario execution."""
    move, ct, speed, target_ct, target_speed, distance = [data[k] for k in
        ('move', 'ct', 'speed', 'targetCt', 'targetSpeed', 'distance')]
    require(all(type(v) is int for v in (move, ct, speed, target_ct, target_speed, distance)) and
            0 <= move <= 124 and 0 <= ct <= 99 and 1 <= speed <= 50 and 0 <= target_ct <= 254 and
            1 <= target_speed <= 50 and distance >= 1 and type(data['acted']) is bool, 'Closed description envelope')
    # Initialization counts Attack + Throw Stone + non-evadeable Frog Attack, before its usability skip.
    initialized, evadeable, silence, mp = 3, 2, 0, 0
    enemy_reach = move + min(1, 4)
    in_reach = distance <= enemy_reach
    return dict(initializedEntries=initialized, usableEntriesBeforeActClear=2,
                silenceModifier=silence * 4 // initialized, mpModifier=mp * 4 // initialized,
                evadeModifier=evadeable * 4 // initialized, highestMpCost=0, lowestMpCost=255,
                actorDelay=(100 - ct + speed - 1) // speed,
                targetDelay=(max(0, 100 - target_ct) + target_speed - 1) // target_speed,
                enemyReach=enemy_reach, allyReach=move, enemyHarmful=2, hasRangedAbility=True,
                attackUsable=not data['acted'], throwStoneUsable=not data['acted'],
                attackExhaustive=True, attackConsumeAim=False, throwStoneExhaustive=False, throwStoneConsumeAim=True,
                randomUseThreshold=0, d0DistanceLimit=max(3, distance - 1) if in_reach and not data['acted'] else None,
                preparationDraws=0)


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-195' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False,
            'Source-only pair preparation fixture')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['input']) == row['expected'], 'Pair preparation: ' + row['id']); ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique descriptions')
    return dict(pairPreparationDescriptions=len(ids))


def frog_facts(path):
    prepared_facts(path)  # Exact edition/hash check before reading this additional record.
    data = path.read_bytes(); ability = 367
    # target/main.yaml at the pinned clean revision: g_command_menu_types_by_skillset.
    # Verify Frog really takes the table loader; Attack dispatches through the weapon summary.
    menus = 0x80065cb4 - 0x80010000 + 0x800
    require([data[menus + skillset] for skillset in (1, 5, 169)] == [8, 0, 0], 'Attack/Basic Skill/Frog menu dispatch')
    primary = 0x8005ebf0 + ability * 8 - 0x80010000 + 0x800
    secondary = 0x8005fbf0 + ability * 14 - 0x80010000 + 0x800
    word = int.from_bytes(data[primary + 4:primary + 8], 'little')
    return dict(abilityId=ability, evadeable=bool(word & 0x200), affectedBySilence=bool(word & 0x100),
                mp=data[secondary + 13], ct=data[secondary + 12])


def audit(reference, prepared=None):
    report = audit_pair(reference, prepared); evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(report['referenceRevision'] == evidence['referenceRevision'], 'Pair preparation revision')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Preparation file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol'])['normalizedLfSha256'] == row['normalizedLfSha256'],
                'Preparation function: ' + row['symbol'])
    if prepared is not None: require(frog_facts(prepared) == evidence['additionalPreparedFacts'], 'Frog initialization facts')
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                pairPreparationFiles=len(evidence['sourceFiles']), pairPreparationFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True); parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path); args = parser.parse_args(); report = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream: json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
