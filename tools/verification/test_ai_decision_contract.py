import copy
import hashlib
import json
from pathlib import Path
import struct
import tempfile
import unittest

from verify_ai_decision_contract import FIXTURE, calculate, direction, rank_compare, validate, verify_prepared_record

ROOT = Path(__file__).resolve().parents[2]


class AiDecisionContractTests(unittest.TestCase):
    def contract(self):
        return json.loads((ROOT/FIXTURE).read_text(encoding='utf-8'))

    def vector(self, name):
        return copy.deepcopy(next(row for row in self.contract()['vectors'] if row['id'] == name))

    def test_current_description_vectors_are_consistent(self):
        result = validate(self.contract())
        self.assertEqual((50, 8, 20), (result['vectors'], result['families'],
                         result['implementationCasesUnexecuted']))

    def test_exact_action_tie_keeps_incumbent_but_lower_proximity_wins(self):
        tie = self.vector('exact-tie-keeps-earlier')
        self.assertEqual(dict(comparison=0, firstIsHigher=False), calculate(tie['family'], tie['inputs']))
        tie['inputs']['first']['proximity'] -= 1
        self.assertEqual(dict(comparison=0, firstIsHigher=True), calculate(tie['family'], tie['inputs']))

    def test_signed_priority_and_decided_marker_precede_hit(self):
        first = dict(decided=0, priority=65535, hit=100)
        second = dict(decided=0, priority=0, hit=0)
        self.assertEqual(-1, rank_compare(first, second))
        first['decided'] = 255
        self.assertEqual(1, rank_compare(first, second))

    def test_destination_ties_take_last_and_limit_remains_inclusive(self):
        row = self.vector('priority-tie-last')
        self.assertEqual(dict(selected='x1'), calculate(row['family'], row['inputs']))
        row['inputs']['limit'] = 1
        self.assertEqual(dict(selected=None), calculate(row['family'], row['inputs']))
        row['inputs']['limit'] = 2
        self.assertEqual(dict(selected='x1'), calculate(row['family'], row['inputs']))

    def test_lottery_uses_modulo_not_multiply_divide_and_equality_fails(self):
        # First BIOS raw16838 gives modulo38, but scaled percentage51.
        result = calculate('ranked-lottery', dict(seed=1, chances=[39, 100]+[0]*6))
        self.assertEqual((0, 1, 16838), (result['selected'], result['draws'], result['raw'][0]))
        result = calculate('ranked-lottery', dict(seed=1, chances=[38, 100]+[0]*6))
        self.assertEqual((1, 2), (result['selected'], result['draws']))

    def test_zero_slots_draw_and_all_failed_nonzero_list_falls_back_to_first(self):
        empty = calculate('ranked-lottery', dict(seed=1, chances=[0]*8))
        self.assertEqual((None, False, 8, 2633739833),
                         (empty['selected'], empty['fallback'], empty['draws'], empty['rngWord']))
        fallback = calculate('ranked-lottery', dict(seed=1, chances=[1]+[0]*7))
        self.assertEqual((0, True, 8), (fallback['selected'], fallback['fallback'], fallback['draws']))

    def test_ai_projection_is_full_normal_damage_with_separate_hit_and_four_evade_draws(self):
        row = self.vector('execution-critical-seed-projects-normal')
        result = calculate(row['family'], row['inputs'])
        self.assertEqual((25, 25, 90, False, False, 4, 3587663605),
                         (result['damage'], result['hp'], result['hitPercent'], result['critical'],
                          result['knockback'], result['resolutionDraws'], result['rngWord']))
        row['inputs']['evade'] = 100
        result = calculate(row['family'], row['inputs'])
        self.assertEqual((0, 50, 0, 4), (result['damage'], result['hp'], result['hitPercent'], result['resolutionDraws']))

    def test_score_hp_quantization_critical_and_dead_weights(self):
        for name, expected in [('healthy-state', 0), ('damage25-state', 64),
                               ('critical-state', 135), ('dead-state', 320), ('own-critical-state', -135)]:
            row = self.vector(name)
            self.assertEqual(dict(priority=expected), calculate(row['family'], row['inputs']))

    def test_selected_plan_emits_one_command_and_requires_post_act_redecision(self):
        for name, command, redecide in [('move-before-act', 'Move', True), ('act-before-move', 'Act', True),
                ('already-moved-act', 'Act', False), ('post-act-replan-move', 'Move', True),
                ('unchanged-flags-end', 'EndTurn', False)]:
            row = self.vector(name)
            self.assertEqual(dict(command=command, redecide=redecide), calculate(row['family'], row['inputs']))

    def test_wait_diagonals_name_mapping_and_overlap_are_explicit(self):
        for target, expected in [([1, 1], 'South'), ([1, 3], 'West'), ([3, 3], 'North'), ([3, 1], 'East')]:
            self.assertEqual(expected, direction([2, 2], target))
        row = self.vector('face-east')
        row['inputs']['enemy'] = [2, 2]
        with self.assertRaisesRegex(ValueError, 'overlap'): calculate(row['family'], row['inputs'])
        row['inputs']['hint'] = 'KeepCurrent'; row['inputs']['current'] = 'North'
        self.assertEqual(dict(facing='North'), calculate(row['family'], row['inputs']))

    def test_nonplayer_out_of_reach_branch_uses_modulo7_only_when_needed(self):
        for name, reach, draws in [('enemy-halves', 2, 1), ('enemy-keeps', 4, 1),
                                   ('player-team-no-roll', 4, 0), ('in-reach-no-roll', 4, 0)]:
            row = self.vector(name); result = calculate(row['family'], row['inputs'])
            self.assertEqual((reach, draws), (result['reach'], result['draws']))

    def test_tampered_vectors_missing_families_and_implementation_claims_fail(self):
        for mutation in ('vector', 'family', 'claim', 'case'):
            contract = self.contract()
            if mutation == 'vector': contract['vectors'][0]['expected']['firstIsHigher'] = False
            elif mutation == 'family': contract['vectors'] = [row for row in contract['vectors'] if row['family'] != 'wait-facing']
            elif mutation == 'claim': contract['fullAiExecuted'] = True
            else: contract['implementationCases'][0]['status'] = 'passed'
            with self.assertRaises(ValueError): validate(contract)

    def test_prepared_record_audit_requires_exact_identity_and_confines_the_slice(self):
        data = bytearray(0x810); data[:8] = b'PS-X EXE'
        struct.pack_into('<II', data, 0x18, 0x80010000, 16)
        record = dict(exeByteLength=len(data), exeSha256=hashlib.sha256(data).hexdigest(), loadAddress=0x80010000,
                      payloadByteLength=16, address=0x80010008, byteLength=8,
                      recordSha256=hashlib.sha256(data[0x808:]).hexdigest())
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)/'synthetic.exe'; path.write_bytes(data)
            verify_prepared_record(path, record)
            for change in (dict(exeSha256='0'*64), dict(recordSha256='0'*64), dict(address=0x8000ffff), dict(address=0x80010010)):
                with self.assertRaises(ValueError): verify_prepared_record(path, dict(record, **change))


if __name__ == '__main__':
    unittest.main()
