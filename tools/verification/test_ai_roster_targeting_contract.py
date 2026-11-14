import copy
import json
import unittest

from verify_ai_roster_targeting_contract import FIXTURE, describe, validate


class AiRosterTargetingContractTests(unittest.TestCase):
    def test_literal_roster_descriptions_preserve_all_supplied_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); before = copy.deepcopy(fixture)
        self.assertEqual(17, validate(fixture)['rosterDescriptions']); self.assertEqual(before, fixture)

    def test_source_ties_live_cache_distinctions_and_cached_delay(self):
        rows = {r['id']: r['expected'] for r in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']}
        self.assertEqual('zulu', rows['nearest-source-order-height-tie']['target'])
        self.assertEqual('corpse-reraise', rows['nearest-dead-reraise-exception']['target'])
        self.assertEqual([], rows['active-specific-hidden']['active'])
        self.assertEqual(['equal-byte'], rows['proximity-cached-byte-not-full-delay']['enemies'])
        self.assertEqual([], rows['proximity-empty-mask-no-grid-clear']['writes'])

    def test_malformed_and_tampered_source_claims_reject(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('runtime', 'expected', 'duplicate', 'bool-order', 'unknown-member', 'coordinates'):
            bad = copy.deepcopy(fixture)
            if kind == 'runtime': bad['productionImplemented'] = True
            elif kind == 'expected': bad['vectors'][1]['expected']['target'] = 'alpha'
            elif kind == 'duplicate': bad['vectors'][1]['id'] = bad['vectors'][0]['id']
            elif kind == 'bool-order': bad['vectors'][0]['input']['units'][0]['order'] = True
            elif kind == 'unknown-member': bad['vectors'][1]['input']['targetable'].append('foreign')
            else: bad['vectors'][0]['input']['units'][0]['position'][0] = -1
            with self.subTest(kind=kind), self.assertRaises(ValueError): validate(bad)
        with self.assertRaises(ValueError): describe('unrecognized', fixture['vectors'][0]['input'])
