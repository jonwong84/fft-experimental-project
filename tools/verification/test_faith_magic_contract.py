import copy,json,unittest
from verify_faith_magic_contract import FIXTURE,amount,divide,faith,scale,signed16,validate

class FaithMagicContractTests(unittest.TestCase):
    def fixture(self):return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_all_literal_source_vectors(self):
        r=validate(self.fixture());self.assertEqual((r['faithDescriptions'],r['productDescriptions'],r['amountDescriptions']),(15,7,22))

    def test_single_final_division_and_negative_truncation(self):
        f=dict(stored=90,faithStatus=False,innocentStatus=False)
        self.assertEqual(scale(5,f,f),4);self.assertEqual(scale(-5,f,f),-4);self.assertEqual(divide(-40500,10000),-4)

    def test_innocent_wins_independently_for_both_units(self):
        f=dict(stored=70,faithStatus=True,innocentStatus=True);g=dict(stored=70,faithStatus=True,innocentStatus=False)
        self.assertEqual(faith(f),0);self.assertEqual(faith(g),100);self.assertEqual(scale(200,f,g),0);self.assertEqual(scale(200,g,f),0)

    def test_halfword_narrowing_precedes_faith(self):
        f=dict(stored=70,faithStatus=False,innocentStatus=False)
        self.assertEqual(signed16(255*255),-511);self.assertEqual(scale(-511,f,f),-250)

    def test_healing_does_not_apply_target_defense_chain(self):
        row=next(r for r in self.fixture()['amounts'] if r['id']=='healing-ignores-target-defenses')
        self.assertEqual(amount(row['input']),dict(xa=12,product=168,amount=82))
        d=copy.deepcopy(row['input']);d['kind']='Damage';self.assertEqual(amount(d),dict(xa=7,product=98,amount=48))

    def test_tampered_expected_or_ordering_parameters_fail(self):
        for group in ['faith','products','amounts']:
            f=self.fixture()
            if group=='amounts':f[group][0]['expected']['amount']+=1
            else:f[group][0]['expected']+=1
            with self.assertRaises(ValueError):validate(f)

    def test_invalid_widths_status_types_and_modes_reject(self):
        f=dict(stored=256,faithStatus=False,innocentStatus=False)
        with self.assertRaises(ValueError):faith(f)
        f['stored']=70;f['faithStatus']=1
        with self.assertRaises(ValueError):faith(f)
        for field,value in [('kind','Unknown'),('magicAttack',256),('power',-1)]:
            d=copy.deepcopy(self.fixture()['amounts'][0]['input']);d[field]=value
            with self.assertRaises(ValueError):amount(d)

    def test_source_identity_and_unknown_modifier_reject(self):
        f=self.fixture();f['productionImplemented']=True
        with self.assertRaises(ValueError):validate(f)
        d=self.fixture()['amounts'][0]['input'];d['modifiers']['unknown']=True
        with self.assertRaises(ValueError):amount(d)
