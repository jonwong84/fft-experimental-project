"""ZOD-128 offline source kernels and proposed pair adapter; never executes managed AI or C."""
import argparse
import copy
import json
from pathlib import Path
import subprocess

from verify_ai_decision_contract import rank_compare
from verify_ai_score_contract import proposed_scoring
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/ai-comparison-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/comparison-contract-v1.json'
MAX = (1 << 64) - 1


def compare(data):
    for row in (data['first'], data['second']):
        require(all(type(row[k]) is int for k in ('decided', 'priority', 'hit', 'proximity')) and
                0 <= row['decided'] <= 255 and -32768 <= row['priority'] <= 32767 and
                0 <= row['hit'] <= 255 and 0 <= row['proximity'] <= 255, 'Rank widths')
    order = rank_compare(data['first'], data['second'])
    higher = order > 0 or order == 0 and data['first']['proximity'] < data['second']['proximity']
    return dict(rankOrder=order, firstIsHigher=higher, additionalDraws=0)


def delay(data):
    """Living None/Critical subset only. The original actor cache is a byte."""
    ct, speed = data['ct'], data['speed']
    require(type(ct) is int and 0 <= ct <= 254 and type(speed) is int and 1 <= speed <= 255,
            'Ordinary CT/Speed domain')
    ticks = (max(0, 100 - ct) + speed - 1) // speed
    return dict(delay=ticks, actorCacheByte=ticks & 255, additionalDraws=0)


def eligible(data):
    require(type(data['actorDelayByte']) is int and 0 <= data['actorDelayByte'] <= 255, 'Actor delay byte')
    require(type(data['targetDelay']) is int and 0 <= data['targetDelay'] <= 0x7fffffff, 'Target delay')
    for key in ('enemy', 'outOfPlay', 'stop', 'dontAct', 'sleep', 'chicken', 'charm'):
        require(type(data[key]) is bool, 'Explicit eligibility flag')
    # Status booleans mean active THROUGH actor delay (not merely currently present).
    admitted = data['enemy'] and not data['outOfPlay'] and data['targetDelay'] <= data['actorDelayByte'] and not any(
        data[k] for k in ('stop', 'dontAct', 'sleep', 'chicken', 'charm'))
    return dict(eligible=admitted, additionalDraws=0)


def proximity(data):
    """Supplied source mask cells. Unmarked cells RETAIN their old bytes, never become zero."""
    require(len(data['foes']) <= 32, 'Original slot bound (diagnostic kernel only)')
    result = []
    for cell in data['cells']:
        require(type(cell['reachable']) is bool and type(cell['previous']) is int and
                0 <= cell['previous'] <= 255, 'Cache cell domain')
        x, y, layer = cell['tile']
        require(all(type(v) is int for v in (x, y, layer)) and 0 <= x < 16 and
                0 <= y < 18 and layer in (0, 1), 'Source cell coordinates')
        value = cell['previous']
        if cell['reachable']:
            value = 0
            for foe in data['foes']:
                require(type(foe['eligible']) is bool and len(foe['position']) == 2 and all(type(v) is int and 0 <= v <= 255
                        for v in foe['position']), 'Source foe facts')
                if foe['eligible']:
                    distance = abs(foe['position'][0] - x) + abs(foe['position'][1] - y)
                    if distance < 11:
                        value = (value + 10 - distance) & 255
        result.append(dict(tile=cell['tile'], value=value, written=cell['reachable']))
    return dict(cells=result, additionalDraws=0)


def proposed_pair(data):
    """Normalized DESCRIPTION, not a managed schema or an authenticator of imported flags/formula results.

    Candidate dictionaries adapt historical scoring descriptions. The future managed entry takes
    original state/candidates/weapon/profiles/RNG; it does not accept these supplied formula fields.
    """
    require(not any(k in data for k in ('scores', 'cache', 'projectionResult')), 'No external results/cache')
    word, count = data['rngWord'], data['rngCount']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and 0 <= count <= MAX, 'RNG')
    calls = 0
    def reject(reason, index=None, evaluation=None):
        return dict(disposition='Rejected', reason=reason, retained=None, evaluations=None,
                    failedIndex=index, evaluationRejection=evaluation,
                    rngWord=word, rngCount=count, scoreCalls=calls, additionalComparisonDraws=0, inputUnchanged=True)
    if data.get('freshDecisionBaseline') is not True or data.get('sameActorAndTarget') is not True:
        return reject('UnsupportedComparisonProfile')
    units = data['units']
    require(len(units) == 2 and len({u['id'] for u in units}) == 2 and sorted(u['slot'] for u in units) == [0,1],
            'Two identified source slots')
    by_id = {u['id']:u for u in units}
    if data['actorId'] == data['targetId'] or data['actorId'] not in by_id or data['targetId'] not in by_id:
        return reject('UnsupportedComparisonProfile')
    actor, target = by_id[data['actorId']], by_id[data['targetId']]
    candidates = copy.deepcopy(data['candidates'])
    require(len(candidates) == 2, 'Exactly incumbent then challenger')
    # The actual implementation derives membership from FlatOrdinaryMovement on the original state.
    if data.get('movementSupported') is not True or any(c.get('scenarioZeroMember') is not True for c in candidates):
        return reject('InvalidCacheDestination')
    a, b = (c['scoring']['candidate'] for c in candidates)
    baseline = ('actorCt', 'actorSpeed', 'targetCt', 'targetSpeed', 'origin', 'actorSlot',
                'actorStatus', 'targetStatus', 'targetHp', 'targetMaxHp', 'moved', 'acted',
                'targetMoved', 'targetActed', 'deathCounter', 'elapsedTicks')
    require(all(a[k] == b[k] for k in baseline), 'One original baseline for both candidates')
    require(all(candidates[0]['scoring'][k] == candidates[1]['scoring'][k] for k in ('actorHp', 'actorMaxHp')),
            'One original actor HP baseline')
    require(candidates[0]['scoring']['profile'] == candidates[1]['scoring']['profile'], 'One shared score profile')
    require((a['actorCt'],a['actorSpeed'],a['actorSlot'],a['origin']) ==
            (actor['ct'],actor['speed'],actor['slot'],actor['position']) and
            (a['targetCt'],a['targetSpeed'],data['targetPosition']) ==
            (target['ct'],target['speed'],target['position']), 'Identity-bound original units')
    actor_delay = delay(actor)['actorCacheByte']
    target_delay = delay(target)['delay']
    present = target_delay <= actor_delay  # Living, stable enemy, no restricting statuses certified above.
    cache = proximity(dict(cells=[dict(tile=c['scoring']['candidate']['destination'] + [0],
        previous=0, reachable=True) for c in candidates], foes=[dict(position=data['targetPosition'], eligible=present)]))
    evaluations = []
    current_word, current_count = word, count
    for i, c in enumerate(candidates):
        scoring = c['scoring']
        scoring['candidate'].update(rngWord=current_word, rngCount=current_count)
        calls += 1
        result = proposed_scoring(scoring)
        if result['disposition'] == 'Rejected':
            return reject('EvaluationRejected', i, dict(reason=result['reason'],
                projectionReason=result.get('projectionReason')))  # Managed pair rollback policy, not a source search rule.
        current_word, current_count = result['rngWord'], result['rngCount']
        evaluations.append(dict(disposition=result['disposition'], score=result['score'],
                                rngWord=current_word, rngCount=current_count))
    retained = None
    if evaluations[0]['score'] is not None: retained = 0
    if evaluations[1]['score'] is not None:
        if retained is None: retained = 1
        else:
            ranks = [dict(decided=0, priority=r['score']['priority'], hit=r['score']['hitPercent'],
                          proximity=cache['cells'][i]['value']) for i, r in enumerate(evaluations)]
            if compare(dict(first=ranks[1], second=ranks[0]))['firstIsHigher']: retained = 1
    return dict(disposition='Compared' if retained is not None else 'NoScoredCandidate', reason=None,
                retained=retained, evaluations=evaluations, rngWord=current_word, rngCount=current_count,
                scoreCalls=calls, additionalComparisonDraws=0, inputUnchanged=True,
                baselineActorDelay=actor_delay, baselineTargetDelay=target_delay,
                eligibleEnemy=present, eligibleEnemyId=data['targetId'] if present else None,
                proximity=[c['value'] for c in cache['cells']])


def calculate(family, data):
    methods = {'source-rank': compare, 'source-delay': delay, 'source-eligibility': eligible,
               'source-proximity': proximity, 'proposed-pair': proposed_pair}
    require(family in methods, 'Family')
    return methods[family](data)


def validate(contract):
    require(contract['format'] == 'fft.ai-comparison-source-description' and contract['schemaVersion'] == 1 and
            contract['ticket'] == 'ZOD-128' and contract['referenceRevision'] == REVISION, 'Contract identity')
    require(contract['execution'] == 'offline-source-and-proposed-pair-description' and
            contract['productionImplemented'] is False, 'Execution boundary')
    seen = set(); families = set()
    for row in contract['vectors']:
        require(row['id'] not in seen, 'Duplicate vector'); seen.add(row['id']); families.add(row['family'])
        require(row['expectationKind'] == ('managed-design-proposal' if row['family'].startswith('proposed-')
                else 'source-derived-isolated-kernel'), 'Classification')
        require(calculate(row['family'], row['inputs']) == row['expected'], 'Vector mismatch: ' + row['id'])
    require(len(seen) == contract['vectorCount'] == 60 and families == {'source-rank', 'source-delay',
            'source-eligibility', 'source-proximity', 'proposed-pair'}, 'Inventory')
    require([c['id'] for c in contract['implementationCases']] == [f'ACM-{n:03}' for n in range(1, 15)] and
            all(c['status'] == 'unexecuted' for c in contract['implementationCases']), 'Production inventory')
    return dict(vectors=len(seen), families=len(families), implementationCasesUnexecuted=14)


def audit(root, reference=None, check_managed_baseline=False):
    evidence = json.loads((root/EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket'] == 'ZOD-128' and evidence['reference']['revision'] == REVISION, 'Evidence identity')
    require(evidence['fixture'] == fingerprint(root, FIXTURE), 'Fixture identity')
    for row in evidence['artifacts']: require(fingerprint(root, row['path']) == row, 'Contract artifact identity')
    result = validate(json.loads((root/FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']: require(fingerprint(root, row['path']) == row, 'History changed: ' + row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']), managedBaselineFilesChecked=0,
                  referenceFilesChecked=0, referenceFunctionsChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']: require(fingerprint(root, row['path']) == row, 'Managed baseline changed')
        result['managedBaselineFilesChecked'] = len(evidence['managedBaseline'])
    if reference is not None:
        revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
        status = subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip()
        require(revision == REVISION and not status, 'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference, row['path']) == {k:v for k,v in row.items() if k != 'functions'}, 'Reference file')
            for function in row['functions']:
                require(function_fingerprint(reference/row['path'], function['symbol']) == function, 'Reference function')
                result['referenceFunctionsChecked'] += 1
            result['referenceFilesChecked'] += 1
    result.update(format='fft.ai-comparison-source-audit', schemaVersion=1, passed=True,
                  productionImplemented=False, fullAiExecuted=False, originalExecuted=False)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path); parser.add_argument('--check-managed-baseline', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = audit(Path(__file__).resolve().parents[2], args.reference, args.check_managed_baseline)
    with args.output.open('x', encoding='utf-8') as output: output.write(json.dumps(result, indent=2)+'\n')
    print(json.dumps(result))


if __name__ == '__main__': main()
