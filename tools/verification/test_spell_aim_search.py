import copy
import json
import unittest
from verify_spell_aim_search import FIXTURE, aim_panels, rank_description, targeting_passes, validate


class SpellAimSearchTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_literal_source_descriptions(self):
        result = validate(self.fixture())
        self.assertEqual((result['aimMaskDescriptions'], result['targetingPassDescriptions'], result['rankDescriptions']), (4, 4, 7))

    def test_followed_unit_and_tile_center_are_distinct(self):
        masks = self.fixture()['masks']
        self.assertEqual(aim_panels(masks[0]['input']), [[2, 1, 0]])
        self.assertNotIn([2, 1, 0], aim_panels(masks[1]['input']))
        self.assertIn([2, 1, 0], aim_panels(masks[2]['input']))

    def test_strict_ties_and_baseline_admission_use_different_keys(self):
        tied = dict(candidate=dict(priority=5, hit=100, proximity=2), incumbent=dict(priority=5, hit=100, proximity=2))
        self.assertFalse(rank_description(tied)['higherWithProximity'])
        tied['candidate']['proximity'] = 1
        self.assertEqual(rank_description(tied), dict(higherWithProximity=True, beatsMovementBaseline=False))

    def test_unit_passes_precede_tile_pass_and_zero_area_stops_after_units(self):
        data = dict(follow=True, consume=False, area=0)
        self.assertEqual(targeting_passes(data), ['unit-moves-by-deadline', 'unit-still-at-deadline'])
        data['area'] = 1
        self.assertEqual(targeting_passes(data)[-1], 'tile')

    def test_radius_table_overread_is_not_silently_normalized(self):
        data = self.fixture()['masks'][0]['input']
        data['area'] = 8
        data['targeting'] = 5
        with self.assertRaises(ValueError):
            aim_panels(data)

    def test_mutations_are_detected_and_description_preserves_input(self):
        original = self.fixture()
        before = copy.deepcopy(original)
        validate(original)
        self.assertEqual(before, original)
        for field in ('masks', 'passes', 'ranks'):
            changed = copy.deepcopy(original)
            changed[field][0]['expected'] = None
            with self.assertRaises(ValueError):
                validate(changed)
