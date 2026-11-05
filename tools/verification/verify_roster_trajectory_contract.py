"""ZOD-299 flat direct trajectory descriptions and offline SDK table identity."""
import argparse,copy,hashlib,json,math,struct,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-trajectory-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-trajectory-source-v1.json'


def sqrt12(value):
    require(0<value<0x80000000,'Positive signed SDK input')
    leading=(32-value.bit_length())&~1
    normalized=value<<(leading-24) if leading>=24 else value>>(24-leading)
    require(64<=normalized<=255,'Normalized table index')
    factor=math.isqrt(normalized<<18);exponent=(19-leading)>>1
    return factor<<exponent if exponent>=0 else factor>>-exponent


def describe(data):
    d=copy.deepcopy(data);units=sorted(d['units'],key=lambda u:u['order'])
    require(2<=len(units)<=16 and len({u['id'] for u in units})==len(units) and len({u['order'] for u in units})==len(units),'Distinct compact roster')
    for u in units:require(0<=u['height']<=255 and len(u['tile'])==2 and 0<=u['tile'][0]<8 and 0<=u['tile'][1]<4,'Explicit flat sprite metadata')
    actor=next(u for u in units if u['id']==d['actor']);target=next(u for u in units if u['id']==d['target'])
    require(actor!=target,'Distinct endpoints');origin=d['origin'];destination=target['tile']
    require(0<=origin[0]<8 and 0<=origin[1]<4 and 1<=sum(abs(a-b) for a,b in zip(origin,destination))<=4,'Bounded origin/range')
    start=[origin[0]*28+14,-120-actor['height']*2//3,origin[1]*28+14]
    end=[destination[0]*28+14,-120-(target['height']*2//3 or 12),destination[1]*28+14]
    delta=[b-a for a,b in zip(start,end)];distance=sqrt12(sum(x*x for x in delta)*4096);samples=distance//8192
    for i in range(1,samples+1):
        fraction=(i*8192*256)//(distance//16);point=[a+((fraction*b)>>12) for a,b in zip(start,delta)]
        blocker=None
        if -120<=point[1]<0:blocked=True
        else:
            blocked=False
            for u in units:
                if u in (actor,target):continue
                if -120-u['height']<point[1]<-120 and u['height']!=0 and abs(point[0]-(u['tile'][0]*28+14))<8 and abs(point[2]-(u['tile'][1]*28+14))<8:
                    blocker=u['id'];blocked=True;break
        if blocked:return dict(clear=False,blocker=blocker,sample=i,samples=samples,distance=distance,point=point)
    return dict(clear=True,blocker=None,sample=None,samples=samples,distance=distance,point=None)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-299' and fixture['referenceRevision']==REVISION and fixture['productionImplemented'] is False,'Source receipt')
    ids=[]
    for row in fixture['vectors']:
        require(describe(row['input'])==row['expected'],'Trajectory '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterTrajectoryDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference,prepared=None):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==r['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(reference,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    result=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']),preparedTableVerified=False)
    if prepared:
        raw=prepared.read_bytes();table=r['sdkTable'];require(len(raw)==356352 and hashlib.sha256(raw).hexdigest()==table['executableSha256'],'Prepared executable')
        offset=table['address']-table['fileMappingBase'];span=raw[offset:offset+384]
        require(hashlib.sha256(span).hexdigest()==table['spanSha256'],'Table span')
        require(list(struct.unpack('<192h',span))==[math.isqrt(n<<18) for n in range(64,256)],'Every table factor identity')
        result.update(preparedTableVerified=True,tableFactorsVerified=192)
    return result


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path);p.add_argument('--output',type=Path)
    args=p.parse_args();r=audit(args.reference,args.prepared_executable)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
