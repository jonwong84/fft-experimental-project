"""Authored DMA chains and native receipt/pixel checks; no emulator or game assets."""
import copy
import json
import struct
import unittest

import analyze_banner_precision as p
import compare_native_banner as native
import compare_observed_banner as b
import compare_visual_checkpoints as v
import test_banner_precision as precision
import test_observed_banner as authored
import trace_banner_draw_state as trace


def frozen_fixture():
    definition, _, _ = authored.artwork()
    ram = bytearray(trace.RAM_SIZE)
    def write(address, value): struct.pack_into('<I', ram, address & 0x1fffffff, value)
    ot = 0x80056b18
    write(trace.CURRENT_BUFFER, trace.BUFFER_BASE); write(trace.BUFFER_BASE + 0xe5b4, ot); write(trace.SCRIPT_OT, ot)
    write(ot + 0x17f * 4, (trace.BURST_MODE_BASE + 12) & 0xffffff)
    mode = trace.BURST_MODE_BASE + 12
    write(mode, 2 << 24 | trace.BANNER_BASE & 0xffffff); write(mode + 4, 0xe1000006); write(mode + 8, 0xe2000000)
    for i, piece in enumerate(definition['pieces']):
        address = trace.BANNER_BASE + i * 52
        write(address, 12 << 24 | ((address + 52) & 0xffffff if i < 5 else 0xffffff))
        for vertex, (dx, dy) in enumerate(((0, 0), (2, 0), (0, 2), (2, 2))):
            color = piece['colors'][vertex]
            write(address + 4 + vertex * 12, color['r'] | color['g'] << 8 | color['b'] << 16 | (0x3e << 24 if vertex == 0 else 0))
            write(address + 8 + vertex * 12, piece['x'] + 128 + dx | (piece['y'] + dy) << 16)
            uv = piece['u'] + dx | (piece['v'] + dy) << 8
            if vertex == 1: uv |= (0x26 if piece['material'] == 'add' else 0x46) << 16
            write(address + 12 + vertex * 12, uv)
    return definition, ram


def state_with_ram(ram):
    gpu_core = precision.core(); gpu_section = gpu_core[32:]
    body = precision.field('MainRAM.data8', bytes(ram))
    main = b'MAIN'.ljust(32, b'\0') + struct.pack('<I', len(body)) + body
    header = bytearray(gpu_core[:32]); struct.pack_into('<I', header, 20, 32 + len(main) + len(gpu_section))
    return precision.container(bytes(header) + main + gpu_section)


class BannerDmaTraceTests(unittest.TestCase):
    def test_frozen_chain_binds_order_state_and_geometry(self):
        definition, ram = frozen_fixture(); report = trace.trace(state_with_ram(ram), definition)
        self.assertEqual(list(range(6)), [x['pieceIndex'] for x in report['banners']])
        self.assertTrue(all(not x['precedingMode']['ditherEnabled'] for x in report['banners']))
        self.assertEqual('g_conditions_burst_draw_modes[1]', report['writerSymbol'])
        self.assertFalse(report['universalBannerDitherDefaultEstablished'])
        self.assertFalse(report['originalEvent401Verified'])

    def test_dma_rejects_cycles_nonram_aliases_and_truncated_packet(self):
        _, ram = frozen_fixture(); struct.pack_into('<I', ram, 0, 0)
        self.assertRaises(ValueError, trace.walk, ram, 0)
        self.assertRaises(ValueError, trace.word, ram, 0x70000000)
        self.assertRaises(ValueError, trace.word, ram, 1)
        struct.pack_into('<I', ram, len(ram) - 4, 0x01ffffff)
        self.assertRaises(ValueError, trace.walk, ram, len(ram) - 4)
        self.assertEqual(trace.word(ram, trace.CURRENT_BUFFER), trace.word(ram, trace.CURRENT_BUFFER + 0x20000000))

    def test_missing_mode_and_changed_dither_reject(self):
        for key in ('missing', 'on'):
            definition, ram = frozen_fixture()
            if key == 'missing': struct.pack_into('<I', ram, (0x80056b18 + 0x17f * 4) & 0x1fffffff, trace.BANNER_BASE & 0xffffff)
            else: struct.pack_into('<I', ram, ((trace.BURST_MODE_BASE + 12) & 0x1fffffff) + 4, 0xe1000206)
            with self.subTest(key=key): self.assertRaises(ValueError, trace.trace, state_with_ram(ram), definition)

    def test_unknown_packet_cannot_hide_a_dither_write_before_banner(self):
        definition, ram = frozen_fixture(); mode = (trace.BURST_MODE_BASE + 12) & 0xffffff
        struct.pack_into('<I', ram, mode, 2 << 24 | 0x1000)
        struct.pack_into('<II', ram, 0x1000, 1 << 24 | trace.BANNER_BASE & 0xffffff, 0xf0000000)
        self.assertRaises(ValueError, trace.trace, state_with_ram(ram), definition)

    def test_mutated_vertex_uv_and_blend_bindings_reject(self):
        for offset in (4, 8, 12, 24):
            definition, ram = frozen_fixture(); at = (trace.BANNER_BASE & 0xffffff) + offset
            struct.pack_into('<I', ram, at, struct.unpack_from('<I', ram, at)[0] ^ 1)
            with self.subTest(offset=offset): self.assertRaises(ValueError, trace.trace, state_with_ram(ram), definition)


class NativeCaptureTests(unittest.TestCase):
    def setUp(self):
        self.fixture = authored.ObservedBannerContractTests('test_existing_output_is_preserved_before_reading_plan')
        self.fixture.setUp(); self.addCleanup(self.fixture.doCleanups)
        d, ix, pal = authored.artwork(); self.definition, self.indices, self.palette = d, ix, pal
        self.rgba, _ = p.native_bounds(d, ix, pal)
        schedule = [0, 1, 2, 5, 9, 10, 11, 14, 20, 80, 81, 82, 85, 90, 91]
        self.receipt = dict(format='fft.general-banner-native-capture', schemaVersion=1, precision='five-bit', policy='authored fixture',
            ditherEnabled=False, framebufferOrigin=[0, 0], referenceRevision=b.REVISION, completionTick=91, phase='Completed',
            originalComparison='pending', rendererTaskAcknowledgments=0,
            captures=[dict(tick=t, phase='Holding', file=f'tick-{t:03d}.png', rgbaSha256=v.sha(self.rgba),
                pieces=[dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)],
                drawStates=[dict(pieceIndex=i, ditherEnabled=False, framebufferX=0, framebufferY=0) for i in range(6)]) for t in schedule])
        self.fixture.write('tick-020.png', self.fixture.png(bytes(self.rgba), (256, 240)))
        self.fixture.write('check.json', self.receipt)

    def write_receipt(self): self.fixture.write('check.json', self.receipt)

    def test_explicit_native_receipt_binds_pixels_and_state(self):
        rgba, receipt, _ = native.capture('scratch')
        self.assertEqual(self.rgba, rgba); self.assertFalse(receipt['ditherEnabled'])

    def test_native_samples_compare_exactly_without_fidelity_claim(self):
        report = native.compare(self.rgba, self.rgba, self.definition, self.indices, self.palette, False)
        self.assertTrue(report['passed']); self.assertEqual(8, report['foreground']['pixels'])
        self.assertFalse(report['originalFidelityEstablished']); self.assertFalse(report['timingComparisonPerformed'])

    def test_actual_pixel_fault_reports_color_and_model_failures(self):
        changed = bytearray(self.rgba); changed[(20 * 256 + 2) * 4] -= 8
        report = native.compare(self.rgba, changed, self.definition, self.indices, self.palette, False)
        self.assertFalse(report['passed']); self.assertEqual(1, report['foreground']['changedPixels'])
        self.assertEqual(1, report['nativeModelAgreement']['changedPixels'])

    def test_missing_or_implicit_draw_state_rejects(self):
        for value in ([], [dict(pieceIndex=i, ditherEnabled=0, framebufferX=0, framebufferY=0) for i in range(6)]):
            self.receipt['captures'][8]['drawStates'] = value; self.write_receipt()
            self.assertRaises(ValueError, native.capture, 'scratch')

    def test_native_and_legacy_receipts_cannot_be_relabeled(self):
        self.receipt['format'] = 'managed-capture'; self.write_receipt()
        self.assertRaises(ValueError, native.capture, 'scratch')

    def test_wrong_pixel_digest_and_partial_anchor_reject(self):
        before = copy.deepcopy(self.receipt)
        self.receipt['captures'][8]['rgbaSha256'] = '0' * 64; self.write_receipt()
        self.assertRaises(ValueError, native.capture, 'scratch')
        self.receipt = before; self.receipt['captures'][8]['pieces'][0]['VisibleRows'] = 30; self.write_receipt()
        self.assertRaises(ValueError, native.capture, 'scratch')

    def test_duplicate_tick_unknown_field_and_incomplete_host_reject(self):
        before = copy.deepcopy(self.receipt)
        for kind in ('duplicate', 'unknown', 'incomplete'):
            self.receipt = copy.deepcopy(before)
            if kind == 'duplicate': self.receipt['captures'][0]['tick'] = 20
            elif kind == 'unknown': self.receipt['overrideTolerance'] = 20
            else: self.receipt['phase'] = 'Holding'
            self.write_receipt()
            with self.subTest(kind=kind): self.assertRaises(ValueError, native.capture, 'scratch')

    def test_existing_report_is_preserved_before_reading_missing_evidence(self):
        output = self.fixture.root / 'scratch/report'; output.mkdir(); (output / 'sentinel').write_bytes(b'keep')
        self.assertRaises(ValueError, native.run, 'scratch/no-plan.json', 'scratch/missing', 'scratch/report')
        self.assertEqual(b'keep', (output / 'sentinel').read_bytes())

    def bind_end_to_end(self, dither=False):
        f = self.fixture
        definition, ram = frozen_fixture()
        f.write('state', state_with_ram(ram)); f.plan['original']['state'] = f.records['state']
        original = bytearray(bytes([0, 0, 0, 255]) * 280 * 240)
        for row in range(240): original[(row * 280 + 12) * 4:(row * 280 + 268) * 4] = self.rgba[row * 256 * 4:(row + 1) * 256 * 4]
        f.write('original.png', f.png(bytes(original), (280, 240))); f.plan['original']['png'] = f.records['original.png']
        observation = copy.deepcopy(f.observation)
        observation['state']['sha256'] = f.records['state']['sha256']; observation['restoreImages'][0]['sha256'] = f.records['original.png']['sha256']
        f.rewrite('observation', observation)
        # Restore the independent legacy receipt/PNG consumed by the historical plan.
        _, _, _, modern = authored.pixels()
        f.write('tick-020.png', f.png(modern, (256, 240))); f.plan['candidate']['png'] = f.records['tick-020.png']
        f.rewrite('receipt', f.receipt)
        (f.root / 'scratch/native').mkdir()
        rgba, _ = p.native_bounds(self.definition, self.indices, self.palette, dither=dither)
        self.receipt['ditherEnabled'] = dither
        for checkpoint in self.receipt['captures']:
            checkpoint['rgbaSha256'] = v.sha(rgba)
            for state in checkpoint['drawStates']: state['ditherEnabled'] = dither
        f.write('native/check.json', self.receipt); f.write('native/tick-020.png', f.png(bytes(rgba), (256, 240)))
        f.write('plan.json', f.plan)

    def test_full_authored_comparison_and_review_repeat_exactly(self):
        self.bind_end_to_end()
        f = self.fixture
        self.assertEqual(0, native.run('scratch/plan.json', 'scratch/native', 'scratch/report-a'))
        self.assertEqual(0, native.run('scratch/plan.json', 'scratch/native', 'scratch/report-b'))
        for name in ('report.json', 'review.html'):
            self.assertEqual((f.root / 'scratch/report-a' / name).read_bytes(), (f.root / 'scratch/report-b' / name).read_bytes())

    def test_draw_state_and_visual_failures_are_retained_separately(self):
        self.bind_end_to_end(dither=True)
        self.assertEqual(1, native.run('scratch/plan.json', 'scratch/native', 'scratch/report'))
        report = json.loads((self.fixture.root / 'scratch/report/report.json').read_bytes())
        self.assertIn('draw-state-mismatch', report['failures'])
        self.assertIn('foreground-color-mismatch', report['failures'])
        self.assertNotIn('native-model-mismatch', report['failures'])


if __name__ == '__main__': unittest.main()
