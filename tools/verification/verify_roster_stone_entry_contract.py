"""ZOD-301 isolated fresh roster Stone source descriptions; no managed/original/C execution."""
import argparse,copy,json,subprocess
from pathlib import Path
from verify_roster_general_contract import describe as general_description
from verify_roster_stone_contract import describe_candidate
from verify_roster_trajectory_contract import describe as trajectory_description
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-stone-entry-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-stone-entry-source-v1.json'


def describe(data, *, fresh=None):
    d=copy.deepcopy(data);d['kind']='scenarios';m=general_description(d) if fresh is None else fresh[0];require(m['rejection']=='None','Ordinary prefix')
    actor=next(u for u in d['units'] if u['id']==d['actor']);word=m['word'] if fresh is None else fresh[1];count=m['count'] if fresh is None else fresh[2]
    require(1<=d['occurrence']<=len(actor['actions']) and actor['actions'][d['occurrence']-1]=='Stone','Selected structural Stone')
    if actor['acted']:return dict(rejection='None',skipped=True,ranked=[],aims=[],origins=[],visits=[],candidates=[],best=None,comparison=None,remainingOrigins=0,remainingAims=0,word=word,count=count)
    foes=sorted((u for u in d['units'] if u['team']!=actor['team']),key=lambda u:(u['tile'][1],u['tile'][0]))
    dist=lambda a,b:sum(abs(x-y) for x,y in zip(a,b));proximity={tuple(t):p for t,p in zip(m['m0'],m['proximity'])}
    keys=[]
    for tile in m['scenario1']:
        if not any(dist(tile,u['tile'])<=4 for u in foes):continue
        if tile in m['mask']:priority=(-m['priority']+32768)%65536-32768
        else:
            retained=next((v for v in d['retained'] if v['tile']==tile),None)
            if retained is None:return dict(rejection='UnknownPriority',tile=tile,word=d['word'],count=d['count'])
            priority=retained['priority']
        keys.append(dict(tile=tile,priority=priority,proximity=proximity[tuple(tile)]))
    keys.sort(key=lambda k:(k['priority']*65536+k['proximity'],-k['tile'][1],-k['tile'][0]));remaining=keys[:];aims=foes[:]
    origins=[];visits=[];candidates=[];best=None
    while remaining and aims:
        origin=remaining.pop(0)['tile'];origins.append(origin)
        for target in aims[:]:
            if not 1<=dist(origin,target['tile'])<=4:continue
            trajectory=trajectory_description(dict(actor=d['actor'],target=target['id'],origin=origin,units=d['units']))
            if not trajectory['clear']:
                visits.append(dict(origin=origin,target=target['id'],clear=False,candidate=None,remainingAims=len(aims),word=word,count=count));continue
            first=origin==actor['tile'];destination=m['d1'] if first else origin
            request=copy.deepcopy(d);request.update(target=target['id'],destination=destination,order='ActThenMove' if first else 'MoveThenAct',move=actor['move'],moved=actor['moved'],clearTrajectory=True,word=word,count=count)
            result=describe_candidate(request);word=result['word'];count=result['count']
            row=dict(origin=origin,target=target['id'],destination=destination,actFirst=first,disposition=result['disposition'],priority=result.get('priority'),hit=result.get('hit'),proximity=proximity[tuple(destination)],word=word,count=count)
            if row['priority'] is not None:
                key=lambda c:(c['priority'],c['hit'],-c['proximity'])
                if best is None or key(row)>key(candidates[best]):best=len(candidates)
            aims.remove(target);visits.append(dict(origin=origin,target=target['id'],clear=True,candidate=len(candidates),remainingAims=len(aims),word=word,count=count));candidates.append(row)
    comparison=None
    if best is not None:
        a=(candidates[best]['priority'],candidates[best]['hit']);b=(m['priority'],100);comparison='Above' if a>b else 'Below' if a<b else 'Equal'
    return dict(rejection='None',skipped=False,ranked=[k['tile'] for k in keys],aims=[u['id'] for u in foes],origins=origins,visits=visits,candidates=candidates,best=best,comparison=comparison,remainingOrigins=len(remaining),remainingAims=len(aims),word=word,count=count)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-301' and fixture['referenceRevision']==REVISION and fixture['productionImplemented'] is False,'Source receipt')
    ids=[]
    for row in fixture['vectors']:require(describe(row['input'])==row['expected'],'Fresh Stone '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterStoneEntryDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==r['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(reference,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
