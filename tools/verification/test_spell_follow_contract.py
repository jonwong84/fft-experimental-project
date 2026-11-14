import copy,json,unittest
import verify_spell_follow_contract as v

class SpellFollowContractTests(unittest.TestCase):
    def setUp(self):self.f=json.loads(v.FIXTURE.read_text(encoding='utf-8'))
    def row(self,group,id):return copy.deepcopy(next(r['input'] for r in self.f[group] if r['id']==id))
    def test_all_42_literal_descriptions(self):self.assertEqual(42,sum(v.validate(self.f)['descriptions'].values()))
    def test_no_follow_changes_choice_but_not_stored_unit_resolution(self):
        self.assertEqual(1,v.choice(self.row('choices','no-follow-offers-tile-only')))
        self.assertEqual([3,1,1],v.center(self.row('centers','no-follow-does-not-rewrite-stored-unit'))['center'])
    def test_yes_is_stored_verbatim_and_resolves_as_a_tile(self):
        self.assertEqual(7,v.confirmation(self.row('confirmations','yes-stores-seven-not-six'))['type'])
        self.assertEqual([1,0,0],v.center(self.row('centers','yes-seven-uses-tile-branch'))['center'])
    def test_preparation_active_guard_is_not_repeated_at_area_centre(self):
        self.assertFalse(v.preparation_target_checks(self.row('preparations','inactive-anchor-blocks-preparation')))
        self.assertTrue(v.center(self.row('centers','inactive-anchor-position-still-read'))['resolved'])
    def test_source_halfwords_narrow_before_map_checks(self):
        self.assertEqual([1,0,1],v.center(self.row('centers','raw-negative-halfwords-wrap-to-byte'))['center'])
        self.assertFalse(v.center(self.row('centers','wrapped-layer-two-rejected'))['resolved'])
    def test_moving_anchor_changes_area_not_stored_tile_or_source_order(self):
        self.assertEqual(['anchor','bystander'],v.scenario(self.row('scenarios','moved-unit-outside-current-blue-range'))['ids'])
        self.assertEqual(['old-tile-unit'],v.scenario(self.row('scenarios','tile-hit-old-tile-occupant'))['ids'])
    def test_inactive_and_jump_anchors_still_centre_bystanders(self):
        for state in ['inactive','jumping']:
            self.assertEqual(['bystander'],v.scenario(self.row('scenarios',state+'-anchor-centres-area-but-is-not-selected'))['ids'])
    def test_tampered_literals_and_non_source_storage_are_rejected(self):
        self.f['centers'][0]['expected']['center']=[1,0,0]
        with self.assertRaises(ValueError):v.validate(self.f)
        d=self.row('centers','tile-keeps-stored-position');d['stored'][0]=32768
        with self.assertRaises(ValueError):v.center(d)

if __name__=='__main__':unittest.main()
