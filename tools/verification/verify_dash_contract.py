"""ZOD-317 independent Dash formula/result descriptions and prepared-record audit."""
import argparse,hashlib,json
from pathlib import Path
from verify_roster_authority_contract import advance,audit
from verify_sword_knockback_contract import require,REVISION

ROOT=Path(__file__).resolve().parents[2];FIXTURE=ROOT/'docs/fixtures/behavior/dash-contract-v1.json'

def damage(pa,raw):
    require(type(pa) is int and 1<=pa<=99 and type(raw) is int and 0<=raw<=32767,'Dash formula domain')
    return pa*(1+raw*4//32768)

def actual(d):
    word=d['word'];count=d['count'];rolls=[]
    for _ in range(3):word=advance(word,1);count+=1;rolls.append((word>>16)&32767)
    value=damage(d['pa'],rolls[0]);hp=max(0,d['hp']-value)
    attempted=rolls[2]*d['targetBrave']//32768<rolls[1]*d['actorBrave']//32768
    dx=d['target'][0]-d['actor'][0];dy=d['target'][1]-d['actor'][1]
    step=[d['target'][0]+(1 if dx>0 else -1),d['target'][1]] if abs(dx)>abs(dy) else [d['target'][0],d['target'][1]+(1 if dy>0 else -1)]
    moved=attempted and hp>0 and 0<=step[0]<d['width'] and 0<=step[1]<d['depth'] and step not in d['occupied']
    return dict(damage=value,hp=hp,attempted=attempted,destination=step if moved else d['target'],ko=(d['ko']+1)%256 if hp==0 else d['ko'],word=word,count=count)

def validate(f):
    require(f['ticket']=='ZOD-317' and f['referenceRevision']==REVISION and not f['productionImplemented'],'Dash source receipt')
    for row in f['formula']:require(damage(**row['input'])==row['expected'],'Dash damage '+row['id'])
    for row in f['actual']:require(actual(row['input'])==row['expected'],'Dash actual '+row['id'])
    return dict(formulaDescriptions=len(f['formula']),actualDescriptions=len(f['actual']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def prepared(path):
    b=path.read_bytes();require(len(b)==356352 and hashlib.sha256(b).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS_942.21')
    require(b[0x4f888:0x4f890]==bytes.fromhex('50005a4182020181'),'Dash primary record')
    require(b[0x50bfa:0x50c08]==bytes.fromhex('0100010911000000370004000000'),'Dash secondary record')
    require(b[0x55311+4]==147 and not (b[0x55311] & 0x40),'Squire Dash slot1 low byte/high bit')
    return dict(abilityId=147,range=1,verticalTolerance=1,formula=55,y=4,evadeable=False,directTrajectory=False,aiPhysical=True,aiDirectTrajectory=False,aiExhaustive=False,preparedVerified=True)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args()
    r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),source=audit(a.reference),prepared=prepared(a.prepared_executable))
    if a.output:
        with a.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
