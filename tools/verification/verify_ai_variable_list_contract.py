"""ZOD-264 isolated variable-list descriptions; no original, C or managed AI execution."""
import argparse
import json
import subprocess
from pathlib import Path

from verify_ai_rank_admission_contract import insert_best
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/variable-list-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-variable-list-contract-v1.json'
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, 'Integer description domain')
    return value


def ordinary_list(entries):
    require(isinstance(entries, list) and 2 <= len(entries) <= 34 and entries[0] == 'Attack'
            and entries[-1] == 'FrogAttack' and all(e in ('Accumulate', 'ThrowStone') for e in entries[1:-1]),
            'Bounded ordinary source list, preserving occurrences')
    return entries


def summary(entries):
    ordinary_list(entries)
    return dict(initialized=len(entries), evadeQuarter=(1 + entries.count('ThrowStone')) * 4 // len(entries),
                harmful=1 + entries.count('ThrowStone'), helpful=entries.count('Accumulate'),
                ranged='ThrowStone' in entries)


def describe(kind, data):
    if kind == 'preparation':
        actor = summary(data['actor']); peer = summary(data['peer']); move = integer(data['move'], 0, 124)
        require(type(data['actionTaken']) is bool, 'Action-used fact')
        share = actor['helpful'] * 128 // (actor['helpful'] + actor['harmful'])
        return dict(actor=actor, peer=peer, actorHelpfulShare=share, firstPassMostlySupport=share >= 77,
                    finalActorMostlySupport=False, peerMostlySupport=None, enemyHarmful=peer['harmful'],
                    enemyPhysical=peer['harmful'], enemyReach=move + 1, allyReach=move,
                    rangedDistanceFloor=3 if actor['ranged'] else None, randomUseThreshold=0,
                    evaluatedActions=[] if data['actionTaken'] else data['actor'][:-1])
    if kind == 'cursor':
        entries = ordinary_list(data['entries']); spent = data['actionTaken']; resume = data['resuming']
        require(type(spent) is bool and type(resume) is bool and type(data['suspendCurrent']) is bool, 'Cursor facts')
        start = integer(data['startIndex'], 0, len(entries) - 1)
        require(resume or start == 0, 'Fresh cursor starts at zero')
        command = data['currentCommand']; require(command in ('None', 'Attack', 'Accumulate', 'ThrowStone'), 'Supplied current command')
        # This resume description starts inside an already-entered outcome, after its fresh-entry gates/filter.
        require(not resume or not spent and entries[start] != 'FrogAttack' and command == entries[start], 'Supplied inside-outcome resume')
        loaded, completed, usable, self_passes = [], [], [], []
        for i in range(start, len(entries)):
            if not (resume and i == start):
                loaded.append(i)
            if i == start and data['suspendCurrent']:
                return dict(loaded=loaded, completed=completed, usable=usable, selfPasses=self_passes,
                            cursor=i, suspended=True, terminalCommand=command)
            if not spent and entries[i] != 'FrogAttack':
                command = entries[i]; usable.append(i)
                if command == 'Accumulate' and not (resume and i == start):
                    self_passes.append(i)
            completed.append(i)
        return dict(loaded=loaded, completed=completed, usable=usable, selfPasses=self_passes,
                    cursor=len(entries), suspended=False, terminalCommand=command)
    if kind == 'rankTape':
        # Candidate ranks and lottery raws are supplied, not results of executing scans, formulas or clocks.
        baseline = dict(decided=0, priority=integer(data['baselinePriority'], -320, 320), hit=100)
        ranked = [dict(decided=0, priority=-32767, hit=0, proximity=0, payload=None) for _ in range(8)]
        inserted, ids = [], []
        for candidate in data['candidates']:
            require(isinstance(candidate['id'], str) and bool(candidate['id']) and candidate['id'] not in ids, 'Unique occurrence label')
            ids.append(candidate['id'])
            require(candidate['kind'] in ('Attack', 'Accumulate', 'ThrowStone'), 'Supplied action kind')
            best = dict(decided=0, priority=integer(candidate['priority'], -320, 320),
                        hit=integer(candidate['hit'], 1, 100), proximity=integer(candidate['proximity'], 0, 10), payload=candidate['id'])
            result = insert_best(best, baseline, ranked); ranked = result['ranked']; inserted.append(result['insertedIndex'])
        require(len(data['raws']) == 8, 'Eight supplied lottery raws')
        for raw in data['raws']:
            integer(raw, 0, 32767)
        any_hit = False; selected = None; draws = 0; reason = 'MovementFallback'
        for i, slot in enumerate(ranked):
            draws += 1; any_hit |= slot['hit'] != 0
            if data['raws'][i] % 100 < slot['hit']:
                selected = slot['payload']; reason = 'FirstRoll'; break
        else:
            if any_hit:
                selected = ranked[0]['payload']; reason = 'AllRollsFailedUseFirst'
        return dict(ranked=[r['payload'] for r in ranked if r['hit']], insertionIndices=inserted,
                    selected=selected, lotteryDraws=draws, reason=reason, additionalRankDraws=0)
    raise ValueError('Unknown description kind')


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['schemaVersion'] == 1 and fixture['ticket'] == evidence['ticket'] == 'ZOD-264'
            and fixture['referenceRevision'] == evidence['referenceRevision'] == REVISION
            and fixture['productionImplemented'] is False and fixture['kind'] == 'isolated-source-descriptions',
            'Source-only variable-list receipt')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['kind'], row['input']) == row['expected'], 'Variable-list description: ' + row['id'])
        ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique descriptions')
    return dict(variableListDescriptions=len(ids), originalExecuted=False, cExecuted=False, managedExecuted=False)


def audit(reference):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Pinned reference')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean read-only reference')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(all(row[k] == v for k, v in actual.items()), 'Source function: ' + row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))), referenceRevision=REVISION, referenceClean=True,
                sourceFiles=len(evidence['sourceFiles']), sourceFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True); parser.add_argument('--output', type=Path)
    args = parser.parse_args(); report = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
