"""Ordinary action outcome/reward descriptions; never executes original/C/managed code."""
import argparse, hashlib, json, struct, subprocess
from pathlib import Path
from verify_sword_knockback_contract import require, fingerprint, function_fingerprint, REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/action-rewards-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/action-rewards-source-v1.json'

def marker(d):
    previous,primary,hit,changed=d
    return 2 if hit and changed else 1 if primary else previous

def experience(d):
    current,actor,target,ko,outcome,primary,bonus=d
    for value in [current,actor,target,ko]:integer(value,0,255)
    require(outcome in [0,1,2], 'Classified outcome')
    if not primary or outcome==0:return current
    base=10 if outcome==1 else 20//ko if ko else 20
    award=max(1,base+target-actor)*(2 if bonus else 1)
    return max(current,min(99,award))

def amounts(d):
    level,job,earned,monster,bonus=d
    integer(level,0,255);integer(earned,0,255)
    if monster:return [earned,0,0,0]
    integer(job,0,15)
    base=0 if not earned else 8+2*job+(level>>2)
    return [earned,base,base*3//2 if bonus else base,base//4]

def balance(d):
    stored,earned,level=d
    for value in d:integer(value,0,255)
    value=min(255,stored+earned)
    if value<100:return [value,level,False]
    if level>=99:return [99,level,False]
    return [0,level+1,True]

def job_points(d):
    current,total,award,thresholds=d
    integer(current,0,65535);integer(total,0,65535);integer(award,0,2147483647-65535)
    for value in thresholds:integer(value,0,65535)
    current=min(9999,current+award);total=min(9999,total+award)
    return [current,total,sum(total>=value for value in thresholds)]

def roster(d):
    def stop(disposition):return dict(disposition=disposition,amounts=None,balance=None,display=None,points=None)
    if d['hamedo']:return stop('Rejected')
    if not d['canEarn']:return stop('Skipped')
    units=d['units'];actor=next((u for u in units if u[0]=='actor'),None)
    if actor is None or set(actor[9])&{'inactive','crystal','dead','petrify','treasure'}:return stop('Rejected')
    flags=actor[9];is_monster='monster' in flags
    quoted=amounts([actor[4],actor[6],d['earned'],is_monster,'jpUp' in flags])
    exp=balance([actor[5],d['earned'],actor[4]])
    slot=d['job']-74 if 75<=d['job']<=93 else 0
    points=[];next_job=actor[6]
    if not is_monster:
        for u in sorted((u for u in units if u[0]!='actor'),key=lambda u:u[1])+[actor]:
            direct=u[0]=='actor';team=actor[2] if direct else actor[3];f=set(u[9])
            if f&{'inactive','crystal','dead','treasure','monster'} or u[2]!=team or slot==17 and 'female' in f or slot==18 and 'male' in f:continue
            award=quoted[2] if direct else quoted[3]
            jp=job_points([u[7],u[8],award,d['thresholds']]);points.append([u[0],award,*jp])
            if direct:next_job=jp[2]
    return dict(disposition='Projected',amounts=quoted,balance=exp,
        display=[exp[1] if exp[2] or d.get('priorLevel',False) else 0,next_job if next_job!=actor[6] else 0],points=points)

def validate(f):
    require(f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    counts={}
    for name,fn in [('markers',marker),('experience',experience),('amounts',amounts),('balances',balance),('jobPoints',job_points)]:
        for r in f[name]:require(fn(r['input'])==r['expected'],name+' '+r['id'])
        counts[name]=len(f[name])
    for r in f.get('rosters',[]):require(roster(r['input'])==r['expected'],'Roster '+r['id'])
    counts['rosters']=len(f.get('rosters',[]))
    return dict(descriptions=counts,originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in source['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in source['sourceFunctions']:
        actual=function_fingerprint(reference/r['path'],r['symbol'])
        require(all(r[k]==v for k,v in actual.items()),'Function '+r['symbol'])
    data=executable.read_bytes()
    require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS_942.21')
    require(list(struct.unpack_from('<9H',data,0x80066182-0x80010000+0x800))==[0,100,200,350,550,800,1150,1550,2100],'Prepared JP thresholds')
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,
                sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),preparedThresholdsVerified=8)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path)
    a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
