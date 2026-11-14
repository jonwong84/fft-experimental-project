"""ZOD-316 independent real-turn/ordinary-clock composition; no original/C/managed execution."""
import argparse,copy,json
from pathlib import Path
from verify_roster_turn_contract import describe as turn
from verify_roster_authority_contract import advance,audit
from verify_sword_knockback_contract import require,REVISION

FIXTURE=Path(__file__).resolve().parents[2]/'docs/fixtures/behavior/roster-replay-contract-v1.json'

def describe(data,turns):
    d=copy.deepcopy(data);steps=[]
    for index in range(turns):
        result=turn(d,True);step=dict(actor=d['actor'],turn=result,clock=None);steps.append(step)
        for u in d['units']:
            v=next(v for v in result['units'] if v['id']==u['id']);u.update(v)
        d.update(word=result['word'],count=result['count'],retained=result['workspace'])
        if result['failure'] is not None or any(u['hp']==0 for u in d['units']) or index==turns-1:break
        # Real turn_end returns PICK_UNIT: check already-ready peers before adding CT.
        ticks=0
        while not any(u['ct']>=100 for u in d['units']):
            for u in d['units']:u['ct']=min(254,u['ct']+u['speed'])
            d['ticks']+=1;ticks+=1
        actor=min((u for u in d['units'] if u['ct']>=100),key=lambda u:(-u['ct'],u['order']))
        before=actor['ct'];actor.update(ct=before%100,moved=False,acted=False)
        d['word']=advance(d['word'],1);d['count']+=1;d['actor']=actor['id']
        step['clock']=dict(ticks=ticks,atTick=d['ticks'],actor=actor['id'],before=before,ct=actor['ct'],raw=(d['word']>>16)&32767,word=d['word'],count=d['count'])
    return steps

def validate(f):
    require(f['ticket']=='ZOD-316' and f['referenceRevision']==REVISION and not f['productionImplemented'],'Receipt')
    for row in f['vectors']:require(describe(row['input'],row['turns'])==row['expected'],'Real-turn replay '+row['id'])
    return dict(rosterReplayDescriptions=len(f['vectors']),turns=sum(len(v['expected']) for v in f['vectors']),originalExecuted=False,cExecuted=False,managedExecuted=False)

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args()
    r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),source=audit(a.reference))
    if a.output:
        with a.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
