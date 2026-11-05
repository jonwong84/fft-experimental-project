import copy,json,unittest
import verify_magic_entry_contract as v

class MagicEntryTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,id):return next(r for r in self.f['entries'] if r['id']==id)
    def test_literal_entries(self):self.assertEqual(24,v.validate(self.f)['entryDescriptions'])
    def test_proc_failure_restores_entire_evade_prefix(self):
        d=self.row('proc-exhaustion-rolls-back-four-earlier-draws');r=v.resolve(d)
        self.assertFalse(r['accepted']);self.assertIsNone(r['evasion']);self.assertIsNone(r['outcome']);self.assertEqual((d['word'],d['count']),(r['word'],r['count']))
    def test_rounded_zero_reaches_proc_and_then_finalization(self):
        d=self.row('rounded-zero-enters-tail-before-final-clear');r=v.resolve(d)
        self.assertTrue(r['evasion']['hit']);self.assertFalse(r['outcome']['hit']);self.assertEqual(34,r['outcome']['damage']);self.assertEqual(5,r['count']-d['count'])
    def test_certain_preview_does_not_leak_private_amount_or_weakness(self):
        r=v.resolve(self.row('preview-certain-evade-skips-tail'))
        self.assertFalse(r['evasion']['hit']);self.assertEqual(0,r['outcome']['damage']);self.assertFalse(r['outcome']['effects']['weak']);self.assertIsNone(r['procDisposition'])
    def test_healing_ignores_evasion_inputs(self):
        d=copy.deepcopy(self.row('healing-bypasses-all-evade-inputs-and-RNG'));d['equipment']=None;d['modifiers']=None
        r=v.resolve(d);self.assertTrue(r['accepted']);self.assertIsNone(r['evasion']);self.assertEqual(d['count'],r['count'])
    def test_tampered_expectations_fail(self):
        self.f['entries'][0]['expected']['outcome']['damage']+=1
        with self.assertRaises(ValueError):v.validate(self.f)

if __name__=='__main__':unittest.main()
