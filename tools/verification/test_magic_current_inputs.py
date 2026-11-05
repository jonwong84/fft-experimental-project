import copy,json,unittest
import verify_magic_current_inputs as v

class CurrentInputsTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def test_literal_descriptions(self):self.assertEqual(12,v.validate(self.f)['projectionDescriptions'])
    def test_serpentarius_precedes_opposition(self):
        r=self.f['compatibility'][0];r['caster']['sign']=12;r['target']['sign']=6
        self.assertEqual('Serpentarius',v.compatibility(r['caster'],r['target']))
    def test_opposition_uses_entire_sex_flag_pair(self):
        r=self.f['compatibility'][6];r['caster'].update(male=True,female=True);r['target'].update(male=True,female=False)
        self.assertEqual('Best',v.compatibility(r['caster'],r['target']))
    def test_current_bytes_are_preserved_without_recomputation(self):
        r=self.f['projections'][0];self.assertEqual((19,43,61),(v.project(r)['amount']['magicAttack'],v.project(r)['amount']['caster']['stored'],v.project(r)['amount']['target']['stored']))
    def test_strengthening_is_any_intersection(self):self.assertTrue(v.project(self.f['projections'][2])['amount']['modifiers']['strengthensElement'])
    def test_healing_ignores_damage_weather(self):self.assertTrue(v.project(self.f['projections'][6])['accepted'])
    def test_unsupported_snapshot_rejects_without_rng(self):
        r=copy.deepcopy(self.f['queries'][0]);r['target']['statuses']['dead']=True;o=v.resolve(r)
        self.assertFalse(o['accepted']);self.assertEqual((r['word'],r['count']),(o['word'],o['count']))
    def test_tamper_and_unknown_identity_fail(self):
        self.f['compatibility'][0]['expected']='Best'
        with self.assertRaises(ValueError):v.validate(self.f)
        r=self.f['compatibility'][0];r['caster']['sign']=13
        with self.assertRaises(ValueError):v.compatibility(r['caster'],r['target'])

if __name__=='__main__':unittest.main()
