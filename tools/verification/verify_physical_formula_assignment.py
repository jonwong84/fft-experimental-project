"""Independent bare physical formula descriptions; source-only, never executes reference C."""
import argparse
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/physical-formula-assignment-v1.json'
RECEIPT=ROOT/'docs/evidence/combat/physical-formula-assignment-source-v1.json'


def describe(data):
    formula=data['formula'];pa=data['pa'];x=data['x'];y=data['y'];mode=data['mode']
    require(formula in (50,55) and mode in ('Execution','Preview','Ai'),'Supported formula/mode')
    require(1<=pa<=99 and 0<=x<=255 and 0<=y<=255,'Source operands')
    require(formula!=50 or (197+(99+y)//2)*max(1,x)<=32767,'Signed16 envelope')
    word=data['word'];draws=data['count'];entry=(word,draws)
    def draw():
        nonlocal word,draws
        if draws==0xffffffffffffffff:raise OverflowError()
        draws+=1;word=(word*1103515245+12345)&0xffffffff
        return (word>>16)&32767
    def roll():return draw() if mode=='Execution' else 16384
    try:
        hit=True;accuracy=100-data['classEvade'] if data['evadeable'] else 100
        if data['evadeable']:
            for chance in (0,0,0,data['classEvade']):
                value=draw()*100//32768
                if mode=='Execution' and value<chance:hit=False;accuracy=0;break
        hit=hit and accuracy>0;critical=False;attempted=False;damage=0
        if hit:
            if formula==50:
                critical=roll()*100//32768<4
                xa=pa+roll()*pa//32768 if critical else pa
                damage=(xa+(pa+y)//2)*(1+roll()*x//32768);attempted=critical
            else:
                damage=pa*(1+roll()*y//32768)
                if mode=='Execution':
                    attacker=draw()*data['attackerBrave']//32768
                    attempted=draw()*data['targetBrave']//32768<attacker
            damage=min(999,damage)
        return dict(accepted=True,hit=hit,accuracy=accuracy,damage=damage,critical=critical,attempted=attempted,word=word,count=draws)
    except OverflowError:return dict(accepted=False,word=entry[0],count=entry[1])


def validate(fixture):
    require(fixture['referenceRevision']==REVISION and fixture['originalExecuted'] is False and fixture['cExecuted'] is False,'Source-only identity')
    require(len(fixture['cases'])==18 and len({r['id'] for r in fixture['cases']})==18,'Literal formula vectors')
    for row in fixture['cases']:require(describe(row['input'])==row['expected'],'Physical formula '+row['id'])
    return dict(physicalFormulaDescriptions=18,sourceFormulaIds=[50,55],originalExecuted=False,cExecuted=False)


def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    receipt=json.loads(RECEIPT.read_text(encoding='utf-8'))
    for row in receipt['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Source file '+row['path'])
    for row in receipt['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol'])
        require(all(row[k]==v for k,v in actual.items()),'Source function '+row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,
                sourceFiles=len(receipt['sourceFiles']),sourceFunctions=len(receipt['sourceFunctions']))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--reference',type=Path,required=True);parser.add_argument('--output',type=Path)
    args=parser.parse_args();result=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
