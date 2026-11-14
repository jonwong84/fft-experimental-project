"""ZOD-124 offline source descriptions and proposed adapter; never runs C or managed AI."""
import argparse
import copy
import json
from pathlib import Path
import subprocess

from verify_ai_decision_contract import draws, verify_prepared_record
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/ai-candidate-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/candidate-contract-v1.json'
MAX = (1 << 64) - 1
FACES = ('East', 'North', 'West', 'South')


def status_result(data):
    """Ordinary neutral status data, no innate/immunity/modifiers, one positive accuracy strike."""
    hp, maximum, damage = data['hp'], data['maxHp'], data['damage']
    require(type(maximum) is int and 1 <= maximum <= 65535 and type(hp) is int and
            0 < hp <= maximum and type(damage) is int and 0 <= damage <= 999, 'HP domain')
    require(data['status'] == ('Critical' if hp <= maximum // 5 else 'None'), 'Consistent incoming status')
    require(type(data['hasAccuracy']) is bool and type(data['deathCounter']) is int and
            0 <= data['deathCounter'] <= 255, 'Status result domain')
    if not data['hasAccuracy']:
        return dict(hp=hp, status=data['status'], deathCounter=data['deathCounter'])
    hp = max(0, hp - damage)
    return dict(hp=hp, status='Dead' if hp == 0 else 'Critical' if hp <= maximum // 5 else 'None',
                deathCounter=3 if hp == 0 else data['deathCounter'])


def action_order(data):
    require(data['order'] in ('MoveThenAct', 'ActThenMove'), 'Order')
    moving = data['origin'] != data['destination']
    move = ['PlaceCoordinates', 'MoveTaken'] if moving else []
    action = ['ProjectAction']
    if data['hasAccuracy']: action += ['ActTaken']
    if not data['hasAccuracy']:
        return dict(steps=(move if data['order'] == 'MoveThenAct' else []) + action + ['RestoreBaseline'],
                    scored=False, facing=data['facing'])
    return dict(steps=(move + action if data['order'] == 'MoveThenAct' else action + move) +
                ['TurnEnd', 'FirstClockEvent', 'RestoreClockGlobals', 'Score', 'RestoreBaseline'],
                scored=True, facing=data['facing'])


def first_clock(data):
    """Closed projection of canonical UNIT_TURN -> PICK_UNIT; no charge/upkeep effects.

    Dead is present and not CT-frozen. Each pick draws upkeep after modulo;
    dead picks continue inside the same source call, before its first ready event.
    AI immediately clears the selected living unit's has_turn after that event.
    """
    units = copy.deepcopy(data['units'])
    require(len(units) == 2 and sorted(u['slot'] for u in units) == [0, 1], 'Two source-order slots')
    for u in units:
        require(type(u['ct']) is int and 0 <= u['ct'] <= 254 and type(u['speed']) is int and
                1 <= u['speed'] <= 255 and u['status'] in ('None', 'Critical', 'Dead'), 'Clock domain')
    require(any(u['status'] != 'Dead' for u in units), 'Living survivor')
    ticks = 0
    picks = []
    while True:
        ready = [u for u in units if u['ct'] >= 100]
        if ready:
            selected = min(ready, key=lambda u: (-u['ct'], u['slot']))
            before = selected['ct']
            selected['ct'] %= 100
            picks.append(dict(slot=selected['slot'], ctBefore=before, ctAfter=selected['ct'],
                              dead=selected['status'] == 'Dead'))
            if selected['status'] == 'Dead':
                continue
            selected['moved'] = selected['acted'] = False
            return dict(units=units, elapsedTicks=ticks, upkeepDraws=len(picks), picks=picks,
                        readySlot=selected['slot'], moveOpportunitySlot=selected['slot'],
                        activeUnit=None, deathCountersChanged=False)
        # The omitted intermediate phases are effect-free only under this certificate.
        ticks += 1
        require(ticks <= 100, 'Positive-speed bound')
        for u in units: u['ct'] = min(254, u['ct'] + u['speed'])


def proposed_candidate(data):
    """Normalized proposal with a supplied movement certificate and formula result.

    This does not enumerate origins, execute a formula, validate terrain, or model
    the full source scheduler. Production must obtain the certificates from the
    original input before applying the private projection.
    """
    word, count = data['rngWord'], data['rngCount']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and 0 <= count <= MAX, 'RNG domain')
    def reject(reason):
        return dict(disposition='Rejected', reason=reason, outcome=None, inputUnchanged=True,
                    rngWord=word, rngCount=count, rngDraws=0)
    for failed, reason in [(data['terminal'] or data['actorStatus'] == 'Dead' or
            data['targetStatus'] == 'Dead' or data['targetHp'] == 0, 'TerminalInput'),
            (not data['activeActor'] or data['acted'], 'UnavailableAction'),
            (not data['profileSupported'], 'UnsupportedProfile'),
            (not data['placementAllowed'] or data['moved'] and data['origin'] != data['destination'], 'InvalidCandidate')]:
        if failed: return reject(reason)
    require(data['facing'] in FACES and data['targetFacing'] in FACES, 'Facing')
    require(data['order'] in ('MoveThenAct', 'ActThenMove') and 0 <= data['hitPercent'] <= 100, 'Candidate domain')
    require(0 <= data['actorCt'] <= 99 and type(data['elapsedTicks']) is int and
            0 <= data['elapsedTicks'] <= MAX, 'Active clock domain')
    if count > MAX - 4: return reject('RandomDrawCountExhausted')
    advanced_word, _ = draws(word, 4)
    if data['hitPercent'] == 0:
        return dict(disposition='NotValuable', reason=None, outcome=None, inputUnchanged=True,
                    rngWord=advanced_word, rngCount=count + 4, rngDraws=4)
    target = status_result(dict(hp=data['targetHp'], maxHp=data['targetMaxHp'], damage=data['damage'],
        status=data['targetStatus'], deathCounter=data['deathCounter'], hasAccuracy=True))
    moved = data['moved'] or data['origin'] != data['destination']
    ended_ct = min(60, data['actorCt'] + (0 if moved else 20))
    clock = first_clock(dict(units=[dict(slot=data['actorSlot'], ct=ended_ct, speed=data['actorSpeed'],
        status=data['actorStatus'], moved=moved, acted=True), dict(slot=1-data['actorSlot'], ct=data['targetCt'],
        speed=data['targetSpeed'], status=target['status'], moved=data['targetMoved'], acted=data['targetActed'])]))
    total = 4 + clock['upkeepDraws']
    if count > MAX - total: return reject('RandomDrawCountExhausted')
    if data['elapsedTicks'] > MAX - clock['elapsedTicks']: return reject('TickCountExhausted')
    final_word, _ = draws(word, total)
    return dict(disposition='Projected', reason=None, inputUnchanged=True, rngWord=final_word,
        rngCount=count + total, rngDraws=total, outcome=dict(position=data['destination'], facing=data['facing'],
            targetFacing=data['targetFacing'], target=target, turnEndCt=ended_ct, clock=clock,
            targetKnockedOut=target['status'] == 'Dead', terminalPolicyApplied=False,
            clockGlobalsRestored=True))


def ownership(data):
    """Managed proposal: hand RNG to the next candidate explicitly; discard spatial result."""
    first = proposed_candidate(data)
    second_input = dict(data, rngWord=first['rngWord'], rngCount=first['rngCount'])
    second = proposed_candidate(second_input)
    return dict(first=first, second=second, replayEqual=first == proposed_candidate(data),
                discardedStateReused=False, carriedDecisionRng=True)


def calculate(family, data):
    functions = {'source-status': status_result, 'source-order': action_order, 'source-clock': first_clock,
                 'proposed-candidate': proposed_candidate, 'proposed-ownership': ownership}
    require(family in functions, 'Family')
    return functions[family](data)


def validate(contract):
    require(contract['format'] == 'fft.ai-candidate-source-description' and contract['schemaVersion'] == 1 and
            contract['ticket'] == 'ZOD-124' and contract['referenceRevision'] == REVISION, 'Contract identity')
    require(contract['execution'] == 'offline-source-and-proposed-adapter-description' and
            contract['productionImplemented'] is False, 'Execution boundary')
    seen = set(); families = set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector'); seen.add(row['id']); families.add(row['family'])
        require(row['expectationKind'] == ('managed-design-proposal' if row['family'].startswith('proposed-')
                else 'source-derived-closed-domain'), 'Classification')
        require(calculate(row['family'], row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
    require(len(seen) == contract['vectorCount'] == 44 and families == {'source-status', 'source-order',
            'source-clock', 'proposed-candidate', 'proposed-ownership'}, 'Inventory')
    cases = contract['implementationCases']
    require([r['id'] for r in cases] == [f'ACP-{n:03}' for n in range(1, 17)] and
            all(r['status'] == 'unexecuted' for r in cases), 'Unexecuted production inventory')
    return dict(vectors=len(seen), families=len(families), implementationCasesUnexecuted=len(cases))


def verify_status_records(executable, record):
    verify_prepared_record(executable, record)
    data = executable.read_bytes()
    offset = record['address'] - record['loadAddress'] + 0x800
    table = data[offset:offset + record['byteLength']]
    facts = dict(deadFreezesCt=bool(table[2*16+4] & 128), criticalFreezesCt=bool(table[23*16+4] & 128),
                 deadCancelsCritical=bool(table[2*16+6+2] & 1))
    require(facts == record['facts'], 'Status data facts')


def audit(root, reference=None, check_managed_baseline=False, executable=None):
    evidence = json.loads((root / EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket'] == 'ZOD-124' and evidence['reference']['revision'] == REVISION, 'Evidence identity')
    require(evidence['fixture'] == fingerprint(root, FIXTURE), 'Fixture identity')
    result = validate(json.loads((root / FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']: require(fingerprint(root, row['path']) == row, 'History changed: ' + row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']), managedBaselineFilesChecked=0,
                  referenceFilesChecked=0, referenceFunctionsChecked=0, retailTablesChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']: require(fingerprint(root, row['path']) == row, 'Managed baseline changed: ' + row['path'])
        result['managedBaselineFilesChecked'] = len(evidence['managedBaseline'])
    if reference is not None:
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        status = subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference, row['path']) == {k:v for k,v in row.items() if k != 'functions'}, 'Reference file: ' + row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference / row['path'], function['symbol']) == function, 'Reference function')
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    if executable is not None:
        verify_status_records(executable, evidence['retailStatusMetadata']); result['retailTablesChecked'] = 1
    result.update(format='fft.ai-candidate-source-audit', schemaVersion=1, passed=True,
                  productionImplemented=False, fullAiExecuted=False, originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path); parser.add_argument('--check-managed-baseline', action='store_true')
    parser.add_argument('--executable', type=Path); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2], args.reference, args.check_managed_baseline, args.executable)
    with args.output.open('x', encoding='utf-8') as output: output.write(json.dumps(result, indent=2) + '\n')
    print(json.dumps(result))


if __name__ == '__main__': main()
