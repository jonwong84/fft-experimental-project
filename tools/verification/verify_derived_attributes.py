"""Mode1 and equipment numeric descriptions; no original/C/managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/derived-attributes-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/derived-attributes-source-v1.json'

def maxima(d):
    require(len(d['raw'])==len(d['multipliers'])==5 and len(d['base'])==3,'Explicit numeric inputs')
    for value in d['raw']:integer(value,0,0xffffff)
    for value in d['multipliers']+d['base']:integer(value,0,255)
    integer(d['hp'],0,65535);integer(d['mp'],0,65535)
    divisor,cap=(10,65535) if d['egg'] else (100,999)
    return [min(cap,max(1,((value*mult)//divisor)>>14)) for value,mult in zip(d['raw'][:2],d['multipliers'][:2])]

def mode1(d):
    hp,mp=maxima(d)
    return dict(hp=min(d['hp'],hp),maxHp=hp,mp=min(d['mp'],mp),maxMp=mp,base=d['base'])

def equipment(d):
    hp,mp=maxima(d);move=d['move'];jump=d['jump'];bonus=[0,0,0]
    integer(move,0,255);integer(jump,0,255)
    if not d['monster']:
        require(d['items'] is not None,'Explicit item contributions')
        for present,pa,ma,speed,movement,leap,armor,hpb,mpb in d['items']:
            if not present:continue
            for value in [pa,ma,speed,movement,leap,hpb,mpb]:integer(value,0,255)
            bonus=[min(255,a+b) for a,b in zip(bonus,[pa,ma,speed])]
            move=min(253,move+movement);jump=min(7,jump+leap)
            if armor:hp=min(999,hp+hpb);mp=min(999,mp+mpb)
    return dict(hp=min(d['hp'],hp),maxHp=hp,mp=min(d['mp'],mp),maxMp=mp,base=d['base'],
                bonuses=bonus,actual=[min(cap,a+b) for cap,a,b in zip([99,99,50],d['base'],bonus)],move=move,jump=jump)

def validate(f):
    require(f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for name,fn in [('mode1',mode1),('equipment',equipment)]:
        for r in f[name]:require(fn(r['input'])==r['expected'],name+' '+r['id'])
    return dict(mode1Descriptions=len(f['mode1']),equipmentDescriptions=len(f['equipment']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        actual=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in actual.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
