"""ZOD-310 source dispatch and separately labelled managed authority/RNG contracts."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2];FIXTURE=ROOT/'docs/fixtures/behavior/roster-authority-contract-v1.json';EVIDENCE=ROOT/'docs/evidence/ai/roster-authority-source-v1.json'


def advance(word,delta):
    a=1103515245;c=12345;mul=1;inc=0
    while delta:
        if delta&1:mul=mul*a&0xffffffff;inc=(inc*a+c)&0xffffffff
        c=(a+1)*c&0xffffffff;a=a*a&0xffffffff;delta>>=1
    return(word*mul+inc)&0xffffffff


def describe(kind,d):
    if kind=='source-dispatch':
        if not d['moved'] and (d['acted'] or not d['actFirst']) and d['destination']!=d['origin']:return 'Move'
        return 'End' if d['acted'] or not d['action'] else 'Act'
    if kind=='managed-turn-route':return 'RetainedCommand' if d['continuation'] else 'FinishFacing' if d['pending'] else 'PostActCommand' if d['active'] else None
    require(kind=='managed-rng-checkpoint','Contract kind')
    require(all(type(d[k]) is int and 0<=d[k]<=(0xffffffff if 'Word' in k else 0xffffffffffffffff) for k in ('beforeWord','afterWord','beforeCount','afterCount')),'Unsigned checkpoint fields')
    return d['afterCount']>=d['beforeCount'] and advance(d['beforeWord'],d['afterCount']-d['beforeCount'])==d['afterWord']


def validate(f):
    require(f['ticket']=='ZOD-310' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source/managed contract receipt')
    ids=[]
    for row in f['vectors']:require(describe(row['kind'],row['input'])==row['expected'],'Authority contract '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterAuthorityDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(ref):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'));require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==REVISION,'Pinned source')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=ref,text=True).strip(),'Clean source')
    for f in r['sourceFiles']:require(fingerprint(ref,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(ref/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
