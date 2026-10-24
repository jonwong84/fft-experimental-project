"""Authored event blocks and saved contexts only; no original instructions or media."""
from pathlib import Path
import struct
import tempfile
import unittest

import inspect_banner_event as tool
import test_native_banner as native


def fixture(frame_wait=False):
    ram = bytearray(tool.d.RAM_SIZE)
    write = lambda address, value: struct.pack_into('<I', ram, address & 0x1fffffff, value)
    raw = bytearray(8192)
    struct.pack_into('<I', raw, 0, 11)
    raw[4:11] = bytes([0xf1, 25, 0, 0xe5, 56, 0, 0xdb] if frame_wait else [0x78, 8, 60, 0xe5, 56, 0, 0xdb])
    archive = bytearray(4096000); archive[4 * 8192:5 * 8192] = raw
    lengths = bytearray(244)
    for opcode in (0x78, 0xe5, 0xf1): lengths[opcode] = 2
    at = tool.BLOCK & 0x1fffffff
    ram[at:at + 8192] = b'\xf2' * 4 + raw[4:]
    base = tool.THREADS + 1024; parent_sp = base + 0x388
    sp = parent_sp - 0x28 if frame_wait else parent_sp
    for address, value in ((0x80165f9c, tool.VARIABLES), (0x80173ca4, tool.BLOCK),
        (tool.c.THREADS_POINTER, tool.THREADS), (tool.VARIABLES + 0x27 * 4, 4), (0x801696fc, 4),
        (0x80173f8c, tool.BLOCK + 11), (base + 0x48, 1), (base + 0x4c, 68),
        (base + 0x40, 4 if frame_wait else 7), (base + 0x20, 0xf1 if frame_wait else 0xe5),
        (base + 0x3c, sp), (base + 0x44, 0x8014c87c if frame_wait else 0x8014597c),
        (base + 0x14, 25 if frame_wait else tool.THREADS), (base + 0x10, 24 if frame_wait else 1),
        (base + 0x28, 56), (parent_sp + 0x4c, 0x80143b98)):
        write(address, value)
    if frame_wait: write(sp + 0x20, 0x80145a34)
    worker = tool.THREADS + 3 * 1024
    for offset, value in ((0, 8), (4, 60), (0x48, 1), (0x4c, 56), (0x44, 0x801c9384)):
        write(worker + offset, value)
    return ram, archive, lengths, write, base, sp, worker


class BannerEventTests(unittest.TestCase):
    def test_current_cache_payload_wait_and_worker_identify_one_event(self):
        ram, archive, lengths, *_ = fixture()
        result = tool.inspect(ram, archive, lengths)
        self.assertEqual((4, 4, [4], 7, 4), tuple(result[key] for key in
            ('currentEventId', 'loadedEventId', 'uniqueArchiveMatch', 'instructionOffset', 'attributedBannerOffset')))
        self.assertEqual(dict(kind='TaskWait', taskId=56), result['wait'])
        self.assertFalse(result['commonBattleStartEventObserved'])

    def test_stale_or_inconsistent_current_and_cached_ids_reject(self):
        for change_both in (False, True):
            ram, archive, lengths, write, *_ = fixture()
            write(tool.VARIABLES + 0x27 * 4, 401)
            if change_both: write(0x801696fc, 401)
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_missing_or_ambiguous_payload_matches_reject(self):
        for duplicate in (False, True):
            ram, archive, lengths, *_ = fixture()
            if duplicate: archive[5 * 8192:6 * 8192] = archive[4 * 8192:5 * 8192]
            else: archive[4 * 8192 + 5] ^= 1
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_processed_header_and_all_bound_pointers_are_required(self):
        for address in (tool.BLOCK, 0x80165f9c, 0x80173ca4, tool.c.THREADS_POINTER, 0x80173f8c):
            ram, archive, lengths, write, *_ = fixture(); write(address, 0)
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_instruction_boundary_opcode_and_live_interpreter_identity_bind(self):
        for offset, value in ((0x40, 8), (0x20, 0x78), (0x48, 0), (0x4c, 56), (0x44, 0x80145980)):
            ram, archive, lengths, write, base, *_ = fixture(); write(base + offset, value)
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_wait_selector_table_stack_and_caller_bind(self):
        for field in ('table', 'selector', 'sp', 'caller'):
            ram, archive, lengths, write, base, sp, _ = fixture()
            write(dict(table=base + 0x14, selector=base + 0x28, sp=base + 0x3c, caller=sp + 0x4c)[field], 0)
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_adjacent_command_requires_matching_unique_active_banner_worker(self):
        for field in ('condition', 'hold', 'idle', 'duplicate'):
            ram, archive, lengths, write, _, _, worker = fixture()
            if field == 'duplicate':
                other = tool.THREADS + 4 * 1024; write(other + 0x48, 1); write(other + 0x4c, 56)
            else: write(worker + dict(condition=0, hold=4, idle=0x48)[field], 0)
            self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_flags_use_packed_words_and_preserve_neighboring_bits(self):
        ram, _, _, write, *_ = fixture()
        word = tool.VARIABLES + 4 * (0x80 + (0x1fd - 0x80) // 32)
        write(word, 1 << 29 | 1 << 31)
        # A decoy at a uniform-word index must not affect either packed flag.
        write(tool.VARIABLES + 4 * 0x1fe, 1)
        self.assertEqual(1, tool.packed_flag(ram, 0x1fd)); self.assertEqual(0, tool.packed_flag(ram, 0x1fe))
        self.assertRaises(ValueError, tool.packed_flag, ram, 0x400)

    def test_raw_parser_rejects_boundary_unknown_opcode_and_missing_sentinel(self):
        for mode in ('boundary', 'opcode', 'sentinel', 'length'):
            _, archive, lengths, *_ = fixture(); raw = bytearray(archive[4 * 8192:5 * 8192])
            if mode == 'boundary': struct.pack_into('<I', raw, 0, 6)
            elif mode == 'opcode': raw[4] = 244
            elif mode == 'sentinel': raw[10] = 0
            else: raw = raw[:-1]
            self.assertRaises(ValueError, tool.instructions, raw, lengths)

    def test_frame_wait_unwinds_helper_and_accepts_last_active_yield(self):
        for ordinal in (1, 24, 25):
            ram, archive, lengths, write, base, *_ = fixture(True); write(base + 0x10, ordinal)
            self.assertEqual(dict(kind='FrameWait', count=25, yieldOrdinal=ordinal), tool.inspect(ram, archive, lengths)['wait'])
        for field in ('zero', 'over', 'count', 'caller'):
            ram, archive, lengths, write, base, sp, _ = fixture(True)
            address, value = dict(zero=(base + 0x10, 0), over=(base + 0x10, 26), count=(base + 0x14, 24), caller=(sp + 0x20, 0))[field]
            write(address, value); self.assertRaises(ValueError, tool.inspect, ram, archive, lengths)

    def test_missing_resident_code_and_existing_output_reject(self):
        ram, *_ = fixture()
        self.assertRaises(ValueError, tool.read_ram, native.state_with_ram(ram))
        base = tool.v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='event-identity-', dir=base) as name:
            out=Path(name); (out / 'sentinel').write_bytes(b'keep')
            self.assertRaises(ValueError, tool.run, out / 'missing', out)
            self.assertEqual(b'keep', (out / 'sentinel').read_bytes())


if __name__ == '__main__': unittest.main()
