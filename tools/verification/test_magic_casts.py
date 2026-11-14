import copy,json,unittest
import verify_magic_casts as v

class MagicCastsTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,id):return next(r for r in self.f['casts'] if r['id']==id)
    def test_all_literal_casts(self):self.assertEqual(13,v.validate(self.f)['castDescriptions'])
    def test_cost_is_once_per_cast(self):
        r=v.resolve(self.f['casts'][0]);self.assertEqual((21,9,2),(r['outcome']['mp'],r['outcome']['cost'],len(r['outcome']['targets'])))
    def test_preview_preserves_mp_but_keeps_real_evade_draws(self):
        d=self.row('two-target-Preview');r=v.resolve(d);self.assertEqual(30,r['outcome']['mp']);self.assertEqual(8,r['count']-d['count'])
    def test_late_counter_failure_returns_no_mp_or_prefix(self):
        d=self.row('late-rng-exhaustion-discards-mp-and-target-prefix');r=v.resolve(d)
        self.assertIsNone(r['outcome']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_late_context_failure_returns_original_checkpoint(self):
        d=self.row('late-target-rejection-discards-prefix');r=v.resolve(d);self.assertFalse(r['accepted']);self.assertEqual(d['word'],r['word'])
    def test_self_target_does_not_mutate_source_mp(self):
        d=self.row('self-heal-uses-post-cost-private-mp');r=v.resolve(d);self.assertEqual(21,r['outcome']['mp']);self.assertEqual(30,d['caster']['mp'])
    def test_order_is_not_sorted_and_changes_flag_assignment(self):
        r=v.resolve(self.row('same-formula-different-target-order'));self.assertEqual(['second','first'],[t['id'] for t in r['outcome']['targets']]);self.assertTrue(r['outcome']['targets'][0]['setFlag'])
    def test_missing_readiness_has_no_proposal(self):
        d=copy.deepcopy(self.f['casts'][0]);d['ready']['chargeReady']=False
        self.assertIsNone(v.resolve(d)['outcome'])

if __name__=='__main__':unittest.main()
