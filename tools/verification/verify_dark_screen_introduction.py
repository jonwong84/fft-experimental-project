"""Independent authored combined timeline, pixels, ownership and retained battle audit."""
import argparse
import json
from pathlib import Path
import re

import verify_banner_ready_sequence as sequence
import verify_dark_screen_preview as dark
import verify_intro_battle as battle

v, paths = sequence.v, sequence.paths


def timeline(general, ready, circles):
    # The general task is born at 56; its birth-tick sample is discarded.
    for tick in circles:
        v.integer(tick, 1, 4096, 'Circle tick')
    v.require(len(circles) == len(set(circles)), 'Duplicate Circle tick')
    relative = sequence.timeline(general, ready, [t - 56 for t in circles if t > 56])
    times = {k: None if value is None else value + 56 for k, value in relative.items()}
    times['generalBirth'] = 56
    times['darkRelease'] = times['eventExit']
    times['darkClear'] = times['darkRelease'] + 56
    times['eventExit'] = times['darkRelease'] + 57
    return times


def dark_geometry(definition, tick, release):
    if tick == 0 or tick >= release + 56:
        return ()
    if 55 < tick <= release:
        return (((0, 0), (256, 0), (0, 256), (256, 256)),)
    quads = []
    for tile in reversed(definition['tiles']):
        delay = tile['initialDelay']
        if tick <= 55 and tick <= delay:
            continue
        active = max(0, (tick if tick <= 55 else tick - release) - 1 - delay)
        angle = min(512, active * 64) if tick <= 55 else max(0, 512 - active * 64)
        radius = min(48, active * 4) if tick <= 55 else max(0, 48 - active * 4)
        axes = definition['rotations'][angle // 64]
        ax, ay, bx, by = (dark.source.trunc_div(axes[k] * radius, 16384) for k in ('ax', 'ay', 'bx', 'by'))
        x, y = tile['x'], tile['y']
        quads.append(((x + ax, y + ay), (x + bx, y + by), (x - bx, y - by), (x - ax, y - ay)))
    return tuple(quads)


def camel(value):
    if isinstance(value, dict):
        return {(k.lower() if k.isupper() else k[0].lower() + k[1:]): camel(item) for k, item in value.items()}
    return [camel(item) for item in value] if isinstance(value, list) else value


def display_fields(tick, general_hold, times):
    if tick < 56:
        return dict(stage='dark', general=None, generalReveal=None, generalHold=None, generalHide=None, ready=None)
    relative = tick - 56
    clear, skip = times['generalClear'] - 56, times['generalSkip']
    progress = min(relative, clear - 1 if skip is None else skip - 57)
    general = 'Completed' if tick >= times['generalClear'] else 'ExitYield' if tick == times['generalClear'] - 1 else 'Revealing' if relative < 20 else 'Holding' if relative < 20 + general_hold else 'Hiding'
    ready = None if tick < times['readyBirth'] else 'Completed' if tick >= times['readyChildClear'] else 'ExitYield' if tick >= times['readyRelease'] + 7 else 'FadeOut' if tick >= times['readyRelease'] else 'Holding' if tick >= times['readyBirth'] + 10 else 'FadeIn'
    return dict(stage='general' if ready is None else 'ready', general=general,
                generalReveal=min(20, progress), generalHold=min(general_hold, max(0, progress - 20)),
                generalHide=0 if skip is not None else min(10, max(0, progress - 20 - general_hold)), ready=ready)


def validate_frame(frame, tick, times, general_hold, initial_hash):
    phase = 'Entering' if tick < 55 else 'Holding' if tick < times['darkRelease'] else 'Leaving' if tick < times['darkClear'] else 'Completed'
    kind = 'None' if tick == 0 or tick >= times['darkClear'] else 'Tiles' if tick <= 55 or tick > times['darkRelease'] else 'Flash'
    revision = 0 if tick < 55 else 1 if tick < times['darkRelease'] else 2 if tick < times['darkClear'] else 3
    total = 1 if tick < 56 else 2 if tick < times['readyBirth'] else 3
    live = int(tick < times['darkClear']) + int(56 <= tick < times['generalClear'] or times['readyBirth'] <= tick < times['readyDispatcherClear'])
    v.require(frame['tick'] == tick and frame['file'] == f'intro/tick-{tick:03}.png' and
              frame['dark'] == phase and frame['darkDraw'] == kind and frame['darkRevision'] == revision and
              frame['totalTasks'] == total and frame['liveTasks'] == live and frame['readyScale'] == sequence.scale(tick, times),
              f'Lifecycle/ledger differs at {tick}')
    v.require(all(frame[key] == value for key, value in display_fields(tick, general_hold, times).items()), 'Display fields differ')
    v.require(frame['Exited'] is (tick == times['eventExit']) and frame['battleAvailable'] is (tick == times['eventExit']) and
              frame['journalEntries'] == 0 and frame['battleSha256'] == initial_hash, 'Battle changed during introduction')
    return kind


def verify(capture, prepared, general_root, ready_root, reference):
    receipt = dark.read_json(capture / 'check.json', 4 * 1024 * 1024)
    v.require(receipt['format'] == 'fft.dark-screen-introduction-battle-capture' and receipt['schemaVersion'] == 1, 'Wrong capture format')
    d = receipt['definition']
    v.require(d['Format'] == 'fft.prepared-dark-screen-introduction' and d['SchemaVersion'] == 1 and
              d['Profile'] == 'sample:scene-profile/preloaded-dark-screen-introduction-v1' and d['Provenance'] == 'authored', 'Wrong authored profile')
    times = timeline(d['GeneralHold'], d['ReadyHold'], receipt['circleTicks'])
    manifest = dark.read_json(prepared / 'assets.manifest.json')
    v.require(len(manifest['assets']) == 1, 'Dark package must contain one definition')
    entry = manifest['assets'][0]
    raw = (prepared / entry['relativePath']).read_bytes()
    v.require(entry['relativePath'] == 'dark-screen.json' and len(raw) == entry['byteLength'] <= 65536 and
              v.sha(raw) == entry['sha256'], 'Dark manifest binding differs')
    definition = json.loads(raw, object_pairs_hook=v.unique)
    v.require(camel(receipt['darkDefinition']) == definition and definition['id'] == d['DarkScreenId'] == entry['id'] and
              definition['ownerId'] == d['OwnerId'] and definition['source']['kind'] == 'verified-retail' and
              definition['source']['referenceRevision'] == d['ReferenceRevision'], 'Dark resource receipt differs')
    v.require(max(t['initialDelay'] for t in definition['tiles']) == 41, 'Unsupported delay maximum')
    general, gi, gp = sequence.artwork(general_root, d['GeneralArtworkId'])
    ready, ri, rp = sequence.artwork(ready_root, d['ReadyArtworkId'])
    v.require(general['paletteBank'] == 4 and ready['paletteBank'] == 2 and general['image'] == ready['image'] == 0, 'Wrong artwork policy')
    for flag in ('battleFrozenThroughHandoff', 'resourcesRebuiltDuringIntroduction', 'focusDuringIntroduction',
                 'focusAfterHandoff', 'heldInputBoundaryChecked', 'gpuReadbackPerformed'):
        v.require(receipt[flag] is True, 'Missing boundary check: ' + flag)
    v.require(receipt['transitionSampleForwarded'] is False and receipt['rendererTaskAcknowledgments'] == 0 and
              receipt['availabilityPublications'] == 1 and receipt['availabilityTick'] == times['eventExit'] and
              0 <= receipt['elapsedSeconds'] <= 120, 'Invalid availability/cadence')
    v.require(len(receipt['captures']) == times['eventExit'] + 1, 'Missing accepted frames')
    initial_hash = v.sha(json.dumps(receipt['initialBattle'], separators=(',', ':'), ensure_ascii=True).encode())
    background = dark.background()
    cache = {}
    checked = []
    order_sensitive = False
    relative_times = sequence.timeline(d['GeneralHold'], d['ReadyHold'], [t - 56 for t in receipt['circleTicks'] if t > 56])
    for tick, frame in enumerate(receipt['captures']):
        kind = validate_frame(frame, tick, times, d['GeneralHold'], initial_hash)
        brightness = sequence.scale(tick, times)
        quads = dark_geometry(definition, tick, times['darkRelease'])
        v.require(frame['darkDrawCount'] == (len(quads) if kind == 'Tiles' else 0), 'Dark submission count differs')
        shape = sequence.geometry(tick - 56, d['GeneralHold'], relative_times) if 56 <= tick < times['readyBirth'] else []
        key = (quads, tuple(shape), brightness)
        if key not in cache:
            under = dark.compose(quads, background)
            if tick >= times['readyBirth']:
                pixels = sequence.ready_pixels(under, ready, ri, rp, brightness)
            elif shape:
                projected = dict(general, pieces=[dict(general['pieces'][i], y=general['pieces'][i]['y'] + crop,
                    v=general['pieces'][i]['v'] + crop, height=31 - crop) for i, crop in shape])
                pixels = sequence.bg.compose(under, projected, gi, gp, partial_triplet=True)
                reversed_layers = dark.compose(quads, sequence.bg.compose(background, projected, gi, gp, partial_triplet=True))
                order_sensitive |= pixels != reversed_layers
            else:
                pixels = under
            cache[key] = pixels
        actual = sequence.b.image((capture / frame['file']).read_bytes(), (256, 240))
        v.require(v.sha(actual) == frame['rgbaSha256'] and actual == cache[key], f'Independent combined pixels differ at {tick}')
        checked.append(dict(tick=tick, sha256=frame['rgbaSha256']))
    final = receipt['finalIntroduction']
    v.require(final['BattleAvailable'] is True and final['Introduction']['Event']['Exited'] is True and
              final['Introduction']['Event']['Tasks']['LiveTasks'] == 0, 'Nonterminal final ownership')
    v.require(re.fullmatch('[0-9a-f]{64}', receipt['introductionTraceSha256']) is not None, 'Missing trace hash')
    # Compare the existing battle report and six PNGs directly, preserving its old rules and renderer.
    old = dark.read_json(reference / 'check.json')
    old = old.get('battle', old)
    for field in ('accepted', 'rejected', 'attempts', 'inputs', 'final', 'finalSha256',
                  'headlessMatched', 'replayMatched', 'renderingPreservedState', 'resizeChecked'):
        v.require(receipt['battle'][field] == old[field], 'Existing battle changed: ' + field)
    for name in battle.BATTLE_PNGS:
        v.require((capture / name).read_bytes() == (reference / name).read_bytes(), 'Historical battle PNG changed: ' + name)
    return dict(format='fft.dark-screen-introduction-independent-check', passed=True, timing=times,
                frames=len(checked), comparedPixels=len(checked)*256*240, orderSensitive=order_sensitive,
                unchangedBattlePngs=6, ownershipPublications=1, originalStartupCompared=False, captures=checked)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ('capture', 'dark', 'general', 'ready', 'battle-reference', 'output'):
        parser.add_argument('--' + flag, type=Path, required=True)
    args = parser.parse_args()
    try:
        capture, prepared, general, ready, reference, output = map(paths.scratch_path,
            (args.capture, args.dark, args.general, args.ready, args.battle_reference, args.output))
        v.require(not output.exists(), 'Output exists')
        report = verify(capture, prepared, general, ready, reference)
        output.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
        print(f'PASS: {report["frames"]} full CPU/GPU/model frames and six unchanged battle PNGs')
    except (ValueError, KeyError, OSError, TypeError, IndexError) as error:
        parser.exit(2, str(error) + '\n')
