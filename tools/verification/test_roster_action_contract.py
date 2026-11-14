import copy
import json
import unittest
from verify_roster_action_contract import FIXTURE,describe,validate


class RosterActionContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_literal_descriptions_preserve_inputs(self):
        before=copy.deepcopy(self.fixture)
        self.assertEqual(19,validate(self.fixture)['rosterActionDescriptions']);self.assertEqual(before,self.fixture)

    def test_ally_coin_and_early_hit_accounting_boundaries(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertFalse(rows['actor-last-receives-third-allied-coin']['selfUseful'])
        other=rows['other-ally-useful-self-candidates-reject-before-clock']
        self.assertEqual(14,other['count']);self.assertEqual(['NotValuable']*2,[a['disposition'] for a in other['attempts']])
        cap=rows['pa99-keeps-stat-hit-credit']['attempts'][0]
        self.assertEqual((99,99,488),(cap['before'],cap['after'],cap['priority']))

    def test_tampered_descriptions_and_invalid_masks_reject(self):
        f=copy.deepcopy(self.fixture);f['productionImplemented']=True
        with self.assertRaises(ValueError):validate(f)
        f=copy.deepcopy(self.fixture);f['vectors'][-1]['expected']['count']=0
        with self.assertRaises(ValueError):validate(f)
        d=copy.deepcopy(next(r['input'] for r in self.fixture['vectors'] if r['kind']=='usefulness'));d['active']=['a','a']
        with self.assertRaises(ValueError):describe('usefulness',d)


if __name__=='__main__':unittest.main()
