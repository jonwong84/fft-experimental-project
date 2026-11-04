"""ZOD-229 source-derived descriptions, not an original/C/managed AI oracle."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/accumulate-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-accumulate-contract-v1.json'
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, 'Integer domain')
    return value


def narrow(value):
    return (value + 32768) % 65536 - 32768


def describe(kind, data):
    if kind == 'usefulness':
        # Supplied classifier preconditions; no general status/target-policy implementation.
        gate = data['gate']
        require(gate in ('EligibleAlly', 'Enemy', 'Blocked'), 'Classifier gate')
        integer(data['pa'], 1, 99)  # Deliberately absent from the source classifier's decision.
        if gate != 'EligibleAlly':
            require(data['raw'] is None, 'Early return draws nothing')
            return dict(effect='Adverse' if gate == 'Enemy' else 'None', draws=0)
        raw = integer(data['raw'], 0, 32767)
        return dict(effect='Useful' if raw & 1 == 0 else 'None', draws=1)
    if kind == 'pa':
        base = integer(data['base'], 1, 99)
        bonus = integer(data['bonus'], 0, 99)
        effective = integer(data['effective'], 1, 99)
        after = min(99, base + 1)
        return dict(base=after, effective=min(99, after + bonus) if after != base else effective,
                    changed=after != base, restoredBase=base, restoredEffective=effective,
                    resolutionDraws=0)
    if kind == 'score':
        # One included allied self stat-effect hit, accuracy 100, no other effects or pickups.
        # The +3 is strike accounting; PA itself has no absolute-state score term.
        priority = 3
        contributions = []
        require(len(data['units']) == 2 and {u['enemy'] for u in data['units']} == {False, True},
                'Closed allied/enemy pair in supplied source-slot order')
        for unit in data['units']:
            require(type(unit['enemy']) is bool, 'Enemy boolean')
            maximum = integer(unit['maxHp'], 1, 65535)
            hp = integer(unit['hp'], 1, maximum)
            term = hp * 128 // maximum
            status = -32 if hp <= maximum // 5 else 0
            total = term + status + 7 * 51
            signed = -total if unit['enemy'] else total
            priority = narrow(priority + signed)
            contributions.append(dict(hp=term, status=status, equipment=357,
                                      signed=signed, running=priority))
        return dict(statHitCredit=3, hitPercent=100, priority=priority, contributions=contributions)
    if kind == 'rank':
        # Supplied marker-zero ranks, not list construction or a lottery implementation.
        first, baseline = data['candidate'], data['baseline']
        for rank in (first, baseline):
            require(len(rank) == 3, 'Priority/hit/proximity rank')
            integer(rank[0], -32768, 32767)
            integer(rank[1], 0, 255)
            integer(rank[2], 0, 255)
        cmp = (first[:2] > baseline[:2]) - (first[:2] < baseline[:2])
        return dict(beatsMovementBaseline=cmp > 0,
                    higherIncludingProximity=cmp > 0 or cmp == 0 and first[2] < baseline[2])
    raise ValueError('Unknown description kind')


def validate(fixture):
    require(fixture['schemaVersion'] == 1 and fixture['ticket'] == 'ZOD-229' and
            fixture['referenceRevision'] == REVISION and fixture['productionImplemented'] is False and
            fixture['kind'] == 'supplied-accumulate-source-descriptions', 'Source-only fixture identity')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['kind'], row['input']) == row['expected'], 'Accumulate description: ' + row['id'])
        ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique description IDs')
    return dict(descriptions=len(ids), originalExecuted=False, cExecuted=False, managedExecuted=False)


def audit(reference, prepared=None):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == REVISION and evidence['ticket'] == 'ZOD-229' and
            evidence['productionImplemented'] is False, 'Source-only evidence')
    revision = subprocess.check_output(['git', '-C', str(reference), 'rev-parse', 'HEAD'], text=True).strip()
    require(revision == REVISION, 'Pinned reference HEAD')
    require(not subprocess.check_output(['git', '-C', str(reference), 'status', '--porcelain'], text=True).strip(),
            'Read-only reference must be clean')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(all(actual[key] == row[key] for key in actual), 'Source function: ' + row['symbol'])
    if prepared is not None:
        content = prepared.read_bytes()
        require(len(content) == evidence['preparedExecutable']['byteLength'] and
                hashlib.sha256(content).hexdigest() == evidence['preparedExecutable']['sha256'],
                'Prepared SCUS executable identity')
        # Check the selected retail routing, not a synthetic effect package's default AI bytes.
        flags = int.from_bytes(content[0x4f884:0x4f888], 'little')
        require(flags == 0x80008009, 'Allied stat effect, map-target route, AI usable')
        require(content[0x50bf4] == 0x36 and content[0x50bf6] == 1 and
                content[0x50bf8:0x50bfa] == bytes(2), 'Immediate zero-cost PA+1')
    report = validate(json.loads(FIXTURE.read_text(encoding='utf-8')))
    return dict(report, referenceRevision=revision, referenceClean=True,
                sourceFiles=len(evidence['sourceFiles']), sourceFunctions=len(evidence['sourceFunctions']),
                preparedDataChecked=prepared is not None)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--prepared', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    report = audit(args.reference, args.prepared)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(report, stream, indent=2)
            stream.write('\n')
    print(json.dumps(report))
