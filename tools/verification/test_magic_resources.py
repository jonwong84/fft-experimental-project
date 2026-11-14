import json,unittest
import verify_magic_resources as v

class MagicResourcesTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def test_all_descriptions(self):self.assertEqual(32,v.validate(self.f)['mpDescriptions'])
    def test_signed_mp_high_bit_is_not_an_unsigned_budget(self):
        d=self.f['mp'][0]['input'];d['actor']['mp']=32768;d['ability']['cost']=0
        self.assertEqual('NotEnoughMp',v.mp(d)['outcome']['block'])
    def test_zero_cost_execution_still_records_subtraction_store(self):
        d=self.f['mp'][0]['input'];d['ability']['cost']=0
        self.assertTrue(v.mp(d)['outcome']['subtracted'])
    def test_preview_still_fails_insufficient_budget(self):
        d=self.f['mp'][0]['input'];d['mode']='Preview';d['actor']['mp']=1
        self.assertEqual('NotEnoughMp',v.mp(d)['outcome']['block'])
    def test_execution_short_ceiling_differs_from_ai_monster_floor(self):
        self.assertEqual(5,v.setup(dict(raw=9,short=True,non=False,persevere=False)))
        self.assertEqual(4,v.ai_delay(dict(raw=9,monsterSkill=True,defend=False,persevere=False)))
    def test_ai_precedence_and_high_bit_are_preserved(self):
        self.assertEqual(127,v.ai_delay(dict(raw=255,monsterSkill=True,defend=True,persevere=False)))
        self.assertEqual(127,v.setup(dict(raw=255,short=False,non=False,persevere=False)))
    def test_blocked_zero_is_not_ready_and_none_is_not_ticked(self):
        self.assertFalse(v.tick(dict(counter=0,blocked=True))['ready']);self.assertEqual(255,v.tick(dict(counter=255,blocked=False))['counter'])
    def test_tampered_descriptions_fail(self):
        self.f['mp'][0]['expected']['outcome']['remaining']+=1
        with self.assertRaises(ValueError):v.validate(self.f)

if __name__=='__main__':unittest.main()
