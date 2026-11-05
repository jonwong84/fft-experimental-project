import copy,json,unittest
import verify_spell_geometry_casts as v

class SpellGeometryCastTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,id):return copy.deepcopy(next(r for r in self.f['casts'] if r['id']==id))
    def test_literal_composition_descriptions(self):self.assertEqual(13,v.validate(self.f)['geometryCastDescriptions'])
    def test_source_order_controls_rng_assignment(self):
        r=v.resolve(self.row('source-order-not-input-order'));self.assertEqual(['first','second'],[t['id'] for t in r['outcome']['targets']]);self.assertTrue(r['outcome']['targets'][0]['setFlag'])
    def test_range_is_not_resolution_area(self):self.assertTrue(v.resolve(self.row('no-blue-range-recheck'))['accepted'])
    def test_late_failures_return_entire_checkpoint(self):
        for id in ['late-dead-target-rolls-back','late-rng-rolls-back']:
            d=self.row(id);r=v.resolve(d);self.assertIsNone(r['outcome']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_preview_still_uses_real_evade_draws(self):
        d=self.row('map-geometry-Preview');r=v.resolve(d);self.assertEqual(30,r['outcome']['mp']);self.assertEqual(8,r['count']-d['count'])
    def test_missing_aim_proof_and_literal_tampering(self):
        d=self.row('source-order-not-input-order');d['entry']['validatedAim']=False;self.assertFalse(v.resolve(d)['accepted'])
        self.f['casts'][0]['expected']['word']=0
        with self.assertRaises(ValueError):v.validate(self.f)

if __name__=='__main__':unittest.main()
