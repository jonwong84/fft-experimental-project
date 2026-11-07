import copy,json,unittest
from verify_roster_live_stone_contract import FIXTURE,describe,validate


class RosterLiveStoneContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_thirteen_independent_live_tapes_preserve_inputs(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(13,validate(self.fixture)['rosterLiveStoneDescriptions']);self.assertEqual(before,self.fixture)

    def test_interception_landing_and_real_draw_counts_are_distinct_from_ai(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']};a=rows['ally-intercepts-intended-enemy'];self.assertEqual(('c',45,14),(a['target'],a['hp'],a['count']))
        self.assertEqual(11,rows['interceptor-facing-and-evade-own-formula']['count']);self.assertEqual([3,1],rows['occupied-landing-suppresses-push-not-draws']['destination'])
        ko=rows['lethal-byte-ko-wrap-cancels-push-after-brave-draws'];self.assertEqual((0,0,14),(ko['hp'],ko['ko'],ko['count']))

    def test_invalid_numeric_or_tampered_live_result_reject(self):
        d=copy.deepcopy(self.fixture['vectors'][0]['input']);d['units'][0]['pa']=0
        with self.assertRaises(ValueError):describe(d)
        f=copy.deepcopy(self.fixture);f['vectors'][2]['expected']['target']='b'
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
