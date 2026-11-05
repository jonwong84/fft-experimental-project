"""ZOD-296 independent fresh roster Attack tapes; no original, C or managed execution."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION
from verify_roster_general_contract import describe as general_description
from verify_roster_sword_contract import describe as sword_description

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-attack-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-attack-source-v1.json'


def describe(data):
    d=copy.deepcopy(data);d['kind']='scenarios';m=general_description(d);require(m['rejection']=='None','Ordinary general prefix')
    actor=next(u for u in d['units'] if u['id']==d['actor']);word=m['word'];count=m['count']
    if actor['acted']:return dict(skipped=True,origins=[],targets=[],candidates=[],best=None,comparison=None,word=word,count=count)
    require(0<=d['power']<=255 and (2*max(1,actor['pa'])-1)*d['power']<=32767,'Bound ordinary sword')
    foes=sorted((u for u in d['units'] if u['team']!=actor['team']),key=lambda u:(u['tile'][1],u['tile'][0]))
    distance=lambda a,b:abs(a[0]-b[0])+abs(a[1]-b[1])
    origins=[t for t in m['scenario1'] if any(distance(t,u['tile'])<=1 for u in foes)]
    candidates=[];best=None;proximity={tuple(t):p for t,p in zip(m['m0'],m['proximity'])}
    for tile in origins:
        for target in foes:
            if distance(tile,target['tile'])!=1:continue
            first=tile==actor['tile'];destination=m['d1'] if first else tile;order='ActThenMove' if first else 'MoveThenAct'
            request=copy.deepcopy(d);request.update(target=target['id'],order=order,destination=destination,
                move=actor['move'],moved=actor['moved'],word=word,count=count)
            result=sword_description(request);word=result['word'];count=result['count']
            row=dict(origin=tile,target=target['id'],destination=destination,order=order,disposition=result['disposition'],
                priority=result.get('priority'),hit=result.get('hit'),proximity=proximity[tuple(destination)],word=word,count=count)
            if row['priority'] is not None:
                key=lambda c:(c['priority'],c['hit'],-c['proximity'])
                if best is None or key(row)>key(candidates[best]):best=len(candidates)
            candidates.append(row)
    comparison=None
    if best is not None:
        a=(candidates[best]['priority'],candidates[best]['hit']);b=(m['priority'],100)
        comparison='Above' if a>b else 'Below' if a<b else 'Equal'
    return dict(skipped=False,origins=origins,targets=[u['id'] for u in foes],candidates=candidates,best=best,
        comparison=comparison,word=word,count=count)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-296' and fixture['referenceRevision']==REVISION and
        fixture['kind']=='isolated-fresh-roster-attack-descriptions' and fixture['productionImplemented'] is False,'Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['input'])==row['expected'],'Roster Attack description '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Unique description IDs')
    return dict(rosterAttackDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


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
