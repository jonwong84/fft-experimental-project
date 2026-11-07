"""Authored pixels and metadata only; no game, emulator, GPU or reference checkout."""
import copy
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image
import compare_observed_banner as b
import compare_visual_checkpoints as v
import prepare_observed_banner_comparison as prepare


def artwork(word=0xffff):
    indices = bytearray(51200)
    indices[0:2] = bytes([1, 1]); indices[256:258] = bytes([1, 1])
    color = dict(r=128, g=128, b=128)
    pieces = [dict(sourceIndex=i, x=2, y=20 if i < 3 else 56, u=0, v=0, width=2, height=2,
                   material='add' if i % 3 == 2 else 'subtract', colors=[color.copy() for _ in range(4)]) for i in range(6)]
    return dict(pieces=pieces, source=dict(referenceRevision=b.REVISION)), bytes(indices), struct.pack('<16H', 0, word, *([0] * 14))


def pixels():
    definition, indices, palette = artwork()
    low, _, _, invariant = b.transfer_bounds(definition, indices, palette)
    rgba = bytearray(bytes([0, 0, 0, 255]) * 256 * 240)
    for at in invariant: rgba[at * 4:at * 4 + 3] = low[at * 3:at * 3 + 3]
    return definition, indices, palette, bytes(rgba)


class ObservedBannerTests(unittest.TestCase):
    def test_saturating_subtract_then_add_resets_background(self):
        definition, indices, palette = artwork()
        low, high, touched, invariant = b.transfer_bounds(definition, indices, palette)
        self.assertEqual(8, len(touched)); self.assertEqual(touched, invariant)
        at = (20 * 256 + 2) * 3
        self.assertEqual(bytes([255] * 3), low[at:at + 3]); self.assertEqual(low[at:at + 3], high[at:at + 3])

    def test_zero_word_discards_and_stp_black_preserves_background(self):
        for word, count in ((0, 0), (0x8000, 8)):
            d, ix, pal = artwork(word)
            _, _, touched, invariant = b.transfer_bounds(d, ix, pal)
            self.assertEqual(count, len(touched)); self.assertFalse(invariant)

    def test_opaque_word_replaces_instead_of_blending(self):
        d, ix, pal = artwork(0x001f)
        lo, hi, _, invariant = b.transfer_bounds(d, ix, pal)
        at = (20 * 256 + 2) * 3
        self.assertEqual(bytes([255, 0, 0]), lo[at:at + 3]); self.assertEqual(lo[at:at + 3], hi[at:at + 3])
        self.assertEqual(8, len(invariant))

    def test_pixel_center_gradient_modulates_before_flooring(self):
        d, ix, pal = artwork()
        for piece in (d['pieces'][2], d['pieces'][5]):
            piece['colors'] = [dict(r=c, g=c, b=c) for c in (64, 64, 128, 128)]
        lo, _, _, _ = b.transfer_bounds(d, ix, pal)
        self.assertEqual(159, lo[(20 * 256 + 2) * 3])
        self.assertEqual(223, lo[(21 * 256 + 2) * 3])

    def test_background_sensitive_texels_are_not_core_samples(self):
        d, ix, pal = artwork(0x8842)
        _, _, touched, invariant = b.transfer_bounds(d, ix, pal)
        self.assertEqual(8, len(touched)); self.assertFalse(invariant)

    def test_invalid_texture_indices_geometry_and_material_reject(self):
        d, ix, pal = artwork()
        self.assertRaises(ValueError, b.transfer_bounds, d, bytes([16]) + ix[1:], pal)
        for key, value in (('x', 256), ('v', 200), ('height', True), ('material', 'multiply')):
            wrong = copy.deepcopy(d); wrong['pieces'][0][key] = value
            with self.subTest(key=key): self.assertRaises(ValueError, b.transfer_bounds, wrong, ix, pal)

    def test_signed_sample_metrics_and_mask_identity(self):
        a = bytes([10, 20, 30, 255]) * 256 * 240
        c = bytearray(a); c[0] = 17; c[1] = 12
        report = b.samples(a, c, {0})
        self.assertEqual([-7, 8, 0], report['minOriginalMinusManaged'])
        self.assertEqual([7, 8, 0], report['maxAbsoluteDelta'])
        self.assertEqual(1, report['changedPixels']); self.assertFalse(report['exactEquality'])

    def test_exact_sample_equality_never_claims_original_fidelity_or_timing(self):
        d, ix, pal, rgba = pixels()
        report = b.compare(rgba, rgba, d, ix, pal)
        self.assertTrue(report['originalComparisonPerformed']); self.assertTrue(report['strictInvariantEquality'])
        for flag in ('originalFidelityEstablished', 'simulationCompared', 'timingComparisonPerformed', 'exactOriginalLogicalTickVerified'):
            self.assertFalse(report[flag])
        self.assertEqual(4, report['rows'][0]['managedBackgroundInvariant']['pixels'])

    def test_original_pixel_fault_is_visible_but_candidate_model_mismatch_rejects(self):
        d, ix, pal, rgba = pixels(); faulty = bytearray(rgba); faulty[(20 * 256 + 2) * 4] -= 1
        report = b.compare(faulty, rgba, d, ix, pal)
        self.assertFalse(report['strictInvariantEquality'])
        self.assertEqual(1, report['rows'][0]['managedBackgroundInvariant']['changedPixels'])
        self.assertRaises(ValueError, b.compare, rgba, faulty, d, ix, pal)

    def test_unmatched_background_is_reported_without_polluting_core_equality(self):
        d, ix, pal, rgba = pixels(); original = bytearray(rgba); original[0] = 80
        report = b.compare(original, rgba, d, ix, pal)
        self.assertTrue(report['strictInvariantEquality'])
        self.assertFalse(report['fullFrameComparisonPerformed'])
        self.assertFalse(report['originalFidelityEstablished'])

    def test_black_border_mapping_is_explicit_and_never_searches(self):
        original = bytes([0, 0, 0, 255]) * 280 * 240
        candidate = bytes([0, 0, 0, 255]) * 256 * 240
        a, c = b.align(original, candidate, [12, 0, 256, 240], [0, 0, 256, 240])
        self.assertEqual(a, c)
        self.assertRaises(ValueError, b.align, original, candidate, [11, 0, 256, 240], [0, 0, 256, 240])
        bad = bytearray(original); bad[0] = 1
        self.assertRaises(ValueError, b.align, bad, candidate, [12, 0, 256, 240], [0, 0, 256, 240])

    def test_image_mode_alpha_and_dimensions_reject(self):
        for mode, size, color in (('L', (280, 240), 0), ('RGB', (279, 240), 0), ('RGBA', (280, 240), (0, 0, 0, 0))):
            data = io.BytesIO(); Image.new(mode, size, color).save(data, format='PNG')
            with self.subTest(mode=mode, size=size): self.assertRaises(ValueError, b.image, data.getvalue(), (280, 240))


class ObservedBannerContractTests(unittest.TestCase):
    def setUp(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(prefix='observed-banner-', dir=base)
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name); (self.root / 'scratch').mkdir()
        self.root_patch = patch.object(v, 'ROOT', self.root); self.root_patch.start(); self.addCleanup(self.root_patch.stop)
        d, ix, pal, rgba = pixels()
        original = bytearray(bytes([0, 0, 0, 255]) * 280 * 240)
        for y in range(240): original[(y * 280 + 12) * 4:(y * 280 + 268) * 4] = rgba[y * 256 * 4:(y + 1) * 256 * 4]
        self.records = {}
        self.write('original.png', self.png(bytes(original), (280, 240)))
        self.write('tick-020.png', self.png(rgba, (256, 240)))
        self.write('state', b'authored state fixture, not a PS1 state')
        self.write('profile.json', dict(format='fft.retroarch-reference-profile', schemaVersion=1, edition='SCUS-94221'))
        self.observation = dict(format='fft.original-banner-state-observation', schemaVersion=1, edition='SCUS-94221',
            repeatRestoredImagesByteIdentical=True, selectedCoreOptionsRetained=True,
            state=dict(sha256=self.records['state']['sha256']), profile=dict(sha256=self.records['profile.json']['sha256']),
            restoreImages=[dict(sha256=self.records['original.png']['sha256'])])
        self.write('observation.json', self.observation)
        self.receipt = dict(referenceRevision=b.REVISION, rendererTaskAcknowledgments=0, policy='authored test', captures=[dict(
            tick=20, phase='Holding', file='tick-020.png', rgbaSha256=v.sha(rgba),
            pieces=[dict(PieceIndex=i, CropTop=0, VisibleRows=31) for i in range(6)])])
        self.write('check.json', self.receipt)
        self.write('definition.json', d); self.write('indices.bin', ix); self.write('palette.bin', pal)
        pins = {key: (self.records[name]['sha256'], len((self.root / self.records[name]['path']).read_bytes())) for key, name in
                (('definition', 'definition.json'), ('indices', 'indices.bin'), ('palette', 'palette.bin'))}
        self.pin_patch = patch.object(b, 'ARTIFACT_PINS', pins); self.pin_patch.start(); self.addCleanup(self.pin_patch.stop)
        self.plan = dict(format='fft.observed-banner-comparison', schemaVersion=1, profile='general-0-full-visible-v1',
            original={key: self.records[name] for key, name in (('png', 'original.png'), ('state', 'state'), ('observation', 'observation.json'), ('profile', 'profile.json'))},
            candidate=dict(png=self.records['tick-020.png'], receipt=self.records['check.json'], tick=20),
            prepared={key: self.records[name] for key, name in (('definition', 'definition.json'), ('indices', 'indices.bin'), ('palette', 'palette.bin'))},
            alignment=dict(originalRect=[12, 0, 256, 240], candidateRect=[0, 0, 256, 240], rationale='Authored black borders'),
            anchorRationale='Authored full-visible fixture; original tick is unknown')

    def png(self, rgba, size):
        data = io.BytesIO(); Image.frombytes('RGBA', size, rgba).save(data, format='PNG'); return data.getvalue()

    def write(self, name, value):
        data = value if type(value) is bytes else json.dumps(value).encode()
        (self.root / 'scratch' / name).write_bytes(data)
        self.records[name] = dict(path='scratch/' + name, sha256=v.sha(data))

    def rewrite(self, field, value):
        name = 'observation.json' if field == 'observation' else 'check.json'
        self.write(name, value)
        self.plan['original' if field == 'observation' else 'candidate'][field if field == 'observation' else 'receipt'] = self.records[name]

    def test_report_and_review_repeat_exactly_without_original_fidelity_claim(self):
        self.write('plan.json', self.plan)
        self.assertEqual(0, b.run('scratch/plan.json', 'scratch/report-a'))
        self.assertEqual(0, b.run('scratch/plan.json', 'scratch/report-b'))
        for name in ('report.json', 'review.html'):
            self.assertEqual((self.root / 'scratch/report-a' / name).read_bytes(), (self.root / 'scratch/report-b' / name).read_bytes())

    def test_changed_original_writes_a_difference_report_and_returns_one(self):
        path = self.root / 'scratch/original.png'
        im = Image.open(path).convert('RGBA'); im.putpixel((14, 20), (250, 255, 255, 255))
        data = io.BytesIO(); im.save(data, format='PNG'); im.close()
        self.write('original.png', data.getvalue()); self.plan['original']['png'] = self.records['original.png']
        wrong = copy.deepcopy(self.observation); wrong['restoreImages'][0]['sha256'] = self.records['original.png']['sha256']
        self.rewrite('observation', wrong); self.write('plan.json', self.plan)
        self.assertEqual(1, b.run('scratch/plan.json', 'scratch/difference'))
        report = json.loads((self.root / 'scratch/difference/report.json').read_bytes())
        self.assertEqual(1, report['rows'][0]['managedBackgroundInvariant']['changedPixels'])
        self.assertFalse(report['strictInvariantEquality'])

    def test_bad_identity_and_unknown_tolerance_fields_reject_without_output(self):
        self.plan['original']['png']['sha256'] = '0' * 64
        self.assertRaises(ValueError, b.load, self.plan)
        self.plan['original']['png'] = self.records['original.png'] = dict(path='scratch/original.png', sha256=v.sha((self.root / 'scratch/original.png').read_bytes()))
        self.plan['maxChannelDelta'] = 20
        self.assertRaises(ValueError, b.load, self.plan)
        self.assertFalse((self.root / 'scratch/report').exists())

    def test_original_state_profile_and_png_bindings_are_required(self):
        for field in ('state', 'profile', 'restoreImages'):
            wrong = copy.deepcopy(self.observation)
            if field == 'restoreImages': wrong[field][0]['sha256'] = '0' * 64
            else: wrong[field]['sha256'] = '0' * 64
            self.rewrite('observation', wrong)
            with self.subTest(field=field): self.assertRaises(ValueError, b.load, self.plan)

    def test_partial_checkpoint_pixel_digest_and_duplicate_tick_reject(self):
        for kind in ('partial', 'pixels', 'duplicate'):
            wrong = copy.deepcopy(self.receipt)
            if kind == 'partial': wrong['captures'][0]['pieces'][0]['VisibleRows'] = 30
            elif kind == 'pixels': wrong['captures'][0]['rgbaSha256'] = '0' * 64
            else: wrong['captures'].append(copy.deepcopy(wrong['captures'][0]))
            self.rewrite('receipt', wrong)
            with self.subTest(kind=kind): self.assertRaises(ValueError, b.load, self.plan)

    def test_unreviewed_prepared_bytes_reject_even_with_matching_plan_hash(self):
        self.write('indices.bin', bytes(51200)); self.plan['prepared']['indices'] = self.records['indices.bin']
        self.assertRaises(ValueError, b.load, self.plan)

    def test_invalid_version_profile_tick_and_anchor_reject(self):
        for key, value in (('schemaVersion', True), ('profile', 'anything'), ('anchorRationale', '')):
            wrong = copy.deepcopy(self.plan); wrong[key] = value
            with self.subTest(key=key): self.assertRaises(ValueError, b.load, wrong)
        self.plan['candidate']['tick'] = True
        self.assertRaises(ValueError, b.load, self.plan)

    def test_existing_output_is_preserved_before_reading_plan(self):
        out = self.root / 'scratch/report'; out.mkdir(); (out / 'sentinel').write_bytes(b'keep')
        self.assertRaises(ValueError, b.run, 'scratch/missing-plan.json', 'scratch/report')
        self.assertEqual(b'keep', (out / 'sentinel').read_bytes())

    def test_preparer_freezes_observation_and_validates_before_publication(self):
        source = self.root / 'docs/evidence/visual/original-banner-state-v1.json'; source.parent.mkdir(parents=True)
        observation = copy.deepcopy(self.observation)
        for key, name in (('state', 'state'), ('profile', 'profile.json')):
            observation[key]['localPath'] = self.records[name]['path']
        observation['restoreImages'][0]['localPath'] = self.records['original.png']['path']
        source.write_text(json.dumps(observation))
        (self.root / 'scratch/banner.json').write_bytes((self.root / 'scratch/definition.json').read_bytes())
        (self.root / 'scratch/palette-4.bin').write_bytes((self.root / 'scratch/palette.bin').read_bytes())
        prepare.run('scratch', 'scratch', 'scratch/plan-root')
        plan = v.parse((self.root / 'scratch/plan-root/plan.json').read_bytes())
        b.load(plan)
        self.assertEqual('scratch/plan-root/observation.json', plan['original']['observation']['path'])
        source.write_bytes(b'changed later')
        b.load(plan)  # Frozen observation still binds the original artifacts.
        self.assertRaises(ValueError, prepare.run, 'scratch', 'scratch', 'scratch/plan-root')

    def test_invalid_preparation_publishes_no_plan_or_staging_directory(self):
        source = self.root / 'docs/evidence/visual/original-banner-state-v1.json'; source.parent.mkdir(parents=True)
        observation = copy.deepcopy(self.observation)
        for key, name in (('state', 'state'), ('profile', 'profile.json')):
            observation[key]['localPath'] = self.records[name]['path']
        observation['restoreImages'][0]['localPath'] = self.records['original.png']['path']
        source.write_text(json.dumps(observation))
        (self.root / 'scratch/banner.json').write_bytes((self.root / 'scratch/definition.json').read_bytes())
        (self.root / 'scratch/palette-4.bin').write_bytes((self.root / 'scratch/palette.bin').read_bytes())
        self.receipt['captures'][0]['phase'] = 'Revealing'; self.write('check.json', self.receipt)
        self.assertRaises(ValueError, prepare.run, 'scratch', 'scratch', 'scratch/plan-root')
        self.assertFalse((self.root / 'scratch/plan-root').exists())
        self.assertFalse(list((self.root / 'scratch').glob('.observed-banner-plan-*')))


if __name__ == '__main__': unittest.main()
