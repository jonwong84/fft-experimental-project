"""Source-only ordinary AI clock descriptions; no C/reference runtime dependency."""
import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
REVISION='11464943bbdbd2a0af022d90cb7f10455cf911df'
FIXTURE=ROOT/'docs/fixtures/behavior/ordinary-spell-ai-clock-v1.json'


def simulate(data):
    units=sorted(copy.deepcopy(data['units']),key=lambda u:u['order'])
    actor=next(u for u in units if u['id']==data['actor'])
    actor['ct']=min(60,actor['ct']+(0 if actor['moved'] else 20)+(0 if actor['acted'] else 20))
    remaining=data['charge'];ticks=data['ticks'];phase='pick';events=0;draws=0;moved=set()
    for attempt in range(1,257):
        if attempt==256:
            return dict(ticks=ticks,remaining=remaining,cts=[u['ct'] for u in units],
                        moved=[u['id'] for u in units if u['id'] in moved],upkeepDraws=draws,eventCount=events,limit=True)
        while True:
            if phase=='pick':
                ready=[u for u in units if u['ct']>=100]
                if ready:
                    unit=min(ready,key=lambda u:(-u['ct'],u['order']))
                    unit['ct']%=100;draws+=1
                    if unit['dead']:continue
                    unit['moved']=False;unit['acted']=False;moved.add(unit['id']);events+=1
                    break
                phase='charge'
            elif phase=='charge':
                if remaining:
                    remaining-=1
                    if remaining==0:
                        events+=1
                        return dict(ticks=ticks,remaining=0,cts=[u['ct'] for u in units],
                                    moved=[u['id'] for u in units if u['id'] in moved],upkeepDraws=draws,eventCount=events,limit=False)
                phase='add'
            else:
                ticks+=1
                for unit in units:unit['ct']=min(254,unit['ct']+unit['speed'])
                phase='pick'
        if data['charge']==0:
            return dict(ticks=ticks,remaining=0,cts=[u['ct'] for u in units],
                        moved=[u['id'] for u in units if u['id'] in moved],upkeepDraws=draws,eventCount=events,limit=False)


def validate(fixture):
    if fixture['referenceRevision']!=REVISION or len(fixture['cases'])!=10:raise ValueError('Fixture identity')
    for case in fixture['cases']:
        if simulate(case['input'])!=case['expected']:raise ValueError('Clock description '+case['id'])
    return dict(clockDescriptions=10,sourceEventCap=255,immediateStillAdvances=True)


def audit(reference):
    if subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()!=REVISION:raise ValueError('Reference revision')
    if subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip():raise ValueError('Clean reference required')
    receipt=json.loads((ROOT/'docs/evidence/ai/ordinary-spell-ai-clock-source-v1.json').read_text(encoding='utf-8'))
    for row in receipt['sourceFiles']:
        if hashlib.sha256((reference/row['path']).read_bytes()).hexdigest()!=row['sha256']:raise ValueError('Source '+row['path'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,
                sourceFiles=len(receipt['sourceFiles']),originalExecuted=False,cExecuted=False)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    a=p.parse_args();r=audit(a.reference)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
