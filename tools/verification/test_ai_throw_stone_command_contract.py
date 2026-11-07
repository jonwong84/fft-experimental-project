import copy
import json
import unittest
from verify_ai_throw_stone_command_contract import FIXTURE, execution, validate


class AiThrowStoneCommandContractTests(unittest.TestCase):
    def test_authored_execution_draws_damage_push_and_rollback_literals_preserve_input(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); validate(fixture)
        for row in fixture['vectors']:
            with self.subTest(vector=row['id']):
                before = copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], execution(row['input'])); self.assertEqual(before, row['input'])

    def test_execution_does_not_reuse_midpoint_or_skip_lethal_push_draws_and_exact_budget_fits(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        normal = fixture['vectors'][0]['input']; lethal = dict(normal, hp=5)
        self.assertEqual(5, execution(normal)['outcome']['damage'])
        self.assertEqual(7, len(execution(lethal)['outcome']['raw']))
        self.assertEqual(normal['target'], execution(lethal)['outcome']['destination'])
        self.assertTrue(execution(dict(normal, count=0xffffffffffffffff - 7))['accepted'])
        self.assertEqual(dict(accepted=False, rng=[normal['word'], 0xffffffffffffffff - 6], outcome=None),
                         execution(dict(normal, count=0xffffffffffffffff - 6)))
        for change in [dict(pa=0), dict(count=-1), dict(actor=[0, 0], target=[7, 7]), dict(targetFacing='Unknown')]:
            with self.subTest(change=change), self.assertRaises(ValueError): execution(dict(normal, **change))

    def test_tampered_midpoint_execution_claim_and_missing_draw_reject(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('midpoint', 'draw', 'claim'):
            altered = copy.deepcopy(fixture)
            if kind == 'midpoint': altered['vectors'][0]['expected']['outcome']['damage'] = 10
            elif kind == 'draw': altered['vectors'][0]['expected']['outcome']['raw'].pop()
            else: altered['productionImplemented'] = True
            with self.subTest(kind=kind), self.assertRaises(ValueError): validate(altered)
