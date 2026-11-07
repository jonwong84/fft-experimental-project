"""ZOD-304 actual roster self settlement descriptions; no managed/original/C execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2];FIXTURE=ROOT/'docs/fixtures/behavior/roster-execution-contract-v1.json';EVIDENCE=ROOT/'docs/evidence/ai/roster-execution-source-v1.json'


def describe(d):
    require(1<=d['pa']<=99 and 0<=d['ct']<100 and type(d['moved']) is bool,'Ordinary active self facts')
    return dict(pa=min(99,d['pa']+1),actCt=min(60,d['ct']) if d['moved'] else d['ct'],endCt=min(60,d['ct']+(0 if d['moved'] else 20)),
        pendingAfterAct=d['moved'],paChanged=d['pa']<99,actionTaken=True,draws=0,peersChanged=False)


def validate(f):
    require(f['ticket']=='ZOD-304' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source receipt')
    ids=[]
    for row in f['vectors']:require(describe(row['input'])==row['expected'],'Self settlement '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct descriptions')
    return dict(rosterSelfSettlementDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(ref):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'));require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==REVISION,'Pinned source')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=ref,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(ref,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        a=function_fingerprint(ref/f['path'],f['symbol']);require(all(f[k]==v for k,v in a.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
