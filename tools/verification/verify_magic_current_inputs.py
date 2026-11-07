"""ZOD-348 current-unit input descriptions. Independent of managed execution; no original/C execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer
import verify_magic_entry_contract as entry

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-current-input-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-current-input-source-v1.json'

def compatibility(a,b):
    for d in [a,b]:
        integer(d['sign'],0,12);require(all(type(d[k]) is bool for k in ['male','female','monster']),'Identity flags')
    if 12 in [a['sign'],b['sign']]:return 'Serpentarius'
    difference=abs(a['sign']-b['sign'])
    if difference==6:
        if a['monster'] or b['monster']:return 'Bad'
        return 'Worst' if (a['male'],a['female'])==(b['male'],b['female']) else 'Best'
    return 'Bad' if difference in [3,9] else 'Good' if difference in [4,8] else 'Neutral'

def flag(d,name):return d.get(name,False)
def validate_unit(d):
    require(isinstance(d['id'],str) and d['id'].strip(),'Stable unit ID')
    integer(d['ma'],0,255);integer(d['faith'],0,255);integer(d['mp'],0,65535)
    require(all(type(v) is bool for v in d['statuses'].values()) and all(type(v) is bool for v in d['supports'].values()),'Named current flags')
    integer(d['strengthen'],0,255)
    for v in d['affinities'].values():integer(v,0,255)
    require(type(d['unrestricted']) is bool and type(d['rider']) is bool,'Current classified facts')
    compatibility(d['identity'],d['identity'])
    if d['equipment'] is not None:require(d['equipment']['monster']==d['identity']['monster'],'Consistent monster facts')

def project(d):
    c=d['caster'];t=d['target'];s=d['spell'];validate_unit(c);validate_unit(t)
    require(s['kind'] in ['Damage','Healing'],'Known family');integer(s['power'],0,255);integer(s['elements'],0,255)
    if s['kind']=='Damage':integer(d['weather'],0,7)
    unsupported=any(flag(u['statuses'],k) for u in [c,t] for k in ['dead','petrify','wall']) or (c['id']==t['id'] and c!=t)
    if s['kind']=='Damage':
        unsupported |= bool(s['elements']&128 and flag(t['statuses'],'oil') or s['elements']&8 and (flag(t['statuses'],'float') or t['rider']))
    if unsupported:return dict(accepted=False,amount=None)
    faith=lambda u:dict(stored=u['faith'],faithStatus=flag(u['statuses'],'faith'),innocentStatus=flag(u['statuses'],'innocent'))
    a=dict(kind=s['kind'],magicAttack=c['ma'],power=s['power'],caster=faith(c),target=faith(t),modifiers=dict(
        strengthensElement=bool(c['strengthen']&s['elements']),magicAttackUp=flag(c['supports'],'magicAttackUp'),
        magicDefenseUp=flag(t['supports'],'magicDefenseUp'),targetShell=flag(t['statuses'],'shell'),
        targetChickenOrFrog=flag(t['statuses'],'chicken') or flag(t['statuses'],'frog'),compatibility=compatibility(c['identity'],t['identity'])))
    return dict(accepted=True,amount=a)

def resolve(d):
    p=project(d);t=d['target'];c=d['caster'];s=d['spell'];st=t['statuses']
    if not p['accepted']:return dict(accepted=False,rejection='UnsupportedContext',outcome=None,evasion=None,procDisposition=None,setFlag=None,word=d['word'],count=d['count'])
    f=dict(input=p['amount'],context=dict(elements=s['elements'],weather=d['weather'],**t['affinities']),targetIsUndead=flag(st,'undead'),accuracy=100,mode=d['mode'],profile=d['profile'])
    modifiers=dict(unrestricted=t['unrestricted'] and not flag(st,'dontAct'),transparent=flag(c['statuses'],'transparent'),abandon=flag(t['supports'],'abandon'),**{k:flag(st,k) for k in ['dontAct','sleep','stop','confusion','charging','performing','defending']})
    return entry.resolve(dict(formula=f,equipment=t['equipment'],modifiers=modifiers,evadeable=s['evadeable'],entry=d['entry'],word=d['word'],count=d['count']))

def validate(f):
    require(f['ticket']=='ZOD-348' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['compatibility']:require(compatibility(r['caster'],r['target'])==r['expected'],'Compatibility '+r['id'])
    for r in f['projections']:require(project(r)==r['expected'],'Projection '+r['id'])
    for r in f['queries']:require(resolve(r)==r['expected'],'Query '+r['id'])
    return dict(compatibilityDescriptions=len(f['compatibility']),projectionDescriptions=len(f['projections']),queryDescriptions=len(f['queries']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        a=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in a.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
