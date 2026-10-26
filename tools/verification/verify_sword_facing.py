"""Audit the explicit settled-facing fixture migration and repeated headless regressions."""
import argparse
import copy
import json
from pathlib import Path
from verify_basic_skill_content import inventory, require, sha, draws, validate_stone

POLICY = 'fft:combat/bounded-sword-settled-facing-v1'
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'


def validate_replacement(historical, replacement):
    """Validate stored replacement bytes against the source-authorized static delta; no runner normalization."""
    expected = copy.deepcopy(historical)
    require(historical['schemaVersion'] == 3 and historical['target']['policyId'] == 'fft:combat/bounded-sword-contract', 'Historical policy/schema')
    require(replacement['fixtureId'] != historical['fixtureId'] and replacement['fixtureId'].endswith('-settled-facing-v1'), 'Replacement ID')
    require(bool(replacement['purpose']) and historical['fixtureId'] in replacement['purpose'], 'Replacement provenance')
    require(len(replacement['evidence']) == len(historical['evidence'])+1, 'Evidence inventory')
    extra = replacement['evidence'][-1]
    require(extra['id'] == 'settled-facing' and extra['kind'] == 'source-derived' and extra['confidence'] == 'source-traced' and
            extra['sources'] == [dict(path='src/battle/turn/controller.c',symbol='reward_grant / facing_all_after_attack_restore / facing_after_attack_restore',revision=REVISION)], 'Facing source evidence')
    expected['fixtureId'] = replacement['fixtureId']
    expected['purpose'] = replacement['purpose']
    expected['reference']['observedOn'] = '2026-10-26'
    expected['target']['policyId'] = POLICY
    expected['evidence'].append(extra)
    faces = {unit['unitId']: unit['facing'] for unit in historical['scenario']['initialState']['combatants']}
    changed = 0
    for command, expectation in zip(expected['scenario']['commands'], expected['expectations']):
        result = next(group for group in expectation['assertions'] if group['kind'] == 'result')
        group = next(group for group in expectation['assertions'] if group['kind'] == 'state-and-events')
        group['evidenceIds'].append('settled-facing')
        if command['kind'] == 'attack-unit' and result['accepted']:
            actor = next(unit for unit in group['state']['combatants'] if unit['unitId'] == command['actorId'])
            changed += actor['facing'] != faces[command['actorId']]
            actor['facing'] = faces[command['actorId']]
    require(expected == replacement, 'Replacement changed unrelated state/events/RNG/inputs or omitted settled facing')
    return changed


def verify(root, save_baseline, save_first, save_repeat, basic_baseline, basic_first, basic_repeat):
    contract = json.loads((root/'docs/evidence/combat/sword-facing-contract-v1.json').read_text())
    preserved = []
    for key in ('swordV3', 'saves', 'combatEvidence'):
        for row in contract['historicalInventory'][key]:
            data = (root/row['path']).read_bytes()
            require(len(data) == row['byteLength'] and sha(data) == row['sha256'], 'Historical bytes changed: '+row['path'])
            preserved.append(row['path'])
    manifest = json.loads((root/'docs/fixtures/behavior/sword-facing-supersession-v1.json').read_text())
    require(manifest['format'] == 'fft.sword-facing-supersession' and manifest['schemaVersion'] == 1 and
            manifest['replacementPolicy'] == POLICY and manifest['historicalHashEncoding'] == 'utf8-lf' and len(manifest['fixtures']) == 9, 'Supersession manifest')
    changes = 0
    seen = set()
    for row in manifest['fixtures']:
        require(row['historicalPath'] not in seen, 'Duplicate supersession')
        seen.add(row['historicalPath'])
        old_bytes = (root/row['historicalPath']).read_bytes()
        old = json.loads(old_bytes); new = json.loads((root/row['replacementPath']).read_text())
        require(sha(old_bytes) == row['historicalSha256'] and old['fixtureId'] == row['historicalId'] and new['fixtureId'] == row['replacementId'], 'Supersession identity')
        changes += validate_replacement(old, new)
    require(seen == {row['path'] for row in contract['historicalInventory']['swordV3']} and changes == 8, 'Historical/replacement coverage')
    saves = [inventory(path) for path in (save_baseline, save_first, save_repeat)]
    require(saves[0] == saves[1] == saves[2] and set(saves[0]) == {'check.json'} | {f'checkpoint-{n:02}.json' for n in range(13)}, 'Sword baseline/repeat bytes or inventory')
    report = json.loads(saves[1]['check.json'])
    require(report['continuationStateAndEventsMatched'] and report['rngWord'] == 2473755763 and report['rngDrawCount'] == 22 and report['checkpoints'] == 13, 'Sword continuation outcome')
    for index in range(13):
        saved = json.loads(saves[1][f'checkpoint-{index:02}.json'])
        require(saved['schemaVersion'] == 2 and saved['snapshot']['profileVersion'] == 1 and saved['snapshot']['contentVersion'] == 1, 'Save/profile version changed')
    basics = [inventory(path) for path in (basic_baseline, basic_first, basic_repeat)]
    require(basics[0] == basics[1] == basics[2] and set(basics[0]) == {'check.json','content.package.json','ability.package.json','targeted.package.json'}, 'Basic Skill historical/repeat bytes')
    basic = json.loads(basics[1]['check.json'])
    require(basic['freshDefinitionContinuation'] and basic['restartMatched'] and len(basic['receipts']) == 46 and basic['checkpointCount'] == 47, 'Basic Skill replay coverage')
    before = None; hits = pushes = 0
    for row in basic['receipts']:
        after = json.loads(row['state'])
        if row['Accepted'] is False:
            require(before == after and row['events'] == [], 'Rejected Basic Skill command mutated state')
        elif row['kind'] == 'UseTargetedAbility':
            hit = validate_stone(before, after); hits += 1; pushes += hit['moved']
        else:
            prior = before['RngState'] if before else dict(Value=1,DrawCount=0)
            require(after['RngState'] == draws(prior,1 if row['kind']=='Advance' else 0)[0], 'Basic Skill nonrandom/readiness RNG')
        require(after['Turns']['RngState'] == after['RngState'], 'Basic Skill RNG copies')
        before = after
    require((hits,pushes) == (6,3) and before['RngState'] == dict(Value=4200531631,DrawCount=58), 'Basic Skill final vector')
    old_window = json.loads((root/'docs/evidence/combat/throw-stone-window-v1.json').read_text())
    for row in old_window['preparedFiles']:
        require(sha((root/row['path']).read_bytes()) == row['sha256'], 'Existing prepared content changed')
    old_native = root/old_window['native']['nativeRoots'][0]
    for name, digest in old_window['audit']['nativeFileHashes'].items():
        require(sha((old_native/name).read_bytes()) == digest, 'Historical native artifact changed')
    return dict(format='fft.sword-facing-implementation-audit',schemaVersion=1,passed=True,
                historicalFilesPreserved=len(preserved),replacementFixtures=9,explicitFacingCorrections=changes,
                swordSaveFilesEqualToPriorBuild=14,basicSkillFilesEqualToHistorical=4,
                oldPreparedFilesPreserved=4,oldNativeFilesPreserved=29,
                swordRng=dict(Value=2473755763,DrawCount=22),basicSkillRng=before['RngState'],
                sourceContractSha256=sha((root/'docs/evidence/combat/sword-facing-contract-v1.json').read_bytes()),
                supersessionSha256=sha((root/'docs/fixtures/behavior/sword-facing-supersession-v1.json').read_bytes()),
                managedCorrectionCoverage='Reported separately in implementation evidence and full-suite TRX',
                limits='Artifact/independent arithmetic audit plus managed tests; no new native/original execution')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('save-baseline','save-first','save-repeat','basic-baseline','basic-first','basic-repeat','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    args = parser.parse_args()
    result = verify(Path(__file__).resolve().parents[2], args.save_baseline,args.save_first,args.save_repeat,
                    args.basic_baseline,args.basic_first,args.basic_repeat)
    with args.output.open('x',encoding='utf-8') as output:
        output.write(json.dumps(result,indent=2)+'\n')
    print('PASS: nine explicit replacements, eight facing-only state corrections, historical bytes and repeated headless regressions')


if __name__ == '__main__':
    main()
