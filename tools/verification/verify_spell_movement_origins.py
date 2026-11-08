"""ZOD-426 source descriptions for origin traversal; no C#, C or original-game execution."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from verify_sword_knockback_contract import REVISION, fingerprint, function_fingerprint

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/spell-movement-origins-v1.json'
RECEIPT = ROOT / 'docs/evidence/ai/spell-movement-origins-source-v1.json'


def require(condition, message):
    if not condition:
        raise ValueError(message)


def boolean(value):
    require(type(value) is bool, 'Named independent boolean policy')
    return value


def integer(value, low, high):
    require(type(value) is int and low <= value <= high, 'Source numeric width')
    return value


def tile(value):
    require(isinstance(value, list) and len(value) == 3, 'X/Y/layer tile')
    integer(value[0], 0, 15); integer(value[1], 0, 15); integer(value[2], 0, 1)
    return tuple(value)


def scan_key(position):
    x, y, layer = tile(position)
    return layer, y, x


def signed16(value):
    return (value + 32768) % 65536 - 32768


def policy(data):
    exhaustive = boolean(data['exhaustiveOrigins'])
    consume = boolean(data['consumeEvaluatedAims']) and not exhaustive
    aims = integer(data['aimTiles'], 0, 512) & 255
    origins = integer(data['originTiles'], 0, 512) & 255
    # The second unconditional assignment overrides the product threshold.
    return dict(ranked=not exhaustive, consume=consume, aimCountByte=aims, originCountByte=origins,
                possibilitiesByte=None if exhaustive else (aims * origins) & 255,
                mayVisitOrigins=data['originTiles'] != 0 if exhaustive else bool(aims and origins))


def origin_order(data):
    exhaustive = boolean(data['exhaustiveOrigins'])
    origins = data['origins']
    require(len({tuple(tile(o['tile'])) for o in origins}) == len(origins), 'Distinct origin cells')
    for o in origins:
        integer(o['priority'], -32768, 32767); integer(o['proximity'], 0, 255)
    if exhaustive:
        return [o['id'] for o in sorted(origins, key=lambda o: scan_key(o['tile']))]
    # origin_next_take scans ascending and replaces on >=, so the last equal cell wins.
    return [o['id'] for o in sorted(origins, key=lambda o:
            (o['priority'] * 65536 + o['proximity'], *(-v for v in scan_key(o['tile']))))]


def finalize(data):
    for name in ('actor', 'chosenOrigin', 'scenarioDestination'):
        require(isinstance(data[name], list) and len(data[name]) == 4, 'X/Y/layer/zero coordinate record')
        tile(data[name][:3]); integer(data[name][3], 0, 255)
    act_first = data['chosenOrigin'] == data['actor']  # Full source word, including its fourth byte.
    return dict(actBeforeMove=act_first, castOrigin=data['actor'] if act_first else data['chosenOrigin'],
                destination=data['scenarioDestination'] if act_first else data['chosenOrigin'])


def baseline(data):
    stored = integer(data['storedTilePriority'], -32768, 32767)
    candidate = data['candidate']
    priority = integer(candidate['priority'], -32768, 32767)
    hit = integer(candidate['hit'], 0, 255)
    decided = boolean(candidate['decided'])
    integer(candidate['proximity'], 0, 255)  # Deliberately absent from the baseline comparison.
    score = signed16(-(stored & 65535))
    return dict(priority=score, hit=100, decided=False,
                passesBaseline=(int(decided), priority, hit) > (0, score, 100))


def masks(data):
    width = integer(data['width'], 1, 16); depth = integer(data['depth'], 1, 16)
    radius = integer(data['range'], 0, 7)
    area = integer(data['area'], 0, 7)
    require(radius + area <= 7, 'No source radius-table overread in the bounded mask description')
    unit_mode = boolean(data['unitTargeting']); follow = boolean(data['followTarget'])
    actor = tile(data['actor'])
    targets = [tile(t) for t in data['usefulTargets'] if tile(t) != actor]
    scenario = [tile(t) for t in data['scenarioOrigins']]
    walkable = {tile(t) for t in data['walkableAims']}
    for p in [actor, *targets, *scenario, *walkable]:
        require(p[0] < width and p[1] < depth, 'Mask coordinates inside selected map')
    cast_radius = radius if unit_mode else radius + area
    cast = [list(p) for p in sorted(set(scenario), key=lambda p: (p[2], p[1], p[0]))
            if any(abs(p[0]-t[0]) + abs(p[1]-t[1]) <= cast_radius for t in targets)]
    aims = set()
    aim_radius = 0 if unit_mode else area
    for t in targets:
        for layer in range(2):
            for y in range(depth):
                for x in range(width):
                    if abs(x-t[0]) + abs(y-t[1]) <= aim_radius:
                        aims.add((x,y,layer))
    if follow and not unit_mode:
        for t in targets:
            aims.symmetric_difference_update([t])  # Original own-layer XOR, not a general set subtraction.
    return dict(castOrigins=cast, aims=[list(p) for p in sorted(aims & walkable, key=lambda p: (p[2],p[1],p[0]))])


def trace(data):
    exhaustive = boolean(data['exhaustiveOrigins'])
    consume_setting = boolean(data['consumeEvaluatedAims']) and not exhaustive
    origins = data['origins']; aims = data['aims']
    require(len(origins) < 256 and len(aims) < 256, 'Trace tapes exclude byte-count aliasing')
    require(len({o['id'] for o in origins}) == len(origins) and len({a['id'] for a in aims}) == len(aims), 'Unique tape IDs')
    order = origin_order(data)
    positions = {a['id']: scan_key(a['tile']) for a in aims}
    remaining = set(positions)
    current = integer(data['seed'], 0, 0xffffffff)
    draws = 0; calls = []; consumed = []; stopped = []; visited = []; best = None; best_key = (-32767,0, -255)
    for origin_id in order:
        if not exhaustive and not remaining:
            break
        visited.append(origin_id)
        origin = next(o for o in origins if o['id'] == origin_id)
        candidates = origin['candidates']
        require(len({c['aim'] for c in candidates}) == len(candidates) and all(c['aim'] in positions for c in candidates), 'One range-intersected result per aim')
        for c in sorted(candidates, key=lambda c: positions[c['aim']]):
            if c['aim'] not in remaining:
                continue
            require(c['status'] in ('scored','not-valuable','unresolved'), 'Supplied candidate disposition')
            calls.append([origin_id,c['aim']])
            for _ in range(integer(c['draws'],0,1024)):
                current = (current*0x41c64e6d + 0x3039) & 0xffffffff
                draws += 1
            if c['status'] != 'not-valuable':
                priority = integer(c['priority'],-30000,32767)  # No equal-to-reset/stale-coordinate branch is described.
                hit = integer(c['hit'],0,255)
                require(hit == 255 if c['status']=='unresolved' else hit <= 100, 'Ordinary/unresolved hit domain')
                key = priority,hit,-integer(c['destinationProximity'],0,255)
                if key > best_key:
                    best = [origin_id,c['aim']]; best_key=key
            if c['status'] == 'unresolved':
                stopped.append(origin_id)
                break  # Comparison precedes processing_done; consumption has not happened.
            self_effect = c.get('selfEffect','none')
            require(self_effect in ('none','useful','adverse'), 'Classified self outcome')
            self_hit = boolean(c.get('selfHit',False))
            consume = consume_setting or (not exhaustive and
                (self_effect=='useful' and self_hit or self_effect=='adverse' and not self_hit))
            if consume:
                remaining.remove(c['aim']); consumed.append([origin_id,c['aim']])
                if not remaining:
                    break
    return dict(originOrder=visited,calls=calls,consumed=consumed,stoppedOrigins=stopped,best=best,draws=draws,
                seed=current,remainingAims=sorted(remaining,key=positions.get))


def validate(document):
    require(document.get('schemaVersion')==1 and document.get('referenceRevision')==REVISION and
            document.get('originalExecuted') is False and document.get('cExecuted') is False and
            document.get('runtimeImplemented') is False, 'Pinned source-only study provenance')
    descriptions = {'policies':policy,'orders':origin_order,'destinations':finalize,'baselines':baseline,'masks':masks,'traces':trace}
    seen=set();result={}
    for group,describe in descriptions.items():
        for row in document[group]:
            require(row['id'] not in seen,'Unique description identity');seen.add(row['id'])
            require(describe(row['input'])==row['expected'],'Source description: '+row['id'])
        result[group]=len(document[group])
    return dict(descriptions=result,total=len(seen),originalExecuted=False,cExecuted=False,runtimeImplemented=False)


def audit(reference, battle_bin=None):
    require(subprocess.check_output(['git','rev-parse','HEAD'],cwd=reference,text=True).strip()==REVISION,'Pinned source revision')
    require(not subprocess.check_output(['git','status','--porcelain'],cwd=reference,text=True).strip(),'Clean read-only source')
    receipt=json.loads(RECEIPT.read_text(encoding='utf-8'))
    require(receipt['referenceRevision']==REVISION,'Receipt revision')
    for row in receipt['sourceFiles']:
        require(fingerprint(reference,row['path'])==row,'Source file '+row['path'])
    for row in receipt['sourceFunctions']:
        require(function_fingerprint(reference/row['path'],row['symbol'])=={k:v for k,v in row.items() if k!='path'},'Source routine '+row['symbol'])
    result = dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))),sourceFiles=len(receipt['sourceFiles']),sourceFunctions=len(receipt['sourceFunctions']))
    if battle_bin is not None:
        prepared = receipt['preparedBattle']
        data = battle_bin.read_bytes()
        require(len(data) == prepared['byteLength'] and hashlib.sha256(data).hexdigest() == prepared['sha256'], 'Prepared BATTLE identity')
        load = int(prepared['loadAddress'], 16)
        for row in prepared['routines']:
            start = int(row['address'], 16) - load
            require(hashlib.sha256(data[start:start+row['byteLength']]).hexdigest() == row['sha256'], 'Prepared routine '+row['symbol'])
        for row in prepared['rankedFlagInstructionAnchors']:
            start = int(row['address'], 16) - load
            require(int.from_bytes(data[start:start+4], 'little') == int(row['word'], 16), 'Ranked flag instruction '+row['address'])
        result['preparedRoutines'] = len(prepared['routines'])
        result['preparedInstructionAnchors'] = len(prepared['rankedFlagInstructionAnchors'])
    return result


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reference',type=Path,required=True)
    parser.add_argument('--battle-bin',type=Path,help='Optional ignored prepared file; never a runtime dependency')
    parser.add_argument('--output',type=Path);args=parser.parse_args();result=audit(args.reference,args.battle_bin)
    if args.output:
        with args.output.open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2);stream.write('\n')
    print(json.dumps(result))
