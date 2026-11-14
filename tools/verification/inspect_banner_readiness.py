"""Inspect hash-bound original banner-loader waits; never emulate loading in the game."""
import argparse
import json
from pathlib import Path

import compare_banner_reveal as reveal


def inspect(plan_path, output):
    r = reveal
    plan_path, output = map(r.profile_tool.scratch_path, (plan_path, output))
    r.v.require(not output.exists(), 'Output already exists')
    r.v.require(plan_path.stat().st_size <= 65536, 'Readiness plan exceeds budget')
    plan = r.v.parse(plan_path.read_bytes())
    r.v.fields(plan, 'format schemaVersion prepared states', 'readiness plan')
    r.v.require(plan['format'] == 'fft.banner-readiness-plan' and type(plan['schemaVersion']) is int and plan['schemaVersion'] == 1,
                'Unsupported readiness plan')
    r.v.fields(plan['prepared'], 'definition indices palette', 'prepared inputs')
    data = {key: r.hide.verified(value) for key, value in plan['prepared'].items()}
    definition = r.v.parse(data['definition'])
    r.b.transfer_bounds(definition, data['indices'], data['palette'])
    packed = bytes(data['indices'][i] | data['indices'][i + 1] << 4 for i in range(0, len(data['indices']), 2))
    r.v.require(type(plan['states']) is list and 5 <= len(plan['states']) <= 16, 'Expected bounded readiness checkpoints')
    observations = []; previous = -1
    for item in plan['states']:
        r.v.fields(item, 'frontendFrames state', 'readiness checkpoint')
        r.v.integer(item['frontendFrames'], 1, 1200, 'frontend budget')
        r.v.require(item['frontendFrames'] > previous, 'Readiness checkpoints must be ordered')
        previous = item['frontendFrames']; state = r.hide.verified(item['state'])
        ram = r.c.read_ram(state)
        if item is plan['states'][-1]:
            context = r.snapshot(state, definition); task = context['task']
            r.v.require(task['logicalTick'] == 0 and task['draws'] == [], 'Expected first pre-reveal yield')
            observation = dict(phase='RevealReadyYield', task=task, frameContext=context)
        else:
            observation = r.loader_snapshot(state)
        gpu = r.p.gpu_records(r.p.unpack_state(state)); vram = gpu['&GPURAM[0][0]']
        texture = b''.join(vram[(row * 1024 + 384) * 2:(row * 1024 + 448) * 2] for row in range(200))
        observations.append(dict(frontendFrames=item['frontendFrames'], state=item['state'], observation=observation,
            frameCounter=r.trace.word(ram, r.hide.FRAME_COUNTER), preparedTextureAlreadyResident=texture == packed,
            preparedPaletteAlreadyResident=vram[r.bg.PALETTE_OFFSET:r.bg.PALETTE_OFFSET + 32] == data['palette']))
    phases = [x['observation']['phase'] for x in observations]
    compact = [phase for i, phase in enumerate(phases) if i == 0 or phase != phases[i - 1]]
    r.v.require(compact == list(r.WAITS.values()) + ['RevealReadyYield'], 'Readiness phase order differs')
    r.v.require(all(b['frameCounter'] > a['frameCounter'] for a, b in zip(observations, observations[1:])), 'Source frame counters are not ordered')
    report = dict(format='fft.banner-readiness-observation', schemaVersion=1, referenceRevision=r.b.REVISION,
        plan=r.c.identity(plan_path), loaderCode=dict(address=r.LOADER_CODE[0], bytes=r.LOADER_CODE[1], sha256=r.LOADER_CODE[2]),
        observations=observations, originalLoadDurationEstablished=False,
        limits=['Selected suspended source waits only; snapshots do not show every retry or define a universal load duration.',
                'Residency is recorded independently: identical texture/palette bytes before upload do not prove readiness.',
                'Reveal logical tick zero starts after the loader returns; managed preview still preloads before scene creation.',
                'No event identity or physical input validation.'])
    output.mkdir(parents=True)
    (output / 'report.json').write_text(json.dumps(report, indent=2) + '\n')
    return report


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', type=Path, required=True); parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    try:
        result = inspect(args.plan, args.output)
        print(f'Validated {len(result["observations"])} source-bound readiness checkpoints; no universal loading duration.')
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(2, 'Readiness inspection failed: ' + str(error) + '\n')
