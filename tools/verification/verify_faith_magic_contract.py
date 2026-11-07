"""ZOD-334 independent signed-store/Faith/magic-amount descriptions and pinned-source audit. No C execution."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/faith-magic-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/faith-magic-source-v1.json'

def integer(value,low,high):
    require(type(value) is int and low <= value <= high,'Integer width');return value

def signed16(value): return (value+32768)%65536-32768
def divide(value,denominator): return (-1 if value < 0 else 1)*(abs(value)//denominator)

def faith(data):
    stored=integer(data['stored'],0,255)
    require(type(data['faithStatus']) is bool and type(data['innocentStatus']) is bool,'Explicit status facts')
    return 0 if data['innocentStatus'] else 100 if data['faithStatus'] else stored

def scale(amount,caster,target):
    integer(amount,-32768,32767)
    return signed16(divide(amount*faith(target)*faith(caster),10000))

def product(data):
    kind=data['kind'];require(kind in ('Damage','Healing'),'Supported amount branch')
    xa=integer(data['magicAttack'],0,255);power=integer(data['power'],0,255);mods=data['modifiers']
    require(set(mods) <= {'strengthensElement','magicAttackUp','magicDefenseUp','targetShell','targetChickenOrFrog','compatibility'},'Known modifiers')
    for k,v in mods.items():
        if k!='compatibility':require(type(v) is bool,'Boolean modifier')
    steps=[('strengthensElement',5,4),('magicAttackUp',4,3)]
    if kind=='Damage':steps += [('magicDefenseUp',2,3),('targetShell',2,3),('targetChickenOrFrog',3,2)]
    for flag,n,d in steps:
        if mods.get(flag,False):xa=signed16(divide(xa*n,d))
    compatibility=mods.get('compatibility','Neutral')
    factors={'Neutral':(0,100),'Good':(1,100),'Best':(1,50),'Bad':(-1,100),'Worst':(-1,50)}
    if compatibility!='Serpentarius':
        require(compatibility in factors,'Classified compatibility')
        sign,divisor=factors[compatibility];xa=max(1,signed16(xa+sign*((xa*25)%65536//divisor)))
    product=signed16(xa*power)
    return dict(xa=xa,product=product)

def amount(data):
    result=product(data)
    return dict(result,amount=scale(result['product'],data['caster'],data['target']))

def validate(fixture):
    require(fixture['ticket']=='ZOD-334' and fixture['referenceRevision']==REVISION and fixture['productionImplemented'] is False,'Source description receipt')
    ids=[]
    for r in fixture['faith']:
        d=r['input'];require(scale(d['amount'],d['caster'],d['target'])==r['expected'],'Faith '+r['id']);ids.append(r['id'])
    for r in fixture['products']:
        integer(r['xa'],-32768,32767);integer(r['ya'],-32768,32767)
        require(signed16(r['xa']*r['ya'])==r['expected'],'Product '+r['id']);ids.append('product-'+r['id'])
    for r in fixture['amounts']:
        require(amount(r['input'])==r['expected'],'Amount '+r['id']);ids.append('amount-'+r['id'])
    require(len(ids)==len(set(ids)),'Distinct descriptions')
    return dict(faithDescriptions=len(fixture['faith']),productDescriptions=len(fixture['products']),amountDescriptions=len(fixture['amounts']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'))
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'File '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']);require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    data=executable.read_bytes()
    require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Verified prepared SCUS_942.21')
    start=0x8005fbf0-0x80010000+0x800
    for index,power,formula in [(1,14,12),(2,20,12),(3,30,12),(4,40,12),(16,14,8),(17,18,8),(18,24,8),(19,32,8)]:
        row=data[start+14*index:start+14*(index+1)]
        require(row[8:11]==bytes([formula,0,power]),'Prepared tier formula/X/Y '+str(index))
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),preparedTiersVerified=8)

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference',type=Path,required=True);parser.add_argument('--prepared-executable',type=Path,required=True);parser.add_argument('--output',type=Path)
    args=parser.parse_args();result=audit(args.reference,args.prepared_executable)
    if args.output:
        with args.output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
