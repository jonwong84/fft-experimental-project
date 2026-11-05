"""ZOD-306 independent live roster Stone interception/formula/settlement descriptions."""
import argparse,copy,json,subprocess
from pathlib import Path
from verify_roster_trajectory_contract import describe as trajectory
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-live-stone-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-live-stone-source-v1.json'


def describe(data):
    d=copy.deepcopy(data);units=d['units'];actor=next(u for u in units if u['id']==d['actor']);intended=next(u for u in units if u['id']==d['target'])
    require(all(1<=u['pa']<=99 and 10<=u['brave']<=100 and 1<=u['hp']<=65535 and 0<=u['evade']<=100 and 0<=u['ko']<=255 for u in units),'Bound live facts')
    path=trajectory(dict(d,origin=actor['tile']),include_target=True,max_units=21)
    require(path['clear'] or path['blocker'] is not None,'Flat occupied target resolution')
    target=next(u for u in units if u['id']==(intended['id'] if path['clear'] else path['blocker']))
    word=d['word'];count=d['count']
    def draw():
        nonlocal word,count
        require(count<0xffffffffffffffff,'Draw budget');word=(word*1103515245+12345)&0xffffffff;count+=1;return(word>>16)&32767
    dx=target['tile'][0]-actor['tile'][0];dy=target['tile'][1]-actor['tile'][1]
    h=dx>0 and target['face']=='West' or dx<0 and target['face']=='East';v=dy>0 and target['face']=='South' or dy<0 and target['face']=='North'
    front=h if abs(dx)>abs(dy) else v if abs(dy)>abs(dx) else h or v
    hit=True;damage=0;attempt=False
    for evade in (0,0,0,target['evade'] if front else 0):
        if draw()*100//32768<evade:hit=False;break
    if hit:
        damage=(draw()*2//32768+1)*actor['pa'];a=draw();b=draw();attempt=b*target['brave']//32768<a*actor['brave']//32768
    hp=max(0,target['hp']-damage);destination=target['tile'][:]
    if attempt and hp:
        proposed=[destination[0]+(1 if dx>0 else -1),destination[1]] if abs(dx)>abs(dy) else [destination[0],destination[1]+(1 if dy>0 else -1)]
        if 0<=proposed[0]<8 and 0<=proposed[1]<4 and not any(u['tile']==proposed for u in units):destination=proposed
    status='Dead' if hp==0 else 'Critical' if hp<=10 else 'None'
    return dict(target=target['id'],intercepted=target['id']!=intended['id'],hit=hit,damage=damage,attempted=attempt,hp=hp,status=status,
        ko=(target['ko']+1)%256 if hp==0 else target['ko'],destination=destination,word=word,count=count,
        path=path,pa=actor['pa'],peersChanged=False)


def validate(f):
    require(f['ticket']=='ZOD-306' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source receipt')
    ids=[]
    for row in f['vectors']:require(describe(row['input'])==row['expected'],'Live Stone '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterLiveStoneDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(ref):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'));require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==REVISION,'Pinned source')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=ref,text=True).strip(),'Clean source')
    for f in r['sourceFiles']:require(fingerprint(ref,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(ref/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
