"""Authored logical expectations and independent raster/control boundaries."""
import unittest
import verify_dark_screen_introduction as intro


class CombinedIntroductionTests(unittest.TestCase):
    def test_natural_checkpoints(self):
        t = intro.timeline(60, 60, [])
        for key, value in dict(generalBirth=56, generalClear=147, readyBirth=148, readyRelease=208,
                               readyChildClear=216, readyDispatcherClear=217, darkRelease=218, darkClear=274, eventExit=275).items():
            self.assertEqual(t[key], value)

    def test_zero_and_maximum_holds(self):
        self.assertEqual(intro.timeline(0, 0, [])['eventExit'], 155)
        self.assertEqual(intro.timeline(255, 255, [])['eventExit'], 665)

    def test_birth_discard_and_explicit_skip(self):
        t = intro.timeline(60, 60, [1, 55, 56, 57, 59, 60])
        self.assertEqual((t['generalSkip'], t['readyBirth'], t['readyRelease'], t['darkRelease'], t['eventExit']), (57, 59, 60, 70, 127))

    def test_general_hide_input_is_not_forwarded(self):
        self.assertEqual(intro.timeline(60, 60, [138]), intro.timeline(60, 60, []))

    def test_invalid_holds_and_schedules_reject(self):
        for g, r, c in [(-1, 0, []), (0, 256, []), (0, 0, [0]), (0, 0, [4097]), (0, 0, [57, 57])]:
            with self.assertRaises(ValueError):
                intro.timeline(g, r, c)

    def test_dark_empty_flash_and_terminal_boundaries(self):
        self.assertEqual(intro.dark_geometry({}, 0, 218), ())
        self.assertEqual(intro.dark_geometry({}, 274, 218), ())
        self.assertEqual(intro.dark_geometry({}, 56, 218), intro.dark_geometry({}, 218, 218))

    def test_dark_background_and_overlap_are_ordered_and_quantized(self):
        quad = ((0, 0), (16, 0), (0, 16), (16, 16))
        bg = intro.dark.background()
        once = intro.dark.compose([quad], bg)
        twice = intro.dark.compose([quad, quad], bg)
        self.assertNotEqual(once, twice)
        self.assertEqual(once[3], 255)
        self.assertEqual(once[0] & 7, 0)

    def test_display_gaps_and_birth_counters(self):
        times = intro.timeline(60, 60, [])
        self.assertIsNone(intro.display_fields(55, 60, times)['general'])
        self.assertEqual(intro.display_fields(56, 60, times)['generalReveal'], 0)
        self.assertEqual(intro.display_fields(147, 60, times)['general'], 'Completed')
        self.assertIsNone(intro.display_fields(147, 60, times)['ready'])
        self.assertEqual(intro.display_fields(148, 60, times)['ready'], 'FadeIn')
        self.assertEqual(intro.display_fields(215, 60, times)['ready'], 'ExitYield')

    def test_capture_cannot_open_at_dark_clear_or_mutate_battle(self):
        times = intro.timeline(60, 60, [])
        frame = dict(tick=274, file='intro/tick-274.png', dark='Completed', darkDraw='None', darkRevision=3,
                     totalTasks=3, liveTasks=0, readyScale=None, Exited=False, battleAvailable=False,
                     journalEntries=0, battleSha256='expected', **intro.display_fields(274, 60, times))
        intro.validate_frame(frame, 274, times, 60, 'expected')
        for key, value in [('battleAvailable', True), ('Exited', True), ('journalEntries', 1),
                           ('battleSha256', 'changed'), ('liveTasks', 1), ('generalHold', 59), ('readyScale', 128)]:
            with self.subTest(key=key), self.assertRaises(ValueError):
                intro.validate_frame(dict(frame, **{key: value}), 274, times, 60, 'expected')


if __name__ == '__main__':
    unittest.main()
