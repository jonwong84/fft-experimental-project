"""ZOD-302 fresh supported roster list source descriptions, rank admission and lottery."""
import argparse,copy,json,subprocess
from pathlib import Path
from verify_roster_general_contract import describe as general_description
from verify_roster_attack_contract import describe as attack_description
from verify_roster_stone_entry_contract import describe as stone_description
from verify_roster_action_contract import describe as self_description
from verify_sword_knockback_contract import fingerprint,function_fingerprint,require,REVISION

ROOT=Path(__file__).resolve().parents[2]
FIXTURE=ROOT/'docs/fixtures/behavior/roster-list-contract-v1.json'
EVIDENCE=ROOT/'docs/evidence/ai/roster-list-source-v1.json'


def describe(data):
    d=copy.deepcopy(data);d['kind']='scenarios';m=general_description(d);require(m['rejection']=='None','Ordinary prefix')
    actor=next(u for u in d['units'] if u['id']==d['actor']);word=m['word'];count=m['count'];ranked=[None]*8;entries=[]
    for index,kind in enumerate(['Attack']+actor['actions']+['Frog']):
        entryWord=word;entryCount=count;best=None;insert=None
        if not actor['acted'] and kind!='Frog':
            fresh=(m,word,count)
            if kind=='Self':
                result=self_description('entry',d,fresh=fresh)
                if result['best'] is not None:
                    c=result['attempts'][result['best']];best=dict(occurrence=index,kind=kind,destination=m['d1'],actFirst=c['order']=='ActFirst',target=None,priority=c['priority'],hit=c['hit'],proximity=m['proximity'][m['m0'].index(m['d1'])])
            else:
                if kind=='Attack':result=attack_description(d,fresh=fresh)
                else:
                    require(kind=='Stone','Supported list');request=copy.deepcopy(d);request['occurrence']=index;result=stone_description(request,fresh=fresh)
                    if result['rejection']!='None':return dict(rejection=result['rejection'],occurrence=index,word=d['word'],count=d['count'])
                if result['best'] is not None:
                    c=result['candidates'][result['best']];best=dict(occurrence=index,kind=kind,destination=c['destination'],actFirst=c.get('actFirst',c.get('order')=='ActThenMove'),target=c['target'],priority=c['priority'],hit=c['hit'],proximity=c['proximity'])
            word=result['word'];count=result['count']
            if best and (best['priority'],best['hit'])>(m['priority'],100):
                higher=lambda old:old is None or (best['priority'],best['hit'],-best['proximity'])>(old['priority'],old['hit'],-old['proximity'])
                if higher(ranked[-1]):
                    insert=next(i for i,old in enumerate(ranked) if higher(old));ranked.insert(insert,best);ranked.pop()
        entries.append(dict(occurrence=index,kind=kind,entryWord=entryWord,entryCount=entryCount,exitWord=word,exitCount=count,best=best,insertion=insert))
    lotteryWord=word;lotteryCount=count;rolls=[];selected=None;anyHit=False
    for index,row in enumerate(ranked):
        require(count<0xffffffffffffffff,'Lottery budget');word=(word*1103515245+12345)&0xffffffff;count+=1;raw=(word>>16)&32767;chance=row['hit'] if row else 0;anyHit|=chance!=0
        passed=raw%100<chance;rolls.append(dict(slot=index,raw=raw,modulo=raw%100,hit=chance,passed=passed))
        if passed:selected=index;break
    if selected is None and anyHit:selected=0;reason='AllRollsFailedUseFirst'
    elif selected is None:reason='MovementFallback'
    else:reason='FirstRoll'
    winner=ranked[selected] if selected is not None else None
    return dict(rejection='None',entries=entries,ranked=ranked,lotteryWord=lotteryWord,lotteryCount=lotteryCount,rolls=rolls,selected=selected,reason=reason,
        destination=winner['destination'] if winner else m['d0'],selectedOccurrence=winner['occurrence'] if winner else None,word=word,count=count)


def validate(fixture):
    require(fixture['schemaVersion']==1 and fixture['ticket']=='ZOD-302' and fixture['referenceRevision']==REVISION and fixture['productionImplemented'] is False,'Source receipt')
    ids=[]
    for row in fixture['vectors']:require(describe(row['input'])==row['expected'],'Roster list '+row['id']);ids.append(row['id'])
    require(bool(ids) and len(ids)==len(set(ids)),'Distinct vectors')
    return dict(rosterListDescriptions=len(ids),originalExecuted=False,cExecuted=False,managedExecuted=False)


def audit(reference):
    r=json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==r['referenceRevision']==REVISION,'Pinned reference')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean reference')
    for f in r['sourceFiles']:require(fingerprint(reference,f['path'])==f,'File '+f['path'])
    for f in r['sourceFunctions']:
        actual=function_fingerprint(reference/f['path'],f['symbol']);require(all(f[k]==v for k,v in actual.items()),'Function '+f['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),referenceRevision=REVISION,referenceClean=True,sourceFiles=len(r['sourceFiles']),sourceFunctions=len(r['sourceFunctions']))


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();r=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as f:json.dump(r,f,indent=2);f.write('\n')
    print(json.dumps(r))
