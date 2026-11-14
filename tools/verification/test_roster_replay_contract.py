import copy,json,unittest
from verify_roster_replay_contract import FIXTURE,validate

class RosterReplayContractTests(unittest.TestCase):
    def test_eighteen_composed_turns_preserve_inputs(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));before=copy.deepcopy(f)
        self.assertEqual(18,validate(f)['turns']);self.assertEqual(before,f)

    def test_real_peer_pick_precedes_next_tick_and_later_turns_advance_clock(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));steps=f['vectors'][0]['expected']
        self.assertEqual(['a','b','c'],[s['actor'] for s in steps[:3]])
        self.assertEqual(0,steps[0]['clock']['ticks']);self.assertEqual(0,steps[1]['clock']['ticks']);self.assertEqual(17,steps[2]['clock']['ticks'])
        bad=copy.deepcopy(f);bad['vectors'][0]['expected'][0]['clock']['count']+=1
        with self.assertRaises(ValueError):validate(bad)

if __name__=='__main__':unittest.main()
