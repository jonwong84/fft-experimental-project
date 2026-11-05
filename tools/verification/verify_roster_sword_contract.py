"""ZOD-294 isolated roster sword descriptions; no original, C or managed execution."""
import argparse
import copy
import itertools
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-sword-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-sword-source-v1.json'


def describe(data, *, stone=False):
    d=copy.deepcopy(data);units=sorted(d['units'],key=lambda u:u['order'])
    require(1<=len(units)<=16 and len({u['id'] for u in units})==len(units) and len({u['order'] for u in units})==len(units),'Compact distinct roster')
    require(len({tuple(u['tile']) for u in units})==len(units),'Distinct positions')
    for u in units:
        require(0<=u['ct']<=254 and 1<=u['speed']<=255 and 1<=u['hp']<=u['maxHp']<=65535 and 0<=u['pa']<=255,'Ordinary numeric facts')
        require(0<=u['team']<=3 and u['face'] in ('East','North','West','South') and 0<=u['evade']<=100,'Team/facing/evade')
        require(len(u['tile'])==2 and 0<=u['tile'][0]<8 and 0<=u['tile'][1]<4,'Selected flat map')
    actor=next(u for u in units if u['id']==d['actor']);target=next(u for u in units if u['id']==d['target'])
    require(actor!=target and actor['team']!=target['team'] and actor['ct']<100,'Selected ordinary enemy')
    require(d['order'] in ('ActThenMove','MoveThenAct') and type(d['moved']) is bool and 0<=d['move']<=255,'Choices')
    destination=tuple(d['destination']);start=tuple(actor['tile'])
    blocked={tuple(u['tile']) for u in units if u['team']!=actor['team']};occupied={tuple(u['tile']) for u in units if u!=actor}
    distances={start:0};queue=[start]
    for tile in queue:
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nxt=(tile[0]+dx,tile[1]+dy)
            if 0<=nxt[0]<8 and 0<=nxt[1]<4 and nxt not in blocked and nxt not in distances:
                distances[nxt]=distances[tile]+1;queue.append(nxt)
    require(destination in distances and distances[destination]<=min(124,d['move']) and destination not in occupied and
        (not d['moved'] or destination==start),'Initial legal destination')
    origin=destination if d['order']=='MoveThenAct' else start;dx=target['tile'][0]-origin[0];dy=target['tile'][1]-origin[1]
    require(1<=abs(dx)+abs(dy)<=4 if stone else abs(dx)+abs(dy)==1,'Formula range')
    xa=max(1,actor['pa'])
    if stone:require(all(1<=u['pa']<=99 for u in units) and d['clearTrajectory'] is True,'Bound certified clear Stone')
    else:require(0<=d['power']<=255 and (2*xa-1)*d['power']<=32767,'Bound sword envelope')
    horizontal=(dx>0 and target['face']=='West') or (dx<0 and target['face']=='East')
    vertical=(dy>0 and target['face']=='South') or (dy<0 and target['face']=='North')
    front=horizontal if abs(dx)>abs(dy) else vertical if abs(dy)>abs(dx) else horizontal or vertical
    hit=100-(target['evade'] if front else 0);word=d['word'];count=d['count'];ticks=d['ticks']
    def draw():
        nonlocal word,count
        require(count<0xffffffffffffffff,'Draw budget');word=(word*1103515245+12345)&0xffffffff;count+=1
    for _ in range(4):draw()
    if hit==0:return dict(disposition='NotValuable',word=word,count=count)
    damage=2*xa if stone else min(999,xa*d['power']);target['hp']=max(0,target['hp']-damage)
    end=min(60,actor['ct']+(0 if d['moved'] or destination!=start else 20));actor['ct']=end
    elapsed=0;picks=[]
    while True:
        ready=[u for u in units if u['ct']>=100]
        if ready:
            chosen=min(ready,key=lambda u:(-u['ct'],u['order']));chosen['ct']%=100;draw();dead=chosen['hp']==0;picks.append((chosen['id'],dead))
            if not dead:break
        else:
            require(ticks<0xffffffffffffffff,'Tick budget');elapsed+=1;ticks+=1
            for u in units:u['ct']=min(254,u['ct']+u['speed'])
    status=lambda u:'Dead' if u['hp']==0 else 'Critical' if u['hp']<=u['maxHp']//5 else 'None'
    total=0;running=[]
    for u in units:
        term=u['hp']*128//u['maxHp']+(-192 if status(u)=='Dead' else -32 if status(u)=='Critical' else 0)+357
        total=((total+(term if u['team']==actor['team'] else -term)+32768)%65536)-32768;running.append(total)
    groups=[dict(id=k[0],dead=k[1],count=len(list(g))) for k,g in itertools.groupby(picks)]
    return dict(disposition='Scored',hit=hit,damage=damage,endCt=end,next=chosen['id'],elapsed=elapsed,ticks=ticks,
        units=[dict(id=u['id'],hp=u['hp'],status=status(u),counter=3 if u['hp']==0 else 255,ct=u['ct']) for u in units],
        groups=groups,priority=total,running=running,word=word,count=count)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-294' and fixture['referenceRevision']==REVISION and
        fixture['kind']=='isolated-roster-sword-descriptions' and fixture['productionImplemented'] is False,'Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['input'])==row['expected'],'Roster sword description '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Unique description IDs')
    return dict(rosterSwordDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==r['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(reference,f['path'])==f,'Source file '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Source function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
