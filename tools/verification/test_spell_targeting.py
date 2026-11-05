import copy,json,unittest
import verify_spell_targeting as v

class SpellTargetingTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def roster(self,id):return next(r for r in self.f['rosters'] if r['id']==id)
    def test_all_literal_descriptions(self):self.assertEqual(26,v.validate(self.f)['panelDescriptions'])
    def test_reference_narrows_after_half_and_water(self):self.assertEqual(43,v.reference(dict(h=255,half=31,water=7)))
    def test_blocked_sentinel_is_not_skipped_in_distance_choice(self):
        r=next(r for r in self.f['columns'] if r['id']=='blocked-sentinel-can-be-closer');self.assertIsNone(v.column(r))
    def test_blue_and_green_untargetable_rules_differ(self):
        b=next(r for r in self.f['panels'] if r['id']=='range-aoe1-keeps-untargetable-aim')
        self.assertIn([1,1,0],v.panels(b)['panels']);b['kind']='Area';self.assertNotIn([1,1,0],v.panels(b)['panels'])
    def test_shared_panel_mutation_depends_on_source_order(self):
        self.assertIn('enemy',v.selection(self.roster('shared-panel-allowed-first-keeps-mark'))['ids'])
        self.assertNotIn('ally',v.selection(self.roster('shared-panel-disallowed-first-removes-budget'))['ids'])
    def test_nearest_order_is_stable_for_equal_distances(self):self.assertEqual(['actor','east','north','far'],v.selection(self.roster('nearest-stable-distance-ties'))['ids'])
    def test_whole_map_team_mask_does_not_mark_empty_tiles(self):self.assertEqual(3,len(v.selection(self.roster('whole-map-both-flags-only-occupied'))['panels']))
    def test_tampering_and_uncertified_context_fail(self):
        self.f['columns'][0]['expected']=1
        with self.assertRaises(ValueError):v.validate(self.f)
        r=copy.deepcopy(self.f['panels'][0]);r['profile']['noSpecial']=False;self.assertFalse(v.panels(r)['accepted'])

if __name__=='__main__':unittest.main()
