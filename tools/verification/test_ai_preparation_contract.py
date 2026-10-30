"""Offline source scalars; actual managed preparation is checked separately in .NET."""
import unittest
from verify_ai_preparation_contract import prepare


class AiPreparationContractTests(unittest.TestCase):
    def test_single_ally_disables_support_in_both_cached_policy_paths(self):
        for cached in (False, True):
            self.assertEqual(dict(actorDelay=17,targetDelay=1,enemyReach=4,allyReach=3,
                actorSupportUnavailable=True,retreatSupport=False,singleEnemy=False,
                proximity=8,randomUseThreshold=0,extraDraws=0),prepare(15,5,95,5,3,2,cached))

    def test_delay_uses_restored_CT_and_rounds_before_proximity_admission(self):
        self.assertEqual(0,prepare(99,255,0,1,0,2,False)['proximity'])
        self.assertEqual(8,prepare(99,255,100,1,124,2,False)['proximity'])
        self.assertEqual(125,prepare(99,255,100,1,124,2,False)['enemyReach'])

    def test_imminent_equality_and_far_distance_boundaries(self):
        self.assertEqual(9,prepare(99,1,99,1,0,1,True)['proximity'])
        self.assertEqual(0,prepare(0,1,0,1,0,30,True)['proximity'])


if __name__ == '__main__':
    unittest.main()
