"""ZOD-352 MP and charge descriptions, no original/C/managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer,signed16

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-resource-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-resource-source-v1.json'

def mp(d):
    a=d['actor'];s=d['ability'];value=integer(a['mp'],0,65535);cost=integer(s['cost'],0,255);ability=integer(s['id'],-32768,32767)
    require(d['mode'] in ['Execute','Preview','AiSimulation'] and s['menu'] in ['Default','Monster','Charge','Other'],'Known mode/menu')
    require(a['restriction'] in ['None','CannotAct','DeepWater'],'Classified restriction')
    if not d['bookkeeping']:return dict(accepted=False,outcome=None)
    def outcome(block='None',recorded=0,remaining=value,subtracted=False):return dict(accepted=True,outcome=dict(block=block,cost=recorded,remaining=remaining,subtracted=subtracted))
    restriction='CannotAct' if a['dontAct'] else a['restriction']
    if restriction!='None':return outcome(restriction)
    if a['frog'] and (ability not in [0,29,367] or s['menu']=='Charge'):return outcome('Frog')
    if 0<=ability<368:
        if s['menu'] in ['Default','Monster'] and s['affected'] and a['silence']:return outcome('Silenced')
        if s['menu']=='Default' and not a['mime']:
            cost=cost//2 if a['half'] else cost
            if signed16(value)<cost:return outcome('NotEnoughMp',cost)
            subtract=d['mode']!='Preview'
            return outcome(recorded=cost,remaining=value-cost if subtract else value,subtracted=subtract)
    return outcome()

def setup(d):
    count=integer(d['raw'],0,255)&127
    if not d['persevere']:
        if d['short']:count=(count+1)//2
        if d['non']:count=0
    return count

def ai_delay(d):
    count=integer(d['raw'],0,255)
    if not d['persevere']:
        if d['monsterSkill']:count//=2
        elif d['defend']:count=0
    return count

def tick(d):
    original=integer(d['counter'],0,255);counter=original
    require(type(d['blocked']) is bool,'Classified turn block')
    if not d['blocked'] and counter not in [0,255]:counter-=1
    return dict(counter=counter,ready=not d['blocked'] and counter==0,changed=counter!=original)

def validate(f):
    require(f['ticket']=='ZOD-352' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['mp']:require(mp(r['input'])==r['expected'],'MP '+r['id'])
    for r in f['setup']:require(setup(r)==r['expected'],'Setup '+r['id'])
    for r in f['ai']:require(ai_delay(r)==r['expected'],'AI '+r['id'])
    for r in f['ticks']:require(tick(r)==r['expected'],'Tick '+str(r['counter']))
    return dict(mpDescriptions=len(f['mp']),setupDescriptions=len(f['setup']),aiDescriptions=len(f['ai']),tickDescriptions=len(f['ticks']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        a=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
