import copy,json,unittest
from verify_roster_execution_contract import FIXTURE,describe,validate


class RosterExecutionContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_eight_actual_self_source_vectors_preserve_input(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(8,validate(self.fixture)['rosterSelfSettlementDescriptions']);self.assertEqual(before,self.fixture)

    def test_cap_and_unused_move_refund_have_no_random_or_peer_work(self):
        a=describe(dict(pa=99,ct=85,moved=False));self.assertEqual((99,85,60,False,0),(a['pa'],a['actCt'],a['endCt'],a['paChanged'],a['draws']))
        b=describe(dict(pa=5,ct=15,moved=True));self.assertTrue(b['pendingAfterAct']);self.assertEqual(15,b['endCt']);self.assertFalse(b['peersChanged'])

    def test_bad_numeric_facts_and_tampered_expectations_reject(self):
        for pa in (0,100):
            with self.assertRaises(ValueError):describe(dict(pa=pa,ct=15,moved=False))
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['draws']=1
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
