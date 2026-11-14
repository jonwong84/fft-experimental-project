"""ZOD-347 bounded entry composition descriptions. No original/C/managed execution."""
import argparse,json
from pathlib import Path
import verify_magic_evasion_contract as evasion
import verify_magic_finalization_contract as final
from verify_sword_knockback_contract import require,REVISION
from verify_magic_elements_contract import project

FIXTURE=Path(__file__).resolve().parents[2]/'docs/fixtures/behavior/magic-entry-contract-v1.json'

def resolve(d):
    f=d['formula'];p=f['profile'];entry=d['entry'];word=d['word'];count=d['count']
    require(f['mode'] in ('Execute','Preview','AiSimulation'),'Known mode')
    project(f['input'],f['context']) # Pure argument/domain validation before private draws.
    def reject(reason):return dict(accepted=False,rejection=reason,outcome=None,evasion=None,procDisposition=None,setFlag=None,word=word,count=count)
    if entry is None or not entry['fresh'] or not entry['noEarlier'] or not entry['nonzero'] or f['accuracy']!=100 or not final.supported(p) or not p['entry']['clearFlag']:return reject('UnsupportedContext')
    next_word=word;next_count=count;diag=None
    if f['input']['kind']=='Damage':
        rolled=evasion.roll(dict(equipment=d['equipment'],modifiers=d['modifiers'],mode=f['mode'],accuracy=f['accuracy'],evadeable=d['evadeable'],word=word,count=count))
        if not rolled['accepted']:return reject('RandomDrawCountExhausted')
        diag=rolled['outcome'];next_word=rolled['word'];next_count=rolled['count']
        if not diag['hit']:
            frame=dict(hit=False,accuracy=diag['accuracy'],miss=diag['miss'],damage=0,healing=0,types=dict(damage=False,healing=False,pseudo=False),effects=dict(absorb=False,weak=False,added=False),clear=False)
            return dict(accepted=True,rejection='None',outcome=final.finalize(frame),evasion=diag,procDisposition=None,setFlag=None,word=next_word,count=next_count)
    tail=final.compose(dict(f,accuracy=diag['accuracy'] if diag is not None else f['accuracy'],word=next_word,count=next_count))
    if not tail['accepted']:return reject('RandomDrawCountExhausted' if tail['procRejection']=='RandomDrawCountExhausted' else 'UnsupportedContext')
    return dict(accepted=True,rejection='None',outcome=tail['outcome'],evasion=diag,procDisposition=tail['procDisposition'],setFlag=tail['setFlag'],word=tail['word'],count=tail['count'])

def validate(f):
    require(f['ticket']=='ZOD-347' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source description identity')
    for row in f['entries']:require(resolve(row)==row['expected'],'Entry '+row['id'])
    require(len({r['id'] for r in f['entries']})==len(f['entries']),'Distinct entry IDs')
    return dict(entryDescriptions=len(f['entries']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    source=evasion.audit(reference);prepared=final.audit(reference,executable)
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=source['sourceFiles'],sourceFunctions=source['sourceFunctions'],emptyStatusRecordVerified=prepared['emptyStatusRecordVerified'],fireTiersVerified=prepared['fireTiersVerified'])

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
