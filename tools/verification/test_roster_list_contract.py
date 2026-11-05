import copy,json,unittest
from verify_roster_list_contract import FIXTURE,describe,validate


class RosterListContractTests(unittest.TestCase):
    def setUp(self):self.fixture=json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_twelve_full_list_tapes_preserve_inputs(self):
        before=copy.deepcopy(self.fixture);self.assertEqual(12,validate(self.fixture)['rosterListDescriptions']);self.assertEqual(before,self.fixture)

    def test_capacity_ties_lottery_and_post_act_empty_ranking(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        cap=rows['eight-slots-stable-ties-drop-later-duplicates'];self.assertEqual(list(range(1,9)),[r['occurrence'] for r in cap['ranked']])
        self.assertEqual('AllRollsFailedUseFirst',rows['partial-hit-all-rolls-fail-keeps-first']['reason'])
        self.assertEqual(1,rows['partial-hit-later-ranked-slot-wins']['selected']);self.assertEqual(8,len(rows['post-act-empty-ranking-still-eight-lottery-draws']['rolls']))

    def test_source_order_unknown_rollback_and_tampered_draws(self):
        rows={r['id']:r['expected'] for r in self.fixture['vectors']}
        self.assertEqual(2,rows['self-coins-between-attack-and-stone']['selectedOccurrence']);self.assertEqual(1,rows['source-reordered-stone-before-self-coins']['selectedOccurrence'])
        self.assertEqual(7,rows['late-unknown-stone-key-discards-earlier-entry-stream']['count'])
        f=copy.deepcopy(self.fixture);f['vectors'][0]['expected']['lotteryCount']+=1
        with self.assertRaises(ValueError):validate(f)


if __name__=='__main__':unittest.main()
