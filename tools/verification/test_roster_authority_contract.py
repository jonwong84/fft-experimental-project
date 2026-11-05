import copy,json,unittest
from verify_roster_authority_contract import FIXTURE,advance,describe,validate


class RosterAuthorityContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_twenty_one_labelled_contracts_preserve_inputs(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(21,validate(self.fixture)['rosterAuthorityDescriptions']);self.assertEqual(before,self.fixture)

    def test_dispatch_and_route_precedence_are_separate_from_checkpoint_contracts(self):
        self.assertEqual('Move',describe('source-dispatch',dict(moved=False,acted=True,action=False,actFirst=True,origin=[1,1],destination=[0,1])))
        self.assertEqual('Act',describe('source-dispatch',dict(moved=True,acted=False,action=True,actFirst=False,origin=[1,1],destination=[0,1])))
        self.assertEqual('FinishFacing',describe('managed-turn-route',dict(continuation=False,pending=True,active=False)))
        self.assertEqual(3655513600,advance(1,7))

    def test_bad_unsigned_checkpoint_or_tampered_expected_result_reject(self):
        with self.assertRaises(ValueError):describe('managed-rng-checkpoint',dict(beforeWord=1,afterWord=1,beforeCount=0,afterCount=-1))
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']='End'
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
