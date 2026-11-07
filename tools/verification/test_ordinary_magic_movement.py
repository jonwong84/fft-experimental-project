import json,unittest
from verify_ordinary_magic_movement import FIXTURE,validate

class OrdinaryMagicMovementTests(unittest.TestCase):
    def test_source_charge_distinction_includes_zero_sentinel_and_alternate_skillset(self):
        self.assertEqual(dict(cancellationDescriptions=7),validate(json.loads(FIXTURE.read_text())))

    def test_treating_ordinary_magic_as_charge_is_detected(self):
        fixture=json.loads(FIXTURE.read_text());fixture['cases'][0]['cancels']=True
        with self.assertRaises(ValueError):validate(fixture)

if __name__=='__main__':unittest.main()
