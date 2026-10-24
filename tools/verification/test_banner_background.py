"""Authored state perturbations, blend pixels and receipts; no original media/emulator."""
import copy
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zlib

import analyze_banner_precision as p
import compare_banner_background as background
import compare_visual_checkpoints as v
import test_banner_precision as precision
import test_observed_banner as authored


def state_fixture():
    palette = struct.pack('<16H', *range(16)); vram = bytearray(1048576); vram[:8] = b'authored'
    vram[background.PALETTE_OFFSET:background.PALETTE_OFFSET + 32] = palette
    core = bytearray(precision.core([('dtd', b'\0'), ('DisplayMode', bytes(4)), ('DisplayFB_XStart', bytes(4)),
                                    ('DisplayFB_YStart', bytes(4)), ('&GPURAM[0][0]', bytes(vram))]))
    for tag, name, data in ((b'MAIN', 'MainRAM.data8', bytes(2 * 1024 * 1024)), (b'CPU', 'authored-registers', b'cpu-state')):
        body = precision.field(name, data); core.extend(tag.ljust(32, b'\0') + struct.pack('<I', len(body)) + body)
    struct.pack_into('<I', core, 20, len(core))
    return precision.container(bytes(core)), palette


def artwork():
    definition, indices, _ = authored.artwork(); indices = bytearray(indices)
    indices[0:2] = indices[256:258] = bytes([1, 2])
    palette = struct.pack('<16H', 0, 0xffff, 0x8842, *([0] * 13))
    return definition, bytes(indices), palette


def canvas():
    return bytes([80, 160, 240, 255]) * 256 * 240


class BannerBackgroundTests(unittest.TestCase):
    def test_palette_fork_changes_only_the_selected_words_and_round_trips(self):
        state, palette = state_fixture(); probe, evidence = background.transparent_palette_state(state, palette)
        left, right = p.unpack_state(state), p.unpack_state(probe)
        self.assertEqual(32, evidence['byteCount']); self.assertEqual(15, evidence['changedCoreBytes'])
        gpu_left, gpu_right = p.gpu_records(left), p.gpu_records(right)
        for key in gpu_left:
            if key != '&GPURAM[0][0]': self.assertEqual(gpu_left[key], gpu_right[key])
        at = background.PALETTE_OFFSET
        expected = gpu_left['&GPURAM[0][0]'][:at] + bytes(32) + gpu_left['&GPURAM[0][0]'][at + 32:]
        self.assertEqual(expected, gpu_right['&GPURAM[0][0]'])
        for tag in (b'MAIN', b'CPU'): self.assertEqual(p.subsystem_records(left, tag), p.subsystem_records(right, tag))
        self.assertEqual(probe, background.transparent_palette_state(state, palette)[0])

    def test_wrong_empty_or_malformed_palette_and_state_reject(self):
        state, palette = state_fixture()
        for wrong in (palette[:-1], bytes(32), bytes([1]) * 32):
            self.assertRaises(ValueError, background.transparent_palette_state, state, wrong)
        self.assertRaises((ValueError, zlib.error), background.transparent_palette_state, state[:-1], palette)

    def test_ambiguous_vram_bytes_cannot_select_a_different_record(self):
        state, palette = state_fixture(); core = bytearray(p.unpack_state(state)); gpu = p.gpu_records(bytes(core))
        # Add a second identical blob in an authored CPU field, retaining valid framing.
        body = precision.field('duplicate', gpu['&GPURAM[0][0]'])
        core.extend(b'OTHER'.ljust(32, b'\0') + struct.pack('<I', len(body)) + body)
        struct.pack_into('<I', core, 20, len(core))
        self.assertRaises(ValueError, background.transparent_palette_state, precision.container(core), palette)

    def test_isolation_compares_every_non_gpu_section_and_gpu_register(self):
        state, palette = state_fixture(); probe, _ = background.transparent_palette_state(state, palette)
        result = background.isolation(state, probe)
        self.assertTrue(result['nonGpuSubsystemsUnchanged']); self.assertTrue(result['gpuRegistersUnchanged'])
        core = p.unpack_state(probe); changed = core.replace(b'cpu-state', b'cpu-fault')
        self.assertFalse(background.isolation(state, precision.container(changed))['nonGpuSubsystemsUnchanged'])
        core = bytearray(core); index = core.index(precision.field('dtd', b'\0')); core[index + len(precision.field('dtd', b'\0')) - 1] = 1
        self.assertFalse(background.isolation(state, precision.container(core))['gpuRegistersUnchanged'])

    def test_model_composes_five_bit_passes_over_the_supplied_pixels(self):
        d, ix, pal = artwork(); source = canvas(); result = background.compose(source, d, ix, pal)
        self.assertEqual(bytes([248, 248, 248, 255]), result[(20 * 256 + 2) * 4:(20 * 256 + 3) * 4])
        self.assertEqual(bytes([64, 144, 224, 255]), result[(20 * 256 + 3) * 4:(20 * 256 + 4) * 4])
        self.assertEqual(source[:4], result[:4]); self.assertEqual(canvas(), source)

    def test_background_cannot_be_resized_transparent_or_implicitly_quantized(self):
        d, ix, pal = artwork()
        for value in (canvas()[:-1], canvas() + b'\0'):
            self.assertRaises(ValueError, background.compose, value, d, ix, pal)
        for channel in range(4):
            value = bytearray(canvas()); value[channel] = 1
            self.assertRaises(ValueError, background.compose, value, d, ix, pal)

    def test_controlled_exact_match_separates_sensitive_samples_from_inherited_background(self):
        d, ix, pal = artwork(); bg = canvas(); expected = background.compose(bg, d, ix, pal)
        report = background.diagnostics(expected, bg, expected, d, ix, pal, dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertTrue(report['passed']); self.assertFalse(report['originalFidelityEstablished'])
        self.assertTrue(report['controlledProbe']); self.assertFalse(report['unalteredRetailBackgroundCapture'])
        self.assertEqual(8, report['checks']['touched']['pixels']); self.assertEqual(4, report['checks']['backgroundSensitive']['pixels'])
        self.assertEqual(4, report['checks']['foregroundInvariant']['pixels']); self.assertEqual(61432, report['checks']['probeOutsideBanner']['pixels'])

    def test_native_fault_reports_original_and_model_failures(self):
        d, ix, pal = artwork(); bg = canvas(); original = background.compose(bg, d, ix, pal); native = bytearray(original)
        native[(20 * 256 + 3) * 4] ^= 8
        report = background.diagnostics(original, bg, native, d, ix, pal, dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertEqual(['native-model-mismatch', 'original-composition-mismatch'], report['failures'])

    def test_state_or_outside_footprint_difference_blocks_the_probe(self):
        d, ix, pal = artwork(); bg = canvas(); expected = background.compose(bg, d, ix, pal)
        report = background.diagnostics(expected, bg, expected, d, ix, pal, dict(nonGpuSubsystemsUnchanged=False, gpuRegistersUnchanged=True))
        self.assertEqual(['state-isolation'], report['failures'])
        bad_original = bytearray(expected); bad_original[0] ^= 8
        report = background.diagnostics(bad_original, bg, expected, d, ix, pal, dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertIn('probe-footprint', report['failures']); self.assertIn('original-composition-mismatch', report['failures'])

    def test_native_receipt_requires_the_exact_source_background_anchor_and_pixels(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='blend-receipt-', dir=base) as directory:
            root = Path(directory); pixels = canvas(); (root / 'composed.rgba').write_bytes(pixels)
            receipt = dict(format='fft.banner-background-cpu-check', schemaVersion=1,
                renderer='FiveBitGeneralBannerPresentation.ComposeOver', precision='five-bit', backgroundPolicy='authored metadata fixture',
                sourceKind='verified-retail', referenceRevision=background.b.REVISION, backgroundSha256=v.sha(pixels), rgbaSha256=v.sha(pixels),
                logicalTick=29, phase='Holding', revealCompleted=20, holdCompleted=9, hideCompleted=0,
                ditherEnabled=False, framebufferOrigin=[0, 0], pieces=[dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)],
                gpuReadbackPerformed=False, originalComparison='pending')
            def save(value): (root / 'check.json').write_text(json.dumps(value))
            save(receipt); self.assertEqual(pixels, background.native_capture(root, pixels)[0])
            for key, value in (('format', 'legacy'), ('ditherEnabled', 0), ('logicalTick', 20), ('holdCompleted', 8),
                               ('backgroundSha256', '0' * 64), ('rgbaSha256', '0' * 64), ('unknown', True)):
                changed = copy.deepcopy(receipt); changed[key] = value; save(changed)
                self.assertRaises(ValueError, background.native_capture, root, pixels)
            for at, value in ((3, 0), (0, 81)):
                broken = bytearray(pixels); broken[at] = value
                (root / 'composed.rgba').write_bytes(broken); changed = dict(receipt, rgbaSha256=v.sha(broken)); save(changed)
                self.assertRaises(ValueError, background.native_capture, root, pixels)

    def test_existing_output_is_preserved_before_original_or_emulator_access(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='blend-existing-', dir=base) as directory:
            root = Path(directory); (root / 'sentinel').write_text('preserve')
            self.assertRaises(ValueError, background.run, root / 'missing', root / 'profile', root)
            self.assertEqual('preserve', (root / 'sentinel').read_text())

    def test_review_labels_the_controlled_probe_and_does_not_claim_map_fidelity(self):
        html = background.review(b'one', b'two', b'three')
        self.assertIn('intentionally altered palette', html); self.assertIn('do not prove map rendering', html)
        self.assertEqual(html, background.review(b'one', b'two', b'three'))


if __name__ == '__main__': unittest.main()
