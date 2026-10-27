"""Source-derived literal kernels only; proposed ZOD-136 C# cases are unexecuted."""
import copy
import unittest

from verify_ai_candidate_contract import first_clock
from verify_ai_decision_contract import calculate
from verify_ai_rank_admission_contract import baseline_rank, insert_best, move_only_end_ct


def action(name, priority, hit=100, proximity=10):
    return dict(id=name, decided=0, priority=priority, hit=hit, proximity=proximity,
                payload=dict(command=name, coords=[2, 1, 0]), waitFacing=2, reflected=True)


def empty_list():
    # Payload is deliberately stale; the supported score envelope beats its sentinel.
    return [action('stale', -32767, hit=0) for _ in range(8)]


class AiRankAdmissionContractTests(unittest.TestCase):
    def test_negated_tile_patterns_round_trip_including_signed_minimum(self):
        for score, pattern in ((0, 0), (160, 65376), (-160, 160), (32767, 32769), (-32768, 32768)):
            with self.subTest(score=score):
                self.assertEqual(dict(decided=0, priority=score, hit=100), baseline_rank(pattern))

    def test_equal_baseline_rejects_even_with_better_proximity(self):
        candidate = action('equal', 0, proximity=0)
        result = insert_best(candidate, baseline_rank(0), empty_list())
        self.assertIsNone(result['insertedIndex'])
        candidate['priority'] = 1
        candidate['hit'] = 1
        self.assertEqual(0, insert_best(candidate, baseline_rank(0), empty_list())['insertedIndex'])

    def test_absence_zero_and_negative_are_distinct(self):
        baseline = baseline_rank(11)  # -11, not an implicit zero baseline.
        self.assertIsNone(insert_best(None, baseline, empty_list())['insertedIndex'])
        for priority in (0, -10):
            self.assertEqual(0, insert_best(action('real', priority), baseline, empty_list())['insertedIndex'])

    def test_full_tail_tie_rejects_and_lower_proximity_displaces(self):
        ranked = [action(str(i), 8-i) for i in range(8)]
        self.assertIsNone(insert_best(action('tie', 1), baseline_rank(0), ranked)['insertedIndex'])
        result = insert_best(action('closer', 1, proximity=9), baseline_rank(0), ranked)
        self.assertEqual(7, result['insertedIndex'])
        self.assertEqual('closer', result['ranked'][-1]['id'])

    def test_stable_ties_allow_duplicate_payload_and_copy_full_record(self):
        ranked = empty_list()
        candidate = action('same', 5)
        ranked = insert_best(candidate, baseline_rank(0), ranked)['ranked']
        result = insert_best(candidate, baseline_rank(0), ranked)
        self.assertEqual(1, result['insertedIndex'])
        self.assertEqual([candidate, candidate], result['ranked'][:2])
        candidate['payload']['coords'][0] = 9
        self.assertEqual(2, result['ranked'][1]['payload']['coords'][0])

    def test_every_attempt_resets_only_rank_and_selection_defaults_without_rng(self):
        candidate = action('payload', 5)
        before = copy.deepcopy(candidate)
        for baseline in (baseline_rank(0), baseline_rank(65526)):
            ranked = empty_list()
            snapshot = copy.deepcopy(ranked)
            result = insert_best(candidate, baseline, ranked)
            self.assertEqual(dict(decided=0, priority=-32767, hit=0, waitFacing=5, reflected=False),
                             {key: result['resetBest'][key] for key in ('decided', 'priority', 'hit', 'waitFacing', 'reflected')})
            self.assertEqual(candidate['payload'], result['resetBest']['payload'])
            self.assertEqual(0, result['additionalDraws'])
            self.assertEqual(snapshot, ranked)
        self.assertEqual(before, candidate)

    def test_move_only_refunds_can_change_first_ready_unit_but_add_no_formula_draws(self):
        self.assertEqual([50, 30, 30, 60], [move_only_end_ct(10, False, False),
            move_only_end_ct(10, False, True), move_only_end_ct(10, True, False), move_only_end_ct(100, False, False)])
        # Same original CT=10; skipping ACT earns 20. Attack at 10 loses this race.
        for ended, expected in ((move_only_end_ct(10, False, True), 0), (10, 1)):
            result = first_clock(dict(units=[dict(slot=0, ct=ended, speed=10, status='None'),
                dict(slot=1, ct=20, speed=10, status='None')]))
            self.assertEqual(expected, result['readySlot'])
            self.assertEqual(1, result['upkeepDraws'])

    def test_baseline_is_absolute_hp_status_score_with_equipment_cancelled_after_scoring(self):
        units = [dict(hp=10, maxHp=100, status='Critical', keptEquipmentSlots=7, enemy=False),
                 dict(hp=100, maxHp=100, status='None', keptEquipmentSlots=7, enemy=True)]
        self.assertEqual(dict(priority=-148), calculate('state-score', dict(units=units)))


if __name__ == '__main__':
    unittest.main()
