import copy,json,unittest
from verify_roster_turn_contract import FIXTURE,describe,validate


class RosterTurnContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_twelve_source_compositions_preserve_inputs(self):
        before=copy.deepcopy(self.fixture)
        self.assertEqual(12,validate(self.fixture)['rosterCommandTurnDescriptions'])
        self.assertEqual(before,self.fixture)

    def test_actual_ko_boundary_retains_accepted_progress_but_move_first_settles(self):
        rows={r['id']:r for r in self.fixture['vectors']}
        act=rows['accepted-ACT-first-KO-retained-at-deferred-corpse-guard']
        e=describe(act['input'],True)
        self.assertEqual('DeferredCorpseInput',e['failure']);self.assertEqual(['Attack'],e['commands'])
        self.assertTrue(e['active']);self.assertEqual(0,e['units'][1]['hp']);self.assertTrue(e['workspace'])
        move=rows['Move-first-KO-finishes-via-real-receipt']
        e=describe(move['input'],True)
        self.assertIsNone(e['failure']);self.assertEqual(['Move','RetainedStone','FinishFacing'],e['commands'])
        self.assertFalse(e['active']);self.assertFalse(e['pending']);self.assertEqual(1,len(e['decisions']))

    def test_tampered_actual_rng_or_private_workspace_is_detected(self):
        for key in ('count','workspace'):
            f=copy.deepcopy(self.fixture)
            if key=='count':f['vectors'][0]['expected'][key]+=1
            else:f['vectors'][0]['expected'][key]=[]
            with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
