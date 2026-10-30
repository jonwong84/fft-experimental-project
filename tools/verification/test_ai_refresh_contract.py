"""ZOD-156 source scalar checks; no managed refresh implementation or full prefix execution."""
import copy
import json
import unittest

from verify_ai_execution_contract import FIXTURE as EXECUTION_FIXTURE, tape
from verify_ai_refresh_contract import FIXTURE, flags, refresh_clock


class AiRefreshContractTests(unittest.TestCase):
    def test_authored_clock_vectors_and_counter_proposals_preserve_inputs(self):
        for row in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']:
            with self.subTest(vector=row['id']):
                before=copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], refresh_clock(row['input']))
                self.assertEqual(before,row['input'])

    def test_owned_flag_rebuild_preserves_policy_bits_and_other_decision_flags(self):
        result=flags(50,50,False,0xff,0xff)
        self.assertEqual(dict(enemy=False,decision=0xf8,targeting=0x0c),result)
        enemy=flags(10,50,True,0x07,0xff)
        self.assertEqual(dict(enemy=True,decision=0x80,targeting=0x8d),enemy)
        # Considered is set even for an initially unconsidered surviving uncharged enemy.
        self.assertTrue(enemy['decision'] & 0x80)

    def test_odd_HP_half_floor_and_strict_three_quarter_boundary(self):
        self.assertEqual(0x80,flags(24,49,True,0,0)['targeting'])
        self.assertEqual(0x81,flags(23,49,True,0,0)['targeting'])
        self.assertEqual(0x80,flags(36,49,True,0,0)['targeting'])
        self.assertEqual(0,flags(37,49,True,0,0)['targeting'])
        self.assertEqual(0,flags(36,48,True,0,0)['targeting'])

    def test_retained_refresh_draw_can_change_supplied_lottery_branch(self):
        vector=json.loads(EXECUTION_FIXTURE.read_text(encoding='utf-8'))['vectors'][0]
        original=tape(vector)
        vector['entry']=dict(word=1103527590,count=8)  # The source refresh's one retained draw.
        later=tape(vector)
        self.assertEqual(1,original['decision']['lotteryDraws'])
        self.assertEqual(dict(word=2111915288,count=22,lotteryDraws=8,reason='AllRollsFailedUseFirst'),later['decision'])
        self.assertEqual((3794901700,26),(later['actual']['word'],later['actual']['count']))


if __name__=='__main__':
    unittest.main()
