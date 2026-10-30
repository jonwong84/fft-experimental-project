"""Independent authored Save/Load UI and isolated-storage audit. No original card/campaign claim."""
import argparse
import html
import json
from pathlib import Path
from verify_world_menu import require, validate_checkpoint, validate_image
from verify_world_checkpoint import snapshot

CAPTURES = ('home', 'empty-slots', 'page-two', 'empty-details', 'saved-harbor', 'overwrite-prompt',
            'saved-hill', 'load-prompt', 'loaded-hill', 'stale-error', 'stale-refresh', 'corrupt-details',
            'recovery-prompt', 'recovered', 'saved-recovered', 'missing-error', 'busy-error', 'busy-refresh',
            'wide-details', 'close-prompt', 'tall-slots', 'closed')


def storage(directory):
    expected = {f'slot-{i:02}.lock' for i in range(1, 11)} | {'slot-01.json', 'slot-01.bak', 'slot-02.json'}
    files = {p.name: p.read_bytes() for p in directory.iterdir() if p.is_file()}
    require(set(files) == expected, 'Unexpected slot/backup/temp inventory')
    require(all(not files[f'slot-{i:02}.lock'] for i in range(1, 11)), 'Coordination locks should be empty')
    require(files['slot-01.json'] == b'{"corrupt":true}', 'Recovery rewrote the damaged primary')
    backup, recovered = snapshot(files['slot-01.bak']), snapshot(files['slot-02.json'])
    require(backup == recovered, 'Recovered checkpoint differs from the validated backup')
    require((backup['locationId'], backup['funds'], backup['elapsedSeconds']) == ('sample:location/hill', 93, 53), 'Unexpected recovered checkpoint')
    require(backup['month'] == 3 and backup['day'] == 14 and backup['progress'] is True, 'Recovery changed date/progress')
    return files


def validate_modal(view, expected_screen):
    require(view['Screen'] == expected_screen and view['Focus'] == 7, 'Confirmation must start on Cancel')
    require([e['Action'] for e in view['Entries']] == [7, 8], 'Modal exposed parent actions')


def verify(first, repeat):
    inventories = []
    expected_screens = dict(zip(CAPTURES, (0, 6, 6, 7, 11, 8, 11, 9, 11, 12, 7, 7, 10, 11, 11, 12, 12, 7, 7, 4, 6, 5)))
    successes = {'saved-harbor', 'saved-hill', 'loaded-hill', 'recovered', 'saved-recovered', 'saved-four'}
    failures = {'stale-error', 'missing-error', 'busy-error'}
    for directory in (first, repeat):
        files = {p.relative_to(directory).as_posix(): p.read_bytes() for p in directory.rglob('*') if p.is_file()}
        report = json.loads(files['check.json']); trace = json.loads(files['trace.json']); captures = json.loads(files['captures.json'])
        require(report['format'] == 'fft.windows-world-slots-check' and report['schemaVersion'] == 1 and report['passed'] is True, 'Wrong/failed slot check')
        require(report['frames'] == report['steps'] == len(trace) == 140 and report['captures'] == 22, 'Unexpected finite budget')
        require(all(report[key] is True for key in ('cpuGpuMatched', 'headlessMatched', 'statePreservedByRendering')) and report['physicalInputVerified'] is False, 'Unsupported check claims')
        require(tuple(item['name'] for item in captures) == CAPTURES, 'Missing/out-of-order captures')
        visited, good, bad = set(), set(), set(); trips = 0; current = None; prior = None
        for row in trace:
            name, view = row['Name'], row['view']
            if name in expected_screens:
                require(view['Screen'] == expected_screens[name], 'Wrong screen: ' + name); visited.add(name)
            if name == 'new': current = (100, 42, 'harbor')
            if name == 'hill': current = (93, 53, 'hill')
            if name == 'travel-after-save': current = (86, 64, 'harbor')
            if name in ('loaded-hill', 'recovered'): current = (93, 53, 'hill')
            if current is None: require(view['Checkpoint'] is None, 'Unexpected initial session')
            else: validate_checkpoint(view['Checkpoint'], *current)
            if name == 'release': require(row['command'] is None and row['result'] is None and view == prior, 'Release repeated an action')
            result = row['result'] or {}
            if 'Operation' in result:
                op = result['Operation']; require(op['Canceled'] is False, 'Canceled operation should not perform I/O')
                if name in successes:
                    require(op['Succeeded'] is True, 'Expected successful operation: ' + name); good.add(name)
                    require(op['RecoveredFromBackup'] == (name == 'recovered'), 'Recovery capability differs')
                elif name in failures:
                    require(op['Succeeded'] is False and view['Slots']['NeedsRefresh'] is True, 'Expected failure/refresh gate: ' + name); bad.add(name)
                else: require(False, 'Unexpected persistence operation: ' + name)
            if result.get('Travel'):
                trips += 1; travel = result['Travel']; require(travel['Accepted'] is True, 'Travel rejected')
                require(travel['Events'] == [dict(From={'Value': 'sample:location/' + ('harbor' if name == 'hill' else 'hill')},
                    To={'Value': 'sample:location/' + current[2]}, FundsSpent=7, ElapsedSeconds=11)], 'Travel events differ')
            if name in ('overwrite-prompt', 'load-prompt', 'recovery-prompt'): validate_modal(view, expected_screens[name])
            if name == 'corrupt-details':
                selected = view['Slots']['Selected']; require(selected['Primary'] == 2 and selected['Backup'] == 1 and selected['Summary'] is None, 'Corrupt primary offered a summary')
                require(selected['BackupSummary']['Funds'] == 93 and selected['BackupSummary']['ElapsedSeconds'] == 53, 'Wrong backup metadata')
                require([e['Enabled'] for e in view['Entries'][:3]] == [False, False, True], 'Invalid primary offered an unsafe operation')
            if name == 'page-two': require(view['Slots']['Page'] == 1 and view['Slots']['Selected']['SlotId'] == 'slot-06', 'Wrong second page')
            if name == 'stale-refresh': require(view['Slots']['Selected']['Summary']['Funds'] == 100 and not view['Slots']['NeedsRefresh'], 'Refresh did not expose external revision')
            if name in failures: require('saves\\' not in view['Slots']['Message'] and 'replay-saves' not in view['Slots']['Message'], 'Storage path leaked into UI')
            prior = view
        require(visited == set(CAPTURES) and good == successes and bad == failures and trips == 2, 'Incomplete UI route')
        require(prior == report['final'] and prior['Screen'] == 5, 'Wrong final view')
        for item in captures: validate_image(directory / (item['name'] + '.png'), item['sha256'])
        live = storage(directory / 'saves'); replay = storage(directory / 'replay-saves'); require(live == replay, 'Live/replay storage differs')
        expected_files = {name + '.png' for name in CAPTURES} | {'trace.json', 'captures.json', 'check.json'} | {prefix + '/' + name for prefix in ('saves', 'replay-saves') for name in live}
        require(set(files) == expected_files and len(files) == 51, 'Unexpected output inventory')
        inventories.append(files)
    require(inventories[0] == inventories[1], 'Repeated Save/Load UI/storage artifacts differ')
    return dict(format='fft.windows-world-slots-audit', schemaVersion=1, passed=True, nativeProcesses=2,
                samplesPerProcess=140, capturesPerProcess=22, exactRepeatedFiles=51, matchedPixels=2 * 22 * 960 * 640,
                successfulOperationsPerProcess=6, failedOperationsPerProcess=3, damagedPrimaryPreserved=True,
                recoveredStateSavedToNewSlot=True, physicalInputVerified=False, originalCompared=False)


def gallery(first, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    cards = ''.join(f'<figure><figcaption>{html.escape(name.replace("-", " ").title())}</figcaption><a href="{(first / (name + ".png")).resolve().as_uri()}">'
                    f'<img src="{(first / (name + ".png")).resolve().as_uri()}" alt="{name}"></a></figure>' for name in CAPTURES)
    destination.write_text('<!doctype html><meta charset="utf-8"><title>FFT Save/Load review</title><style>'
        'body{background:#0e1522;color:#e4eef4;font:16px system-ui;margin:32px}h1{font-size:26px}p{max-width:960px;line-height:1.6;color:#acbdcf}'
        'main{display:grid;grid-template-columns:repeat(auto-fit,minmax(440px,1fr));gap:24px}figure{margin:0;background:#1b2835;padding:12px;border-radius:8px}'
        'figcaption{margin-bottom:10px}img{width:100%;image-rendering:pixelated}</style><h1>FFT / Save and Load slots</h1>'
        '<p>Review both slot pages, validated primary/backup details, Cancel-first overwrite/load/recovery prompts, success and failure states. '
        'Recovery preserves the damaged primary and the recovered checkpoint is saved to another slot. Native and repeated-artifact checks pass. '
        'These new screens still need owner visual and interactive input review; prior navigation approval is retained separately. Click an image for full size.</p><main>' + cards + '</main>', encoding='utf-8')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for key in ('first', 'repeat', 'output', 'review'): parser.add_argument('--' + key, type=Path, required=True)
    args = parser.parse_args()
    try:
        require(not args.output.exists() and not args.review.exists(), 'Audit/review output exists')
        result = verify(args.first, args.repeat); args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
        gallery(args.first, args.review); print('PASS: 51 repeated UI/storage files, 44 CPU/GPU images and independent continuation/recovery audit')
    except (ValueError, KeyError, TypeError, OSError) as error: parser.exit(2, str(error) + '\n')
