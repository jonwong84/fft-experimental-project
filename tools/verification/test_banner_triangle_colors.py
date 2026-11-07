"""Authored signed fixed-point and diagonal vectors; no original media or emulator."""
from pathlib import Path
import tempfile
import unittest

import analyze_banner_precision as p
import compare_visual_checkpoints as v
import verify_banner_triangle_colors as tool


class BannerTriangleColorTests(unittest.TestCase):
    def test_triangle_seeds_explain_rounding_threshold_and_falling_gradient(self):
        for top, bottom, column, row, expected in ((10, 120, 173, 6, 37), (10, 120, 174, 6, 38),
                (10, 120, 57, 18, 92), (10, 120, 58, 18, 93), (120, 10, 173, 6, 93), (120, 10, 174, 6, 92)):
            self.assertEqual(expected, p.quad_vertex_channel(top, bottom, column, row, 232, 24))
        self.assertEqual(37, p.vertex_channel(10, 120, 6, 24))  # Historical top-seed counterfactual remains expressible.

    def test_shared_diagonal_matches_independent_q32_edge_walk(self):
        for width in range(1, 257):
            for height in range(1, 32):
                edge = (width << 32) + (1 << 32) - 2048
                numerator = -(width << 32) - (height - 1)
                step = -(abs(numerator) // height)
                for row in range(height):
                    self.assertEqual((width * (height - row) + height - 1) // height, edge >> 32)
                    edge += step

    def test_constant_and_exact_slopes_do_not_depend_on_triangle_seed(self):
        for height in (8, 16, 24, 31):
            for row in range(height):
                self.assertEqual(128, p.quad_vertex_channel(128, 128, 231, row, 232, height))
        for height in (8, 16):
            for row in range(height):
                self.assertEqual(p.vertex_channel(10, 120, row, height), p.quad_vertex_channel(10, 120, 231, row, 232, height))

    def test_unsupported_coordinates_and_channel_bounds_reject(self):
        for args in ((10, 120, -1, 0, 232, 24), (10, 120, 232, 0, 232, 24), (10, 120, 0, 24, 232, 24),
                     (10, 120, 0, 0, 0, 24), (10, 120, 0, 0, 257, 24), (-1, 120, 0, 0, 232, 24),
                     (10, 256, 0, 0, 232, 24), (10, 120, 0, 0, 232, 0)):
            self.assertRaises(ValueError, p.quad_vertex_channel, *args)

    def test_existing_output_preserved_before_archived_media_access(self):
        base = v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='triangle-existing-', dir=base) as folder:
            root = Path(folder); (root / 'sentinel').write_bytes(b'keep')
            self.assertRaises(ValueError, tool.run, root / 'missing-plan', root)
            self.assertEqual(b'keep', (root / 'sentinel').read_bytes())


if __name__ == '__main__': unittest.main()
