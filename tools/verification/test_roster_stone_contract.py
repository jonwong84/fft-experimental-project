import copy,json,unittest
from verify_roster_stone_contract import FIXTURE,describe_candidate,describe_search,validate


class RosterStoneContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_literal_candidates_and_control_flow_preserve_input(self):
        before=copy.deepcopy(self.fixture);r=validate(self.fixture)
        self.assertEqual((14,4),(r['rosterStoneCandidates'],r['rosterStoneSearches']));self.assertEqual(before,self.fixture)

    def test_blocked_retry_and_zero_accuracy_consumption(self):
        rows={r['id']:r['expected'] for r in self.fixture['search']}
        self.assertEqual(['c','b'],rows['last-origin-tie-map-aim-order-and-consume-zero']['consumed'])
        self.assertEqual(['Scored','Blocked','Scored'],[e['result'] for e in rows['blocked-aim-remains-for-later-origin']['events']])
        self.assertEqual(0,rows['all-blocked-exhausts-origins-without-consuming-aim']['draws'])
        self.assertEqual(1,rows['all-blocked-exhausts-origins-without-consuming-aim']['remainingAims'])

    def test_missing_clearance_bad_numeric_and_tampered_expectation_reject(self):
        d=copy.deepcopy(self.fixture['candidates'][0]['input']);d['clearTrajectory']=False
        with self.assertRaises(ValueError):describe_candidate(d)
        d['clearTrajectory']=True;d['units'][0]['pa']=0
        with self.assertRaises(ValueError):describe_candidate(d)
        d=copy.deepcopy(self.fixture['search'][0]['input']);d['clearance']={}
        with self.assertRaises(ValueError):describe_search(d)
        f=copy.deepcopy(self.fixture);f['search'][0]['expected']['draws']+=1
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
