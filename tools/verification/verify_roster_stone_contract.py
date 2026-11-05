"""ZOD-297 source descriptions; trajectory facts are supplied, not computed geometry."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
from verify_roster_sword_contract import describe as damage_description
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-stone-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-stone-source-v1.json'


def describe_candidate(data):return damage_description(data,stone=True)


def describe_search(data):
    d=copy.deepcopy(data);origins=d['origins'];aims=d['aims'];events=[];consumed=[]
    require(len({tuple(o['tile']) for o in origins})==len(origins),'Distinct origins')
    require(len({tuple(a['tile']) for a in aims})==len(aims),'Distinct aims')
    require(all(-32768<=o['priority']<=32767 and 0<=o['proximity']<=255 for o in origins),'Known keys')
    while origins and aims:
        # Source minimizes the combined signed priority/proximity key; >= keeps last map tie.
        origin=min(origins,key=lambda o:(o['priority']*65536+o['proximity'],-o['tile'][1],-o['tile'][0]))
        origins.remove(origin)
        for aim in sorted(aims[:],key=lambda a:(a['tile'][1],a['tile'][0])):
            if sum(abs(x-y) for x,y in zip(origin['tile'],aim['tile']))>4:continue
            key=f"{origin['tile'][0]},{origin['tile'][1]}:{aim['id']}"
            require(key in d['clearance'],'Explicit trajectory fact')
            clear=d['clearance'][key];require(type(clear) is bool,'Clearance boolean')
            if not clear:
                events.append(dict(origin=origin['tile'],target=aim['id'],result='Blocked',draws=0));continue
            disposition='NotValuable' if aim['hit']==0 else 'Scored'
            events.append(dict(origin=origin['tile'],target=aim['id'],result=disposition,draws=4+(aim['upkeep'] if aim['hit'] else 0)))
            aims.remove(aim);consumed.append(aim['id'])
    return dict(events=events,consumed=consumed,remainingOrigins=len(origins),remainingAims=len(aims),draws=sum(e['draws'] for e in events))


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-297' and fixture['referenceRevision']==REVISION and
        fixture['productionImplemented'] is False,'Source description receipt')
    ids=[]
    for row in fixture['candidates']:
        require(describe_candidate(row['input'])==row['expected'],'Stone candidate '+row['id']);ids.append(row['id'])
    for row in fixture['search']:
        require(describe_search(row['input'])==row['expected'],'Stone search '+row['id']);ids.append(row['id'])
    require(len(ids)==len(set(ids)) and bool(ids),'Distinct vectors')
    return dict(rosterStoneCandidates=len(fixture['candidates']),rosterStoneSearches=len(fixture['search']),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    receipt=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==receipt['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in receipt['sourceFiles']:require(fingerprint(reference,f['path'])==f,'Source '+f['path'])
    for f in receipt['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(receipt['sourceFiles']),sourceFunctions=len(receipt['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();result=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(result,f,indent=2);f.write('\n')
    print(json.dumps(result))
