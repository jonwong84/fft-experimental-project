"""Audit repeated authored Accumulate GPU captures and diagnostic command receipts."""
import argparse
import copy
import hashlib
import html
import json
from pathlib import Path
from PIL import Image

CAPTURES = ('start', 'ready', 'boosted', 'duplicate-rejected', 'inactive-selection',
            'inactive-rejected', 'moved', 'no-grant', 'damage', 'cap-reached',
            'cap-ready', 'cap-used', 'finished', 'terminal-rejected', 'restarted')


def require(value, message):
    if not value:
        raise ValueError(message)


def validate_transition(before, step):
    after = step['state']
    if step['accepted'] is False:
        require(after == before and step['events'] == [], 'Rejected command changed state/events')
    command = step['command']
    if step['accepted'] is not True or not command or 'AbilityId' not in command:
        return False
    require(command['ActorId']['Value'] == 'sample:unit/first', 'Wrong granted actor')
    expected = copy.deepcopy(before)
    old_pa = expected['Combatants'][0]['PhysicalAttack']
    expected['Combatants'][0]['PhysicalAttack'] = min(99, old_pa + 1)
    require(expected['Turns']['Units'][0]['ActionTaken'] is False, 'Act was already consumed')
    expected['Turns']['Units'][0]['ActionTaken'] = True
    require(after == expected, 'Ability changed unrelated state/RNG or wrong PA/Act')
    require(any('ImmediateAbilityResolvedEvent' in event for event in step['events']), 'Missing resolution event')
    require(any('AttackPhysicalAttackChangedEvent' in event for event in step['events']) == (old_pa < 99),
            'Wrong PA-change event at cap')
    return True


def verify(prepared, prepared_repeat, first, repeat):
    inventories = []
    for root in (prepared, prepared_repeat):
        files = {p.name: p.read_bytes() for p in root.iterdir() if p.is_file()}
        require(set(files) == {'assets.manifest.json', 'content.package.json', 'ability.package.json'}, 'Prepared inventory')
        manifest = json.loads(files['assets.manifest.json'])
        require(len(manifest['assets']) == 2, 'Prepared asset count')
        for asset in manifest['assets']:
            data = files[asset['relativePath']]
            require(len(data) == asset['byteLength'] and hashlib.sha256(data).hexdigest() == asset['sha256'], 'Prepared hash/length')
        inventories.append(files)
    require(inventories[0] == inventories[1], 'Preparations differ')
    metadata = hashlib.sha256(inventories[0]['content.package.json']).hexdigest()
    effects = hashlib.sha256(inventories[0]['ability.package.json']).hexdigest()
    native = []
    for root in (first, repeat):
        files = {p.name: p.read_bytes() for p in root.iterdir() if p.is_file()}
        require(set(files) == {name + '.png' for name in CAPTURES} | {'check.json'}, 'Native inventory')
        report = json.loads(files['check.json'])
        require(report['format'] == 'fft.accumulate-window-check' and report['schemaVersion'] == 1, 'Report format')
        require(report['metadataSha256'] == metadata and report['effectsSha256'] == effects, 'Prepared binding')
        require(all(report[key] is True for key in ('headlessMatched', 'replayMatched', 'resizeChecked', 'renderingPreservedState')), 'Missing native check')
        require(len(report['steps']) == 560 and report['frames'] == 576 and report['backbuffer'] == [1280, 720], 'Finite route/resize')
        before = report['final']
        boosts = rejects = 0
        captures = {}
        for index, step in enumerate(report['steps'], 1):
            require(step['step'] == index, 'Step ordering')
            boosts += validate_transition(before, step)
            rejects += step['accepted'] is False
            if step['Capture']:
                require(step['Capture'] not in captures, 'Duplicate capture')
                captures[step['Capture']] = step
            before = step['state']
        require(boosts == 95 and rejects == 4, 'Ability/rejection count')
        require(captures['boosted.png']['state']['Combatants'][0]['PhysicalAttack'] == 6, 'First boost')
        require(captures['damage.png']['state']['Combatants'][1]['Hp'] == 26, 'Boosted sword damage')
        require(captures['cap-ready.png']['available'] is True and captures['cap-used.png']['available'] is False, 'Cap availability')
        require(captures['cap-used.png']['Status'].startswith('ACCUMULATE / PA 99 MAX'), 'Cap feedback')
        require(captures['finished.png']['state']['Outcome'] != 0, 'Missing completion')
        require(captures['restarted.png']['Restart'] and before == report['final'] and
                before['Combatants'][0]['PhysicalAttack'] == 5 and before['RngState']['DrawCount'] == 0, 'Restart baseline')
        for name in CAPTURES:
            with Image.open(root / (name + '.png')) as image:
                image.load()
                require(image.size == (960, 640), 'Capture dimensions')
        require(files['start.png'] == files['restarted.png'], 'Restart presentation differs')
        native.append(files)
    require(native[0] == native[1], 'Repeated native evidence differs')
    return dict(format='fft.accumulate-window-audit', schemaVersion=1, passed=True,
                steps=560, acceptedAbilityUses=95, atomicRejections=4, captures=15,
                preparedFiles=3, nativeFiles=16, repeatedBytesEqual=True,
                files={name: hashlib.sha256(data).hexdigest() for name, data in sorted(native[0].items())},
                limits='Authored GPU/command evidence; physical input and owner approval pending; original equivalence unestablished')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('prepared', 'prepared-repeat', 'first', 'repeat', 'output'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    result = verify(args.prepared, args.prepared_repeat, args.first, args.repeat)
    args.output.mkdir(exist_ok=False, parents=True)
    (args.output / 'audit.json').write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    cards = ''.join(f'<figure><figcaption>{html.escape(name)}</figcaption><img src="{(args.first / (name + ".png")).resolve().as_uri()}"></figure>' for name in CAPTURES)
    (args.output / 'review.html').write_text('<!doctype html><meta charset="utf-8"><title>Accumulate review</title><style>body{background:#101722;color:#ddd;font:18px system-ui;margin:24px}figure{margin:24px 0}img{max-width:100%;border:1px solid #456}</style><h1>Accumulate controls</h1><p>Authored test battle. Programmed input passed; owner visual and physical-input review pending.</p>' + cards, encoding='utf-8')
    print('PASS: repeated prepared/native bytes, 560 steps, 95 uses, 4 atomic rejections, 15 captures')


if __name__ == '__main__':
    main()
