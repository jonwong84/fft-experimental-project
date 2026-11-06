import unittest
from verify_learn_on_hit import roll, validate


class LearnOnHitArithmeticTests(unittest.TestCase):
    def test_complete_output_boundaries_and_row(self):
        self.assertEqual(validate(), dict(arithmeticComparisons=229376, rowHex='808004'))

    def test_zero_and_above_hundred_still_advance(self):
        self.assertEqual(roll(57, 0), (2770839166, False))
        self.assertEqual(roll(57, 255), (2770839166, True))
