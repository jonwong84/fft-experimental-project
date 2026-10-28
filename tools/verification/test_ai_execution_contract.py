"""Source guard/arithmetic checks; ZOD-155 managed transaction proposals are not executed."""
import json
import unittest

from verify_ai_decision_contract import draws
from verify_ai_execution_contract import FIXTURE, command_stage, dispatch, tape
from verify_sword_knockback_contract import resolve


class AiExecutionContractTests(unittest.TestCase):
    def test_supplied_prefix_tapes_keep_scoring_lottery_and_fresh_combat_draws(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for vector in fixture['vectors']:
            with self.subTest(vector=vector['id']):
                result = tape(vector)
                self.assertEqual(dict(decision=vector['expectedDecision'], actual=vector['expectedActual']), result)
                if result['actual']:
                    self.assertEqual(50, result['actual']['hp'])  # Projected HP25 is not applied.
                    self.assertGreater(result['actual']['count'], result['decision']['count'])

    def test_rewinding_to_query_entry_is_a_different_stream(self):
        rewound = resolve(dict(seed=1, drawCount=7, pa=5, power=5, hp=50, classEvade=89,
            actor=[2, 1], target=[1, 1], mapSize=[5, 3], actorFacing='East', targetFacing='East', actorBrave=70, targetBrave=70))
        self.assertEqual([16838, 5758, 10113, 17515], rewound['raw'])
        self.assertEqual((3295386429, 11), (rewound['rngWord'], rewound['drawCount']))
        self.assertNotEqual(180171308, rewound['rngWord'])

    def test_command_progress_guard_precedes_redecision_and_ordered_dispatch(self):
        self.assertEqual('EndUnchanged', command_stage(True, True, True, True, True))
        self.assertEqual('Decide', command_stage(False, False, False, False, False))
        self.assertEqual('Reuse', command_stage(True, False, False, True, False))
        self.assertEqual('Decide', command_stage(True, False, False, False, True))
        self.assertEqual('Move', dispatch(False, False, False, False, True))
        self.assertEqual('Act', dispatch(False, False, True, False, True))
        self.assertEqual('Act', dispatch(True, False, True, True, True))
        self.assertEqual('EndTurn', dispatch(True, False, False, True, False))
        self.assertEqual('Move', dispatch(False, True, True, False, True))
        self.assertEqual('EndTurn', dispatch(True, True, False, False, True))

    def test_one_source_VSync_draw_changes_the_next_formula_entry(self):
        word, raw = draws(3655513600, 1)
        self.assertEqual([7419], raw)  # Callback consumes what would otherwise be the first evade draw.
        _, following = draws(word, 1)
        self.assertEqual([16212], following)
        self.assertNotEqual(raw, following)  # Not a proposal to draw per managed rendered frame.


if __name__ == '__main__':
    unittest.main()
