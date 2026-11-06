import unittest
from verify_ordinary_spell_ai import charge, known_row, mp_term, validate


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
