import copy
import json
import unittest
from verify_equipment_selection import FIXTURE, SELECTORS, describe, validate


class EquipmentSelectionTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_literal_source_selection_boundaries(self):
        self.assertEqual(validate(self.fixture())['selectionDescriptions'], 19)

    def test_every_original_ability_alias_selects_its_family_target(self):
        equipment = dict(Head=101, Body=102, Accessory=103, RightWeapon=1, LeftWeapon=2, RightShield=90, LeftShield=91)
        expected = {110:'Head',111:'Body',112:'RightShield',113:'LeftWeapon',114:'Accessory',
                    138:'Head',139:'Body',140:'RightShield',141:'LeftWeapon',
                    160:'Body',161:'Head',162:'LeftWeapon',163:'Accessory'}
        self.assertEqual(set(expected), set(SELECTORS))
        for ability, location in expected.items():
            self.assertEqual(describe(dict(abilityId=ability, equipment=equipment, requiredLevels={'1':10,'2':20}))['location'], location)

    def test_higher_power_and_price_do_not_choose_the_weapon(self):
        inputs=dict(abilityId=141,equipment={'RightWeapon':1,'LeftWeapon':2},requiredLevels={'1':10,'2':20},
                    weaponPower={'1':99,'2':1},price={'1':60000,'2':1})
        self.assertEqual(describe(inputs)['location'], 'LeftWeapon')

    def test_shield_priority_and_weapon_tie_mutations_are_detected(self):
        for identity, location, mask, item in [('shield-right-first-with-both-present','LeftShield',2,91),
                                             ('weapon-equal-required-level-right-tie','LeftWeapon',4,2)]:
            fixture=self.fixture();row=next(c for c in fixture['cases'] if c['id']==identity)
            row['expected'].update(location=location,mask=mask,item=item)
            with self.assertRaises(ValueError):validate(fixture)

    def test_unremovable_item_does_not_fall_back_to_other_hand(self):
        for identity in ['unremovable-preferred-weapon-does-not-retarget','unremovable-preferred-shield-does-not-retarget']:
            row=next(c for c in self.fixture()['cases'] if c['id']==identity)
            result=describe(row['input']);self.assertTrue(result['location'].startswith('Right'))
            self.assertFalse(result['passesRemovalItemGuards'])

    def test_unsigned_required_level_and_explicit_location_inputs(self):
        for levels in [{'1':-1},{'1':256}]:
            with self.assertRaises(ValueError):describe(dict(abilityId=141,equipment={'RightWeapon':1},requiredLevels=levels))
        with self.assertRaises(ValueError):describe(dict(abilityId=141,equipment={'MainHand':1}))

    def test_source_only_identity_cannot_claim_original_execution(self):
        fixture=self.fixture();fixture['sourceOriginalExecuted']=True
        with self.assertRaises(ValueError):validate(fixture)

    def test_description_retains_caller_equipment_and_item_metadata(self):
        inputs=self.fixture()['cases'][11]['input'];before=copy.deepcopy(inputs)
        first=describe(inputs);self.assertEqual(first,describe(inputs));self.assertEqual(before,inputs)
