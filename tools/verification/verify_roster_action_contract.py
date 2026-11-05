"""ZOD-290 isolated roster cast/usefulness/self descriptions; no original, C or managed execution."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require
from verify_roster_general_contract import describe as general_description,REVISION
from verify_roster_preparation_contract import describe as preparation_description

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-action-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-action-source-v1.json'


def usefulness(data,word,count,active):
    units=sorted(data['units'],key=lambda u:u['order']);actor=next(u for u in units if u['id']==data['actor'])
    require(type(active) is list and len(active)==len(set(active)) and set(active)<=set(u['id'] for u in units),'Active mask')
    targets=[];self_useful=False
    for u in units:
        if u['id'] not in active:continue
        raw=None
        if u['team']!=actor['team']:effect='Adverse'
        else:
            require(count<0xffffffffffffffff,'Draw budget');word=(word*1103515245+12345)&0xffffffff;count+=1;raw=(word>>16)&0x7fff
            effect='Useful' if raw%2==0 else 'None'
        if u==actor:self_useful=effect=='Useful'
        targets.append(dict(id=u['id'],effect=effect,raw=raw))
    return dict(targets=targets,selfUseful=self_useful,word=word,count=count)


def describe(kind,data):
    d=copy.deepcopy(data);actor=next(u for u in d['units'] if u['id']==d['actor'])
    # Reuse independently established input-domain checks, not a managed or original simulator.
    preparation_description('preparation',d)
    if kind=='usefulness':return usefulness(d,d['word'],d['count'],d['active'])
    require(kind in ('casts','entry'),'Description kind')
    d['kind']='scenarios';m=general_description(d);require(m['rejection']=='None','Admitted ordinary general prefix')
    if kind=='casts':
        retained={tuple(v['tile']):v['priority'] for v in d['retained']}
        require(all(type(v) is int and -32768<=v<=32767 for v in retained.values()),'Signed priority')
        fresh={tuple(t):-m['priority'] for t in m['mask']};prox={tuple(t):v for t,v in zip(m['m0'],m['proximity'])}
        foes=[u for u in d['units'] if u['team']!=actor['team']];rows=[]
        for index,action in enumerate(['Attack']+actor['actions']+['Frog']):
            origins=[];keys=[]
            if not actor['acted'] and action in ('Attack','Stone'):
                radius=1 if action=='Attack' else 4
                origins=[t for t in m['scenario1'] if any(abs(t[0]-u['tile'][0])+abs(t[1]-u['tile'][1])<=radius for u in foes)]
            if action=='Stone':
                for t in origins:
                    tile=tuple(t)
                    if tile not in fresh and tile not in retained:return dict(rejection='UnknownPriority',occurrence=index,tile=t)
                    keys.append(dict(tile=t,priority=fresh[tile] if tile in fresh else retained[tile],proximity=prox[tile],
                        provenance='Fresh' if tile in fresh else 'Retained'))
                keys.sort(key=lambda k:(k['priority']*65536+k['proximity'],-k['tile'][1],-k['tile'][0]))
            rows.append(dict(action=action,origins=origins,keys=keys))
        return dict(rejection='None',occurrences=rows,word=m['word'],count=m['count'])
    require(all(1<=u['pa']<=99 for u in d['units']),'Zero-bonus PA envelope')
    if actor['acted']:return dict(skipped=True,targets=[],attempts=[],best=None,word=m['word'],count=m['count'])
    f=usefulness(d,m['word'],m['count'],m['active']);attempts=[];best=None;word=f['word'];count=f['count']
    if any(t['effect']=='Useful' for t in f['targets']):
        for index in range(1 if m['d1']==actor['tile'] else 2):
            if not f['selfUseful']:attempts.append(dict(order='ActFirst' if index==0 else 'MoveFirst',disposition='NotValuable'));continue
            # Fresh self hit is immediate and has zero formula draws. Each order restores the same baseline.
            ct=min(60,actor['ct']+(0 if actor['moved'] or m['d1']!=actor['tile'] else 20))
            units=copy.deepcopy(d['units']);next(u for u in units if u['id']==actor['id'])['ct']=ct;elapsed=0
            while not any(u['ct']>=100 for u in units):
                elapsed+=1
                for u in units:u['ct']=min(254,u['ct']+u['speed'])
            chosen=min((u for u in units if u['ct']>=100),key=lambda u:(-u['ct'],u['order']))
            require(count<0xffffffffffffffff and d['ticks']+elapsed<=0xffffffffffffffff,'Clock budget')
            word=(word*1103515245+12345)&0xffffffff;count+=1
            attempts.append(dict(order='ActFirst' if index==0 else 'MoveFirst',disposition='Scored',before=actor['pa'],
                after=min(99,actor['pa']+1),endCt=ct,priority=m['priority']+3,hit=100,next=chosen['id'],elapsed=elapsed))
            if best is None:best=index
    return dict(skipped=False,targets=f['targets'],attempts=attempts,best=best,word=word,count=count)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-290' and fixture['referenceRevision']==REVISION and
        fixture['kind']=='isolated-roster-action-descriptions' and fixture['productionImplemented'] is False,'Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['kind'],row['input'])==row['expected'],'Roster action description '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Unique description IDs')
    return dict(rosterActionDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


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
