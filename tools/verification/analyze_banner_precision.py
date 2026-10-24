"""Source-informed full-visible banner precision study; no runtime renderer change.

Reads the pinned comparison plan and frozen local state. No emulator execution,
network, native code, original instruction execution or new package is required.
"""
import argparse
import json
from pathlib import Path
import struct
import sys
import uuid
import zlib

import compare_observed_banner as b
import compare_visual_checkpoints as v

DITHER = ((-4, 0, -3, 1), (2, -2, 3, -1), (-3, 1, -4, 0), (3, -1, 2, -2))
EMULATOR_SOURCE = 'dda8cf71d420b1972d92324a5edecf2b92b1ec92'
MAX_STATE = 32 * 1024 * 1024


def unpack_state(data):
    """Bounded RZIP v1 -> RASTATE v1 MEM block; ignore replay data, never execute it."""
    v.require(20 <= len(data) <= MAX_STATE and data[:8] == b'#RZIPv\x01#', 'Unsupported RZIP state')
    chunk_size, total = struct.unpack_from('<IQ', data, 8)
    v.require(chunk_size == 131072 and 1 <= total <= MAX_STATE, 'Unsupported state/chunk budget')
    result, pos = bytearray(), 20
    while pos < len(data):
        v.require(pos + 4 <= len(data), 'Truncated chunk header')
        size = struct.unpack_from('<I', data, pos)[0]; pos += 4
        v.require(0 < size <= chunk_size + 1024 and pos + size <= len(data), 'Invalid compressed chunk')
        decoder = zlib.decompressobj()
        block = decoder.decompress(data[pos:pos + size], chunk_size + 1)
        v.require(decoder.eof and not decoder.unused_data and not decoder.unconsumed_tail and
                  len(block) == min(chunk_size, total - len(result)), 'Invalid state decompression length')
        result.extend(block); pos += size
        v.require(len(result) <= total, 'State exceeds declared size')
    v.require(len(result) == total and result[:8] == b'RASTATE\x01', 'Invalid RASTATE payload')
    blocks, pos = {}, 8
    while pos + 8 <= len(result):
        tag = bytes(result[pos:pos + 4]); size = struct.unpack_from('<I', result, pos + 4)[0]; pos += 8
        v.require(tag not in blocks and len(blocks) < 16 and pos + ((size + 7) & ~7) <= len(result), 'Invalid/duplicate state block')
        blocks[tag] = bytes(result[pos:pos + size]); pos += (size + 7) & ~7
        if tag == b'END ': break
    v.require(pos == len(result) and blocks.get(b'END ') == b'' and b'MEM ' in blocks, 'Missing/trailing state blocks')
    return blocks[b'MEM ']


def subsystem_records(core, subsystem):
    v.require(len(core) >= 32 and core[:8] == b'MDFNSVST', 'Expected labeled Mednafen state')
    used = struct.unpack_from('<I', core, 20)[0]
    v.require(32 <= used <= len(core) <= MAX_STATE, 'Invalid core state length')
    sections, pos = {}, 32
    while pos < used:
        v.require(pos + 36 <= used, 'Truncated subsystem header')
        raw_name = core[pos:pos + 32]; name = raw_name.split(b'\0', 1)[0]
        size = struct.unpack_from('<I', core, pos + 32)[0]; pos += 36
        v.require(name and name not in sections and len(sections) < 128 and pos + size <= used, 'Invalid/duplicate subsystem')
        sections[name] = core[pos:pos + size]; pos += size
    v.require(subsystem in sections, 'Missing state subsystem')
    data = sections[subsystem]; records, pos = {}, 0
    while pos < len(data):
        size = data[pos]; pos += 1
        v.require(size and pos + size + 4 <= len(data), 'Invalid subsystem field name')
        name = data[pos:pos + size].decode('ascii'); pos += size
        length = struct.unpack_from('<I', data, pos)[0]; pos += 4
        v.require(name not in records and len(records) < 256 and pos + length <= len(data), 'Invalid/duplicate subsystem field')
        records[name] = data[pos:pos + length]; pos += length
    return records


def gpu_records(core):
    records = subsystem_records(core, b'GPU')
    for key, length in (('dtd', 1), ('DisplayMode', 4), ('DisplayFB_XStart', 4), ('DisplayFB_YStart', 4), ('&GPURAM[0][0]', 1048576)):
        v.require(key in records and len(records[key]) == length, 'Missing/invalid GPU field: ' + key)
    v.require(records['dtd'] in (b'\0', b'\1'), 'Invalid dither flag')
    return records


def vertex_channel(top, bottom, row, height, rounded=True):
    v.integer(top, 0, 255, 'top channel'); v.integer(bottom, 0, 255, 'bottom channel')
    v.integer(height, 1, 256, 'height'); v.integer(row, 0, height - 1, 'row')
    numerator = (bottom - top) * 4096
    slope = (abs(numerator) // height) * (-1 if numerator < 0 else 1)
    return (top * 4096 + (2048 if rounded else 0) + slope * row) >> 12


def shade5(texel, vertex, noise=0):
    v.integer(texel, 0, 31, 'five-bit texel'); v.integer(vertex, 0, 255, 'vertex channel')
    v.integer(noise, -4, 3, 'dither offset')
    return min(31, max(0, (((texel * vertex) >> 4) + noise) >> 3))


def native_bounds(definition, indices, palette, dither=False, replicate=False, rounded=True, *, partial_triplet=False):
    # Reuse bounds validation without reinterpreting original image formats.
    b.transfer_bounds(definition, indices, palette, partial_triplet=partial_triplet)
    words = struct.unpack('<16H', palette)
    low, high = bytearray(256 * 240 * 3), bytearray([31]) * (256 * 240 * 3)
    for p in definition['pieces']:
        for row in range(p['height']):
            for col in range(p['width']):
                word = words[indices[(p['v'] + row) * 256 + p['u'] + col]]
                if not word: continue
                x, y = p['x'] + col, p['y'] + row
                noise = DITHER[y & 3][x & 3] if dither else 0
                for ch, key in enumerate('rgb'):
                    vertex = vertex_channel(p['colors'][0][key], p['colors'][2][key], row, p['height'], rounded)
                    shade = shade5((word >> (ch * 5)) & 31, vertex, noise)
                    at = (y * 256 + x) * 3 + ch
                    for target in (low, high):
                        target[at] = shade if not word & 0x8000 else min(31, target[at] + shade) if p['material'] == 'add' else max(0, target[at] - shade)
    def encode(rgb):
        rgba = bytearray(bytes([0, 0, 0, 255]) * 256 * 240)
        for at in range(256 * 240):
            for ch in range(3):
                q = rgb[at * 3 + ch]; rgba[at * 4 + ch] = (q << 3) | (q >> 2) if replicate else q << 3
        return rgba
    return encode(low), encode(high)


def study(plan):
    inputs, original, managed, definition, _, _ = b.load(plan)
    modern_low, _, _, mask = b.transfer_bounds(definition, inputs['prepared']['indices'], inputs['prepared']['palette'])
    v.require(all(managed[at * 4:at * 4 + 3] == modern_low[at * 3:at * 3 + 3] for at in mask),
              'Managed capture disagrees with the diagnostic mask model')
    records = gpu_records(unpack_state(inputs['original']['state']))
    dtd = records['dtd'] == b'\1'
    mode = int.from_bytes(records['DisplayMode'], 'little')
    x = int.from_bytes(records['DisplayFB_XStart'], 'little'); y = int.from_bytes(records['DisplayFB_YStart'], 'little')
    v.require(mode == 0 and x == 0 and y in (0, 240), 'Unsupported frozen display mode/framebuffer')
    vram = records['&GPURAM[0][0]']
    state_pixels = bytearray(bytes([0, 0, 0, 255]) * 256 * 240)
    for at in mask:
        word = struct.unpack_from('<H', vram, ((y + at // 256) * 1024 + at % 256 + x) * 2)[0]
        for ch in range(3): state_pixels[at * 4 + ch] = ((word >> (5 * ch)) & 31) << 3
    cases = []
    for name, options in (('state-selected-native', dict(dither=dtd)),
                          ('counterfactual-dither-toggle', dict(dither=not dtd)),
                          ('counterfactual-bit-replication', dict(dither=dtd, replicate=True)),
                          ('counterfactual-no-half-unit-color-bias', dict(dither=dtd, rounded=False))):
        lo, hi = native_bounds(definition, inputs['prepared']['indices'], inputs['prepared']['palette'], **options)
        invariant = {at for at in mask if lo[at * 4:at * 4 + 3] == hi[at * 4:at * 4 + 3]}
        if name == 'state-selected-native':
            v.require(invariant == mask, 'State-selected study subset depends on native background')
        cases.append(dict(id=name, options=options, excludedBackgroundSensitivePixels=len(mask - invariant),
                          metrics=b.samples(original, lo, invariant)))
    return dict(format='fft.banner-precision-study', schemaVersion=1, originalComparisonPerformed=True,
        originalFidelityEstablished=False, runtimeRendererChanged=False, renderingPolicySelected=False,
        sourceRevision=b.REVISION, emulatorStudyRevision=EMULATOR_SOURCE, emulatorStudyBuildMatchesInstalledDll=None,
        installedDllSourceRevisionVerified=False,
        frozenGpu=dict(dtd=int(dtd), displayMode=mode, displayFramebuffer=[x, y], vramSha256=v.sha(vram)),
        originalScreenshotAllRgbChannelsMultiplesOfEight=all(original[i] % 8 == 0 for i in range(len(original)) if i % 4 != 3),
        frozenVramVersusRestoredScreenshot=b.samples(original, state_pixels, mask),
        historicalModernPreview=b.samples(original, managed, mask), cases=cases,
        stateSelectedModelMatchesExactly=cases[0]['metrics']['exactEquality'],
        limits=['Selected full-visible glyph subset only; no timing or event-path equivalence.',
                'Saved dtd is snapshot state, not a per-primitive command trace or proof of the writer responsible.',
                '12-bit vertical-gradient reduction is studied only for the selected equal horizontal vertex pairs.',
                'Pinned emulator source is study evidence, not the identified source build of the installed DLL.',
                'Five-bit zero-padding explains this emulator capture encoding, not analog CRT colorimetry.',
                'No general GPU/rasterizer or production precision policy is implemented.'])


def run(plan_path, output):
    from PIL import __version__
    v.require(__version__ == '12.3.0', 'Review Pillow changes explicitly')
    source, destination = v.scratch_file(plan_path), v.scratch_file(output)
    v.require(not destination.exists() and source.stat().st_size <= 65536, 'Existing output or oversized plan')
    raw = source.read_bytes(); plan = v.parse(raw); report = study(plan)
    report.update(plan=plan, planSha256=v.sha(raw), tools=dict(python=sys.version.split()[0], pillow=__version__,
        analyzerSha256=v.sha(Path(__file__).read_bytes()), comparisonSha256=v.sha(Path(b.__file__).read_bytes()), sharedSha256=v.sha(Path(v.__file__).read_bytes())))
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.banner-precision-' + uuid.uuid4().hex); staging.mkdir()
    try:
        (staging / 'report.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists(): (staging / 'report.json').unlink(missing_ok=True); staging.rmdir()
    print('State-selected precision model exact=' + str(report['stateSelectedModelMatchesExactly']) + '; runtime renderer unchanged')
    return 0 if report['stateSelectedModelMatchesExactly'] else 1


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', required=True); parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try: sys.exit(run(args.plan, args.output))
    except (ValueError, OSError, KeyError, TypeError, IndexError, RecursionError, zlib.error) as error:
        parser.exit(2, 'Invalid/unavailable precision evidence: ' + str(error) + '\n')
