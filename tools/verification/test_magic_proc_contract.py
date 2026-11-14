import copy,json,unittest
from verify_magic_proc_contract import FIXTURE,MAX,decision,roll,validate

class MagicProcContractTests(unittest.TestCase):
    def fixture(self):return json.loads(FIXTURE.read_text(encoding='utf-8'))
    def case(self,id):return next(r for r in self.fixture()['decisions'] if r['id']==id)

    def test_all_source_and_managed_boundary_descriptions(self):
        r=validate(self.fixture());self.assertEqual((r['rollDescriptions'],r['decisionDescriptions']),(6,26))

    def test_scaled_roll_boundary_differs_from_modulo(self):
        self.assertTrue(roll(6225)['passed']);self.assertFalse(roll(6226)['passed']);self.assertFalse(roll(32700)['passed']);self.assertEqual(32700%100,0)

    def test_preview_passes_without_flag_set_or_clear_flag_knowledge(self):
        r=decision(self.case('preview-forces-status-without-setting-flag')['input'])
        self.assertTrue(r['applyStatus']);self.assertFalse(r['setFlag']);self.assertIsNone(r['raw']);self.assertEqual(r['count'],MAX)

    def test_AI_requires_clear_added_flag_in_its_bounded_context(self):
        self.assertFalse(decision(self.case('AI-entered-tail-excludes-proc')['input'])['applyStatus'])
        self.assertEqual(decision(self.case('AI-added-flag-unknown')['input'])['rejection'],'UnsupportedContext')

    def test_bypassed_paths_do_not_draw_or_require_profile(self):
        for id in ['missed-Execute','nullified-Execute','healing-tail-Execute']:
            r=decision(self.case(id)['input']);self.assertTrue(r['accepted']);self.assertFalse(r['invoked']);self.assertEqual(r['count'],MAX)

    def test_counter_exhaustion_and_context_rejection_are_atomic(self):
        for id in ['draw-counter-exhaustion-atomic','context-rejection-before-exhaustion']:
            d=self.case(id)['input'];r=decision(d);self.assertFalse(r['accepted']);self.assertEqual((r['word'],r['count']),(d['word'],d['count']));self.assertNotIn('raw',r)

    def test_status_record_annotation_does_not_change_the_roll(self):
        d=copy.deepcopy(self.case('execute-empty-status-record-pass')['input']);r=decision(d);d['statusRecordId']=7
        self.assertEqual(decision(d),r);self.assertEqual(r['count'],d['count']+1)

    def test_tampered_checkpoint_and_unknown_modes_reject(self):
        f=self.fixture();f['decisions'][0]['expected']['count']+=1
        with self.assertRaises(ValueError):validate(f)
        d=self.case('execute-empty-status-record-pass')['input'];d['mode']='Unknown'
        with self.assertRaises(ValueError):decision(d)
