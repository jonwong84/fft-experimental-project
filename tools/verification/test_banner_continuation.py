"""Authored task contexts/packets; no original code, media or emulator required."""
import copy
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

import capture_banner_continuation as c
import test_native_banner as native
import trace_banner_draw_state as dma
import compare_visual_checkpoints as v


def fixture(pc=0x801c9384, counter=18, crop=0):
    definition, ram = native.frozen_fixture()
    write = lambda address, value: struct.pack_into('<I', ram, address & 0x1fffffff, value)
    write(c.THREADS_POINTER, c.THREADS_BASE)
    slot = c.THREADS_BASE + 3 * 1024; sp = slot + 0x360
    for offset, value in ((0, 8), (4, 60), (0x2c, 0), (0x3c, sp), (0x44, pc), (0x48, 1), (0x4c, 56)):
        write(slot + offset, value)
    for offset, value in ((0x10, 60), (0x18, counter), (0x20, 6)): write(sp + offset, value)
    for index, piece in enumerate(definition['pieces']):
        piece['height'] = 31
        for vertex, (dx, dy) in enumerate(((0, 0), (2, 0), (0, 31 - crop), (2, 31 - crop))):
            address = dma.BANNER_BASE + index * 52 + vertex * 12
            write(address + 8, piece['x'] + 128 + dx | (piece['y'] + crop + dy) << 16)
            uv = piece['u'] + dx | ((piece['v'] + crop + dy) & 255) << 8
            if vertex == 1: uv |= (0x26 if piece['material'] == 'add' else 0x46) << 16
            write(address + 12, uv)
    return definition, ram, write, slot, sp


class BannerContinuationTests(unittest.TestCase):
    def test_phase_is_decoded_from_saved_pc_and_counter_not_frontend_budget(self):
        for pc, counter, phase, tick in ((0x801c9384, 18, 'Revealing', 18), (0x801c9384, 19, 'Revealing', 19),
                (0x801c9ad8, 0, 'Holding', 20), (0x801c9ad8, 59, 'Holding', 79),
                (0x801c9bc4, 0, 'Hiding', 80), (0x801c9bc4, 9, 'Hiding', 89), (0x801c9f68, 10, 'ExitYield', 90)):
            with self.subTest(phase=phase, counter=counter):
                definition, ram, *_ = fixture(pc, counter)
                result = c.inspect_ram(ram, definition)
                self.assertEqual((phase, tick, counter), (result['phase'], result['logicalTick'], result['phaseCounter']))

    def test_each_cropped_quad_binds_all_xy_and_uv_vertices_without_rescaling(self):
        for crop in (0, 8, 16, 24, 31):
            definition, ram, *_ = fixture(0x801c9bc4, min(9, crop // 8 + 1), crop)
            result = c.inspect_ram(ram, definition)
            self.assertEqual([dict(PieceIndex=i, CropTop=crop, VisibleRows=31 - crop) for i in range(6)], result['draws'])

    def test_completion_requires_cleared_task_at_scheduler_exit_and_empty_chain(self):
        definition, ram, write, slot, _ = fixture()
        write(slot + 0x44, 0x8014c984); write(slot + 0x48, 0); write(slot + 0x4c, 0)
        self.assertRaises(ValueError, c.inspect_ram, ram, definition, 3)
        write(0x80056b18 + 0x17f * 4, 0xffffff)
        result = c.inspect_ram(ram, definition, 3)
        self.assertEqual(('Completed', 91, [], False), (result['phase'], result['logicalTick'], result['draws'], result['taskRunning']))

    def test_table_pointer_missing_or_duplicate_task_reject(self):
        for address, value in ((c.THREADS_POINTER, 0x80000000), (c.THREADS_BASE + 3 * 1024 + 0x4c, 0),
                               (c.THREADS_BASE + 4 * 1024 + 0x4c, 56)):
            definition, ram, write, *_ = fixture(); write(address, value)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition)

    def test_selected_slot_cannot_be_reused_or_suspended_or_reclassified(self):
        for offset, value in ((0, 0), (4, 61), (0x48, 0), (0x4c, 0), (0x44, 0x801c3bc8)):
            definition, ram, write, slot, _ = fixture(); write(slot + offset, value)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition, 3)

    def test_stack_and_spilled_variables_are_bounded(self):
        for offset, value in ((0x3c, 0x80001000), (0x3c, c.THREADS_BASE + 3 * 1024 + 0x3f0)):
            definition, ram, write, slot, _ = fixture(); write(slot + offset, value)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition)
        for offset, value in ((0x10, 59), (0x20, 5), (0x18, 20)):
            definition, ram, write, _, sp = fixture(); write(sp + offset, value)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition)

    def test_phase_counter_ranges_and_final_yield_are_exact(self):
        for pc, counter in ((0x801c9ad8, 60), (0x801c9bc4, 10), (0x801c9f68, 9), (0x801c9384, 0xffffffff)):
            definition, ram, *_ = fixture(pc, counter)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition)

    def test_packet_order_bank_geometry_and_uv_corruption_reject(self):
        for address in (dma.BANNER_BASE + 8, dma.BANNER_BASE + 12, dma.BANNER_BASE + 20,
                        dma.BANNER_BASE + 32, dma.BANNER_BASE + 48):
            definition, ram, write, *_ = fixture(); write(address, dma.word(ram, address) ^ 1)
            self.assertRaises(ValueError, c.inspect_ram, ram, definition)
        definition, ram, write, slot, _ = fixture(); write(slot + 0x2c, 1)
        self.assertRaises(ValueError, c.inspect_ram, ram, definition)
        definition, ram, write, *_ = fixture(); write(0x80056b18 + 0x17f * 4, 0xffffff)
        self.assertRaises(ValueError, c.inspect_ram, ram, definition)

    def test_labeled_state_requires_unchanged_code_fingerprints(self):
        _, ram, *_ = fixture(); state = native.state_with_ram(ram)
        self.assertRaises(ValueError, c.read_ram, state)  # Authored RAM does not contain original functions.
        fingerprint = (0, 16, v.sha(ram[:16]))
        with patch.object(c, 'CODE', (fingerprint,)):
            self.assertEqual(bytes(ram), c.read_ram(state))
            ram[0] ^= 1
            self.assertRaises(ValueError, c.read_ram, native.state_with_ram(ram))

    def test_command_bounds_and_input_isolation(self):
        profile = dict(executable='retroarch.exe', coreLibrary='core.dll', cue='game.cue')
        args = c.command(profile, Path('/profile'), Path('/output'), 148)
        self.assertEqual('148', args[args.index('--max-frames') + 1]); self.assertEqual(2, args.count('--nodevice'))
        self.assertEqual('0', args[args.index('--entryslot') + 1]); self.assertIn('noload-nosave', args)
        self.assertNotIn('--record-replay', args)
        for frames in (0, 3, 149, True, 4.5):
            self.assertRaises(ValueError, c.command, profile, Path('/profile'), Path('/output'), frames)

    def test_existing_output_is_preserved_before_plan_or_emulator_access(self):
        base = c.profile_tool.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='continuation-', dir=base) as directory:
            root = Path(directory); (root / 'sentinel').write_text('keep')
            self.assertRaises(ValueError, c.run, root / 'missing-plan', root / 'missing-profile', root)
            self.assertEqual('keep', (root / 'sentinel').read_text())


if __name__ == '__main__':
    unittest.main()
