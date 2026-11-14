"""ZOD-287 independent isolated admission/scenario descriptions; no C, original or managed execution."""
import argparse
import copy
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require
from verify_roster_preparation_contract import describe as prepare_description,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-general-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-general-source-v1.json'


def describe(data):
    d=copy.deepcopy(data); units=d['units']; actor=next(u for u in units if u['id']==d['actor'])
    require(all(u['tile'][0]<8 and u['tile'][1]<4 for u in units),'Selected 8x4 map')
    p=prepare_description('preparation',d)
    if actor['team']==0 and actor['hp']*128//actor['maxHp']<45: return dict(rejection='BlueHealerFocus')
    if actor['hp']<=actor['maxHp']//5 and p['support']: return dict(rejection='ConditionalRetreat')
    if actor['team']!=0 and p['mostly'][actor['id']] and not p['policies'][actor['id']]: return dict(rejection='FadingLife')
    units.sort(key=lambda u:u['order']); foes=[u for u in units if u['team']!=actor['team']]
    xy=tuple(actor['tile']); dist=lambda tile,other:abs(tile[0]-other[0])+abs(tile[1]-other[1])
    focus=min(foes,key=lambda u:dist(xy,u['tile'])) if foes else actor
    target_distance=lambda tile:0 if focus==actor else dist(tile,focus['tile'])
    reach=actor['move']+(focus['team']!=actor['team'])
    # Forward propagation has enemy blockers but may transit allies. Endpoints must be empty.
    blocked={tuple(u['tile']) for u in foes}; occupied={tuple(u['tile']) for u in units if u!=actor}
    frontier=[xy]; distances={xy:0}
    for tile in frontier:
        for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
            nxt=(tile[0]+dx,tile[1]+dy)
            if 0<=nxt[0]<8 and 0<=nxt[1]<4 and nxt not in blocked and nxt not in distances:
                distances[nxt]=distances[tile]+1;frontier.append(nxt)
    m0=sorted((t for t,v in distances.items() if v<=actor['move'] and t not in occupied and (not actor['moved'] or t==xy)),key=lambda t:(t[1],t[0]))
    actor_delay=p['cachedDelay']; imminent=[u for u in foes if (max(0,100-u['ct'])+u['speed']-1)//u['speed']<=actor_delay]
    proximity={t:sum(max(0,10-dist(t,u['tile'])) for u in imminent)%256 for t in m0}
    result=dict(rejection='None',focus=focus['id'],fallback=not foes,reach=reach,active=[u['id'] for u in units],
        imminent=[u['id'] for u in imminent],m0=[list(t) for t in m0])
    if d['kind']=='preparation':return result
    require(d['kind']=='scenarios','Description kind')
    distance=target_distance(xy); outside=distance>reach;limit=distance-1 if outside else actor['move']+distance
    mask=[t for t in m0 if target_distance(t)<=limit];filter_fallback=not mask;mask=mask or m0
    word=p['word'];count=p['count'];priority=sum((1 if u['team']==actor['team'] else -1)*
        (u['hp']*128//u['maxHp']+(-32 if u['hp']<=u['maxHp']//5 else 0)+357) for u in units)
    priority=(priority+32768)%65536-32768
    def draw():
        nonlocal word,count
        require(count<0xffffffffffffffff,'Draw budget');word=(word*1103515245+12345)&0xffffffff;count+=1
        return (word>>16)&0x7fff
    for tile in mask:draw() # Living effect-free whole-roster clock ends at first ready unit.
    effective=reach;modulo=None
    if outside and actor['team']!=0:
        modulo=draw()%7
        if modulo==0:effective//=2
    def select(bound=None):
        candidates=[t for t in mask if bound is None or target_distance(t)<=bound]
        fallback=not candidates;candidates=candidates or mask
        key=lambda t: (target_distance(t),proximity[t],-t[1],-t[0]) if fallback else (proximity[t],target_distance(t),-t[1],-t[0])
        return min(candidates,key=key),fallback
    d1,fall=select(effective if outside else None);d0=d1;bound=None
    if not outside and not actor['acted']:
        bound=max(3,distance-1) if 'Stone' in actor['actions'] and focus in foes else distance-1
        d0,fall=select(bound)
    result.update(distance=distance,outside=outside,mask=[list(t) for t in mask],
        scenario1=[list(t) for t in m0 if t in mask or t==xy],scenario2=[list(t) for t in m0 if t not in mask],
        d0=list(d0),d1=list(d1),baseline=mask.index(d0),effective=effective,modulo=modulo,bound=bound,
        filterFallback=filter_fallback,destinationFallback=fall,priority=priority,
        proximity=[proximity[t] for t in m0],word=word,count=count)
    return result


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-287' and fixture['referenceRevision']==REVISION and
        fixture['kind']=='isolated-roster-general-descriptions' and fixture['productionImplemented'] is False,'Source-only receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['input'])==row['expected'],'Roster general description '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Unique description IDs')
    return dict(rosterGeneralDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


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
