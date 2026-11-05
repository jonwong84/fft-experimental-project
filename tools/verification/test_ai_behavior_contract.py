import copy,json,unittest
from verify_ai_behavior_contract import FIXTURE,describe,validate

class AiBehaviorContractTests(unittest.TestCase):
    def test_all_static_bits_and_retail_vectors_preserve_inputs(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));before=copy.deepcopy(f)
        self.assertEqual(37,validate(f)['aiFlagDescriptions']);self.assertEqual(before,f)

    def test_static_fourth_byte_does_not_become_runtime_aim_policy(self):
        a=describe([130,2,65,129]);b=describe([130,2,65,128])
        self.assertEqual(a['behavior'],b['behavior']);self.assertTrue(a['motion']);self.assertFalse(b['motion']);self.assertTrue(a['usable'])
        self.assertTrue(describe([0,0,0,2])['unresolved']);self.assertFalse(describe([0,0,0,2])['motion'])
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));f['vectors'][0]['expected']['motion']=True
        with self.assertRaises(ValueError):validate(f)

if __name__=='__main__':unittest.main()
