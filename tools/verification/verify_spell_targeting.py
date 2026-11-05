"""ZOD-358 normalized ordinary targeting descriptions; no original/C/managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/spell-targeting-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/spell-targeting-source-v1.json'

def height(t):
    return integer(t['h'],0,255)*2+integer(t['half'],0,31)+integer(t['water'],0,7)*2
def reference(t):return height(t)&255
def column(d):
    ref=integer(d['reference'],0,255);tol=integer(d['tolerance'],0,255)*2
    a=d['lower'];b=d['upper'];height(a)
    if b is not None:height(b)
    valid=[not a['blocked'],b is not None and not b['blocked']]
    hs=[height(a) if valid[0] else -255,height(b) if valid[1] else -255]
    if d['topDown']:
        removed=1 if hs[0]>=hs[1] else 0;hs[removed]=-255;valid[removed]=False
    ds=[abs(h-ref) for h in hs];choice=1 if ds[1]<ds[0] else 0
    return choice if valid[choice] and ds[choice]<=tol else None

def tiles(m):
    result={}
    for l in range(m['layers']):
        for y in range(m['depth']):
            for x in range(m['width']):result[(x,y,l)]=dict(h=10,half=0,water=0,blocked=l!=0,untargetable=False)
    for r in m['overrides']:result[tuple(r['position'])]=r['tile']
    return result
def sort_panels(ps):return [list(p) for p in sorted(ps,key=lambda p:(p[2],p[1],p[0]))]

def panels(d):
    m=d['map'];g=d['geometry'];ts=tiles(m);p=d['profile'];caster=tuple(d['caster']);aim=caster if d['kind']=='Range' else tuple(d['aim'])
    def reject():return dict(accepted=False,panels=None)
    if not all(p[k] for k in ['ordinary','noSpecial','noCross','padding']) or not (1<=m['width']<=255 and 1<=m['depth']<=255 and m['width']*m['depth']<=256 and m['layers'] in [1,2]):return reject()
    for t in ts.values():height(t)
    if caster not in ts or ts[caster]['blocked'] or aim not in ts or ts[aim]['blocked']:return reject()
    radius=integer(g['range'] if d['kind']=='Range' else g['area'],0,255)
    # Model the source's in-place frontier/budget passes, independently of managed distance enumeration.
    grid={(x,y):0 for y in range(m['depth']) for x in range(m['width'])};marks=set()
    if radius==255:
        for pos in grid:grid[pos]=1
    else:
        grid[aim[:2]]=radius+1;marks.add(aim[:2])
        if radius==0:marks.clear()
        for _ in range(radius):
            changed=False
            for y in range(m['depth']):
                for x in range(m['width']):
                    if (x,y) not in marks:continue
                    marks.remove((x,y));budget=grid[x,y]
                    for nx,ny in [(x+1,y),(x,y+1),(x-1,y),(x,y-1)]:
                        if (nx,ny) in grid and grid[nx,ny]<budget:
                            grid[nx,ny]=budget-1
                            if budget>=3:marks.add((nx,ny));changed=True
            if not changed:break
    chosen={pos for pos in ts if grid[pos[:2]]!=0}
    if d['kind']=='Range' and g['cannotTargetSelf']:chosen.discard(caster)
    if (d['kind']=='Range' and g['rangeVertical']) or (d['kind']=='Area' and radius!=255):
        ref=reference(ts[aim]);filtered=set()
        for y in range(m['depth']):
            for x in range(m['width']):
                layer=column(dict(lower=ts[x,y,0],upper=ts.get((x,y,1)),reference=ref,tolerance=g['vertical'],topDown=d['kind']=='Area' and g['topDown']))
                if layer is not None and (x,y,layer) in chosen:filtered.add((x,y,layer))
        chosen=filtered
    if d['kind']=='Area' and g['cannotHitCaster']:chosen.discard(caster)
    chosen={pos for pos in chosen if not ts[pos]['blocked'] and not (ts[pos]['untargetable'] and (d['kind']=='Area' or g['area']==0))}
    return dict(accepted=True,panels=sort_panels(chosen))

def eligible(u):return u['active'] and not any(u[k] for k in ['crystal','treasure','jump','mount'])
def selection(d):
    area=panels(dict(d,kind='Area'));require(area['accepted'] and d['complete'],'Known area/roster')
    units=sorted(d['units'],key=lambda u:u['order']);orders=[u['order'] for u in units];require(len(orders)==len(set(orders)) and all(v>=0 for v in orders),'Source order')
    active=[u for u in units if u['active']];require(len({u['id'] for u in active})==len(active),'Distinct active IDs')
    actor=next(u for u in active if u['id']==d['casterId']);require(actor['position']==d['caster'],'Caster position')
    remaining={tuple(p) for p in area['panels']};marked=set();g=d['geometry']
    if g['allies'] or g['enemies']:
        for u in units:
            if not eligible(u):continue
            pos=tuple(u['position'])
            if pos not in remaining:continue
            allied=u['id']==actor['id'] or actor['current']==u['initial']
            if g['allies'] if allied else g['enemies']:marked.add(pos)
            else:remaining.remove(pos)
        if g['area']!=255:marked |= remaining
    else:marked=remaining
    selected=[u for u in units if eligible(u) and tuple(u['position']) in marked]
    if d['nearest']:selected.sort(key=lambda u:abs(u['position'][0]-d['caster'][0])+abs(u['position'][1]-d['caster'][1]))
    return dict(ids=[u['id'] for u in selected],panels=sort_panels(marked))

def validate(f):
    require(f['ticket']=='ZOD-358' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['references']:require(reference(r['tile'])==r['expected'],'Reference height')
    for r in f['columns']:require(column(r)==r['expected'],'Column '+r['id'])
    for r in f['panels']:require(panels(r)==r['expected'],'Panels '+r['id'])
    for r in f['rosters']:require(selection(r)==r['expected'],'Roster '+r['id'])
    return dict(referenceDescriptions=len(f['references']),columnDescriptions=len(f['columns']),panelDescriptions=len(f['panels']),rosterDescriptions=len(f['rosters']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(ref):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=ref,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=ref,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(ref,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        a=function_fingerprint(ref/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
