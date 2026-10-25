"""Authored timer callback/TCB fixtures; no original opcodes, assets, BIOS or emulator."""
import copy
import json
from pathlib import Path
import struct
import tempfile
import unittest

import compare_ready_birth as birth
import inspect_ready_interrupt as irq
import test_banner_hide as hide


def fixture(next_frame=False):
    ram, write, gpu, _, build = hide.fixture(int(next_frame))
    words = {0x108: 0xa000e294, 0x10c: 4, 0x110: 0xa000e29c, 0x114: 0x300, 0xe294: 0xa000e29c,
             0xe29c: 0x4000, 0xe2a0: 0, 0xe318: 0x801fff40, 0xe320: 0x8001dc64,
             0xe324: 0x8001dd18 if next_frame else 0x8001dd1c, 0xe330: 0x40020404,
             0xe334: 0x30000400 if next_frame else 0x400, 0x8d0: 0xf40, 0x75c0: 0,
             0x120: 0xa000e028, 0x124: 0x268, 0xe124: 0xf2000002, 0xe128: 0x2000,
             0xe12c: 2, 0xe130: 0x1000, 0xe134: 0x800149dc, 0x8578: 0x1c20,
             0x8568: 0xa000e124, 0x856c: 0x2000, 0x8570: 0x4000, 0x8574: 0x1000,
             0x8544: 0x80014d50 if next_frame else 0x800149f8,
             0x801fff58: 0x8001dc64, 0x801fff78: 0x80093b68, 0x801fff98: 0x8007a058}
    for at, value in words.items(): write(at, value)
    gpr = [0] * 32; gpr[26] = 0xa000e2a4; gpr[28] = 0xf450
    gpr[29] = 0x8510 if next_frame else 0x84f8; gpr[31] = 0x80014d50 if next_frame else 0x800149f8
    cop0 = [0] * 32; cop0[12] = 0x40020404; cop0[13] = 0x30000400 if next_frame else 0x400
    cop0[14] = 0x8001dd18 if next_frame else 0x8001dd1c
    pc = 0x80015370 if next_frame else 0x80014630
    cpu = dict(GPR=struct.pack('<32I', *gpr), **{'CP0.Regs': struct.pack('<32I', *cop0)},
               BACKED_PC=struct.pack('<I', pc), BACKED_new_PC=struct.pack('<I', pc + 4), BACKED_new_PC_mask=bytes(4), Halted=b'\0')
    return ram, write, cpu, dict(logicalTick=3 if next_frame else 2), build, gpu


class ReadyInterruptTests(unittest.TestCase):
    def test_two_callbacks_recover_saved_main_not_live_interrupt_registers(self):
        for next_frame in (False, True):
            ram, _, cpu, task, _, _ = fixture(next_frame); saved = irq.saved_context(ram, cpu, task)
            self.assertEqual(0x801fff40, saved['resumeSp']); self.assertEqual(0x8001dc64, saved['resumeReturn'])
            self.assertLess(saved['liveSp'], 0x10000); self.assertEqual(9, saved['eventSlot'])
            self.assertEqual(0x8001dd18 if next_frame else 0x8001dd1c, saved['resumePc'])

    def test_original_main_classifier_still_rejects_both_valid_irq_layouts(self):
        for next_frame in (False, True):
            ram, _, cpu, task, build, _ = fixture(next_frame)
            irq.saved_context(ram, cpu, task)
            self.assertRaises(ValueError, irq.h.context, build(cpu_fields=list(cpu.items())), ram, task)

    def test_missing_original_fingerprints_cannot_reach_interrupt_classifier(self):
        ram, _, cpu, task, build, _ = fixture()
        self.assertRaises(ValueError, irq.context, build(cpu_fields=list(cpu.items())), ram, task)
        self.assertRaises(ValueError, irq.verify_code, ram[:-4])

    def test_stale_or_redirected_pcb_tcb_records_are_rejected(self):
        for at in (0x108, 0x10c, 0x110, 0x114, 0xe294, 0xe29c, 0xe2a0):
            ram, write, cpu, task, _, _ = fixture(); write(at, irq.d.word(ram, at) ^ 4)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_saved_epc_sp_ra_status_and_cause_must_match_source_context(self):
        for at in (0xe318, 0xe320, 0xe324, 0xe330, 0xe334):
            ram, write, cpu, task, _, _ = fixture(); write(at, irq.d.word(ram, at) ^ 4)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_exception_kind_branch_delay_and_cop0_inconsistency_reject(self):
        for register, wrong in ((12, 0x40020405), (13, 0x404), (13, 0x80000400), (14, 0x8001dc64)):
            ram, _, cpu, task, _, _ = fixture(); cop0 = list(struct.unpack('<32I', cpu['CP0.Regs'])); cop0[register] = wrong
            cpu['CP0.Regs'] = struct.pack('<32I', *cop0)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_active_save_pointer_callback_stack_and_return_are_required(self):
        for register in (26, 28, 29, 31):
            ram, _, cpu, task, _, _ = fixture(); gpr = list(struct.unpack('<32I', cpu['GPR'])); gpr[register] ^= 4
            cpu['GPR'] = struct.pack('<32I', *gpr)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)
        for at in (0x8544, 0x8568, 0x856c, 0x8570, 0x8574, 0x8578):
            ram, write, cpu, task, _, _ = fixture(); write(at, 0)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_disabled_event_wrong_handler_and_unsafe_card_gate_reject(self):
        for at in (0x8d0, 0x75c0, 0x120, 0x124, 0xe124, 0xe128, 0xe12c, 0xe130, 0xe134):
            ram, write, cpu, task, _, _ = fixture(); write(at, irq.d.word(ram, at) ^ 1)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_untraced_instruction_site_pipeline_and_tick_are_not_inferred(self):
        for key, value in (('BACKED_PC', 0x80014634), ('BACKED_new_PC', 0x80014630), ('BACKED_new_PC_mask', 4)):
            ram, _, cpu, task, _, _ = fixture(); cpu[key] = struct.pack('<I', value)
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)
        ram, _, cpu, task, _, _ = fixture(); cpu['Halted'] = b'\1'
        self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)
        ram, _, cpu, task, _, _ = fixture()
        self.assertRaises(ValueError, irq.saved_context, ram, cpu, dict(logicalTick=1))

    def test_missing_and_truncated_cpu_fields_do_not_fall_back_to_memory_search(self):
        for field in ('GPR', 'CP0.Regs', 'BACKED_PC', 'BACKED_new_PC', 'BACKED_new_PC_mask', 'Halted'):
            ram, _, cpu, task, _, _ = fixture(); cpu[field] = cpu[field][:-1]
            self.assertRaises(ValueError, irq.saved_context, ram, cpu, task)

    def test_saved_main_stack_and_gpu_target_still_require_independent_environment_checks(self):
        ram, write, cpu, task, build, _ = fixture(); saved = irq.saved_context(ram, cpu, task)
        context = lambda: irq.h._frame_context(build(cpu_fields=list(cpu.items())), ram, task,
                                               saved['resumePc'], saved['resumeSp'], saved['resumeReturn'])
        self.assertEqual([0, 0], context()['pendingDrawOrigin'])
        write(0x801fff78, 0); self.assertRaises(ValueError, context)
        write(0x801fff78, 0x80093b68); write(irq.h.DRAW_ENVS + 8, 0); self.assertRaises(ValueError, context)

    def test_wrong_bios_or_executable_cannot_authenticate_kernel_layout(self):
        self.assertRaises(ValueError, birth.source_inputs, b'', bytes(524288), bytes(356352))

    def test_native_birth_receipt_binds_both_frames_hashes_scales_and_reverse_order(self):
        pixels = bytes([8, 16, 24, 255]) * 256 * 240
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt = dict(format='fft.ready-background-cpu-check', policy='ready-glyph-rgb5-v1', renderer='ReadyPresentation.ComposeOver',
                originalComparison='pending', gpuReadbackPerformed=False, checkpoints=[])
            for tick, scale in ((0, 48), (1, 56)):
                file = f'tick-{tick:03}.rgba'; (root / file).write_bytes(pixels)
                receipt['checkpoints'].append(dict(logicalTick=tick, file=file, drawScale=scale, backgroundSha256=birth.r.v.sha(pixels),
                    rgbaSha256=birth.r.v.sha(pixels), draws=[dict(GlyphIndex=i, Scale=scale) for i in (5, 4, 3, 2, 1, 0)]))
            (root / 'check.json').write_text(json.dumps(receipt))
            frames, _ = birth.native_frames(root, [pixels, pixels]); self.assertEqual([pixels, pixels], frames)
            for field, wrong in (('logicalTick', 2), ('drawScale', 48), ('file', '../wrong.rgba'), ('backgroundSha256', '0' * 64),
                                 ('rgbaSha256', '0' * 64), ('draws', [dict(GlyphIndex=i, Scale=56) for i in range(6)])):
                changed = copy.deepcopy(receipt); changed['checkpoints'][1][field] = wrong
                (root / 'check.json').write_text(json.dumps(changed))
                self.assertRaises(ValueError, birth.native_frames, root, [pixels, pixels])


if __name__ == '__main__': unittest.main()
