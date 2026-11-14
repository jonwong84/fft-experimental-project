import copy
import json
import unittest
from verify_roster_sword_contract import FIXTURE,describe,validate


class RosterSwordContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_thirteen_literal_descriptions_preserve_inputs(self):
        before=copy.deepcopy(self.fixture)
        self.assertEqual(13,validate(self.fixture)['rosterSwordDescriptions']);self.assertEqual(before,self.fixture)

    def test_corpse_clock_and_full_damage_boundaries(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertEqual(10,rows['half-front-hit-applies-full-normal-damage']['damage'])
        self.assertEqual(11,rows['certain-front-evade-stops-before-clock']['count'])
        stress=rows['fast-corpse-101-picks-retains-counter-three']
        self.assertEqual(101,stress['groups'][0]['count']);self.assertEqual(113,stress['count']);self.assertEqual(3,stress['units'][1]['counter'])

    def test_tampering_and_invalid_width_or_destination_reject(self):
        f=copy.deepcopy(self.fixture);f['productionImplemented']=True
        with self.assertRaises(ValueError):validate(f)
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['units'][-1]['hp']=40
        with self.assertRaises(ValueError):validate(f)
        d=copy.deepcopy(self.fixture['vectors'][-3]['input']);d['power']=65
        with self.assertRaises(ValueError):describe(d)
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['destination']=[2,1]
        with self.assertRaises(ValueError):describe(d)


if __name__=='__main__':unittest.main()
