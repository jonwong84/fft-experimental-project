import copy,json,unittest
from verify_magic_elements_contract import FIXTURE,project,route,transform,validate

class MagicElementsContractTests(unittest.TestCase):
    def fixture(self):return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_all_fifty_literal_descriptions(self):
        r=validate(self.fixture());self.assertEqual((r['transformDescriptions'],r['projectionDescriptions'],r['routingDescriptions']),(24,12,14))

    def test_weather_rounds_separately_for_multi_element_ability(self):
        r=next(r for r in self.fixture()['transforms'] if r['id']=='storm-fire-lightning-sequential-stores')
        self.assertEqual(transform(r['input'])['amount'],3)

    def test_half_weak_order_and_nullification_markers(self):
        rows=self.fixture()['transforms']
        r=next(r for r in rows if r['id']=='half-then-weak-is-not-identity-on-odd');self.assertEqual(transform(r['input'])['amount'],4)
        r=next(r for r in rows if r['id']=='nullify-clears-absorb-and-skips-half-weak');self.assertEqual(transform(r['input']),r['expected'])

    def test_Faith_is_after_weather_and_not_applied_to_nullification(self):
        rows=self.fixture()['projections']
        r=next(r for r in rows if r['id']=='weather-before-Faith-differs-from-reordered');self.assertEqual(project(r['input'],r['context'])[0]['amount'],2)
        r=next(r for r in rows if r['id']=='nullification-skips-Faith');self.assertIsNone(project(r['input'],r['context'])[0]['amount'])

    def test_healing_does_not_read_element_weather_context(self):
        r=next(r for r in self.fixture()['projections'] if r['id']=='healing-ignores-all-element-context')
        self.assertEqual(project(r['input'],None)[0]['amount'],34)

    def test_absorption_on_undead_differs_from_healing_on_undead(self):
        rows=self.fixture()['routes']
        a=next(r for r in rows if r['id']=='absorbed-damage-undead-stays-healing');b=next(r for r in rows if r['id']=='healing-undead-clears-whole-first-byte')
        self.assertEqual(route(a['input'],a['context'],True)['route'],'Healing');self.assertTrue(route(b['input'],b['context'],True)['clearFirstCanceledStatusByte'])

    def test_tampered_literal_checkpoints_are_rejected(self):
        for section,field in [('transforms','amount'),('projections','amount'),('routes','hpDamage')]:
            f=self.fixture();f[section][0]['expected'][field]+=1
            with self.assertRaises(ValueError):validate(f)

    def test_unsupported_numeric_inputs_and_identity_reject(self):
        r=copy.deepcopy(self.fixture()['transforms'][0]['input'])
        for field,value in [('weather',8),('amount',32768),('elements',256)]:
            d=dict(r);d[field]=value
            with self.assertRaises(ValueError):transform(d)
        f=self.fixture();f['productionImplemented']=True
        with self.assertRaises(ValueError):validate(f)
