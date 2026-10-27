"""ZOD-120 offline closed flat movement/path descriptions; never executes C or managed movement."""
import argparse
from collections import deque
import hashlib
import json
from pathlib import Path
import struct
import subprocess

from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require
from verify_ai_decision_contract import calculate as ai_calculate, verify_prepared_record

FIXTURE = 'docs/fixtures/behavior/flat-ai-movement-contract-v1.json'
EVIDENCE = 'docs/evidence/ai/flat-movement-contract-v1.json'
# Spread presets, original packed move-step ordinals and managed facing ordinals are different domains.
STEPS = (('East',1,0,0), ('North',0,1,192), ('West',-1,0,64), ('South',0,-1,128))
OPPOSITE = {'East':'West','West':'East','North':'South','South':'North'}


def point_order(points): return sorted(points, key=lambda p:(p[1],p[0]))


def distances(width, depth, origin, enemy):
    """Closed-domain reduction: unit-cost neighbors, excluding the one living enemy's tile."""
    best={tuple(origin):0}; pending=deque([tuple(origin)])
    while pending:
        x,y=pending.popleft()
        for _,dx,dy,_ in STEPS:
            to=(x+dx,y+dy)
            if 0<=to[0]<width and 0<=to[1]<depth and to!=tuple(enemy) and to not in best:
                best[to]=best[(x,y)]+1; pending.append(to)
    return best


def preferred_predecessor(preferred, candidates):
    require(preferred in OPPOSITE and len(set(candidates))==len(candidates) and all(c in OPPOSITE for c in candidates), 'Directions')
    eligible=[name for name,_,_,_ in STEPS if name in candidates]
    return preferred if preferred in eligible else eligible[0] if eligible else None


def flat_route(data):
    width,depth=data['width'],data['depth'];origin=data['origin'];enemy=data['enemy'];dest=data['destination']
    require(type(width) is int and type(depth) is int and 1<=width<=16 and 1<=depth<=16,'Closed map domain')
    require(all(len(p)==2 and all(type(v) is int for v in p) and 0<=p[0]<width and 0<=p[1]<depth
                for p in (origin,enemy,dest)) and origin!=enemy,'Distinct ordinary units and bounded coordinates')
    require(type(data['move']) is int and 0<=data['move']<=255 and type(data['jump']) is int and 0<=data['jump']<=255,'Movement byte domain')
    require(type(data['moved']) is bool,'Movement choice flag')
    move=min(124,data['move']);best=distances(width,depth,origin,enemy)
    physical=point_order([p for p,d in best.items() if p!=tuple(origin) and d<=move])
    ai_origins=point_order([tuple(origin)] if data['moved'] else [tuple(origin)]+physical)
    rows=sorted({p[1] for p in ai_origins})
    # Source writes every x in each nonempty reachable row, not only the mask's set bits.
    target_rows=[dict(y=y,values=[abs(enemy[0]-x)+abs(enemy[1]-y) for x in range(width)]) for y in rows]
    path=None;heading=None;encoded=None
    if tuple(dest) in physical:
        reverse_path=[tuple(dest)]; current=tuple(dest); preferred='East'
        while current!=tuple(origin):
            eligible=[]
            for name,dx,dy,_ in STEPS:
                neighbor=(current[0]+dx,current[1]+dy)
                if best.get(neighbor)==best[current]-1:eligible.append(name)
            selected=preferred_predecessor(preferred,eligible)
            require(selected is not None,'Flat predecessor must exist')
            _,dx,dy,_=next(s for s in STEPS if s[0]==selected)
            current=(current[0]+dx,current[1]+dy);reverse_path.append(current);preferred=selected
        path=list(reversed(reverse_path));encoded=[]
        for first,second in zip(path,path[1:]):
            name,_,_,value=next(s for s in STEPS if (s[1],s[2])==(second[0]-first[0],second[1]-first[1]))
            encoded.append(value);heading=name
    manhattan=point_order([(x,y) for y in range(depth) for x in range(width)
        if (x,y)!=tuple(origin) and (x,y)!=tuple(enemy) and abs(x-origin[0])+abs(y-origin[1])<=data['move']])
    return dict(effectiveMove=move,originRemainingRange=move+1,jumpHalf=min(7,data['jump'])//2,
        remainingRanges=[dict(tile=list(p),remaining=move+1-best[p])
                         for p in point_order([p for p,d in best.items() if d<=move])],
        physicalDestinations=[list(p) for p in physical],aiOrigins=[list(p) for p in ai_origins],
        targetDistanceRows=target_rows,path=[list(p) for p in path] if path else None,
        packedSteps=encoded,executionFinalHeading=heading,
        defaultPrototypeExtraDestinations=[list(p) for p in point_order(set(manhattan)-set(physical))],
        rngDraws=0)


def placement(data):
    require(data['facing'] in OPPOSITE,'Facing')
    return dict(position=data['destination'],facing=data['facing'],
        movementTaken=data['movementTaken'] or data['origin']!=data['destination'],rngDraws=0)


def calculate(family,data):
    if family=='flat-route':return flat_route(data)
    if family=='path-tie':return dict(selected=preferred_predecessor(data['preferred'],data['eligible']))
    if family=='simulation-placement':return placement(data)
    if family=='dispatch':return ai_calculate('command-dispatch',data)
    if family=='destination':
        # The source visits layer/y/x, not arbitrary input list order.
        value=dict(data,tiles=sorted(data['tiles'],key=lambda t:(t['layer'],t['y'],t['x'])))
        return ai_calculate('destination',value)
    raise ValueError('Unsupported helper family: '+family)


def verify_retail(overlay,executable,records):
    record=records['spreadPresets'];data=overlay.read_bytes()
    require(len(data)==record['byteLength'] and hashlib.sha256(data).hexdigest()==record['sha256'],'Overlay identity')
    offset=record['address']-record['loadAddress'];count=record['recordByteLength']
    require(0<=offset and offset+count<=len(data),'Overlay slice bounds')
    require(hashlib.sha256(data[offset:offset+count]).hexdigest()==record['recordSha256'],'Preset fingerprint')
    require(list(struct.unpack('<4I',data[offset:offset+count]))==[p['address'] for p in record['decodedOrder']],'Preset order')
    record=records['naturalCost'];verify_prepared_record(executable,record)
    data=executable.read_bytes();offset=record['address']-record['loadAddress']+0x800
    require(data[offset]==record['decodedDefaultCost']==1,'Default Natural cost')


def validate(contract):
    require(contract['format']=='fft.flat-ai-movement-source-description' and contract['schemaVersion']==1 and
        contract['ticket']=='ZOD-120' and contract['referenceRevision']==REVISION,'Contract identity')
    require(contract['execution']=='offline-closed-domain-description' and contract['productionImplemented'] is False,'Execution boundary')
    seen=set();families=set()
    for row in contract['vectors']:
        require(row['id'] not in seen,'Duplicate vector');seen.add(row['id']);families.add(row['family'])
        require(row['expectationKind']=='source-derived-closed-domain','Expectation classification')
        require(calculate(row['family'],row['inputs'])==row['expected'],'Vector mismatch: '+row['id'])
    require(len(seen)==contract['vectorCount']==48,'Vector inventory')
    require(families=={'flat-route','path-tie','simulation-placement','dispatch','destination'},'Family inventory')
    require([r['id'] for r in contract['implementationCases']]==[f'FMP-{n:03}' for n in range(1,19)] and
        all(r['status']=='unexecuted' for r in contract['implementationCases']),'Unexecuted production inventory')
    return dict(vectors=len(seen),families=len(families),implementationCasesUnexecuted=18)


def audit(root,reference=None,check_managed_baseline=False,overlay=None,executable=None):
    evidence=json.loads((root/EVIDENCE).read_text(encoding='utf-8'))
    require(evidence['ticket']=='ZOD-120' and evidence['reference']['revision']==REVISION,'Evidence identity')
    require(evidence['fixture']==fingerprint(root,FIXTURE),'Fixture identity')
    result=validate(json.loads((root/FIXTURE).read_text(encoding='utf-8')))
    for row in evidence['preservedFiles']:require(fingerprint(root,row['path'])==row,'History changed: '+row['path'])
    result.update(preservedFiles=len(evidence['preservedFiles']),managedBaselineFilesChecked=0,
        referenceFilesChecked=0,referenceFunctionsChecked=0,retailRecordsChecked=0)
    if check_managed_baseline:
        for row in evidence['managedBaseline']:require(fingerprint(root,row['path'])==row,'Managed baseline changed: '+row['path'])
        result['managedBaselineFilesChecked']=len(evidence['managedBaseline'])
    if reference is not None:
        revision=subprocess.check_output(['git','-C',str(reference),'rev-parse','HEAD'],text=True).strip()
        status=subprocess.check_output(['git','-C',str(reference),'status','--porcelain'],text=True).strip()
        require(revision==REVISION and not status,'Reference revision/clean boundary')
        for row in evidence['reference']['files']:
            require(fingerprint(reference,row['path'])=={k:v for k,v in row.items() if k!='functions'},'Reference file changed: '+row['path'])
            for function in row['functions']:
                require(function_fingerprint(reference/row['path'],function['symbol'])==function,'Reference function changed: '+function['symbol'])
                result['referenceFunctionsChecked']+=1
            result['referenceFilesChecked']+=1
    require((overlay is None)==(executable is None),'Supply both prepared inputs or neither')
    if overlay is not None:
        verify_retail(overlay,executable,evidence['retailMetadata']);result['retailRecordsChecked']=2
    result.update(format='fft.flat-ai-movement-source-audit',schemaVersion=1,passed=True,
        productionImplemented=False,fullAiExecuted=False,originalExecuted=False)
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference',type=Path);parser.add_argument('--check-managed-baseline',action='store_true')
    parser.add_argument('--overlay',type=Path);parser.add_argument('--executable',type=Path)
    parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
    result=audit(Path(__file__).resolve().parents[2],args.reference,args.check_managed_baseline,args.overlay,args.executable)
    with args.output.open('x',encoding='utf-8') as output:output.write(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result))


if __name__=='__main__':main()
