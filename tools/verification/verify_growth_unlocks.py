"""Battle raw growth and job-unlock descriptions; no original/C/managed execution."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/growth-unlocks-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/growth-unlocks-source-v1.json'

def growth(d):
    require(len(d['raw'])==len(d['growth'])==5,'Five ordered stat facts')
    for value in d['raw']:integer(value,0,0xffffff)
    for value in d['growth']:integer(value,0,255)
    integer(d['level'],0,255);integer(d['word'],0,0xffffffff);integer(d['count'],0,2**64-1)
    if d['count']>2**64-6:return dict(accepted=False,raw=None,word=d['word'],count=d['count'])
    word=d['word'];raw=[]
    for value,rate in zip(d['raw'],d['growth']):
        word=(word*1103515245+12345)&0xffffffff
        delta=value//(max(1,rate)+d['level'])
        raw.append(min(0xffffff,value-delta if d['down'] else value+delta))
    return dict(accepted=True,raw=raw,word=word,count=d['count']+5)

def unlocks(d,f):
    names=f['originalJobOrder'];levels={name:d.get('allLevels',0) for name in names};levels.update(d.get('levels',{}))
    for value in levels.values():integer(value,0,15)
    if d['female']:levels['bard']=15
    if d['male']:levels['dancer']=15
    if d['monster']:return []
    rows={r['job']:dict(r['requires']) for r in f['unlockDefinitions']};rows.update(d.get('syntheticRequirements',{}))
    result=['squire']
    for name in names[1:]:
        if all(levels[key]>=minimum for key,minimum in rows[name].items()):result.append(name)
    if d['male']:result=[name for name in result if name!='dancer']
    if d['female']:result=[name for name in result if name!='bard']
    return result

def validate(f):
    require(f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['growth']:require(growth(r['input'])==r['expected'],'Growth '+r['id'])
    for r in f['unlocks']:
        require(r['variant']==('synthetic-source-rule' if 'syntheticRequirements' in r['input'] else 'original'),'Explicit content variant')
        require(unlocks(r['input'],f)==r['expected'],'Unlock '+r['id'])
    return dict(growthDescriptions=len(f['growth']),unlockDescriptions=len(f['unlocks']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'));f=json.loads(FIXTURE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        actual=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in actual.items()),'Function '+r['symbol'])
    data=executable.read_bytes()
    require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS_942.21')
    offset=0x800660c4-0x80010000+0x800;table=data[offset:offset+190]
    for i,row in enumerate(f['unlockDefinitions']):
        values=[]
        for value in table[i*10:(i+1)*10]:values.extend([value>>4,value&15])
        require(row['job']==f['originalJobOrder'][i+1],'Source job order')
        require(row['requires']=={name:v for name,v in zip(f['originalJobOrder'],values) if v},'Prepared prerequisites '+row['job'])
    offset=0x800610b8-0x80010000+0x800+74*48+13
    require(list(data[offset:offset+10])==[11,100,15,75,100,100,60,90,50,80],'Prepared Squire growth/multipliers')
    return dict(validate(f),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']),preparedUnlockRows=19,preparedGrowthPairs=5)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path)
    a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
