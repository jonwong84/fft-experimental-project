"""Independent audit of ZOD-111 authored prepared bundles and finite GPU receipts."""
import argparse
import copy
import html
import json
from pathlib import Path
from PIL import Image
from verify_basic_skill_content import draws, inventory, require, sha, validate_stone
from verify_accumulate_window import validate_transition as validate_self

CAPTURES = ('start', 'not-ready', 'ready', 'range', 'cancelled', 'self-rejected',
            'inactive-rejected', 'empty-target', 'out-of-range', 'range-restarted',
            'boosted', 'stone-act-used', 'act-used', 'moved', 'no-grant',
            *(f'stone-{n}' for n in range(1, 7)),
            *(f'terminal-{n}' for n in range(1, 7)), 'restarted')


def validate_step(before, step, initial):
    after = step['state']
    require(after['Turns']['RngState'] == after['RngState'], 'RNG copies differ')
    if step['Restart']:
        require(step['command'] is None and after == initial, 'Restart did not restore baseline')
        return None
    if step['accepted'] is not True:
        require(after == before, 'Selection/rejection changed battle state')
        require(step['events'] == ([] if step['accepted'] is False else None), 'Non-command/rejection emitted events')
        return None
    kind = step['kind']
    if kind == 'UseTargetedAbility':
        require(step['command']['ActorId']['Value'] == 'sample:unit/first' and
                step['command']['TargetId']['Value'] == 'sample:unit/second', 'Wrong stone actor/target')
        result = validate_stone(before, after)
        require(result['damage'] in (6, 12), 'Boosted damage differs')
        require(any('ThrowStoneResolvedEvent' in event for event in step['events']), 'Missing stone event')
        require(any('ThrowStoneKnockbackEvent' in event for event in step['events']) == result['moved'], 'Push event differs')
        return result
    if kind == 'UseAbility':
        require(validate_self(before, step), 'Missing self effect')
    else:
        require(kind in ('Advance', 'Move', 'Wait'), 'Unexpected accepted command kind')
        require(after['RngState'] == draws(before['RngState'], 1 if kind == 'Advance' else 0)[0], 'Unexpected readiness/nonrandom draws')
    return None


def validate_availability(step):
    state = step['state']
    selected = step['SelectedUnit']
    expected_range = []
    if selected is not None:
        origin = next(unit['Position'] for unit in state['units'] if unit['Id'] == selected)
        expected_range = [dict(X=x, Y=y, Layer=0) for y in range(5) for x in range(7)
                          if 1 <= abs(x-origin['X']) + abs(y-origin['Y']) <= 4]
    require(step['range'] == expected_range, 'Geometric range differs')
    a, b = state['units']
    distance = abs(a['Position']['X']-b['Position']['X']) + abs(a['Position']['Y']-b['Position']['Y'])
    eligible = (state['Outcome'] == 0 and selected == a['Id'] and state['Turns']['ActiveUnitId'] == a['Id']
                and not state['Turns']['Units'][0]['ActionTaken'])
    targeted = eligible and 1 <= distance <= 4 and state['Combatants'][1]['Hp'] > 0
    require(step['targetedAvailable'] == targeted and step['targets'] == ([b['Id']] if targeted else []), 'Target availability differs')
    require(step['available'] == eligible, 'Self availability differs')


def verify(prepared, prepared_repeat, first, repeat):
    bundles = [inventory(root) for root in (prepared, prepared_repeat)]
    require(bundles[0] == bundles[1] and set(bundles[0]) == {
        'assets.manifest.json', 'content.package.json', 'ability.package.json', 'targeted.package.json'}, 'Prepared repeat/inventory')
    manifest = json.loads(bundles[0]['assets.manifest.json'])
    require(len(manifest['assets']) == 3, 'Prepared asset count')
    for asset in manifest['assets']:
        data = bundles[0][asset['relativePath']]
        require(len(data) == asset['byteLength'] and sha(data) == asset['sha256'], 'Prepared binding')
    native = [inventory(root) for root in (first, repeat)]
    require(native[0] == native[1], 'Repeated native bytes differ')
    for root, files in zip((first, repeat), native):
        require(set(files) == {name+'.png' for name in CAPTURES} | {'check.json'}, 'Native inventory')
        report = json.loads(files['check.json'])
        require(report['format'] == 'fft.basic-skill-window-check' and report['schemaVersion'] == 1, 'Report format')
        for key, name in (('metadataSha256', 'content.package.json'), ('effectsSha256', 'ability.package.json'), ('targetedSha256', 'targeted.package.json')):
            require(report[key] == sha(bundles[0][name]), 'Report/prepared hash binding')
        require(all(report[key] is True for key in ('headlessMatched', 'replayMatched', 'resizeChecked', 'renderingPreservedState', 'focusBaselineChecked')), 'Missing checks')
        require(report['journalReplays'] == 2 and 50 <= len(report['steps']) <= 300 and
                report['frames'] == len(report['steps']) + 16 and report['backbuffer'] == [1280, 720], 'Finite budget/resize')
        initial = report['final']
        require(initial['Outcome'] == 0 and initial['RngState'] == dict(Value=1, DrawCount=0) and
                [unit['PhysicalAttack'] for unit in initial['Combatants']] == [5, 5], 'Initial baseline')
        before = initial
        hits = pushes = rejected = replays = 0
        captures = {}
        for index, step in enumerate(report['steps'], 1):
            require(step['step'] == index, 'Step ordering')
            result = validate_step(before, step, initial)
            validate_availability(step)
            if result:
                hits += 1
                pushes += result['moved']
            rejected += step['accepted'] is False
            replays += step['Restart']
            if step['Capture']:
                require(step['Capture'] not in captures, 'Duplicate capture')
                captures[step['Capture']] = step
            before = step['state']
        require((hits, pushes, rejected, replays) == (6, 3, 13, 2), 'Route coverage')
        require(set(captures) == {name+'.png' for name in CAPTURES if name != 'start'}, 'Capture route inventory')
        require(captures['stone-6.png']['state']['Outcome'] == 1 and
                captures['stone-6.png']['state']['RngState'] == dict(Value=4200531631, DrawCount=58), 'KO/RNG vector')
        require('OutOfRange' in captures['out-of-range.png']['rejection'], 'Range rejection')
        require(captures['cancelled.png']['targetedAvailable'] and 'NO TARGET' in captures['empty-target.png']['Status'], 'Cancel/empty feedback')
        require(any('NO PUSH' in captures[f'stone-{n}.png']['Status'] for n in range(1, 6)), 'Missing living no-push feedback')
        require(before == initial and files['start.png'] == files['restarted.png'] == files['range-restarted.png'], 'Restart state/presentation')
        require(sha(json.dumps(initial, separators=(',', ':'), ensure_ascii=False).encode()) == report['finalSha256'], 'Final state hash')
        for name in CAPTURES:
            with Image.open(root/(name+'.png')) as image:
                image.load()
                require(image.size == (960, 640), 'GPU capture dimensions')
    return dict(format='fft.basic-skill-window-audit', schemaVersion=1, passed=True,
                steps=len(report['steps']), captures=len(CAPTURES), preparedFiles=4, nativeFiles=len(native[0]),
                throwStoneHits=hits, pushes=pushes, atomicRejections=rejected, journalReplays=replays,
                repeatedBytesEqual=True, files={name: sha(data) for name, data in sorted(native[0].items())},
                limits='Programmed authored GPU routes; physical input, owner approval and original equivalence unestablished')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('prepared', 'prepared-repeat', 'first', 'repeat', 'output'):
        parser.add_argument('--'+name, type=Path, required=True)
    args = parser.parse_args()
    result = verify(args.prepared, args.prepared_repeat, args.first, args.repeat)
    args.output.mkdir(exist_ok=False, parents=True)
    (args.output/'audit.json').write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
    cards = ''.join(f'<figure><figcaption>{html.escape(name)}</figcaption><img src="{(args.first/(name+".png")).resolve().as_uri()}"></figure>' for name in CAPTURES)
    (args.output/'review.html').write_text('<!doctype html><meta charset="utf-8"><title>Basic Skill review</title><style>body{background:#101722;color:#ddd;font:18px system-ui;margin:24px}figure{margin:24px 0}img{max-width:100%;border:1px solid #456}</style><h1>Basic Skill controls</h1><p>Authored battle; owner visual and physical-input review pending.</p>'+cards, encoding='utf-8')
    print(f"PASS: {result['steps']} steps, {result['captures']} GPU captures, repeated bundles/native bytes and independent state/RNG audit")


if __name__ == '__main__':
    main()
