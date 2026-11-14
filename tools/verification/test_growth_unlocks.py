import copy,json,unittest
import verify_growth_unlocks as v

class GrowthUnlockTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def test_all_literals(self):v.validate(self.f)
    def test_growth_uses_five_discarded_draws_even_when_values_unchanged(self):
        row=next(r for r in self.f['growth'] if r['id']=='zero-raw-still-five-draws')
        self.assertEqual(5,v.growth(row['input'])['count'])
    def test_growth_divisor_uses_supplied_level_and_zero_rate_counts_as_one(self):
        a=dict(raw=[1000]*5,growth=[11]*5,level=10,down=False,word=57,count=9)
        self.assertEqual([1047]*5,v.growth(a)['raw']);a['level']=11;self.assertEqual([1045]*5,v.growth(a)['raw'])
    def test_counter_failure_does_not_return_a_partial_prefix(self):
        a=dict(raw=[1000]*5,growth=[11]*5,level=10,down=False,word=57,count=2**64-5)
        self.assertEqual(dict(accepted=False,raw=None,word=57,count=2**64-5),v.growth(a))
    def test_opposite_sex_prerequisite_and_output_masks_are_separate(self):
        a=dict(male=False,female=True,monster=False,syntheticRequirements={'mime':{'bard':15}})
        self.assertEqual(['squire','chemist','mime'],v.unlocks(a,self.f));a['female']=False
        self.assertEqual(['squire','chemist'],v.unlocks(a,self.f))
    def test_both_and_neither_sex_bits_remain_independent(self):
        a=dict(male=False,female=False,monster=False,allLevels=15)
        self.assertEqual(20,len(v.unlocks(a,self.f)));a.update(male=True,female=True)
        self.assertEqual(18,len(v.unlocks(a,self.f)))
    def test_monster_has_no_base_unlock(self):self.assertEqual([],v.unlocks(dict(male=True,female=True,monster=True,allLevels=15),self.f))
    def test_tampered_literals_and_out_of_domain_raw_values_fail(self):
        self.f['growth'][0]['expected']['raw'][0]=0
        with self.assertRaises(ValueError):v.validate(self.f)
        with self.assertRaises(ValueError):v.growth(dict(raw=[0x1000000]*5,growth=[1]*5,level=1,down=False,word=0,count=0))

if __name__=='__main__':unittest.main()
