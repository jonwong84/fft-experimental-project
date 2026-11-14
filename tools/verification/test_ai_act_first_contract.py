"""Offline ACT-first/source descriptions; actual managed execution belongs to ZOD-177."""
import copy
import json
import unittest

from verify_ai_act_first_contract import FIXTURE, act_first_tape, post_act_facts, validate
from verify_ai_command_continuation_contract import dispatch


class AiActFirstContractTests(unittest.TestCase):
    def test_authored_lifecycle_and_supplied_tapes_preserve_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        validate(fixture)
        for family, calculate in [('dispatchVectors', dispatch), ('postActVectors', post_act_facts), ('tapes', act_first_tape)]:
            for row in fixture[family]:
                with self.subTest(vector=row['id']):
                    before = copy.deepcopy(row['input'])
                    self.assertEqual(row['expected'], calculate(row['input']))
                    self.assertEqual(before, row['input'])

    def test_post_ACT_has_no_action_refund_but_keeps_reach_computed_before_usable_is_cleared(self):
        facts = post_act_facts(dict(ct=15, move=1, moved=False, acted=True))
        self.assertEqual((35, 15, 35, 2), (facts['refreshEndCt'], facts['movingEndCt'], facts['stayingEndCt'], facts['enemyReach']))
        self.assertFalse(facts['attackUsable']); self.assertTrue(facts['baselineCopiesAttackDestination'])
        self.assertEqual(60, post_act_facts(dict(ct=95, move=124, moved=False, acted=True))['refreshEndCt'])

    def test_post_ACT_description_rejects_wrong_choices_and_invalid_source_envelope(self):
        data = dict(ct=15, move=1, moved=False, acted=True)
        for change in [dict(moved=True), dict(acted=False), dict(ct=100), dict(ct=-1), dict(move=125)]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                post_act_facts(dict(data, **change))


if __name__ == '__main__':
    unittest.main()
