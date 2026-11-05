"""ZOD-277 isolated flat roster movement/sword descriptions; no original/C/managed execution."""
import argparse
from collections import deque
import json
from pathlib import Path
import subprocess

from verify_flat_movement_contract import STEPS, preferred_predecessor
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, resolve

ROOT=Path(__file__).resolve().parents[2]
EVIDENCE=ROOT/'docs/evidence/combat/roster-combat-source-v1.json'
FIXTURE=ROOT/'docs/fixtures/behavior/roster-combat-contract-v1.json'
REVISION='11464943bbdbd2a0af022d90cb7f10455cf911df'


def integer(value, low, high):
    require(type(value) is int and low<=value<=high,'Integer source domain'); return value


def describe(kind,data):
    w=integer(data['width'],1,16); h=integer(data['depth'],1,16); units=data['units']
    require(type(units) is list and 1<=len(units)<=21,'Original source slot extent')
    for u in units:
        require(type(u['id']) is str and bool(u['id']) and len(u['tile'])==2,'Unit identity/position')
        integer(u['tile'][0],0,w-1); integer(u['tile'][1],0,h-1); integer(u['team'],0,3); integer(u['hp'],0,50)
    require(len({u['id'] for u in units})==len(units) and len({tuple(u['tile']) for u in units})==len(units),'Distinct IDs/positions')
    by_id={u['id']:u for u in units}; require(data['actor'] in by_id,'Represented actor'); actor=by_id[data['actor']]
    if kind=='route':
        require(len(units)<=17 and all(u['hp']>0 for u in units),'Living movement-record domain')
        require(type(data['moved']) is bool,'Movement choice fact')
        move=min(124,integer(data['move'],0,255)); integer(data['jump'],0,255)
        origin=tuple(actor['tile']); occupied={tuple(u['tile']) for u in units if u!=actor}
        blocked={tuple(u['tile']) for u in units if u['team']!=actor['team']}
        best={origin:0}; pending=deque([origin])
        while pending:
            x,y=pending.popleft()
            for _,dx,dy,_ in STEPS:
                p=(x+dx,y+dy)
                if 0<=p[0]<w and 0<=p[1]<h and p not in blocked and p not in best:
                    best[p]=best[(x,y)]+1; pending.append(p)
        physical=sorted([p for p,d in best.items() if d<=move and p!=origin and p not in occupied],key=lambda p:(p[1],p[0]))
        origins=sorted([origin] if data['moved'] else [origin]+physical,key=lambda p:(p[1],p[0]))
        dest=tuple(data['destination']); path=None; packed=None; heading=None
        if dest in physical:
            backwards=[dest]; current=dest; preferred='East'
            while current!=origin:
                eligible=[name for name,dx,dy,_ in STEPS if best.get((current[0]+dx,current[1]+dy))==best[current]-1]
                selected=preferred_predecessor(preferred,eligible)
                _,dx,dy,_=next(s for s in STEPS if s[0]==selected)
                current=(current[0]+dx,current[1]+dy); backwards.append(current); preferred=selected
            path=list(reversed(backwards)); packed=[]
            for a,b in zip(path,path[1:]):
                heading,_,_,byte=next(s for s in STEPS if (s[1],s[2])==(b[0]-a[0],b[1]-a[1])); packed.append(byte)
        return dict(physical=[list(p) for p in physical],origins=[list(p) for p in origins],
                    path=[list(p) for p in path] if path else None,packed=packed,heading=heading,rngDraws=0)
    if kind=='sword':
        require(data['target'] in by_id and data['target']!=data['actor'],'Represented distinct target')
        target=by_id[data['target']]
        old=resolve(dict(seed=data['seed'],drawCount=data['count'],pa=data['pa'],power=data['power'],actor=actor['tile'],
            target=target['tile'],mapSize=[w,h],actorFacing='East',targetFacing='West',hp=target['hp'],classEvade=data['evade'],actorBrave=70,targetBrave=70))
        if old['moved'] and any(u['tile']==old['target'] for u in units): old.update(moved=False,target=target['tile'])
        return {k:old[k] for k in ('hit','critical','damage','hp','target','moved','rngWord','drawCount')}
    raise ValueError('Unknown roster combat description')


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-277' and fixture['referenceRevision']==REVISION
        and fixture['productionImplemented'] is False and fixture['kind']=='isolated-roster-combat-descriptions','Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['kind'],row['input'])==row['expected'],'Source description: '+row['id']); ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct description IDs')
    return dict(rosterCombatDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    receipt=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==receipt['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for row in receipt['sourceFiles']: require(fingerprint(reference,row['path'])==row,'Source file '+row['path'])
    for row in receipt['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']); require(all(row[k]==v for k,v in actual.items()),'Source function '+row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,
        sourceFiles=len(receipt['sourceFiles']),sourceFunctions=len(receipt['sourceFunctions']))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument('--reference',type=Path,required=True); parser.add_argument('--output',type=Path)
    args=parser.parse_args(); report=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f: json.dump(report,f,indent=2); f.write('\n')
    print(json.dumps(report))
