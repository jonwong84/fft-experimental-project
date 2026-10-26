import copy
import json
from pathlib import Path
import tempfile
import unittest

from verify_sword_knockback_contract import (FIXTURE, audit, critical_roll, critical_xa,
                                           envelope_supported, fingerprint, resolve, validate_contract)

ROOT = Path(__file__).resolve().parents[2]


class SwordKnockbackContractTests(unittest.TestCase):
    def contract(self):
        return json.loads((ROOT / FIXTURE).read_text(encoding='utf-8'))

    def inputs(self, name='interior-East-North'):
        return copy.deepcopy(next(row['inputs'] for row in self.contract()['vectors'] if row['id'] == name))

    def test_contract_and_preserved_history_audit_without_local_reference(self):
        result = audit(ROOT)
        self.assertEqual((35, 20, 34), (result['vectors'], result['implementationCasesUnexecuted'], result['preservedFiles']))
        self.assertEqual(0, result['referenceFilesChecked'])
        self.assertEqual(0, result['managedBaselineFilesChecked'])
        self.assertFalse(result['productionImplementationExecuted'])

    def test_known_critical_stream_and_push(self):
        result = resolve(self.inputs())
        self.assertEqual([9511, 10768, 17155, 21975, 1171, 28206, 19852], result['raw'])
        self.assertEqual((45, 5, [4, 2], 3448561432, 7),
                         (result['damage'], result['hp'], result['target'], result['rngWord'], result['drawCount']))

    def test_exact_critical_raw_boundary_exhaustively(self):
        self.assertEqual(list(range(1311)), [raw for raw in range(32768) if critical_roll(raw)])

    def test_bonus_threshold_floor_and_signed_damage_envelope(self):
        self.assertEqual([5, 5, 6, 9], [critical_xa(5, raw) for raw in (0, 6553, 6554, 32767)])
        self.assertEqual(1, critical_xa(0, 32767))
        self.assertTrue(envelope_supported(255, 64))
        self.assertFalse(envelope_supported(255, 65))
        with self.assertRaisesRegex(ValueError, 'Signed-16'):
            resolve(dict(self.inputs(), pa=255, power=65))

    def test_lethal_and_offmap_suppression_keep_identical_draws(self):
        original = resolve(self.inputs())
        for hp in (44, 45):
            result = resolve(dict(self.inputs(), hp=hp))
            self.assertEqual([3, 2], result['target'])
            self.assertTrue(result['lethalCanceled'])
            self.assertEqual(original['raw'], result['raw'])
        self.assertTrue(resolve(dict(self.inputs(), hp=46))['moved'])
        for direction in ('East', 'North', 'West', 'South'):
            result = resolve(self.inputs('edge-' + direction))
            self.assertFalse(result['moved'])
            self.assertEqual(original['raw'], result['raw'])

    def test_zero_damage_push_and_brave_independence(self):
        result = resolve(dict(self.inputs(), power=0))
        self.assertEqual((0, 50, True), (result['damage'], result['hp'], result['moved']))
        first = resolve(self.inputs('brave-low-high'))
        second = resolve(self.inputs('brave-high-low'))
        self.assertEqual(first, second)

    def test_empty_proc_success_does_not_cancel_push_or_add_draw(self):
        result = resolve(self.inputs('empty-proc-pass'))
        self.assertEqual([23933, 31194, 6516, 31310, 885, 5547, 565], result['raw'])
        self.assertEqual((25, True, 2184573576, 7),
                         (result['damage'], result['moved'], result['rngWord'], result['drawCount']))

    def test_normal_miss_and_side_hit_streams(self):
        result = resolve(self.inputs('normal'))
        self.assertEqual((25, False, 2516284547, 6),
                         (result['damage'], result['moved'], result['rngWord'], result['drawCount']))
        result = resolve(self.inputs('miss'))
        self.assertEqual((False, 0, 3587663605, 4),
                         (result['hit'], result['damage'], result['rngWord'], result['drawCount']))
        self.assertTrue(resolve(self.inputs('side-hit'))['moved'])

    def test_each_branch_uses_only_its_budget_and_never_mutates_input(self):
        for name, budget in (('interior-East-North', 7), ('normal', 6), ('miss', 4)):
            original = self.inputs(name)
            before = copy.deepcopy(original)
            self.assertEqual(0xffffffffffffffff, resolve(dict(original, drawCount=0xffffffffffffffff-budget))['drawCount'])
            for remaining in range(budget):
                with self.assertRaisesRegex(ValueError, 'Draw count exhausted'):
                    resolve(dict(original, drawCount=0xffffffffffffffff-remaining))
            self.assertEqual(before, original)

    def test_rejects_damaged_contract_and_accidental_implementation_claims(self):
        for mutation in ('vector', 'inventory', 'boundary', 'status', 'execution'):
            contract = self.contract()
            if mutation == 'vector': contract['vectors'][0]['expected']['target'][0] -= 1
            elif mutation == 'inventory': contract['vectors'].pop()
            elif mutation == 'boundary': contract['arithmeticBoundaries']['criticalLastRaw'] += 1
            elif mutation == 'status': contract['implementationCases'][0]['status'] = 'passed'
            else: contract['productionImplementationExecuted'] = True
            with self.assertRaises(ValueError): validate_contract(contract)

    def test_noncardinal_or_unsupported_inputs_are_not_silently_evaluated(self):
        for changes in (dict(target=[3, 3]), dict(target=[4, 2]), dict(pa=-1), dict(seed=-1),
                        dict(actorFacing='Unknown'), dict(actorBrave=0)):
            with self.assertRaises(ValueError): resolve(dict(self.inputs(), **changes))

    def test_text_fingerprints_are_explicit_utf8_and_portable_across_newlines(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root/'a.txt').write_bytes('Facing → North\n'.encode('utf-8'))
            first = fingerprint(root, 'a.txt')
            (root/'a.txt').write_bytes('Facing → North\r\n'.encode('utf-8'))
            self.assertEqual(first, fingerprint(root, 'a.txt'))


if __name__ == '__main__':
    unittest.main()
