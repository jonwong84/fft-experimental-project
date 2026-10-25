"""Authored structural and field-width checks, with no original data or execution."""
import struct
import unittest

import inspect_ready_handoff as tool


def fixture(curve=False, hold=9):
    raw = bytearray(8192)
    code = bytes([0x78, 0, hold, 0xe5, 56, 0, 0x77, 0xe5, 54, 0, 0x1c, 1])
    if curve:
        code += bytes([0x63, 7])
    code += bytes([0xdb])
    struct.pack_into('<I', raw, 0, 4+len(code))
    raw[4:4+len(code)] = code
    lengths = bytearray(244)
    lengths[0x78] = lengths[0xe5] = 2
    lengths[0x1c] = lengths[0x63] = 1
    return raw, lengths


class ReadyHandoffTests(unittest.TestCase):
    def test_tail_requires_release_barrier_speed_and_event_end(self):
        raw, lengths = fixture()
        result = tool.tail(raw, lengths, 4)
        self.assertEqual((7, 10, 11, 14, 16, 17), tuple(result[k] for k in
                         ('readyBarrierOffset', 'releaseOffset', 'releaseBarrierOffset',
                          'restoreSpeedOffset', 'endOffset', 'textOffset')))
        self.assertEqual({'1C': 1, '77': 1, 'DB': 1, 'E5': 1}, result['remainingOpcodeCounts'])
        self.assertFalse(result['immediateControlAfterReady'])

    def test_optional_curve_is_a_distinct_remaining_command(self):
        raw, lengths = fixture(True)
        result = tool.tail(raw, lengths, 4)
        self.assertTrue(result['hasCameraCurveRestore'])
        self.assertEqual((5, 18, 19), (result['remainingRecordCount'], result['endOffset'], result['textOffset']))

    def test_unsigned_hold_boundaries_do_not_change_completion_shape(self):
        for hold in (0, 1, 127, 128, 255):
            raw, lengths = fixture(hold=hold)
            self.assertEqual(hold, tool.tail(raw, lengths, 4)['hold'])

    def test_ready_must_be_condition_zero_at_an_exact_boundary(self):
        raw, lengths = fixture()
        self.assertRaises(ValueError, tool.tail, raw, lengths, 5)
        raw[5] = 1
        self.assertRaises(ValueError, tool.tail, raw, lengths, 4)

    def test_waits_cannot_be_substituted_or_shortened(self):
        for at, value in ((8, 54), (12, 56), (9, 1), (13, 1)):
            raw, lengths = fixture()
            raw[at] = value
            self.assertRaises(ValueError, tool.tail, raw, lengths, 4)

    def test_other_speed_or_thread_exit_is_not_event_end(self):
        for at, value in ((15, 2), (16, 0xd4)):
            raw, lengths = fixture()
            raw[at] = value
            self.assertRaises(ValueError, tool.tail, raw, lengths, 4)

    def test_missing_end_or_text_boundary_mismatch_rejects(self):
        for boundary in (16, 18, 8192):
            raw, lengths = fixture()
            struct.pack_into('<I', raw, 0, boundary)
            self.assertRaises(ValueError, tool.tail, raw, lengths, 4)

    def test_operand_length_changes_reject(self):
        for op in (0x78, 0xe5, 0x1c, 0x77, 0xdb):
            raw, lengths = fixture()
            lengths[op] += 1
            self.assertRaises(ValueError, tool.tail, raw, lengths, 4)

    def test_snapshot_uses_halfword_and_signed_widths_without_adjacent_bytes(self):
        ram = bytearray(tool.event.d.RAM_SIZE)
        def write(fmt, at, value):
            struct.pack_into(fmt, ram, at & 0x1fffffff, value)
        for at in (0x80165ff6, 0x80166048, 0x80166050, 0x8016d9c0):
            write('<I', at, 0xa5a50000)
        write('<H', 0x80165ff6, 0)
        write('<H', 0x80166048, 1)
        write('<H', 0x80166050, 4)
        write('<h', 0x8016d9c0, -1)
        write('<H', 0x8004e5d0 + 8, 0x81ff)
        result = tool.context(ram, 4)
        self.assertEqual((0, 1, 4, -1, 0x81ff), tuple(result[k] for k in
                         ('menuInputMode', 'menuOverlayState', 'inputSuppression', 'scriptThreadsIdle', 'finishWord')))
        self.assertFalse(result['playerControlObserved'])

    def test_unknown_input_mode_missing_ram_and_out_of_range_id_reject(self):
        ram = bytearray(tool.event.d.RAM_SIZE)
        struct.pack_into('<H', ram, 0x165ff6, 3)
        self.assertRaises(ValueError, tool.context, ram, 4)
        self.assertRaises(ValueError, tool.context, ram[:-1], 4)
        for value in (-1, 500, True):
            self.assertRaises(ValueError, tool.context, ram, value)

    def test_inspection_preserves_input_buffers(self):
        raw, lengths = fixture()
        before = bytes(raw), bytes(lengths)
        self.assertEqual(tool.tail(raw, lengths, 4), tool.tail(raw, lengths, 4))
        self.assertEqual(before, (bytes(raw), bytes(lengths)))


if __name__ == '__main__':
    unittest.main()
