import copy,json,unittest
import verify_magic_evasion_contract as v

class MagicEvasionTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,id):return next(r for r in self.f['rolls'] if r['id']==id)
    def test_all_literal_descriptions(self):
        r=v.validate(self.f);self.assertEqual((8,15,24),(r['loadDescriptions'],r['preparationDescriptions'],r['rollDescriptions']))
    def test_none_sentinel_differs_from_zero_item(self):
        d=self.f['loads'][0]['input'];d['rightWeapon']=0
        self.assertEqual(0,v.load(d)['items'][1]);d['rightWeapon']=255;self.assertEqual(20,v.load(d)['items'][1])
    def test_abandon_survives_later_contribution_clear(self):
        d=self.f['preparations'][-3];r=v.prepare(d['equipment'],d['modifiers'])
        self.assertEqual(dict(base=25,evades=[0,0,0],abandon=True),r)
    def test_preview_and_AI_draw_even_with_zero_evades(self):
        for mode in ('Preview','AiSimulation'):
            r=v.roll(self.row('zero-evades-'+mode));self.assertEqual(4,r['draws']);self.assertTrue(r['outcome']['hit'])
    def test_certain_and_rounded_zero_have_distinct_hit_facts(self):
        self.assertFalse(v.roll(self.row('certain-right-Preview'))['outcome']['hit'])
        r=v.roll(self.row('rounded-zero-preview-still-hit'));self.assertTrue(r['outcome']['hit']);self.assertEqual(0,r['outcome']['accuracy'])
    def test_early_success_can_use_last_available_draw(self):
        r=v.roll(self.row('last-draw-can-evade-and-succeed'));self.assertTrue(r['accepted']);self.assertEqual(v.MAX,r['count'])
    def test_counter_rejection_rolls_back_private_draws(self):
        d=self.row('exhausted-after-private-draws');r=v.roll(d)
        self.assertFalse(r['accepted']);self.assertIsNone(r['outcome']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_tampering_widths_and_unknown_modes_fail(self):
        self.f['loads'][0]['expected']['evades'][0]+=1
        with self.assertRaises(ValueError):v.validate(self.f)
        d=copy.deepcopy(self.row('zero-evades-Execute'));d['mode']='Unknown'
        with self.assertRaises(ValueError):v.roll(d)
        d['mode']='Execute';d['equipment']['accessory']['evade']=256
        with self.assertRaises(ValueError):v.roll(d)

if __name__=='__main__':unittest.main()
