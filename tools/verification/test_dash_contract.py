import copy,json,unittest
from verify_dash_contract import FIXTURE,damage,validate

class DashContractTests(unittest.TestCase):
    def test_literal_quartile_boundaries_and_actual_three_draw_results(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));before=copy.deepcopy(f);r=validate(f)
        self.assertEqual((24,8),(r['formulaDescriptions'],r['actualDescriptions']));self.assertEqual(before,f)
        self.assertEqual(15,damage(5,16384))

    def test_bad_domain_and_tampered_actual_stream_reject(self):
        with self.assertRaises(ValueError):damage(0,0)
        with self.assertRaises(ValueError):damage(1,32768)
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));f['actual'][0]['expected']['count']+=4
        with self.assertRaises(ValueError):validate(f)

if __name__=='__main__':unittest.main()
