"""Independent ZOD-110 retail-record, preserved-import and authored headless audit."""
import argparse
import copy
import hashlib
import json
from pathlib import Path


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def draws(state, count):
    word = state['Value']
    raw = []
    for _ in range(count):
        word = (word * 1103515245 + 12345) & 0xffffffff
        raw.append((word >> 16) & 32767)
    return dict(Value=word, DrawCount=state['DrawCount'] + count), raw


def validate_stone(before, after):
    """Closed authored route: actor A, zero evade, unused Move, clear flat rectangle."""
    expected = copy.deepcopy(before)
    actor, target = expected['Combatants']
    require(target['ClassPhysicalEvade'] == 0, 'Audit route must select zero class evade')
    require(not expected['Turns']['Units'][0]['MovementTaken'], 'Unexpected Move-first audit branch')
    rng, raw = draws(before['RngState'], 7)
    damage = (raw[4] * 2 // 32768 + 1) * actor['PhysicalAttack']
    attempted = raw[6] * target['Brave'] // 32768 < raw[5] * actor['Brave'] // 32768
    target['Hp'] = max(0, target['Hp'] - damage)
    target['Status'] = 1 if target['Hp'] == 0 else 2 if target['Hp'] <= target['MaxHp'] // 5 else 0
    moved = False
    if target['Hp'] == 0:
        target['KoCount'] = (target['KoCount'] + 1) & 255
        expected['Outcome'] = 1
        expected['WinnerId'] = {'Value': actor['UnitId']['Value']}
    elif attempted:
        a, b = expected['units'][0]['Position'], expected['units'][1]['Position']
        dx, dy = b['X'] - a['X'], b['Y'] - a['Y']
        candidate = dict(b)
        if abs(dx) > abs(dy):
            candidate['X'] += 1 if dx > 0 else -1
        else:
            candidate['Y'] += 1 if dy > 0 else -1
        if 0 <= candidate['X'] < 7 and 0 <= candidate['Y'] < 5:
            expected['units'][1]['Position'] = candidate
            moved = True
    expected['Turns']['Units'][0]['ActionTaken'] = True
    expected['Turns']['RngState'] = expected['RngState'] = rng
    require(after == expected, 'Throw Stone damage/position/choices/facing/RNG delta differs')
    return dict(damage=damage, attempted=attempted, moved=moved)


def inventory(root):
    return {path.name: path.read_bytes() for path in root.iterdir() if path.is_file()}


def verify(root, exe_path, first_import, repeat_import, first_check, repeat_check):
    exe = exe_path.read_bytes()
    require(len(exe) == 356352 and sha(exe) == '287169026029ea8ff80b9d4e2bdfd8a401e828b7cf7544e0a22c19d41b6cc562', 'Wrong executable edition/hash')
    require(exe[:8] == b'PS-X EXE' and int.from_bytes(exe[0x18:0x1c], 'little') == 0x80010000 and
            int.from_bytes(exe[0x1c:0x20], 'little') == 0x56800, 'Wrong executable header')
    imports = [inventory(path) for path in (first_import, repeat_import)]
    require(imports[0] == imports[1] and set(imports[0]) == {'targeted.package.json', 'targeted.import.json'}, 'Import repeat/inventory')
    provenance = json.loads(imports[0]['targeted.import.json'])
    require(provenance['targetEdition'] == 'SCUS-94221' and provenance['input']['sha256'] == sha(exe), 'Import identity')
    expected = {('g_job_data',74):(0x800610b8,48), ('g_ability_human_skillset_abilities',5):(0x80064a94,25),
                ('g_ability_data',148):(0x8005ebf0,8), ('g_ability_range_data',148):(0x8005fbf0,14),
                ('g_command_menu_types_by_skillset',5):(0x80065cb4,1), ('g_status_infliction_data',0):(0x80063fc4,6)}
    seen = set()
    for row in provenance['records']:
        key = row['table'], row['index']
        require(key in expected and key not in seen, 'Unexpected/duplicate source record')
        seen.add(key)
        base, stride = expected[key]
        address = base + key[1] * stride
        offset = address - 0x80010000 + 0x800
        require((row['virtualAddress'], row['fileOffset'], row['byteLength']) == (address,offset,stride), 'Source mapping')
        require(row['rawHex'] == exe[offset:offset+stride].hex(), 'Source bytes')
    require(seen == set(expected), 'Missing source record')
    secondary = exe[0x50c08:0x50c08+14]
    require(list(secondary) == [4,0,0,1,17,0,34,0,55,0,2,0,0,0], 'Secondary semantics')
    skillset = exe[0x55311:0x55311+25]
    require(exe[0x52698] == 5 and skillset[5] | (((skillset[0] >> 5) & 1) << 8) == 148, 'Job/slot membership')
    require(exe[0x564b9] == 0 and exe[0x547c4:0x547c4+6] == bytes(6), 'Menu/status semantics')
    package = json.loads(imports[0]['targeted.package.json'])
    require(package['abilities'] == [dict(id='fft.ps1:ability/source-0148', effect='throw-stone-flat', target='occupied-unit',
                range=4, maximumDamageMultiplier=2, chargeTicks=0, mpCost=0)], 'Converted effect/stable ID')
    historical = json.loads((root/'docs/evidence/combat/accumulate-implementation-v1.json').read_text(encoding='utf-8'))['offlineImport']
    preserved = []
    for item in historical['preservedHistoricalFiles']:
        require(sha((root/item['original']).read_bytes()) == item['sha256'], 'Historical metadata/sword file changed')
        preserved.append(item['original'])
    for item in historical['repeatedFiles']:
        require(sha((root/item['path']).read_bytes()) == item['sha256'], 'Historical Accumulate import changed')
        preserved.append(item['path'])
    metadata = json.loads((root/'scratch/imported-squire-check-a/content.package.json').read_text(encoding='utf-8'))
    old_records = json.loads((root/'scratch/imported-squire-check-a/import.provenance.json').read_text(encoding='utf-8'))['records']
    require(len(old_records) == 22, 'Historical record inventory')
    for row in old_records:
        offset = row['virtualAddress'] - 0x80010000 + 0x800
        require(row['fileOffset'] == offset and exe[offset:offset+row['byteLength']].hex() == row['rawHex'], 'Historical imported record bytes')
    learning = next(item for item in metadata['abilities'] if item['id'] == 'fft.ps1:ability/source-0148')
    primary = exe[0x4f890:0x4f898]
    require(learning['jpCost'] == int.from_bytes(primary[:2], 'little') and learning['learnOnHitRateRaw'] == primary[2] and
            learning['serializedClassRaw'] == primary[3] & 15 and learning['aiFlagsRaw'] == list(primary[4:]), 'Learning mapping')
    require(package['requiredMetadataPackageId'] == metadata['packageId'] and package['profileId'] == metadata['profileId'], 'Metadata dependency binding')
    checks = [inventory(path) for path in (first_check, repeat_check)]
    require(checks[0] == checks[1] and set(checks[0]) == {'content.package.json','ability.package.json','targeted.package.json','check.json'}, 'Headless repeat/inventory')
    report = json.loads(checks[0]['check.json'])
    require(report['format'] == 'fft.basic-skill-headless-check' and report['schemaVersion'] == 1 and
            report['freshDefinitionContinuation'] and report['restartMatched'], 'Headless format/check flags')
    require(len(report['receipts']) == 46 and report['checkpointCount'] == 47, 'Finite journal budget')
    before = None
    rejected = uses = pushes = 0
    for receipt in report['receipts']:
        after = json.loads(receipt['state'])
        if not receipt['Accepted']:
            require(before == after and receipt['events'] == [], 'Rejected command changed state/events')
            rejected += 1
        elif receipt['kind'] == 'UseTargetedAbility':
            require(receipt['command']['ActorId']['Value'] == 'sample:unit/first' and
                    receipt['command']['TargetId']['Value'] == 'sample:unit/second', 'Unexpected actor/target')
            result = validate_stone(before, after)
            require(result['damage'] in (6,12), 'Boosted PA did not feed damage')
            uses += 1
            pushes += result['moved']
        else:
            rng = before['RngState'] if before else dict(Value=1,DrawCount=0)
            require(after['RngState'] == draws(rng, 1 if receipt['kind'] == 'Advance' else 0)[0], 'Unexpected readiness/nonrandom draws')
        require(after['Turns']['RngState'] == after['RngState'], 'RNG copies diverged')
        before = after
    require(uses == 6 and pushes == 3 and rejected == 8, 'Route coverage counts')
    require(before == json.loads(report['final']) and before['Outcome'] == 1 and before['Combatants'][0]['PhysicalAttack'] == 6, 'Final outcome')
    require(before['RngState'] == dict(Value=4200531631,DrawCount=58), 'Final RNG vector')
    return dict(format='fft.basic-skill-content-audit',schemaVersion=1,passed=True,sourceRecords=6,historicalRecordsRechecked=22,
                historicalFilesPreserved=preserved,importFilesRepeated=2,headlessFilesRepeated=4,commands=46,
                continuations=47,throwStoneUses=uses,pushes=pushes,atomicRejections=rejected,
                finalRng=before['RngState'],originalExecution=False,nativeExecution=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('exe','import-first','import-repeat','check-first','check-repeat','output'):
        parser.add_argument('--'+name,type=Path,required=True)
    args = parser.parse_args()
    result = verify(Path(__file__).resolve().parents[2],args.exe,args.import_first,args.import_repeat,args.check_first,args.check_repeat)
    with args.output.open('x',encoding='utf-8') as output:
        output.write(json.dumps(result,indent=2)+'\n')
    print('PASS: six retail records, six preserved historical files, repeated import/headless bytes and independent damage/knockback/RNG deltas')


if __name__ == '__main__':
    main()
