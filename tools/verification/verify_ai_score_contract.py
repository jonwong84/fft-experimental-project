"""ZOD-126 offline scoring descriptions and wrapper proposal; never executes C or managed AI."""
import argparse
import json
from pathlib import Path
import subprocess

from verify_ai_candidate_contract import proposed_candidate
from verify_ai_decision_contract import signed16, verify_prepared_record
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/ai-score-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/score-contract-v1.json'
WEIGHTS = {'None': 0, 'Critical': -32, 'Dead': -192}


def unit_score(data):
    hp, maximum = data['hp'], data['maxHp']
    require(type(hp) is int and type(maximum) is int and 1 <= maximum <= 65535 and 0 <= hp <= maximum, 'HP domain')
    status = 'Dead' if hp == 0 else 'Critical' if hp <= maximum // 5 else 'None'
    require(data['status'] == status and type(data['monster']) is bool, 'Consistent status/unit kind')
    require(type(data['keptSlots']) is int and 0 <= data['keptSlots'] <= 7, 'Kept equipment domain')
    hp_term = hp * 128 // maximum
    status_term = WEIGHTS[status]
    equipment = 0 if data['monster'] else data['keptSlots'] * 51
    return dict(hpTerm=hp_term, statusTerm=status_term, equipmentTerm=equipment,
                unitTotal=hp_term + status_term + equipment)


def state_score(data):
    require(len(data['units']) == 2 and len({u['slot'] for u in data['units']}) == 2, 'Two original-order units')
    total = 0
    contributions = []
    for unit in sorted(data['units'], key=lambda u: u['slot']):
        require(type(unit['enemy']) is bool and type(unit['slot']) is int and unit['slot'] >= 0, 'Relation/order')
        part = unit_score(unit)
        signed = -part['unitTotal'] if unit['enemy'] else part['unitTotal']
        total = signed16(total + signed)
        contributions.append(dict(slot=unit['slot'], **part, signedContribution=signed, storedPriority=total))
    return dict(priority=total, contributions=contributions, additionalDraws=0)


def hit_accounting(data):
    accuracy = data['accuracies']
    require(len(accuracy) < 255 and all(type(a) is int and 0 <= a <= 100 for a in accuracy), 'Counted accuracy domain')
    positive = [a for a in accuracy if a > 0]
    valuable = data['valuable'] and bool(positive)
    return dict(valuable=valuable, countedPairs=len(positive),
                baseHitPercent=sum(positive) // len(positive) if valuable else None, additionalDraws=0)


def enemy_relation(data):
    require(all(type(data[k]) is int and 0 <= data[k] <= 255 for k in ('actorCurrentTeam','targetInitialTeam')), 'Team byte domain')
    return dict(enemy=not data['self'] and (data['targetInitialTeam'] & 0x30) != (data['actorCurrentTeam'] & 0x30))


def proposed_scoring(data):
    """Fresh projection exactly once, then zero-draw score; no external/cached result input.

    Profiles here are normalized explicit facts. They describe the managed proposal,
    not actual validation of imported teams/equipment or a production score API.
    """
    require('projectionResult' not in data, 'External projection is not an entry point')
    candidate, profile = data['candidate'], data['profile']
    word, count = candidate['rngWord'], candidate['rngCount']
    common = dict(additionalScoreDraws=0, inputUnchanged=True)
    def reject(reason):
        return dict(disposition='Rejected', reason=reason, score=None, projectionCalls=0,
                    rngWord=word, rngCount=count, **common)
    units = profile.get('units', [])
    if not (profile.get('idsMatch') is True and profile.get('stableOpponents') is True and
            profile.get('unchangedTeams') is True and isinstance(units,list) and len(units) == 2 and
            all(isinstance(u,dict) and u.get('monster') is False and
                all(type(u.get(k)) is int for k in ('keptSlots','mp','maxMp','golem','baselineGolem')) and
                u['keptSlots'] == 7 and u['mp'] == u['maxMp'] == 0 and
                u['golem'] == u['baselineGolem'] == 0 for u in units)):
        return reject('UnsupportedScoreProfile')
    # Metadata/flags omitted from the live model must be explicitly certified; no guess from a sword ID.
    projection = proposed_candidate(candidate)
    forwarded = dict(rngWord=projection['rngWord'], rngCount=projection['rngCount'], projectionCalls=1, **common)
    if projection['disposition'] == 'Rejected':
        return dict(disposition='Rejected', reason='ProjectionRejected', projectionReason=projection['reason'], score=None, **forwarded)
    if projection['disposition'] == 'NotValuable':
        return dict(disposition='NotValuable', reason=None, score=None, **forwarded)
    outcome = projection['outcome']
    score = state_score(dict(units=[dict(slot=candidate['actorSlot'], hp=data['actorHp'], maxHp=data['actorMaxHp'],
        status=candidate['actorStatus'], monster=False, keptSlots=7, enemy=False),
        dict(slot=1-candidate['actorSlot'], hp=outcome['target']['hp'], maxHp=candidate['targetMaxHp'],
        status=outcome['target']['status'], monster=False, keptSlots=7, enemy=True)]))
    return dict(disposition='Scored', reason=None, score=dict(priority=score['priority'],
                hitPercent=candidate['hitPercent'], contributions=score['contributions']), **forwarded)


def calculate(family, data):
    methods = {'source-unit': unit_score, 'source-state': state_score, 'source-hit': hit_accounting,
               'source-relation': enemy_relation, 'proposed-scoring': proposed_scoring}
    require(family in methods, 'Family')
    return methods[family](data)


def validate(contract):
    require(contract['format'] == 'fft.ai-score-source-description' and contract['schemaVersion'] == 1 and
            contract['ticket'] == 'ZOD-126' and contract['referenceRevision'] == REVISION, 'Contract identity')
    require(contract['execution'] == 'offline-source-and-proposed-wrapper-description' and
            contract['productionImplemented'] is False, 'Execution boundary')
    seen = set(); families = set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector'); seen.add(row['id']); families.add(row['family'])
        require(row['expectationKind'] == ('managed-design-proposal' if row['family'].startswith('proposed-')
                else 'source-derived-closed-kernel'), 'Classification')
        require(calculate(row['family'],row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
    require(len(seen) == contract['vectorCount'] == 44 and families == {'source-unit','source-state',
            'source-hit','source-relation','proposed-scoring'}, 'Inventory')
    cases = contract['implementationCases']
    require([r['id'] for r in cases] == [f'ASC-{n:03}' for n in range(1,15)] and
            all(r['status'] == 'unexecuted' for r in cases), 'Unexecuted production inventory')
    return dict(vectors=len(seen), families=len(families), implementationCasesUnexecuted=len(cases))


def verify_retail(executable, records):
    data = executable.read_bytes()
    for record in records:
        verify_prepared_record(executable,record)
        offset = record['address'] - record['loadAddress'] + 0x800
        if record['symbol'] == 'g_ability_data[0]':
            flags = data[offset+4]
            facts = dict(targetEnemies=bool(flags & 2), affectsStats=bool(flags & 8), affectsHp=bool(flags & 128))
        else:
            facts = dict(menuType=data[offset], throwHitHalving=data[offset] == 2)
        require(facts == record['facts'], 'Retail facts')


def audit(root, reference=None, check_managed_baseline=False, executable=None):
    evidence = json.loads((root/EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket'] == 'ZOD-126' and evidence['reference']['revision'] == REVISION, 'Evidence identity')
    require(evidence['fixture'] == fingerprint(root,FIXTURE), 'Fixture identity')
    result = validate(json.loads((root/FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']: require(fingerprint(root,row['path']) == row, 'History changed: ' + row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']), managedBaselineFilesChecked=0,
                  referenceFilesChecked=0, referenceFunctionsChecked=0, retailRecordsChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']: require(fingerprint(root,row['path']) == row, 'Managed baseline changed: ' + row['path'])
        result['managedBaselineFilesChecked'] = len(evidence['managedBaseline'])
    if reference is not None:
        revision = subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
        status = subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference,row['path']) == {k:v for k,v in row.items() if k != 'functions'}, 'Reference file: ' + row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference/row['path'],function['symbol']) == function,'Reference function')
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    if executable is not None:
        verify_retail(executable,evidence['retailRecords']); result['retailRecordsChecked'] = len(evidence['retailRecords'])
    result.update(format='fft.ai-score-source-audit',schemaVersion=1,passed=True,productionImplemented=False,
                  fullAiExecuted=False,originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path); parser.add_argument('--check-managed-baseline',action='store_true')
    parser.add_argument('--executable',type=Path); parser.add_argument('--output',type=Path,required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2],args.reference,args.check_managed_baseline,args.executable)
    with args.output.open('x',encoding='utf-8') as output: output.write(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))


if __name__ == '__main__': main()
