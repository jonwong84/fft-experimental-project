"""Authored paired input/phase witnesses; no original code, media or emulator."""
from pathlib import Path
import struct
import tempfile
import unittest

import inspect_banner_circle as tool
import test_banner_reveal as reveal
import test_native_banner as native


def fixture(hold=None):
    definition, before, _, slot, sp, _ = reveal.fixture(5 if hold is None else 19)
    write = lambda ram, at, value: struct.pack_into('<I', ram, at & 0x1fffffff, value)
    if hold is not None:
        write(before, slot + 0x44, 0x801c9ad8)
        write(before, sp + 0x18, hold)
    write(before, tool.h.FRAME_COUNTER, 500)
    after = bytearray(before)
    for at in tool.INPUTS.values(): write(after, at, 0x20)
    write(after, tool.h.FRAME_COUNTER, 501); write(after, slot + 0x44, 0x801c9f68)
    write(after, 0x801d208c, tool.INPUTS['eventPressed']); write(after, 0x80056b18 + 0x17f * 4, 0xffffff)
    terminal = bytearray(after)
    for at in tool.INPUTS.values(): write(terminal, at, 0)
    for at, value in ((tool.h.FRAME_COUNTER, 502), (slot + 0x44, 0x8014c984), (slot + 0x48, 0), (slot + 0x4c, 0)):
        write(terminal, at, value)
    return definition, before, after, terminal, write, slot, sp


class BannerCircleTests(unittest.TestCase):
    def test_hold_witness_retains_completed_reveal_and_sampled_hold_progress(self):
        for count in (0, 1, 59):
            definition, before, after, terminal, *_ = fixture(hold=count)
            result = tool.witness(before, after, definition)
            self.assertEqual('Holding', result['before']['phase'])
            for observed, tick in ((result['after'], 21 + count), (tool.cleared(terminal, result), 22 + count)):
                self.assertEqual((tick, 20, count, 0, 'CircleSkipped'), tuple(observed[key] for key in
                    ('logicalTick', 'revealCompleted', 'holdCompleted', 'hideCompleted', 'exitCause')))

    def test_hold_witness_rejects_an_exhausted_counter(self):
        definition, before, after, *_ = fixture(hold=60)
        self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_paired_witness_preserves_counter_and_observes_extra_exit_yield(self):
        definition, before, after, terminal, *_ = fixture()
        result = tool.witness(before, after, definition)
        self.assertEqual((5, 6), (result['before']['logicalTick'], result['after']['logicalTick']))
        self.assertEqual(('ExitYield', 5, 0, 0, 'CircleSkipped', False), tuple(result['after'][key] for key in
            ('phase', 'revealCompleted', 'holdCompleted', 'hideCompleted', 'exitCause', 'isTerminal')))
        self.assertEqual((7, 'Completed', 5), tuple(tool.cleared(terminal, result)[key] for key in ('logicalTick', 'phase', 'revealCompleted')))

    def test_natural_completion_inspector_does_not_misclassify_shared_skip_pc(self):
        definition, _, after, *_ = fixture()
        self.assertRaises(ValueError, tool.c.inspect_ram, after, definition, early_reveal=True)

    def test_before_must_be_neutral_and_after_circle_only_at_all_input_layers(self):
        for key, address in tool.INPUTS.items():
            for changed, value in (('before', 0x20), ('after', 0), ('after', 0x30)):
                definition, before, after, _, write, *_ = fixture()
                write(before if changed == 'before' else after, address, value)
                self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_frame_counter_stack_progress_and_retained_task_identity_bind(self):
        for field in ('frame', 'parameter', 'pc', 'running', 'task', 'stack', 'counter', 'pointer'):
            definition, before, after, _, write, slot, sp = fixture()
            at = dict(frame=tool.h.FRAME_COUNTER, parameter=slot, pc=slot + 0x44, running=slot + 0x48,
                      task=slot + 0x4c, stack=slot + 0x3c, counter=sp + 0x18, pointer=0x801d208c)[field]
            write(after, at, 0); self.assertRaises(ValueError, tool.witness, before, after, definition)

    def test_skip_and_clear_require_empty_current_banner_draw_list(self):
        definition, before, after, terminal, write, *_ = fixture()
        result = tool.witness(before, after, definition)
        packet = tool.d.BANNER_BASE & 0xffffff
        write(after, 0x80056b18 + 0x17f * 4, packet)
        self.assertRaises(ValueError, tool.witness, before, after, definition)
        write(terminal, 0x80056b18 + 0x17f * 4, packet)
        self.assertRaises(ValueError, tool.cleared, terminal, result)

    def test_task_clear_rejects_reuse_or_early_late_or_non_neutral_continuation(self):
        for field, value in (('frame', 501), ('frame', 503), ('parameter', 0), ('task', 56), ('running', 1), ('pc', 0x801c9f68), ('input', 0x20)):
            definition, before, after, terminal, write, slot, _ = fixture(); result = tool.witness(before, after, definition)
            at = dict(frame=tool.h.FRAME_COUNTER, parameter=slot, task=slot + 0x4c, running=slot + 0x48,
                      pc=slot + 0x44, input=tool.INPUTS['eventPressed'])[field]
            write(terminal, at, value); self.assertRaises(ValueError, tool.cleared, terminal, result)

    def test_dead_stack_is_not_reinterpreted_as_completed_reveal_or_hide_progress(self):
        definition, before, after, terminal, write, _, sp = fixture(); result = tool.witness(before, after, definition)
        write(terminal, sp + 0x18, 999)
        self.assertEqual(5, tool.cleared(terminal, result)['revealCompleted'])
        self.assertEqual(0, tool.cleared(terminal, result)['hideCompleted'])

    def test_missing_code_fingerprints_and_existing_output_reject(self):
        _, before, *_ = fixture()
        self.assertRaises(ValueError, tool.read_state, native.state_with_ram(before))
        base = tool.v.ROOT / 'scratch/test-temp'; base.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix='circle-existing-', dir=base) as name:
            output = Path(name); (output / 'sentinel').write_bytes(b'keep')
            self.assertRaises(ValueError, tool.run, output / 'missing', output)
            self.assertEqual(b'keep', (output / 'sentinel').read_bytes())


if __name__ == '__main__': unittest.main()
