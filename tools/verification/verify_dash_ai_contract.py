"""ZOD-321–325 independent semantic/candidate/ranked/list/actual-turn compositions."""
import argparse,json
from pathlib import Path
from verify_roster_preparation_contract import describe as preparation
from verify_roster_sword_contract import describe as candidate
from verify_roster_stone_entry_contract import describe as entry
from verify_roster_list_contract import describe as selection
from verify_roster_turn_contract import describe as turn
from verify_roster_replay_contract import describe as replay
from verify_roster_authority_contract import audit
from verify_sword_knockback_contract import require,REVISION

FIXTURE=Path(__file__).resolve().parents[2]/'docs/fixtures/behavior/dash-ai-contract-v1.json'
def describe(row):
    d=row['input'];kind=row['kind']
    if kind=='preparation':return preparation(kind,d)
    if kind=='candidate':return candidate(d,dash=True)
    if kind=='entry':return entry(d,dash=True)
    if kind=='list':return selection(d)
    if kind=='turn':return turn(d,True)
    require(kind=='replay','Description kind');return replay(d,row['turns'])
def validate(f):
    require(f['ticket']=='ZOD-321–325' and f['referenceRevision']==REVISION and not f['productionImplemented'],'Dash AI source receipt')
    for r in f['vectors']:require(describe(r)==r['expected'],'Dash AI '+r['id'])
    require(len(f['vectors'])==len({r['id'] for r in f['vectors']}),'Distinct descriptions')
    return dict(dashAiDescriptions=len(f['vectors']),originalExecuted=False,cExecuted=False,managedExecuted=False)
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path);a=p.parse_args();r=dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),source=audit(a.reference))
    if a.output:
        with a.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
