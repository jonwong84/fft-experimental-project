"""ZOD-341 bounded HP result finalization and private composition. No original/C execution."""
import argparse,copy,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION
from verify_faith_magic_contract import integer
from verify_magic_elements_contract import project,route
from verify_magic_proc_contract import decision,audit as audit_proc

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-finalization-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-finalization-source-v1.json'

def finalize(d):
    for k in ('damage','healing'):integer(d[k],-32768,32767)
    integer(d['accuracy'],0,65535);integer(d['miss'],0,13)
    for value in [d['hit'],d['clear'],*d['types'].values(),*d['effects'].values()]:require(type(value) is bool,'Boolean result flag')
    r=copy.deepcopy(d)
    if not r['accuracy']:r['hit']=False
    for k in ('damage','healing'):
        if r[k]>999:r[k]=999
    if any(r['effects'].values()):r['types']['pseudo']=True
    return r

def supported(p):
    return p is not None and all(p[k] for k in ('living','noBlocked','noGolem','emptyStatus','noKnockback','hpOnly')) and p['entry'] is not None and p['entry']['primary'] and p['entry']['noPending']

def compose(d):
    p=d['profile'];projection,element=project(d['input'],d['context'])
    def reject(reason,proc='None'):
        return dict(accepted=False,rejection=reason,procRejection=proc,draws=0,outcome=None,word=d['word'],count=d['count'])
    if not supported(p) or (not projection['nullified'] and not p['entry']['clearFlag']):return reject('UnsupportedContext')
    hp=route(d['input'],d['context'],d['targetIsUndead'])
    proc=decision(dict(formula=d['input']['kind'],invocation='ElementNullified' if projection['nullified'] else 'FamilyTail',mode=d['mode'],word=d['word'],count=d['count'],profile=p['entry']))
    if not proc['accepted']:return reject('ProcDecisionRejected',proc['rejection'])
    nullified=projection['nullified']
    raw=dict(hit=not nullified,accuracy=0 if nullified else d['accuracy'],miss=5 if nullified else 0,
        damage=hp['hpDamage'],healing=hp['hpHealing'],clear=hp['clearFirstCanceledStatusByte'],
        types=dict(damage=hp['route']=='Damage',healing=hp['route']=='Healing',pseudo=False),
        effects=dict(absorb=element is not None and element['absorbed'],weak=element is not None and element['weaknessApplied'],added=proc['setFlag']))
    return dict(accepted=True,rejection='None',procRejection='None',outcome=finalize(raw),draws=proc['count']-d['count'],
        procDisposition=proc['disposition'],setFlag=proc['setFlag'],word=proc['word'],count=proc['count'])

def validate(f):
    require(f['ticket']=='ZOD-341' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source description identity')
    for row in f['raw']:require(finalize(row['input'])==row['expected'],'Raw '+row['id'])
    for row in f['composed']:require(compose(row)==row['expected'],'Composed '+row['id'])
    for name in ('raw','composed'):require(len({r['id'] for r in f[name]})==len(f[name]),'Distinct '+name+' IDs')
    return dict(rawDescriptions=len(f['raw']),composedDescriptions=len(f['composed']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'));require(source['ticket']=='ZOD-341' and source['referenceRevision']==REVISION,'Source receipt identity')
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Source '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']);require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    prepared=audit_proc(reference,executable)
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,
        sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),emptyStatusRecordVerified=prepared['emptyStatusRecordVerified'],fireTiersVerified=prepared['fireTiersVerified'])

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
