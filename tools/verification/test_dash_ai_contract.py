import copy,json,unittest
from verify_dash_ai_contract import FIXTURE,validate

class DashAiContractTests(unittest.TestCase):
    def test_twenty_independent_compositions_preserve_inputs(self):
        f=json.loads(FIXTURE.read_text(encoding='utf-8'));before=copy.deepcopy(f)
        self.assertEqual(20,validate(f)['dashAiDescriptions']);self.assertEqual(before,f)

    def test_zero_effect_draws_and_consumed_ranked_aims_differ_from_Stone(self):
        rows={r['id']:r for r in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']}
        self.assertEqual(8,rows['ACT-first']['expected']['count'])
        self.assertEqual(15,rows['ACT-first']['expected']['damage'])
        self.assertEqual(100,rows['ACT-first']['expected']['hit'])
        self.assertEqual(0,rows['ranked-multiple-enemy-teams']['expected']['remainingAims'])
        self.assertEqual(6,len(rows['six-real-Dash-mixed-turns']['expected']))

    def test_tampered_candidate_clock_or_real_resolution_stream_reject(self):
        for name in ('ACT-first','complete-Dash-Move-first'):
            f=json.loads(FIXTURE.read_text(encoding='utf-8'));r=next(r for r in f['vectors'] if r['id']==name);r['expected']['count']+=1
            with self.assertRaises(ValueError):validate(f)

if __name__=='__main__':unittest.main()
