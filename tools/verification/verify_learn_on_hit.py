"""Source-only learn-on-hit audit. Never builds, invokes or links the C reference."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REVISION = '11464943bbdbd2a0af022d90cb7f10455cf911df'


def roll(seed, rate):
    seed = (seed * 1103515245 + 12345) & 0xffffffff
    output = (seed >> 16) & 32767
    return seed, output * 100 // 32768 < rate


def validate():
    # Arithmetic checked over every BIOS output, independent of the managed rule.
    boundaries = 0
    for rate in (0, 1, 29, 30, 99, 100, 255):
        count = sum(output * 100 // 32768 < rate for output in range(32768))
        expected = min(32768, (rate * 32768 + 99) // 100)
        if count != expected:
            raise ValueError('Raw-rate boundary')
        boundaries += 32768
    if roll(57, 29) != (2770839166, False) or roll(57, 30) != (2770839166, True):
        raise ValueError('Seed57 vector')
    seed, success = roll(1, 30)
    if success or not roll(seed, 30)[1]:
        raise ValueError('Duplicate-slot failure/success vector')
    # Original MSB-first three-byte row: bits 0,8,21 -> 80 80 04.
    row = bytearray(3)
    for slot in (0, 8, 21):
        row[slot // 8] |= 128 >> (slot % 8)
    if row.hex() != '808004':
        raise ValueError('Learned row packing')
    return dict(arithmeticComparisons=boundaries, rowHex=row.hex())


def audit(reference):
    if subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=reference, text=True).strip() != REVISION:
        raise ValueError('Reference revision')
    if subprocess.check_output(['git', 'status', '--porcelain'], cwd=reference, text=True).strip():
        raise ValueError('Reference must be clean')
    evidence = json.loads((ROOT / 'docs/evidence/combat/learn-on-hit-source-v1.json').read_text(encoding='utf-8'))
    for row in evidence['sourceFiles']:
        if hashlib.sha256((reference / row['path']).read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Source fingerprint: ' + row['path'])
    return dict(validate(), referenceRevision=REVISION, sourceFiles=len(evidence['sourceFiles']),
                referenceClean=True, originalExecuted=False, cExecuted=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--reference', type=Path, required=True)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.reference)
    if args.output:
        with args.output.open('x', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2)
            stream.write('\n')
    print(json.dumps(result))
