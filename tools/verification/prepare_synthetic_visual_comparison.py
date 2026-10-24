"""Bind two existing synthetic Windows check receipts into a ZOD-033 comparison plan.

This is an offline diagnostic adapter, not an original capture importer or save/replay codec.
"""
import argparse
import json
import uuid

import compare_visual_checkpoints as v


def canonical_hash(value):
    return v.sha(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode('ascii'))


def turn_projection(turns):
    """Explicit scalar projection; never hash an arbitrary serialized .NET object graph."""
    def unit_id(value):
        return None if value is None else value['Value']
    return dict(format='fft.visual-synthetic-turn-projection', schemaVersion=1,
                ticks=turns['Ticks'], phase=turns['Phase'], activeUnitId=unit_id(turns['ActiveUnitId']),
                rngState=turns['RngState']['Value'], rngDraws=turns['RngState']['DrawCount'],
                units=[dict(id=unit_id(u['Id']), speed=u['Speed'], ct=u['Ct'], tieKey=u['TieKey'], hasTurn=u['HasTurn'],
                            movementTaken=u['MovementTaken'], actionTaken=u['ActionTaken']) for u in turns['Units']])


def capture(root):
    receipt_path = v.scratch_file(root + '/check.json')
    png_path = v.scratch_file(root + '/scene.png')
    v.require(receipt_path.stat().st_size <= 65536 and png_path.stat().st_size <= 32 * 1024 * 1024, 'Capture exceeds budget')
    raw = receipt_path.read_bytes(); receipt = v.parse(raw)
    v.require(receipt['evidence'] == 'synthetic GPU expectation; no original-game comparison' and
              all(receipt[field] is True for field in ('snapshotUnchanged', 'transparentCorner', 'opaquePalette')),
              'Expected successful synthetic Windows host receipt')
    v.require(receipt['frames'] == 120 and all(receipt[field] == expected for field, expected in
              (('clientWidth', 960), ('clientHeight', 640), ('backBufferWidth', 960), ('backBufferHeight', 640))),
              'Only the native 960x640 synthetic checkpoint is supported')
    v.require(receipt['viewport'] == dict(X=0, Y=0, Width=960, Height=640), 'Unexpected synthetic viewport')
    environment = json.dumps({key: receipt[key] for key in ('runtime', 'os', 'adapter')}, sort_keys=True)
    projection = turn_projection(receipt['turns'])
    v.require(projection['ticks'] == 0 and len(projection['units']) == 2, 'Only the initial two-unit synthetic view is supported')
    result = dict(format='fft.visual-capture', schemaVersion=1, kind='managed-capture', width=960, height=640,
                  environment=environment, viewBindingSha256=canonical_hash(dict(host='synthetic-window/v1', viewport=receipt['viewport'],
                      simulationProjection='synthetic-turns-v1', canvas=[960, 640])),
                  checkpoints=[dict(id='synthetic-held-frame-120', tick=120, timingDomain='presentation-frame',
                      simulationSha256=canonical_hash(projection), png=dict(path=root + '/scene.png', sha256=v.sha(png_path.read_bytes())),
                      receipt=dict(path=root + '/check.json', sha256=v.sha(raw)))])
    v.validate_capture(result)
    return result


def run(reference_root, candidate_root, output):
    destination = v.scratch_file(output)
    v.require(not destination.exists(), 'Output already exists; choose a fresh directory')
    a, b = capture(reference_root), capture(candidate_root)
    def region(name, category, rect):
        return dict(id=name, category=category, rect=rect, maxChannelDelta=[0, 0, 0, 0], maxChangedPixels=0,
                    rationale='Same recorded environment and authored static point-sampled scene; exact bytes required. No threshold calibrated from failing output.')
    plan = dict(format='fft.visual-comparison', schemaVersion=1, mode='same-environment', width=960, height=640,
                checkpoints=[dict(id='synthetic-held-frame-120', tick=120, timingDomain='presentation-frame')],
                regions=[region('whole-canvas', 'geometry', [0, 0, 960, 640]),
                         region('first-unit-and-background', 'transparency-order', [456, 228, 48, 72]),
                         region('first-unit-palette', 'palette', [468, 231, 24, 24])])
    v.validate_plan(plan)
    destination.parent.mkdir(parents=True, exist_ok=True)
    staging = destination.parent / ('.visual-plan-' + uuid.uuid4().hex); staging.mkdir()
    try:
        for name, value in (('plan', plan), ('reference', a), ('candidate', b)):
            (staging / (name + '.json')).write_text(json.dumps(value, indent=2) + '\n', encoding='utf-8', newline='\n')
        staging.rename(destination)
    finally:
        if staging.exists():
            for name in ('plan.json', 'reference.json', 'candidate.json'):
                (staging / name).unlink(missing_ok=True)
            staging.rmdir()
    print('Prepared exact synthetic comparison; turn projection only, original comparison not performed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reference-root', required=True)
    parser.add_argument('--candidate-root', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    try:
        run(args.reference_root, args.candidate_root, args.output)
    except (ValueError, OSError, KeyError, TypeError) as error:
        parser.exit(2, 'Invalid/unavailable synthetic capture: ' + str(error) + '\n')
