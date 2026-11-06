"""ZOD-387 refresh ordering descriptions; no original/C/managed execution."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/status-refresh-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/status-refresh-source-v1.json'

def refresh(d):
    before=set(d['oldInnate'])|set(d['inflicted']);innate=set(d['newInnate']);inflicted=set(d['inflicted']);timers=dict(d['timers'])
    if d['floatAbility']:innate.add('Float')
    immunity=set(d['newImmunity'])-innate
    for name in d['timedOrder']:
        if name in inflicted and timers[name]==0:timers[name]=d['seeds'][name]
    if not d['skipStatus']:
        if d['hp']<=d['maxHp']//5:inflicted.add('Critical')
        else:inflicted.discard('Critical')
    current=innate|inflicted
    changes=[] if d['skipStatus'] or d['mode']!='Execute' else [[name,'Add' if name in current else 'Remove'] for name in d['statusOrder'] if (name in before)!=(name in current)]
    return dict(innate=sorted(innate),immunity=sorted(immunity),inflicted=sorted(inflicted),current=sorted(current),timers=timers,changes=changes)

def validate(f):
    require(f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['cases']:require(refresh(r['input'])==r['expected'],'Refresh '+r['id'])
    return dict(descriptions=len(f['cases']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned source')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean source')
    source=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in source['sourceFiles']:require(fingerprint(reference,r['path'])==r,'File '+r['path'])
    for r in source['sourceFunctions']:
        a=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Function '+r['symbol'])
    data=executable.read_bytes();require(hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared edition')
    start=0x80065de4-0x80010000+0x800
    for r in source['statusRecords']:require(data[start+16*r['id']:start+16*(r['id']+1)].hex()==r['hex'],'Status record '+str(r['id']))
    clock=json.loads((ROOT/'docs/evidence/combat/ordinary-magic-clock-source-v1.json').read_text(encoding='utf-8'))
    for r in clock['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Clock file '+r['path'])
    for r in clock['sourceFunctions']:
        a=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Clock function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),preparedStatusRecords=len(source['statusRecords']),clockSourceFiles=len(clock['sourceFiles']),clockSourceFunctions=len(clock['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
