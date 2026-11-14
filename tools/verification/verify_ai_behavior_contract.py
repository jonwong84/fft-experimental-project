"""ZOD-326 independent static AI-flag semantics; no original/C/managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2];FIXTURE=ROOT/'docs/fixtures/behavior/ai-behavior-contract-v1.json';SOURCE=ROOT/'docs/evidence/ai/ai-behavior-source-v1.json'
BITS=[('TargetsAllies',0),('TargetsEnemies',1),('Unequips',2),('AffectsStats',3),('AddsStatus',4),('CancelsStatus',5),('AffectsMp',6),('AffectsHp',7),
      ('AffectedBySilence',8),('Evadeable',9),('AffectedByFaith',10),('RandomUse',11),('FollowTarget',12),('UndeadReverse',13),('Reflectable',14),('TargetsMapTiles',15),
      ('PhysicalAttack',16),('MagicalAttack',17),('DirectTrajectory',22),('ArcTrajectory',23)]

def describe(flags):
    require(type(flags) is list and len(flags)==4 and all(type(v) is int and 0<=v<=255 for v in flags),'Four unsigned original bytes')
    word=flags[0]+256*flags[1]+65536*flags[2]
    behavior={name:bool(word&(1<<bit)) for name,bit in BITS};behavior['HasExhaustiveAreaShape']=bool(word&0x3c0000)
    return dict(behavior=behavior,usable=bool(flags[3]&128),motion=bool(flags[3]&1),unresolved=bool(flags[3]&126))

def validate(f):
    require(f['ticket']=='ZOD-326' and f['referenceRevision']==REVISION and not f['productionImplemented'],'Source description receipt')
    for row in f['vectors']:require(describe(row['flags'])==row['expected'],'AI flag description '+row['id'])
    require(len(f['vectors'])==len({r['id'] for r in f['vectors']}),'Distinct vectors')
    return dict(aiFlagDescriptions=len(f['vectors']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(ref):
    r=json.loads(SOURCE.read_text(encoding='utf-8'));require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==REVISION,'Pinned source')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=ref,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(ref,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(ref/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
