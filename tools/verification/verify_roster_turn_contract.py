"""ZOD-314/315 independent bounded decision-to-actual-command and turn descriptions."""
import argparse,copy,json
from pathlib import Path
from verify_roster_list_contract import describe as select
from verify_roster_general_contract import describe as scenarios
from verify_roster_combat_contract import describe as combat
from verify_roster_live_stone_contract import describe as stone
from verify_sword_knockback_contract import resolve,require,REVISION
from verify_roster_authority_contract import audit as authority_audit

ROOT=Path(__file__).resolve().parents[2];FIXTURE=ROOT/'docs/fixtures/behavior/roster-turn-contract-v1.json'


def describe(data,whole_turn):
    d=copy.deepcopy(data);actor=next(u for u in d['units'] if u['id']==d['actor']);workspace={tuple(v['tile']):v['priority'] for v in d['retained']}
    decisions=[];commands=[];ended=False;pending=False;frozen=None;failure=None
    def finish():
        nonlocal ended,pending
        actor['ct']=min(60,actor['ct']+(0 if actor['moved'] else 20)+(0 if actor['acted'] else 20));ended=True;pending=False
        if d['facing'] is not None:actor['face']=d['facing']
    def actual(action):
        nonlocal ended,pending
        if action['kind']=='Self':actor['pa']=min(99,actor['pa']+1)
        elif action['kind']=='Stone':
            request=copy.deepcopy(d);request['target']=action['target']
            for u in request['units']:u.update(brave=70,ko=u.get('ko',0))
            r=stone(request);target=next(u for u in d['units'] if u['id']==r['target']);target.update(hp=r['hp'],tile=r['destination'],ko=r['ko']);d.update(word=r['word'],count=r['count'])
        else:
            target=next(u for u in d['units'] if u['id']==action['target'])
            r=resolve(dict(seed=d['word'],drawCount=d['count'],pa=actor['pa'],power=d['power'],actor=actor['tile'],target=target['tile'],mapSize=[8,4],actorFacing=actor['face'],targetFacing=target['face'],hp=target['hp'],classEvade=target['evade'],actorBrave=70,targetBrave=70))
            if r['moved'] and any(u['tile']==r['target'] for u in d['units']):r['target']=target['tile']
            if r['hp']==0:target['ko']=(target.get('ko',0)+1)%256
            target.update(hp=r['hp'],tile=r['target']);d.update(word=r['rngWord'],count=r['drawCount'])
        actor['acted']=True
        if actor['moved']:actor['ct']=min(60,actor['ct']);ended=True;pending=True
    for _ in range(5):
        if frozen is not None:
            action=frozen;frozen=None;commands.append('Retained'+(action['kind'] if action.get('kind') else 'End'))
            if action.get('kind'):actual(action)
            else:finish()
        elif pending:
            commands.append('FinishFacing');pending=False
            if d['facing'] is not None:actor['face']=d['facing']
        elif ended:break
        else:
            if any(u['hp']==0 for u in d['units']):failure='DeferredCorpseInput';break
            request=copy.deepcopy(d);request['retained']=[dict(tile=list(k),priority=v) for k,v in workspace.items()]
            q=select(request)
            if q['rejection']!='None':failure=q['rejection'];break
            beforeWord=d['word'];beforeCount=d['count'];m=scenarios(dict(request,kind='scenarios'));action=q['ranked'][q['selected']] if q['selected'] is not None else None
            move=not actor['moved'] and (actor['acted'] or not(action and action['actFirst'])) and q['destination']!=actor['tile']
            kind='Move' if move else ('End' if actor['acted'] or action is None else action['kind'])
            decisions.append(dict(afterAct=actor['acted'],kind=kind,occurrence=q['selectedOccurrence'],entryWord=beforeWord,entryCount=beforeCount,word=q['word'],count=q['count']))
            d.update(word=q['word'],count=q['count']);commands.append(kind)
            if move:
                route=combat('route',dict(width=8,depth=4,units=d['units'],actor=d['actor'],move=actor['move'],jump=0,moved=actor['moved'],destination=q['destination']))
                require(route['path'] is not None,'Actual confirmed route');actor.update(tile=q['destination'],face=route['heading'],moved=True)
                if actor['acted']:ended=True;actor['ct']=min(60,actor['ct']);pending=False;actor['face']=d['facing'] or actor['face']
                else:frozen=action or {}
            elif kind=='End':finish()
            else:actual(action)
            for tile in m['mask']:workspace[tuple(tile)]=(-m['priority']+32768)%65536-32768
            if not whole_turn:break
    else:raise ValueError('Bounded turn did not settle')
    return dict(decisions=decisions,commands=commands,failure=failure,active=not ended,pending=pending,continuation=frozen is not None,
        units=[dict(id=u['id'],tile=u['tile'],hp=u['hp'],pa=u['pa'],ct=u['ct'],moved=u['moved'],acted=u['acted'],face=u['face'],ko=u.get('ko',0)) for u in sorted(d['units'],key=lambda u:u['order'])],
        word=d['word'],count=d['count'],ticks=d['ticks'],workspace=[dict(tile=list(k),priority=v) for k,v in sorted(workspace.items(),key=lambda p:(p[0][1],p[0][0]))])


def validate(f):
    require(f['ticket']=='ZOD-314/315' and f['referenceRevision']==REVISION and f['productionImplemented'] is False,'Description receipt')
    ids=[]
    for row in f['vectors']:require(describe(row['input'],row['wholeTurn'])==row['expected'],'Roster command/turn '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterCommandTurnDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);args=p.parse_args()
    a=authority_audit(args.reference);r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=a['referenceClean'],sourceFiles=a['sourceFiles'],sourceFunctions=a['sourceFunctions'])
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
