"""Source descriptions only, independent of the managed scan implementation."""
import copy
import json
import unittest

from verify_ai_throw_stone_scan_contract import FIXTURE, describe_scan, validate


class AiThrowStoneScanContractTests(unittest.TestCase):
    def test_authored_order_consumption_and_skip_literals_preserve_inputs(self):
        fixture = json.loads(FIXTURE.read_text(encoding='utf-8'))
        validate(fixture)
        for row in fixture['vectors']:
            with self.subTest(vector=row['id']):
                before = copy.deepcopy(row['input'])
                self.assertEqual(row['expected'], describe_scan(row['input']))
                self.assertEqual(before, row['input'])

    def test_malformed_source_key_and_duplicate_origin_descriptions_reject(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))['vectors'][0]['input']
        for key, value in [('tilePriority', 32768), ('tilePriority', -32769), ('proximity', 256), ('clearPath', 1)]:
            altered = copy.deepcopy(original); altered['origins'][0][key] = value
            with self.subTest(key=key, value=value), self.assertRaises(ValueError):
                describe_scan(altered)
        altered = copy.deepcopy(original); altered['origins'].append(altered['origins'][0])
        with self.assertRaises(ValueError):
            describe_scan(altered)

    def test_tampered_order_and_runtime_claims_reject(self):
        original = json.loads(FIXTURE.read_text(encoding='utf-8'))
        for kind in ('order', 'production'):
            altered = copy.deepcopy(original)
            if kind == 'order':
                altered['vectors'][0]['expected']['ordered'].reverse()
            else:
                altered['productionImplemented'] = True
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                validate(altered)


if __name__ == '__main__':
    unittest.main()
