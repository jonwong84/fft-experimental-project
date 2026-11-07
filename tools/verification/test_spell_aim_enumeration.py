import copy
import json
import unittest
from verify_spell_aim_enumeration import FIXTURE, deadline, mask, usage, validate


class SpellAimEnumerationTests(unittest.TestCase):
    def fixture(self):
        return json.loads(FIXTURE.read_text(encoding='utf-8'))

    def test_source_literals_and_input_isolation(self):
        document = self.fixture()
        before = copy.deepcopy(document)
        result = validate(document)
        self.assertEqual((result['usageDescriptions'], result['deadlineDescriptions'], result['maskDescriptions']), (7, 5, 4))
        self.assertEqual(document, before)

    def test_literal_changes_are_detected(self):
        for kind in ('usage', 'deadlines', 'masks'):
            document = self.fixture()
            document[kind][0]['expected'] = None
            with self.assertRaises(ValueError):
                validate(document)

    def test_instant_ready_and_equal_deadline_are_distinct(self):
        self.assertEqual(deadline(dict(ct=100, speed=8, charge=0)), dict(delay=0, moves=False))
        self.assertEqual(deadline(dict(ct=80, speed=8, charge=3)), dict(delay=3, moves=True))
        self.assertFalse(usage(dict(ct=10, actorDelay=10, selfNone=False, force=False, exhaustive=True)))

    def test_unsupported_widths_and_source_table_overread_reject(self):
        data = self.fixture()['masks'][0]['input']
        for changed in (dict(data, area=8), dict(data, width=17), dict(data, range=0)):
            with self.assertRaises(ValueError):
                mask(changed)
        with self.assertRaises(ValueError):
            deadline(dict(ct=80, speed=0, charge=3))
