import copy
import json
import unittest
from verify_ai_pair_out_of_reach_contract import FIXTURE, describe, validate


class AiPairOutOfReachContractTests(unittest.TestCase):
    def test_authored_mask_provenance_order_and_RNG_literals_preserve_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); validate(fixture)
        for row in fixture['vectors']:
            with self.subTest(vector=row['id']):
                before = copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], describe(row['input'])); self.assertEqual(before, row['input'])

    def test_unknown_actor_priority_is_not_zero_and_malformed_envelopes_reject(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        row = next(r for r in fixture['vectors'] if r['id'] == 'unknown-actor-cell-remains-unknown')
        self.assertTrue(describe(row['input'])['unknownPriorityRead'])
        zero = copy.deepcopy(row['input']); zero['retainedActorPriority'] = 0
        self.assertFalse(describe(zero)['unknownPriorityRead']); self.assertIsNotNone(describe(zero)['firstThrowOrigin'])
        for key, value in [('retainedActorPriority', 32768), ('move', -1), ('movementDrawsPerTile', 2)]:
            altered = copy.deepcopy(row['input']); altered[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError): describe(altered)
        duplicate = copy.deepcopy(row['input']); duplicate['reachable'].append(duplicate['reachable'][0])
        with self.assertRaises(ValueError): describe(duplicate)

    def test_tampered_zero_halving_order_and_execution_claim_reject(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('zero', 'order', 'claim'):
            altered = copy.deepcopy(fixture)
            if kind == 'zero':
                next(r for r in altered['vectors'] if r['id'] == 'unknown-actor-cell-remains-unknown')['expected']['actorStoredPriority'] = 0
            elif kind == 'order': altered['vectors'][0]['expected']['reachRoll']['raw'] = 16838
            else: altered['productionImplemented'] = True
            with self.subTest(kind=kind), self.assertRaises(ValueError): validate(altered)
