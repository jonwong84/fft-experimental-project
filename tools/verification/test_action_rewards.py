import copy,json,unittest
import verify_action_rewards as v

class ActionRewardsTests(unittest.TestCase):
    def test_literal_descriptions(self):v.validate(json.loads(v.FIXTURE.read_text(encoding='utf-8')))
    def test_markers_replace_and_reactions_can_affect(self):
        self.assertEqual(1,v.marker([4,True,False,False]));self.assertEqual(2,v.marker([4,False,True,True]))
    def test_exp_is_maximum_not_sum_and_floor_precedes_bonus(self):
        self.assertEqual(10,v.experience([10,10,10,1,1,True,False]));self.assertEqual(2,v.experience([0,99,1,1,1,True,True]))
    def test_zero_ko_and_large_ko_differ(self):
        self.assertEqual(20,v.experience([0,10,10,0,2,True,False]));self.assertEqual(1,v.experience([0,10,10,255,2,True,False]))
    def test_spillover_uses_pre_bonus_jp(self):
        self.assertEqual([10,11,16,2],v.amounts([4,1,10,False,True]))
        self.assertEqual([10,0,0,0],v.amounts([4,255,10,True,True]))
    def test_excess_exp_is_discarded(self):self.assertEqual([0,11,True],v.balance([99,99,10]))
    def test_threshold_count_does_not_assume_sorted_values(self):self.assertEqual([1,200,2],v.job_points([1,200,0,[300,100,200]]))
    def test_composed_roster_literals(self):
        f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
        for r in f['rosters']:self.assertEqual(r['expected'],v.roster(copy.deepcopy(r['input'])),r['id'])
    def test_tampered_and_out_of_domain_descriptions_fail(self):
        f=json.loads(v.FIXTURE.read_text(encoding='utf-8'));f['markers'][0]['expected']=99
        with self.assertRaises(ValueError):v.validate(f)
        with self.assertRaises(ValueError):v.job_points([0,0,-1,[]])

if __name__=='__main__':unittest.main()
