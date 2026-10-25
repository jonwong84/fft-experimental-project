"""Independent finite authored sequence timeline/pixel audit; no original runtime or emulator."""
import argparse
import json
from pathlib import Path
import struct
import sys

import compare_banner_background as bg
import compare_observed_banner as b
import compare_visual_checkpoints as v
import prepare_retroarch_reference as paths


def timeline(general_hold, ready_hold, circles):
    for hold in (general_hold, ready_hold): v.integer(hold, 0, 255, 'hold')
    v.require(type(circles) is list and len(circles) == len(set(circles)), 'Invalid Circle schedule')
    for tick in circles: v.integer(tick, 1, 4096, 'Circle tick')
    skip = min((t for t in circles if t <= 20 + general_hold), default=None)
    general_clear = general_hold + 31 if skip is None else skip + 1
    birth = general_clear + 1
    release = min((t for t in circles if birth < t <= birth + ready_hold), default=birth + ready_hold)
    return dict(generalSkip=skip, generalClear=general_clear, readyBirth=birth, readyRelease=release,
                readyChildClear=release + 8, readyDispatcherClear=release + 9, eventExit=release + 10)


def geometry(tick, hold, times):
    if tick == 0 or tick >= times['generalClear'] or times['generalSkip'] is not None and tick >= times['generalSkip']: return []
    hide = max(0, tick - 20 - hold)
    if hide: return [(i, min(31, (hide - 1) * 8)) for i in range(6) if (hide - 1) * 8 < 31]
    reveal = min(tick, 20)
    return [(i, max(0, 31 - (reveal - 1 if i < 3 else reveal - 10) * 8))
            for i in range(3 if reveal < 10 else 6) if (reveal - 1 if i < 3 else reveal - 10) > 0]


def scale(tick, times):
    birth, release = times['readyBirth'], times['readyRelease']
    if tick < birth or tick >= release + 7: return None
    return 128 - 12 * (tick - release) if tick >= release else min(128, 48 + 8 * (tick - birth))


def vertex(colors, width, height, x, y):
    vertices = [(0, 0, colors[0]), (width, 0, colors[1]), (0, height, colors[2]), (width, height, colors[3])]
    triangle = vertices[:3] if x * height < width * (height - y) else vertices[1:]
    seed = min(triangle, key=lambda p: (p[0], p[1]))
    a, c, d = sorted(triangle, key=lambda p: p[1])
    def cross(i, j): return (c[i] - a[i]) * (d[j] - c[j]) - (d[i] - c[i]) * (c[j] - a[j])
    def trunc(n, denominator): return (1 if n * denominator >= 0 else -1) * (abs(n) // abs(denominator))
    dx, dy = trunc(cross(2, 1) * 4096, cross(0, 1)), trunc(cross(0, 2) * 4096, cross(0, 1))
    return (((seed[2] * 4096 + 2048) << 12) + (dx << 12) * (x - seed[0]) + (dy << 12) * (y - seed[1])) & 0xffffffff


def ready_pixels(background, definition, indices, palette, brightness):
    if brightness is None: return background
    rgba = bytearray(background); words = struct.unpack('<16H', palette)
    for glyph in reversed(definition['glyphs']):
        colors = [[c[ch] * brightness // 128 for c in glyph['colors']] for ch in 'rgb']
        for y in range(glyph['height']):
            for x in range(glyph['width']):
                word = words[indices[(glyph['v'] + y) * 256 + glyph['u'] + x]]
                if not word: continue
                at = ((glyph['y'] + y) * 256 + glyph['x'] + x) * 4
                for channel in range(3):
                    color = vertex(colors[channel], glyph['width'], glyph['height'], x, y) >> 24
                    front = min(31, ((word >> (5 * channel) & 31) * color) // 128)
                    back = rgba[at + channel] // 8
                    rgba[at + channel] = ((front + back) // 2 if word & 0x8000 else front) * 8
    return bytes(rgba)


def artwork(root, asset_id):
    manifest = v.parse((root / 'assets.manifest.json').read_bytes()); payload = {}
    for entry in manifest['assets']:
        path = paths.scratch_path(root / entry['relativePath']); v.require(path.stat().st_size == entry['byteLength'] <= 65536, 'Prepared size differs')
        data = path.read_bytes(); v.require(v.sha(data) == entry['sha256'] and entry['id'] not in payload, 'Prepared identity differs')
        payload[entry['id']] = data
    definition = v.parse(payload[asset_id]); indices, palette = payload[definition['indicesId']], payload[definition['paletteId']]
    v.require(len(indices) == 51200 and max(indices) < 16 and len(palette) == 32, 'Prepared payload differs')
    return definition, indices, palette


def verify(capture_root, general_root, ready_root):
    receipt_path = capture_root / 'check.json'; v.require(receipt_path.stat().st_size <= 4 * 1024 * 1024, 'Receipt exceeds budget')
    receipt = json.loads(receipt_path.read_bytes(), object_pairs_hook=v.unique)
    v.require(receipt['format'] == 'fft.banner-ready-sequence-capture' and receipt['schemaVersion'] == 1 and receipt['rendererTaskAcknowledgments'] == 0,
              'Unsupported combined capture')
    d = receipt['definition']; times = timeline(d['GeneralHold'], d['ReadyHold'], receipt['circleTicks'])
    v.require(len(receipt['captures']) == times['eventExit'] + 1 and receipt['resourcesRebuiltAtTick5'] is True, 'Incomplete finite capture/resource check')
    general, gi, gp = artwork(general_root, d['GeneralArtworkId']); ready, ri, rp = artwork(ready_root, d['ReadyArtworkId'])
    background = bytearray(bytes([0, 0, 0, 255]) * 256 * 240)
    for y in range(240):
        for x in range(256):
            shade = ((x // 16 + y // 16) % 2) * 12; at = (y * 256 + x) * 4
            background[at:at + 3] = bytes(((base + shade) // 8) * 8 for base in (40, 56, 72))
    background = bytes(background); cache = {}; checked = []
    for tick, frame in enumerate(receipt['captures']):
        v.require(frame['tick'] == tick and frame['file'] == f'tick-{tick:03}.png', 'Capture sequence/path differs')
        stage = 'general' if tick < times['readyBirth'] else 'ready'; brightness = scale(tick, times)
        progress_tick = min(tick, times['generalClear'] - 1 if times['generalSkip'] is None else times['generalSkip'] - 1)
        reveal, hold = min(20, progress_tick), min(d['GeneralHold'], max(0, progress_tick - 20))
        hide = 0 if times['generalSkip'] is not None else min(10, max(0, progress_tick - 20 - d['GeneralHold']))
        general_phase = 'Completed' if tick >= times['generalClear'] else 'ExitYield' if tick == times['generalClear'] - 1 else 'Revealing' if tick < 20 else 'Holding' if tick < 20 + d['GeneralHold'] else 'Hiding'
        ready_phase = None if stage == 'general' else 'Completed' if tick >= times['readyChildClear'] else 'ExitYield' if tick >= times['readyRelease'] + 7 else 'FadeOut' if tick >= times['readyRelease'] else 'Holding' if tick >= times['readyBirth'] + 10 else 'FadeIn'
        live = int(tick < times['generalClear'] or times['readyBirth'] <= tick < times['readyDispatcherClear'])
        v.require(frame['stage'] == stage and frame['totalTasks'] == (1 if stage == 'general' else 2) and frame['readyScale'] == brightness,
                  'Captured stage/task/scale differs from independent timeline')
        v.require((frame['generalReveal'], frame['generalHold'], frame['generalHide'], frame['general'], frame['ready'], frame['liveTasks']) ==
                  (reveal, hold, hide, general_phase, ready_phase, live), 'Captured counters/phases/ledger differ from independent timeline')
        shape = geometry(tick, d['GeneralHold'], times) if stage == 'general' else []
        key = (stage, tuple(shape), brightness)
        if key not in cache:
            if stage == 'ready': cache[key] = ready_pixels(background, ready, ri, rp, brightness)
            elif not shape: cache[key] = background
            else:
                projected = dict(general, pieces=[dict(general['pieces'][i], y=general['pieces'][i]['y'] + crop,
                    v=general['pieces'][i]['v'] + crop, height=31 - crop) for i, crop in shape])
                cache[key] = bg.compose(background, projected, gi, gp, partial_triplet=True)
        path = capture_root / frame['file']; v.require(path.stat().st_size <= 1024 * 1024, 'PNG exceeds budget')
        pixels = b.image(path.read_bytes(), (256, 240)); v.require(v.sha(pixels) == frame['rgbaSha256'] and pixels == cache[key], f'Independent pixels differ at tick {tick}')
        checked.append(dict(tick=tick, sha256=frame['rgbaSha256']))
    final = receipt['finalState']; v.require(final['Event']['Tick'] == times['eventExit'] and final['Event']['Exited'] and
        final['General']['LastProcessedTick'] == times['generalClear'] and final['Ready']['StartTick'] == times['readyBirth'] and
        final['Ready']['ReleaseTick'] == times['readyRelease'] and len(final['Event']['Tasks']['Tasks']) == 2 and
        all(t['Revision'] == 1 and t['Phase'] == 4 for t in final['Event']['Tasks']['Tasks']), 'Final sequence/ledger differs')
    return dict(format='fft.banner-ready-independent-check', passed=True, timing=times, frames=len(checked),
                comparedPixels=len(checked) * 256 * 240, uniqueModelCanvases=len(cache), checks=checked)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('capture', 'general', 'ready', 'output'): parser.add_argument('--' + flag, type=Path, required=True)
    args = parser.parse_args()
    try:
        capture, general, ready, output = map(paths.scratch_path, (args.capture, args.general, args.ready, args.output))
        v.require(not output.exists(), 'Output exists'); result = verify(capture, general, ready)
        output.write_text(json.dumps(result, indent=2) + '\n'); print(f'PASS: {result["frames"]} combined CPU/GPU/model frames')
    except (ValueError, KeyError, OSError, TypeError, IndexError) as error: parser.exit(2, str(error) + '\n')
