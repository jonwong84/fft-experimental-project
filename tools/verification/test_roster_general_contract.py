import copy
import json
import unittest
from verify_roster_general_contract import FIXTURE,describe,validate


class RosterGeneralContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_literal_source_descriptions_and_input_isolation(self):
        before=copy.deepcopy(self.fixture)
        self.assertEqual(18,validate(self.fixture)['rosterGeneralDescriptions'])
        self.assertEqual(before,self.fixture)

    def test_policy_order_and_mask_provenance_boundaries(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertEqual('ConditionalRetreat',rows['critical-retreat-precedes-fading-life']['rejection'])
        self.assertEqual('None',rows['retained-unavailable-overrides-mostly']['rejection'])
        outside=rows['out-of-reach-score-before-nonblue-draw']
        self.assertIn([1,1],outside['scenario1']);self.assertIn([1,1],outside['scenario2']);self.assertNotIn([1,1],outside['mask'])
        self.assertEqual(-1,rows['no-enemy-self-focus-zeroes-distance-grid']['bound'])

    def test_tampering_malformed_facts_and_runtime_claims_reject(self):
        changed=copy.deepcopy(self.fixture);changed['vectors'][-1]['expected']['priority']=0
        with self.assertRaises(ValueError):validate(changed)
        changed=copy.deepcopy(self.fixture);changed['productionImplemented']=True
        with self.assertRaises(ValueError):validate(changed)
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['units'][0]['move']=125
        with self.assertRaises(ValueError):describe(d)
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['units'][0]['tile']=[8,0]
        with self.assertRaises(ValueError):describe(d)


if __name__=='__main__':unittest.main()
