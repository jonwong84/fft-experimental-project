"""Authored state containers and color vectors; no original media or emulator."""
import copy
import json
from pathlib import Path
import struct
import unittest
import zlib

from PIL import Image
import analyze_banner_precision as p
import compare_observed_banner as b
import compare_visual_checkpoints as v
import test_observed_banner as authored


def field(name, data):
    return bytes([len(name)]) + name.encode('ascii') + struct.pack('<I', len(data)) + data


def core(fields=None):
    if fields is None:
        fields = [('dtd', b'\0'), ('DisplayMode', bytes(4)), ('DisplayFB_XStart', bytes(4)),
                  ('DisplayFB_YStart', bytes(4)), ('&GPURAM[0][0]', bytes(1048576))]
    body = b''.join(field(name, data) for name, data in fields)
    section = b'GPU'.ljust(32, b'\0') + struct.pack('<I', len(body)) + body
    header = bytearray(32); header[:8] = b'MDFNSVST'; struct.pack_into('<I', header, 20, 32 + len(section))
    return bytes(header) + section


def container(payload):
    raw = b'RASTATE\x01MEM ' + struct.pack('<I', len(payload)) + payload + bytes((-len(payload)) % 8) + b'END ' + bytes(4)
    return pack(raw)


def pack(raw):
    result = b'#RZIPv\x01#' + struct.pack('<IQ', 131072, len(raw))
    for i in range(0, len(raw), 131072):
        chunk = zlib.compress(raw[i:i + 131072]); result += struct.pack('<I', len(chunk)) + chunk
    return result


class BannerPrecisionTests(unittest.TestCase):
    def test_neutral_modulation_and_capture_zero_padding(self):
        self.assertEqual(31, p.shade5(31, 128))
        self.assertEqual(248, p.shade5(31, 128) << 3)
        self.assertEqual(15, p.shade5(31, 64))
        self.assertEqual(31, p.shade5(31, 255))

    def test_dither_is_applied_before_five_bit_quantization_and_clamp(self):
        self.assertEqual(30, p.shade5(31, 128, -4))
        self.assertEqual(31, p.shade5(31, 128, 3))
        self.assertEqual(0, p.shade5(0, 0, -4))
        for args in ((32, 128), (31, 256), (31, 128, 4)):
            self.assertRaises(ValueError, p.shade5, *args)

    def test_fixed_gradient_bias_and_signed_slope_truncate(self):
        self.assertEqual(80, p.vertex_channel(64, 128, 1, 4))
        self.assertEqual(128, p.vertex_channel(128, 64, 0, 31))
        self.assertEqual(126, p.vertex_channel(128, 64, 1, 31))
        self.assertEqual(103, p.vertex_channel(88, 168, 6, 31))

    def test_color_bias_can_cross_a_quantization_boundary(self):
        self.assertEqual(2, p.vertex_channel(0, 10, 1, 4, rounded=False))
        self.assertEqual(3, p.vertex_channel(0, 10, 1, 4, rounded=True))

    def test_rzip_and_labeled_gpu_round_trip(self):
        payload = core(); self.assertEqual(payload, p.unpack_state(container(payload)))
        records = p.gpu_records(payload)
        self.assertEqual(b'\0', records['dtd']); self.assertEqual(1048576, len(records['&GPURAM[0][0]']))

    def test_truncated_and_oversized_state_declarations_reject(self):
        data = container(core())
        for wrong in (b'', data[:19], data[:-1], b'badmagic' + data[8:]):
            self.assertRaises((ValueError, zlib.error), p.unpack_state, wrong)
        wrong = bytearray(data); struct.pack_into('<Q', wrong, 12, p.MAX_STATE + 1)
        self.assertRaises(ValueError, p.unpack_state, wrong)

    def test_extra_compressed_bytes_and_wrong_decompressed_length_reject(self):
        data = bytearray(container(core()))
        struct.pack_into('<Q', data, 12, struct.unpack_from('<Q', data, 12)[0] + 1)
        self.assertRaises(ValueError, p.unpack_state, data)
        self.assertRaises(ValueError, p.unpack_state, container(core()) + b'extra')

    def test_duplicate_blocks_and_missing_terminator_reject(self):
        block = b'MEM ' + struct.pack('<I', 8) + bytes(8)
        self.assertRaises(ValueError, p.unpack_state, pack(b'RASTATE\x01' + block + block + b'END ' + bytes(4)))
        self.assertRaises(ValueError, p.unpack_state, pack(b'RASTATE\x01' + block))

    def test_bad_core_lengths_duplicate_gpu_fields_and_missing_fields_reject(self):
        wrong = bytearray(core()); struct.pack_into('<I', wrong, 20, len(wrong) + 1)
        self.assertRaises(ValueError, p.gpu_records, wrong)
        self.assertRaises(ValueError, p.gpu_records, core([('dtd', b'\0'), ('dtd', b'\1')]))
        self.assertRaises(ValueError, p.gpu_records, core([('dtd', b'\0')]))

    def test_native_endpoints_and_output_encoding_are_distinct(self):
        d, ix, pal = authored.artwork()
        low, high = p.native_bounds(d, ix, pal)
        at = (20 * 256 + 2) * 4
        self.assertEqual(bytes([248, 248, 248]), low[at:at + 3])
        self.assertEqual(low[at:at + 3], high[at:at + 3])
        replicated, _ = p.native_bounds(d, ix, pal, replicate=True)
        self.assertEqual(bytes([255, 255, 255]), replicated[at:at + 3])


class BannerPrecisionIntegrationTests(unittest.TestCase):
    def setUp(self):
        self.fixture = authored.ObservedBannerContractTests('test_existing_output_is_preserved_before_reading_plan')
        self.fixture.setUp(); self.addCleanup(self.fixture.doCleanups)
        f = self.fixture
        d, ix, pal, _ = authored.pixels()
        rgba, _ = p.native_bounds(d, ix, pal)
        _, _, _, self.mask = b.transfer_bounds(d, ix, pal)
        self.vram = bytearray(1048576)
        original = bytearray(bytes([0, 0, 0, 255]) * 280 * 240)
        for at in self.mask:
            pixel = rgba[at * 4:at * 4 + 4]
            dest = (at // 256 * 280 + at % 256 + 12) * 4; original[dest:dest + 4] = pixel
            word = sum((pixel[c] >> 3) << (5 * c) for c in range(3))
            struct.pack_into('<H', self.vram, (at // 256 * 1024 + at % 256) * 2, word)
        f.write('original.png', f.png(bytes(original), (280, 240))); f.plan['original']['png'] = f.records['original.png']
        fields = [('dtd', b'\0'), ('DisplayMode', bytes(4)), ('DisplayFB_XStart', bytes(4)),
                  ('DisplayFB_YStart', bytes(4)), ('&GPURAM[0][0]', bytes(self.vram))]
        self.payload_fields = fields
        f.write('state', container(core(fields))); f.plan['original']['state'] = f.records['state']
        observation = copy.deepcopy(f.observation)
        observation['state']['sha256'] = f.records['state']['sha256']
        observation['restoreImages'][0]['sha256'] = f.records['original.png']['sha256']
        f.rewrite('observation', observation)

    def test_state_selected_model_and_stored_vram_match_authored_capture(self):
        report = p.study(self.fixture.plan)
        self.assertEqual(0, report['frozenGpu']['dtd'])
        self.assertTrue(report['stateSelectedModelMatchesExactly'])
        self.assertTrue(report['frozenVramVersusRestoredScreenshot']['exactEquality'])
        self.assertFalse(report['originalFidelityEstablished'])
        self.assertFalse(report['runtimeRendererChanged'])
        self.assertEqual(8, report['historicalModernPreview']['changedPixels'])

    def test_study_reports_repeat_and_preserve_existing_output(self):
        f = self.fixture; f.write('plan.json', f.plan)
        self.assertEqual(0, p.run('scratch/plan.json', 'scratch/study-a'))
        self.assertEqual(0, p.run('scratch/plan.json', 'scratch/study-b'))
        self.assertEqual((f.root / 'scratch/study-a/report.json').read_bytes(), (f.root / 'scratch/study-b/report.json').read_bytes())
        self.assertRaises(ValueError, p.run, 'scratch/missing.json', 'scratch/study-a')

    def test_state_sha_mismatch_prevents_analysis_and_output(self):
        f = self.fixture; f.write('state', b'changed')
        self.assertRaises(ValueError, p.study, f.plan)

    def test_bound_receipt_cannot_bypass_managed_mask_model_guard(self):
        f = self.fixture
        with Image.open(f.root / 'scratch/tick-020.png') as im:
            pixels = bytearray(im.convert('RGBA').tobytes())
        pixels[(20 * 256 + 2) * 4] = 250
        f.write('tick-020.png', f.png(bytes(pixels), (256, 240)))
        f.plan['candidate']['png'] = f.records['tick-020.png']
        receipt = copy.deepcopy(f.receipt); receipt['captures'][0]['rgbaSha256'] = v.sha(pixels)
        f.rewrite('receipt', receipt)
        self.assertRaises(ValueError, p.study, f.plan)


if __name__ == '__main__': unittest.main()
