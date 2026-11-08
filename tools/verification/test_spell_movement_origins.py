import copy
import json
import unittest
from verify_spell_movement_origins import FIXTURE, baseline, finalize, masks, origin_order, policy, trace, validate


class SpellMovementOriginsTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_all_literal_source_descriptions_preserve_the_input(self):
        document=self.fixture();before=copy.deepcopy(document)
        self.assertEqual(validate(document)['total'],24)
        self.assertEqual(document,before)

    def test_small_and_wrapped_products_still_choose_ranked_scan(self):
        for aims,origins in [(1,1),(16,16),(255,255)]:
            result=policy(dict(exhaustiveOrigins=False,consumeEvaluatedAims=False,aimTiles=aims,originTiles=origins))
            self.assertTrue(result['ranked']);self.assertTrue(result['mayVisitOrigins'])
        self.assertFalse(policy(dict(exhaustiveOrigins=False,consumeEvaluatedAims=False,aimTiles=256,originTiles=1))['mayVisitOrigins'])
        self.assertTrue(policy(dict(exhaustiveOrigins=True,consumeEvaluatedAims=True,aimTiles=256,originTiles=256))['mayVisitOrigins'])

    def test_ranked_coordinate_ties_reverse_scan_order_while_exhaustive_ignores_keys(self):
        ranked=self.fixture()['orders'][1]
        self.assertEqual(origin_order(ranked['input']),['d','c','b','a'])
        data=copy.deepcopy(ranked['input']);data['exhaustiveOrigins']=True
        self.assertEqual(origin_order(data),['a','b','c','d'])

    def test_origin_destination_word_equality_is_not_just_xy(self):
        row=self.fixture()['destinations'][0]['input'];self.assertTrue(finalize(row)['actBeforeMove'])
        data=copy.deepcopy(row);data['chosenOrigin'][3]=1
        self.assertFalse(finalize(data)['actBeforeMove'])
        self.assertEqual(finalize(data)['destination'],[1,1,0,1])

    def test_baseline_rejects_proximity_only_and_preserves_negation_width(self):
        rows=self.fixture()['baselines']
        self.assertEqual(baseline(rows[0]['input'])['priority'],-32768)
        self.assertFalse(baseline(rows[1]['input'])['passesBaseline'])
        self.assertTrue(baseline(rows[2]['input'])['passesBaseline'])

    def test_walkable_aims_and_reachable_origins_are_distinct_masks(self):
        result=masks(self.fixture()['masks'][0]['input'])
        self.assertEqual(result['castOrigins'],[[2,0,0]])
        self.assertEqual(result['aims'],[[4,0,0]])
        result=masks(self.fixture()['masks'][1]['input'])
        self.assertNotIn([3,1,0],result['aims']);self.assertIn([3,1,1],result['aims'])

    def test_consumption_and_unresolved_exits_preserve_one_rng_across_origins(self):
        rows=self.fixture()['traces']
        self.assertEqual(trace(rows[0]['input'])['calls'],[['a','p'],['b','q']])
        self.assertEqual(trace(rows[1]['input'])['remainingAims'],['q'])
        result=trace(rows[2]['input'])
        self.assertEqual(result['calls'],[['a','p'],['b','p'],['b','q']])
        self.assertEqual(result['stoppedOrigins'],['a']);self.assertEqual(result['seed'],1800344689)

    def test_malformed_policy_numeric_widths_and_changed_tapes_are_rejected(self):
        for change in [lambda d:d['orders'][1]['expected'].reverse(),
                       lambda d:d['traces'][0]['input']['origins'][0]['candidates'][0].update(draws=0),
                       lambda d:d['policies'][0]['input'].update(exhaustiveOrigins=0),
                       lambda d:d['orders'][0]['input']['origins'][0].update(priority=-32769),
                       lambda d:d['baselines'][0]['input']['candidate'].update(hit=256),
                       lambda d:d['masks'][0]['input'].update(range=8)]:
            document=self.fixture();change(document)
            with self.assertRaises(ValueError):validate(document)
