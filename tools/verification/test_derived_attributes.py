import copy,json,unittest
import verify_derived_attributes as v

class DerivedAttributeTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def data(self,group,id):return copy.deepcopy(next(r['input'] for r in self.f[group] if r['id']==id))
    def test_literal_descriptions(self):v.validate(self.f)
    def test_mode1_keeps_base_attributes_even_when_raw_and_multipliers_are_huge(self):
        self.assertEqual([1,255,0],v.mode1(self.data('mode1','grown-raw-retains-base-bytes'))['base'])
    def test_egg_quantization_precedes_floor_and_is_not_ten_times_normal(self):
        d=self.data('mode1','egg-division-before-rounding');self.assertEqual(9,v.mode1(d)['maxHp']);d['egg']=False
        self.assertEqual(1,v.mode1(d)['maxHp'])
    def test_current_clamps_follow_armor_not_bare_maxima(self):
        d=self.data('equipment','armor-before-current-clamp');self.assertEqual(100,v.mode1(d)['hp']);self.assertEqual(350,v.equipment(d)['hp'])
    def test_zero_armor_can_lower_egg_maximum(self):self.assertEqual(999,v.equipment(self.data('equipment','zero-armor-lowers-egg-maxima'))['maxHp'])
    def test_monster_skips_unknown_contributions_but_caps_actual_attributes(self):
        self.assertEqual([99,99,50],v.equipment(self.data('equipment','monster-skips-unknown-item-captures'))['actual'])
    def test_item_presence_is_distinct_from_zero_bonus(self):
        d=self.data('equipment','present-zero-item-applies-move-jump-caps');self.assertEqual(253,v.equipment(d)['move']);d['items'][0][0]=False
        self.assertEqual(255,v.equipment(d)['move'])
    def test_tampered_and_invalid_raw_descriptions_fail(self):
        self.f['mode1'][0]['expected']['maxHp']=0
        with self.assertRaises(ValueError):v.validate(self.f)
        d=self.data('mode1','ordinary');d['raw'][4]=0x1000000
        with self.assertRaises(ValueError):v.mode1(d)

if __name__=='__main__':unittest.main()
