"""ZOD-154 pinned source and supplied-prefix RNG tapes; no fresh AI, C# or C execution."""
import argparse
import json
from pathlib import Path

from verify_ai_composition_contract import source_schedule
from verify_ai_selection_contract import lottery_tail
from verify_ai_wait_facing_contract import audit as audit_wait
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, resolve

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-execution-rng-contract-v1.json'
EVIDENCE = ROOT / 'docs/evidence/ai/execution-rng-contract-v1.json'


def command_stage(decided, previous_moved, previous_acted, moved, acted):
    """Source guards only; no admission, decision computation or command execution."""
    if decided and previous_moved == moved and previous_acted == acted:
        return 'EndUnchanged'
    return 'Decide' if not decided or acted else 'Reuse'


def dispatch(moved, acted, act_before_move, same_tile, skillset):
    """Source post-selection branch only, assuming an admitted ordinary action."""
    if not moved and (acted or not act_before_move) and not same_tile:
        return 'Move'
    return 'EndTurn' if acted or not skillset else 'Act'


def tape(vector):
    """Draw costs are supplied by earlier source/managed receipts, not evaluated here."""
    entry = vector['entry']
    prefix = source_schedule(entry['word'], entry['count'], vector['movementCells'], vector['projectedAttackCosts'])
    require(prefix['accepted'], 'Supplied prefix exhausted')
    selected = lottery_tail(prefix['rngWord'], prefix['rngCount'], vector['hitPercent'])
    require(selected['accepted'], 'Supplied lottery exhausted')
    decision = dict(word=selected['rngWord'], count=selected['rngCount'],
                    lotteryDraws=selected['outcome']['draws'], reason=selected['outcome']['reason'])
    actual = None
    if vector['actualClassEvade'] is not None:
        result = resolve(dict(seed=decision['word'], drawCount=decision['count'], pa=5, power=5, hp=50,
            classEvade=vector['actualClassEvade'], actor=[2, 1], target=[1, 1], mapSize=[5, 3],
            actorFacing='East', targetFacing='East', actorBrave=70, targetBrave=70))
        actual = dict(word=result['rngWord'], count=result['drawCount'], hit=result['hit'],
                      damage=result['damage'], hp=result['hp'], raw=result['raw'])
    return dict(decision=decision, actual=actual)


def audit(reference):
    report = audit_wait(reference, settlement=True)
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Execution reference revision')
    for entry in evidence['additionalSourceFiles']:
        require(fingerprint(reference, entry['path']) == entry, 'Source mismatch: ' + entry['path'])
    for entry in evidence['additionalSourceFunctions']:
        actual = function_fingerprint(reference / entry['path'], entry['symbol'])
        require(actual['normalizedLfSha256'] == entry['normalizedLfSha256'], 'Function mismatch: ' + entry['symbol'])
    fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
    require(fixture['referenceRevision'] == report['referenceRevision'], 'Tape reference revision')
    for vector in fixture['vectors']:
        require(tape(vector) == dict(decision=vector['expectedDecision'], actual=vector['expectedActual']), 'Tape mismatch: ' + vector['id'])
    return dict(**report, executionFiles=len(evidence['additionalSourceFiles']),
                executionFunctions=len(evidence['additionalSourceFunctions']), suppliedRngTapes=len(fixture['vectors']))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))


if __name__ == '__main__':
    main()
