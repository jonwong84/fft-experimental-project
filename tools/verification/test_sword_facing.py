import copy
import json
from pathlib import Path
import unittest
from verify_sword_facing import validate_replacement


class SwordFacingMigrationAuditTests(unittest.TestCase):
    def fixture(self, name='normal'):
        root = Path(__file__).resolve().parents[2]/'docs/fixtures/behavior'
        return (json.loads((root/f'sword-{name}.v3.source-derived.json').read_text()),
                json.loads((root/f'sword-{name}.v3.settled-facing.json').read_text()))

    def test_all_replacements_have_only_the_authorized_facing_delta(self):
        for name in ('normal','miss','critical','ko','side','zero-pa','cap-ko-wrap','diagonal-rejected','move-attack'):
            self.assertEqual(0 if name=='diagonal-rejected' else 1, validate_replacement(*self.fixture(name)))

    def test_rejects_omitted_correction(self):
        old,new = self.fixture()
        new['expectations'][0]['assertions'][1]['state']['combatants'][0]['facing'] = 'East'
        with self.assertRaises(ValueError): validate_replacement(old,new)

    def test_rejects_unrelated_HP_or_target_facing_mutation(self):
        for key,value in (('hp',26),('facing','South')):
            old,new = self.fixture()
            new['expectations'][0]['assertions'][1]['state']['combatants'][1][key] = value
            with self.assertRaises(ValueError): validate_replacement(old,new)

    def test_rejects_event_or_rng_mutation(self):
        for change in ('events','rng'):
            old,new = self.fixture()
            group = new['expectations'][0]['assertions'][1]
            if change=='events': group['events'] = []
            else: group['state']['rngState']['drawCount'] += 1
            with self.assertRaises(ValueError): validate_replacement(old,new)

    def test_rejects_policy_or_command_mutation(self):
        for change in ('policy','command'):
            old,new = self.fixture()
            if change=='policy': new['target']['policyId'] = old['target']['policyId']
            else: new['scenario']['commands'][0]['targetId'] = 'sample:unit/other'
            with self.assertRaises(ValueError): validate_replacement(old,new)

    def test_requires_explicit_identity_and_pinned_source(self):
        for change in ('identity','source','evidence'):
            old,new = self.fixture()
            if change=='identity': new['fixtureId'] = old['fixtureId']
            elif change=='source': new['evidence'][-1]['sources'][0]['revision'] = '0'*40
            else: new['evidence'].pop()
            with self.assertRaises(ValueError): validate_replacement(old,new)
