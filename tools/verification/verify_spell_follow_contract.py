"""ZOD-363 source-trace descriptions, not original/C/managed execution or a command engine."""
import argparse,copy,json,subprocess
from pathlib import Path
import verify_spell_targeting as targeting
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/spell-follow-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/spell-follow-source-v1.json'

def choice(d):
    # Ordinary default-menu ability branch only, before any UI decision.
    if d['auto'] and d['range']==0 and not d['weaponRange']:return 2
    if d['panelCount']==0:return 3
    return 1 if d['noFollow'] else 0

def confirmation(d):
    kind=integer(d['answer'],0,255);t=d['storedType'];id=d['storedId']
    if kind not in [5,6,7]:return dict(type=t,targetId=id,startsAttack=False)
    if d['player']:
        if kind==5:t=5
        elif d['occupantId'] is None:t=5
        else:t=kind;id=d['occupantId']  # YES7 is stored verbatim, not normalized to UNIT6.
    return dict(type=t,targetId=id,startsAttack=True)

def preparation_target_checks(d):
    return (0<=d['actorId']<21 and d['actorActive'] and
            (d['type']!=6 or 0<=d['targetId']<21 and d['targetActive']))

def center(d):
    """Only the ordinary wrapper/centre-selection slice, not complete green area validity."""
    kind=integer(d['type'],0,255);integer(d['actorId'],0,255);integer(d['targetId'],0,255)
    for v in d['stored']:integer(v,-32768,32767)
    for p in [d['actorPosition'],d['targetPosition']]:
        integer(p[0],0,255);integer(p[1],0,255);integer(p[2],0,1)
    if d['actorId']>=21 or not d['actorActive'] or kind==6 and d['targetId']>=21:return dict(resolved=False,center=None)
    if d['directional']:p=d['actorPosition'][:]
    elif kind==6:p=d['targetPosition'][:]
    else:p=[v&255 for v in d['stored']]
    if p[0]>=d['width'] or p[1]>=d['depth'] or p[2]>=2 or p in d['blocked']:return dict(resolved=False,center=None)
    return dict(resolved=True,center=p)

def scenario(d):
    c=center(d['aim']);require(c['resolved'],'Scenario centre')
    return targeting.selection(dict(d['area'],aim=c['center'],kind='Area',nearest=False))

def validate(f):
    require(f['ticket']=='ZOD-363' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    groups=[('choices',choice),('confirmations',confirmation),('preparations',preparation_target_checks),('centers',center),('scenarios',scenario)]
    for name,fn in groups:
        for r in f[name]:require(fn(copy.deepcopy(r['input']))==r['expected'],name+' '+r['id'])
    return dict(descriptions={k:len(f[k]) for k,_ in groups},originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        a=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,
        sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']),priorGeometry=targeting.audit(reference))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
