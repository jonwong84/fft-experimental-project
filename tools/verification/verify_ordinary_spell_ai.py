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


def audit(reference):
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() != REVISION:
        raise ValueError('Reference revision')
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip():
        raise ValueError('Reference must be clean')
    evidence = json.loads((ROOT/'docs/evidence/ai/ordinary-spell-ai-source-v1.json').read_text(encoding='utf-8'))
    for row in evidence['sourceFiles']:
        if hashlib.sha256((reference/row['path']).read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Source fingerprint: '+row['path'])
    return dict(validate(), referenceRevision=REVISION, sourceFiles=len(evidence['sourceFiles']),
                originalExecuted=False, cExecuted=False, chargeClockSearchImplemented=False)


if __name__ == '__main__':
    p=argparse.ArgumentParser();p.add_argument('--reference',type=Path,required=True);p.add_argument('--output',type=Path)
    args=p.parse_args();result=audit(args.reference)
    if args.output:
        with args.output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
