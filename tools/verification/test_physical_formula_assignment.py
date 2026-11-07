import copy
import json
import unittest
from verify_physical_formula_assignment import FIXTURE, describe, validate


class PhysicalFormulaAssignmentTests(unittest.TestCase):
    def fixture(self):return json.loads(FIXTURE.read_text(encoding='utf-8'))
    def row(self,name):return next(row for row in self.fixture()['cases'] if row['id']==name)

    def test_all_source_vectors(self):
        self.assertEqual(validate(self.fixture())['physicalFormulaDescriptions'],27)

    def test_critical_changes_XA_without_recomputing_YA(self):
        result=describe(self.row('sum-random-y-uses-original-pa')['input'])
        self.assertEqual((result['damage'],result['critical'],result['attempted'],result['count']),(13,True,True,3))

    def test_formula_only_edit_and_explicit_parameter_edit_are_distinct(self):
        a=self.row('formula-only-edit-retains-dash-operands')['input'];a['mode']='Ai'
        b=self.row('sum-random-ai-midpoint')['input']
        self.assertEqual((describe(a)['damage'],describe(b)['damage']),(9,35))

    def test_mutated_critical_draw_and_damage_expectations_are_detected(self):
        for field,value in [('count',2),('damage',35),('attempted',False)]:
            fixture=self.fixture();row=next(r for r in fixture['cases'] if r['id']=='sum-random-critical')
            row['expected'][field]=value
            with self.assertRaises(ValueError):validate(fixture)

    def test_mode_rng_rollback_and_caps(self):
        ai=self.row('ai-no-draws-at-limit');limited=self.row('critical-late-counter-rollback')
        self.assertEqual(describe(ai['input'])['count'],0xffffffffffffffff)
        self.assertEqual(describe(limited['input']),dict(accepted=False,word=0,count=0xfffffffffffffffd))
        self.assertEqual(describe(self.row('sum-random-upper-cap')['input'])['damage'],999)

    def test_source_width_envelope_and_input_preservation(self):
        data=self.row('sum-random-no-critical')['input'];before=copy.deepcopy(data)
        describe(data);self.assertEqual(before,data)
        data.update(x=255,y=255)
        with self.assertRaises(ValueError):describe(data)

    def test_neutral_YA_floor_is_applied_before_factor_in_every_mode(self):
        self.assertEqual(describe(self.row('zero-operands-are-valid')['input'])['damage'],2)
        for name,damage in [('minimum-ya-ai',10),('minimum-ya-preview',10),('minimum-ya-execution',4),('minimum-ya-critical',6)]:
            self.assertEqual(describe(self.row(name)['input'])['damage'],damage)

    def test_floor_neighbors_and_other_formula_remain_distinct(self):
        self.assertEqual(describe(self.row('minimum-ya-neighbor-y1')['input'])['damage'],2)
        self.assertEqual(describe(self.row('minimum-ya-neighbor-pa2')['input'])['damage'],3)
        self.assertEqual(describe(self.row('random-pa-zero-y-pa1')['input'])['damage'],1)

    def test_projectile_keeps_real_evade_draws_with_corrected_midpoint(self):
        result=describe(self.row('minimum-ya-projectile-ai')['input'])
        self.assertEqual((result['damage'],result['count'],result['word']),(10,4,3295386429))

    def test_former_zero_term_assumption_is_detected(self):
        fixture=self.fixture();row=next(r for r in fixture['cases'] if r['id']=='zero-operands-are-valid')
        row['expected']['damage']=1
        with self.assertRaises(ValueError):validate(fixture)
