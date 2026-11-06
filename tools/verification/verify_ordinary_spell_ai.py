"""Independent source arithmetic descriptions for ZOD-403 onward; no C execution."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'


def known_row(skillset, primary, base):
    return skillset - 5 if 5 <= skillset < 24 else 0 if skillset in (primary, base) else None


def charge(raw, monster_skill=False, defend=False, persevere=False):
    return raw if persevere else raw // 2 if monster_skill else 0 if defend else raw


def mp_term(mp, maximum, minimum, share):
    if not maximum:
        return 0
    amount = mp * 64 // maximum
    if mp < minimum:
        amount //= 2
    return amount * share // 4


def validate():
    assert known_row(11, 11, 5) == 6
    assert known_row(23, 23, 5) == 18
    assert known_row(74, 74, 5) == 0
    assert known_row(74, 11, 5) is None  # Source treats an unowned special skillset as known.
    assert charge(5, True, True) == 2
    assert charge(5, False, True) == 0
    assert charge(5, True, True, True) == 5
    assert mp_term(6, 75, 6, 1) == 1
    assert mp_term(5, 75, 6, 1) == 0
    assert mp_term(69, 75, 6, 1) == 14
    assert mp_term(27, 75, 48, 4) == 11
    # Full/full HP: bare equipment cancels, enemy damage54 yields +35.
    assert (100*128//100+357+mp_term(69,75,6,1))-(146*128//200+357) == 49
    return dict(learningDescriptions=4, chargeDescriptions=3, mpDescriptions=4, scoreDescriptions=1)


def validate_inspection(report, selection):
    expected = {'fire':[16], 'elemental':list(range(16,28)), 'learning':[16,19]}[selection]
    if not report['eachCaseRestartsFromSameStateAndRng'] or report['cExecuted'] or report['sourceOriginalExecuted']:
        raise ValueError('Independent hypothesis identity')
    if [r['OriginalAbilityId'] for r in report['cases']] != expected:
        raise ValueError('Prepared source slot order')
    share = len(expected)*4//(len(expected)+2)
    for row in report['cases']:
        original = row['OriginalAbilityId']; tier=(original-16)%4
        power=[14,18,24,32][tier];cost=[6,12,24,48][tier]
        if original==21:cost=10
        damage=power*8*70*70//10000
        priority=128+mp_term(75-cost,75,6,share)-(200-damage)*128//200
        if row['sourceSlot']!=original-16 or row['disposition']!='Scored' or row['Score']['Priority']!=priority:
            raise ValueError('Independent score '+str(original))
        if [(u['Hp'],u['Mp']) for u in row['units']] != [(100,75-cost),(200-damage,75 if selection=='learning' else 50)]:
            raise ValueError('HP/MP '+str(original))
        if len(row['frames'])!=1 or row['frames'][0]['Result']['HpDamage']!=damage or row['frames'][0]['Result']['Effects']['AddedEffect']:
            raise ValueError('AI formula '+str(original))
        state=row['entryRng']['Value']
        for _ in range(4):state=(state*1103515245+12345)&0xffffffff
        if row['exitRng']['Value']!=state or row['exitRng']['DrawCount']-row['entryRng']['DrawCount']!=4:
            raise ValueError('Four real AI evasion draws '+str(original))
    return dict(preparedAiHypotheses=len(expected),selection=selection,independentScoresMatched=True)


def audit(reference, executable=None, inspection=None, selection='fire'):
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() != REVISION:
        raise ValueError('Reference revision')
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip():
        raise ValueError('Reference must be clean')
    evidence = json.loads((ROOT/'docs/evidence/ai/ordinary-spell-ai-source-v1.json').read_text(encoding='utf-8'))
    for row in evidence['sourceFiles']:
        if hashlib.sha256((reference/row['path']).read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Source fingerprint: '+row['path'])
    if executable:
        data=executable.read_bytes()
        if hashlib.sha256(data).hexdigest()!=evidence['preparedInputSha256']:raise ValueError('Prepared edition')
        for row in evidence['structuralFrogRecords']:
            offset=row['address']-0x80010000+0x800+row['stride']*row['index']
            if data[offset:offset+row['stride']].hex()!=row['hex']:raise ValueError('Structural Frog record')
    checked={} if inspection is None else validate_inspection(json.loads(inspection.read_text(encoding='utf-8-sig')),selection)
    return dict(validate(), referenceRevision=REVISION, sourceFiles=len(evidence['sourceFiles']),
                structuralPreparedRecords=0 if executable is None else len(evidence['structuralFrogRecords']),
                originalExecuted=False, cExecuted=False, chargeClockSearchImplemented=False,**checked)


if __name__ == '__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    p.add_argument('--prepared-executable',type=Path);p.add_argument('--inspection',type=Path)
    p.add_argument('--selection',choices=['fire','elemental','learning'],default='fire')
    args=p.parse_args();result=audit(args.reference,args.prepared_executable,args.inspection,args.selection)
    if args.output:
        with args.output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
