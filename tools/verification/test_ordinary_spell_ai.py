import unittest
from verify_ordinary_spell_ai import charge, known_row, mp_term, validate, validate_inspection


class OrdinarySpellAiTests(unittest.TestCase):
    def test_source_descriptions(self):
        self.assertEqual(sum(validate().values()),12)

    def test_learning_generic_and_special_rows(self):
        self.assertEqual([known_row(i,i,5) for i in range(5,24)],list(range(19)))
        self.assertIsNone(known_row(24,11,5))

    def test_charge_support_precedence(self):
        for raw in range(256):
            self.assertEqual(charge(raw,True,True),raw//2)
            self.assertEqual(charge(raw,True,True,True),raw)

    def test_mp_halving_precedes_share(self):
        self.assertEqual(mp_term(47,75,48,3),15)
        self.assertEqual(mp_term(48,75,48,3),30)
        self.assertEqual(mp_term(0,0,255,4),0)

    def test_managed_inspection_is_checked_against_independent_literal_fire(self):
        report=dict(eachCaseRestartsFromSameStateAndRng=True,cExecuted=False,sourceOriginalExecuted=False,cases=[
            dict(OriginalAbilityId=16,sourceSlot=0,disposition='Scored',Score=dict(Priority=49),
                 units=[dict(Hp=100,Mp=69),dict(Hp=146,Mp=50)],
                 frames=[dict(Result=dict(HpDamage=54,Effects=dict(AddedEffect=False)))],
                 entryRng=dict(Value=57,DrawCount=1),exitRng=dict(Value=1798592085,DrawCount=5))])
        # Four affine RNG steps from seed57, independent of managed output.
        state=57
        for _ in range(4):state=(state*1103515245+12345)&0xffffffff
        report['cases'][0]['exitRng']['Value']=state
        self.assertEqual(validate_inspection(report,'fire')['preparedAiHypotheses'],1)
        report['cases'][0]['Score']['Priority']=50
        with self.assertRaises(ValueError):validate_inspection(report,'fire')
