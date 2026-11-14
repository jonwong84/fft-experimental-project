"""Source arithmetic proposals only; ZOD-172 will exercise managed unmoved preparation."""
import copy
import json
import unittest

from verify_ai_preparation_contract import prepare
from verify_ai_refresh_contract import refresh_clock
from verify_ai_unmoved_preparation_contract import FIXTURE, refresh_unmoved, reachable_unmoved, validate


class AiUnmovedPreparationContractTests(unittest.TestCase):
    def test_source_vectors_preserve_input_data_and_replay(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        validate(fixture)
        for family, calculate in [('clockVectors', refresh_unmoved), ('reachVectors', reachable_unmoved)]:
            for row in fixture[family]:
                with self.subTest(vector=row['id']):
                    before = copy.deepcopy(row['input'])
                    self.assertEqual(row['expected'], calculate(row['input']))
                    self.assertEqual(before, row['input'])
                    self.assertEqual(calculate(row['input']), calculate(row['input']))

    def test_unused_Move_changes_hypothetical_pick_but_policy_uses_restored_live_CT(self):
        data = json.loads(FIXTURE.read_text(encoding='utf-8'))['clockVectors'][1]['input']
        hypothetical = refresh_unmoved(data)
        self.assertEqual(('a', 5, {'a': 5, 'b': 90}),
                         (hypothetical['picked'], hypothetical['elapsed'], hypothetical['ct']))
        # Applying the old already-moved refund would pick the peer instead.
        self.assertEqual('b', refresh_clock(data)['picked'])
        policy = prepare(15, 10, 40, 10, 1, 2, False)
        self.assertEqual((9, 6, 8), (policy['actorDelay'], policy['targetDelay'], policy['proximity']))
        self.assertNotEqual(policy['actorDelay'], prepare(5, 10, 90, 10, 1, 2, False)['actorDelay'])
        self.assertEqual(0, policy['extraDraws'])

    def test_invalid_entry_counters_and_raw_Move_are_rejected(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        data = fixture['clockVectors'][0]['input']
        for change in [dict(moved=True), dict(acted=True), dict(count=-1), dict(word=1 << 32), dict(ticks=1 << 64)]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                refresh_unmoved(dict(data, **change))
        reach = fixture['reachVectors'][0]['input']
        for change in [dict(moved=True), dict(move=125), dict(move=-1)]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                reachable_unmoved(dict(reach, **change))


if __name__ == '__main__':
    unittest.main()
