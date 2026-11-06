import copy,json,unittest
import verify_magic_publication as v

class MagicPublicationTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,group,id):return copy.deepcopy(next(r['input'] for r in self.f[group] if r['id']==id))
    def test_all_literal_descriptions(self):self.assertEqual((18,18),tuple(v.validate(self.f)[k] for k in ['vitalDescriptions','livingDescriptions']))
    def test_combine_signed_fields_before_clamping(self):self.assertEqual(10,v.vitals(self.row('vitals','one-expression-not-two-clamps'))['hp'])
    def test_unsigned_old_values_and_signed_minimum(self):self.assertEqual(32768,v.vitals(self.row('vitals','negative-mp-damage'))['mp'])
    def test_miss_bypasses_clamps_and_later_work(self):
        self.assertEqual((200,60),tuple(v.vitals(self.row('vitals','miss-skips-clamps'))[k] for k in ['hp','mp']))
        self.assertTrue(v.living(self.row('living','miss-before-unknown-downstream-work'))['accepted'])
    def test_miss_still_requires_absent_targeted_reactions(self):self.assertFalse(v.living(self.row('living','miss-still-needs-known-reactions'))['accepted'])
    def test_zero_damage_type_still_cancels_sleep(self):self.assertEqual('WouldChangeStatus',v.living(self.row('living','sleep-removal-even-zero-damage'))['rejection'])
    def test_knockout_and_critical_boundaries_do_not_publish(self):
        for id in ['would-knock-out','would-inflict-critical','would-remove-critical']:
            r=v.living(self.row('living',id));self.assertFalse(r['accepted']);self.assertIsNone(r['hp'])
    def test_tampered_description_and_out_of_storage_fields_fail(self):
        self.f['vitals'][0]['expected']['hp']=0
        with self.assertRaises(ValueError):v.validate(self.f)
        d=self.row('vitals','ordinary-damage');d['damage']=32768
        with self.assertRaises(ValueError):v.vitals(d)

if __name__=='__main__':unittest.main()
