"""ZOD-340 source-derived proc thresholds/modes/RNG with explicit managed scope/counter guards. No original/C execution."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-proc-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-proc-source-v1.json'
MAX=(1<<64)-1

def roll(raw):
    integer(raw,0,32767);percent=raw*100//32768
    return dict(percent=percent,passed=percent<19)

def decision(d):
    require(d['formula'] in ('Damage','Healing') and d['invocation'] in ('FormulaMissed','ElementNullified','FamilyTail') and d['mode'] in ('Execute','AiSimulation','Preview'),'Known decision modes')
    require(not(d['formula']=='Healing' and d['invocation']=='ElementNullified'),'Healing has no damage-element stage')
    word=integer(d['word'],0,0xffffffff);count=integer(d['count'],0,MAX)
    result=dict(accepted=True,rejection='None',word=word,count=count)
    def reject(reason):return dict(accepted=False,rejection=reason,word=word,count=count)
    def without_draw(reason):
        return dict(result,disposition=reason,invoked=reason in ('Passed','Failed'),applyStatus=reason=='Passed',setFlag=False,raw=None,percent=None)
    if d['invocation']=='FormulaMissed':return without_draw('FormulaMissed')
    if d['formula']=='Healing':return without_draw('NoConditionalBranch')
    if d['invocation']=='ElementNullified':return without_draw('ElementNullified')
    p=d['profile']
    if p is None or not p['primary'] or not p['noPending'] or (d['mode']!='Preview' and not p['clearFlag']):return reject('UnsupportedContext')
    if d['mode']=='Preview':return without_draw('Passed')
    if d['mode']=='AiSimulation':return without_draw('Failed')
    if count==MAX:return reject('RandomDrawCountExhausted')
    word=(word*0x41c64e6d+12345)&0xffffffff;raw=(word>>16)&32767;r=roll(raw)
    return dict(accepted=True,rejection='None',word=word,count=count+1,disposition='Passed' if r['passed'] else 'Failed',invoked=True,applyStatus=r['passed'],setFlag=r['passed'],raw=raw,percent=r['percent'])

def validate(f):
    require(f['ticket']=='ZOD-340' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source description identity')
    for row in f['rolls']:require(roll(row['raw'])==dict(percent=row['percent'],passed=row['passed']),'Threshold '+str(row['raw']))
    for row in f['decisions']:require(decision(row['input'])==row['expected'],'Decision '+row['id'])
    require(len({r['id'] for r in f['decisions']})==len(f['decisions']),'Distinct decision IDs')
    return dict(rollDescriptions=len(f['rolls']),decisionDescriptions=len(f['decisions']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    source=json.loads(SOURCE.read_text(encoding='utf-8'))
    require(source['ticket']=='ZOD-340' and source['referenceRevision']==REVISION,'Source receipt')
    for row in source['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Source '+row['path'])
    for row in source['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']);require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    data=executable.read_bytes()
    require(len(data)==356352 and hashlib.sha256(data).hexdigest()=='287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562','Prepared SCUS_942.21')
    offset=0x80063fc4-0x80010000+0x800
    require(data[offset:offset+6]==bytes(6),'Empty status record0')
    for ability in range(16,20):
        offset=0x8005fbf0-0x80010000+0x800+14*ability
        require(data[offset+8]==8 and data[offset+11]==0,'Prepared Fire tier formula and empty status record')
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(source['sourceFiles']),sourceFunctions=len(source['sourceFunctions']),emptyStatusRecordVerified=True,fireTiersVerified=4)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
