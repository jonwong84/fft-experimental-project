"""Closed Ready birth/next-frame timer-2 IRQ contexts; never emulate or patch CPU state."""
import struct

import analyze_banner_precision as p
import compare_banner_hide as h
import compare_visual_checkpoints as v
import trace_banner_draw_state as d

# Resident original code fingerprints. ROM offsets document independently verified BIOS matches;
# None marks retail-patched code, checked against executable templates in the study.
KERNEL = ((0x80, 16, 'a9c217338111a9e42bb0298255fb4ca24063e5d21b674e8567755922cf7b8405', 0x10a0c),
          (0xc80, 128, '77d15e7ce94250a7a24671234aa6205078203370af718e2e398ceaa3fe63dacd', None),
          (0xd30, 444, '17dd2fa8ffccc9d238daef52c92fdf770ccbeb62a3bbef28cff40d17be00f7ed', 0x10830),
          (0xf40, 216, 'a784ec1182ab7eb4dcfb23ed8d1c15073454966ba455ec1f61300649f6fe87e9', 0x10a40),
          (0x1b30, 312, 'badba3f1d6b70ad9020ce389d9563d82a74e5479c1af47473a77e718999192e2', 0x11630),
          (0x5e0, 32, '7b2d3e0f812e51bdc09074f25e3bc3103f24932fef6d5190833b7e6893ff9ffb', 0x100e0),
          (0x641c, 56, '2c2495331ec51c7a0fe18ed57fb9e4507ba9cc9366a7ce6955ae7edf40b1daf1', None),
          (0x6594, 8, '6d64edf91449c1b17746c1ef18afa2eb25c70bdf1322ab3df5a2630993b7e2f1', 0x16094))
SOUND = ((0x80014590, 648, '8c3ed88a730f8d9d1ef11b426515f5f5feb8df4f88734506041928afa050adbc'),
         (0x800149dc, 1340, '47914f50ab68d8bfc34d528974f89b03fa394c9c6caef58ec5e09c52d65a3302'),
         (0x80015324, 1344, '8eb6628213d126e4886a89e6ac83ebf6ade55cbe13697d44b2838b98a6d5c924'))
TCB, REGISTERS = 0xa000e29c, 0xa000e2a4


def verify_code(ram):
    v.require(len(ram) == d.RAM_SIZE, 'Missing main RAM')
    for address, size, digest in SOUND + h.CODE + tuple(item[:3] for item in KERNEL):
        at = address & 0x1fffffff
        v.require(v.sha(ram[at:at + size]) == digest, 'IRQ/frame/BIOS source code differs')


def saved_context(ram, cpu, observation):
    """Decode source-derived fields after verify_code; separately testable with authored RAM."""
    for field, size in (('GPR', 128), ('CP0.Regs', 128), ('BACKED_PC', 4), ('BACKED_new_PC', 4), ('BACKED_new_PC_mask', 4), ('Halted', 1)):
        v.require(len(cpu.get(field, b'')) == size, 'Missing IRQ CPU field: ' + field)
    registers = struct.unpack('<32I', cpu['GPR']); cp0 = struct.unpack('<32I', cpu['CP0.Regs'])
    pc = int.from_bytes(cpu['BACKED_PC'], 'little'); sp = registers[29]
    # Two observed instruction sites, not a general callback/unwinding classifier.
    sites = {0x80014630: (0x800149f8, 0x84f8, 0x4c, 0x8001dd1c, 2, 0x400),
             0x80015370: (0x80014d50, 0x8510, 0x34, 0x8001dd18, 3, 0x30000400)}
    v.require(pc in sites, 'Unsupported interrupted Ready instruction site')
    caller, expected_sp, caller_offset, resume_pc, tick, cause = sites[pc]
    v.require(observation['logicalTick'] == tick and registers[31] == caller and sp == expected_sp and
              int.from_bytes(cpu['BACKED_new_PC'], 'little') == pc + 4 and not any(cpu['BACKED_new_PC_mask']) and
              cpu['Halted'] == b'\0', 'IRQ instruction pipeline/callback context differs')
    v.require([d.word(ram, at) for at in (0x108, 0x10c, 0x110, 0x114, 0xe294, TCB, TCB + 4)] ==
              [0xa000e294, 4, TCB, 0x300, TCB, 0x4000, 0], 'BIOS PCB/TCB identity differs')
    v.require(registers[26] == REGISTERS and registers[28] == 0xf450, 'Not the active saved exception context')
    v.require(cp0[12] == 0x40020404 and cp0[13] == cause and cp0[14] == resume_pc and
              cp0[13] & 0x8000007c == 0, 'Unsupported exception/status/branch-delay context')
    v.require([d.word(ram, REGISTERS + x) for x in (0x80, 0x8c, 0x90)] == [resume_pc, cp0[12], cause],
              'Saved exception record differs from live COP0')
    saved_sp, saved_ra = d.word(ram, REGISTERS + 29 * 4), d.word(ram, REGISTERS + 31 * 4)
    v.require(saved_sp == 0x801fff40 and saved_ra == 0x8001dc64, 'Saved main VSync registers differ')
    v.require([d.word(ram, at) for at in (0x8d0, 0x75c0, 0x120, 0x124)] ==
              [0xf40, 0, 0xa000e028, 0x268], 'BIOS restore/event table or card gate differs')
    v.require([d.word(ram, 0xe124 + x) for x in (0, 4, 8, 12, 16)] ==
              [0xf2000002, 0x2000, 2, 0x1000, 0x800149dc], 'Enabled timer-2 event/callback differs')
    v.require(d.word(ram, sp + caller_offset) == caller and d.word(ram, 0x8548 + 0x30) == 0x1c20 and
              [d.word(ram, 0x8548 + x) for x in (0x20, 0x24, 0x28, 0x2c)] ==
              [0xa000e124, 0x2000, 0x4000, 0x1000], 'Sound callback/BIOS event-dispatch stack differs')
    return dict(livePc=pc, liveSp=sp, liveReturn=caller, cp0Status=cp0[12], cp0Cause=cause,
                cp0Epc=resume_pc, pcb=0xa000e294, tcb=TCB, savedRegisters=REGISTERS,
                resumePc=resume_pc, resumeSp=saved_sp, resumeReturn=saved_ra,
                eventClass=0xf2000002, eventSlot=9, eventHandler=0x800149dc,
                method='Pinned BIOS exception save + B(17) restore, active timer-2 event and callback stack; no search or state patch')


def context(core, ram, observation):
    verify_code(ram)
    saved = saved_context(ram, p.subsystem_records(core, b'CPU'), observation)
    result = h._frame_context(core, ram, observation, saved['resumePc'], saved['resumeSp'], saved['resumeReturn'])
    result.update(cpuContextSource='BIOS TCB resume registers; live CPU remains in the timer-2 callback', interrupt=saved)
    return result
