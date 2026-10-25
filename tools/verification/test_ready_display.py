"""Authored Ready contexts/pixels only; these tests never require original files or an emulator."""
import copy
import struct
import unittest

import compare_ready_display as r
import test_banner_background as background
import test_banner_precision as precision


def thread_fixture(tick):
    ram = bytearray(r.d.RAM_SIZE)
    def write(at, value): struct.pack_into('<I', ram, at & 0x1fffffff, value)
    write(r.h.FRAME_COUNTER, 100 + tick); write(r.c.THREADS_POINTER, r.c.THREADS_BASE)
    child, dispatcher = r.c.THREADS_BASE + 4096, r.c.THREADS_BASE + 3072
    # Independently authored transition vectors, including both hold banks and the exit call frame.
    vectors = {0: (0x801ca440, 56, 1), 9: (0x801ca440, 128, 0), 10: (0x801ca524, 2080, 1),
               59: (0x801ca524, 0, 0), 60: (0x801ca5f4, 116, 1), 66: (0x801ca5f4, 44, 1),
               67: (0x801ca608, 44, 1), 68: (0x8014c984, 44, 1), 69: (0x8014c984, 44, 1)}
    pc, saved, bank = vectors[tick]
    for off, value in ((8, 0 if tick < 60 else 2), (0x1c, saved), (0x20, 6), (0x24, bank), (0x2c, 6),
                       (0x3c, 0x8016abfc if tick < 68 else 0x8016abe4), (0x44, pc), (0x48, int(tick < 68))): write(child + off, value)
    write(0x8016ac58, 0x801c9fac); write(0x8016abf4, 0x801ca610)
    for off, value in ((4, 60), (0x44, 0x801c3bc8 if tick < 60 else 0x8014c9e8 if tick < 69 else 0x8014c984),
                       (0x48, int(tick < 69)), (0x4c, 56 if tick < 69 else 0), (0x10, tick if tick < 60 else 4096),
                       (0x14, 60), (0x3c, 0x8016a7fc if tick < 60 else 0x8016a80c)): write(dispatcher + off, value)
    write(0x8016a81c, 0x801cb054); write(0x8016a820, 0x801cb120)
    return ram, write


def dma_fixture():
    ram = bytearray(r.d.RAM_SIZE)
    def write(at, value): struct.pack_into('<I', ram, at & 0x1fffffff, value)
    ot, mode = 0x80056b18, r.d.BURST_MODE_BASE
    write(r.d.CURRENT_BUFFER, r.d.BUFFER_BASE); write(r.d.BUFFER_BASE + 0xe5b4, ot); write(r.d.SCRIPT_OT, ot)
    write(ot + 0x17f * 4, mode & 0xffffff)
    write(mode, 2 << 24 | (r.d.BANNER_BASE + 5 * 52) & 0xffffff)
    write(mode + 4, 0xe1000006); write(mode + 8, 0xe2000000)
    glyphs = [dict(x=i * 20, y=2, u=i * 20, v=0, width=16, height=24,
                   colors=[dict(r=128, g=64, b=32), dict(r=32, g=64, b=128), dict(r=64, g=128, b=32), dict(r=64, g=32, b=128)]) for i in range(6)]
    for i, g in enumerate(glyphs):
        address = r.d.BANNER_BASE + i * 52
        write(address, 12 << 24 | ((address - 52) & 0xffffff if i else 0xffffff))
        for j, (x, y) in enumerate(((0, 0), (16, 0), (0, 24), (16, 24))):
            # Scale 64 gives authored exact halves at the four vertices.
            color = g['colors'][j]
            write(address + 4 + j * 12, color['r'] // 2 | color['g'] // 2 << 8 | color['b'] // 2 << 16 | (0x3e << 24 if j == 0 else 0))
            write(address + 8 + j * 12, g['x'] + 128 + x | (g['y'] + y) << 16)
            write(address + 12 + j * 12, g['u'] + x | y << 8 | (0x3f9a << 16 if j == 0 else 6 << 16 if j == 1 else 0))
    return ram, write, glyphs, dict(scale=64, bank=0)


class ReadyDisplayTests(unittest.TestCase):
    def test_native_transition_vectors_use_submitted_scale_not_saved_next_scale(self):
        expected = {0: 48, 9: 120, 10: 128, 59: 128, 60: 128, 66: 56, 67: None, 68: None, 69: None}
        for tick, scale in expected.items():
            with self.subTest(tick=tick):
                ram, _ = thread_fixture(tick); result = r.threads(ram, 100)
                self.assertEqual((tick, scale), (result['logicalTick'], result['scale']))

    def test_wrong_delay_slot_scale_or_hold_bank_offset_rejects(self):
        for tick, wrong in ((0, 48), (10, 128), (59, 2080), (60, 128), (66, 56), (67, 48)):
            ram, write = thread_fixture(tick); write(r.c.THREADS_BASE + 4096 + 0x1c, wrong)
            self.assertRaises(ValueError, r.threads, ram, 100)

    def test_child_slot_reuse_wrong_stack_and_cancel_parameter_reject(self):
        child = r.c.THREADS_BASE + 4096
        for off, wrong in ((0, 1), (8, 1), (0x3c, 0x8016ac00), (0x44, 0x801ca608), (0x48, 0), (0x4c, 56), (0x24, 1)):
            ram, write = thread_fixture(9); write(child + off, wrong)
            self.assertRaises(ValueError, r.threads, ram, 100)

    def test_exit_call_frame_and_caller_are_required(self):
        for at, wrong in ((0x8016abf4, 0), (r.c.THREADS_BASE + 4096 + 0x3c, 0x8016abfc)):
            ram, write = thread_fixture(68); write(at, wrong)
            self.assertRaises(ValueError, r.threads, ram, 100)

    def test_dispatcher_wait_counter_caller_and_unique_task_are_required(self):
        for at, wrong in ((r.c.THREADS_BASE + 3072 + 0x10, 8), (0x8016a81c, 0), (r.c.THREADS_BASE + 5120 + 0x48, 1)):
            ram, write = thread_fixture(9); write(at, wrong)
            if at == r.c.THREADS_BASE + 5120 + 0x48: write(r.c.THREADS_BASE + 5120 + 0x4c, 56)
            self.assertRaises(ValueError, r.threads, ram, 100)

    def test_outside_bounded_history_is_not_classified(self):
        for tick in (-1, 70, True, 1.0): self.assertRaises(ValueError, r.phase, tick)

    def test_reverse_six_glyph_dma_uses_four_corners_and_bank_two(self):
        ram, _, glyphs, task = dma_fixture(); result = r.packets(ram, task, glyphs)
        self.assertEqual([5, 4, 3, 2, 1, 0], [q['glyphIndex'] for q in result['draws']])

    def test_dma_color_geometry_uv_clut_material_and_mode_mutations_reject(self):
        for offset in (4, 8, 12, 16, 24, 28, 40):
            ram, write, glyphs, task = dma_fixture(); at = r.d.BANNER_BASE + 5 * 52 + offset
            write(at, r.d.word(ram, at) ^ 1)
            self.assertRaises(ValueError, r.packets, ram, task, glyphs)
        for word in (0xe1000206, 0xe1000026):
            ram, write, glyphs, task = dma_fixture(); write(r.d.BURST_MODE_BASE + 4, word)
            self.assertRaises(ValueError, r.packets, ram, task, glyphs)

    def test_unknown_packet_invalidates_mode_and_empty_yield_rejects_stale_draws(self):
        ram, write, glyphs, task = dma_fixture()
        write(r.d.BURST_MODE_BASE, 2 << 24 | 0x1000)
        write(0x1000, 1 << 24 | (r.d.BANNER_BASE + 5 * 52) & 0xffffff); write(0x1004, 0xf0000000)
        self.assertRaises(ValueError, r.packets, ram, task, glyphs)
        ram, _, glyphs, task = dma_fixture(); task['scale'] = None
        self.assertRaises(ValueError, r.packets, ram, task, glyphs)

    def test_two_frame_binding_rejects_other_capture_history_even_if_counters_match(self):
        queued = dict(epoch='a', frameCounter=100, polarity=1, pendingDrawOrigin=[0, 240], task=dict(logicalTick=8, draws=[]))
        current = dict(epoch='a', frameCounter=102, polarity=1, frontOrigin=[0, 240], task=dict(logicalTick=10))
        self.assertEqual(8, r.bind(current, queued)['displayedLogicalTick'])
        for key, wrong in (('epoch', 'b'), ('frameCounter', 101), ('polarity', 0), ('pendingDrawOrigin', [0, 0])):
            changed = dict(queued, **{key: wrong}); self.assertRaises(ValueError, r.bind, current, changed)

    def test_bank_two_patch_preserves_general_bank_and_other_core_bytes(self):
        state, palette = background.state_fixture(); core = r.p.unpack_state(state)
        vram = r.p.gpu_records(core)['&GPURAM[0][0]']; modified = bytearray(vram)
        modified[r.PALETTE_OFFSET:r.PALETTE_OFFSET + 32] = palette
        core = core.replace(vram, bytes(modified)); state = precision.container(core)
        probe, intervention = r.bg.replace_palette_state(state, palette, r.PALETTE_OFFSET)
        actual = r.p.gpu_records(r.p.unpack_state(probe))['&GPURAM[0][0]']
        expected = bytes(modified[:r.PALETTE_OFFSET]) + bytes(32) + bytes(modified[r.PALETTE_OFFSET + 32:])
        self.assertEqual(expected, actual); self.assertEqual(r.PALETTE_OFFSET, intervention['vramByteOffset'])
        self.assertEqual(palette, actual[r.bg.PALETTE_OFFSET:r.bg.PALETTE_OFFSET + 32])
        self.assertTrue(r.bg.isolation(state, probe)['nonGpuSubsystemsUnchanged'])

    def test_invalid_palette_offsets_cannot_patch_other_state_records(self):
        state, palette = background.state_fixture()
        for at in (-2, 1, 1048576, True): self.assertRaises(ValueError, r.bg.replace_palette_state, state, palette, at)

    def test_opaque_stp_transparent_and_empty_are_separate_sample_sets(self):
        glyphs = [dict(x=0, y=0, u=0, v=0, width=3, height=1)]
        indices = bytes([0, 1, 2]); palette = struct.pack('<3H', 0, 1, 0x8000)
        groups = r.footprint(glyphs, indices, palette)
        self.assertEqual(dict(transparent={0}, opaque={1}, stp={2}), groups)
        self.assertEqual(dict(transparent=set(), opaque=set(), stp=set()), r.footprint(glyphs, indices, palette, False))

    def test_exact_metrics_reject_one_channel_difference_outside_footprint_and_isolation_failure(self):
        pixels = bytes([8, 16, 24, 255]) * 256 * 240
        groups = dict(opaque={0}, stp={1}, transparent={2}); isolated = dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True)
        self.assertTrue(r.compare(pixels, pixels, pixels, groups, pixels, pixels, isolated)['passed'])
        wrong = bytearray(pixels); wrong[0] += 8
        self.assertIn('opaque', r.compare(pixels, pixels, wrong, groups, pixels, pixels, isolated)['failures'])
        wrong = bytearray(pixels); wrong[40] += 8
        self.assertIn('outsideProbeFootprint', r.compare(pixels, wrong, pixels, groups, pixels, wrong, isolated)['failures'])
        self.assertIn('probe-isolation', r.compare(pixels, pixels, pixels, groups, pixels, pixels, dict(isolated, gpuRegistersUnchanged=False))['failures'])

    def test_empty_scanout_does_not_claim_visible_artwork_comparison(self):
        pixels = bytes([0, 0, 0, 255]) * 256 * 240
        result = r.compare(pixels, pixels, pixels, dict(opaque=set(), stp=set(), transparent=set()), pixels, pixels,
                           dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertTrue(result['passed']); self.assertFalse(result['visibleArtworkCompared'])
        self.assertFalse(result['checks']['stp']['compared'])

    def test_alpha_padding_and_truncated_canvases_cannot_pass_rgb_only_metrics(self):
        pixels = bytes([8, 16, 24, 255]) * 256 * 240
        alpha = bytearray(pixels); alpha[3] = 254
        padded = bytearray(pixels); padded[0] = 9
        groups = dict(opaque=set(), stp=set(), transparent=set())
        for wrong in (alpha, padded, pixels[:-4]):
            self.assertRaises(ValueError, r.compare, pixels, pixels, wrong, groups, pixels, pixels,
                              dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))


if __name__ == '__main__': unittest.main()
