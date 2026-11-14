"""Authored buffer/stack histories, scanout pixels and crop receipts; no original media/code."""
import copy
import json
from pathlib import Path
import struct
import tempfile
import unittest

import analyze_banner_precision as p
import compare_banner_hide as h
import compare_visual_checkpoints as v
import test_banner_background as background
import test_banner_continuation as continuation
import test_banner_precision as precision
import test_native_banner as native
import test_observed_banner as authored


def fixture(polarity=0):
    ram = bytearray(2 * 1024 * 1024); front = polarity * 240; sp = 0x801fff00
    write = lambda at, value: struct.pack_into('<I', ram, at & 0x1fffffff, value)
    for at, value in ((h.POLARITY, polarity), (h.FRAME_COUNTER, 101), (0x80045980, 2),
                      (sp + 0x18, 0x8001dc64), (sp + 0x38, 0x80093b68), (sp + 0x58, 0x8007a058)):
        write(at, value)
    struct.pack_into('<6h', ram, (h.DRAW_ENVS & 0x1fffffff) + polarity * 92, -128, front, 384, 240, -128, front)
    struct.pack_into('<4h', ram, (h.DISPLAY_ENVS & 0x1fffffff) + polarity * 20, 0, 240 - front, 256, 240)
    registers = [0] * 32; registers[29] = sp; registers[31] = 0x8001dc64
    vram = bytearray(1048576); struct.pack_into('<H', vram, front * 1024 * 2, 0x8000 | 1 | 2 << 5 | 3 << 10)
    fields = [('dtd', b'\0'), ('DisplayMode', bytes(4)), ('DisplayFB_XStart', bytes(4)), ('DisplayFB_YStart', struct.pack('<I', front)),
        ('&GPURAM[0][0]', bytes(vram)), ('OffsX', struct.pack('<i', -128)), ('OffsY', struct.pack('<I', 240 - front)),
        ('DisplayOff', b'\0'), ('InCmd', b'\0'), ('BlitterFIFO.in_count', bytes(4))]
    cpu = [('GPR', struct.pack('<32I', *registers)), ('BACKED_PC', struct.pack('<I', 0x8001dd20))]
    def build(gpu_fields=fields, cpu_fields=cpu):
        core = bytearray(precision.core(gpu_fields))
        for tag, values in ((b'MAIN', [('MainRAM.data8', bytes(ram))]), (b'CPU', cpu_fields)):
            body = b''.join(precision.field(name, data) for name, data in values)
            core.extend(tag.ljust(32, b'\0') + struct.pack('<I', len(body)) + body)
        struct.pack_into('<I', core, 20, len(core))
        return bytes(core)
    return ram, write, fields, cpu, build


def geometry(crop=0):
    return [dict(PieceIndex=i, CropTop=crop, VisibleRows=31 - crop) for i in range(6)]


class BannerHideTests(unittest.TestCase):
    def test_pre_submission_context_binds_cpu_stack_and_opposite_buffers(self):
        for polarity in (0, 1):
            ram, _, _, _, build = fixture(polarity)
            result = h.context(build(), ram, dict(logicalTick=83, draws=geometry(16)))
            self.assertEqual([0, 240 * polarity], result['frontOrigin'])
            self.assertEqual([0, 240 * (1 - polarity)], result['backOrigin'])
            self.assertEqual(101, result['frameCounter']); self.assertEqual(83, result['task']['logicalTick'])

    def test_wrong_stack_chain_pacing_or_pending_environment_rejects(self):
        for at, value in ((0x801fff18, 0), (0x801fff38, 0x80093b64), (0x801fff58, 0),
                          (0x80045980, 1), (h.POLARITY, 2), (h.DRAW_ENVS + 8, 0)):
            ram, write, _, _, build = fixture(); write(at, value)
            self.assertRaises(ValueError, h.context, build(), ram, dict(logicalTick=83))

    def test_cpu_outside_wait_and_gpu_busy_or_wrong_front_cannot_infer_lag(self):
        ram, _, fields, cpu, build = fixture()
        for name, wrong in (('DisplayFB_YStart', struct.pack('<I', 240)), ('dtd', b'\1'),
                            ('InCmd', b'\1'), ('BlitterFIFO.in_count', struct.pack('<I', 1))):
            changed = [(key, wrong if key == name else data) for key, data in fields]
            self.assertRaises(ValueError, h.context, build(changed), ram, dict(logicalTick=83))
        for pc in (0x8001dba8, 0x8001dd8c):
            changed = [(key, struct.pack('<I', pc) if key == 'BACKED_PC' else data) for key, data in cpu]
            self.assertRaises(ValueError, h.context, build(cpu_fields=changed), ram, dict(logicalTick=83))

    def test_frame_binding_requires_exact_counter_history_and_target_surface(self):
        ram, _, _, _, build = fixture(); current = h.context(build(), ram, dict(logicalTick=83, draws=geometry(16)))
        queued = copy.deepcopy(current); queued['frameCounter'] -= 2; queued['task'] = dict(logicalTick=81, draws=geometry())
        result = h.bind_display(current, queued)
        self.assertEqual(81, result['displayedLogicalTick']); self.assertEqual(geometry(), result['displayedGeometry'])
        for key, value in (('frameCounter', 100), ('polarity', 1), ('pendingDrawOrigin', [0, 240])):
            wrong = copy.deepcopy(queued); wrong[key] = value
            self.assertRaises(ValueError, h.bind_display, current, wrong)
        wrong = copy.deepcopy(queued); wrong['task']['logicalTick'] = 82
        self.assertRaises(ValueError, h.bind_display, current, wrong)

    def test_scanout_uses_selected_surface_and_zero_padding_ignoring_stp(self):
        for polarity in (0, 1):
            _, _, _, _, build = fixture(polarity)
            result = h.scanout(precision.container(build()))
            self.assertEqual(bytes([8, 16, 24, 255]), result[:4]); self.assertEqual(256 * 240 * 4, len(result))
        _, _, fields, _, build = fixture()
        changed = [(key, struct.pack('<I', 1) if key == 'DisplayMode' else data) for key, data in fields]
        self.assertRaises(ValueError, h.scanout, precision.container(build(changed)))

    def test_missing_original_function_fingerprints_prevent_snapshot_classification(self):
        _, _, _, _, build = fixture()
        self.assertRaises(ValueError, h.snapshot, precision.container(build()), authored.artwork()[0])

    def test_explicit_cropped_dma_trace_binds_colors_uv_geometry_and_mode(self):
        definition, ram, *_ = continuation.fixture(0x801c9bc4, 2, 8)
        state = native.state_with_ram(ram); trace = h.trace.trace(state, definition, geometry(8))
        self.assertEqual(list(range(6)), [x['pieceIndex'] for x in trace['banners']])
        self.assertRaises(ValueError, h.trace.trace, state, definition)  # Full-visible contract stays strict.
        wrong = geometry(8); wrong[0]['VisibleRows'] = 31
        self.assertRaises(ValueError, h.trace.trace, state, definition, wrong)

    def test_crop_projection_retains_bottom_uv_and_omits_degenerate_model_quads(self):
        definition, *_ = continuation.fixture()
        for crop in (0, 8, 16, 24):
            result = h.cropped_definition(definition, geometry(crop))
            self.assertEqual(31 - crop, result['pieces'][0]['height'])
            self.assertEqual(definition['pieces'][0]['v'] + crop, result['pieces'][0]['v'])
            self.assertEqual(definition['pieces'][0]['y'] + crop, result['pieces'][0]['y'])
        self.assertEqual([], h.cropped_definition(definition, geometry(31))['pieces'])
        wrong = geometry(); wrong[1]['PieceIndex'] = 0
        self.assertRaises(ValueError, h.cropped_definition, definition, wrong)

    def test_hidden_frame_has_no_vacuous_artwork_pixel_success(self):
        definition, *_ = continuation.fixture(); pixels = background.canvas()
        result = h.compare(pixels, pixels, pixels, pixels, pixels, definition, geometry(31), bytes(51200), bytes(32),
                           dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertTrue(result['passed']); self.assertFalse(result['visibleArtworkCompared'])
        self.assertIsNone(result['checks']['touched']['exactEquality']); self.assertFalse(result['checks']['touched']['compared'])
        self.assertEqual(0, result['checks']['touched']['pixels']); self.assertTrue(result['checks']['fullCanvas']['exactEquality'])

    def test_separate_scanout_and_state_faults_block_hidden_frame(self):
        definition, *_ = continuation.fixture(); pixels = background.canvas(); bad = bytearray(pixels); bad[0] ^= 8
        result = h.compare(pixels, pixels, pixels, bad, bad, definition, geometry(31), bytes(51200), bytes(32),
                           dict(nonGpuSubsystemsUnchanged=False, gpuRegistersUnchanged=True))
        self.assertEqual(['state-isolation', 'original-scanout-mismatch', 'probe-scanout-mismatch'], result['failures'])

    def test_native_sequence_receipt_rejects_wrong_projection_phase_state_or_hash(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='hide-receipt-', dir=base) as directory:
            root = Path(directory); pixels = background.canvas(); backgrounds = {t: pixels for t in range(81, 86)}
            geometries = {t: geometry(min(31, (t - 81) * 8)) for t in range(81, 86)}
            receipt = dict(format='fft.banner-hide-cpu-check', schemaVersion=1, renderer='FiveBitGeneralBannerPresentation.ComposeOver',
                sourceKind='verified-retail', referenceRevision=h.b.REVISION, precision='five-bit', ditherEnabled=False, framebufferOrigin=[0, 0],
                gpuReadbackPerformed=False, originalComparison='pending', checkpoints=[dict(logicalTick=t, phase='Hiding', revealCompleted=20,
                    holdCompleted=60, hideCompleted=t - 80, file=f'tick-{t:03}.rgba', backgroundSha256=v.sha(pixels), rgbaSha256=v.sha(pixels), pieces=geometries[t]) for t in range(81, 86)])
            for tick in range(81, 86): (root / f'tick-{tick:03}.rgba').write_bytes(pixels)
            def save(value): (root / 'check.json').write_text(json.dumps(value))
            save(receipt); self.assertEqual(5, len(h.native_frames(root, backgrounds, geometries)[1]))
            for key, value in (('phase', 'Holding'), ('logicalTick', 83), ('backgroundSha256', '0' * 64), ('rgbaSha256', '0' * 64), ('pieces', []), ('file', '../wrong')):
                wrong = copy.deepcopy(receipt); wrong['checkpoints'][0][key] = value; save(wrong)
                self.assertRaises(ValueError, h.native_frames, root, backgrounds, geometries)
            wrong = dict(receipt, ditherEnabled=0); save(wrong)
            self.assertRaises(ValueError, h.native_frames, root, backgrounds, geometries)

    def test_archived_input_and_existing_output_guards(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='hide-archive-', dir=base) as directory:
            root = Path(directory); file = root / 'input'; file.write_bytes(b'authored')
            record = h.c.identity(file); self.assertEqual(b'authored', h.verified(record))
            self.assertRaises(ValueError, h.verified, dict(record, sha256='0' * 64))
            self.assertRaises(ValueError, h.verified, dict(record, bytes=record['bytes'] + 1))
            self.assertRaises(ValueError, h.run, root / 'missing-plan', root / 'missing-profile', root)
            self.assertEqual(b'authored', file.read_bytes())


if __name__ == '__main__': unittest.main()
