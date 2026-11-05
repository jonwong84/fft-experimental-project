import copy
import json
import unittest
from verify_roster_attack_contract import FIXTURE,describe,validate


class RosterAttackContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_ten_literal_tapes_preserve_inputs(self):
        before=copy.deepcopy(self.fixture)
        self.assertEqual(10,validate(self.fixture)['rosterAttackDescriptions']);self.assertEqual(before,self.fixture)

    def test_map_order_restore_and_unread_priority_boundaries(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertEqual(['c','b'],rows['target-panels-map-order-not-source-slot-order']['targets'])
        mixed=rows['all-origins-continue-after-success-and-zero-accuracy']
        self.assertEqual(['Scored','NotValuable','Scored'],[c['disposition'] for c in mixed['candidates']]);self.assertEqual(0,mixed['best'])
        self.assertEqual(1,rows['each-hypothesis-restores-killed-target-before-next-origin']['best'])

    def test_tampering_and_unsupported_sword_width_reject(self):
        f=copy.deepcopy(self.fixture);f['productionImplemented']=True
        with self.assertRaises(ValueError):validate(f)
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['candidates'][0]['order']='MoveThenAct'
        with self.assertRaises(ValueError):validate(f)
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['power']=65;d['units'][0]['pa']=255
        with self.assertRaises(ValueError):describe(d)


if __name__=='__main__':unittest.main()
