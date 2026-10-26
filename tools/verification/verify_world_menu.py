"""Audit finite authored Windows-menu checks. No original-game equivalence claim."""
import argparse
import hashlib
import html
import json
from pathlib import Path
from PIL import Image

CAPTURES = ('home', 'new', 'hill', 'party', 'home-retained', 'confirm-new', 'new-harbor',
            'wide', 'wide-travel', 'wide-party', 'close-callback', 'tall', 'closed')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def validate_checkpoint(state, funds, elapsed, location):
    require(state is not None, 'Missing checkpoint')
    require(state['Funds'] == funds and state['ElapsedSeconds'] == elapsed and
            state['LocationId']['Value'] == 'sample:location/' + location, 'Wrong checkpoint continuation')
    require(state['Date'] == {'Month': 3, 'Day': 14} and state['Progress'] is True, 'Date/progress changed')
    require(state['Party'] == [
        dict(Id={'Value': 'sample:party/ada'}, JobId={'Value': 'sample:job/courier'}, EquippedItemId={'Value': 'sample:item/training-blade'}),
        dict(Id={'Value': 'sample:party/borin'}, JobId={'Value': 'sample:job/courier'}, EquippedItemId=None)], 'Party changed')
    require(state['Inventory'] == [dict(ItemId={'Value': 'sample:item/rations'}, Quantity=5),
                                   dict(ItemId={'Value': 'sample:item/training-blade'}, Quantity=2)], 'Inventory changed')
    require(state['Definition']['RouteId']['Value'] == 'sample:route/world-idle' and
            state['Definition']['ProgressFlagId']['Value'] == 'sample:progress/bridge-open', 'Definition binding changed')


def validate_image(path, expected_hash):
    with Image.open(path) as source:
        require(source.size == (960, 640), 'Wrong menu image size')
        image = source.convert('RGBA')
        require(hashlib.sha256(image.tobytes()).hexdigest() == expected_hash, 'GPU PNG/CPU digest differs')
        require(image.getchannel('A').getextrema() == (255, 255), 'Menu image is not opaque')
        require(image.getpixel((0, 0)) == (14, 21, 34, 255) and image.getpixel((32, 32)) == (82, 202, 232, 255), 'Authored palette signature differs')


def verify(first, repeat):
    inventories = []
    # Screen/focus expectations are authored independently of the C# reducer.
    expected = {'home': (0, 0), 'new': (1, 3), 'hill': (1, 3), 'party': (2, 6),
                'home-retained': (0, 1), 'confirm-new': (3, 7), 'cancel-new': (0, 0),
                'new-again': (3, 7), 'accept-new-focus': (3, 8), 'new-harbor': (1, 3),
                'wide': (1, 3), 'wide-travel': (1, 3), 'wide-party': (2, 6), 'close-callback': (4, 7),
                'cancel-close': (2, 6), 'tall': (2, 6), 'pointer-back': (1, 4), 'home-back': (0, 1),
                'exit-prompt': (4, 7), 'exit-focus': (4, 8), 'closed': (5, None)}
    suppressed = {'held-confirm', 'connect-held', 'connected-held', 'controller-held', 'inactive', 'reactivated', 'reactivated-held'}
    for directory in (first, repeat):
        files = {p.relative_to(directory).as_posix(): p.read_bytes() for p in directory.rglob('*') if p.is_file()}
        require(set(files) == {name + '.png' for name in CAPTURES} | {'trace.json', 'captures.json', 'check.json'}, 'Unexpected/missing output or save files')
        report = json.loads(files['check.json'])
        require(report['format'] == 'fft.windows-world-navigation-check' and report['schemaVersion'] == 1 and report['passed'] is True,
                'Failed or wrong check report')
        require(report['frames'] == report['steps'] == 51 and report['captures'] == 13 and report['physicalInputVerified'] is False,
                'Unexpected finite check budget or physical-input claim')
        require(all(report[key] is True for key in ('cpuGpuMatched', 'headlessMatched', 'statePreservedByRendering')), 'Missing native check')
        trace = json.loads(files['trace.json']); require(len(trace) == 51, 'Unexpected sample count')
        visited = set(); travel_count = 0; funds = 100; elapsed = 42; location = 'harbor'; prior = None
        for row in trace:
            name, view = row['Name'], row['view']
            if name in expected:
                require((view['Screen'], view['Focus']) == expected[name], 'Wrong screen/focus: ' + name)
                visited.add(name)
            if name in suppressed or name == 'release':
                require(row['command'] is None and row['result'] is None and view == prior, 'Held/release sample changed view: ' + name)
            if name in ('new', 'new-harbor'):
                funds, elapsed, location = 100, 42, 'harbor'
            if name in ('hill', 'wide-travel'):
                funds, elapsed, location = 93, 53, 'hill'; travel_count += 1
                result = row['result']['Travel']
                require(result['Accepted'] is True and result['Rejection'] == 0 and result['Events'] == [dict(
                    From={'Value': 'sample:location/harbor'}, To={'Value': 'sample:location/hill'}, FundsSpent=7, ElapsedSeconds=11)], 'Wrong travel events')
            if name == 'home':
                require(view['Checkpoint'] is None and view['Entries'][1]['Enabled'] is False, 'Fresh Home should disable Resume')
            else:
                validate_checkpoint(view['Checkpoint'], funds, elapsed, location)
            if name in ('wide', 'tall'):
                require(row['backbuffer'] == ([1280, 720] if name == 'wide' else [720, 960]), 'Native resize differs')
            if view['Screen'] in (3, 4):
                require([entry['Action'] for entry in view['Entries']] == [7, 8], 'Modal exposed parent actions')
            prior = view
        require(visited == set(expected) and travel_count == 2 and prior == report['final'], 'Incomplete authored route')
        captures = json.loads(files['captures.json'])
        require(tuple(item['name'] for item in captures) == CAPTURES, 'Capture order differs')
        for item in captures:
            validate_image(directory / (item['name'] + '.png'), item['sha256'])
        inventories.append(files)
    require(inventories[0] == inventories[1], 'Repeated menu artifacts differ')
    return dict(format='fft.windows-world-navigation-audit', schemaVersion=1, passed=True,
                nativeProcesses=2, samplesPerProcess=51, imagesPerProcess=13, exactRepeatedFiles=16,
                matchedPixels=2 * 13 * 960 * 640, independentStateAndEvents=True, physicalInputVerified=False, originalCompared=False)


def gallery(first, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    cards = ''.join(f'<figure><figcaption>{html.escape(name.replace("-", " ").title())}</figcaption>'
                    f'<a href="{(first / (name + ".png")).resolve().as_uri()}"><img src="{(first / (name + ".png")).resolve().as_uri()}" alt="{name}"></a></figure>' for name in CAPTURES)
    destination.write_text('<!doctype html><meta charset="utf-8"><title>FFT world navigation review</title><style>'
                          'body{background:#0e1522;color:#e4eef4;font:16px system-ui;margin:32px}h1{font-size:26px}'
                          'p{max-width:900px;line-height:1.6;color:#acbdcf}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(440px,1fr));gap:24px}'
                          'figure{margin:0;background:#1b2835;padding:12px;border-radius:8px}figcaption{margin-bottom:10px}img{width:100%;image-rendering:pixelated}</style>'
                          '<h1>FFT / Authored world navigation</h1><p>Review labels, layout, selected and disabled entries, the read-only Party view, and Cancel-first New/Exit prompts. '
                          'These are authored diagnostic screens. Native checks and repeated images pass; physical keyboard, mouse and controller mapping still need a separate check. '
                          'Wide and tall entries show the unchanged 960x640 canvas captured after resizing. Click an image for full size.</p><main>' + cards + '</main>', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('first', 'repeat', 'output', 'review'):
        parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    try:
        require(not args.output.exists() and not args.review.exists(), 'Audit/review output exists')
        result = verify(args.first, args.repeat)
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        gallery(args.first, args.review)
        print('PASS: 16 repeated files, 26 CPU/GPU images, authored screens/state/events and finite budgets')
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(2, str(error) + '\n')
