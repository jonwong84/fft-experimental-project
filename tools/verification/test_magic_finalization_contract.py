import copy,json,unittest
import verify_magic_finalization_contract as v

class MagicFinalizationTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def test_literal_descriptions(self):
        r=v.validate(self.f);self.assertEqual((20,20),(r['rawDescriptions'],r['composedDescriptions']))
    def test_only_upper_cap_and_no_route_inference(self):
        d=self.f['raw'][0]['input'];d['damage']=-32768;d['healing']=32767
        r=v.finalize(d);self.assertEqual((-32768,999),(r['damage'],r['healing']));self.assertFalse(r['types']['healing'])
    def test_zero_accuracy_preserves_miss_and_effect_typing(self):
        d=self.f['raw'][0]['input'];d.update(accuracy=0,miss=9);d['effects']['added']=True
        r=v.finalize(d);self.assertFalse(r['hit']);self.assertEqual(9,r['miss']);self.assertTrue(r['types']['pseudo'])
    def test_zero_accuracy_does_not_skip_prior_proc_draw(self):
        d=next(r for r in self.f['composed'] if r['id']=='zero-accuracy-still-draws-before-clearing-hit')
        r=v.compose(d);self.assertEqual(1,r['draws']);self.assertFalse(r['outcome']['hit']);self.assertTrue(r['outcome']['effects']['added'])
    def test_preview_final_flags_need_known_entry(self):
        d=self.f['composed'][0];d['mode']='Preview';d['profile']['entry']['clearFlag']=False
        self.assertEqual('UnsupportedContext',v.compose(d)['rejection'])
    def test_each_unsupported_fact_rejects_before_drawing(self):
        d=self.f['composed'][0]
        for key in ('living','noBlocked','noGolem','emptyStatus','noKnockback','hpOnly'):
            bad=copy.deepcopy(d);bad['profile'][key]=False;r=v.compose(bad)
            self.assertFalse(r['accepted']);self.assertIsNone(r['outcome']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_exhausted_counter_rejects_without_partial_output(self):
        d=self.f['composed'][0];d['count']=(1<<64)-1;r=v.compose(d)
        self.assertEqual('RandomDrawCountExhausted',r['procRejection']);self.assertIsNone(r['outcome']);self.assertEqual(d['word'],r['word'])
    def test_tampered_literal_and_unknown_label_fail(self):
        self.f['raw'][0]['expected']['damage']=1000
        with self.assertRaises(ValueError):v.validate(self.f)
        self.f['raw'][0]['input']['miss']=255
        with self.assertRaises(ValueError):v.finalize(self.f['raw'][0]['input'])

if __name__=='__main__':unittest.main()
