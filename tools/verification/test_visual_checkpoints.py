"""Synthetic-only comparison regressions; python -m unittest discover -s tools/verification -p test_visual_checkpoints.py."""
import argparse
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock

import compare_visual_checkpoints as v
import prepare_synthetic_visual_comparison as synthetic


def fixtures():
    plan = dict(format='fft.visual-comparison', schemaVersion=1, mode='authored-reference', width=4, height=4,
                checkpoints=[dict(id='held', tick=2, timingDomain='logical-tick')],
                regions=[dict(id='shape', category='geometry', rect=[0, 0, 4, 4], maxChannelDelta=[0, 0, 0, 0],
                              maxChangedPixels=0, rationale='Exact authored RGBA bytes; no driver or native raster tolerance.')])
    checkpoint = dict(id='held', tick=2, timingDomain='logical-tick', simulationSha256='a' * 64,
                      png=dict(path='scratch/image.png', sha256='b' * 64), receipt=dict(path='scratch/receipt.json', sha256='c' * 64))
    reference = dict(format='fft.visual-capture', schemaVersion=1, kind='authored-expectation', width=4, height=4,
                     environment='authored', viewBindingSha256='d' * 64, checkpoints=[checkpoint])
    candidate = copy.deepcopy(reference); candidate.update(kind='managed-capture', environment='synthetic-test')
    pixels = bytes([20, 40, 60, 255] * 16)
    return plan, reference, candidate, pixels


class VisualCheckpointTests(unittest.TestCase):
    def test_synthetic_adapter_projects_explicit_scalars_in_unit_order(self):
        turns = dict(Ticks=0, Phase=0, ActiveUnitId=None, RngState=dict(Value=1, DrawCount=0),
                     Units=[dict(Id=dict(Value='sample:a'), Speed=10, Ct=0, TieKey=0, HasTurn=False, MovementTaken=False, ActionTaken=False)])
        first = synthetic.turn_projection(turns)
        self.assertEqual('sample:a', first['units'][0]['id'])
        self.assertEqual(1, first['rngState'])
        before = synthetic.canonical_hash(first)
        turns['Units'][0]['Ct'] = 1
        self.assertNotEqual(before, synthetic.canonical_hash(synthetic.turn_projection(turns)))
        self.assertEqual(synthetic.canonical_hash(dict(a=1, b=2)), synthetic.canonical_hash(dict(b=2, a=1)))

    def test_exact_authored_pass_and_no_original_claim(self):
        plan, a, b, rgba = fixtures()
        report = v.compare(plan, a, b, lambda *_: rgba)
        self.assertTrue(report['passed']); self.assertFalse(report['originalComparisonPerformed'])
        self.assertFalse(report['automaticRootCauseDiagnosis'])
        metrics = report['checkpoints'][0]['regions'][0]
        self.assertEqual([0, 0, 0, 0], metrics['maxAbsoluteDelta'])
        self.assertIsNone(metrics['changedBounds'])

    def test_one_pixel_color_difference_is_localized(self):
        plan, a, b, rgba = fixtures()
        changed = bytearray(rgba); changed[(1 * 4 + 2) * 4] += 7
        images = iter((rgba, bytes(changed)))
        report = v.compare(plan, a, b, lambda *_: next(images))
        self.assertEqual(['visual-mismatch'], report['checkpoints'][0]['failures'])
        m = report['checkpoints'][0]['regions'][0]
        self.assertEqual(1, m['changedPixels']); self.assertEqual(1, m['colorChangedPixels'])
        self.assertEqual(0, m['alphaChangedPixels']); self.assertEqual([2, 1, 1, 1], m['changedBounds'])
        self.assertEqual([7, 0, 0, 0], m['maxAbsoluteDelta']); self.assertEqual([7, 0, 0, 0], m['sumAbsoluteDelta'])

    def test_alpha_difference_has_separate_metric(self):
        plan, a, b, rgba = fixtures(); plan['regions'][0]['category'] = 'transparency-order'
        changed = bytearray(rgba); changed[3] = 0
        images = iter((rgba, bytes(changed)))
        report = v.compare(plan, a, b, lambda *_: next(images))
        m = report['checkpoints'][0]['regions'][0]
        self.assertEqual('transparency-order', m['declaredCategory'])
        self.assertEqual(1, m['alphaChangedPixels']); self.assertEqual(0, m['colorChangedPixels'])
        self.assertFalse(m['passed'])

    def test_each_declared_category_reports_without_inferring_cause(self):
        for category in sorted(v.CATEGORIES):
            with self.subTest(category=category):
                plan, a, b, rgba = fixtures(); plan['regions'][0]['category'] = category
                images = iter((rgba, bytes(len(rgba))))
                report = v.compare(plan, a, b, lambda *_: next(images))
                self.assertEqual(category, report['checkpoints'][0]['regions'][0]['declaredCategory'])
                self.assertFalse(report['passed'])

    def test_tick_and_simulation_mismatches_never_run_pixel_comparison(self):
        for failure in ('timing-mismatch', 'simulation-mismatch', 'view-binding-mismatch', 'canvas-mismatch'):
            with self.subTest(failure=failure):
                plan, a, b, _ = fixtures()
                if failure == 'timing-mismatch': b['checkpoints'][0]['tick'] = 3
                if failure == 'simulation-mismatch': b['checkpoints'][0]['simulationSha256'] = 'e' * 64
                if failure == 'view-binding-mismatch': b['viewBindingSha256'] = 'e' * 64
                if failure == 'canvas-mismatch': b['width'] = 5
                load = Mock(side_effect=AssertionError('incompatible image must not be compared'))
                report = v.compare(plan, a, b, load)
                self.assertEqual([failure], report['checkpoints'][0]['failures']); load.assert_not_called()

    def test_timing_domain_cannot_be_substituted(self):
        plan, a, b, _ = fixtures(); b['checkpoints'][0]['timingDomain'] = 'presentation-frame'
        report = v.compare(plan, a, b, Mock())
        self.assertEqual(['timing-mismatch'], report['checkpoints'][0]['failures'])

    def test_combined_simulation_timing_failures_stay_separate(self):
        plan, a, b, _ = fixtures(); b['checkpoints'][0].update(tick=3, simulationSha256='e' * 64)
        failures = v.compare(plan, a, b, Mock())['checkpoints'][0]['failures']
        self.assertEqual(['simulation-mismatch', 'timing-mismatch'], failures)

    def test_same_environment_mode_rejects_driver_drift(self):
        plan, a, b, rgba = fixtures(); plan['mode'] = 'same-environment'; a['kind'] = 'managed-capture'
        report = v.compare(plan, a, b, Mock())
        self.assertEqual(['environment-mismatch'], report['checkpoints'][0]['failures'])
        a['environment'] = b['environment']
        self.assertTrue(v.compare(plan, a, b, lambda *_: rgba)['passed'])

    def test_missing_extra_and_duplicate_checkpoints_reject(self):
        for variant in ('missing', 'extra', 'duplicate'):
            with self.subTest(variant=variant):
                plan, a, b, _ = fixtures()
                if variant == 'missing': b['checkpoints'] = []
                else:
                    b['checkpoints'].append(copy.deepcopy(b['checkpoints'][0]))
                    if variant == 'extra': b['checkpoints'][1]['id'] = 'other'
                self.assertRaises(ValueError, v.compare, plan, a, b, Mock())

    def test_channel_tolerance_is_inclusive_and_never_hides_raw_metrics(self):
        plan, a, b, rgba = fixtures(); plan['regions'][0]['maxChannelDelta'] = [2, 0, 0, 0]
        changed = bytearray(rgba); changed[0] += 2
        images = iter((rgba, bytes(changed)))
        report = v.compare(plan, a, b, lambda *_: next(images))
        self.assertTrue(report['passed']); self.assertEqual(1, report['checkpoints'][0]['regions'][0]['changedPixels'])
        changed[0] += 1; images = iter((rgba, bytes(changed)))
        self.assertFalse(v.compare(plan, a, b, lambda *_: next(images))['passed'])

    def test_pixel_count_threshold_is_inclusive(self):
        plan, a, b, rgba = fixtures(); plan['regions'][0]['maxChangedPixels'] = 1
        changed = bytearray(rgba); changed[0] += 1
        images = iter((rgba, bytes(changed))); self.assertTrue(v.compare(plan, a, b, lambda *_: next(images))['passed'])
        changed[4] += 1
        images = iter((rgba, bytes(changed))); self.assertFalse(v.compare(plan, a, b, lambda *_: next(images))['passed'])

    def test_half_open_region_does_not_include_adjacent_pixel(self):
        plan, a, b, rgba = fixtures(); plan['regions'][0]['rect'] = [1, 1, 1, 1]
        changed = bytearray(rgba); changed[(1 * 4 + 2) * 4] += 1
        images = iter((rgba, bytes(changed)))
        self.assertTrue(v.compare(plan, a, b, lambda *_: next(images))['passed'])

    def test_malformed_plans_reject(self):
        mutations = [lambda p: p.update(schemaVersion=True), lambda p: p.update(schemaVersion=2), lambda p: p.update(extra=0),
                     lambda p: p.update(mode='original-reference'), lambda p: p.update(width=0), lambda p: p.update(width=4097),
                     lambda p: p['regions'][0].update(rect=[0, 0, 5, 4]), lambda p: p['regions'][0].update(rect=[-1, 0, 4, 4]),
                     lambda p: p['regions'][0].update(maxChannelDelta=[-1, 0, 0, 0]), lambda p: p['regions'][0].update(rationale=' '),
                     lambda p: p['regions'][0].update(maxChangedPixels=17), lambda p: p['regions'][0].update(category='simulation'),
                     lambda p: p['regions'].append(copy.deepcopy(p['regions'][0]))]
        for index, mutation in enumerate(mutations):
            with self.subTest(index=index):
                plan, *_ = fixtures(); mutation(plan); self.assertRaises(ValueError, v.validate_plan, plan)

    def test_unknown_and_missing_capture_fields_reject(self):
        for variant in ('unknown', 'missing', 'unknown-nested', 'original-kind'):
            with self.subTest(variant=variant):
                _, a, *_ = fixtures()
                if variant == 'unknown': a['unknown'] = True
                if variant == 'missing': del a['viewBindingSha256']
                if variant == 'unknown-nested': a['checkpoints'][0]['extra'] = 0
                if variant == 'original-kind': a['kind'] = 'original-capture'
                self.assertRaises(ValueError, v.validate_capture, a)

    def test_duplicate_json_keys_and_oversized_metadata_reject(self):
        self.assertRaises(ValueError, v.parse, b'{"a":1,"a":2}')
        self.assertRaises(ValueError, v.parse, b'x' * 65537)

    def test_evidence_paths_cannot_escape_scratch(self):
        for path in ('../outside', 'scratch/../outside.png', 'C:/outside.png', '/outside.png', 'docs/test.png', 'scratch\\a.png'):
            with self.subTest(path=path): self.assertRaises(ValueError, v.scratch_file, path)

    def test_wrong_decoded_byte_count_rejects(self):
        plan, a, b, _ = fixtures()
        self.assertRaises(ValueError, v.compare, plan, a, b, lambda *_: b'')

    def test_report_and_inputs_are_stable_under_repetition(self):
        plan, a, b, rgba = fixtures(); frozen = json.dumps([plan, a, b])
        first = v.compare(plan, a, b, lambda *_: rgba)
        self.assertEqual(first, v.compare(plan, a, b, lambda *_: rgba))
        self.assertEqual(frozen, json.dumps([plan, a, b]))

    def test_png_adapter_and_cli_reports_repeat_without_gpu_or_game(self):
        from PIL import Image
        base = v.ROOT / 'scratch' / 'test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='visual-', dir=base) as directory:
            root = Path(directory); plan, a, b, rgba = fixtures()
            png = root / 'image.png'; Image.frombytes('RGBA', (4, 4), rgba).save(png)
            receipt = root / 'receipt.json'; receipt.write_text('{"evidence":"synthetic-only"}')
            for capture in (a, b):
                capture['checkpoints'][0]['png'] = dict(path=png.relative_to(v.ROOT).as_posix(), sha256=v.sha(png.read_bytes()))
                capture['checkpoints'][0]['receipt'] = dict(path=receipt.relative_to(v.ROOT).as_posix(), sha256=v.sha(receipt.read_bytes()))
            for name, value in (('plan', plan), ('reference', a), ('candidate', b)):
                (root / (name + '.json')).write_text(json.dumps(value))
            options = {name: (root / (name + '.json')).relative_to(v.ROOT).as_posix() for name in ('plan', 'reference', 'candidate')}
            for name in ('a', 'b'):
                self.assertEqual(0, v.run(argparse.Namespace(**options, output=(root / name).relative_to(v.ROOT).as_posix())))
            original = (root / 'a/report.json').read_bytes()
            self.assertEqual(original, (root / 'b/report.json').read_bytes())
            self.assertRaises(ValueError, v.run, argparse.Namespace(**options, output=(root / 'a').relative_to(v.ROOT).as_posix()))
            changed = bytearray(rgba); changed[0] += 1
            bad_png = root / 'changed.png'; Image.frombytes('RGBA', (4, 4), bytes(changed)).save(bad_png)
            b['checkpoints'][0]['png'] = dict(path=bad_png.relative_to(v.ROOT).as_posix(), sha256=v.sha(bad_png.read_bytes()))
            (root / 'candidate.json').write_text(json.dumps(b))
            self.assertEqual(1, v.run(argparse.Namespace(**options, output=(root / 'visual-fail').relative_to(v.ROOT).as_posix())))
            failure = json.loads((root / 'visual-fail/report.json').read_bytes())
            self.assertEqual(['visual-mismatch'], failure['checkpoints'][0]['failures'])
            png.write_bytes(b'bad')
            self.assertRaises(ValueError, v.run, argparse.Namespace(**options, output=(root / 'rejected').relative_to(v.ROOT).as_posix()))
            self.assertFalse((root / 'rejected').exists()); self.assertEqual(original, (root / 'a/report.json').read_bytes())

    def test_png_adapter_rejects_palette_gray_16bit_and_wrong_size(self):
        from PIL import Image
        base = v.ROOT / 'scratch' / 'test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='visual-formats-', dir=base) as directory:
            root = Path(directory)
            receipt = root / 'receipt.json'; receipt.write_text('{}')
            for mode, size in [('P', (4, 4)), ('L', (4, 4)), ('I;16', (4, 4)), ('RGBA', (5, 4))]:
                with self.subTest(mode=mode, size=size):
                    image = root / 'image.png'; Image.new(mode, size).save(image)
                    record = dict(receipt=dict(path=receipt.relative_to(v.ROOT).as_posix(), sha256=v.sha(receipt.read_bytes())),
                                  png=dict(path=image.relative_to(v.ROOT).as_posix(), sha256=v.sha(image.read_bytes())))
                    self.assertRaises(ValueError, v.read_pixels, record, 4, 4)

    def test_synthetic_adapter_rejects_missing_capture_without_partial_output(self):
        base = v.ROOT / 'scratch' / 'test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='visual-missing-', dir=base) as directory:
            root = Path(directory).relative_to(v.ROOT).as_posix()
            self.assertRaises(OSError, synthetic.run, root + '/missing', root + '/missing', root + '/output')
            self.assertFalse((v.ROOT / root / 'output').exists())


if __name__ == '__main__':
    unittest.main()
