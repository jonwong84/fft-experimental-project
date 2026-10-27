"""ZOD-130 offline scan descriptions; no production enumerator or original/C execution."""
import argparse
import json
import re
from pathlib import Path
import subprocess

from verify_ai_comparison_contract import compare, delay
from verify_ai_decision_contract import draws, verify_prepared_record
from verify_ai_score_contract import proposed_scoring
from verify_flat_movement_contract import distances
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

FIXTURE = 'docs/fixtures/behavior/ai-scan-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/scan-contract-v1.json'
MAX = (1 << 64) - 1
CERTIFICATES = ('freshDecisionBaseline', 'ordinaryAttackTilePolicy', 'originalScenarioMask',
                'targetSelectionMatchesBaseline', 'freshAbilityBestReset', 'candidateProfileSupported', 'scoreProfileClosed')


def source_policy(data):
    """Isolated source initialization reduction; not general ability/weapon import."""
    flags1, flags2, flags3 = data['weaponSummaryFlags']
    require(all(type(v) is int and 0 <= v <= 255 for v in (flags1, flags2, flags3, data['usageBefore'],
            data['range'], data['aoe'], data['secondaryFlags2'])), 'Policy byte widths')
    require(type(data['secondaryEvadeable']) is bool and type(data['usableAfter']) is bool, 'Policy facts')
    usage = data['usageBefore'] & 0xf8
    if not flags3 & 0xc0:
        if flags3 & 0x3c or data['range'] + data['aoe'] < 3 and data['secondaryEvadeable']:
            usage |= 4
        elif data['secondaryFlags2'] & 0x41 or data['aoe'] == 0:
            usage |= 2
    usage = usage | 0x80 if data['usableAfter'] else usage & ~0x80
    if usage & 2: usage |= 1
    # Nonexhaustive policy timing is deliberately outside this kernel's admission.
    require(usage & 4 or usage & 2 or flags3 & 0xc0, 'Outside described policy branches')
    return dict(usage=usage, consideredFlags=flags1 | flags2 << 8 | flags3 << 16 | usage << 24,
                exhaustive=bool(usage & 4), followTarget=bool(flags2 & 0x10),
                usable=bool(usage & 0x80), additionalDraws=0)


def source_masks(data):
    """Range1/AoE0 tile-targeted exhaustive branch, on the admitted flat lower layer.

    Supplied origins are a source mask, not an assertion that managed movement accepts them.
    The distance-zero diagnostic origin is filtered by the real weapon range before evaluation.
    """
    width, depth = data['width'], data['depth']
    require(type(width) is int and type(depth) is int and 1 <= width <= 16 and 1 <= depth <= 16, 'Map bounds')
    points = [data['actor'], data['target']] + data['origins']
    require(all(len(p) == 2 and all(type(v) is int for v in p) and 0 <= p[0] < width and 0 <= p[1] < depth
                for p in points), 'Lower-layer coordinates')
    require(type(data['targetActive']) is bool and type(data['targetUseful']) is bool, 'Explicit target flags')
    active = data['targetActive'] and data['targetUseful']
    target = data['target']; actor = data['actor']
    cast = sorted({tuple(p) for p in data['origins'] if active and abs(target[0]-p[0])+abs(target[1]-p[1]) <= 1},
                  key=lambda p: (p[1], p[0]))
    rows = [sum(0x8000 >> x for x, cy in cast if cy == y) for y in range(depth)]
    emitted = []
    for origin in cast:
        if abs(target[0]-origin[0])+abs(target[1]-origin[1]) != 1: continue
        own = list(origin) == actor
        emitted.append(dict(origin=list(origin), destination=data['destination'] if own else list(origin),
                            order='ActThenMove' if own else 'MoveThenAct', targetPanel=target))
    return dict(castOrigins=[list(p) for p in cast], castRows=rows, aimTiles=[target] if active else [],
                emitted=emitted, aimConsumed=False, additionalDraws=0)


def source_resume(data):
    """Program-counter/RNG-ownership description with supplied draw costs, not a resumable scheduler."""
    require(data['site'] in ('before-origins', 'before-aim', 'inside-evaluation'), 'Resume site')
    require(all(type(n) is int and n >= 0 for n in data['completedDrawCosts']) and
            type(data['partialDraws']) is int and data['partialDraws'] >= 0, 'Supplied stage costs')
    require(data['site'] == 'inside-evaluation' or data['partialDraws'] == 0, 'Only an in-flight evaluation has partial draws')
    require(data.get('simulationPhase') in ('advance_turns','score_state') if data['site']=='inside-evaluation'
            else data.get('simulationPhase') is None, 'Explicit source simulation reentry')
    count = sum(data['completedDrawCosts']) + data['partialDraws']
    require(data['rngCount'] + count <= MAX, 'Description count bound')
    word, _ = draws(data['rngWord'], count)
    return dict(nextIndex=len(data['completedDrawCosts']), rngWord=word, rngCount=data['rngCount']+count,
                rebuildCastAndAimMasks=False, replayCompleted=False, restartFormula=False,
                simulationReentry=data.get('simulationPhase'),
                reentry='evaluate_priority' if data['site'] == 'inside-evaluation' else
                        'origin_aim_tiles_evaluate' if data['site'] == 'before-aim' else 'begin_evaluation')


def source_roster_bound(data):
    require(data['unitSlots'] == 21 and data['actorExcluded'] is True, 'Pinned original slot count')
    return dict(maximumEligibleEnemies=20, maximumProximity=200, byteWrapReachable=False,
                historical26FoeWrapAdmitted=False)


def proposed_scan(data):
    """Managed scan PROPOSAL over normalized original inputs. All formula/clock calls are old descriptions.

    This is neither an actual C# entry nor evidence of native frame-resume equivalence.
    """
    require(not any(k in data for k in ('scores', 'cache', 'candidates', 'projectionResult')), 'No external result/mask cache input')
    word, count = data['rngWord'], data['rngCount']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and 0 <= count <= MAX, 'RNG domain')
    calls = 0
    def reject(reason, index=None, nested=None):
        return dict(disposition='Rejected', reason=reason, failedIndex=index, evaluationRejection=nested,
                    outcome=None, rngWord=word, rngCount=count, scoreCalls=calls, additionalScanDraws=0, inputUnchanged=True)
    profile = data['profile']
    if not all(profile.get(k) is True for k in CERTIFICATES) or type(profile.get('targetActive')) is not bool:
        return reject('UnsupportedScanProfile')
    state = data['state']; units = state['units']; ids = [u['id'] for u in units]
    if len(ids) != 2 or len(set(ids)) != 2 or state['actorId'] == state['targetId'] or set(ids) != {state['actorId'], state['targetId']}:
        return reject('UnsupportedState')
    by_id = {u['id']: u for u in units}; actor, target = by_id[state['actorId']], by_id[state['targetId']]
    if any(u['hp'] <= 0 or not 1 <= u['speed'] <= 255 for u in units): return reject('UnsupportedState')
    if not state['activeActor'] or actor['acted']: return reject('UnavailableAction')
    if not (1 <= state['width'] <= 16 and 1 <= state['depth'] <= 16 and 0 <= state['move'] <= 255
            and 0 <= actor['ct'] <= 99 and 0 <= target['ct'] <= 254 and sorted(u['slot'] for u in units) == [0,1]):
        return reject('UnsupportedState')
    for u in units:
        require(type(u['hp']) is int and type(u['maxHp']) is int and 0 < u['hp'] <= u['maxHp'] <= 65535 and
                u['facing'] in ('East','North','West','South') and 0 <= u['evade'] <= 100, 'Normalized combat domain')
    power = data['weaponPower']; xa = max(1, actor['pa'])
    if not (type(power) is int and 0 <= power <= 255 and 0 <= actor['pa'] <= 255 and (2*xa-1)*power <= 32767):
        return reject('UnsupportedState')
    width, depth = state['width'], state['depth']
    for u in units:
        p=u['position']
        if not (len(p)==2 and all(type(v) is int for v in p) and 0 <= p[0] < width and 0 <= p[1] < depth): return reject('UnsupportedState')
    if actor['position'] == target['position']: return reject('UnsupportedState')
    best = distances(width,depth,actor['position'],target['position'])
    allowed = {tuple(actor['position'])} if actor['moved'] else {p for p,d in best.items() if d <= min(124,state['move'])}
    scenario = data['scenario']; origins=scenario['origins']; destination=scenario['destination']
    if any(len(p)!=2 or not all(type(v) is int for v in p) for p in origins): return reject('InvalidScenario')
    mask={tuple(p) for p in origins}
    if len(mask)!=len(origins) or not mask.issubset(allowed): return reject('InvalidScenario')
    if not mask and destination is not None or mask and (destination is None or tuple(destination) not in mask):
        return reject('InvalidScenario')
    masks=source_masks(dict(width=width,depth=depth,actor=actor['position'],target=target['position'],origins=origins,
        destination=destination,targetActive=profile['targetActive'],targetUseful=True))
    actor_delay=delay(actor)['actorCacheByte']; target_delay=delay(target)['delay']; eligible=target_delay<=actor_delay
    evaluations=[]; retained=None; rng_word,rng_count=word,count
    score_profile=dict(idsMatch=True,stableOpponents=True,unchangedTeams=True,
        units=[dict(monster=False,keptSlots=7,mp=0,maxMp=0,golem=0,baselineGolem=0) for _ in range(2)])
    def status(u): return 'Critical' if u['hp'] <= u['maxHp']//5 else 'None'
    for i,candidate in enumerate(masks['emitted']):
        x,y=target['position'][0]-candidate['origin'][0],target['position'][1]-candidate['origin'][1]
        front='West' if x>0 else 'East' if x<0 else 'South' if y>0 else 'North'
        hit=100-target['evade'] if target['facing']==front else 100
        projection=dict(terminal=False,activeActor=True,acted=False,moved=actor['moved'],profileSupported=True,placementAllowed=True,
            order=candidate['order'],origin=actor['position'],destination=candidate['destination'],facing=actor['facing'],
            targetFacing=target['facing'],rngWord=rng_word,rngCount=rng_count,hitPercent=hit,
            damage=min(999,xa*power) if hit else 0,actorCt=actor['ct'],actorSpeed=actor['speed'],actorSlot=actor['slot'],
            actorStatus=status(actor),targetCt=target['ct'],targetSpeed=target['speed'],targetStatus=status(target),
            targetHp=target['hp'],targetMaxHp=target['maxHp'],deathCounter=target['deathCounter'],
            targetMoved=target['moved'],targetActed=target['acted'],elapsedTicks=state['ticks'])
        calls+=1
        result=proposed_scoring(dict(candidate=projection,profile=score_profile,actorHp=actor['hp'],actorMaxHp=actor['maxHp']))
        if result['disposition']=='Rejected':
            return reject('EvaluationRejected',i,dict(reason=result['reason'],projectionReason=result.get('projectionReason')))
        rng_word,rng_count=result['rngWord'],result['rngCount']
        proximity=max(0,10-abs(target['position'][0]-candidate['destination'][0])-abs(target['position'][1]-candidate['destination'][1])) if eligible else 0
        evaluations.append(dict(candidate=candidate,disposition=result['disposition'],score=result['score'],
            proximity=proximity,rngWord=rng_word,rngCount=rng_count))
        if result['score'] is not None:
            def rank(e):return dict(decided=0,priority=e['score']['priority'],hit=e['score']['hitPercent'],proximity=e['proximity'])
            if retained is None or compare(dict(first=rank(evaluations[i]),second=rank(evaluations[retained])))['firstIsHigher']:
                retained=i
    return dict(disposition='NoCandidates' if not evaluations else 'Scanned' if retained is not None else 'NoScoredCandidate',
        reason=None,failedIndex=None,evaluationRejection=None,
        outcome=dict(emitted=masks['emitted'],evaluations=evaluations,retainedIndex=retained,actorDelay=actor_delay,
            targetDelay=target_delay,eligibleEnemyId=state['targetId'] if eligible else None),
        rngWord=rng_word,rngCount=rng_count,scoreCalls=calls,additionalScanDraws=0,inputUnchanged=True)


def calculate(family,data):
    functions={'source-policy':source_policy,'source-masks':source_masks,'source-resume':source_resume,
               'source-roster-bound':source_roster_bound,'proposed-scan':proposed_scan}
    require(family in functions,'Family')
    return functions[family](data)


def validate(contract):
    require(contract['format']=='fft.ai-scan-source-description' and contract['schemaVersion']==1 and
            contract['ticket']=='ZOD-130' and contract['referenceRevision']==REVISION,'Contract identity')
    require(contract['execution']=='offline-source-and-proposed-scan-description' and contract['productionImplemented'] is False,'Execution boundary')
    seen=set();families=set()
    for row in contract['vectors']:
        require(row['id'] not in seen,'Duplicate vector');seen.add(row['id']);families.add(row['family'])
        kind='managed-design-proposal' if row['family'].startswith('proposed-') else 'source-derived-stage-description' if row['family']=='source-resume' else 'source-derived-isolated-kernel'
        require(row['expectationKind']==kind,'Classification')
        require(calculate(row['family'],row['inputs'])==row['expected'],'Vector mismatch: '+row['id'])
    require(len(seen)==contract['vectorCount']==56 and families=={'source-policy','source-masks','source-resume','source-roster-bound','proposed-scan'},'Inventory')
    require([r['id'] for r in contract['implementationCases']]==[f'ASN-{n:03}' for n in range(1,17)] and
            all(r['status']=='unexecuted' for r in contract['implementationCases']),'Unexecuted production inventory')
    return dict(vectors=len(seen),families=len(families),implementationCasesUnexecuted=16)


def verify_retail(executable,records):
    data=executable.read_bytes()
    for record in records:
        verify_prepared_record(executable,record)
        offset=record['address']-record['loadAddress']+0x800
        if record['symbol']=='g_ability_range_data[0]':
            facts=dict(staticRange=data[offset],staticAoe=data[offset+1],secondaryEvadeable=bool(data[offset+6]&2),
                cannotHitCaster=bool(data[offset+4]&1),canTargetEnemies=bool(data[offset+4]&64))
            require(facts==record['facts'],'Attack secondary facts')
        elif record['symbol']=='g_ability_data[0]':
            facts=dict(targetEnemies=bool(data[offset+4]&2),affectsStats=bool(data[offset+4]&8),affectsHp=bool(data[offset+4]&128))
            require(facts==record['facts'],'Attack primary facts')
        else:
            require(dict(menuType=data[offset],throwHitHalving=data[offset]==2)==record['facts'],'Attack menu facts')


def audit(root,reference=None,check_managed_baseline=False,executable=None):
    e=json.loads((root/EVIDENCE).read_text(encoding='utf-8'))
    require(e['ticket']=='ZOD-130' and e['reference']['revision']==REVISION,'Evidence identity')
    require(e['sourceConstants']['UNIT_SLOT_COUNT']==21,'Corrected source roster constant')
    require(e['fixture']==fingerprint(root,FIXTURE),'Fixture identity')
    for row in e['artifacts']:require(fingerprint(root,row['path'])==row,'Artifact identity')
    result=validate(json.loads((root/FIXTURE).read_text(encoding='utf-8')))
    for row in e['preservedFiles']:require(fingerprint(root,row['path'])==row,'History changed: '+row['path'])
    result.update(preservedFiles=len(e['preservedFiles']),managedBaselineFilesChecked=0,referenceFilesChecked=0,
                  referenceFunctionsChecked=0,retailRecordsChecked=0)
    if check_managed_baseline:
        for row in e['managedBaseline']:require(fingerprint(root,row['path'])==row,'Managed baseline changed')
        result['managedBaselineFilesChecked']=len(e['managedBaseline'])
    if reference is not None:
        revision=subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
        status=subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip()
        require(revision==REVISION and not status,'Reference revision/clean boundary')
        require(re.search(r'\bUNIT_SLOT_COUNT\s*=\s*21\b',(reference/'include/fft/unit.h').read_text(encoding='utf-8')) is not None,
                'Original has 21 unit slots')
        for row in e['reference']['files']:
            require(fingerprint(reference,row['path'])=={k:v for k,v in row.items() if k!='functions'},'Reference file')
            for function in row['functions']:
                require(function_fingerprint(reference/row['path'],function['symbol'])==function,'Reference function')
                result['referenceFunctionsChecked']+=1
            result['referenceFilesChecked']+=1
    if executable is not None:verify_retail(executable,e['retailRecords']);result['retailRecordsChecked']=len(e['retailRecords'])
    result.update(format='fft.ai-scan-source-audit',schemaVersion=1,passed=True,productionImplemented=False,
                  fullAiExecuted=False,originalExecuted=False)
    return result


def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--reference',type=Path);p.add_argument('--check-managed-baseline',action='store_true')
    p.add_argument('--executable',type=Path);p.add_argument('--output',type=Path,required=True)
    a=p.parse_args();result=audit(Path(__file__).resolve().parents[2],a.reference,a.check_managed_baseline,a.executable)
    with a.output.open('x',encoding='utf-8') as output:output.write(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))


if __name__=='__main__':main()
