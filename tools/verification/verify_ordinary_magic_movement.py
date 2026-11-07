"""Fresh source-only movement audit, without requiring historical managed snapshot paths."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import REVISION,fingerprint,function_fingerprint,require
from verify_flat_movement_contract import validate as validate_flat
from verify_voluntary_move_contract import validate as validate_voluntary

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/ordinary-magic-movement-v1.json'

def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['referenceRevision']==REVISION,'Fixture identity')
    require(len(fixture['cases'])==7 and len({r['id'] for r in fixture['cases']})==7,'Cancellation descriptions')
    for row in fixture['cases']:
        cancels=row['charging'] and row['counter']!=255 and row['skillset']==8
        require(cancels==row['cancels'],'Cancellation '+row['id'])
    return dict(cancellationDescriptions=len(fixture['cases']))

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    counts=[]
    for name,validator in [('flat',validate_flat),('voluntary',validate_voluntary)]:
        evidence_path='docs/evidence/ai/'+('flat-movement-contract-v1.json' if name=='flat' else 'voluntary-move-contract-v1.json')
        evidence=json.loads((ROOT/evidence_path).read_text(encoding='utf-8'))
        require(evidence['reference']['revision']==REVISION,'Historical source identity')
        fixture=evidence['fixture'];require(fingerprint(ROOT,fixture['path'])==fixture,'Unchanged historical descriptions')
        model=validator(json.loads((ROOT/fixture['path']).read_text(encoding='utf-8')))
        functions=0
        for row in evidence['reference']['files']:
            require(fingerprint(reference,row['path'])=={k:v for k,v in row.items() if k!='functions'},'Source '+row['path'])
            for fn in row['functions']:
                require(function_fingerprint(reference/row['path'],fn['symbol'])==fn,'Function '+fn['symbol']);functions+=1
        counts.append(dict(contract=name,sourceFiles=len(evidence['reference']['files']),sourceFunctions=functions,descriptions=model['vectors']))
    source=json.loads((ROOT/'docs/evidence/combat/ordinary-magic-movement-source-v1.json').read_text())
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'File '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol'])
        require(all(row[k]==v for k,v in actual.items()),'Current function '+row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text())),referenceRevision=REVISION,referenceClean=True,
        sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),reusedSourceContracts=counts,
        historicalManagedPathsChecked=False,originalExecuted=False,cExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
