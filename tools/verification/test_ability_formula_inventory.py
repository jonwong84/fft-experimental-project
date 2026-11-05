import unittest
from inventory_ability_formulas import BUCKETS, ROOT, decode_secondary, normalize_formula, output_path, parse_dispatch, summarize

class AbilityFormulaInventoryTests(unittest.TestCase):
    def table(self, names=None):
        names = names or [f'formula_example_{i}' for i in range(100)]
        return 'static void (*s_formulas[ABILITY_FORMULA_ID_LIMIT - 1])(void) = {\n'+',\n'.join(names)+',\n};'

    def test_one_based_dispatch_ignores_comments(self):
        rows = parse_dispatch(self.table().replace('formula_example_0','/* formula_fake, */ formula_example_0'))
        self.assertEqual((rows[0]['id'],rows[-1]['id']),(1,100))
        self.assertEqual(rows[54]['handler'],'formula_example_54')

    def test_missing_duplicate_or_nonhandler_slots_reject(self):
        for names in ([f'formula_x{i}' for i in range(99)], ['formula_same']*100, ['0']*100):
            with self.assertRaises(ValueError): parse_dispatch(self.table(names))

    def test_formula_zero_and_invalid_bytes_fall_back_to_weapon(self):
        self.assertEqual([normalize_formula(i) for i in [0,1,8,100,101,255]],[1,1,8,100,1,1])
        for value in [-1,256,True,1.0]:
            with self.assertRaises(ValueError): normalize_formula(value)

    def test_same_formula_retains_distinct_power_and_geometry(self):
        a=bytearray(14);a[8]=55;a[10]=2;a[0]=4
        b=bytearray(a);b[10]=4;b[0]=1
        rows=decode_secondary(a+b)
        self.assertEqual([r['formula'] for r in rows],[55,55])
        self.assertEqual([(r['range'],r['y']) for r in rows],[(4,2),(1,4)])
        self.assertEqual(summarize(rows)['rawFormulaCounts'],{'55':2})

    def test_partial_secondary_record_rejects(self):
        for value in [b'',bytes(13),bytes(15)]:
            with self.assertRaises(ValueError): decode_secondary(value)

    def test_generated_inventory_output_is_confined_to_scratch(self):
        self.assertEqual(output_path(ROOT/'scratch/inventory.json'),(ROOT/'scratch/inventory.json').resolve())
        for path in [ROOT/'docs/inventory.json',ROOT/'scratch/../inventory.json']:
            with self.assertRaises(ValueError): output_path(path)

    def test_navigation_buckets_cover_every_slot_once(self):
        values=[v for bucket in BUCKETS.values() for v in bucket]
        self.assertEqual(sorted(values),list(range(1,101)))
        self.assertEqual(BUCKETS['empty-dispatch-handlers'],[17,19,24,25,70])
