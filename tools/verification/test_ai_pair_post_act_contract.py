import copy
import json
import unittest
from verify_ai_pair_post_act_contract import FIXTURE, validate


class AiPairPostActContractTests(unittest.TestCase):
    def test_actual_checkpoint_fact_descriptions_and_input_preservation(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        before = copy.deepcopy(fixture)
        validate(fixture)
        self.assertEqual(before, fixture)

    def test_malformed_entry_stale_summary_rng_or_execution_claim_rejects(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('choice', 'decided', 'summary', 'reach', 'rng', 'branch', 'claim'):
            altered = copy.deepcopy(original)
            row = altered['vectors'][0]
            if kind == 'choice': row['input']['acted'] = False
            elif kind == 'decided': row['input']['decided'] = False
            elif kind == 'summary': row['expected']['hasRangedAbility'] = False
            elif kind == 'reach': row['expected']['enemyReach'] = row['input']['move'] + 4
            elif kind == 'rng': row['expected']['refreshRng'][1] -= 1
            elif kind == 'branch': altered['vectors'][3]['expected']['branch'] = 'InReach'
            else: altered['productionImplemented'] = True
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                validate(altered)
