"""Authored independent model vectors; no prepared retail files or graphics device required."""
import struct
import unittest
import verify_banner_ready_sequence as s


class BannerReadyModelTests(unittest.TestCase):
    def test_natural_timeline_uses_overlapping_ready_fade_in_and_two_parent_observation_gaps(self):
        t = s.timeline(60, 60, [])
        self.assertEqual([91, 92, 152, 160, 161, 162], [t[k] for k in ('generalClear','readyBirth','readyRelease','readyChildClear','readyDispatcherClear','eventExit')])
        self.assertEqual(42, s.timeline(0, 0, [])['eventExit']); self.assertEqual(552, s.timeline(255, 255, [])['eventExit'])

    def test_circle_at_successor_birth_is_not_deferred_and_next_tick_can_release(self):
        ignored = s.timeline(60, 60, [5, 7]); released = s.timeline(60, 60, [5, 7, 8])
        self.assertEqual((7, 67), (ignored['readyBirth'], ignored['readyRelease']))
        self.assertEqual((48, 56), (s.scale(7, ignored), s.scale(8, ignored)))
        self.assertEqual(128, s.scale(8, released)); self.assertEqual(18, released['eventExit'])

    def test_geometry_uses_half_open_crops_and_has_no_stale_draw_after_clear(self):
        t = s.timeline(60, 60, [])
        self.assertEqual([], s.geometry(1, 60, t)); self.assertEqual([(0,23),(1,23),(2,23)], s.geometry(2, 60, t))
        self.assertEqual([(0,0),(1,0),(2,0)], s.geometry(10, 60, t))
        self.assertEqual([(i,0) for i in range(6)], s.geometry(81, 60, t))
        for tick in (85, 91, 92): self.assertEqual([], s.geometry(tick, 60, t))

    def test_unsigned_cross_product_gradients_match_literal_asymmetric_and_falling_samples(self):
        for colors, x, y, expected in [([0,64,128,255],0,0,0),([0,64,128,255],4,6,48),([0,64,128,255],12,6,80),
                                        ([0,64,128,255],15,23,239),([255,128,64,0],15,23,9)]:
            self.assertEqual(expected, s.vertex(colors,16,24,x,y)>>24)

    def test_ready_pixels_distinguish_transparent_opaque_and_stp_samples(self):
        background = bytes([80,160,240,255]) * 256 * 240
        definition = dict(glyphs=[dict(x=0,y=0,u=0,v=0,width=16,height=24,colors=[dict(r=128,g=128,b=128)]*4)])
        indices = bytes([0,1,2]+[0]*51197)
        palette = struct.pack('<16H',0,0x001f,0x801f,*([0]*13))
        pixels = s.ready_pixels(background,definition,indices, palette,128)
        self.assertEqual(bytes([80,160,240,255,248,0,0,255,160,80,120,255]),pixels[:12])
        self.assertEqual(background,s.ready_pixels(background,definition,indices,palette,None))


if __name__ == '__main__': unittest.main()
