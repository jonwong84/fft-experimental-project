import copy
import json
import unittest
from verify_roster_preparation_contract import FIXTURE,validate


class RosterPreparationContractTests(unittest.TestCase):
    def test_literal_clock_team_and_score_descriptions_preserve_inputs(self):
        fixture=json.loads(FIXTURE.read_text(encoding='utf-8'));before=copy.deepcopy(fixture)
        self.assertEqual(13,validate(fixture)['rosterPreparationDescriptions']);self.assertEqual(before,fixture)

    def test_source_ties_policy_propagation_and_many_unit_scores(self):
        e={r['id']:r['expected'] for r in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']}
        self.assertEqual(0,e['refresh-ready-peer-before-tick-first-tie']['elapsed'])
        self.assertFalse(e['support-source-order-clears-other-ally']['mostly']['c'])
        self.assertFalse(e['support-retained-early-exit-does-not-propagate']['policies']['c'])
        self.assertEqual(5,e['preparation-multiple-enemy-threats-after-ACT']['harmful'])
        self.assertEqual(-485,e['movement-three-teams-all-enemies-score']['priority'])

    def test_tampering_invalid_facts_and_runtime_claims_reject(self):
        fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('runtime','expected','duplicate','speed','order','actions'):
            bad=copy.deepcopy(fixture)
            if kind=='runtime':bad['productionImplemented']=True
            elif kind=='expected':bad['vectors'][0]['expected']['cachedDelay']=8
            elif kind=='duplicate':bad['vectors'][1]['id']=bad['vectors'][0]['id']
            elif kind=='speed':bad['vectors'][0]['input']['units'][0]['speed']=0
            elif kind=='order':bad['vectors'][0]['input']['units'][1]['order']=0
            else:bad['vectors'][0]['input']['units'][0]['actions']=['Unknown']
            with self.subTest(kind=kind),self.assertRaises(ValueError):validate(bad)
