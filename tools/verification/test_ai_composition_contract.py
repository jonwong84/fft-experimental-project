"""Isolated source-derived handoff literals; ZOD-138 managed proposals remain unexecuted."""
import copy
import unittest

from verify_ai_composition_contract import MAX, source_handoff, source_schedule


class AiCompositionContractTests(unittest.TestCase):
    def test_actor_is_added_after_movement_scoring_not_rescored_as_an_extra_cell(self):
        origins = [[1, 2], [1, 0], [0, 1]]
        before = copy.deepcopy(origins)
        result = source_handoff(origins, [2, 1], [1, 2], [1, 0])
        self.assertEqual([[1, 0], [0, 1], [1, 2]], result['scoredOrigins'])
        self.assertEqual([[1, 0], [0, 1], [2, 1], [1, 2]], result['attackOrigins'])
        self.assertEqual(2, result['baselineIndex'])
        self.assertEqual(0, result['additionalDraws'])
        self.assertEqual(before, origins)
        self.assertEqual(result, source_handoff(list(reversed(origins)), [2, 1], [1, 2], [1, 0]))

    def test_actor_already_in_scoring_mask_is_not_duplicated_in_attack_mask(self):
        result = source_handoff([[2, 1]], [2, 1], [2, 1], [2, 1])
        self.assertEqual([[2, 1]], result['scoredOrigins'])
        self.assertEqual(result['scoredOrigins'], result['attackOrigins'])

    def test_new_actor_bit_does_not_prove_fresh_baseline_or_preselected_destination(self):
        for origins, baseline, destination in (([], [1, 0], [1, 0]),
                ([[1, 0], [1, 0]], [1, 0], [1, 0]),
                ([[1, 0]], [2, 1], [1, 0]), ([[1, 0]], [1, 0], [2, 1])):
            with self.subTest(origins=origins, baseline=baseline, destination=destination):
                with self.assertRaises(ValueError): source_handoff(origins, [2, 1], baseline, destination)

    def test_all_movement_draws_precede_attack_and_cached_baseline_adds_none(self):
        # Three source movement cells, then four supplied five-draw Attack evaluations.
        result = source_schedule(1, 7, 3, [5, 5, 5, 5])
        self.assertEqual(dict(rngWord=662824084, rngCount=10), result['outcome']['scanEntry'])
        self.assertEqual((1343201072, 30), (result['rngWord'], result['rngCount']))
        self.assertEqual((0, 0), (result['outcome']['baselineSeedDraws'], result['outcome']['insertionDraws']))

    def test_no_attack_candidates_still_retains_every_movement_draw_at_exact_MAX(self):
        result = source_schedule(1, MAX - 3, 3, [])
        self.assertTrue(result['accepted'])
        self.assertEqual((662824084, MAX), (result['rngWord'], result['rngCount']))

    def test_movement_and_late_attack_failure_discard_private_prefix_and_restore_original_RNG(self):
        for remaining, stage, index in ((2, 'Movement', 2), (7, 'Attack', 0), (12, 'Attack', 1)):
            with self.subTest(remaining=remaining):
                result = source_schedule(1, MAX - remaining, 3, [5, 5])
                self.assertFalse(result['accepted']); self.assertIsNone(result['outcome'])
                self.assertEqual((stage, index), (result['failedStage'], result['failedIndex']))
                self.assertEqual((1, MAX - remaining), (result['rngWord'], result['rngCount']))


if __name__ == '__main__':
    unittest.main()
