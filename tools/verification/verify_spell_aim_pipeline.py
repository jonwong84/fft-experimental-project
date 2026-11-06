"""Normalized stored-aim casting phases; no original/C/managed execution."""
import argparse,copy,json
from pathlib import Path
import verify_spell_follow_contract as follow
import verify_magic_resources as resources
import verify_magic_casts as casts
from verify_sword_knockback_contract import require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/spell-aim-pipeline-contract-v1.json'

def resolve(d):
    word=d['word'];count=d['count'];counter=None
    def reject(block=None,reason='UnsupportedContext'):
        return dict(accepted=False,block=block,rejection=reason,counter=counter,mp=None,cost=None,ids=None,damage=None,added=None,word=word,count=count)
    # Default nonweapon choice: Auto0 precedes blue panels. Manual capture excludes NoTargeting.
    mode=follow.choice(dict(auto=d['autoZero'],range=0 if d['autoZero'] else 1,weaponRange=False,panelCount=2,noFollow=d['noFollow']))
    if mode not in [0,1]:return reject()
    aim=follow.confirmation(dict(player=True,answer=d['answer'],occupantId=1,storedType=5,storedId=255))
    if not aim['startsAttack']:return reject()
    if not follow.preparation_target_checks(dict(actorId=0,actorActive=True,type=aim['type'],targetId=aim['targetId'],targetActive=d['beginAnchorActive'])):return reject()
    counter=resources.setup(dict(raw=d['rawCt'],short=d['short'],non=d['non'],persevere=False))
    for _ in range(d['ticks']):counter=resources.tick(dict(counter=counter,blocked=d['tickBlocked']))['counter']
    if counter!=0 or d['readyBlocked'] or not d['identityKnown']:return reject()
    template=copy.deepcopy(json.loads(casts.FIXTURE.read_text(encoding='utf-8'))['casts'][0])
    c=template['caster'];c['mp']=d['readyMp'];c['ma']=d['readyMa'];c['supports']['halfMp']=d['readyHalf']
    cost=resources.mp(dict(actor=dict(mp=c['mp'],half=d['readyHalf'],mime=False,frog=False,silence=False,dontAct=False,restriction='None'),ability=dict(id=16,cost=9,menu='Default',affected=True),mode=d['mode'],bookkeeping=True))
    if cost['outcome']['block']!='None':return reject(cost['outcome']['block'])
    if aim['type']==6 and not d['knownAnchor']:return reject()
    centre=[3,0,0] if aim['type']==6 else [1,0,0]
    def u(id,order,x,active=True):return dict(id=id,order=order,position=[x,0,0],initial=0,current=0,active=active,crystal=False,treasure=False,jump=False,mount=False)
    area=dict(map=dict(width=5,depth=1,layers=1,overrides=[]),geometry=dict(range=1,area=0,vertical=0,rangeVertical=False,cannotTargetSelf=False,cannotHitCaster=False,topDown=False,allies=False,enemies=False),casterId='actor',caster=[0,0,0],aim=centre,profile=dict(ordinary=True,noSpecial=True,noCross=True,padding=True),complete=True,nearest=False,units=[u('second',3,3),u('actor',0,0),u('old',2,1),u('first',1,3,d['readyAnchorActive'])])
    selected=follow.targeting.selection(area)['ids'];first,second=template['targets'];old=copy.deepcopy(first);old['id']='old'
    second['statuses']['dead']=d['secondDead'];by_id=dict(first=first,second=second,old=old,actor=c)
    result=casts.resolve(dict(template,caster=c,targets=[by_id[id] for id in selected],mode=d['mode'],targetProfile=dict(order=True,noEarlier=d['noEarlier']),word=word,count=count))
    if not result['accepted']:return reject(reason=result['rejection'])
    o=result['outcome']
    return dict(accepted=True,block=None,rejection='None',counter=counter,mp=o['mp'],cost=o['cost'],ids=[t['id'] for t in o['targets']],damage=[t['result']['damage'] for t in o['targets']],added=[t['setFlag'] for t in o['targets']],word=result['word'],count=result['count'])

def validate(f):
    require(f['referenceRevision']==REVISION and f['productionImplemented'] is False and f['tickets']==['ZOD-'+str(n) for n in range(364,370)],'Fixture identity')
    for r in f['cases']:require(resolve(copy.deepcopy(r['input']))==r['expected'],'Phase '+r['id'])
    return dict(phaseDescriptions=len(f['cases']),originalExecuted=False,cExecuted=False,managedExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--prepared-executable',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args()
    r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),followSource=follow.audit(a.reference),castSource=casts.audit(a.reference,a.prepared_executable))
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
