"""ZOD-199 isolated out-of-reach mask/cache descriptions; never executes original, C or managed AI."""
import argparse
import json
from pathlib import Path

from verify_ai_pair_preparation_contract import audit as audit_preparation
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / 'docs/evidence/ai/pair-out-of-reach-contract-v1.json'
FIXTURE = ROOT / 'docs/fixtures/behavior/ai-pair-out-of-reach-contract-v1.json'


def describe(data):
    """Supplied reachable mask, uniform closed movement score, prior actor cell and per-tile draw cost.

    No movement spread, refresh, clock, path, formula, complete search or prior-memory reconstruction.
    None means genuinely undescribed prior contents; it never becomes zero.
    """
    def tile(value):
        require(len(value) == 3 and all(type(v) is int for v in value) and
                0 <= value[0] < 16 and 0 <= value[1] < 16 and value[2] == 0, 'Closed logical tile')
        return tuple(value)
    order = lambda p: (p[2], p[1], p[0])
    actor, target = tile(data['actor']), tile(data['target'])
    supplied = [tile(p) for p in data['reachable']]; reachable = set(supplied)
    require(actor in reachable and target not in reachable and len(reachable) == len(supplied), 'Admitted unique mask')
    move, score, retained = data['move'], data['movementScore'], data['retainedActorPriority']
    require(type(move) is int and 0 <= move <= 124 and type(score) is int and -160 <= score <= 160 and
            (retained is None or type(retained) is int and -32768 <= retained <= 32767), 'Closed source widths')
    require(type(data['nonBlue']) is bool and type(data['imminent']) is bool, 'Policy booleans')
    distance = lambda p: abs(p[0] - target[0]) + abs(p[1] - target[1])
    require(distance(actor) > move + 1, 'Out-of-reach description')
    filtered = {p for p in reachable if distance(p) <= distance(actor) - 1}
    filter_fallback = not filtered
    scored = filtered or reachable
    # Source scenario2 is computed before actor is ORed into scenario1, so final masks can overlap.
    second = reachable - scored
    scenario = scored | {actor}
    proximity = lambda p: max(0, 10 - distance(p)) if data['imminent'] else 0
    key = lambda p: -score * 65536 + proximity(p)
    word, count = data['seed'], data['drawCount']
    require(type(word) is int and 0 <= word <= 0xffffffff and type(count) is int and 0 <= count <= 0xffffffffffffffff,
            'Description RNG widths')
    require(data['movementDrawsPerTile'] == 1, 'Supplied closed one-draw movement examples only')
    def draw():
        nonlocal word, count
        require(count < 0xffffffffffffffff, 'Description draw budget')
        word = (word * 1103515245 + 12345) & 0xffffffff; count += 1
        return (word >> 16) & 32767
    for _ in sorted(scored, key=order): draw()
    effective = move + 1; roll = None
    if data['nonBlue']:
        raw = draw(); roll = dict(raw=raw, modulo=raw % 7)
        if raw % 7 == 0: effective //= 2
    within = [p for p in scored if distance(p) <= effective]
    destination_fallback = not within
    cells = within or scored
    d1 = min(cells, key=lambda p: ((distance(p), key(p)) if destination_fallback else (key(p), distance(p))) +
             (-p[2], -p[1], -p[0]))
    actor_fresh = actor in scored
    actor_priority = -score if actor_fresh else retained
    casts = sorted((p for p in scenario if distance(p) <= 4), key=order)
    unknown = actor in casts and actor_priority is None
    selected = None
    if casts and not unknown:
        selected = min(casts, key=lambda p: ((actor_priority if p == actor else -score) * 65536 + proximity(p),
                                            -p[2], -p[1], -p[0]))
    return dict(scoreMask=[list(p) for p in sorted(scored, key=order)], filterFallback=filter_fallback,
                scenario1=[list(p) for p in sorted(scenario, key=order)], scenario2=[list(p) for p in sorted(second, key=order)],
                actorScoreFresh=actor_fresh, actorStoredPriority=actor_priority, actorProximity=proximity(actor),
                effectiveReach=effective, reachRoll=roll, destinationFallback=destination_fallback,
                d0=list(d1), d1=list(d1), throwCastOrigins=[list(p) for p in casts], unknownPriorityRead=unknown,
                firstThrowOrigin=list(selected) if selected is not None else None,
                actBeforeMove=selected == actor if selected is not None else None, rngState=[word, count])


def validate(fixture):
    evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(fixture['ticket'] == evidence['ticket'] == 'ZOD-199' and fixture['schemaVersion'] == 1 and
            fixture['referenceRevision'] == evidence['referenceRevision'] and fixture['productionImplemented'] is False and
            fixture['kind'] == 'isolated-source-descriptions', 'Source-only out-of-reach fixture')
    ids = []
    for row in fixture['vectors']:
        require(describe(row['input']) == row['expected'], 'Out-of-reach provenance: ' + row['id']); ids.append(row['id'])
    require(bool(ids) and len(ids) == len(set(ids)), 'Unique descriptions')
    return dict(pairOutOfReachDescriptions=len(ids))


def audit(reference, prepared=None):
    report = audit_preparation(reference, prepared); evidence = json.loads(EVIDENCE.read_text(encoding='utf-8'))
    require(evidence['referenceRevision'] == report['referenceRevision'], 'Out-of-reach revision')
    for row in evidence['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Out-of-reach source file: ' + row['path'])
    for row in evidence['sourceFunctions']:
        require(function_fingerprint(reference / row['path'], row['symbol'])['normalizedLfSha256'] == row['normalizedLfSha256'],
                'Out-of-reach source function: ' + row['symbol'])
    return dict(**report, **validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),
                pairOutOfReachFiles=len(evidence['sourceFiles']), pairOutOfReachFunctions=len(evidence['sourceFunctions']))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True); parser.add_argument('--prepared-executable', type=Path)
    parser.add_argument('--output', type=Path); args = parser.parse_args(); report = audit(args.reference, args.prepared_executable)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream: json.dump(report, stream, indent=2); stream.write('\n')
    print(json.dumps(report))
