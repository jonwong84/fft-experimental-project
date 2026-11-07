"""Source vital arithmetic and bounded living projections; no original/C/managed execution."""
import argparse,json,subprocess
from pathlib import Path
from verify_sword_knockback_contract import require,fingerprint,function_fingerprint,REVISION
from verify_faith_magic_contract import integer

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/magic-publication-contract-v1.json'
SOURCE=ROOT/'docs/evidence/combat/magic-publication-source-v1.json'

def vitals(d):
    for k in ['hp','maxHp','mp','maxMp']:integer(d[k],0,65535)
    for k in ['damage','healing','mpDamage','mpHealing']:integer(d[k],-32768,32767)
    require(type(d['hit']) is bool,'Hit bit')
    if not d['hit']:return dict(hp=d['hp'],mp=d['mp'],changed=False)
    hp=min(d['maxHp'],max(0,d['hp']-d['damage']+d['healing']))
    mp=min(d['maxMp'],max(0,d['mp']-d['mpDamage']+d['mpHealing']))
    return dict(hp=hp,mp=mp,changed=hp!=d['hp'] or mp!=d['mp'])

def living(d):
    def reject(reason):return dict(accepted=False,rejection=reason,hp=None,mp=None,changed=False)
    if not d.get('activeKnown',True) or not d['reactionsAbsent']:return reject('UnsupportedContext')
    if not 0<d['hp']<=d['maxHp'] or d['mp']>d['maxMp'] or any(d[k] for k in ['dead','petrify','wall']):return reject('UnsupportedState')
    if not d['hit']:return dict(accepted=True,rejection='None',hp=d['hp'],mp=d['mp'],changed=False)
    if not all(d[k] for k in ['golemSharingAbsent','otherWorkAbsent','ordinaryStatusRules']):return reject('UnsupportedContext')
    if d['damage']>999 or d['healing']>999:return reject('UnsupportedFrame')
    if d['critical']!=(d['hp']<=d['maxHp']//5) or d['chicken']!=(d['brave']<10):return reject('UnsupportedState')
    if d['hpDamageType']:
        if not d['charmAbsent']:return reject('UnsupportedContext')
        if any(d[k] for k in ['sleep','confusion','transparent']):return reject('WouldChangeStatus')
    r=vitals(dict(d,mpDamage=0,mpHealing=0))
    if r['hp']==0:return reject('WouldKnockOut')
    if (r['hp']<=d['maxHp']//5)!=d['critical']:return reject('WouldChangeStatus')
    return dict(accepted=True,rejection='None',**r)

def validate(f):
    require(f['ticket']=='ZOD-370' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['vitals']:require(vitals(r['input'])==r['expected'],'Vitals '+r['id'])
    for r in f['living']:require(living(r['input'])==r['expected'],'Living '+r['id'])
    return dict(vitalDescriptions=len(f['vitals']),livingDescriptions=len(f['living']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    s=json.loads(SOURCE.read_text(encoding='utf-8'))
    for r in s['sourceFiles']:require(fingerprint(reference,r['path'])==r,'Source '+r['path'])
    for r in s['sourceFunctions']:
        actual=function_fingerprint(reference/r['path'],r['symbol']);require(all(r[k]==v for k,v in actual.items()),'Function '+r['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(s['sourceFiles']),sourceFunctions=len(s['sourceFunctions']))

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
