"""Source-description checks only; no execution of the proposed non-Attack AI port."""
import copy
import json
import unittest

from verify_ai_ability_iteration_contract import FIXTURE, KERNELS, cursor, load_entry, validate


class AiAbilityIterationContractTests(unittest.TestCase):
    def test_authored_descriptions_match_literals_and_preserve_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        validate(fixture)
        for family, calculate in KERNELS.items():
            for row in fixture[family]:
                with self.subTest(vector=row['id']):
                    before = copy.deepcopy(row['input'])
                    self.assertEqual(row['expected'], calculate(row['input']))
                    self.assertEqual(before, row['input'])

    def test_malformed_cursor_and_packed_loader_descriptions_reject(self):
        for data in [dict(resume=True, counter=34, entries=[]), dict(resume=False, counter=0, entries=[]),
                     dict(resume=True, counter=-1, entries=[])]:
            with self.subTest(data=data), self.assertRaises(ValueError):
                cursor(data)
        for change in [dict(packedId=-1), dict(packedId=21 * 1024), dict(usage=256), dict(staticAiBytes=[1, 2, 3])]:
            with self.subTest(change=change), self.assertRaises(ValueError):
                load_entry(dict(dict(packedId=148, staticAiBytes=[1, 2, 3, 4], usage=128), **change))

    def test_tampered_or_runtime_claiming_descriptions_reject(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for change in ('accuracy-scaled-hp', 'production-claim'):
            altered = copy.deepcopy(fixture)
            if change == 'accuracy-scaled-hp':
                row = next(row for row in altered['throwStoneVectors'] if row['id'] == 'accuracy-does-not-scale-projected-hp')
                row['expected']['targetHp'] = 48
            else:
                altered['productionImplemented'] = True
            with self.subTest(change=change), self.assertRaises(ValueError):
                validate(altered)


if __name__ == '__main__':
    unittest.main()
