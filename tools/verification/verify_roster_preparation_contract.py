"""ZOD-283 isolated living-roster descriptions, not original/C/managed execution or general AI."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-preparation-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-preparation-source-v1.json'
REVISION='11464943bbdbd2a0af022d90cb7f10455cf911df'


def integer(v,lo,hi):
    require(type(v) is int and lo<=v<=hi,'Integer fact domain');return v


def describe(kind,data):
    units=copy.deepcopy(data['units']);require(type(units) is list and 1<=len(units)<=16,'Compact AI extent')
    for u in units:
        require(type(u['id']) is str and bool(u['id']),'Explicit ID')
        integer(u['order'],0,0x7fffffff);integer(u['team'],0,3);integer(u['speed'],1,255);integer(u['ct'],0,254)
        integer(u['maxHp'],1,65535);integer(u['hp'],1,u['maxHp']);integer(u['move'],0,255)
        require(len(u['tile'])==2 and all(type(v) is int and 0<=v<16 for v in u['tile']),'Flat coordinates')
        require(all(type(u[k]) is bool for k in ('moved','acted','support','conserve')),'Choice/policy facts')
        require(type(u['actions']) is list and len(u['actions'])<=32 and all(a in ('Self','Stone','Dash') for a in u['actions']),'Supported list occurrences')
    require(len({u['id'] for u in units})==len(units) and len({u['order'] for u in units})==len(units),'Distinct units/order')
    require(len({tuple(u['tile']) for u in units})==len(units),'Distinct occupied positions')
    units.sort(key=lambda u:u['order']); actor=next((u for u in units if u['id']==data['actor']),None)
    require(actor is not None and actor['ct']<100 and not (actor['moved'] and actor['acted']),'Active represented actor')
    cached=(max(0,100-actor['ct'])+actor['speed']-1)//actor['speed']
    original=copy.deepcopy(units); actor_live=next(u for u in original if u['id']==data['actor'])
    moved=actor['moved'] or kind=='movement' and data['destination']!=actor['tile']
    end=min(60,actor['ct']+(0 if actor['acted'] else 20)+(0 if moved else 20));actor['ct']=end
    elapsed=0
    while not any(u['ct']>=100 for u in units):
        elapsed+=1
        for u in units:u['ct']=min(254,u['ct']+u['speed'])
    chosen=min((u for u in units if u['ct']>=100),key=lambda u:(-u['ct'],u['order']))
    chosen['ct']%=100
    word=(integer(data['word'],0,0xffffffff)*1103515245+12345)&0xffffffff
    ticks=integer(data['ticks'],0,0xffffffffffffffff)+elapsed;require(ticks<=0xffffffffffffffff,'Tick budget')
    result=dict(turnEndCt=end,next=chosen['id'],elapsed=elapsed,ticks=ticks,
        word=word,count=integer(data['count'],0,0xfffffffffffffffe)+1,ct=[dict(id=u['id'],ct=u['ct']) for u in units])
    if kind in ('refresh','preparation'):
        result.update(cachedDelay=cached,enemy=[u['id'] for u in original if u['team']!=actor_live['team']],
            belowHalf=[u['id'] for u in original if u['hp']<u['maxHp']//2],
            belowThreeQuarters=[u['id'] for u in original if u['hp']*4<u['maxHp']*3])
    if kind=='preparation':
        require(actor_live['move']<=124,'Nonwrapping reach domain')
        allies=[u for u in original if u['team']==actor_live['team']];enemies=[u for u in original if u['team']!=actor_live['team']]
        share={u['id']:u['actions'].count('Self')*128//(1+len(u['actions'])) for u in original}
        low=min(allies,key=lambda u:share[u['id']])['id']
        mostly={u['id']: None if u in enemies else u['id']!=low and share[u['id']]>=77 for u in original}
        blocked=actor_live['support'];impaired=sum(u['hp']<=u['maxHp']//5 for u in allies)
        support=not blocked and len(allies)>=2 and len(allies)!=impaired
        policies={u['id']:u['support'] or not blocked and not support and actor_live['team']!=0 and u in allies for u in original}
        result.update(share=share,mostly=mostly,harmful=sum(1+u['actions'].count('Stone')+u['actions'].count('Dash') for u in enemies),
            support=support,single=support and len(enemies)==1,policyExit=blocked,
            counts=None if blocked else [len(allies),len(enemies),impaired],policies=policies,
            usable=[] if actor_live['acted'] else ['Attack']+actor_live['actions'],enemyReach=actor_live['move']+1,allyReach=actor_live['move'])
    elif kind=='movement':
        running=0;terms=[]
        for u in original:
            term=u['hp']*128//u['maxHp']+(-32 if u['hp']<=u['maxHp']//5 else 0)+357
            if u['team']!=actor_live['team']:term=-term
            running=((running+term+32768)%65536)-32768;terms.append(running)
        result.update(priority=running,running=terms,hit=100)
    else:require(kind=='refresh','Description kind')
    return result


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-283' and fixture['referenceRevision']==REVISION and
        fixture['kind']=='isolated-roster-preparation-descriptions' and fixture['productionImplemented'] is False,'Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['kind'],row['input'])==row['expected'],'Roster description '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Unique description IDs')
    return dict(rosterPreparationDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==r['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(reference,f['path'])==f,'Source file '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Source function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
