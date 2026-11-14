import copy
import json
import unittest
from verify_spell_aim_ranking import FIXTURE, trace, validate


class SpellAimRankingTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_source_trace_literals_and_input_isolation(self):
        document = self.fixture()
        before = copy.deepcopy(document)
        self.assertEqual(validate(document)['rankIterationDescriptions'], 4)
        self.assertEqual(document, before)

    def test_changed_rank_and_draw_order_are_detected(self):
        document = self.fixture()
        document['traces'][0]['passes'][1][0]['priority'] = 50
        with self.assertRaises(ValueError):
            validate(document)
        document = self.fixture()
        document['traces'][2]['passes'][0][0]['draws'] = 0
        with self.assertRaises(ValueError):
            validate(document)

    def test_unresolved_stops_its_pass_after_ranking_and_later_pass_still_runs(self):
        row = self.fixture()['traces'][3]
        result = trace(row['passes'])
        self.assertEqual(result['calls'], [[0, 0], [1, 0]])
        self.assertEqual(result['best'], [0, 0])
        self.assertEqual(result['draws'], 510)

    def test_signed_priority_precedes_hit_and_ordinary_hit255_is_rejected(self):
        candidates = [[dict(status='scored', priority=-10, hit=100, draws=0),
                       dict(status='scored', priority=-9, hit=1, draws=0)]]
        self.assertEqual(trace(candidates)['best'], [0, 1])
        candidates[0][0]['hit'] = 255
        with self.assertRaises(ValueError):
            trace(candidates)
