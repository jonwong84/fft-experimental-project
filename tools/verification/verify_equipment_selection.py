"""Source-only equipment selection descriptions. No C execution or managed combat port."""
import argparse
import copy
import json
from pathlib import Path
import re
import subprocess
from verify_sword_knockback_contract import fingerprint, function_fingerprint, require, REVISION

ROOT = Path(__file__).resolve().parents[2]
FIXTURE = ROOT / 'docs/fixtures/behavior/equipment-selection-v1.json'
RECEIPT = ROOT / 'docs/evidence/combat/equipment-selection-source-v1.json'
LOCATIONS = ('Head', 'Body', 'Accessory', 'RightWeapon', 'RightShield', 'LeftWeapon', 'LeftShield')
# Compatibility mapping comes from the ability-ID branches, not a secondary X/Y parameter.
SELECTORS = {
    110: 'Head', 111: 'Body', 112: 'ShieldRightFirst', 113: 'WeaponRequiredLevelRightTie', 114: 'Accessory',
    138: 'Head', 139: 'Body', 140: 'ShieldRightFirst', 141: 'WeaponRequiredLevelRightTie',
    160: 'Body', 161: 'Head', 162: 'WeaponRequiredLevelRightTie', 163: 'Accessory',
}


def describe(inputs):
    """Describe only the ID/slot picker and whether its selected item passes removal's item guards.

    This does not calculate hit/accuracy, apply Maintenance, remove an item, award inventory,
    refresh stats, simulate AI, consume RNG or authorize a managed command.
    """
    equipment = {location: 255 for location in LOCATIONS}
    equipment.update(inputs.get('equipment', {}))
    require(set(equipment) == set(LOCATIONS), 'Known source equipment locations')
    require(all(type(item) is int and 0 <= item <= 255 for item in equipment.values()), 'Equipment bytes')
    levels = inputs.get('requiredLevels', {})
    require(all(type(level) is int and 0 <= level <= 255 for level in levels.values()), 'Unsigned required levels')
    selector = SELECTORS.get(inputs['abilityId'])
    location = None
    if not inputs.get('monster', False):
        if selector in LOCATIONS:
            if equipment[selector] != 255: location = selector
        elif selector == 'ShieldRightFirst':
            location = next((slot for slot in ('RightShield', 'LeftShield') if equipment[slot] != 255), None)
        elif selector == 'WeaponRequiredLevelRightTie':
            available = [slot for slot in ('RightWeapon', 'LeftWeapon') if equipment[slot] != 255]
            if available:
                # Stable max retains the first (right) weapon on equal required levels.
                location = max(available, key=lambda slot: levels[str(equipment[slot])])
    if location is None:
        return dict(location=None, mask=0, item=None, passesRemovalItemGuards=False)
    item = equipment[location]
    return dict(location=location, mask=128 >> LOCATIONS.index(location), item=item,
                passesRemovalItemGuards=item not in (0, 255) and item not in inputs.get('unremovable', []))


def validate(fixture):
    require(fixture['referenceRevision'] == REVISION and fixture['sourceOriginalExecuted'] is False and
            fixture['cExecuted'] is False, 'Source-only fixture identity')
    require(len(fixture['cases']) == 19 and len({c['id'] for c in fixture['cases']}) == 19, 'Literal selector cases')
    for case in fixture['cases']:
        before = copy.deepcopy(case['input'])
        require(describe(case['input']) == case['expected'], 'Equipment selection: ' + case['id'])
        require(case['input'] == before, 'Private description inputs preserved')
    return dict(selectionDescriptions=19, originalAbilitySelectorMappings=len(SELECTORS),
                selectionConsumesRandomDraws=False, managedEquipmentEffectsImplemented=False)


def audit(reference):
    require(subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() == REVISION, 'Pinned reference')
    require(not subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip(), 'Clean read-only reference')
    receipt = json.loads(RECEIPT.read_text(encoding='utf-8'))
    require(receipt['referenceRevision'] == REVISION, 'Source receipt revision')
    for row in receipt['sourceFiles']:
        require(fingerprint(reference, row['path']) == row, 'Source file ' + row['path'])
    for row in receipt['sourceFunctions']:
        actual = function_fingerprint(reference / row['path'], row['symbol'])
        require(all(row[key] == value for key, value in actual.items()), 'Source function ' + row['symbol'])
    ability_source = (reference / 'include/fft/ability.h').read_text(encoding='utf-8')
    for row in receipt['abilitySelectors']:
        require(re.search(r'\b' + row['symbol'] + r'\s*=\s*' + str(row['originalId']) + r'\b', ability_source), 'Source ability ID')
        require(SELECTORS[row['originalId']] == row['selector'], 'Original selector mapping')
    require(len(receipt['abilitySelectors']) == len(SELECTORS), 'Complete inspected ID mapping')
    return dict(validate(json.loads(FIXTURE.read_text(encoding='utf-8'))), referenceRevision=REVISION,
                referenceClean=True, sourceFiles=len(receipt['sourceFiles']), sourceFunctions=len(receipt['sourceFunctions']),
                originalExecuted=False, cExecuted=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream: json.dump(result, stream, indent=2); stream.write('\n')
    print(json.dumps(result))
