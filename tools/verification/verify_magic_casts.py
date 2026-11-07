"""ZOD-357 supplied-order ready-cast descriptions; no original/C/managed execution."""
import argparse,copy,json
from pathlib import Path
import verify_magic_current_inputs as current
import verify_magic_resources as resources
import verify_magic_entry_contract as entry
from verify_sword_knockback_contract import require,REVISION

FIXTURE=Path(__file__).resolve().parents[2]/'docs/fixtures/behavior/magic-cast-contract-v1.json'

def resolve(d):
    c=d['caster'];definition=d['definition'];ready=d['ready'];profile=d['targetProfile'];word=d['word'];count=d['count']
    current.validate_unit(c)
    def reject(reason='UnsupportedContext'):return dict(accepted=False,rejection=reason,outcome=None,word=word,count=count)
    if not all(ready[k] for k in ['chargeReady','bookkeeping','noHamedo','single','fresh']) or c.get('isMime',False) or definition['persevere'] or not 1<=definition['originalId']<368:return reject()
    if any(current.flag(c['statuses'],k) for k in ['dead','petrify','wall','charging','performing','defending']):return reject()
    cost=resources.mp(dict(actor=dict(mp=c['mp'],half=current.flag(c['supports'],'halfMp'),mime=c.get('isMime',False),frog=current.flag(c['statuses'],'frog'),silence=current.flag(c['statuses'],'silence'),dontAct=current.flag(c['statuses'],'dontAct'),restriction='None'),ability=dict(id=definition['originalId'],cost=definition['cost'],menu='Default',affected=definition['affected']),mode=d['mode'],bookkeeping=True))
    require(cost['accepted'] and cost['outcome']['block']=='None','Fixture assumes prepared usable cost')
    if not profile['order'] or not profile['noEarlier'] or not d['targets']:return reject()
    ids=[t['id'] for t in d['targets']]
    if len(ids)!=len(set(ids)):return reject()
    actor=copy.deepcopy(c);actor['mp']=cost['outcome']['remaining'];targets=[]
    for t in d['targets']:
        if t['id']==actor['id']:
            if t!=c and t!=actor:return reject()
            t=actor
        targets.append(t)
    results=[];next_word=word;next_count=count
    for t in targets:
        r=current.resolve(dict(caster=actor,target=t,spell=d['spell'],weather=d['weather'],mode=d['mode'],profile=d['resultProfile'],entry=dict(fresh=True,noEarlier=True,nonzero=True),word=next_word,count=next_count))
        if not r['accepted']:return reject(r['rejection'])
        results.append(dict(id=t['id'],result=r['outcome'],evasion=r['evasion'],procDisposition=r['procDisposition'],setFlag=r['setFlag']))
        next_word=r['word'];next_count=r['count']
    return dict(accepted=True,rejection='None',outcome=dict(mp=cost['outcome']['remaining'],cost=cost['outcome']['cost'],targets=results),word=next_word,count=next_count)

def validate(f):
    require(f['ticket']=='ZOD-357' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Fixture identity')
    for r in f['casts']:require(resolve(r)==r['expected'],'Cast '+r['id'])
    return dict(castDescriptions=len(f['casts']),originalExecuted=False,cExecuted=False,managedExecuted=False)

def audit(reference,executable):
    a=current.audit(reference);b=resources.audit(reference);p=entry.audit(reference,executable)
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,currentInputSourceFiles=a['sourceFiles'],currentInputSourceFunctions=a['sourceFunctions'],resourceSourceFiles=b['sourceFiles'],resourceSourceFunctions=b['sourceFunctions'],emptyStatusRecordVerified=p['emptyStatusRecordVerified'],fireTiersVerified=p['fireTiersVerified'])

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=audit(a.reference,a.prepared_executable)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
