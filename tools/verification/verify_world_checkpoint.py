"""Independent invented world-save fixture and finite developer-check audit. No original inputs."""
import argparse
import hashlib
import json
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def unique(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'Duplicate JSON field: ' + key)
        result[key] = value
    return result


def snapshot(raw):
    require(len(raw) <= 65536, 'Oversized world save')
    envelope = json.loads(raw, object_pairs_hook=unique)
    require(set(envelope) == {'format', 'schemaVersion', 'sha256', 'snapshot'} and
            envelope['format'] == 'fft.synthetic-world-save' and type(envelope['schemaVersion']) is int and envelope['schemaVersion'] in (1, 2), 'Wrong world save envelope')
    data = envelope['snapshot']
    canonical = json.dumps(data, separators=(',', ':'), ensure_ascii=True).encode()
    require(hashlib.sha256(canonical).hexdigest() == envelope['sha256'], 'World checksum differs')
    expected = {'profileId', 'profileVersion', 'contentVersion', 'routeId', 'locationId', 'progressFlagId',
                'progress', 'funds', 'month', 'day', 'elapsedSeconds', 'party', 'inventory', 'summary'}
    if envelope['schemaVersion'] == 2:
        expected |= {'checkpoint', 'rngPolicy'}
        require(data['checkpoint'] == 'world-idle' and data['rngPolicy'] == 'unsupported', 'Unexpected checkpoint/RNG policy')
    require(set(data) == expected, 'Unexpected snapshot fields')
    require(data['profileId'] == 'sample:profile/world-checkpoint' and data['profileVersion'] == data['contentVersion'] == 1 and
            data['routeId'] == 'sample:route/world-idle' and data['locationId'] in ('sample:location/harbor', 'sample:location/hill') and
            data['progressFlagId'] == 'sample:progress/bridge-open', 'Wrong authored content binding')
    require([p['id'] for p in data['party']] == ['sample:party/ada', 'sample:party/borin'] and
            all(p['jobId'] == 'sample:job/courier' for p in data['party']), 'Wrong authored party')
    require(data['summary'] == dict(locationId=data['locationId'], partyCount=len(data['party']), funds=data['funds'],
            month=data['month'], day=data['day'], elapsedSeconds=data['elapsedSeconds'], progress=data['progress']), 'Stale summary')
    return {k: value for k, value in data.items() if k not in ('checkpoint', 'rngPolicy')}


def verify(first, repeat, fixtures):
    current = snapshot((fixtures / 'synthetic-world-idle-v2.json').read_bytes())
    previous = snapshot((fixtures / 'synthetic-world-idle-v1.json').read_bytes())
    require(current == previous, 'Invented predecessor/current fixtures diverge')
    require((current['locationId'], current['funds'], current['month'], current['day'], current['elapsedSeconds'], current['progress']) ==
            ('sample:location/harbor', 100, 3, 14, 42, True), 'Independent checkpoint expectation differs')
    artifacts = []
    for directory in (first, repeat):
        report = json.loads((directory / 'check.json').read_bytes(), object_pairs_hook=unique)
        require(report['format'] == 'fft.synthetic-world-save-check' and report['schemaVersion'] == 1 and report['catalogSlots'] == 10 and
                report['rngCapability'] == 'unsupported' and report['nextLocation'] == 'sample:location/hill' and
                report['nextFunds'] == current['funds'] - 7 and report['nextElapsedSeconds'] == current['elapsedSeconds'] + 11, 'Wrong continuation report')
        for flag in ('freshDefinitionsMatched', 'continuationStateAndEventsMatched', 'staleSaveRejected',
                     'canceledAndFailedLoadPreservedState', 'explicitValidatedBackupRecovery', 'damagedPrimaryPreserved', 'recoveredSavedToNewSlot'):
            require(report[flag] is True, 'Missing check: ' + flag)
        require((directory / 'slots/slot-01.json').read_bytes() == b'{"corrupt":true}', 'Damaged primary was silently repaired')
        require(snapshot((directory / 'slots/slot-01.bak').read_bytes()) == current and
                snapshot((directory / 'slots/slot-02.json').read_bytes()) == current, 'Backup/new-slot recovery differs')
        files = {p.relative_to(directory).as_posix(): p.read_bytes() for p in directory.rglob('*') if p.is_file()}
        expected_files = {'check.json', 'slots/slot-01.json', 'slots/slot-01.bak', 'slots/slot-02.json'} | {f'slots/slot-{i:02}.lock' for i in range(1, 11)}
        require(set(files) == expected_files and all(not files[f'slots/slot-{i:02}.lock'] for i in range(1, 11)), 'Unexpected slot/temp files')
        artifacts.append(files)
    require(artifacts[0] == artifacts[1], 'Repeated save-check artifacts differ')
    return dict(format='fft.synthetic-world-save-audit', passed=True, repeatedFiles=len(artifacts[0]), fixturesMatched=2,
                backupAndRecoveredStateMatched=True, originalCampaignCompared=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('first', 'repeat', 'fixtures', 'output'):
        parser.add_argument('--' + flag, type=Path, required=True)
    args = parser.parse_args()
    try:
        require(not args.output.exists(), 'Audit output exists')
        report = verify(args.first, args.repeat, args.fixtures)
        args.output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(f'PASS: two invented fixtures and {report["repeatedFiles"]} exact repeated artifacts')
    except (ValueError, KeyError, TypeError, OSError) as error:
        parser.exit(2, str(error) + '\n')
