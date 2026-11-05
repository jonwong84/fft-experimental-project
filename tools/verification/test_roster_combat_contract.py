import copy
import json
import unittest
from verify_roster_combat_contract import FIXTURE,validate


class RosterCombatContractTests(unittest.TestCase):
    def test_literal_descriptions_preserve_source_inputs(self):
        data=json.loads(FIXTURE.read_text(encoding='utf-8')); before=copy.deepcopy(data)
        self.assertEqual(11,validate(data)['rosterCombatDescriptions']); self.assertEqual(before,data)

    def test_transit_occupancy_and_resolution_streams_remain_distinct(self):
        rows={r['id']:r['expected'] for r in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']}
        self.assertEqual([0,0,0],rows['ally-transit-empty-endpoint']['packed'])
        self.assertIsNone(rows['ally-cannot-be-endpoint']['path'])
        self.assertFalse(rows['critical-corpse-occupies-landing']['moved'])
        self.assertTrue(rows['critical-empty-landing']['moved'])
        self.assertEqual(7,rows['critical-ally-occupies-landing']['drawCount'])

    def test_tampered_claims_invalid_domains_and_duplicate_vectors_reject(self):
        data=json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('claim','expected','duplicate','team','position','byte'):
            bad=copy.deepcopy(data)
            if kind=='claim':bad['productionImplemented']=True
            elif kind=='expected':bad['vectors'][0]['expected']['path']=None
            elif kind=='duplicate':bad['vectors'][1]['id']=bad['vectors'][0]['id']
            elif kind=='team':bad['vectors'][0]['input']['units'][0]['team']=4
            elif kind=='position':bad['vectors'][0]['input']['units'][1]['tile']=[0,0]
            else:bad['vectors'][0]['input']['move']=True
            with self.subTest(kind=kind),self.assertRaises(ValueError):validate(bad)
