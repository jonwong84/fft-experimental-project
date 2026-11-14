"""Authored early-reveal geometry/readiness contexts; no original media or code."""
import copy
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

import compare_banner_reveal as r
import test_banner_continuation as continuation
import test_banner_hide as hide
import test_native_banner as native
import test_banner_background as background
import test_banner_precision as precision


def fixture(counter):
    definition, ram, write, slot, sp = continuation.fixture(counter=counter)
    geometry = []
    count = 0 if counter == 0 else 3 if counter < 10 else 6
    for i, piece in enumerate(definition['pieces']):
        amount = max(0, min(31, (counter - (1 if i < 3 else 10)) * 8))
        crop = 31 - amount
        if i < count: geometry.append(dict(PieceIndex=i, CropTop=crop, VisibleRows=amount))
        for vertex, (dx, dy) in enumerate(((0, 0), (2, 0), (0, amount), (2, amount))):
            at = r.trace.BANNER_BASE + i * 52 + vertex * 12
            write(at + 8, piece['x'] + 128 + dx | (piece['y'] + crop + dy) << 16)
            uv = piece['u'] + dx | ((piece['v'] + crop + dy) & 255) << 8
            if vertex == 1: uv |= (0x26 if piece['material'] == 'add' else 0x46) << 16
            write(at + 12, uv)
    if count == 0:
        write(0x80056b18 + 0x17f * 4, 0xffffff); write(slot + 0x2c, 0xffffffff)
    else: write(r.trace.BANNER_BASE + (count - 1) * 52, 0x0cffffff)
    return definition, ram, write, slot, sp, geometry


class BannerRevealTests(unittest.TestCase):
    def test_source_triplets_and_all_counter_crops(self):
        for counter in range(20):
            definition, ram, *_, geometry = fixture(counter)
            result = r.c.inspect_ram(ram, definition, early_reveal=True)
            self.assertEqual(counter, result['logicalTick']); self.assertEqual(geometry, result['draws'])

    def test_zero_counter_has_no_draws_and_does_not_invent_initial_parity(self):
        definition, ram, _, slot, _, _ = fixture(0)
        self.assertEqual(0xffffffff, r.trace.word(ram, slot + 0x2c))
        self.assertEqual([], r.c.inspect_ram(ram, definition, early_reveal=True)['draws'])

    def test_legacy_six_piece_contract_remains_strict(self):
        definition, ram, *_ = fixture(2)
        self.assertRaises(ValueError, r.c.inspect_ram, ram, definition)
        self.assertRaises(ValueError, r.trace.trace, native.state_with_ram(ram), definition)

    def test_explicit_three_piece_trace_binds_draw_mode_colors_uv_and_order(self):
        definition, ram, write, _, _, geometry = fixture(2)
        state = native.state_with_ram(ram)
        self.assertEqual([0, 1, 2], [x['pieceIndex'] for x in r.trace.trace(state, definition, geometry)['banners']])
        write(r.trace.BANNER_BASE + 12, r.trace.word(ram, r.trace.BANNER_BASE + 12) ^ 1)
        self.assertRaises(ValueError, r.trace.trace, native.state_with_ram(ram), definition, geometry)

    def test_counter_must_explain_crops_not_just_plausible_vertices(self):
        definition, ram, write, _, sp, _ = fixture(2); write(sp + 0x18, 3)
        self.assertRaises(ValueError, r.c.inspect_ram, ram, definition, early_reveal=True)

    def test_missing_extra_or_wrong_bank_submissions_reject(self):
        for counter, mutate in ((2, 'extra'), (10, 'missing'), (2, 'bank')):
            definition, ram, write, slot, *_ = fixture(counter)
            if mutate == 'extra': write(r.trace.BANNER_BASE + 2 * 52, 12 << 24 | (r.trace.BANNER_BASE + 3 * 52) & 0xffffff)
            elif mutate == 'missing': write(r.trace.BANNER_BASE + 2 * 52, 0x0cffffff)
            else: write(slot + 0x2c, 1)
            self.assertRaises(ValueError, r.c.inspect_ram, ram, definition, early_reveal=True)

    def test_both_source_vsync_wait_sites_require_their_own_stack_chain(self):
        ram, write, _, cpu, build = hide.fixture()
        registers = list(struct.unpack('<32I', cpu[0][1])); registers[31] = 0x8001dc40
        first_cpu = [('GPR', struct.pack('<32I', *registers)), cpu[1]]
        write(0x801fff18, 0x8001dc40)
        self.assertRaises(ValueError, r.hide.context, build(cpu_fields=first_cpu), ram, {})
        self.assertEqual(0x8001dc40, r.hide.context(build(cpu_fields=first_cpu), ram, {}, first_vsync_wait=True)['returnAddresses'][0])
        write(0x801fff18, 0x8001dc64)
        self.assertRaises(ValueError, r.hide.context, build(cpu_fields=first_cpu), ram, {}, first_vsync_wait=True)

    def loader_fixture(self, pc):
        _, ram, write, slot, _ = continuation.fixture(); sp = slot + 0x340
        for at, value in ((slot + 0x44, pc), (slot + 0x3c, sp), (sp + 0x1c, 0x801c8fbc),
            (sp + 0x14, sp + 32), (sp + 0x18, 60), (slot + 0x10, 0x801df000), (0x80010010, 0x801df000)):
            write(at, value)
        return ram, write, slot, sp

    def classify_loader(self, ram):
        # Authored bytes only: substitute a hash of an authored zero range.
        with patch.object(r.c, 'read_ram', return_value=ram), patch.object(r, 'LOADER_CODE', (0, 16, r.v.sha(ram[:16]))):
            return r.loader_snapshot(b'authored')

    def test_all_four_loader_yields_remain_outside_logical_ticks(self):
        for pc, phase in r.WAITS.items():
            ram, *_ = self.loader_fixture(pc); result = self.classify_loader(ram)
            self.assertEqual(phase, result['phase']); self.assertIsNone(result['logicalTick'])
            self.assertEqual(None if phase == 'AllocationYield' else 0x801df000, result['allocation'])

    def test_loader_rejects_corrupt_task_stack_allocation_and_caller(self):
        for field in ('condition', 'hold', 'running', 'stack', 'caller', 'parentStack', 'allocation'):
            ram, write, slot, sp = self.loader_fixture(0x801c3b40)
            address = dict(condition=slot, hold=slot + 4, running=slot + 0x48, stack=slot + 0x3c,
                caller=sp + 0x1c, parentStack=sp + 0x14, allocation=slot + 0x10)[field]
            write(address, 0); self.assertRaises(ValueError, self.classify_loader, ram)

    def test_unknown_loader_site_and_missing_fingerprints_reject(self):
        ram, *_ = self.loader_fixture(0x801c3b28)
        self.assertRaises(ValueError, self.classify_loader, ram)
        self.assertRaises(ValueError, r.loader_snapshot, native.state_with_ram(ram))

    def test_projection_padding_does_not_create_artwork_success(self):
        definition, _, _, _, _, geometry = fixture(1); padded = r.padded_geometry(geometry)
        self.assertEqual(6, len(padded)); self.assertEqual([0] * 6, [x['VisibleRows'] for x in padded])
        pixels = background.canvas()
        result = r.hide.compare(pixels, pixels, pixels, pixels, pixels, definition, padded, bytes(51200), bytes(32),
            dict(nonGpuSubsystemsUnchanged=True, gpuRegistersUnchanged=True))
        self.assertFalse(result['visibleArtworkCompared']); self.assertIsNone(result['checks']['touched']['exactEquality'])
        self.assertRaises(ValueError, r.padded_geometry, geometry[:2])

    def test_partial_model_is_explicit_and_requires_first_ordered_triplet(self):
        definition, *_ = fixture(2)
        partial = dict(definition, pieces=definition['pieces'][:3])
        indices, palette = bytes(51200), bytes(32)
        self.assertRaises(ValueError, r.b.transfer_bounds, partial, indices, palette)
        self.assertEqual(set(), r.b.transfer_bounds(partial, indices, palette, partial_triplet=True)[2])
        self.assertEqual(background.canvas(), r.bg.compose(background.canvas(), partial, indices, palette, partial_triplet=True))
        bad = copy.deepcopy(partial); bad['pieces'][1]['sourceIndex'] = 0
        self.assertRaises(ValueError, r.b.transfer_bounds, bad, indices, palette, partial_triplet=True)

    def test_native_receipt_binds_actual_projection_background_and_phase(self):
        base = r.v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='reveal-receipt-', dir=base) as directory:
            root = Path(directory); pixels = background.canvas()
            backgrounds = {t: pixels for t in r.TICKS}; geometries = {t: fixture(t)[-1] for t in r.TICKS}
            receipt = dict(format='fft.banner-reveal-cpu-check', schemaVersion=1, renderer='FiveBitGeneralBannerPresentation.ComposeOver',
                sourceKind='verified-retail', referenceRevision=r.b.REVISION, precision='five-bit', ditherEnabled=False, framebufferOrigin=[0, 0],
                gpuReadbackPerformed=False, originalComparison='pending', checkpoints=[dict(logicalTick=t, phase='Revealing', revealCompleted=t,
                    holdCompleted=0, hideCompleted=0, file=f'tick-{t:03}.rgba', backgroundSha256=r.v.sha(pixels), rgbaSha256=r.v.sha(pixels), pieces=geometries[t]) for t in r.TICKS])
            for tick in r.TICKS: (root / f'tick-{tick:03}.rgba').write_bytes(pixels)
            path = root / 'check.json'; path.write_text(json.dumps(receipt))
            self.assertEqual(len(r.TICKS), len(r.native_frames(root, backgrounds, geometries)[1]))
            for field, value in (('logicalTick', 2), ('phase', 'Holding'), ('pieces', []), ('backgroundSha256', '0' * 64), ('rgbaSha256', '0' * 64), ('file', '../wrong')):
                bad = copy.deepcopy(receipt); bad['checkpoints'][0][field] = value; path.write_text(json.dumps(bad))
                self.assertRaises(ValueError, r.native_frames, root, backgrounds, geometries)

    def test_existing_output_is_preserved_before_missing_evidence_access(self):
        base = r.v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='reveal-existing-', dir=base) as directory:
            root = Path(directory); (root / 'sentinel').write_bytes(b'keep')
            self.assertRaises(ValueError, r.run, root / 'missing', root / 'missing-profile', root)
            self.assertEqual(b'keep', (root / 'sentinel').read_bytes())


if __name__ == '__main__': unittest.main()
