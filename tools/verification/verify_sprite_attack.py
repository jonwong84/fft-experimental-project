"""Optional executable-reference capture of verified SCUS-94221 attack-image instructions.

Uses existing pinned Unicorn dependencies; never called by the managed runtime/tests.
Original executable bytes, input data and output media remain in ignored scratch/.
"""
import argparse
import hashlib
import json
from pathlib import Path
import platform
import struct
import sys
import uuid

ROOT = Path(__file__).resolve().parents[2]
REFERENCE = '11464943bbdbd2a0af022d90cb7f10455cf911df'
BATTLE_HASH = '7af579c7bcfd46f21f8d6886acb6aae156474dace667553e76079be7c7d6db94'
SPR_HASH = '7f7a94e5615137f9a4fa6e1dcfbad8b2760a6b66099154171b0e1feb137632d2'
EXPECTED = 'd07fff4e49bf5e0a8b91343b2a722521ac5309e34ec23d2d5810e579a741c24f'


def digest(data):
    return hashlib.sha256(data).hexdigest()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--battle', type=Path, required=True)
    parser.add_argument('--sprite', type=Path, required=True)
    parser.add_argument('--dependencies', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True, help='Fresh directory below repository scratch/')
    args = parser.parse_args()
    scratch = (ROOT / 'scratch').resolve()
    destination = args.output.absolute()
    require(destination.resolve().is_relative_to(scratch) and destination.resolve() != scratch,
            'Capture output must remain below repository scratch/')
    require(not destination.exists(), 'Output already exists; choose a fresh directory')
    for ancestor in (destination, *destination.parents):
        require(not ancestor.is_symlink() and not ancestor.is_junction(), 'Reparse capture paths are unsupported')
    for path in (args.battle, args.sprite):
        require(path.resolve().is_relative_to(scratch), 'Original inputs must remain below scratch/')
    require(args.battle.stat().st_size == 1397096 and args.sprite.stat().st_size == 49572,
            'Unexpected original input lengths')
    battle, sprite = args.battle.read_bytes(), args.sprite.read_bytes()
    require(digest(battle) == BATTLE_HASH and digest(sprite) == SPR_HASH, 'Original input SHA-256 mismatch')
    fixture_path = ROOT / 'docs/fixtures/chocobo-attack-image.json'
    fixture_bytes = fixture_path.read_bytes()
    fixture = json.loads(fixture_bytes)
    require(fixture['sourceSha256'] == SPR_HASH and fixture['decodedSha256'] == EXPECTED and
            fixture['decodedByteLength'] == 25600 and fixture['pixelPacking'] == 'first-pixel-high-nibble',
            'Historical fixture selection changed; review explicitly')
    ranges = []
    for address, size, name, prefix in (
        (0x8007A9B0, 132, 'compressed_nibble_read', 'ccef6d3b68664059'),
        (0x8007AA34, 412, 'sprite_attack_image_decompress', 'caae250e7e6322ea'),
    ):
        offset = address - 0x80067000
        sha = digest(battle[offset:offset + size])
        require(sha.startswith(prefix), 'Pinned original instruction range differs: ' + name)
        ranges.append(dict(name=name, address=address, bytes=size, sha256=sha))

    sys.path.insert(0, str(args.dependencies.resolve()))
    import unicorn
    from unicorn import Uc, UC_ARCH_MIPS, UC_MODE_MIPS32, UC_MODE_LITTLE_ENDIAN, UC_HOOK_CODE
    from unicorn.mips_const import UC_CPU_MIPS32_4KC, UC_MIPS_REG_0, UC_MIPS_REG_A0, UC_MIPS_REG_A1, UC_MIPS_REG_SP, UC_MIPS_REG_RA, UC_MIPS_REG_PC
    require(unicorn.__version__ == '2.1.4', 'Review changes to pinned Unicorn 2.1.4 explicitly')
    cpu = Uc(UC_ARCH_MIPS, UC_MODE_MIPS32 | UC_MODE_LITTLE_ENDIAN)
    cpu.ctl_set_cpu_model(UC_CPU_MIPS32_4KC)
    cpu.mem_map(0, 0x200000)
    cpu.mem_write(0x67000, battle)  # Original overlay at its linked address, no relocations/patches.
    compressed = sprite[0x9200:]
    cpu.mem_write(0x10000, compressed)
    cpu.mem_write(0x1FFF0, b'\xd3' * 16 + b'\xcd' * 25600 + b'\xd3' * 16)
    cpu.reg_write(UC_MIPS_REG_A0, 0x80010000)
    cpu.reg_write(UC_MIPS_REG_A1, 0x80020000)
    cpu.reg_write(UC_MIPS_REG_SP, 0x801FF000)
    cpu.reg_write(UC_MIPS_REG_RA, 0x80040000)
    counts = dict(instructions=0, sourceReads=0, sourceHighWater=0, outputWrites=0)
    output_bytes = set()

    def code(machine, address, size, user):
        require(0x8007A9B0 <= address < 0x8007ABD0, f'Unexpected instruction target: {address:#x}')
        counts['instructions'] += 1
        # Memory hooks raise a CPU exception on this locally observed MIPS path.
        # Inspect the effective address of each original load/store before execution;
        # final output/globals/canaries independently check the resulting writes.
        instruction = struct.unpack('<I', machine.mem_read(address & 0x1FFFFFFF, 4))[0]
        opcode = instruction >> 26
        if opcode >= 0x20:
            require(opcode in (0x23, 0x24, 0x28, 0x2B), f'Unreviewed memory opcode: {opcode:#x}')
            base = machine.reg_read(UC_MIPS_REG_0 + ((instruction >> 21) & 31))
            immediate = instruction & 0xFFFF
            if immediate & 0x8000:
                immediate -= 0x10000
            memory((base + immediate) & 0xFFFFFFFF, 4 if opcode in (0x23, 0x2B) else 1, opcode in (0x28, 0x2B))

    def memory(address, size, writing):
        physical = address & 0x1FFFFFFF
        if 0x10000 <= physical and physical + size <= 0x10000 + len(compressed) and not writing:
            counts['sourceReads'] += 1
            counts['sourceHighWater'] = max(counts['sourceHighWater'], physical + size - 0x10000)
        elif 0x20000 <= physical and physical + size <= 0x26400:
            if writing:
                counts['outputWrites'] += 1
                output_bytes.update(range(physical, physical + size))
        elif 0x962C8 <= physical and physical + size <= 0x962D4:
            pass  # Only the original nibble phase, offset and source pointer globals.
        elif 0x1FE000 <= physical and physical + size <= 0x1FF000:
            pass  # Controlled stack, disjoint from overlay/input/output.
        else:
            raise ValueError(f'Unexpected data access: {address:#x}, bytes={size}, write={writing}')

    cpu.hook_add(UC_HOOK_CODE, code)
    cpu.emu_start(0x8007AA34, 0x80040000, count=2000000)
    require(cpu.reg_read(UC_MIPS_REG_PC) == 0x80040000, 'Original routine did not return within instruction budget')
    decoded = bytes(cpu.mem_read(0x20000, 25600))
    require(len(output_bytes) == 25600, 'Not every output byte was initialized by original writes')
    require(bytes(cpu.mem_read(0x1FFF0, 16)) == b'\xd3' * 16 and bytes(cpu.mem_read(0x26400, 16)) == b'\xd3' * 16,
            'Output canary changed')
    require(bytes(cpu.mem_read(0x10000, len(compressed))) == compressed, 'Compressed input changed')
    require(digest(decoded) == EXPECTED, 'Executable output differs from the historical managed fixture')
    phase, offset, pointer = struct.unpack('<III', cpu.mem_read(0x962C8, 12))
    report = dict(format='fft.sprite-attack-executable-capture', schemaVersion=1, referenceRevision=REFERENCE,
                  edition='SCUS-94221', evidenceKind='executable-reference', originalGameBooted=False,
                  battleSha256=BATTLE_HASH, spriteSha256=SPR_HASH, sourceOffset=0x9200, sourceBytes=len(compressed),
                  codeRanges=ranges, tools=dict(python=platform.python_version(), unicorn=unicorn.__version__, cpu='MIPS32-4KC-little-endian'),
                  harnessSha256=digest(Path(__file__).read_bytes()), counters=counts,
                  finalGlobals=dict(phase=phase, offset=offset, pointer=pointer), outputFile='decoded.bin',
                  outputBytes=len(decoded), outputSha256=digest(decoded), comparedFixture='docs/fixtures/chocobo-attack-image.json',
                  comparedFixtureSha256=digest(fixture_bytes),
                  palette=None, frame=None, camera=None, timing=None,
                  limitations='Architectural routine execution only; not PS1 cycle/load-delay/GPU fidelity, sprite rendering or a booted game.')
    payload = (json.dumps(report, indent=2) + '\n').encode()
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.sprite-oracle-' + uuid.uuid4().hex)
    staging.mkdir()
    try:
        (staging / 'decoded.bin').write_bytes(decoded)
        (staging / 'capture.json').write_bytes(payload)
        staging.rename(destination)
    finally:
        if staging.exists():
            for name in ('decoded.bin', 'capture.json'):
                (staging / name).unlink(missing_ok=True)
            staging.rmdir()
    print(f'Executable-reference agrees: {len(decoded)} bytes, SHA-256 {digest(decoded)}; {counts["instructions"]} instructions')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
