"""ZOD-342 magic-only evasion source descriptions; no original, C or managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-evasion-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-evasion-source-v1.json'
MAX=(1<<64)-1

def load(d):
    require(type(d['monster']) is bool,'Known monster fact')
    for key in ('accessory','right','left'):
        slot=d[key];integer(slot['item'],0,255);integer(slot['evade'],0,255);require(type(slot['qualifies']) is bool,'Known primary type fact')
    rw=integer(d['rightWeapon'],0,255);lw=integer(d['leftWeapon'],0,255)
    values=[0 if d['monster'] or not d[k]['qualifies'] else d[k]['evade'] for k in ('accessory','right','left')]
    return dict(evades=values,items=[d['accessory']['item'],d['right']['item'] if rw==255 else rw,d['left']['item'] if lw==255 else lw])

def prepare(d,m):
    require(all(type(m[k]) is bool for k in ('unrestricted','transparent','abandon','dontAct','sleep','stop','confusion','charging','performing','defending')),'Explicit modifier facts')
    c=load(d);values=c['evades'];base=100
    if m['transparent']:values=[0,0,0]
    abandon=m['unrestricted'] and m['abandon']
    if abandon:base//=2
    if any(m[k] for k in ('dontAct','sleep','stop','confusion','charging','performing')):values=[0,0,0]
    if m['defending']:base//=2
    return dict(base=base,evades=values,abandon=abandon)

def roll(d):
    prepared=prepare(d['equipment'],d['modifiers']);items=load(d['equipment'])['items']
    require(d['mode'] in ('Execute','Preview','AiSimulation'),'Known mode');require(type(d['evadeable']) is bool,'Known secondary evadeable')
    accuracy=integer(d['accuracy'],0,65535);word=integer(d['word'],0,0xffffffff);count=integer(d['count'],0,MAX)
    original=(word,count);hit=True;miss=0;item=0
    if d['evadeable']:
        base=prepared['base'];evades=prepared['evades']+[0]
        if any(e>=base for e in evades):hit=False;accuracy=0
        else:
            product=1
            for e in evades:product*=(base-e)*100//base
            accuracy=(product//1000000)*accuracy//100
        for index,e in enumerate(evades):
            if count==MAX:return dict(accepted=False,rejection='RandomDrawCountExhausted',outcome=None,word=original[0],count=original[1],draws=0)
            word=(word*0x41c64e6d+12345)&0xffffffff;count+=1
            if ((word>>16)&32767)*base//32768<e and d['mode']=='Execute':
                hit=False;miss=index+1;item=(items+[255])[index];break
    return dict(accepted=True,rejection='None',outcome=dict(hit=hit,accuracy=accuracy,miss=miss,itemLost=item,abandon=prepared['abandon']),word=word,count=count,draws=count-original[1])

def validate(f):
    require(f['ticket']=='ZOD-342' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Source description identity')
    for row in f['loads']:require(load(row['input'])==row['expected'],'Load '+row['id'])
    for row in f['preparations']:require(prepare(row['equipment'],row['modifiers'])==row['expected'],'Prepare '+row['id'])
    for row in f['rolls']:require(roll(row)==row['expected'],'Roll '+row['id'])
    for name in ('loads','preparations','rolls'):require(len({r['id'] for r in f[name]})==len(f[name]),'Distinct '+name+' IDs')
    return dict(loadDescriptions=len(f['loads']),preparationDescriptions=len(f['preparations']),rollDescriptions=len(f['rolls']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'));require(s['ticket']=='ZOD-342' and s['referenceRevision']==REVISION,'Source receipt')
    for row in s['sourceFiles']:require(fingerprint(reference,row['path'])==row,'Source '+row['path'])
    for row in s['sourceFunctions']:
        actual=function_fingerprint(reference/row['path'],row['symbol']);require(all(row[k]==v for k,v in actual.items()),'Function '+row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
