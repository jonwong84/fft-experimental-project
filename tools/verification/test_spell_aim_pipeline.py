import copy,json,unittest
import verify_spell_aim_pipeline as v

class SpellAimPipelineTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,id):return copy.deepcopy(next(r['input'] for r in self.f['cases'] if r['id']==id))
    def test_all_literals_and_tampering(self):
        self.assertEqual(19,v.validate(self.f)['phaseDescriptions']);self.f['cases'][0]['expected']['mp']=0
        with self.assertRaises(ValueError):v.validate(self.f)
    def test_exact_unit_versus_yes_tile(self):
        self.assertEqual(['first','second'],v.resolve(self.row('charged-follow-execute'))['ids'])
        self.assertEqual(['old'],v.resolve(self.row('yes-seven-keeps-old-position'))['ids'])
    def test_inactive_anchor_at_preparation_differs_from_resolution(self):
        self.assertIsNone(v.resolve(self.row('inactive-anchor-rejects-at-preparation'))['counter'])
        self.assertEqual(['second'],v.resolve(self.row('inactive-anchor-still-centres-bystander'))['ids'])
    def test_charge_setup_does_not_spend_or_check_mp(self):
        r=v.resolve(self.row('charging-does-not-read-zero-mp'));self.assertEqual(1,r['counter']);self.assertIsNone(r['block']);self.assertIsNone(r['mp'])
    def test_current_stats_preserve_element_before_faith_rounding(self):
        r=v.resolve(self.row('current-mp-and-ma-not-start-snapshots'));self.assertEqual((11,[68,137]),(r['mp'],r['damage']))
    def test_late_failures_restore_entire_checkpoint(self):
        for id in ['late-counter-exhaustion-rolls-back','late-dead-target-rolls-back','unknown-current-anchor-never-falls-back']:
            d=self.row(id);r=v.resolve(d);self.assertFalse(r['accepted']);self.assertIsNone(r['mp']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_preview_and_half_mp_are_read_at_readiness(self):
        r=v.resolve(self.row('non-charge-preview-ready-with-no-ticks'));self.assertEqual((0,30,17),(r['counter'],r['mp'],r['count']))
        r=v.resolve(self.row('half-mp-read-at-readiness'));self.assertEqual((26,4),(r['mp'],r['cost']))
    def test_blocked_zero_and_automatic_capture_are_not_activated(self):
        self.assertFalse(v.resolve(self.row('blocked-zero-counter-is-not-ready'))['accepted'])
        self.assertIsNone(v.resolve(self.row('auto-no-targeting-remains-outside-manual-capture'))['counter'])

if __name__=='__main__':unittest.main()
