"""Authored hide/input contexts only; original media and code are not test inputs."""
from pathlib import Path
import struct
import tempfile
import unittest

import inspect_banner_hide_circle as tool
import test_banner_continuation as continuation
import test_native_banner as native


def fixture(counter=1, before_crop=None, after_crop=None):
    crop = lambda count: min(31, max(0, count - 1) * 8)
    definition, before, _, slot, sp = continuation.fixture(0x801c9bc4, counter,
        crop(counter) if before_crop is None else before_crop)
    _, after, _, _, _ = continuation.fixture(0x801c9bc4, counter + 1,
        crop(counter + 1) if after_crop is None else after_crop)
    write = lambda ram, address, value: struct.pack_into('<I', ram, address & 0x1fffffff, value)
    write(before, tool.circle.h.FRAME_COUNTER, 500)
    write(after, tool.circle.h.FRAME_COUNTER, 501)
    # Move the after-chain into the other quad bank and retain its linked order.
    first = tool.d.BANNER_BASE & 0x1fffffff
    bank = tool.d.BANNER_BANK_STRIDE
    after[first + bank:first + bank + 6 * 52] = after[first:first + 6 * 52]
    for index in range(5):
        write(after, tool.d.BANNER_BASE + bank + index * 52,
            12 << 24 | ((tool.d.BANNER_BASE + bank + (index + 1) * 52) & 0xffffff))
    write(after, 0x80056b18 + 0x17f * 4, (tool.d.BANNER_BASE + bank) & 0xffffff)
    write(after, slot + 0x2c, 1)
    for address in tool.circle.INPUTS.values(): write(after, address, 0x20)
    return definition, before, after, write, slot, sp


class BannerHideCircleTests(unittest.TestCase):
    def test_circle_during_hide_preserves_task_and_advances_crop(self):
        definition, before, after, *_ = fixture()
        result = tool.witness(before, after, definition)
        self.assertEqual((81, 82), (result['before']['logicalTick'], result['after']['logicalTick']))
        self.assertEqual(('Hiding', 20, 60, 2, True), tuple(result['after'][key] for key in
            ('phase', 'revealCompleted', 'holdCompleted', 'hideCompleted', 'taskRunning')))
        self.assertEqual([dict(PieceIndex=index, CropTop=8, VisibleRows=23) for index in range(6)], result['after']['draws'])

    def test_bounded_hide_counters_and_last_iteration_rejection(self):
        for count in (0, 4, 8):
            definition, before, after, *_ = fixture(count)
            self.assertEqual(count + 1, tool.witness(before, after, definition)['after']['hideCompleted'])
        definition, before, after, *_ = fixture(9)
        self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_neutral_before_and_circle_only_after_are_required_at_each_layer(self):
        for address in tool.circle.INPUTS.values():
            for changed, value in (('before', 0x20), ('after', 0), ('after', 0x30)):
                definition, before, after, write, *_ = fixture()
                write(before if changed == 'before' else after, address, value)
                self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_frame_identity_stack_progress_and_buffer_guards_reject(self):
        for field in ('frame', 'parameter', 'pc', 'running', 'task', 'stack', 'counter', 'bank'):
            definition, before, after, write, slot, sp = fixture()
            address = dict(frame=tool.circle.h.FRAME_COUNTER, parameter=slot, pc=slot + 0x44,
                running=slot + 0x48, task=slot + 0x4c, stack=slot + 0x3c, counter=sp + 0x18, bank=slot + 0x2c)[field]
            write(after, address, 0)
            self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_counter_must_explain_crops_in_both_states(self):
        for key in ('before_crop', 'after_crop'):
            definition, before, after, *_ = fixture(**{key: 16})
            self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_hide_witness_cannot_be_classified_as_a_circle_skip(self):
        definition, before, after, *_ = fixture()
        self.assertRaises(ValueError, tool.circle.witness, before, after, definition)

    def test_missing_original_code_and_existing_output_reject(self):
        _, before, *_ = fixture()
        self.assertRaises(ValueError, tool.circle.read_state, native.state_with_ram(before))
        base = tool.v.ROOT / 'scratch/test-temp'
        base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='hide-circle-', dir=base) as name:
            out = Path(name); (out / 'sentinel').write_bytes(b'keep')
            self.assertRaises(ValueError, tool.run, out / 'missing', out)
            self.assertEqual(b'keep', (out / 'sentinel').read_bytes())


if __name__ == '__main__': unittest.main()
