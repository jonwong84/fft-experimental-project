import copy
import json
import unittest

from verify_ai_accumulate_contract import FIXTURE, describe, validate


class AccumulateSourceDescriptionTests(unittest.TestCase):
    def test_literal_descriptions_and_input_preservation(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        before = copy.deepcopy(fixture)
        validate(fixture)
        self.assertEqual(before, fixture)

    def test_cap_does_not_remove_usefulness_or_stat_hit_credit(self):
        self.assertEqual({'effect': 'Useful', 'draws': 1},
                         describe('usefulness', dict(gate='EligibleAlly', pa=99, raw=0)))
        self.assertFalse(describe('pa', dict(base=99, bonus=0, effective=99))['changed'])
        units = [dict(enemy=False, hp=50, maxHp=50), dict(enemy=True, hp=50, maxHp=50)]
        self.assertEqual(3, describe('score', dict(units=units))['priority'])

    def test_wrong_coin_cap_score_rank_or_execution_claim_rejects(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('usefulness', 'pa', 'score', 'rank', 'claim'):
            changed = copy.deepcopy(original)
            if kind == 'claim':
                changed['productionImplemented'] = True
            else:
                row = next(r for r in changed['vectors'] if r['kind'] == kind)
                key = {'usefulness': 'draws', 'pa': 'base', 'score': 'priority',
                       'rank': 'beatsMovementBaseline'}[kind]
                row['expected'][key] = 'incorrect'
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                validate(changed)

    def test_early_return_cannot_claim_a_coin(self):
        for gate in ('Enemy', 'Blocked'):
            with self.subTest(gate=gate), self.assertRaises(ValueError):
                describe('usefulness', dict(gate=gate, pa=5, raw=0))

    def test_invalid_numeric_domains_reject(self):
        for kind, value in [('usefulness', dict(gate='EligibleAlly', pa=5, raw=32768)),
                            ('pa', dict(base=100, bonus=0, effective=99)),
                            ('pa', dict(base=True, bonus=0, effective=1)),
                            ('rank', dict(candidate=[32768, 100, 0], baseline=[0, 100, 0]))]:
            with self.subTest(kind=kind, value=value), self.assertRaises(ValueError):
                describe(kind, value)
