import copy,json,unittest
from verify_ordinary_magic_content import FIXTURE,validate

class OrdinaryMagicContentTests(unittest.TestCase):
    def test_source_reviewed_vectors_are_coherent(self):
        self.assertEqual(dict(bindingVectors=12,sharedFormulas=1),validate(json.loads(FIXTURE.read_text())))

    def test_binding_order_formula_flags_and_amount_changes_are_detected(self):
        fixture=json.loads(FIXTURE.read_text())
        for field,value in [('id',17),('formula',12),('status',1),('flags2',1),('flags3',0),('expectedDamageAtMa8Faith70',55)]:
            changed=copy.deepcopy(fixture);changed['cases'][0][field]=value
            with self.assertRaises(ValueError,msg=field):validate(changed)

if __name__=='__main__':unittest.main()
