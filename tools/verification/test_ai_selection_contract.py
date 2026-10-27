"""Source literals and managed counter-boundary proposals; no C# selection execution."""
import copy
import unittest

from verify_ai_selection_contract import MAX, lottery_tail, source_fallback


class AiSelectionContractTests(unittest.TestCase):
    def test_one_positive_entry_is_selected_after_either_one_or_eight_draws(self):
        # Entry after six prefix draws from seed 1: next raw23010 % 100 = 10.
        first = lottery_tail(2516284547, 13, 11)
        later = lottery_tail(2516284547, 13, 10)
        self.assertEqual((0, 'FirstRoll', 1), tuple(first['outcome'][k] for k in ('selectedIndex','reason','draws')))
        self.assertEqual((3655513600,14),(first['rngWord'],first['rngCount']))
        self.assertEqual((0, 'AllRollsFailedUseFirst', 8), tuple(later['outcome'][k] for k in ('selectedIndex','reason','draws')))
        self.assertEqual((2937909499,21),(later['rngWord'],later['rngCount']))
        self.assertEqual(23010,first['outcome']['raw'][0])

    def test_absence_visits_all_eight_zero_hit_slots_before_movement_fallback(self):
        result = lottery_tail(1103527590, 8, None)  # One movement-prefix draw, inactive Attack.
        self.assertEqual((None,'NoRankedAction',8),tuple(result['outcome'][k] for k in ('selectedIndex','reason','draws')))
        self.assertEqual((3210001534,16),(result['rngWord'],result['rngCount']))
        with self.assertRaises(ValueError): lottery_tail(1, 0, 0)  # A scored zero priority is not zero accuracy.

    def test_exact_MAX_reserves_only_visited_draws_and_late_exhaustion_returns_no_prefix(self):
        self.assertEqual(MAX,lottery_tail(1,MAX-1,100)['rngCount'])
        self.assertEqual(MAX,lottery_tail(1,MAX-8,None)['rngCount'])
        for available in (0,3,7):
            with self.subTest(available=available):
                result = lottery_tail(1,MAX-available,38)  # First raw16838 % 100 equals hit38, so all eight are visited.
                self.assertFalse(result['accepted']);self.assertIsNone(result['outcome'])
                self.assertEqual(available,result['failedLotteryIndex'])
                self.assertEqual((1,MAX-available),(result['rngWord'],result['rngCount']))

    def test_fallback_writes_only_move_order_D0_and_skillset_without_scoring_or_clearing_stale_fields(self):
        selected = dict(actBeforeMove=True,coords=[9,9,0],rank=dict(priority=-32767,hit=0,decided=0),
            waitFacing=5,reflected=False,command=dict(skillset=8,ability=123,target=17))
        before=copy.deepcopy(selected);destination=[1,2,0]
        result=source_fallback(selected,destination,0)
        self.assertFalse(result['actBeforeMove']);self.assertEqual(destination,result['coords'])
        self.assertEqual(dict(skillset=0,ability=123,target=17),result['command'])
        self.assertEqual(before['rank'],result['rank']);self.assertEqual(5,result['waitFacing'])
        self.assertFalse(result['reflected']);self.assertEqual(before,selected)
        destination[0]=8;self.assertEqual([1,2,0],result['coords'])

    def test_exact_Defend_support_selects_Defend_and_stays_outside_the_managed_no_Defend_profile(self):
        selected=dict(command=dict(skillset=0),rank=dict(priority=-32767,hit=0,decided=0),waitFacing=5)
        for support,skillset in ((478,0),(479,2),(480,0)):
            with self.subTest(support=support):
                result=source_fallback(selected,[1,2,0],support)
                self.assertEqual(skillset,result['command']['skillset'])
                self.assertEqual(selected['rank'],result['rank'])


if __name__=='__main__':unittest.main()
