"""Offline source branch vectors; managed ZOD-166 acceptance remains unexecuted."""
import copy
import json
import unittest
from verify_ai_command_continuation_contract import FIXTURE, dispatch


class AiCommandContinuationContractTests(unittest.TestCase):
    def test_source_vectors_preserve_description_inputs(self):
        for row in json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors']:
            with self.subTest(vector=row['id']):
                before = copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], dispatch(row['input']))
                self.assertEqual(before, row['input'])

    def test_fresh_dispatch_must_not_reenter_the_earlier_no_progress_guard(self):
        data = dict(stage='after-selection', decided=255, saved=[False,False], current=[False,False],
                    actFirst=False, hasSkillset=True, sameTile=False)
        self.assertEqual('Move', dispatch(data)['kind'])
        data['stage'] = 'entry'
        self.assertEqual(dict(kind='EndTurn',recordCommand=False,selectionRequired=False), dispatch(data))

    def test_exact_decided_byte_guard_differs_from_noncanonical_nonzero_source_bytes(self):
        data = dict(stage='entry', decided=1, saved=[True,False], current=[True,False],
                    actFirst=False, hasSkillset=True, sameTile=True)
        self.assertEqual('Attack', dispatch(data)['kind'])
        data['decided'] = 255
        self.assertEqual('EndTurn', dispatch(data)['kind'])


if __name__ == '__main__':
    unittest.main()
