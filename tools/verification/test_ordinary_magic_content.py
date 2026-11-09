import copy,json,unittest
from verify_ordinary_magic_content import FIXTURE,HEALING_FIXTURE,validate

class DefaultAbilityContentTests(unittest.TestCase):
    def test_source_reviewed_vectors_are_coherent(self):
        self.assertEqual(dict(bindingVectors=12,sharedFormulas=1),validate(json.loads(FIXTURE.read_text())))

    def test_binding_order_formula_flags_and_amount_changes_are_detected(self):
        fixture=json.loads(FIXTURE.read_text())
        for field,value in [('id',17),('formula',12),('status',1),('flags2',1),('flags3',0),('expectedDamageAtMa8Faith70',55)]:
            changed=copy.deepcopy(fixture);changed['cases'][0][field]=value
            with self.assertRaises(ValueError,msg=field):validate(changed)

    def test_healing_vectors_preserve_the_distinct_formula_and_no_evade_flag(self):
        self.assertEqual(dict(bindingVectors=2,sharedFormulas=2),validate(json.loads(HEALING_FIXTURE.read_text())))

    def test_healing_cannot_be_substituted_with_the_damage_or_raise_formula(self):
        fixture=json.loads(HEALING_FIXTURE.read_text())
        for field,value in [('formula',8),('formula',13),('flags4',2),('expectedHealingAtMa8Faith70',55)]:
            changed=copy.deepcopy(fixture);changed['cases'][0][field]=value
            with self.assertRaises(ValueError,msg=field):validate(changed)

    def test_primary_ai_evasion_is_distinct_from_actual_secondary_evasion(self):
        fixture=json.loads(FIXTURE.read_text());fixture['cases'][0]['primaryAiFlags'][1]|=2
        with self.assertRaises(ValueError):validate(fixture)

    def test_highest_tier_learning_flag_is_preserved_as_a_separate_post_action_requirement(self):
        fixture=json.loads(FIXTURE.read_text());self.assertEqual([19,23,27],[r['id'] for r in fixture['cases'] if r['learnOnHit']])
        fixture['cases'][3]['learnOnHit']=False
        with self.assertRaises(ValueError):validate(fixture)

if __name__=='__main__':unittest.main()
