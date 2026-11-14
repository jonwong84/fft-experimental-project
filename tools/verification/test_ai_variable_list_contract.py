import copy
import json
import unittest

from verify_ai_variable_list_contract import FIXTURE, describe, validate


class AiVariableListContractTests(unittest.TestCase):
    def test_literal_preparation_cursor_and_supplied_rank_tapes_preserve_input(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8')); before = copy.deepcopy(fixture)
        validate(fixture); self.assertEqual(before, fixture)

    def test_absent_self_duplicate_occurrences_support_override_and_terminal_context(self):
        rows = {r['id']: r['expected'] for r in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']}
        self.assertEqual([], rows['cursor-no-self']['selfPasses'])
        self.assertEqual([1, 2], rows['cursor-repeated-self']['selfPasses'])
        self.assertTrue(rows['summary-self-heavy']['firstPassMostlySupport'])
        self.assertFalse(rows['summary-self-heavy']['finalActorMostlySupport'])
        self.assertEqual('Accumulate', rows['cursor-no-stone']['terminalCommand'])
        self.assertEqual('stone-2', rows['duplicate-ranked-slots']['selected'])
        self.assertEqual(2, rows['duplicate-ranked-slots']['lotteryDraws'])

    def test_malformed_inputs_tampering_and_runtime_claims_reject(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('claim', 'share', 'duplicate-id', 'width', 'empty-list'):
            altered = copy.deepcopy(original)
            if kind == 'claim': altered['productionImplemented'] = True
            elif kind == 'share': altered['vectors'][0]['expected']['actorHelpfulShare'] = 64
            elif kind == 'duplicate-id': altered['vectors'][1]['id'] = altered['vectors'][0]['id']
            elif kind == 'width': altered['vectors'][0]['input']['move'] = True
            else: altered['vectors'][0]['input']['actor'] = []
            with self.subTest(kind=kind), self.assertRaises(ValueError): validate(altered)
        with self.assertRaises(ValueError): describe('cursor', dict(entries=['Attack', 'FrogAttack'], actionTaken=False,
            resuming=False, startIndex=1, suspendCurrent=False, currentCommand='None'))
