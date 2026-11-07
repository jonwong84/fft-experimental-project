"""ZOD-337-339 stored magic transforms/composition/routing descriptions and source audit. No original/C execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION
from verify_faith_magic_contract import integer,signed16,divide,product,scale

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-elements-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-elements-source-v1.json'

def transform(d):
    amount=integer(d['amount'],-32768,32767);elements=integer(d['elements'],0,255);weather=integer(d['weather'],0,7)
    for name in ['absorb','nullify','half','weak']:integer(d[name],0,255)
    steps=[]
    if weather in (3,4):
        if elements&128:steps.append((3,4))
        if elements&64:steps.append((5,4))
    if weather in (6,7) and elements&32:steps.append((5,4))
    for n,denominator in steps:amount=signed16(divide(amount*n,denominator))
    after_weather=amount;absorbed=bool(elements&d['absorb'])
    if elements&d['nullify']:return dict(weatherAmount=after_weather,amount=0,nullified=True,absorbed=False,weaknessApplied=False)
    if elements&d['half']:amount=divide(amount,2)
    weak=bool(elements&d['weak'])
    if weak:amount=signed16(amount*2)
    return dict(weatherAmount=after_weather,amount=amount,nullified=False,absorbed=absorbed,weaknessApplied=weak)

def project(d,context):
    prefix=product(d)
    if d['kind']=='Healing':
        return dict(product=prefix['product'],preFaith=None,amount=scale(prefix['product'],d['caster'],d['target']),nullified=False),None
    element=transform(dict(context,amount=prefix['product']))
    return dict(product=prefix['product'],preFaith=element['amount'],amount=None if element['nullified'] else scale(element['amount'],d['caster'],d['target']),nullified=element['nullified']),element

def route(d,context,undead):
    require(type(undead) is bool,'Explicit target Undead fact')
    projection,element=project(d,context)
    if projection['nullified']:return dict(route='Nullified',hpDamage=0,hpHealing=0,clearFirstCanceledStatusByte=False)
    value=projection['amount'];clear=False
    if d['kind']=='Healing':healing=not undead;clear=undead
    else:healing=element['absorbed']
    return dict(route='Healing' if healing else 'Damage',hpDamage=0 if healing else value,hpHealing=value if healing else 0,clearFirstCanceledStatusByte=clear)

def validate(f):
    require(f['ticket']=='ZOD-337' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source description identity')
    names=[]
    for row in f['transforms']:require(transform(row['input'])==row['expected'],'Transform '+row['id']);names.append('t-'+row['id'])
    for row in f['projections']:require(project(row['input'],row['context'])[0]==row['expected'],'Projection '+row['id']);names.append('p-'+row['id'])
    for row in f['routes']:require(route(row['input'],row['context'],row['targetIsUndead'])==row['expected'],'Routing '+row['id']);names.append('r-'+row['id'])
    require(len(names)==len(set(names)),'Distinct vectors')
    return dict(transformDescriptions=len(f['transforms']),projectionDescriptions=len(f['projections']),routingDescriptions=len(f['routes']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'));require(source['ticket']=='ZOD-337' and source['referenceRevision']==REVISION,'Source receipt identity')
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Source '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']);require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),referenceRevision=REVISION,referenceClean=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
