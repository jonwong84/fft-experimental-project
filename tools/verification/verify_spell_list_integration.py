"""ZOD-442 source-only list/caller/rank descriptions; never executes C, C# or FFT."""
import argparse
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/spell-list-integration-v1.json'
RECEIPT = ROOT / 'docs/evidence/ai/spell-list-integration-source-v1.json'


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, 'Explicit source numeric width')
    return value


def boolean(value):
    require(type(value) is bool, 'Explicit boolean fact')
    return value


def human_list(data):
    require(boolean(data['cleanDefaultHuman']), 'Clean ordinary human description only')
    maximum = integer(data['maxMp'], 0, 65535)
    half = boolean(data['halfMp'])
    entries = ['attack']
    for name in ('primary', 'secondary'):
        slots = data[name]
        require(len({s[0] for s in slots}) == len(slots), 'Distinct skillset slots')
        for slot, ability, known, usable, cost in sorted(slots):
            integer(slot, 0, 15); integer(ability, 0, 367); integer(cost, 0, 255)
            boolean(known); boolean(usable)
            if ability and known and usable and maximum >= (cost // 2 if half else cost):
                entries.append(f'{name}:{slot}:{ability}')
    entries.append('frog')
    require(len(entries) <= 34, 'Original human list packing')
    return dict(entries=entries, terminatorWritten=len(entries) < 34)


def walk(data):
    loaded, eligible = [], []
    termination = 'capacity'
    for skillset, packed, usable in data['rows'][:34]:
        integer(skillset, 0, 255)
        if skillset == 255:
            termination = 'sentinel'
            break
        integer(packed, 0, (20 << 10) + 1023); boolean(usable)
        loaded.append([skillset, packed & 1023, packed >> 10])
        eligible.append(usable)
    require(termination == 'sentinel' or len(loaded) == 34, 'Complete source list description')
    # ability_list_evaluate calls outcome even when its entry is unusable; outcome's sign gate skips search.
    return dict(loaded=loaded, eligible=eligible, outcomeCalls=len(loaded), termination=termination)


def usage(data):
    flags = integer(data['raw'], 0, 255) & 0xf8
    radius = integer(data['range'], 0, 255) + integer(data['area'], 0, 255)
    if not boolean(data['trajectory']):
        if boolean(data['exhaustiveShape']) or radius < 3 and boolean(data['secondaryEvadeable']):
            flags |= 4
        elif boolean(data['cannotHitCaster']) or boolean(data['canHitEnemies']) or data['area'] == 0:
            flags |= 2
    initialized = flags
    flags &= ~128
    if boolean(data['usableNow']):
        flags |= 128
        if flags & 2:
            flags |= 1
        elif not flags & 4:
            ct = integer(data['ct'], 0, 255)
            delay = integer(data['actorDelay'], 0, 255)
            if ct >= delay or boolean(data['selfEffectNone']):
                flags |= 1
            else:
                flags &= ~1
        if boolean(data['actionTaken']):
            flags &= ~128
    return dict(initialized=initialized, completed=flags)


def general(data):
    events = ['scenarios:1', 'baseline:0', 'keep-charging:1']
    scenarios = []
    fallback = None
    if not boolean(data['chargingSelected']):
        events.append('targets:any:1')
        if boolean(data['hasTargets']):
            events.append('list:1')
            scenarios.append(1)
        events.append('lottery')
        if not boolean(data['lotterySelected']):
            events.append('fallback:0')
            fallback = 0
    return dict(events=events, listScenarios=scenarios, baselineScenario=0,
                fallbackScenario=fallback, scenarioBuilds=1)


def rank(action):
    return (integer(action[1], 0, 255), integer(action[2], -32768, 32767), integer(action[3], 0, 255))


def insert(data):
    baseline = tuple(data['baseline'])
    require(len(baseline) == 3, 'Rank triple')
    rank(['baseline', *baseline, 0])
    slots = [None] * 8
    indices = []
    for candidate in data['bests']:
        require(len(candidate) == 5 and isinstance(candidate[0], str) and candidate[0], 'Named best description')
        key = rank(candidate)
        integer(candidate[4], 0, 255)

        def higher(previous):
            prior = (0, -32767, 0) if previous is None else rank(previous)
            if key != prior:
                return key > prior
            require(previous is not None, 'Reset-coordinate tie needs retained payload evidence')
            return candidate[4] < previous[4]

        index = None
        if key > baseline and higher(slots[-1]):
            index = next(i for i, previous in enumerate(slots) if higher(previous))
            slots.insert(index, candidate)
            slots.pop()
        indices.append(index)
    # ai_best_rank_insert resets best after every call, including failed gates. No RNG is called here.
    return dict(slots=[None if s is None else s[0] for s in slots], indices=indices,
                bestResets=len(indices), insertionDraws=0)


DESCRIPTIONS = dict(lists=human_list, walks=walk, usages=usage, general=general, insertions=insert)


def validate(document):
    require(document['schemaVersion'] == 1 and document['referenceRevision'] == REVISION and
            document['originalExecuted'] is False and document['cExecuted'] is False and
            document['runtimeImplemented'] is False, 'Pinned source-only provenance')
    counts = {}
    for name, evaluate in DESCRIPTIONS.items():
        rows = document[name]
        require(rows and len({r['id'] for r in rows}) == len(rows), 'Distinct source descriptions')
        for row in rows:
            require(evaluate(row['input']) == row['expected'], 'Source description: ' + row['id'])
        counts[name] = len(rows)
    return dict(descriptions=counts, total=sum(counts.values()), originalExecuted=False,
                cExecuted=False, runtimeImplemented=False)


def audit(reference):
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Pinned revision')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean reference')
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
    require(receipt['referenceRevision'] == REVISION, 'Receipt revision')
    for row in receipt['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source file ' + row['path'])
    for row in receipt['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol']) ==
                {k: v for k, v in row.items() if k != 'path'}, 'Source function ' + row['symbol'])
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))), referenceRevision=REVISION,
                sourceFiles=len(receipt['sourceFiles']), sourceFunctions=len(receipt['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
