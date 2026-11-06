"""Source-only ordinary AI clock descriptions; no C/reference runtime dependency."""
import argparse
import copy
import hashlib
import json
import subprocess
from pathlib import Path
from verify_ordinary_spell_ai import mp_term

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


def prepared_case(original, selection, mode):
    """Independent two-unit source description; no managed output supplies timing facts."""
    tier=(original-16)%4;charge=[4,5,7,10][tier];cost=[6,12,24,48][tier]
    if original==21:cost=10
    damage=[14,18,24,32][tier]*8*70*70//10000
    count={'fire':1,'elemental':12,'learning':2}[selection]
    share=count*4//(count+2);guard_mp=75 if selection=='learning' else 50
    # Each prepared profile has already consumed Mage's initial tick10 upkeep draw.
    seed=10 if selection=='learning' else 57
    value=(seed*1103515245+12345)&0xffffffff;entry=dict(Value=value,DrawCount=1)
    ct=[20,80];acted=[True,False];opportunities=set();events=[];upkeep=[]
    for elapsed in range(charge):
        if elapsed:ct=[min(254,ct[0]+10),min(254,ct[1]+8)]
        while max(ct)>=100:
            index=ct.index(max(ct));name=['mage','guard'][index];before=ct[index];ct[index]%=100
            value=(value*1103515245+12345)&0xffffffff
            upkeep.append(dict(id=name,CtBefore=before,CtAfter=ct[index],Dead=False,RawRoll=(value>>16)&32767))
            acted[index]=False;opportunities.add(name)
            events.append(dict(kind='UnitReady',id=name,Tick=10+elapsed))
    events.append(dict(kind='AbilityReady',id='mage',Tick=9+charge))
    formula=dict(Value=value,DrawCount=1+len(upkeep))
    for _ in range(4):value=(value*1103515245+12345)&0xffffffff
    score=128+mp_term(75-cost,75,6,share)-(200-damage)*128//200
    scored=mode=='Unit'
    return dict(sourceSlot=original-16,spellId=f'fft.ps1:ability/source-{original:04}',OriginalAbilityId=original,
                aimMode=mode,disposition='Scored' if scored else 'NotValuable',consideredDelay=charge,actualChargeTime=charge,
                priority=score if scored else None,
                units=[dict(id='mage',Hp=100,Mp=75-cost if scored else 75),
                       dict(id='guard',Hp=200-damage if scored else 200,Mp=guard_mp)],
                damage=damage,clock=dict(Ticks=9+charge,ElapsedTicks=charge-1,RemainingCharge=0,EventLimitReached=False,
                    units=[dict(id=name,Ct=ct[i],MovementTaken=False,ActionTaken=acted[i]) for i,name in enumerate(['mage','guard'])],
                    opportunities=[name for name in ['mage','guard'] if name in opportunities],events=events,upkeep=upkeep),
                formulaEntryRng=formula,entryRng=entry,exitRng=dict(Value=value,DrawCount=5+len(upkeep)))


def validate_inspection(report, selection):
    ids={'fire':[16],'elemental':list(range(16,28)),'learning':[16,19]}[selection]
    expected=[prepared_case(original,selection,mode) for original in ids for mode in ['Unit','Tile']]
    if (report['referenceRevision']!=REVISION or not report['eachCaseRestartsFromSameStateAndRng'] or
        report['cExecuted'] or report['sourceOriginalExecuted'] or len(report['cases'])!=len(expected)):
        raise ValueError('Independent delayed hypothesis identity')
    for actual,wanted in zip(report['cases'],expected):
        for key in wanted.keys()-{'priority','damage'}:
            if actual[key]!=wanted[key]:raise ValueError(f'Delayed {wanted["OriginalAbilityId"]}/{wanted["aimMode"]}: {key}')
        score=actual['Score']
        if wanted['priority'] is None:
            if score is not None:raise ValueError('Excluded tile must not rank')
        elif score is None or score['Priority']!=wanted['priority'] or score['HitPercent']!=100:
            raise ValueError('Independent delayed score')
        frames=actual['frames']
        if (len(frames)!=1 or frames[0]['targetId']!='guard' or not frames[0]['Result']['Hit'] or
            frames[0]['Result']['HpDamage']!=wanted['damage'] or frames[0]['Result']['Effects']['AddedEffect']):
            raise ValueError('Delayed AI formula, including excluded tile draws')
    return dict(preparedDelayedHypotheses=len(expected),selection=selection,independentTimingRngAndScoresMatched=True)


def audit(reference, inspection=None, selection='fire'):
    if subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()!=REVISION:raise ValueError('Reference revision')
    if subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip():raise ValueError('Clean reference required')
    receipt=json.loads((ROOT/'docs/evidence/ai/ordinary-spell-ai-clock-source-v1.json').read_text(encoding='utf-8'))
    for row in receipt['sourceFiles']:
        if hashlib.sha256((reference/row['path']).read_bytes()).hexdigest()!=row['sha256']:raise ValueError('Source '+row['path'])
    checked={} if inspection is None else validate_inspection(json.loads(inspection.read_text(encoding='utf-8-sig')),selection)
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,
                sourceFiles=len(receipt['sourceFiles']),originalExecuted=False,cExecuted=False,**checked)


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    p.add_argument('--inspection',type=Path);p.add_argument('--selection',choices=['fire','elemental','learning'],default='fire')
    a=p.parse_args();r=audit(a.reference,a.inspection,a.selection)
    if a.output:
        with a.output.open('x',encoding='utf-8') as stream:json.dump(r,stream,indent=2);stream.write('\n')
    print(json.dumps(r))
