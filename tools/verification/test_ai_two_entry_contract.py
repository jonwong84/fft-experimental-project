import copy
import json
import unittest
from verify_ai_two_entry_contract import FIXTURE, describe, validate


class AiTwoEntryContractTests(unittest.TestCase):
    def test_literal_rank_and_supplied_RNG_ledger_preserve_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); validate(fixture)
        for row in fixture['vectors']:
            with self.subTest(vector=row['id']):
                before = copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], describe(row['input']))
                self.assertEqual(before, row['input'])

    def test_malformed_order_or_cost_and_tampered_claim_reject(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('order', 'cost', 'claim', 'literal'):
            altered = copy.deepcopy(original)
            if kind == 'order': altered['vectors'][0]['input']['entries'].reverse()
            elif kind == 'cost': altered['vectors'][0]['input']['prefixDraws'] = -1
            elif kind == 'claim': altered['productionImplemented'] = True
            else: altered['vectors'][0]['expected']['ranked'].reverse()
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                validate(altered)
