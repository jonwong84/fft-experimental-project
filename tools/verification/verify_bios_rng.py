"""Optional, development-only oracle for the user-supplied SCPH-5501 BIOS.

Runs actual ROM instructions through their copied A0 vector/table in Unicorn.
No ROM bytes, disassembly, dependencies or raw traces belong in Git. This tool
is never called by the application or ordinary .NET tests.
"""

import argparse
import hashlib
import json
from pathlib import Path
import struct
import sys


EXPECTED_SHA256 = "11052b6499e466bbf0a709b1f9cb6834a9418e66680387912451e971cf8a1fef"
SEEDS = [0, 1, 0x7FFFFFFF, 0x80000000, 0xFFFFFFFF, 0x12345678,
    0xFC77A683, 0x0DBDBB1E]  # First updates become 0 and 0xffffffff: add-wrap boundaries.


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--bios", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--dependencies", type=Path, help="Optional local pip --target directory")
    args = parser.parse_args()
    if args.dependencies:
        sys.path.insert(0, str(args.dependencies.resolve()))
    import unicorn
    from unicorn import Uc, UC_ARCH_MIPS, UC_MODE_MIPS32, UC_MODE_LITTLE_ENDIAN, UC_HOOK_CODE, UC_HOOK_MEM_WRITE
    from unicorn.mips_const import UC_CPU_MIPS32_4KC, UC_MIPS_REG_RA, UC_MIPS_REG_A0, UC_MIPS_REG_V0, UC_MIPS_REG_T1, UC_MIPS_REG_PC

    scratch = Path(__file__).resolve().parents[2] / "scratch"
    if not args.output.resolve().is_relative_to(scratch.resolve()):
        raise ValueError("Raw oracle reports must remain beneath this checkout's ignored scratch directory")
    if args.bios.stat().st_size != 524288:
        raise ValueError("Expected the reviewed 512 KiB BIOS image")
    image = args.bios.read_bytes()
    digest = hashlib.sha256(image).hexdigest()
    if len(image) != 524288 or digest != EXPECTED_SHA256:
        raise ValueError("BIOS identity differs from the reviewed SCPH-5501 image")
    if unicorn.__version__ != "2.1.4":
        raise ValueError("This oracle was reviewed with Unicorn 2.1.4; review version changes explicitly")
    cpu = Uc(UC_ARCH_MIPS, UC_MODE_MIPS32 | UC_MODE_LITTLE_ENDIAN)
    cpu.ctl_set_cpu_model(UC_CPU_MIPS32_4KC)
    cpu.mem_map(0, 0x200000)
    cpu.mem_map(0x1FC00000, len(image))
    cpu.mem_write(0x1FC00000, image)
    # Reproduce the boot copy's final RAM image, without booting devices or a game.
    cpu.mem_write(0x500, image[0x10000:0x18BF0])
    stop = 0x80010000

    def execute(entry, budget=4096):
        cpu.reg_write(UC_MIPS_REG_RA, stop)
        cpu.emu_start(entry, stop, count=budget)
        if cpu.reg_read(UC_MIPS_REG_PC) != stop:
            raise AssertionError(f"Routine did not return within its instruction budget; PC={cpu.reg_read(UC_MIPS_REG_PC):x}")

    execute(0xBFC042A0)  # Copies the kernel's A0/B0/C0 vector stubs into low RAM.
    execute(0xBFC042D0)  # Copies A0 function pointers from ROM to RAM 0x200.
    rand_entry = struct.unpack("<I", cpu.mem_read(0x200 + 0x2F * 4, 4))[0]
    srand_entry = struct.unpack("<I", cpu.mem_read(0x200 + 0x30 * 4, 4))[0]
    if (rand_entry, srand_entry) != (0xBFC02200, 0xBFC02230):
        raise AssertionError("A0 dispatch targets differ from static analysis")
    initialized_ram = bytes(cpu.mem_read(0, 0x200000))

    def state():
        return struct.unpack("<I", cpu.mem_read(0x9010, 4))[0]

    def call(selector, argument=0):
        nonlocal cpu
        previous_state = state()
        # A fresh CPU per isolated BIOS call avoids emulator stop/translation-cache
        # artifacts. The observed sole persistent write is threaded explicitly in RAM.
        cpu = Uc(UC_ARCH_MIPS, UC_MODE_MIPS32 | UC_MODE_LITTLE_ENDIAN)
        cpu.ctl_set_cpu_model(UC_CPU_MIPS32_4KC)
        cpu.mem_map(0, 0x200000)
        cpu.mem_map(0x1FC00000, len(image))
        cpu.mem_write(0, initialized_ram)
        cpu.mem_write(0x9010, struct.pack("<I", previous_state))
        cpu.mem_write(0x1FC00000, image)
        trace, writes = [], []
        cpu.hook_add(UC_HOOK_CODE, lambda uc, address, size, data: trace.append(address))
        cpu.hook_add(UC_HOOK_MEM_WRITE, lambda uc, access, address, size, value, data: writes.append((address, size, value & 0xFFFFFFFF)))
        cpu.reg_write(UC_MIPS_REG_T1, selector)
        cpu.reg_write(UC_MIPS_REG_A0, argument)
        execute(0xA0, budget=128)
        # Unicorn reports translated physical RAM addresses for these KSEG1 writes.
        if len(writes) != 1 or writes[0][0:2] != (0x9010, 4):
            raise AssertionError(f"Unexpected RNG memory side effects: {writes}; PCs={[hex(pc) for pc in trace]}")
        if writes[0][2] != state():
            raise AssertionError("Captured store differs from resulting RAM word")
        return cpu.reg_read(UC_MIPS_REG_V0), list(trace)

    cases = []
    for seed in SEEDS:
        _, seed_trace = call(0x30, seed)
        if state() != seed or 0xBFC02238 not in seed_trace:
            raise AssertionError("Seed or return delay-slot store was not retained")
        observed = []
        expected_state = seed
        for draw in range(8):
            before = state()
            returned, draw_trace = call(0x2F)
            # Separate arithmetic expectation; observed values come from BIOS instruction execution.
            product = expected_state * 0x41C64E6D
            expected_state = (product + 0x3039) & 0xFFFFFFFF
            if state() != expected_state or returned != (expected_state >> 16) & 0x7FFF:
                raise AssertionError("ROM execution differs from the derived managed arithmetic")
            if 0xBFC0222C not in draw_trace:
                raise AssertionError("rand return delay-slot store was not executed")
            observed.append({"draw": draw + 1, "stateBefore": before,
                "stateAfter": state(), "value": returned,
                "multiplyExceeds32Bits": product > 0xFFFFFFFF,
                "additionExceeds32Bits": (product & 0xFFFFFFFF) + 0x3039 > 0xFFFFFFFF,
                "instructionCount": len(draw_trace)})
        # Re-seeding must replace state and reproduce the initial next output after intervening calls.
        call(0x30, seed)
        repeated, _ = call(0x2F)
        if (state(), repeated) != (observed[0]["stateAfter"], observed[0]["value"]):
            raise AssertionError("Repeated seed did not reproduce the first draw")
        cases.append({"seed": seed, "seedState": seed, "seedInstructionCount": len(seed_trace), "draws": observed})

    report = {"format": "fft.bios-rng-local-oracle", "schemaVersion": 1,
        "biosSha256": digest, "biosByteLength": len(image),
        "harnessSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "pythonVersion": sys.version, "unicornVersion": unicorn.__version__,
        "cpuModel": "MIPS32 4KC, little endian; not cycle-accurate PS1 hardware",
        "entry": "A0 vector at 0x000000a0, selectors 0x2f/0x30",
        "randEntry": rand_entry, "srandEntry": srand_entry,
        "stateVirtualAddress": 0xA0009010, "statePhysicalAddress": 0x9010,
        "cases": cases, "reseedChecksPassed": True,
        "limitations": ["Controlled RAM image and copied dispatch tables; no full BIOS boot or game execution",
            "Fresh CPU per isolated call; only the observed persistent state word is threaded between calls",
            "MIPS32 architectural execution of this MIPS-I subset; no cycle timing or general PS1 CPU equivalence",
            "Other BIOS revisions, game seed/draw history, devices and whole-command ordering not executed"]}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(f"Verified {len(cases)} seeds, {sum(len(c['draws']) for c in cases)} draws and reseeding through the supplied BIOS A0 path.")
    print(f"Raw report: {args.output}")


if __name__ == "__main__":
    main()
