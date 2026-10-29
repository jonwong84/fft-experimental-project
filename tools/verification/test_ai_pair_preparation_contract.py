import copy
import json
import unittest
from verify_ai_pair_preparation_contract import FIXTURE, validate


class AiPairPreparationContractTests(unittest.TestCase):
    def test_literal_initialization_usage_reach_and_distance_floor_descriptions(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); before = copy.deepcopy(fixture)
        validate(fixture); self.assertEqual(before, fixture)

    def test_malformed_or_tampered_descriptions_and_execution_claim_reject(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('speed', 'modifier', 'range', 'claim'):
            altered = copy.deepcopy(original); row = altered['vectors'][0]
            if kind == 'speed': row['input']['speed'] = 0
            elif kind == 'modifier': row['expected']['evadeModifier'] = 4
            elif kind == 'range': row['expected']['enemyReach'] = 7
            else: altered['productionImplemented'] = True
            with self.subTest(kind=kind), self.assertRaises(ValueError): validate(altered)
